@echo off
cd /d "%~dp0\.."
echo [1/2] .NET tests
dotnet test Tests\AppiumBuilder.Tests.csproj
if errorlevel 1 goto fail
echo [2/2] Dashboard Python tests
python -m pytest Tests\python -q
if errorlevel 1 goto fail
echo All tests passed.
pause
exit /b 0
:fail
echo Tests failed.
pause
exit /b 1
