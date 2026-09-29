@echo off
dotnet restore ESD.System.sln
if errorlevel 1 exit /b 1
dotnet build ESD.System.sln -c Release
if errorlevel 1 exit /b 1
echo.
echo BUILD SUCCESS
pause
