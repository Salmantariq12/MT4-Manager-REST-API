@echo off
echo ============================================
echo    MT4 REST API SERVER - REAL MT4 PRICES
echo ============================================
echo.

REM Kill any existing instances
taskkill /f /im MT4RestApi.exe 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Previous instances stopped.
    ping localhost -n 3 > nul
)

echo Starting MT4 REST API Server...
echo.
echo Connecting to FXCubic via FIX Protocol 4.3:
echo Server: live.fxcubic.net:9120
echo SenderCompID: AimsTest_Q
echo TargetCompID: FXC_Q
echo Account: 100860
echo.

cd /d "%~dp0MT4RestApi_SelfContained"
start "MT4 REST API Server" MT4RestApi.exe

ping localhost -n 4 > nul

echo.
echo ============================================
echo    SERVER STARTED WITH FIX PRICE FEED
echo ============================================
echo.
echo API Server:  http://localhost:5000
echo Swagger UI:  http://localhost:5000/swagger
echo.
echo Connected to: FXCubic FIX Server (live.fxcubic.net:9120)
echo Protocol: FIX 4.3
echo Account: 100860
echo Symbols: EURUSD, GBPUSD, USDJPY, AUDUSD, XAUUSD, etc.
echo.
echo Test Endpoints:
echo   EURUSD:    http://localhost:5000/api/price/EURUSD
echo   XAUUSD:    http://localhost:5000/api/price/XAUUSD
echo   All:       http://localhost:5000/api/realtime/all
echo   Status:    http://localhost:5000/api/realtime/status
echo.
echo NOTE: Using FIX Protocol 4.3 for real-time price feed
echo.
echo The server is running in the background.
echo To stop it, run STOP_API.bat
echo.
pause