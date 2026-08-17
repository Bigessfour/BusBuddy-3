@echo off
REM Double-click this in the UTM Windows VM (from the shared BusBuddy-3 folder).
REM Uses Windows PowerShell 5.1 — no PowerShell 7 required.

cd /d "%~dp0"
echo Running BusBuddy launcher from:
echo   %CD%
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0utm_run_in_vm.ps1"
set EXITCODE=%ERRORLEVEL%

echo.
if %EXITCODE% NEQ 0 (
  echo Launcher failed with exit code %EXITCODE%.
  echo Copy the red error text above and paste it into Cursor chat.
) else (
  echo Done. Check the VM desktop for the BusBuddy window.
)
echo.
pause
exit /b %EXITCODE%
