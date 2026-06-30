@echo off
setlocal
cd /d "%~dp0MediaPlayer"

echo Closing previous FPlayer instance if running...
taskkill /IM FPlayer.exe /F >nul 2>&1
taskkill /IM MediaPlayer.exe /F >nul 2>&1
if not errorlevel 1 (
    echo Waiting for process to release files...
    timeout /t 1 /nobreak >nul
)

echo Building FPlayer (WinUI)...
dotnet build MediaPlayer.csproj -c Debug
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

set "EXE_DIR=bin\Debug\net8.0-windows10.0.19041.0\win-x64"
set "EXE=%EXE_DIR%\FPlayer.exe"
if not exist "%EXE%" (
    echo ERROR: %EXE% not found.
    exit /b 1
)

echo Starting %EXE%
cd /d "%EXE_DIR%"
start "" "%CD%\FPlayer.exe"
