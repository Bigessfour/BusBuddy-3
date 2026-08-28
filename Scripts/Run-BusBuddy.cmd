@echo off
REM Fixed desktop-icon target. Rarely changes.
REM Always refreshes Launch-BusBuddy.cmd from the UTM share, then runs it
REM (sync + rebuild + start). That way the icon never sticks to an old exe.

setlocal EnableExtensions
title BusBuddy

set "SHARE="
if exist "Z:\Scripts\Launch-BusBuddy.cmd" set "SHARE=Z:\"
if not defined SHARE if exist "Z:\Shared with Windows\Scripts\Launch-BusBuddy.cmd" set "SHARE=Z:\Shared with Windows\"
if not defined SHARE if exist "Z:\BusBuddy-3\Scripts\Launch-BusBuddy.cmd" set "SHARE=Z:\BusBuddy-3\"

set "LOCAL=C:\dev\BusBuddy-3"
set "LAUNCH=%LOCAL%\Scripts\Launch-BusBuddy.cmd"

if defined SHARE (
  if not exist "%LOCAL%\Scripts" mkdir "%LOCAL%\Scripts"
  copy /Y "%SHARE%Scripts\Launch-BusBuddy.cmd" "%LAUNCH%" >nul
  if exist "%SHARE%utm_run_in_vm.ps1" copy /Y "%SHARE%utm_run_in_vm.ps1" "%LOCAL%\utm_run_in_vm.ps1" >nul
  if exist "%SHARE%utm_run_in_vm.cmd" copy /Y "%SHARE%utm_run_in_vm.cmd" "%LOCAL%\utm_run_in_vm.cmd" >nul
) else (
  echo WARNING: UTM share with Scripts\Launch-BusBuddy.cmd not found.
  echo Using existing local launcher if present.
)

if not exist "%LAUNCH%" (
  echo ERROR: Missing %LAUNCH%
  echo Open the shared BusBuddy-3 folder in Explorer and run:
  echo   Scripts\Install-BusBuddyDesktopIcon.cmd
  pause
  exit /b 1
)

call "%LAUNCH%"
exit /b %ERRORLEVEL%
