using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Models;
using MT4RestApi.Services;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IMT4ManagerService _mt4Service;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IMT4ManagerService mt4Service, ILogger<AccountController> logger)
    {
        _mt4Service = mt4Service;
        _logger = logger;
    }

    /// <summary>
    /// Get account balance information
    /// </summary>
    [HttpGet("{login:int}/balance")]
    public async Task<ActionResult<ApiResponse<BalanceInfo>>> GetBalance(int login)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<BalanceInfo>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving balance for account: {Login}", login);
        
        var balanceInfo = await _mt4Service.GetBalanceInfoAsync(login);
        return Ok(ApiResponse<BalanceInfo>.SuccessResult(balanceInfo));
    }

    /// <summary>
    /// Get account equity
    /// </summary>
    [HttpGet("{login:int}/equity")]
    public async Task<ActionResult<ApiResponse<double>>> GetEquity(int login)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<double>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving equity for account: {Login}", login);
        
        var balanceInfo = await _mt4Service.GetBalanceInfoAsync(login);
        return Ok(ApiResponse<double>.SuccessResult(balanceInfo.Equity));
    }

    /// <summary>
    /// Get account margin
    /// </summary>
    [HttpGet("{login:int}/margin")]
    public async Task<ActionResult<ApiResponse<double>>> GetMargin(int login)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<double>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving margin for account: {Login}", login);
        
        var balanceInfo = await _mt4Service.GetBalanceInfoAsync(login);
        return Ok(ApiResponse<double>.SuccessResult(balanceInfo.Margin));
    }
}