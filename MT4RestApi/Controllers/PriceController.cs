using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Models;
using MT4RestApi.Services;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/price")]
public class PriceController : ControllerBase
{
    private readonly IMT4ManagerService _mt4Service;
    private readonly IPriceWebSocketService _priceWebSocketService;
    private readonly ILogger<PriceController> _logger;

    public PriceController(IMT4ManagerService mt4Service, IPriceWebSocketService priceWebSocketService, ILogger<PriceController> logger)
    {
        _mt4Service = mt4Service;
        _priceWebSocketService = priceWebSocketService;
        _logger = logger;
    }

    /// <summary>
    /// Get real-time price quote for a specific symbol
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., EURUSD, XAUUSD)</param>
    [HttpGet("{symbol}")]
    public async Task<ActionResult<PriceResponse>> GetPrice(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new PriceResponse
            {
                Success = false,
                Message = "Symbol is required"
            });
        }

        _logger.LogInformation("Getting price quote for {Symbol} from WebSocket cache", symbol);

        // Get price from WebSocket cache
        var quote = _priceWebSocketService.GetCachedPrice(symbol);

        if (quote == null)
        {
            // Fallback to MT4 direct connection if WebSocket doesn't have the price
            _logger.LogWarning("No cached price for {Symbol}, attempting MT4 direct connection", symbol);

            if (_mt4Service.IsConnected)
            {
                quote = await _mt4Service.GetQuoteAsync(symbol);
                if (quote != null)
                {
                    quote.CleanSymbol();
                    return Ok(new PriceResponse
                    {
                        Success = true,
                        Message = "Quote retrieved from MT4 (fallback)",
                        Data = quote
                    });
                }
            }

            return NotFound(new PriceResponse
            {
                Success = false,
                Message = $"No price available for {symbol}. Symbol might not be subscribed or invalid."
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
            Message = "Quote retrieved successfully from WebSocket cache",
            Data = quote
        });
    }

    /// <summary>
    /// Get real-time price quotes for multiple symbols
    /// </summary>
    /// <param name="symbols">Comma-separated list of symbols</param>
    [HttpGet("multiple")]
    public async Task<ActionResult<ApiResponse<List<PriceQuote>>>> GetMultiplePrices([FromQuery] string symbols)
    {
        if (string.IsNullOrWhiteSpace(symbols))
        {
            // Return all cached prices if no symbols specified
            var allPrices = _priceWebSocketService.GetAllCachedPrices().Values.ToList();
            if (allPrices.Any())
            {
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
            return BadRequest(ApiResponse<List<PriceQuote>>.ErrorResult("No prices available. WebSocket might not be connected."));
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

        _logger.LogInformation("Getting price quotes for {Count} symbols from WebSocket cache", symbolList.Count);

        var quotes = new List<PriceQuote>();
        var notFoundSymbols = new List<string>();

        foreach (var symbol in symbolList)
        {
            var quote = _priceWebSocketService.GetCachedPrice(symbol);
            if (quote != null)
            {
                quote.CleanSymbol();
                quotes.Add(quote);
            }
            else
            {
                notFoundSymbols.Add(symbol);
            }
        }

        // Fallback to MT4 for symbols not in cache
        if (notFoundSymbols.Any() && _mt4Service.IsConnected)
        {
            _logger.LogInformation("Attempting MT4 fallback for {Count} symbols", notFoundSymbols.Count);
            foreach (var symbol in notFoundSymbols)
            {
                var quote = await _mt4Service.GetQuoteAsync(symbol);
                if (quote != null)
                {
                    quote.CleanSymbol();
                    quotes.Add(quote);
                }
            }
        }

        var message = quotes.Count == symbolList.Count
            ? $"Retrieved all {quotes.Count} requested quotes"
            : $"Retrieved {quotes.Count} out of {symbolList.Count} requested";

        return Ok(new ApiResponse<List<PriceQuote>>
        {
            Success = true,
            Message = message,
            Data = quotes
        });
    }

    /// <summary>
    /// Get current price quote (alternative endpoint)
    /// </summary>
    [HttpPost("quote")]
    public async Task<ActionResult<PriceResponse>> GetQuote([FromBody] PriceRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
        {
            return BadRequest(new PriceResponse
            {
                Success = false,
                Message = "Symbol is required in request body"
            });
        }

        return await GetPrice(request.Symbol);
    }
}