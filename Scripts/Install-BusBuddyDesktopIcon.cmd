@echo off
REM Run ONCE inside the UTM Windows VM (File Explorer on the shared folder).
REM Removes old BusBuddy desktop icons and installs a reusable one.

cd /d "%~dp0\.."
echo Installing BusBuddy desktop icon from:
echo   %CD%
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-BusBuddyDesktopIcon.ps1"
set EXITCODE=%ERRORLEVEL%

echo.
if %EXITCODE% NEQ 0 (
  echo Install failed. Copy the error text above into Cursor chat.
) else (
  echo Look on the Windows desktop for the new "BusBuddy" icon.
)
echo.
pause
exit /b %EXITCODE%
