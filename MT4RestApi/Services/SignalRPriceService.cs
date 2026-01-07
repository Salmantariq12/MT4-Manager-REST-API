using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using MT4RestApi.Models;

namespace MT4RestApi.Services;

public class SignalRPriceService : IPriceWebSocketService, IHostedService
{
    private readonly ConcurrentDictionary<string, PriceQuote> _priceCache = new();
    private readonly ILogger<SignalRPriceService> _logger;
    private readonly IConfiguration _configuration;
    private HubConnection? _hubConnection;
    private bool _isConnected = false;

    public bool IsConnected => _isConnected;

    public SignalRPriceService(ILogger<SignalRPriceService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public PriceQuote? GetCachedPrice(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        // Handle both formats: AUDUSD and AUDUSD.r
        var symbolUpper = symbol.ToUpper();

        // Try exact match first
        if (_priceCache.TryGetValue(symbolUpper, out var price))
            return price;

        // Try with .r suffix
        if (_priceCache.TryGetValue($"{symbolUpper}.r", out price))
            return price;

        // Try without .r suffix if input had it
        if (symbolUpper.EndsWith(".R"))
        {
            var symbolWithoutSuffix = symbolUpper.Substring(0, symbolUpper.Length - 2);
            if (_priceCache.TryGetValue(symbolWithoutSuffix, out price))
                return price;
        }

        return null;
    }

    public Dictionary<string, PriceQuote> GetAllCachedPrices()
    {
        return new Dictionary<string, PriceQuote>(_priceCache);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Use the real SignalR hub URL from the PDF
        var hubUrl = _configuration["SignalR:HubUrl"] ?? "http://8.211.192.153:5333/signalr";
        var accountsToSubscribe = _configuration.GetSection("SignalR:Accounts").Get<string[]>() ??
            new[] { "LMC_1100-RS-CK", "LMS2_2200-RS-CK", "LMC_2200-RS-CK" };

        _logger.LogInformation("Starting SignalR connection to {HubUrl}", hubUrl);

        try
        {
            await StartListening(hubUrl, cancellationToken);

            // Subscribe to accounts after connection
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                foreach (var account in accountsToSubscribe)
                {
                    await _hubConnection.InvokeAsync("subscribe", account, cancellationToken);
                    _logger.LogInformation("Subscribed to account: {Account}", account);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping SignalR connection");

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        _isConnected = false;
    }

    public async Task StartListening(string hubUrl, CancellationToken cancellationToken)
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{hubUrl}/PriceBroadcasterHub")
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30) })
                .Build();

            // Handle the NotifyPriceUpdate event
            _hubConnection.On<PriceUpdateMessage>("NotifyPriceUpdate", (priceUpdate) =>
            {
                ProcessPriceUpdate(priceUpdate);
            });

            // Connection event handlers
            _hubConnection.Reconnecting += (error) =>
            {
                _isConnected = false;
                _logger.LogWarning("SignalR connection lost. Reconnecting...");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                _isConnected = true;
                _logger.LogInformation("SignalR reconnected with ID: {ConnectionId}", connectionId);
                return Task.CompletedTask;
            };

            _hubConnection.Closed += (error) =>
            {
                _isConnected = false;
                if (error != null)
                {
                    _logger.LogError(error, "SignalR connection closed with error");
                }
                else
                {
                    _logger.LogInformation("SignalR connection closed");
                }
                return Task.CompletedTask;
            };

            // Start the connection
            await _hubConnection.StartAsync(cancellationToken);
            _isConnected = true;
            _logger.LogInformation("SignalR connected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub");
            throw;
        }
    }

    private void ProcessPriceUpdate(PriceUpdateMessage priceUpdate)
    {
        try
        {
            if (priceUpdate?.Data == null)
                return;

            var data = priceUpdate.Data;

            // Create PriceQuote from the SignalR data
            var priceQuote = new PriceQuote
            {
                Symbol = data.SymbolName ?? "",
                Bid = data.Bid ?? 0,
                Ask = data.Ask ?? 0,
                Spread = Math.Round(((data.Ask ?? 0) - (data.Bid ?? 0)) * GetPointMultiplier(data.SymbolName ?? ""), 2),
                Time = DateTime.UtcNow,
                Timestamp = DateTime.UtcNow,
                High = data.High ?? 0,
                Low = data.Low ?? 0,
                Digits = data.Digits ?? 5
            };

            // Store both with and without .r suffix for easier access
            _priceCache.AddOrUpdate(priceQuote.Symbol, priceQuote, (key, old) => priceQuote);

            // Also store without .r suffix if it exists
            if (priceQuote.Symbol.EndsWith(".r"))
            {
                var symbolWithoutSuffix = priceQuote.Symbol.Substring(0, priceQuote.Symbol.Length - 2);
                _priceCache.AddOrUpdate(symbolWithoutSuffix, priceQuote, (key, old) => priceQuote);
            }

            _logger.LogDebug("Updated price for {Symbol}: Bid={Bid}, Ask={Ask}",
                priceQuote.Symbol, priceQuote.Bid, priceQuote.Ask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process price update");
        }
    }

    private double GetPointMultiplier(string symbol)
    {
        if (symbol.Contains("JPY"))
            return 100;
        if (symbol.Contains("XAU") || symbol.Contains("GOLD"))
            return 10;
        return 10000;
    }

    // Classes to match the SignalR message format
    private class PriceUpdateMessage
    {
        public PriceData? Data { get; set; }
    }

    private class PriceData
    {
        public string? SymbolName { get; set; }
        public double? Bid { get; set; }
        public double? Ask { get; set; }
        public double? High { get; set; }
        public double? Low { get; set; }
        public int? Digits { get; set; }
        public DateTime? Time { get; set; }
    }
}