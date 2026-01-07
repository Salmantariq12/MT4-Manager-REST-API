using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Models;
using MT4RestApi.Services;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/trades")]
public class TradesController : ControllerBase
{
    private readonly IMT4ManagerService _mt4Service;
    private readonly ILogger<TradesController> _logger;

    public TradesController(IMT4ManagerService mt4Service, ILogger<TradesController> logger)
    {
        _mt4Service = mt4Service;
        _logger = logger;
    }

    /// <summary>
    /// Get all trades from MT4 server
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TradeRecord>>>> GetTrades([FromQuery] bool openOnly = false)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<List<TradeRecord>>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving trades (openOnly: {OpenOnly})", openOnly);
        
        var trades = await _mt4Service.GetTradesAsync(0, openOnly);
        return Ok(ApiResponse<List<TradeRecord>>.SuccessResult(trades));
    }

    /// <summary>
    /// Get trades for specific user
    /// </summary>
    [HttpGet("user/{login:int}")]
    public async Task<ActionResult<ApiResponse<List<TradeRecord>>>> GetUserTrades(int login, [FromQuery] bool openOnly = false)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<List<TradeRecord>>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving trades for user: {Login} (openOnly: {OpenOnly})", login, openOnly);
        
        var trades = await _mt4Service.GetTradesAsync(login, openOnly);
        return Ok(ApiResponse<List<TradeRecord>>.SuccessResult(trades));
    }

    /// <summary>
    /// Get specific trade by order number
    /// </summary>
    [HttpGet("{order:int}")]
    public async Task<ActionResult<ApiResponse<TradeRecord>>> GetTrade(int order)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<TradeRecord>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving trade: {Order}", order);
        
        var trade = await _mt4Service.GetTradeAsync(order);
        if (trade == null)
        {
            return NotFound(ApiResponse<TradeRecord>.ErrorResult("Trade not found"));
        }

        return Ok(ApiResponse<TradeRecord>.SuccessResult(trade));
    }

    /// <summary>
    /// Open a new trade
    /// </summary>
    [HttpPost("open")]
    public async Task<ActionResult<ApiResponse<OpenTradeResult>>> OpenTrade([FromBody] OpenTradeRequest request)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<OpenTradeResult>.ErrorResult("Not connected to MT4 server"));
        }

        if (request == null)
        {
            return BadRequest(ApiResponse<OpenTradeResult>.ErrorResult("Trade data is required"));
        }

        _logger.LogInformation("Opening trade for login: {Login}, symbol: {Symbol}, cmd: {Cmd}, volume: {Volume}", 
            request.Login, request.Symbol, request.Cmd, request.Volume);

        var result = await _mt4Service.OpenTradeAsync(request);

        if (result.Success)
        {
            return Ok(ApiResponse<OpenTradeResult>.SuccessResult(result));
        }

        return BadRequest(new ApiResponse<OpenTradeResult>
        {
            Success = false,
            Message = result.Message,
            Data = result
        });
    }

    /// <summary>
    /// Close a specific trade by order number
    /// </summary>
    [HttpPost("close/{order:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> CloseTrade(int order)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<bool>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Closing trade: {Order}", order);

        var result = await _mt4Service.CloseTradeAsync(order);

        if (result)
        {
            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Trade closed successfully",
                Data = true
            });
        }

        return BadRequest(new ApiResponse<bool>
        {
            Success = false,
            Message = _mt4Service.GetLastError(),
            Data = false
        });
    }

    /// <summary>
    /// Close all trades (optionally for a specific user)
    /// </summary>
    [HttpPost("closeall")]
    public async Task<ActionResult<ApiResponse<CloseTradesResult>>> CloseAllTrades([FromBody] CloseAllTradesRequest? request = null)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<CloseTradesResult>.ErrorResult("Not connected to MT4 server"));
        }

        int login = request?.Login ?? 0;
        _logger.LogInformation("Closing all trades for login: {Login}", login == 0 ? "all users" : login.ToString());

        var result = await _mt4Service.CloseAllTradesAsync(login);

        if (result.Success)
        {
            return Ok(ApiResponse<CloseTradesResult>.SuccessResult(result));
        }

        return BadRequest(new ApiResponse<CloseTradesResult>
        {
            Success = false,
            Message = result.Message,
            Data = result
        });
    }
}