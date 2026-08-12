@echo off
setlocal
cd /d "%~dp0\.."
dotnet test Tests\AppiumBuilder.Tests.csproj
