#Requires -Version 5.1
# Interactive-session launcher for BusBuddy WPF (called by schtasks /IT).
# Do not run this over SSH: Start-Process would not attach to the Macbook desktop.
# Working directory must be the exe folder — schtasks /TR on the exe alone uses System32.

$ErrorActionPreference = 'Stop'

$winRoot = Split-Path -Parent $PSScriptRoot
$exeDir = Join-Path $winRoot 'BusBuddy.WPF\bin\Debug\net9.0-windows'
$exe = Join-Path $exeDir 'BusBuddy.WPF.exe'
$outDir = Join-Path $winRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$statusFile = Join-Path $outDir 'utm-launch-last.txt'

function Write-LaunchStatus {
    param([string]$Text)
    Set-Content -LiteralPath $statusFile -Value $Text -Encoding UTF8
}

$env:DatabaseProvider = 'Postgres'
$currentProvider = [Environment]::GetEnvironmentVariable('DatabaseProvider', 'User')
if ($currentProvider -ne 'Postgres') {
    [Environment]::SetEnvironmentVariable('DatabaseProvider', 'Postgres', 'User')
}

Get-Process -Name 'BusBuddy.WPF' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

if (-not (Test-Path -LiteralPath $exe)) {
    Write-LaunchStatus 'LAUNCH_FAIL no exe'
    exit 3
}

$proc = Start-Process -FilePath $exe -WorkingDirectory $exeDir -WindowStyle Normal -PassThru
$handle = 0
for ($i = 0; $i -lt 50; $i++) {
    Start-Sleep -Milliseconds 400
    $proc.Refresh()
    if ($proc.HasExited) {
        Write-LaunchStatus ("LAUNCH_FAIL exited code=" + $proc.ExitCode + " pid=" + $proc.Id)
        exit 3
    }
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
        $handle = [int64]$proc.MainWindowHandle
        try {
            if (-not ('UtmLaunchWin32' -as [type])) {
                Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class UtmLaunchWin32 {
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
            }
            [UtmLaunchWin32]::ShowWindowAsync($proc.MainWindowHandle, 9) | Out-Null
            [UtmLaunchWin32]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
        } catch {
        }
        break
    }
}

Write-LaunchStatus ("LAUNCH pid=" + $proc.Id + " handle=" + $handle + " title=" + $proc.MainWindowTitle)
if ($proc.HasExited) { exit 3 }
exit 0
