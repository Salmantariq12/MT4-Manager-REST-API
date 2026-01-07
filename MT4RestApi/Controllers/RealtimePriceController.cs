using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Models;
using MT4RestApi.Services;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/realtime")]
public class RealtimePriceController : ControllerBase
{
    private readonly IPriceWebSocketService _priceService;
    private readonly ILogger<RealtimePriceController> _logger;

    public RealtimePriceController(IPriceWebSocketService priceService, ILogger<RealtimePriceController> logger)
    {
        _priceService = priceService;
        _logger = logger;
    }

    /// <summary>
    /// Get real-time price quote from WebSocket cache
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., AUDUSD, EURUSD, XAUUSD)</param>
    [HttpGet("price/{symbol}")]
    public ActionResult<PriceResponse> GetRealtimePrice(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new PriceResponse
            {
                Success = false,
                Message = "Symbol is required"
            });
        }

        if (!_priceService.IsConnected)
        {
            return ServiceUnavailable(new PriceResponse
            {
                Success = false,
                Message = "WebSocket service is not connected. Prices may not be available."
            });
        }

        _logger.LogInformation("Getting cached real-time price for {Symbol}", symbol);

        var quote = _priceService.GetCachedPrice(symbol);

        if (quote == null)
        {
            return NotFound(new PriceResponse
            {
                Success = false,
                Message = $"No cached price available for {symbol}. Symbol might not be subscribed or invalid."
            });
        }

        // Check if price is stale (older than 30 seconds)
        var age = DateTime.UtcNow - quote.Time;
        if (age.TotalSeconds > 30)
        {
            _logger.LogWarning("Price for {Symbol} is stale (age: {Age} seconds)", symbol, age.TotalSeconds);
        }

        quote.CleanSymbol();
        return Ok(new PriceResponse
        {
            Success = true,
            Message = "Real-time quote retrieved successfully from cache",
            Data = quote
        });
    }

    /// <summary>
    /// Get real-time quotes for multiple symbols
    /// </summary>
    /// <param name="symbols">Comma-separated list of symbols (e.g., AUDUSD,EURUSD,GBPUSD)</param>
    [HttpGet("prices")]
    public ActionResult<ApiResponse<List<PriceQuote>>> GetMultipleRealtimePrices([FromQuery] string symbols)
    {
        if (string.IsNullOrWhiteSpace(symbols))
        {
            // Return all cached prices if no symbols specified
            var allPrices = _priceService.GetAllCachedPrices().Values.ToList();
            // Clean all symbols
            foreach (var price in allPrices)
            {
                price.CleanSymbol();
            }
            return Ok(new ApiResponse<List<PriceQuote>>
            {
                Success = true,
                Message = $"Retrieved all {allPrices.Count} cached prices",
                Data = allPrices
            });
        }

        var symbolList = symbols.Split(',')
            .Select(s => s.Trim().ToUpper())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        if (!symbolList.Any())
        {
            return BadRequest(ApiResponse<List<PriceQuote>>.ErrorResult("No valid symbols provided"));
        }

        _logger.LogInformation("Getting real-time prices for {Count} symbols", symbolList.Count);

        var quotes = new List<PriceQuote>();
        var notFound = new List<string>();

        foreach (var symbol in symbolList)
        {
            var quote = _priceService.GetCachedPrice(symbol);
            if (quote != null)
            {
                quote.CleanSymbol();
                quotes.Add(quote);
            }
            else
            {
                notFound.Add(symbol);
            }
        }

        var message = quotes.Count == symbolList.Count
            ? $"Retrieved all {quotes.Count} requested quotes"
            : $"Retrieved {quotes.Count} out of {symbolList.Count} requested. Not found: {string.Join(", ", notFound)}";

        return Ok(new ApiResponse<List<PriceQuote>>
        {
            Success = true,
            Message = message,
            Data = quotes
        });
    }

    /// <summary>
    /// Get quote for specific symbol (POST method)
    /// </summary>
    [HttpPost("quote")]
    public ActionResult<PriceResponse> GetQuote([FromBody] PriceRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
        {
            return BadRequest(new PriceResponse
            {
                Success = false,
                Message = "Symbol is required in request body"
            });
        }

        return GetRealtimePrice(request.Symbol);
    }

    /// <summary>
    /// Get all available cached prices
    /// </summary>
    [HttpGet("all")]
    public ActionResult<ApiResponse<Dictionary<string, PriceQuote>>> GetAllPrices()
    {
        var allPrices = _priceService.GetAllCachedPrices();

        // Clean all symbols
        foreach (var price in allPrices.Values)
        {
            price.CleanSymbol();
        }

        return Ok(new ApiResponse<Dictionary<string, PriceQuote>>
        {
            Success = true,
            Message = $"Retrieved {allPrices.Count} cached prices",
            Data = allPrices
        });
    }

    /// <summary>
    /// Get WebSocket connection status
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ApiResponse<object>> GetStatus()
    {
        var allPrices = _priceService.GetAllCachedPrices();
        var status = new
        {
            Connected = _priceService.IsConnected,
            CachedSymbolsCount = allPrices.Count,
            CachedSymbols = allPrices.Keys.OrderBy(k => k).ToList(),
            Timestamp = DateTime.UtcNow
        };

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = _priceService.IsConnected ? "WebSocket connected" : "WebSocket disconnected",
            Data = status
        });
    }

    private ObjectResult ServiceUnavailable(object value)
    {
        return StatusCode(503, value);
    }
}