using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Models;
using MT4RestApi.Services;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IMT4ManagerService _mt4Service;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IMT4ManagerService mt4Service, ILogger<UsersController> logger)
    {
        _mt4Service = mt4Service;
        _logger = logger;
    }

    /// <summary>
    /// Get all users from MT4 server
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UserRecord>>>> GetUsers()
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<List<UserRecord>>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving all users");
        
        var users = await _mt4Service.GetUsersAsync();
        return Ok(ApiResponse<List<UserRecord>>.SuccessResult(users));
    }

    /// <summary>
    /// Get specific user by login
    /// </summary>
    [HttpGet("{login:int}")]
    public async Task<ActionResult<ApiResponse<UserRecord>>> GetUser(int login)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<UserRecord>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving user: {Login}", login);
        
        var user = await _mt4Service.GetUserAsync(login);
        if (user == null)
        {
            return NotFound(ApiResponse<UserRecord>.ErrorResult("User not found"));
        }

        return Ok(ApiResponse<UserRecord>.SuccessResult(user));
    }

    /// <summary>
    /// Create new user
    /// </summary>
    [HttpPost]
    [ApiExplorerSettings(IgnoreApi = true)]  // Hide from Swagger
    public async Task<ActionResult<ApiResponse>> CreateUser([FromBody] UserRecord user)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse.ErrorResult("Not connected to MT4 server"));
        }

        if (user.Login <= 0)
        {
            return BadRequest(ApiResponse.ErrorResult("Valid login is required"));
        }

        _logger.LogInformation("Creating user: {Login}", user.Login);
        
        var success = await _mt4Service.CreateUserAsync(user);
        if (success)
        {
            return Ok(ApiResponse.SuccessResult());
        }

        var error = _mt4Service.GetLastError();
        return BadRequest(ApiResponse.ErrorResult(error));
    }

    /// <summary>
    /// Update existing user
    /// </summary>
    [HttpPut("{login:int}")]
    [ApiExplorerSettings(IgnoreApi = true)]  // Hide from Swagger
    public async Task<ActionResult<ApiResponse>> UpdateUser(int login, [FromBody] UserRecord user)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse.ErrorResult("Not connected to MT4 server"));
        }

        // Set the login from URL to ensure consistency
        user.Login = login;

        _logger.LogInformation("Updating user: {Login}", login);
        
        var success = await _mt4Service.UpdateUserAsync(user);
        if (success)
        {
            return Ok(ApiResponse.SuccessResult());
        }

        var error = _mt4Service.GetLastError();
        return BadRequest(ApiResponse.ErrorResult(error));
    }

    /// <summary>
    /// Delete user
    /// </summary>
    [HttpDelete("{login:int}")]
    [ApiExplorerSettings(IgnoreApi = true)]  // Hide from Swagger
    public async Task<ActionResult<ApiResponse>> DeleteUser(int login)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Deleting user: {Login}", login);
        
        var success = await _mt4Service.DeleteUserAsync(login);
        if (success)
        {
            return Ok(ApiResponse.SuccessResult());
        }

        var error = _mt4Service.GetLastError();
        return BadRequest(ApiResponse.ErrorResult(error));
    }

    /// <summary>
    /// Get user balance information
    /// </summary>
    [HttpGet("{login:int}/balance")]
    public async Task<ActionResult<ApiResponse<BalanceInfo>>> GetUserBalance(int login)
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<BalanceInfo>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving balance for user: {Login}", login);
        
        var balanceInfo = await _mt4Service.GetBalanceInfoAsync(login);
        return Ok(ApiResponse<BalanceInfo>.SuccessResult(balanceInfo));
    }
}