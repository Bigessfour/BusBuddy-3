<#!
.SYNOPSIS
  Verifies repo PowerShell profile and BusBuddy module import, then lists core commands.
.USAGE
  pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File PowerShell/Diagnostics/Verify-Module-Load.ps1
#>
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

try {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    . (Join-Path $root 'PowerShell/Profiles/Microsoft.PowerShell_profile.ps1')
    Import-Module (Join-Path $root 'PowerShell/Modules/BusBuddy/BusBuddy.psd1') -Force

    'PSVersion:'
    $PSVersionTable.PSVersion

    'Module:'
    Get-Module BusBuddy* | Select-Object Name,Version | Format-Table -Auto

    'Commands:'
    Get-Command bb-build,bb-run,bb-health -ErrorAction SilentlyContinue |
      Select-Object Name,CommandType,Source | Format-Table -Auto

    if (-not (Get-Command bb-health -ErrorAction SilentlyContinue)) {
        Write-Warning "bb-health alias not visible; trying function fallback"
        if (Get-Command Invoke-BusBuddyHealthCheck -ErrorAction SilentlyContinue) {
            Write-Information "Invoke-BusBuddyHealthCheck is available." -InformationAction Continue
        }
    }
}
catch {
    Write-Error $_
    exit 1
}
exit 0
