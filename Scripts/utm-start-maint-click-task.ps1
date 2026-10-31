#Requires -Version 5.1
$ErrorActionPreference = 'Continue'
$ps1 = 'C:\dev\BusBuddy-3\Scripts\utm-click-fleet-maintenance.ps1'
$wrapper = 'C:\dev\BusBuddy-3\Scripts\_utm-maint-run.ps1'
Set-Content -LiteralPath $wrapper -Value "& '$ps1'" -Encoding UTF8
cmd /c "schtasks /Delete /TN BusBuddyMaintClick /F >nul 2>&1"
$tr = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $wrapper + '"'
cmd /c "schtasks /Create /TN BusBuddyMaintClick /SC ONCE /ST 23:59 /RL LIMITED /IT /F /TR `"$tr`" /RU Macbook"
cmd /c "schtasks /Run /TN BusBuddyMaintClick"
Write-Output 'TASK_STARTED'
