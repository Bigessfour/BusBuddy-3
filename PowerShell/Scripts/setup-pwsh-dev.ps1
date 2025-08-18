<#
.SYNOPSIS
Initializes PowerShell session for BusBuddy modules (PowerShell 7.5.2 compliant).
.DESCRIPTION
Adds repo module paths to PSModulePath (session), imports BusBuddy modules safely, and reports status.
Run:  . ./PowerShell/setup-pwsh-dev.ps1
#>
param(
    [switch]$Quiet
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSCommandPath
$modulesRoot = Join-Path $repoRoot 'Modules'
if (-not (Test-Path $modulesRoot)) { throw "Modules directory not found: $modulesRoot" }
# Ensure PSModulePath includes repo Modules (session only)
if (-not ($env:PSModulePath -split ';' | Where-Object { $_ -eq $modulesRoot })) {
    $env:PSModulePath = "$modulesRoot;" + $env:PSModulePath
}
$modules = @(
    'BusBuddy.ExceptionCapture',
    'BusBuddy.Rules',
    'BusBuddy.BuildOutput',
    'BusBuddy.Testing',
    'BusBuddy'
) | Where-Object { Test-Path (Join-Path $modulesRoot $_ ("$_.psd1")) }
$results = @()
foreach ($m in $modules) {
    try {
        Import-Module (Join-Path $modulesRoot $m ("$m.psd1")) -Force -ErrorAction Stop
        $results += [pscustomobject]@{Module = $m; Status = 'Imported' }
    }
    catch {
        $results += [pscustomobject]@{Module = $m; Status = 'Failed'; Error = $_.Exception.Message }
    }
}
if (-not $Quiet) { $results | Format-Table -AutoSize }
# Recommend profile snippet (no automatic write by default)
$profileSnippet = @"
# BusBuddy Dev Environment (add manually to your profile if desired)
# Use the repository folder detected by setup script
`$repo = '$repoRoot'
`$modulesPath = Join-Path `$repo 'PowerShell/Modules'
if(-not (`$env:PSModulePath -split ';' | Where-Object { `$_ -eq `$modulesPath })) {
    `$env:PSModulePath = "`$modulesPath;" + `$env:PSModulePath
}
# Import key modules silently
`$busModules = 'BusBuddy.ExceptionCapture','BusBuddy.Rules','BusBuddy'
foreach(`$m in `$busModules){ `$mf = Join-Path `$modulesPath `$m ("`$m.psd1"); if(Test-Path `$mf){ Import-Module `$mf -ErrorAction SilentlyContinue }}
"@
Set-Content -LiteralPath (Join-Path $repoRoot 'suggested-profile-snippet.ps1') -Value $profileSnippet -Encoding UTF8
if (-not $Quiet) { Write-Information "Created suggested-profile-snippet.ps1 (review & append to your profile)." -InformationAction Continue }
