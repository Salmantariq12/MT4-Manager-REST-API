using Microsoft.AspNetCore.Mvc;
using MT4RestApi.Services;
using System.Runtime.InteropServices;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly IMT4ManagerService _mt4Service;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(IMT4ManagerService mt4Service, ILogger<DiagnosticsController> logger)
    {
        _mt4Service = mt4Service;
        _logger = logger;
    }

    /// <summary>
    /// Get detailed system and service diagnostics
    /// </summary>
    [HttpGet("status")]
    public ActionResult<object> GetDiagnostics()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var exeLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

        var diagnostics = new
        {
            ServiceInitialized = _mt4Service != null,
            MT4Connected = _mt4Service?.IsConnected ?? false,
            LastError = _mt4Service?.GetLastError() ?? "No error available",
            SystemInfo = new
            {
                OSVersion = Environment.OSVersion.ToString(),
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Is64BitProcess = Environment.Is64BitProcess,
                Is64BitOS = Environment.Is64BitOperatingSystem,
                CurrentDirectory = currentDir,
                ExecutableLocation = exeLocation
            },
            FileCheck = new
            {
                MT4WrapperExists = System.IO.File.Exists(Path.Combine(currentDir, "MT4Wrapper.dll")),
                MtmanapiExists = System.IO.File.Exists(Path.Combine(currentDir, "mtmanapi.dll")),
                MT4WrapperPath = Path.Combine(currentDir, "MT4Wrapper.dll"),
                MtmanapiPath = Path.Combine(currentDir, "mtmanapi.dll")
            },
            Environment = new
            {
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                WorkingSet = Environment.WorkingSet,
                ProcessorCount = Environment.ProcessorCount
            }
        };

        return Ok(diagnostics);
    }

    /// <summary>
    /// Test MT4 service initialization
    /// </summary>
    [HttpPost("test-init")]
    public ActionResult<object> TestInitialization()
    {
        try
        {
            var result = new
            {
                ServiceAvailable = _mt4Service != null,
                ServiceType = _mt4Service?.GetType().Name,
                IsConnected = _mt4Service?.IsConnected ?? false,
                LastError = _mt4Service?.GetLastError() ?? "No service available"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Error = ex.Message,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException?.Message
            });
        }
    }
}