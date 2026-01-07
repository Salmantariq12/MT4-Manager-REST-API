using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Runtime.InteropServices;

namespace MT4RestApi.Controllers;

[ApiController]
[Route("api/diagnostic")]
[ApiExplorerSettings(IgnoreApi = true)]  // Hide from Swagger
public class DiagnosticController : ControllerBase
{
    private readonly ILogger<DiagnosticController> _logger;

    public DiagnosticController(ILogger<DiagnosticController> logger)
    {
        _logger = logger;
    }

    [HttpGet("check")]
    public ActionResult<object> CheckSystem()
    {
        var result = new
        {
            Platform = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Framework = RuntimeInformation.FrameworkDescription,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            DllCheck = new
            {
                MtmanapiExists = System.IO.File.Exists("mtmanapi.dll"),
                MtmanapiPath = Path.GetFullPath("mtmanapi.dll"),
                MtmanapiSize = System.IO.File.Exists("mtmanapi.dll") ? 
                    new FileInfo("mtmanapi.dll").Length : 0,
                ExecutableDirectory = AppContext.BaseDirectory,
                DllInExeDirectory = System.IO.File.Exists(Path.Combine(AppContext.BaseDirectory, "mtmanapi.dll"))
            },
            TestDllLoad = TestDllLoad()
        };
        
        return Ok(result);
    }
    
    private object TestDllLoad()
    {
        try
        {
            // Try to load the DLL directly
            var handle = LoadLibrary("mtmanapi.dll");
            if (handle == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                return new 
                { 
                    Success = false, 
                    Error = $"Failed to load DLL. Win32 Error: {errorCode}",
                    ErrorMessage = GetErrorMessage(errorCode)
                };
            }
            
            FreeLibrary(handle);
            return new { Success = true, Message = "DLL loaded successfully" };
        }
        catch (Exception ex)
        {
            return new { Success = false, Error = ex.Message };
        }
    }
    
    private string GetErrorMessage(int errorCode)
    {
        return errorCode switch
        {
            126 => "ERROR_MOD_NOT_FOUND - The specified module could not be found",
            127 => "ERROR_PROC_NOT_FOUND - The specified procedure could not be found",
            193 => "ERROR_BAD_EXE_FORMAT - Not a valid Win32 application (32/64-bit mismatch)",
            _ => $"Unknown error code: {errorCode}"
        };
    }
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string dllToLoad);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);
}