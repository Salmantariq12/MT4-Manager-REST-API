@echo off
echo ============================================
echo    STOPPING MT4 REST API SERVER
echo ============================================
echo.

echo Stopping all MT4RestApi.exe instances...
taskkill /f /im MT4RestApi.exe 2>nul

if %ERRORLEVEL% EQU 0 (
    echo API Server stopped successfully.
) else (
    echo No running API server found.
)

echo.
echo ============================================
echo    SERVER STOPPED
echo ============================================
echo.
pause