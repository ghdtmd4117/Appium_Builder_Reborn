@echo off
setlocal
cd /d "%~dp0"
set "PORT=%~2"
if "%PORT%"=="" set "PORT=8765"

if "%~1"=="" goto START_DEFAULT
goto START_WITH_LOG

:START_WITH_LOG
where py >nul 2>nul
if %errorlevel%==0 (
    py -3 dashboard_server.py --port %PORT% --log-folder "%~1"
    exit /b %errorlevel%
)
where python >nul 2>nul
if %errorlevel%==0 (
    python dashboard_server.py --port %PORT% --log-folder "%~1"
    exit /b %errorlevel%
)
goto PYTHON_NOT_FOUND

:START_DEFAULT
where py >nul 2>nul
if %errorlevel%==0 (
    py -3 dashboard_server.py --port %PORT%
    exit /b %errorlevel%
)
where python >nul 2>nul
if %errorlevel%==0 (
    python dashboard_server.py --port %PORT%
    exit /b %errorlevel%
)

:PYTHON_NOT_FOUND
echo Python 3 was not found.
exit /b 9009
