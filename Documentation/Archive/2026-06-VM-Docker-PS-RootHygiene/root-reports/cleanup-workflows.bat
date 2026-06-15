@echo off
REM Quick workflow cleanup for BusBuddy-3
REM This batch file provides common cleanup scenarios

echo.
echo 🚌 BusBuddy Workflow Cleanup
echo ============================
echo.

REM Check if PowerShell is available
where powershell >nul 2>nul
if %errorlevel% neq 0 (
    echo ❌ PowerShell is required but not found
    pause
    exit /b 1
)

REM Check if gh CLI is available
where gh >nul 2>nul
if %errorlevel% neq 0 (
    echo ❌ GitHub CLI (gh) is required but not found
    echo Download from: https://cli.github.com/
    pause
    exit /b 1
)

echo Select cleanup option:
echo.
echo 1. Preview all failed runs (WhatIf mode)
echo 2. Delete all failed runs
echo 3. Delete runs older than 30 days
echo 4. Delete runs older than 7 days
echo 5. Preview all runs (WhatIf mode)
echo 6. Delete ALL runs (DANGEROUS!)
echo 7. Custom options (run PowerShell script manually)
echo 8. Exit
echo.

set /p choice=Enter your choice (1-8): 

if "%choice%"=="1" (
    echo Running preview of failed runs...
    powershell -ExecutionPolicy Bypass -File "Scripts\Delete-WorkflowRuns.ps1" -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly -WhatIf
) else if "%choice%"=="2" (
    echo Deleting all failed runs...
    powershell -ExecutionPolicy Bypass -File "Scripts\Delete-WorkflowRuns.ps1" -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly
) else if "%choice%"=="3" (
    echo Deleting runs older than 30 days...
    powershell -ExecutionPolicy Bypass -File "Scripts\Delete-WorkflowRuns.ps1" -Repository "Bigessfour/BusBuddy-3" -OlderThanDays 30
) else if "%choice%"=="4" (
    echo Deleting runs older than 7 days...
    powershell -ExecutionPolicy Bypass -File "Scripts\Delete-WorkflowRuns.ps1" -Repository "Bigessfour/BusBuddy-3" -OlderThanDays 7
) else if "%choice%"=="5" (
    echo Running preview of all runs...
    powershell -ExecutionPolicy Bypass -File "Scripts\Delete-WorkflowRuns.ps1" -Repository "Bigessfour/BusBuddy-3" -DeleteAll -WhatIf
) else if "%choice%"=="6" (
    echo.
    echo ⚠️  WARNING: This will delete ALL workflow runs!
    echo This action cannot be undone.
    echo.
    set /p confirm=Type 'DELETE ALL' to confirm: 
    if "!confirm!"=="DELETE ALL" (
        powershell -ExecutionPolicy Bypass -File "Scripts\Delete-WorkflowRuns.ps1" -Repository "Bigessfour/BusBuddy-3" -DeleteAll
    ) else (
        echo Operation cancelled.
    )
) else if "%choice%"=="7" (
    echo.
    echo Run the PowerShell script manually with custom parameters:
    echo powershell -File "Scripts\Delete-WorkflowRuns.ps1" -Repository "Bigessfour/BusBuddy-3" [options]
    echo.
    echo Available options:
    echo   -DeleteFailedOnly     : Delete only failed runs
    echo   -OlderThanDays N      : Delete runs older than N days
    echo   -WorkflowId ID        : Target specific workflow
    echo   -DeleteAll            : Delete all runs (dangerous)
    echo   -WhatIf               : Preview what would be deleted
    echo   -Force                : Skip confirmations
    echo.
) else if "%choice%"=="8" (
    echo Exiting...
    exit /b 0
) else (
    echo Invalid choice. Please try again.
    goto start
)

echo.
echo Press any key to continue...
pause >nul