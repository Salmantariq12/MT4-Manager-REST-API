using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MT4RestApi.Models;
using MT4RestApi.Native;

namespace MT4RestApi.Services;

public class MT4ManagerService : IMT4ManagerService, IDisposable
{
    private bool _initialized = false;
    private bool _disposed = false;
    private readonly object _lock = new();
    private readonly ILogger<MT4ManagerService> _logger;
    private string _lastError = string.Empty;

    public MT4ManagerService(ILogger<MT4ManagerService> logger)
    {
        _logger = logger;
        
        try
        {
            // Try to initialize the wrapper
            int result = MT4WrapperApi.MT4_Initialize();
            if (result == MT4WrapperApi.MT4_SUCCESS)
            {
                _initialized = true;
                _logger.LogInformation("MT4 Wrapper initialized successfully");
            }
            else
            {
                _lastError = MT4WrapperApi.GetLastErrorString();
                _logger.LogError("Failed to initialize MT4 Wrapper: {Error}", _lastError);
            }
        }
        catch (DllNotFoundException ex)
        {
            // DLL not found - log error but allow service to start for debugging
            _initialized = false;
            _logger.LogError("MT4Wrapper.dll not found: {Error}", ex.Message);
            _logger.LogError("Current directory: {Dir}", Directory.GetCurrentDirectory());
            _logger.LogError("Executable location: {Exe}", System.Reflection.Assembly.GetExecutingAssembly().Location);

            // Check if DLL exists in current directory
            var currentDir = Directory.GetCurrentDirectory();
            var dllPath = Path.Combine(currentDir, "MT4Wrapper.dll");
            _logger.LogError("Checking for DLL at: {Path}", dllPath);
            _logger.LogError("DLL exists at path: {Exists}", File.Exists(dllPath));

            // Check for mtmanapi.dll
            var mtmanapiPath = Path.Combine(currentDir, "mtmanapi.dll");
            _logger.LogError("mtmanapi.dll exists: {Exists}", File.Exists(mtmanapiPath));

            _logger.LogError("To fix this issue:");
            _logger.LogError("1. Ensure MT4Wrapper.dll is in the same folder as MT4RestApi.exe");
            _logger.LogError("2. Ensure mtmanapi.dll is in the same folder");
            _logger.LogError("3. Install Visual C++ Redistributable 2015-2022 x86");
            _lastError = $"MT4Wrapper.dll not found. {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MT4 Manager Service");
            _lastError = ex.Message;
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (_lock)
            {
                if (!_initialized) return false;
                return MT4WrapperApi.MT4_IsConnected() != 0;
            }
        }
    }

    public async Task<bool> ConnectAsync(string server)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    if (!_initialized)
                    {
                        _lastError = "Service not initialized";
                        return false;
                    }

                    _logger.LogInformation("Connecting to MT4 server: {Server}", server);
                    
                    int result = MT4WrapperApi.MT4_Connect(server);
                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        _logger.LogInformation("Connected successfully to {Server}", server);
                        return true;
                    }
                    
                    _lastError = MT4WrapperApi.GetLastErrorString();
                    _logger.LogError("Connection failed: {Error}", _lastError);
                    return false;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Exception during connection to {Server}", server);
                    return false;
                }
            }
        });
    }

    public async Task<bool> LoginAsync(int login, string password)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    _lastError = "Service not initialized";
                    return false;
                }

                if (!IsConnected)
                {
                    _lastError = "Not connected to server";
                    return false;
                }


                try
                {
                    int result = MT4WrapperApi.MT4_Login(login, password);
                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        _logger.LogInformation("Logged in successfully with login: {Login}", login);
                        return true;
                    }
                    
                    _lastError = MT4WrapperApi.GetLastErrorString();
                    _logger.LogError("Login failed for {Login}: {Error}", login, _lastError);
                    return false;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Exception during login for {Login}", login);
                    return false;
                }
            }
        });
    }

    public async Task DisconnectAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized) return;
                
                try
                {
                    MT4WrapperApi.MT4_Disconnect();
                    _logger.LogInformation("Disconnected from MT4 server");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during disconnect");
                }
            }
        });
    }

    public async Task<bool> PingAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected) return false;
                
                try
                {
                    return MT4WrapperApi.MT4_Ping() == MT4WrapperApi.MT4_SUCCESS;
                }
                catch
                {
                    return false;
                }
            }
        });
    }

    public async Task<OpenTradeResult> OpenTradeAsync(OpenTradeRequest request)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected)
                {
                    return new OpenTradeResult
                    {
                        Success = false,
                        Message = "Not connected to MT4 server"
                    };
                }

                try
                {
                    byte[] buffer = new byte[1024];
                    int result = MT4WrapperApi.MT4_OpenTrade(
                        request.Login,
                        request.Symbol,
                        request.Cmd,
                        request.Volume,
                        request.Price,
                        request.StopLoss,
                        request.TakeProfit,
                        request.Comment,
                        buffer,
                        buffer.Length
                    );

                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        // Parse the order number from response
                        int jsonEnd = Array.IndexOf(buffer, (byte)0);
                        if (jsonEnd < 0) jsonEnd = buffer.Length;
                        string json = Encoding.UTF8.GetString(buffer, 0, jsonEnd);
                        
                        var response = JsonSerializer.Deserialize<Dictionary<string, object>>(json, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });

                        int order = 0;
                        if (response != null && response.ContainsKey("order"))
                        {
                            order = response["order"] is JsonElement orderJson ? orderJson.GetInt32() : Convert.ToInt32(response["order"]);
                        }

                        return new OpenTradeResult
                        {
                            Success = true,
                            Order = order,
                            Message = "Trade opened successfully"
                        };
                    }

                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return new OpenTradeResult
                    {
                        Success = false,
                        Message = _lastError
                    };
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error opening trade");
                    return new OpenTradeResult
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }
        });
    }

    public async Task<bool> CloseTradeAsync(int order)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected)
                {
                    _lastError = "Not connected to MT4 server";
                    return false;
                }

                try
                {
                    int result = MT4WrapperApi.MT4_CloseTrade(order, 0, 0); // Use market price
                    
                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        _logger.LogInformation("Trade {Order} closed successfully", order);
                        return true;
                    }

                    _lastError = MT4WrapperApi.GetLastErrorString();
                    _logger.LogError("Failed to close trade {Order}: {Error}", order, _lastError);
                    return false;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error closing trade {Order}", order);
                    return false;
                }
            }
        });
    }

    public async Task<CloseTradesResult> CloseAllTradesAsync(int login = 0)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected)
                {
                    return new CloseTradesResult
                    {
                        Success = false,
                        Message = "Not connected to MT4 server"
                    };
                }

                try
                {
                    // Get all open trades
                    var trades = GetTradesAsync(login, true).Result;
                    var closedOrders = new List<int>();
                    
                    foreach (var trade in trades)
                    {
                        int result = MT4WrapperApi.MT4_CloseTrade(trade.Order, 0, 0);
                        if (result == MT4WrapperApi.MT4_SUCCESS)
                        {
                            closedOrders.Add(trade.Order);
                        }
                    }

                    return new CloseTradesResult
                    {
                        Success = true,
                        ClosedCount = closedOrders.Count,
                        ClosedOrders = closedOrders,
                        Message = $"Closed {closedOrders.Count} of {trades.Count} trades"
                    };
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error closing all trades");
                    return new CloseTradesResult
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }
        });
    }

    public async Task<List<SymbolInfo>> GetSymbolsAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected)
                {
                    return new List<SymbolInfo>();
                }

                try
                {
                    byte[] buffer = new byte[65536];
                    int result = MT4WrapperApi.MT4_GetSymbols(buffer, buffer.Length);
                    
                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        int jsonEnd = Array.IndexOf(buffer, (byte)0);
                        if (jsonEnd < 0) jsonEnd = buffer.Length;
                        string json = Encoding.UTF8.GetString(buffer, 0, jsonEnd);
                        
                        var symbols = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        
                        if (symbols != null)
                        {
                            return symbols.Select(s =>
                            {
                                var symbolInfo = new SymbolInfo
                                {
                                    Symbol = s.ContainsKey("symbol") ? s["symbol"].ToString() ?? "" : "",
                                    Description = s.ContainsKey("description") ? s["description"].ToString() ?? "" : "",
                                    Bid = s.ContainsKey("bid") ?
                                        (s["bid"] is JsonElement bidJson ? bidJson.GetDouble() : Convert.ToDouble(s["bid"])) : 0,
                                    Ask = s.ContainsKey("ask") ?
                                        (s["ask"] is JsonElement askJson ? askJson.GetDouble() : Convert.ToDouble(s["ask"])) : 0,
                                    Spread = s.ContainsKey("spread") ?
                                        (s["spread"] is JsonElement spreadJson ? spreadJson.GetDouble() : Convert.ToDouble(s["spread"])) : 0,
                                    Digits = s.ContainsKey("digits") ?
                                        (s["digits"] is JsonElement digitsJson ? digitsJson.GetInt32() : Convert.ToInt32(s["digits"])) : 0,
                                    ContractSize = s.ContainsKey("contractSize") ?
                                        (s["contractSize"] is JsonElement contractJson ? contractJson.GetDouble() : Convert.ToDouble(s["contractSize"])) : 0,
                                    MinLot = s.ContainsKey("minLot") ?
                                        (s["minLot"] is JsonElement minLotJson ? minLotJson.GetDouble() : Convert.ToDouble(s["minLot"])) : 0,
                                    MaxLot = s.ContainsKey("maxLot") ?
                                        (s["maxLot"] is JsonElement maxLotJson ? maxLotJson.GetDouble() : Convert.ToDouble(s["maxLot"])) : 0,
                                    LotStep = s.ContainsKey("lotStep") ?
                                        (s["lotStep"] is JsonElement lotStepJson ? lotStepJson.GetDouble() : Convert.ToDouble(s["lotStep"])) : 0
                                };
                                symbolInfo.CleanSymbol();
                                return symbolInfo;
                            }).ToList();
                        }
                    }

                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return new List<SymbolInfo>();
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error getting symbols");
                    return new List<SymbolInfo>();
                }
            }
        });
    }

    public async Task<UserRecord?> GetUserAsync(int login)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected) return null;
                
                
                try
                {
                    byte[] buffer = new byte[4096];
                    int result = MT4WrapperApi.MT4_GetUserInfo(login, buffer, buffer.Length);
                    
                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        // Find the actual end of the JSON string
                        int jsonEnd = Array.IndexOf(buffer, (byte)0);
                        if (jsonEnd < 0) jsonEnd = buffer.Length;
                        string json = Encoding.UTF8.GetString(buffer, 0, jsonEnd);
                        var info = JsonSerializer.Deserialize<Dictionary<string, object>>(json, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        
                        if (info != null)
                        {
                            var user = new UserRecord
                            {
                                Login = login,
                                Name = info.ContainsKey("name") ? info["name"].ToString() ?? "" : "",
                                Balance = info.ContainsKey("balance") ? 
                                    (info["balance"] is JsonElement balanceJson ? balanceJson.GetDouble() : Convert.ToDouble(info["balance"])) : 0
                            };
                            return user;
                        }
                    }
                    
                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return null;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error getting user info for login {Login}", login);
                    return null;
                }
            }
        });
    }

    public async Task<List<UserRecord>> GetUsersAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected) return new List<UserRecord>();
                
                
                try
                {
                    byte[] buffer = new byte[65536]; // 64KB buffer
                    int result = MT4WrapperApi.MT4_GetAllUsers(buffer, buffer.Length);
                    
                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        // Find the actual end of the JSON string
                        int jsonEnd = Array.IndexOf(buffer, (byte)0);
                        if (jsonEnd < 0) jsonEnd = buffer.Length;
                        string json = Encoding.UTF8.GetString(buffer, 0, jsonEnd);
                        var users = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        
                        if (users != null)
                        {
                            return users.Select(u => new UserRecord
                            {
                                Login = u["login"] is JsonElement loginJson ? loginJson.GetInt32() : Convert.ToInt32(u["login"]),
                                Name = u.ContainsKey("name") ? u["name"].ToString() ?? "" : "",
                                Balance = u.ContainsKey("balance") ? 
                                    (u["balance"] is JsonElement balanceJson ? balanceJson.GetDouble() : Convert.ToDouble(u["balance"])) : 0
                            }).ToList();
                        }
                        return new List<UserRecord>();
                    }
                    
                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return new List<UserRecord>();
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error getting all users");
                    return new List<UserRecord>();
                }
            }
        });
    }

    public async Task<bool> CreateUserAsync(UserRecord user)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected)
                {
                    _lastError = "Not connected to MT4 server";
                    return false;
                }

                try
                {
                    // Prepare JSON data for the user
                    var jsonData = JsonSerializer.Serialize(new
                    {
                        login = user.Login,
                        password = user.Password ?? "defaultpass123",
                        group = user.Group ?? "demo",
                        name = user.Name ?? "New User",
                        email = user.Email ?? "",
                        leverage = user.Leverage > 0 ? user.Leverage : 100
                    });

                    byte[] buffer = new byte[4096];
                    int result = MT4WrapperApi.MT4_CreateUser(jsonData, buffer, buffer.Length);

                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        _lastError = "";
                        return true;
                    }

                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return false;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error creating user");
                    return false;
                }
            }
        });
    }

    public async Task<bool> UpdateUserAsync(UserRecord user)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected)
                {
                    _lastError = "Not connected to MT4 server";
                    return false;
                }

                try
                {
                    // Prepare JSON data for the update
                    var jsonData = JsonSerializer.Serialize(new
                    {
                        name = user.Name,
                        email = user.Email,
                        group = user.Group
                    });

                    int result = MT4WrapperApi.MT4_UpdateUser(user.Login, jsonData);

                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        _lastError = "";
                        return true;
                    }

                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return false;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error updating user");
                    return false;
                }
            }
        });
    }

    public async Task<bool> DeleteUserAsync(int login)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected)
                {
                    _lastError = "Not connected to MT4 server";
                    return false;
                }

                try
                {
                    int result = MT4WrapperApi.MT4_DeleteUser(login);

                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        _lastError = "";
                        return true;
                    }

                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return false;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error deleting user");
                    return false;
                }
            }
        });
    }

    public async Task<List<TradeRecord>> GetTradesAsync(int login = 0, bool openOnly = false)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected) return new List<TradeRecord>();
                
                
                try
                {
                    byte[] buffer = new byte[65536]; // 64KB buffer
                    int result = MT4WrapperApi.MT4_GetTrades(login, buffer, buffer.Length);
                    
                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        // Find the actual end of the JSON string
                        int jsonEnd = Array.IndexOf(buffer, (byte)0);
                        if (jsonEnd < 0) jsonEnd = buffer.Length;
                        string json = Encoding.UTF8.GetString(buffer, 0, jsonEnd);
                        var trades = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        
                        if (trades != null)
                        {
                            return trades.Select(t =>
                            {
                                var trade = new TradeRecord
                                {
                                    Order = t["order"] is JsonElement orderJson ? orderJson.GetInt32() : Convert.ToInt32(t["order"]),
                                    Login = t["login"] is JsonElement loginJson ? loginJson.GetInt32() : Convert.ToInt32(t["login"]),
                                    Symbol = t.ContainsKey("symbol") ? t["symbol"].ToString() ?? "" : "",
                                    Volume = t.ContainsKey("volume") ?
                                        (t["volume"] is JsonElement volumeJson ? volumeJson.GetInt32() : Convert.ToInt32(t["volume"])) : 0,
                                    Profit = t.ContainsKey("profit") ?
                                        (t["profit"] is JsonElement profitJson ? profitJson.GetDouble() : Convert.ToDouble(t["profit"])) : 0
                                };
                                trade.CleanSymbol();
                                return trade;
                            }).ToList();
                        }
                        return new List<TradeRecord>();
                    }
                    
                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return new List<TradeRecord>();
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error getting trades for login {Login}", login);
                    return new List<TradeRecord>();
                }
            }
        });
    }

    public async Task<TradeRecord?> GetTradeAsync(int order)
    {
        // Not implemented in wrapper yet - could filter from GetTradesAsync
        var trades = await GetTradesAsync();
        return trades.FirstOrDefault(t => t.Order == order);
    }

    public async Task<BalanceInfo> GetBalanceInfoAsync(int login)
    {
        var user = await GetUserAsync(login);
        if (user != null)
        {
            return new BalanceInfo
            {
                Login = login,
                Balance = user.Balance,
                Equity = user.Balance,  // Would need more data for accurate equity
                Margin = 0,
                FreeMargin = user.Balance
            };
        }
        
        return new BalanceInfo { Login = login };
    }

    public async Task<PriceQuote?> GetQuoteAsync(string symbol)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_initialized || !IsConnected)
                {
                    _lastError = "Not connected to MT4 server";
                    return null;
                }

                try
                {
                    byte[] buffer = new byte[1024];
                    int result = MT4WrapperApi.MT4_GetQuote(symbol, buffer, buffer.Length);

                    if (result == MT4WrapperApi.MT4_SUCCESS)
                    {
                        int jsonEnd = Array.IndexOf(buffer, (byte)0);
                        if (jsonEnd < 0) jsonEnd = buffer.Length;
                        string json = Encoding.UTF8.GetString(buffer, 0, jsonEnd);
                        
                        var quoteData = JsonSerializer.Deserialize<JsonNode>(json);
                        if (quoteData != null)
                        {
                            return new PriceQuote
                            {
                                Symbol = symbol,
                                Bid = quoteData["bid"]?.GetValue<double>() ?? 0,
                                Ask = quoteData["ask"]?.GetValue<double>() ?? 0,
                                Spread = quoteData["spread"]?.GetValue<double>() ?? 0,
                                Digits = quoteData["digits"]?.GetValue<int>() ?? 0,
                                Timestamp = DateTime.UtcNow
                            };
                        }
                    }

                    _lastError = MT4WrapperApi.GetLastErrorString();
                    return null;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Error getting quote for {Symbol}", symbol);
                    return null;
                }
            }
        });
    }

    public string GetLastError()
    {
        return _lastError;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    if (_initialized)
                    {
                        try
                        {
                            MT4WrapperApi.MT4_Shutdown();
                            _logger.LogInformation("MT4 Wrapper shutdown");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during shutdown");
                        }
                    }
                }
            }
            
            _disposed = true;
        }
    }
}