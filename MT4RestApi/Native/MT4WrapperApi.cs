using System.Runtime.InteropServices;

namespace MT4RestApi.Native;

public static class MT4WrapperApi
{
    private const string DllName = "MT4Wrapper.dll";

    // Return codes
    public const int MT4_SUCCESS = 0;
    public const int MT4_ERROR_NOT_INITIALIZED = -1;
    public const int MT4_ERROR_ALREADY_INITIALIZED = -2;
    public const int MT4_ERROR_CONNECTION_FAILED = -3;
    public const int MT4_ERROR_LOGIN_FAILED = -4;
    public const int MT4_ERROR_NOT_CONNECTED = -5;
    public const int MT4_ERROR_INVALID_PARAMETER = -6;
    public const int MT4_ERROR_BUFFER_TOO_SMALL = -7;
    public const int MT4_ERROR_INTERNAL = -99;

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MT4_Initialize")]
    public static extern int MT4_Initialize();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void MT4_Shutdown();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_Connect([MarshalAs(UnmanagedType.LPStr)] string server);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_Login(int login, [MarshalAs(UnmanagedType.LPStr)] string password);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int MT4_Disconnect();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int MT4_IsConnected();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr MT4_GetLastError();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int MT4_Ping();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_GetUserInfo(int login, [Out] byte[] buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_GetAllUsers([Out] byte[] buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_GetTrades(int login, [Out] byte[] buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_OpenTrade(int login, [MarshalAs(UnmanagedType.LPStr)] string symbol, int cmd, 
        double volume, double price, double stoploss, double takeprofit, 
        [MarshalAs(UnmanagedType.LPStr)] string comment, [Out] byte[] buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int MT4_CloseTrade(int order, double lots, double price);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_GetSymbols([Out] byte[] buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_CreateUser([MarshalAs(UnmanagedType.LPStr)] string jsonData, [Out] byte[] buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_UpdateUser(int login, [MarshalAs(UnmanagedType.LPStr)] string jsonData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int MT4_DeleteUser(int login);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int MT4_GetQuote([MarshalAs(UnmanagedType.LPStr)] string symbol, [Out] byte[] buffer, int bufferSize);

    public static string GetLastErrorString()
    {
        IntPtr ptr = MT4_GetLastError();
        return Marshal.PtrToStringAnsi(ptr) ?? "";
    }
}