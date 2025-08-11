<#!
.SYNOPSIS
  Minimal, safe loader for the BusBuddy PowerShell module inside this repo.
.DESCRIPTION
  Imports PowerShell/Modules/BusBuddy/BusBuddy.psm1 (or .psd1) and suppresses noisy output.
  Uses Write-Information/Verbose per Microsoft guidance — no Write-Host.
  Intended to be dot-sourced by Profiles/Microsoft.PowerShell_profile.ps1.
#>
[CmdletBinding()]
param(
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    # This script lives at: <repo>/PowerShell/Import-BusBuddyMinimal.ps1
    # Resolve paths relative to PowerShell/ folder
    $powerShellDir = $PSScriptRoot
    $moduleDir     = Join-Path $powerShellDir 'Modules/BusBuddy'

    $psd1 = Join-Path $moduleDir 'BusBuddy.psd1'
    $psm1 = Join-Path $moduleDir 'BusBuddy.psm1'
    $moduleToLoad = if (Test-Path $psd1) { $psd1 } elseif (Test-Path $psm1) { $psm1 } else { $null }

    if (-not $moduleToLoad) {
        throw "BusBuddy module not found. Expected at: $moduleDir"
    }

    if (-not $Quiet) { Write-Information "Loading BusBuddy module: $moduleToLoad" -InformationAction Continue }
    Import-Module $moduleToLoad -Force -ErrorAction Stop

    # Light verification — commands should now be available
    $cmd = Get-Command bb-health -ErrorAction SilentlyContinue
    if (-not $cmd) {
        if (-not $Quiet) { Write-Information "Module loaded, but 'bb-health' alias not yet visible. Exported functions will still be available." -InformationAction Continue }
    }

    if (-not $Quiet) { Write-Information "BusBuddy module loaded successfully." -InformationAction Continue }
}
catch {
    Write-Error "Failed to import BusBuddy module: $($_.Exception.Message)"
}
