@echo off
REM BusBuddy WPF with .NET Hot Reload (dotnet watch) — use while iterating on UI/code.
REM Saves to the shared folder or C:\dev\BusBuddy-3; watch restarts or hot-reloads on change.

cd /d "%~dp0"
echo Running BusBuddy Hot Reload launcher from:
echo   %CD%
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0utm_run_in_vm.ps1" -Watch
set EXITCODE=%ERRORLEVEL%

echo.
if %EXITCODE% NEQ 0 (
  echo Hot Reload launcher failed with exit code %EXITCODE%.
  echo Copy the red error text above and paste it into Cursor chat.
) else (
  echo dotnet watch exited.
)
echo.
pause
exit /b %EXITCODE%
