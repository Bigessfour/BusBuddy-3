@echo off
title BusBuddy
REM Desktop icon launcher for the Windows VM.
REM Do not call utm_run_in_vm.ps1 from Windows PowerShell 5.1 (#Requires 7.4).

set "LOCAL_ROOT=C:\dev\BusBuddy-3"
set "EXE_REL=%LOCAL_ROOT%\BusBuddy.WPF\bin\Release\net9.0-windows\BusBuddy.WPF.exe"
set "EXE_DBG=%LOCAL_ROOT%\BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.exe"
set "KEYFILE=%LOCAL_ROOT%\keys\SYNCFUSION_LICENSE_KEY.txt"
if exist "%KEYFILE%" (
  for /f "usebackq delims=" %%A in ("%KEYFILE%") do set "SYNCFUSION_LICENSE_KEY=%%A"
)

if exist "%EXE_REL%" (
  cd /d "%LOCAL_ROOT%\BusBuddy.WPF\bin\Release\net9.0-windows"
  start "" "%EXE_REL%"
  exit /b 0
)
if exist "%EXE_DBG%" (
  cd /d "%LOCAL_ROOT%\BusBuddy.WPF\bin\Debug\net9.0-windows"
  start "" "%EXE_DBG%"
  exit /b 0
)

if exist "%LOCAL_ROOT%\BusBuddy.WPF\BusBuddy.WPF.csproj" (
  cd /d "%LOCAL_ROOT%"
  start "" dotnet run --project "BusBuddy.WPF\BusBuddy.WPF.csproj" -c Release --no-restore
  exit /b 0
)

echo BusBuddy was not found at C:\dev\BusBuddy-3.
echo Sync the project from the UTM share, then try again.
pause
exit /b 1
