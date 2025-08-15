@echo off
REM Wrapper — use the canonical script under PowerShell\Scripts (avoids identical duplicates in repo)
setlocal
set SCRIPT_PATH=%~dp0PowerShell\Scripts\Query-Students-Azure.cmd
if not exist "%SCRIPT_PATH%" (
    echo Error: Canonical script not found at %SCRIPT_PATH%
    exit /b 1
)
call "%SCRIPT_PATH%"
set EXITCODE=%ERRORLEVEL%
endlocal & exit /b %EXITCODE%
