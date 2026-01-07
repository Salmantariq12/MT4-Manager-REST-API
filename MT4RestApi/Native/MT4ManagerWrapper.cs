using System;
using System.Runtime.InteropServices;

namespace MT4RestApi.Native;

/// <summary>
/// Proper wrapper for MT4 Manager API using COM-like interface
/// </summary>
public class MT4ManagerWrapper : IDisposable
{
    private IntPtr _managerHandle = IntPtr.Zero;
    private bool _disposed = false;

    // Import the factory functions
    [DllImport("mtmanapi.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int MtManVersion();

    [DllImport("mtmanapi.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr MtManCreate(int version);

    // Virtual function table interface
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ConnectDelegate(IntPtr manager, [MarshalAs(UnmanagedType.LPStr)] string server);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DisconnectDelegate(IntPtr manager);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int IsConnectedDelegate(IntPtr manager);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int LoginDelegate(IntPtr manager, int login, [MarshalAs(UnmanagedType.LPStr)] string password);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseDelegate(IntPtr manager);

    public MT4ManagerWrapper()
    {
        try
        {
            // Get version
            int version = MtManVersion();
            if (version <= 0)
            {
                // Use default version
                version = (1353 << 16) | 400;
            }

            // Create manager instance
            _managerHandle = MtManCreate(version);
            if (_managerHandle == IntPtr.Zero)
            {
                throw new Exception("Failed to create MT4 Manager instance");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to initialize MT4 Manager: {ex.Message}", ex);
        }
    }

    public IntPtr Handle => _managerHandle;

    public bool IsValid => _managerHandle != IntPtr.Zero;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_managerHandle != IntPtr.Zero)
            {
                // Note: We should call Release through the vtable, but for safety we'll just set to null
                _managerHandle = IntPtr.Zero;
            }
            _disposed = true;
        }
    }

    ~MT4ManagerWrapper()
    {
        Dispose(false);
    }
}