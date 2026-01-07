#define MT4WRAPPER_EXPORTS
#include <windows.h>
#include <time.h>
#include "MT4Wrapper.h"
#include "../MT4ManagerAPI.h"
#include <string>
#include <sstream>
#include <memory>
#include <cmath>
#include <cstring>

// Global manager instance
static CManagerInterface* g_pManager = nullptr;
static CManagerFactory* g_pFactory = nullptr;  // MUST keep factory alive!
static std::string g_lastError;
static bool g_initialized = false;
static bool g_bypassMode = false;  // Bypass mode to prevent crashes
static bool g_mockConnected = false;  // Mock connection state

// Helper to set error message
static void SetError(const char* error) {
    g_lastError = error ? error : "";
}

// Safe wrapper for IsConnected check
static bool SafeIsConnected() {
    if (!g_initialized || !g_pManager) {
        return false;
    }
    try {
        return g_pManager->IsConnected() ? true : false;
    }
    catch (...) {
        return false;
    }
}

MT4WRAPPER_API int MT4_Initialize() {
    if (g_initialized) {
        SetError("Already initialized");
        return MT4_ERROR_ALREADY_INITIALIZED;
    }

    try {
        // Initialize Winsock (required for network operations)
        WSADATA wsa;
        if (WSAStartup(0x0202, &wsa) != 0) {
            SetError("Failed to initialize Winsock");
            return MT4_ERROR_INTERNAL;
        }
        
        // Create manager factory and KEEP IT ALIVE
        // The factory MUST remain alive for the entire lifetime of the manager
        // because it holds the DLL handle!
        g_pFactory = new CManagerFactory();
        
        // Check if factory loaded the DLL properly
        if (!g_pFactory->IsValid()) {
            delete g_pFactory;
            g_pFactory = nullptr;
            WSACleanup();
            SetError("Failed to load mtmanapi.dll");
            return MT4_ERROR_INTERNAL;
        }
        
        // Also call factory's WinsockStartup for compatibility
        g_pFactory->WinsockStartup();
        
        // Create the manager instance
        g_pManager = g_pFactory->Create(ManAPIVersion);
        
        if (!g_pManager) {
            delete g_pFactory;
            g_pFactory = nullptr;
            WSACleanup();
            SetError("Failed to create manager instance");
            return MT4_ERROR_INTERNAL;
        }

        g_initialized = true;
        SetError("");
        return MT4_SUCCESS;
    }
    catch (const std::exception& e) {
        if (g_pFactory) {
            delete g_pFactory;
            g_pFactory = nullptr;
        }
        WSACleanup();
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        if (g_pFactory) {
            delete g_pFactory;
            g_pFactory = nullptr;
        }
        WSACleanup();
        SetError("Unknown error during initialization");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API void MT4_Shutdown() {
    if (g_pManager) {
        g_pManager->Release();
        g_pManager = nullptr;
    }
    
    // Clean up the factory AFTER releasing the manager
    // The factory destructor will unload the DLL
    if (g_pFactory) {
        g_pFactory->WinsockCleanup();
        delete g_pFactory;
        g_pFactory = nullptr;
    }
    
    g_initialized = false;
    g_mockConnected = false;
    SetError("");
    
    // Cleanup Winsock
    WSACleanup();
}

MT4WRAPPER_API int MT4_Connect(const char* server) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!server) {
        SetError("Invalid server parameter");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    try {
        // Create a mutable copy of the server string
        // Some MT4 API versions modify the string during connection
        char serverCopy[256] = {0};
        strncpy_s(serverCopy, sizeof(serverCopy), server, _TRUNCATE);
        
        // Set working directory first (may be required by some MT4 Manager versions)
        char currentPath[MAX_PATH] = {0};
        if (GetCurrentDirectoryA(MAX_PATH, currentPath) > 0) {
            g_pManager->WorkingDirectory(currentPath);
        }
        
        // Add a small delay to ensure initialization is complete
        Sleep(100);
        
        // MT4 Manager API expects just IP:port format
        int result = g_pManager->Connect(serverCopy);
        if (result == RET_OK) {
            SetError("");
            return MT4_SUCCESS;
        }

        const char* errorDesc = g_pManager->ErrorDescription(result);
        SetError(errorDesc ? errorDesc : "Connection failed");
        return MT4_ERROR_CONNECTION_FAILED;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error during connection");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_Login(int login, const char* password) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!password) {
        SetError("Invalid password parameter");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    try {
        int result = g_pManager->Login(login, const_cast<char*>(password));
        if (result == RET_OK) {
            SetError("");
            return MT4_SUCCESS;
        }

        const char* errorDesc = g_pManager->ErrorDescription(result);
        SetError(errorDesc ? errorDesc : "Login failed");
        return MT4_ERROR_LOGIN_FAILED;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error during login");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_Disconnect() {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    try {
        int result = g_pManager->Disconnect();
        SetError("");
        return (result == RET_OK) ? MT4_SUCCESS : MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Error during disconnect");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_IsConnected() {
    return SafeIsConnected() ? 1 : 0;
}

MT4WRAPPER_API const char* MT4_GetLastError() {
    return g_lastError.c_str();
}

MT4WRAPPER_API int MT4_Ping() {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!SafeIsConnected()) {
        SetError("Not connected");
        return MT4_ERROR_NOT_CONNECTED;
    }

    try {
        int result = g_pManager->Ping();
        return (result == RET_OK) ? MT4_SUCCESS : MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Error during ping");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_GetUserInfo(int login, char* buffer, int bufferSize) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!buffer || bufferSize <= 0) {
        SetError("Invalid buffer parameter");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    try {
        int logins[] = { login };
        int total = 0;
        UserRecord* users = g_pManager->UserRecordsRequest(logins, &total);
        
        if (users && total > 0) {
            // Convert to JSON
            std::stringstream json;
            json << "{\"login\":" << users[0].login
                 << ",\"name\":\"" << users[0].name << "\""
                 << ",\"email\":\"" << users[0].email << "\""
                 << ",\"balance\":" << users[0].balance
                 << ",\"credit\":" << users[0].credit
                 << ",\"leverage\":" << users[0].leverage
                 << ",\"group\":\"" << users[0].group << "\"}";
            
            std::string result = json.str();
            if (result.length() >= (size_t)bufferSize) {
                g_pManager->MemFree(users);
                SetError("Buffer too small");
                return MT4_ERROR_BUFFER_TOO_SMALL;
            }
            
            strcpy_s(buffer, bufferSize, result.c_str());
            g_pManager->MemFree(users);
            SetError("");
            return MT4_SUCCESS;
        }
        
        SetError("User not found");
        return MT4_ERROR_INTERNAL;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_GetAllUsers(char* buffer, int bufferSize) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!buffer || bufferSize <= 0) {
        SetError("Invalid buffer parameter");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    try {
        int total = 0;
        UserRecord* users = g_pManager->UsersRequest(&total);
        
        if (users && total > 0) {
            // Convert to JSON array
            std::stringstream json;
            json << "[";
            
            for (int i = 0; i < total && i < 100; i++) { // Limit to 100 users for safety
                if (i > 0) json << ",";
                json << "{\"login\":" << users[i].login
                     << ",\"name\":\"" << users[i].name << "\""
                     << ",\"balance\":" << users[i].balance << "}";
            }
            
            json << "]";
            
            std::string result = json.str();
            if (result.length() >= (size_t)bufferSize) {
                g_pManager->MemFree(users);
                SetError("Buffer too small");
                return MT4_ERROR_BUFFER_TOO_SMALL;
            }
            
            strcpy_s(buffer, bufferSize, result.c_str());
            g_pManager->MemFree(users);
            SetError("");
            return MT4_SUCCESS;
        }
        
        // Return empty array if no users
        strcpy_s(buffer, bufferSize, "[]");
        SetError("");
        return MT4_SUCCESS;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_GetTrades(int login, char* buffer, int bufferSize) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!buffer || bufferSize <= 0) {
        SetError("Invalid buffer parameter");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    try {
        int total = 0;
        TradeRecord* trades = nullptr;
        
        if (login > 0) {
            // Get trades for specific user - using correct method name
            trades = g_pManager->TradesUserHistory(login, 0, time(NULL), &total);
        } else {
            // Get all trades
            trades = g_pManager->TradesRequest(&total);
        }
        
        if (trades && total > 0) {
            // Convert to JSON array
            std::stringstream json;
            json << "[";
            
            for (int i = 0; i < total && i < 100; i++) { // Limit to 100 trades for safety
                if (i > 0) json << ",";
                json << "{\"order\":" << trades[i].order
                     << ",\"login\":" << trades[i].login
                     << ",\"symbol\":\"" << trades[i].symbol << "\""
                     << ",\"volume\":" << trades[i].volume
                     << ",\"profit\":" << trades[i].profit << "}";
            }
            
            json << "]";
            
            std::string result = json.str();
            if (result.length() >= (size_t)bufferSize) {
                g_pManager->MemFree(trades);
                SetError("Buffer too small");
                return MT4_ERROR_BUFFER_TOO_SMALL;
            }
            
            strcpy_s(buffer, bufferSize, result.c_str());
            g_pManager->MemFree(trades);
            SetError("");
            return MT4_SUCCESS;
        }
        
        // Return empty array if no trades
        strcpy_s(buffer, bufferSize, "[]");
        SetError("");
        return MT4_SUCCESS;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_OpenTrade(int login, const char* symbol, int cmd, double volume, 
    double price, double stoploss, double takeprofit, const char* comment, char* buffer, int bufferSize) {
    
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!symbol || !buffer || bufferSize <= 0) {
        SetError("Invalid parameters");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    if (!SafeIsConnected()) {
        SetError("Not connected");
        return MT4_ERROR_NOT_CONNECTED;
    }

    try {
        TradeTransInfo trade = {0};
        trade.type = cmd;
        trade.cmd = cmd;
        strcpy_s(trade.symbol, symbol);
        trade.volume = (int)(volume * 100); // Convert lots to volume
        // Note: login is not a member of TradeTransInfo in this API version
        trade.price = price;
        trade.sl = stoploss;
        trade.tp = takeprofit;
        if (comment) {
            strncpy_s(trade.comment, comment, sizeof(trade.comment) - 1);
        }

        int result = g_pManager->TradeTransaction(&trade);
        
        if (result == RET_OK) {
            // Return the order number in JSON format
            std::stringstream json;
            json << "{\"order\":" << trade.order << "}";
            
            std::string resultStr = json.str();
            if (resultStr.length() >= (size_t)bufferSize) {
                SetError("Buffer too small");
                return MT4_ERROR_BUFFER_TOO_SMALL;
            }
            
            strcpy_s(buffer, bufferSize, resultStr.c_str());
            SetError("");
            return MT4_SUCCESS;
        }

        const char* errorDesc = g_pManager->ErrorDescription(result);
        SetError(errorDesc ? errorDesc : "Trade transaction failed");
        return MT4_ERROR_INTERNAL;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error opening trade");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_CloseTrade(int order, double lots, double price) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!SafeIsConnected()) {
        SetError("Not connected");
        return MT4_ERROR_NOT_CONNECTED;
    }

    try {
        // First get the trade info
        TradeRecord trade = {0};
        int result = g_pManager->TradeRecordGet(order, &trade);
        if (result != RET_OK) {
            SetError("Trade not found");
            return MT4_ERROR_INTERNAL;
        }

        TradeTransInfo closeTrade = {0};
        closeTrade.type = TT_ORDER_CLOSE_BY;
        closeTrade.cmd = OP_BUY;  // Will be adjusted based on trade type
        closeTrade.order = order;
        closeTrade.orderby = order;
        closeTrade.volume = (lots > 0) ? (int)(lots * 100) : trade.volume;
        closeTrade.price = (price > 0) ? price : trade.close_price;
        strcpy_s(closeTrade.symbol, trade.symbol);

        result = g_pManager->TradeTransaction(&closeTrade);
        
        if (result == RET_OK) {
            SetError("");
            return MT4_SUCCESS;
        }

        const char* errorDesc = g_pManager->ErrorDescription(result);
        SetError(errorDesc ? errorDesc : "Close trade failed");
        return MT4_ERROR_INTERNAL;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error closing trade");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_CreateUser(const char* jsonData, char* buffer, int bufferSize) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!jsonData || !buffer || bufferSize <= 0) {
        SetError("Invalid parameters");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    if (!SafeIsConnected()) {
        SetError("Not connected");
        return MT4_ERROR_NOT_CONNECTED;
    }

    try {
        // Parse JSON data to extract user fields
        // For simplicity, we'll expect specific format
        UserRecord user = {0};
        
        // Simple parsing - in production, use a proper JSON parser
        std::string json(jsonData);
        
        // Extract login
        size_t pos = json.find("\"login\":");
        if (pos != std::string::npos) {
            user.login = atoi(json.c_str() + pos + 8);
        }
        
        // Extract password
        pos = json.find("\"password\":\"");
        if (pos != std::string::npos) {
            size_t endPos = json.find("\"", pos + 12);
            if (endPos != std::string::npos) {
                std::string pwd = json.substr(pos + 12, endPos - pos - 12);
                strcpy_s(user.password, pwd.c_str());
            }
        }
        
        // Extract group
        pos = json.find("\"group\":\"");
        if (pos != std::string::npos) {
            size_t endPos = json.find("\"", pos + 9);
            if (endPos != std::string::npos) {
                std::string grp = json.substr(pos + 9, endPos - pos - 9);
                strcpy_s(user.group, grp.c_str());
            }
        }
        
        // Extract name
        pos = json.find("\"name\":\"");
        if (pos != std::string::npos) {
            size_t endPos = json.find("\"", pos + 8);
            if (endPos != std::string::npos) {
                std::string nm = json.substr(pos + 8, endPos - pos - 8);
                strcpy_s(user.name, nm.c_str());
            }
        }
        
        // Set defaults
        user.enable = 1;
        user.enable_change_password = 1;
        user.leverage = 100;
        
        // Create the user
        int result = g_pManager->UserRecordNew(&user);
        
        if (result == RET_OK) {
            // Return success with the login
            std::stringstream response;
            response << "{\"success\":true,\"login\":" << user.login << "}";
            
            std::string resultStr = response.str();
            if (resultStr.length() >= (size_t)bufferSize) {
                SetError("Buffer too small");
                return MT4_ERROR_BUFFER_TOO_SMALL;
            }
            
            strcpy_s(buffer, bufferSize, resultStr.c_str());
            SetError("");
            return MT4_SUCCESS;
        }
        
        const char* errorDesc = g_pManager->ErrorDescription(result);
        SetError(errorDesc ? errorDesc : "Failed to create user");
        return MT4_ERROR_INTERNAL;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error creating user");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_UpdateUser(int login, const char* jsonData) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!jsonData) {
        SetError("Invalid parameters");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    if (!SafeIsConnected()) {
        SetError("Not connected");
        return MT4_ERROR_NOT_CONNECTED;
    }

    try {
        // First get the existing user
        UserRecord user = {0};
        int result = g_pManager->UserRecordGet(login, &user);
        if (result != RET_OK) {
            SetError("User not found");
            return MT4_ERROR_INTERNAL;
        }
        
        // Parse JSON and update fields
        std::string json(jsonData);
        
        // Update name if provided
        size_t pos = json.find("\"name\":\"");
        if (pos != std::string::npos) {
            size_t endPos = json.find("\"", pos + 8);
            if (endPos != std::string::npos) {
                std::string nm = json.substr(pos + 8, endPos - pos - 8);
                strcpy_s(user.name, nm.c_str());
            }
        }
        
        // Update email if provided
        pos = json.find("\"email\":\"");
        if (pos != std::string::npos) {
            size_t endPos = json.find("\"", pos + 9);
            if (endPos != std::string::npos) {
                std::string em = json.substr(pos + 9, endPos - pos - 9);
                strcpy_s(user.email, em.c_str());
            }
        }
        
        // Update group if provided
        pos = json.find("\"group\":\"");
        if (pos != std::string::npos) {
            size_t endPos = json.find("\"", pos + 9);
            if (endPos != std::string::npos) {
                std::string grp = json.substr(pos + 9, endPos - pos - 9);
                strcpy_s(user.group, grp.c_str());
            }
        }
        
        // Update the user
        result = g_pManager->UserRecordUpdate(&user);
        
        if (result == RET_OK) {
            SetError("");
            return MT4_SUCCESS;
        }
        
        const char* errorDesc = g_pManager->ErrorDescription(result);
        SetError(errorDesc ? errorDesc : "Failed to update user");
        return MT4_ERROR_INTERNAL;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error updating user");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_DeleteUser(int login) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!SafeIsConnected()) {
        SetError("Not connected");
        return MT4_ERROR_NOT_CONNECTED;
    }

    try {
        // Note: MT4 Manager API doesn't have a direct delete function
        // We can disable the user instead
        UserRecord user = {0};
        int result = g_pManager->UserRecordGet(login, &user);
        if (result != RET_OK) {
            SetError("User not found");
            return MT4_ERROR_INTERNAL;
        }
        
        // Disable the user account
        user.enable = 0;
        result = g_pManager->UserRecordUpdate(&user);
        
        if (result == RET_OK) {
            SetError("");
            return MT4_SUCCESS;
        }
        
        const char* errorDesc = g_pManager->ErrorDescription(result);
        SetError(errorDesc ? errorDesc : "Failed to disable user");
        return MT4_ERROR_INTERNAL;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error deleting user");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_GetSymbols(char* buffer, int bufferSize) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!buffer || bufferSize <= 0) {
        SetError("Invalid buffer parameter");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    try {
        // Refresh symbols from server first
        g_pManager->SymbolsRefresh();
        
        ConSymbol* symbols = nullptr;
        int total = 0;
        
        symbols = g_pManager->SymbolsGetAll(&total);
        
        if (symbols && total > 0) {
            // Convert to JSON array
            std::stringstream json;
            json << "[";
            
            for (int i = 0; i < total && i < 50; i++) { // Limit to 50 symbols for safety
                if (i > 0) json << ",";
                json << "{\"symbol\":\"" << symbols[i].symbol << "\""
                     << ",\"description\":\"" << symbols[i].description << "\""
                     << ",\"digits\":" << symbols[i].digits
                     << ",\"contractSize\":" << symbols[i].contract_size
                     << ",\"currency\":\"" << symbols[i].currency << "\""
                     << ",\"type\":" << symbols[i].type << "}";
            }
            
            json << "]";
            
            std::string result = json.str();
            if (result.length() >= (size_t)bufferSize) {
                g_pManager->MemFree(symbols);
                SetError("Buffer too small");
                return MT4_ERROR_BUFFER_TOO_SMALL;
            }
            
            strcpy_s(buffer, bufferSize, result.c_str());
            g_pManager->MemFree(symbols);
            SetError("");
            return MT4_SUCCESS;
        }
        
        // Return empty array if no symbols
        strcpy_s(buffer, bufferSize, "[]");
        SetError("");
        return MT4_SUCCESS;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error getting symbols");
        return MT4_ERROR_INTERNAL;
    }
}

MT4WRAPPER_API int MT4_GetQuote(const char* symbol, char* buffer, int bufferSize) {
    if (!g_initialized || !g_pManager) {
        SetError("Not initialized");
        return MT4_ERROR_NOT_INITIALIZED;
    }

    if (!SafeIsConnected()) {
        SetError("Not connected to MT4 server");
        return MT4_ERROR_NOT_CONNECTED;
    }

    if (!symbol || !buffer || bufferSize <= 0) {
        SetError("Invalid parameters");
        return MT4_ERROR_INVALID_PARAMETER;
    }

    try {
        // First, try to get updated symbol info with latest prices
        SymbolInfo symbolsInfo[128];
        int updated = g_pManager->SymbolInfoUpdated(symbolsInfo, 128);
        
        // Look for our symbol in the updated list
        SymbolInfo* targetSymbol = nullptr;
        for (int i = 0; i < updated; i++) {
            if (strcmp(symbolsInfo[i].symbol, symbol) == 0) {
                targetSymbol = &symbolsInfo[i];
                break;
            }
        }
        
        // If found in updated symbols and has prices
        if (targetSymbol && (targetSymbol->bid > 0 || targetSymbol->ask > 0)) {
            std::ostringstream json;
            json << "{";
            json << "\"symbol\":\"" << symbol << "\",";
            json << "\"bid\":" << targetSymbol->bid << ",";
            json << "\"ask\":" << targetSymbol->ask << ",";
            json << "\"spread\":" << targetSymbol->spread << ",";
            json << "\"digits\":" << targetSymbol->digits << ",";
            json << "\"time\":" << targetSymbol->lasttime;
            json << "}";
            
            std::string result_str = json.str();
            if (result_str.length() >= (size_t)bufferSize) {
                SetError("Buffer too small for quote data");
                return MT4_ERROR_BUFFER_TOO_SMALL;
            }
            
            strcpy_s(buffer, bufferSize, result_str.c_str());
            SetError("");
            return MT4_SUCCESS;
        }
        
        // Try to get last tick info for real-time prices
        int total = 0;
        TickInfo* ticks = g_pManager->TickInfoLast(symbol, &total);
        
        if (!ticks || total == 0) {
            // If no tick data, try SymbolInfoGet as last fallback
            SymbolInfo symbolInfo = {0};
            int result = g_pManager->SymbolInfoGet(symbol, &symbolInfo);
            
            if (result != RET_OK) {
                SetError("No price data available for symbol");
                return MT4_ERROR_INTERNAL;
            }
            
            // Use SymbolInfo data
            std::ostringstream json;
            json << "{";
            json << "\"symbol\":\"" << symbol << "\",";
            json << "\"bid\":" << symbolInfo.bid << ",";
            json << "\"ask\":" << symbolInfo.ask << ",";
            json << "\"spread\":" << symbolInfo.spread << ",";
            json << "\"digits\":" << symbolInfo.digits << ",";
            json << "\"time\":" << time(NULL);
            json << "}";
            
            std::string result_str = json.str();
            if (result_str.length() >= (size_t)bufferSize) {
                SetError("Buffer too small for quote data");
                return MT4_ERROR_BUFFER_TOO_SMALL;
            }
            
            strcpy_s(buffer, bufferSize, result_str.c_str());
            SetError("");
            return MT4_SUCCESS;
        }
        
        // Use the most recent tick
        TickInfo& tick = ticks[total - 1];
        
        // Get symbol info for digits
        SymbolInfo symbolInfo = {0};
        g_pManager->SymbolInfoGet(symbol, &symbolInfo);
        
        // Create JSON response with tick data
        std::ostringstream json;
        json << "{";
        json << "\"symbol\":\"" << symbol << "\",";
        json << "\"bid\":" << tick.bid << ",";
        json << "\"ask\":" << tick.ask << ",";
        json << "\"spread\":" << (int)((tick.ask - tick.bid) * pow(10, symbolInfo.digits)) << ",";
        json << "\"digits\":" << symbolInfo.digits << ",";
        json << "\"time\":" << tick.ctm;
        json << "}";

        std::string result_str = json.str();
        if (result_str.length() >= (size_t)bufferSize) {
            SetError("Buffer too small for quote data");
            return MT4_ERROR_BUFFER_TOO_SMALL;
        }

        strcpy_s(buffer, bufferSize, result_str.c_str());
        
        // Free tick memory
        if (ticks) {
            g_pManager->MemFree(ticks);
        }
        
        SetError("");
        return MT4_SUCCESS;
    }
    catch (const std::exception& e) {
        SetError(e.what());
        return MT4_ERROR_INTERNAL;
    }
    catch (...) {
        SetError("Unknown error getting quote");
        return MT4_ERROR_INTERNAL;
    }
}