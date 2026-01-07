using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Models;
using MT4RestApi.Services;
using System.Text.Json;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]  // Hide from Swagger
public class LayerController : ControllerBase
{
    private readonly IMT4ManagerService _mt4Service;
    private readonly ILogger<LayerController> _logger;

    public LayerController(IMT4ManagerService mt4Service, ILogger<LayerController> logger)
    {
        _mt4Service = mt4Service;
        _logger = logger;
    }

    /// <summary>
    /// Layer API - Unified endpoint for all MT4 trading operations
    /// </summary>
    /// <param name="request">Layer request with endpoint and data</param>
    /// <returns>Response based on the endpoint called</returns>
    /// <example>
    /// POST /api/layer
    /// {
    ///   "endpoint": "/trade/open",
    ///   "data": {
    ///     "login": 1001,
    ///     "symbol": "EURUSD", 
    ///     "cmd": 0,
    ///     "volume": 0.01,
    ///     "price": 1.0850
    ///   }
    /// }
    /// </example>
    [HttpPost]
    public async Task<IActionResult> ExecuteLayer([FromBody] LayerRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Endpoint))
        {
            return BadRequest(new ApiResponse<object> 
            { 
                Success = false, 
                Message = "Invalid request. Endpoint is required." 
            });
        }

        _logger.LogInformation("Layer API called with endpoint: {Endpoint}", request.Endpoint);

        try
        {
            switch (request.Endpoint.ToLower())
            {
                case "/trade/closeall":
                    return await CloseAllTrades(request.Data);

                case "/trade/close":
                    return await CloseTrade(request.Data);

                case "/trade/open":
                    return await OpenTrade(request.Data);

                case "/symbol/list":
                    return await GetSymbolList();

                default:
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Unknown endpoint: {request.Endpoint}"
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing layer endpoint: {Endpoint}", request.Endpoint);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = $"Internal server error: {ex.Message}"
            });
        }
    }

    private async Task<IActionResult> CloseAllTrades(JsonElement? data)
    {
        try
        {
            int login = 0;
            if (data.HasValue && data.Value.TryGetProperty("login", out var loginElement))
            {
                login = loginElement.GetInt32();
            }

            var result = await _mt4Service.CloseAllTradesAsync(login);
            
            return Ok(new ApiResponse<CloseTradesResult>
            {
                Success = result.Success,
                Message = result.Success ? $"Closed {result.ClosedCount} trades" : result.Message,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing all trades");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = $"Failed to close all trades: {ex.Message}"
            });
        }
    }

    private async Task<IActionResult> CloseTrade(JsonElement? data)
    {
        try
        {
            if (!data.HasValue)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Order number is required"
                });
            }

            if (!data.Value.TryGetProperty("order", out var orderElement))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Order number is required in data"
                });
            }

            int order = orderElement.GetInt32();
            var result = await _mt4Service.CloseTradeAsync(order);

            return Ok(new ApiResponse<bool>
            {
                Success = result,
                Message = result ? "Trade closed successfully" : "Failed to close trade",
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing trade");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = $"Failed to close trade: {ex.Message}"
            });
        }
    }

    private async Task<IActionResult> OpenTrade(JsonElement? data)
    {
        try
        {
            if (!data.HasValue)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Trade data is required"
                });
            }

            var tradeRequest = JsonSerializer.Deserialize<OpenTradeRequest>(data.Value.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tradeRequest == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid trade data"
                });
            }

            var result = await _mt4Service.OpenTradeAsync(tradeRequest);

            return Ok(new ApiResponse<OpenTradeResult>
            {
                Success = result.Success,
                Message = result.Success ? "Trade opened successfully" : result.Message,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening trade");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = $"Failed to open trade: {ex.Message}"
            });
        }
    }

    private async Task<IActionResult> GetSymbolList()
    {
        try
        {
            var symbols = await _mt4Service.GetSymbolsAsync();

            return Ok(new ApiResponse<List<SymbolInfo>>
            {
                Success = true,
                Message = $"Found {symbols.Count} symbols",
                Data = symbols
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting symbol list");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = $"Failed to get symbols: {ex.Message}"
            });
        }
    }
}