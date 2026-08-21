@echo off
setlocal
cd /d "%~dp0\.."
echo ============================================================
echo  Appium Builder Reborn - Local TC Server
echo ============================================================
echo.
echo Server URL: http://0.0.0.0:7788
echo Default Model: Qwen3-VL 4B
echo.
echo To use 2B instead, close this window and run:
echo   set LOCAL_TC_MODEL=qwen3-vl:2b
echo   dotnet run --project LocalTcServer\LocalTcServer.csproj
echo.
dotnet run --project LocalTcServer\LocalTcServer.csproj
pause
