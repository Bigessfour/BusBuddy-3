#Requires -Version 5.1
<#
.SYNOPSIS
  Removes old BusBuddy desktop shortcuts and installs one reusable icon.

.DESCRIPTION
  Run this ONCE inside the UTM Windows VM (not on Mac).

  From shared folder (Z:\):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Install-BusBuddyDesktopIcon.ps1

  Or double-click:
    Scripts\Install-BusBuddyDesktopIcon.cmd
#>

$ErrorActionPreference = "Stop"

function Get-ShareRoot {
    $candidates = @(
        "Z:\",
        "Z:\Shared with Windows\",
        "Z:\BusBuddy-3\",
        "D:\Shared with Windows\",
        "D:\BusBuddy-3\"
    )
    foreach ($c in $candidates) {
        $launch = Join-Path $c "Scripts\Launch-BusBuddy.cmd"
        if (Test-Path -LiteralPath $launch) { return $c }
    }
    # Script may be run from repo root on share
    $here = Split-Path -Parent $PSScriptRoot
    if (Test-Path (Join-Path $here "Scripts\Launch-BusBuddy.cmd")) {
        return ($here.TrimEnd('\') + '\')
    }
    return $null
}

Write-Host ""
Write-Host "BusBuddy desktop icon installer (Windows VM only)" -ForegroundColor Cyan
Write-Host ""

$share = Get-ShareRoot
if (-not $share) {
    Write-Host "ERROR: Could not find Scripts\Launch-BusBuddy.cmd on Z:\ (or known shares)." -ForegroundColor Red
    Write-Host "In UTM, share the Mac BusBuddy-3 folder, open it in Explorer, then re-run this installer from that folder." -ForegroundColor Yellow
    exit 1
}
Write-Host "Share root: $share" -ForegroundColor Green

$fixedDir = "C:\dev\BusBuddy-Launch"
$fixedCmd = Join-Path $fixedDir "Run-BusBuddy.cmd"
$localRepo = "C:\dev\BusBuddy-3"

New-Item -ItemType Directory -Force -Path $fixedDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $localRepo "Scripts") | Out-Null

# Refresh fixed stub + main launcher from share
Copy-Item -LiteralPath (Join-Path $share "Scripts\Run-BusBuddy.cmd") -Destination $fixedCmd -Force
Copy-Item -LiteralPath (Join-Path $share "Scripts\Launch-BusBuddy.cmd") -Destination (Join-Path $localRepo "Scripts\Launch-BusBuddy.cmd") -Force
if (Test-Path (Join-Path $share "utm_run_in_vm.ps1")) {
    Copy-Item (Join-Path $share "utm_run_in_vm.ps1") (Join-Path $localRepo "utm_run_in_vm.ps1") -Force
}
if (Test-Path (Join-Path $share "utm_run_in_vm.cmd")) {
    Copy-Item (Join-Path $share "utm_run_in_vm.cmd") (Join-Path $localRepo "utm_run_in_vm.cmd") -Force
}
Write-Host "Installed fixed launcher: $fixedCmd" -ForegroundColor Green

# Remove old desktop shortcuts
$desktopDirs = @(
    [Environment]::GetFolderPath("Desktop"),
    [Environment]::GetFolderPath("CommonDesktopDirectory")
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique

$removed = 0
foreach ($desk in $desktopDirs) {
    Get-ChildItem -LiteralPath $desk -Filter "*.lnk" -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -match 'BusBuddy|bus.?buddy|Launch-BusBuddy|utm_run' -or
        $_.BaseName -match 'BusBuddy'
    } |
    ForEach-Object {
        Write-Host "Removing old shortcut: $($_.FullName)" -ForegroundColor Yellow
        Remove-Item -LiteralPath $_.FullName -Force
        $removed++
    }
}
Write-Host "Removed $removed old shortcut(s)." -ForegroundColor DarkGray

# Create new shortcut
$desktop = [Environment]::GetFolderPath("Desktop")
$lnkPath = Join-Path $desktop "BusBuddy.lnk"
if (Test-Path $lnkPath) { Remove-Item $lnkPath -Force }

$wsh = New-Object -ComObject WScript.Shell
$sc = $wsh.CreateShortcut($lnkPath)
$sc.TargetPath = $fixedCmd
$sc.WorkingDirectory = $fixedDir
$sc.WindowStyle = 1
$sc.Description = "BusBuddy — sync from UTM share, rebuild, launch"
# Prefer app exe icon if a previous build exists; else cmd.ico is fine
$exeIcon = "C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.exe"
if (Test-Path $exeIcon) {
    $sc.IconLocation = "$exeIcon,0"
} else {
    $sc.IconLocation = "%SystemRoot%\System32\shell32.dll,137"
}
$sc.Save()
Write-Host "Created desktop icon: $lnkPath" -ForegroundColor Green
Write-Host "  Target: $fixedCmd" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Done. Double-click the BusBuddy desktop icon." -ForegroundColor Cyan
Write-Host "First launch syncs + builds (can take a minute); later launches are faster after robocopy." -ForegroundColor DarkGray
Write-Host ""
