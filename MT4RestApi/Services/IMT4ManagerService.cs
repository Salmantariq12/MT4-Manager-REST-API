using MT4RestApi.Models;

namespace MT4RestApi.Services;

public interface IMT4ManagerService
{
    // Connection Management
    Task<bool> ConnectAsync(string server);
    Task<bool> LoginAsync(int login, string password);
    Task DisconnectAsync();
    bool IsConnected { get; }
    
    // User Management
    Task<List<UserRecord>> GetUsersAsync();
    Task<UserRecord?> GetUserAsync(int login);
    Task<bool> CreateUserAsync(UserRecord user);
    Task<bool> UpdateUserAsync(UserRecord user);
    Task<bool> DeleteUserAsync(int login);
    
    // Trade Management
    Task<List<TradeRecord>> GetTradesAsync(int login = 0, bool openOnly = false);
    Task<TradeRecord?> GetTradeAsync(int order);
    
    // Account Information
    Task<BalanceInfo> GetBalanceInfoAsync(int login);
    
    // New Trading Operations
    Task<OpenTradeResult> OpenTradeAsync(OpenTradeRequest request);
    Task<bool> CloseTradeAsync(int order);
    Task<CloseTradesResult> CloseAllTradesAsync(int login = 0);
    Task<List<SymbolInfo>> GetSymbolsAsync();
    Task<bool> PingAsync();
    
    // Price/Quote Operations
    Task<PriceQuote?> GetQuoteAsync(string symbol);
    
    // Error Handling
    string GetLastError();
}