using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Models;
using MT4RestApi.Services;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api")]
public class ConnectionController : ControllerBase
{
    private readonly IMT4ManagerService _mt4Service;
    private readonly ILogger<ConnectionController> _logger;

    public ConnectionController(IMT4ManagerService mt4Service, ILogger<ConnectionController> logger)
    {
        _mt4Service = mt4Service;
        _logger = logger;
    }

    /// <summary>
    /// Connect to MT4 server
    /// </summary>
    [HttpPost("connect")]
    public async Task<ActionResult<ApiResponse>> Connect([FromBody] ConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Server))
        {
            return BadRequest(ApiResponse.ErrorResult("Server address is required"));
        }

        _logger.LogInformation("Attempting to connect to server: {Server}", request.Server);
        
        var success = await _mt4Service.ConnectAsync(request.Server);
        if (success)
        {
            return Ok(ApiResponse.SuccessResult());
        }

        var error = _mt4Service.GetLastError();
        _logger.LogError("Connection failed: {Error}", error);
        return BadRequest(ApiResponse.ErrorResult(error));
    }

    /// <summary>
    /// Login to MT4 server with manager credentials
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse>> Login([FromBody] LoginRequest request)
    {
        if (request.Login <= 0 || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponse.ErrorResult("Login and password are required"));
        }

        _logger.LogInformation("Attempting to login with login: {Login}", request.Login);
        
        var success = await _mt4Service.LoginAsync(request.Login, request.Password);
        if (success)
        {
            return Ok(ApiResponse.SuccessResult());
        }

        var error = _mt4Service.GetLastError();
        _logger.LogError("Login failed: {Error}", error);
        return BadRequest(ApiResponse.ErrorResult(error));
    }

    /// <summary>
    /// Check connection status
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ApiResponse<object>> GetStatus()
    {
        var status = new
        {
            Connected = _mt4Service.IsConnected,
            Timestamp = DateTime.UtcNow,
            LastError = _mt4Service.GetLastError(),
            ManagerInitialized = !string.IsNullOrEmpty(_mt4Service.GetLastError()) ? 
                _mt4Service.GetLastError() : "Manager ready"
        };

        return Ok(ApiResponse<object>.SuccessResult(status));
    }

    /// <summary>
    /// Disconnect from MT4 server
    /// </summary>
    [HttpPost("disconnect")]
    public async Task<ActionResult<ApiResponse>> Disconnect()
    {
        _logger.LogInformation("Disconnecting from MT4 server");
        
        await _mt4Service.DisconnectAsync();
        return Ok(ApiResponse.SuccessResult());
    }
}