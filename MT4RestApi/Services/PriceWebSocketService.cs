using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MT4RestApi.Models;

namespace MT4RestApi.Services;

public interface IPriceWebSocketService
{
    PriceQuote? GetCachedPrice(string symbol);
    Dictionary<string, PriceQuote> GetAllCachedPrices();
    Task StartListening(string webSocketUrl, CancellationToken cancellationToken);
    bool IsConnected { get; }
}

public class PriceWebSocketService : IPriceWebSocketService, IHostedService
{
    private readonly ConcurrentDictionary<string, PriceQuote> _priceCache = new();
    private readonly ILogger<PriceWebSocketService> _logger;
    private readonly IConfiguration _configuration;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isConnected = false;

    public bool IsConnected => _isConnected;

    public PriceWebSocketService(ILogger<PriceWebSocketService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public PriceQuote? GetCachedPrice(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        return _priceCache.TryGetValue(symbol.ToUpper(), out var price) ? price : null;
    }

    public Dictionary<string, PriceQuote> GetAllCachedPrices()
    {
        return new Dictionary<string, PriceQuote>(_priceCache);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var webSocketUrl = _configuration["WebSocket:PriceServerUrl"] ?? "ws://localhost:8080/prices";

        _logger.LogInformation("Starting WebSocket price listener at {Url}", webSocketUrl);

        _ = Task.Run(async () => await StartListening(webSocketUrl, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping WebSocket price listener");

        _cancellationTokenSource?.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopping", cancellationToken);
        }

        _webSocket?.Dispose();
        _isConnected = false;
    }

    public async Task StartListening(string webSocketUrl, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using (_webSocket = new ClientWebSocket())
                {
                    await ConnectAsync(webSocketUrl, cancellationToken);

                    if (_webSocket.State == WebSocketState.Open)
                    {
                        _isConnected = true;
                        await ReceiveLoop(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _logger.LogError(ex, "WebSocket connection error. Retrying in 5 seconds...");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task ConnectAsync(string webSocketUrl, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Connecting to WebSocket at {Url}", webSocketUrl);
            await _webSocket!.ConnectAsync(new Uri(webSocketUrl), cancellationToken);
            _logger.LogInformation("WebSocket connected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to WebSocket");
            throw;
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[4096]);

        while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                    ProcessPriceMessage(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("WebSocket closed by server");
                    _isConnected = false;
                    break;
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket receive error");
                _isConnected = false;
                break;
            }
        }
    }

    private void ProcessPriceMessage(string message)
    {
        try
        {
            // Expected message format: {"symbol":"AUDUSD","bid":0.6543,"ask":0.6545,"time":"2024-01-23T10:30:00"}
            var priceData = JsonSerializer.Deserialize<PriceWebSocketMessage>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (priceData != null && !string.IsNullOrWhiteSpace(priceData.Symbol))
            {
                var priceQuote = new PriceQuote
                {
                    Symbol = priceData.Symbol.ToUpper(),
                    Bid = priceData.Bid,
                    Ask = priceData.Ask,
                    Time = priceData.Time ?? DateTime.UtcNow,
                    Spread = Math.Round((priceData.Ask - priceData.Bid) * GetPointMultiplier(priceData.Symbol), 2),
                    High = priceData.High ?? 0,
                    Low = priceData.Low ?? 0
                };

                _priceCache.AddOrUpdate(priceQuote.Symbol, priceQuote, (key, old) => priceQuote);

                _logger.LogDebug("Updated price for {Symbol}: Bid={Bid}, Ask={Ask}",
                    priceQuote.Symbol, priceQuote.Bid, priceQuote.Ask);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse price message: {Message}", message);
        }
    }

    private double GetPointMultiplier(string symbol)
    {
        // For forex pairs with JPY, use 100, for others use 10000
        if (symbol.Contains("JPY"))
            return 100;
        return 10000;
    }

    private class PriceWebSocketMessage
    {
        public string Symbol { get; set; } = string.Empty;
        public double Bid { get; set; }
        public double Ask { get; set; }
        public DateTime? Time { get; set; }
        public double? High { get; set; }
        public double? Low { get; set; }
    }
}