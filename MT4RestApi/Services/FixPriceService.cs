using System.Collections.Concurrent;
using MT4RestApi.Models;

namespace MT4RestApi.Services;

/// <summary>
/// FIX Protocol Price Service - Replaces SignalR implementation
/// </summary>
public class FixPriceService : IPriceWebSocketService, IHostedService, IDisposable
{
    private readonly ConcurrentDictionary<string, PriceQuote> _priceCache = new();
    private readonly ILogger<FixPriceService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private FixClient? _fixClient;
    private bool _isConnected = false;
    private bool _disposed = false;
    private Timer? _heartbeatTimer;

    public bool IsConnected => _isConnected && _fixClient != null && _fixClient.IsConnected;

    public FixPriceService(ILogger<FixPriceService> logger, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get FIX configuration from appsettings
            var host = _configuration["MT4Settings:DefaultServer"]?.Split(':')[0] ?? "live.fxcubic.net";
            var portStr = _configuration["MT4Settings:DefaultServer"]?.Split(':')[1] ?? "9120";
            var port = int.Parse(portStr);

            var username = _configuration["FIX:Username"] ?? "AimsTest_q";
            var password = _configuration["FIX:Password"] ?? "fxc";
            var account = _configuration["FIX:Account"] ?? "100860";

            // SenderCompID and TargetCompID from FIX configuration
            var senderCompID = _configuration["FIX:SenderCompID"] ?? "AimsTest_Q";
            var targetCompID = _configuration["FIX:TargetCompID"] ?? "FXC_Q";

            _logger.LogInformation("Starting FIX 4.3 connection to {Host}:{Port}", host, port);
            _logger.LogInformation("SenderCompID: {SenderCompID}, Username: {Username}, Account: {Account}", senderCompID, username, account);

            // Create FIX client
            _fixClient = new FixClient(
                host,
                port,
                senderCompID,
                targetCompID,
                username,
                password,
                _loggerFactory.CreateLogger<FixClient>()
            );

            // Subscribe to market data events
            _logger.LogInformation("Subscribing to market data events");
            _fixClient.OnMarketDataReceived += HandleMarketData;
            _logger.LogInformation("Event handler attached successfully");

            // Connect to FIX server
            var connected = await _fixClient.ConnectAsync();

            if (connected)
            {
                _isConnected = true;
                _logger.LogInformation("FIX connection established successfully");

                // Wait a bit for logon to complete
                await Task.Delay(2000, cancellationToken);

                // Subscribe to market data for common symbols
                var symbols = GetSymbolsToSubscribe();
                _logger.LogInformation("Subscribing to {Count} symbols with account {Account}", symbols.Length, account);

                await _fixClient.SendMarketDataRequestAsync(symbols, account);

                // Start heartbeat timer (send heartbeat every 25 seconds)
                _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(25));
            }
            else
            {
                _logger.LogError("Failed to establish FIX connection");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FIX price service");
        }
    }

    private string[] GetSymbolsToSubscribe()
    {
        // Get symbols from configuration or use defaults
        var symbolsConfig = _configuration.GetSection("FIX:Symbols").Get<string[]>();

        if (symbolsConfig != null && symbolsConfig.Length > 0)
        {
            return symbolsConfig;
        }

        // Default major symbols
        return new[]
        {
            "EURUSD", "GBPUSD", "USDJPY", "USDCHF", "AUDUSD", "USDCAD", "NZDUSD",
            "EURJPY", "GBPJPY", "EURGBP", "AUDJPY",
            "XAUUSD", "XAGUSD", // Gold, Silver
            "US30", "US500", "NAS100", // Indices
            "BTCUSD", "ETHUSD" // Crypto if available
        };
    }

    private void SendHeartbeat(object? state)
    {
        // Heartbeat is automatically handled by the FIX client
        // This is just to keep the service alive
        if (_isConnected && _fixClient != null && !_fixClient.IsConnected)
        {
            _logger.LogWarning("FIX connection lost, attempting reconnect...");
            _isConnected = false;

            // Attempt reconnect
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(5000);
                    await StartAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconnect FIX client");
                }
            });
        }
    }

    private void HandleMarketData(string rawMessage)
    {
        _logger.LogInformation("HandleMarketData called - Processing message");

        try
        {
            // Parse FIX MarketDataSnapshotFullRefresh message (35=W)
            // Field 55 = Symbol
            // Field 268 = NoMDEntries (number of entries)
            // Repeating group:
            //   Field 269 = MDEntryType (0=Bid, 1=Offer/Ask, 2=Trade, etc.)
            //   Field 270 = MDEntryPx (Price)
            //   Field 271 = MDEntrySize (Size)

            // Split by SOH character to get fields
            var fields = rawMessage.Split('\x01', StringSplitOptions.RemoveEmptyEntries);
            _logger.LogDebug("Split message into {Count} fields", fields.Length);

            string? symbol = null;
            double bid = 0, ask = 0;
            int digits = 5;

            // Parse fields sequentially to handle repeating groups
            string? currentEntryType = null;

            foreach (var field in fields)
            {
                var parts = field.Split('=', 2);
                if (parts.Length != 2) continue;

                var tag = parts[0];
                var value = parts[1];

                switch (tag)
                {
                    case "55": // Symbol
                        symbol = value;
                        break;

                    case "269": // MDEntryType - start of new entry
                        currentEntryType = value;
                        break;

                    case "270": // MDEntryPx - Price
                        if (currentEntryType == "0") // Bid
                        {
                            double.TryParse(value, out bid);
                        }
                        else if (currentEntryType == "1") // Ask
                        {
                            double.TryParse(value, out ask);
                        }
                        currentEntryType = null; // Reset after parsing price
                        break;
                }
            }

            // If we have symbol and both bid and ask, update cache
            if (!string.IsNullOrEmpty(symbol) && bid > 0 && ask > 0)
            {
                var priceQuote = new PriceQuote
                {
                    Symbol = symbol,
                    Bid = bid,
                    Ask = ask,
                    Spread = Math.Round((ask - bid) * GetPointMultiplier(symbol), 2),
                    Time = DateTime.UtcNow,
                    Timestamp = DateTime.UtcNow,
                    Digits = digits,
                    High = ask, // Can be updated if available in FIX message
                    Low = bid   // Can be updated if available in FIX message
                };

                // Store in cache
                _priceCache.AddOrUpdate(symbol.ToUpper(), priceQuote, (key, old) => priceQuote);

                // Also store without suffix if it has one
                if (symbol.EndsWith(".r", StringComparison.OrdinalIgnoreCase))
                {
                    var symbolWithoutSuffix = symbol.Substring(0, symbol.Length - 2);
                    _priceCache.AddOrUpdate(symbolWithoutSuffix.ToUpper(), priceQuote, (key, old) => priceQuote);
                }

                _logger.LogDebug("Cached price for {Symbol}: Bid={Bid}, Ask={Ask}, Spread={Spread}",
                    symbol, bid, ask, priceQuote.Spread);

                // Log major symbols
                if (symbol.Contains("XAUUSD", StringComparison.OrdinalIgnoreCase) ||
                    symbol.Contains("EURUSD", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("FIX Price Update - {Symbol}: Bid={Bid}, Ask={Ask}, Spread={Spread}",
                        symbol, bid, ask, priceQuote.Spread);
                }
            }
            else
            {
                _logger.LogWarning("Incomplete market data: Symbol={Symbol}, Bid={Bid}, Ask={Ask}",
                    symbol ?? "null", bid, ask);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling market data");
        }
    }

    private double GetPointMultiplier(string symbol)
    {
        if (symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase))
            return 100;
        if (symbol.Contains("XAU", StringComparison.OrdinalIgnoreCase) ||
            symbol.Contains("GOLD", StringComparison.OrdinalIgnoreCase))
            return 10;
        return 10000;
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

    public async Task StartListening(string url, CancellationToken cancellationToken)
    {
        // Not used for FIX implementation - connection is established in StartAsync
        // This method is required by IPriceWebSocketService interface for compatibility
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping FIX price service");

        _heartbeatTimer?.Dispose();

        if (_fixClient != null)
        {
            await _fixClient.DisconnectAsync();
            _fixClient.Dispose();
        }

        _isConnected = false;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _heartbeatTimer?.Dispose();
        _fixClient?.Dispose();
        _disposed = true;
    }
}
