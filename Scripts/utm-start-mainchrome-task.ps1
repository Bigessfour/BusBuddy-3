#Requires -Version 5.1
# Creates/runs the interactive MainWindow chrome UIA smoke (schtasks /IT).
$ErrorActionPreference = 'Continue'
$ps1 = 'C:\dev\BusBuddy-3\Scripts\utm-mainwindow-chrome-smoke.ps1'
$wrapper = 'C:\dev\BusBuddy-3\Scripts\_utm-mainchrome-run.ps1'
if (-not (Test-Path -LiteralPath $ps1)) {
    throw "missing $ps1"
}

Set-Content -LiteralPath $wrapper -Value "& '$ps1' -StartupWaitSeconds 5" -Encoding UTF8
cmd /c "schtasks /Delete /TN BusBuddyMainChromeSmoke /F >nul 2>&1"
$tr = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $wrapper + '"'
cmd /c "schtasks /Create /TN BusBuddyMainChromeSmoke /SC ONCE /ST 23:59 /RL LIMITED /IT /F /TR `"$tr`" /RU Macbook"
if ($LASTEXITCODE -ne 0) { throw "schtasks create failed exit=$LASTEXITCODE" }
cmd /c "schtasks /Run /TN BusBuddyMainChromeSmoke"
if ($LASTEXITCODE -ne 0) { throw "schtasks run failed exit=$LASTEXITCODE" }
Write-Output 'TASK_STARTED'
