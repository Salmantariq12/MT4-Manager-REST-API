using System;
using System.Runtime.InteropServices;

namespace MT4RestApi.Native;

/// <summary>
/// Factory class for creating MT4 Manager instances
/// This properly uses the exported functions from mtmanapi.dll
/// </summary>
public static class MT4ManagerFactory
{
    private const string DLL_NAME = "mtmanapi.dll";
    
    // These are the actual exported functions from the DLL
    // Note: These functions use Cdecl calling convention, not StdCall
    [DllImport(DLL_NAME, EntryPoint = "MtManVersion", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetVersion();
    
    [DllImport(DLL_NAME, EntryPoint = "MtManCreate", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CreateManager(int version);
    
    // Constants from the API
    public const int ManAPIProgramVersion = 400;
    public const int ManAPIProgramBuild = 1353;
    
    public static IntPtr Create()
    {
        try
        {
            // Use the standard MT4 Manager API version
            // Version is constructed as: (build << 16) | version
            int apiVersion = (1353 << 16) | 400;  // Build 1353, Version 400
            return CreateManager(apiVersion);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create manager: {ex.Message}");
            // Try with just version number
            return CreateManager(400);
        }
    }
}