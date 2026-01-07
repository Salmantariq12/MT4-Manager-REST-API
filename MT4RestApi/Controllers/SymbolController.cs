using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Models;
using MT4RestApi.Services;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/symbol")]
public class SymbolController : ControllerBase
{
    private readonly IMT4ManagerService _mt4Service;
    private readonly ILogger<SymbolController> _logger;

    public SymbolController(IMT4ManagerService mt4Service, ILogger<SymbolController> logger)
    {
        _mt4Service = mt4Service;
        _logger = logger;
    }

    /// <summary>
    /// Get list of all available trading symbols
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<List<SymbolInfo>>>> GetSymbols()
    {
        if (!_mt4Service.IsConnected)
        {
            return BadRequest(ApiResponse<List<SymbolInfo>>.ErrorResult("Not connected to MT4 server"));
        }

        _logger.LogInformation("Retrieving symbol list");

        var symbols = await _mt4Service.GetSymbolsAsync();

        return Ok(new ApiResponse<List<SymbolInfo>>
        {
            Success = true,
            Message = $"Found {symbols.Count} symbols",
            Data = symbols
        });
    }

    /// <summary>
    /// Get all symbols (alternative endpoint)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SymbolInfo>>>> GetAllSymbols()
    {
        return await GetSymbols();
    }
}