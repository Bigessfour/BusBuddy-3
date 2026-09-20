@echo off
REM Double-click or run from C:\dev or C:\dev\BusBuddy-3.
REM Rebuilds BusBuddy WPF (Debug) on NTFS and launches the desktop window.
REM Requires PowerShell 7+ (guest has Microsoft.PowerShell 7.6.6 via winget/MSIX).

setlocal
cd /d "%~dp0"

set "PWSH="
if exist "%ProgramFiles%\PowerShell\7\pwsh.exe" (
  set "PWSH=%ProgramFiles%\PowerShell\7\pwsh.exe"
)
if not defined PWSH if exist "%ProgramFiles%\WindowsApps\Microsoft.PowerShell_7.6.6.0_arm64__8wekyb3d8bbwe\pwsh.exe" (
  set "PWSH=%ProgramFiles%\WindowsApps\Microsoft.PowerShell_7.6.6.0_arm64__8wekyb3d8bbwe\pwsh.exe"
)
if not defined PWSH (
  where pwsh >nul 2>&1
  if errorlevel 1 (
    echo PowerShell 7+ ^(pwsh^) is required.
    echo Install:
    echo   winget install --id Microsoft.PowerShell --source winget
    echo Then re-run this file.
    pause
    exit /b 1
  )
  set "PWSH=pwsh"
)

echo Launch-BusBuddy from %CD%
echo Using "%PWSH%"
echo.
"%PWSH%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Launch-BusBuddy.ps1" %*
set EXITCODE=%ERRORLEVEL%
echo.
if not %EXITCODE%==0 (
  echo Launcher failed with exit code %EXITCODE%.
  pause
)
exit /b %EXITCODE%
