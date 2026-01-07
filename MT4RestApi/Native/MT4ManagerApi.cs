using System.Runtime.InteropServices;
using MT4RestApi.Models;

namespace MT4RestApi.Native;

public static class MT4ManagerApi
{
    private const string DLL_NAME = "mtmanapi.dll";
    
    // Factory Methods - These are the actual exported functions
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MtManCreate")]
    public static extern IntPtr ManCreate(int version);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MtManVersion")]
    public static extern int ManVersion();
    
    // Manager Interface Methods - These are called through the interface pointer
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManConnect(IntPtr manager, [MarshalAs(UnmanagedType.LPStr)] string server);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManDisconnect(IntPtr manager);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManIsConnected(IntPtr manager);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManLogin(IntPtr manager, int login, [MarshalAs(UnmanagedType.LPStr)] string password);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManPing(IntPtr manager);
    
    // User Management
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr ManUsersRequest(IntPtr manager, out int total);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr ManUserRecordsRequest(IntPtr manager, int[] logins, out int total);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManUserRecordNew(IntPtr manager, ref UserRecordNative user);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManUserRecordUpdate(IntPtr manager, ref UserRecordNative user);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManUserRecordDelete(IntPtr manager, int login);
    
    // Trade Management
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr ManAdmTradesRequest(IntPtr manager, [MarshalAs(UnmanagedType.LPStr)] string group, int open_only, out int total);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr ManAdmTradesUserHistory(IntPtr manager, int login, int from, int to, out int total);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManAdmTradeRecordGet(IntPtr manager, int order, out TradeRecordNative trade);
    
    // Memory Management
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern void ManMemFree(IntPtr manager, IntPtr ptr);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr ManErrorDescription(IntPtr manager, int code);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int ManRelease(IntPtr manager);
    
    // Constants
    public const int RET_OK = 0;
    public const int ManAPIVersion = 400;
}

public static class MT4Constants
{
    // Trade Commands
    public const int OP_BUY = 0;
    public const int OP_SELL = 1;
    public const int OP_BUYLIMIT = 2;
    public const int OP_SELLLIMIT = 3;
    public const int OP_BUYSTOP = 4;
    public const int OP_SELLSTOP = 5;
    
    // Error Codes
    public const int RET_OK = 0;
    public const int RET_ERROR = 1;
    public const int RET_INVALID_DATA = 2;
    public const int RET_TECH_PROBLEM = 3;
    public const int RET_OLD_VERSION = 4;
    public const int RET_NO_CONNECT = 5;
    public const int RET_NOT_ENOUGH_RIGHTS = 6;
    public const int RET_TOO_FREQUENT = 7;
    public const int RET_MALFUNCTIONAL_TRADE = 8;
    public const int RET_ACCOUNT_DISABLED = 64;
    public const int RET_INVALID_ACCOUNT = 65;
}