using System.Collections.Concurrent;
using Microsoft.AspNet.SignalR.Client;
using MT4RestApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MT4RestApi.Services;

public class OldSignalRPriceService : IPriceWebSocketService, IHostedService, IDisposable
{
    private readonly ConcurrentDictionary<string, PriceQuote> _priceCache = new();
    private readonly ILogger<OldSignalRPriceService> _logger;
    private readonly IConfiguration _configuration;
    private HubConnection? _hubConnection;
    private IHubProxy? _hubProxy;
    private bool _isConnected = false;
    private bool _disposed = false;

    public bool IsConnected => _isConnected;

    public OldSignalRPriceService(ILogger<OldSignalRPriceService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public PriceQuote? GetCachedPrice(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

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

        _logger.LogInformation("Starting OLD SignalR connection to {HubUrl}", hubUrl);

        try
        {
            await StartListening(hubUrl, cancellationToken);

            // Subscribe to accounts after connection
            if (_isConnected && _hubProxy != null)
            {
                foreach (var account in accountsToSubscribe)
                {
                    try
                    {
                        await _hubProxy.Invoke("subscribe", account);
                        _logger.LogInformation("Subscribed to MT4 account: {Account}", account);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to subscribe to account {Account}", account);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OLD SignalR connection");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OLD SignalR connection");

        if (_hubConnection != null)
        {
            _hubConnection.Stop();
            _hubConnection.Dispose();
        }

        _isConnected = false;
    }

    public async Task StartListening(string hubUrl, CancellationToken cancellationToken)
    {
        try
        {
            // Create connection using old SignalR client
            _hubConnection = new HubConnection(hubUrl);

            // Create hub proxy
            _hubProxy = _hubConnection.CreateHubProxy("PriceBroadcasterHub");

            // Subscribe to the NotifyPriceUpdate event
            _hubProxy.On<JObject>("NotifyPriceUpdate", (priceUpdate) =>
            {
                try
                {
                    ProcessPriceUpdate(priceUpdate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing price update");
                }
            });

            // Connection event handlers
            _hubConnection.StateChanged += (change) =>
            {
                _logger.LogInformation("SignalR state changed from {OldState} to {NewState}",
                    change.OldState, change.NewState);

                if (change.NewState == ConnectionState.Connected)
                {
                    _isConnected = true;
                    _logger.LogInformation("OLD SignalR connected successfully");
                }
                else if (change.NewState == ConnectionState.Disconnected)
                {
                    _isConnected = false;
                    _logger.LogWarning("OLD SignalR disconnected");

                    // Try to reconnect
                    Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        if (!_disposed && _hubConnection.State == ConnectionState.Disconnected)
                        {
                            try
                            {
                                await _hubConnection.Start();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to reconnect");
                            }
                        }
                    });
                }
            };

            _hubConnection.Error += (error) =>
            {
                _logger.LogError(error, "SignalR connection error");
            };

            // Start the connection
            _logger.LogInformation("Connecting to OLD SignalR hub at {HubUrl}", hubUrl);
            await _hubConnection.Start();

            _logger.LogInformation("OLD SignalR connection established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to OLD SignalR hub");
            throw;
        }
    }

    private void ProcessPriceUpdate(JObject priceUpdate)
    {
        try
        {
            // Parse the Data property
            var data = priceUpdate["Data"];
            if (data == null)
            {
                _logger.LogWarning("Received price update without Data property");
                return;
            }

            // Extract price information matching the format from the PDF
            var symbolName = data["SymbolName"]?.ToString();
            if (string.IsNullOrEmpty(symbolName))
            {
                _logger.LogWarning("Received price update without SymbolName");
                return;
            }

            var bid = data["Bid"]?.Value<double>() ?? 0;
            var ask = data["Ask"]?.Value<double>() ?? 0;
            var high = data["High"]?.Value<double>() ?? 0;
            var low = data["Low"]?.Value<double>() ?? 0;
            var digits = data["Digits"]?.Value<int>() ?? 5;

            // Create PriceQuote
            var priceQuote = new PriceQuote
            {
                Symbol = symbolName,
                Bid = bid,
                Ask = ask,
                Spread = Math.Round((ask - bid) * GetPointMultiplier(symbolName), 2),
                Time = DateTime.UtcNow,
                Timestamp = DateTime.UtcNow,
                High = high,
                Low = low,
                Digits = digits
            };

            // Store in cache
            _priceCache.AddOrUpdate(priceQuote.Symbol, priceQuote, (key, old) => priceQuote);

            // Also store without .r suffix if it exists for easier access
            if (priceQuote.Symbol.EndsWith(".r"))
            {
                var symbolWithoutSuffix = priceQuote.Symbol.Substring(0, priceQuote.Symbol.Length - 2);
                _priceCache.AddOrUpdate(symbolWithoutSuffix, priceQuote, (key, old) => priceQuote);
            }

            _logger.LogDebug("Updated price for {Symbol}: Bid={Bid}, Ask={Ask}",
                priceQuote.Symbol, priceQuote.Bid, priceQuote.Ask);

            // Log specific symbols like in the PDF example
            if (symbolName == "XAUUSD.r" || symbolName == "AUDUSD.r")
            {
                _logger.LogInformation("MT4 Price Update - {Symbol}: Bid={Bid}, Ask={Ask}, Spread={Spread}",
                    symbolName, bid, ask, priceQuote.Spread);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process price update: {Json}",
                priceUpdate?.ToString(Formatting.None));
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

    public void Dispose()
    {
        _disposed = true;
        _hubConnection?.Stop();
        _hubConnection?.Dispose();
    }
}