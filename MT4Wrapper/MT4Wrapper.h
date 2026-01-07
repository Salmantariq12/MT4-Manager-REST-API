#pragma once

#ifdef MT4WRAPPER_EXPORTS
#define MT4WRAPPER_API extern "C" __declspec(dllexport)
#else
#define MT4WRAPPER_API extern "C" __declspec(dllimport)
#endif

// Simple C-style functions that C# can easily call
MT4WRAPPER_API int MT4_Initialize();
MT4WRAPPER_API void MT4_Shutdown();
MT4WRAPPER_API int MT4_Connect(const char* server);
MT4WRAPPER_API int MT4_Login(int login, const char* password);
MT4WRAPPER_API int MT4_Disconnect();
MT4WRAPPER_API int MT4_IsConnected();
MT4WRAPPER_API const char* MT4_GetLastError();
MT4WRAPPER_API int MT4_Ping();

// User management
MT4WRAPPER_API int MT4_GetUserInfo(int login, char* buffer, int bufferSize);
MT4WRAPPER_API int MT4_GetAllUsers(char* buffer, int bufferSize);
MT4WRAPPER_API int MT4_CreateUser(const char* jsonData, char* buffer, int bufferSize);
MT4WRAPPER_API int MT4_UpdateUser(int login, const char* jsonData);
MT4WRAPPER_API int MT4_DeleteUser(int login);

// Trade management  
MT4WRAPPER_API int MT4_GetTrades(int login, char* buffer, int bufferSize);
MT4WRAPPER_API int MT4_OpenTrade(int login, const char* symbol, int cmd, double volume, 
    double price, double stoploss, double takeprofit, const char* comment, char* buffer, int bufferSize);
MT4WRAPPER_API int MT4_CloseTrade(int order, double lots, double price);

// Symbol management
MT4WRAPPER_API int MT4_GetSymbols(char* buffer, int bufferSize);
MT4WRAPPER_API int MT4_GetQuote(const char* symbol, char* buffer, int bufferSize);

// Return codes
#define MT4_SUCCESS 0
#define MT4_ERROR_NOT_INITIALIZED -1
#define MT4_ERROR_ALREADY_INITIALIZED -2
#define MT4_ERROR_CONNECTION_FAILED -3
#define MT4_ERROR_LOGIN_FAILED -4
#define MT4_ERROR_NOT_CONNECTED -5
#define MT4_ERROR_INVALID_PARAMETER -6
#define MT4_ERROR_BUFFER_TOO_SMALL -7
#define MT4_ERROR_INTERNAL -99