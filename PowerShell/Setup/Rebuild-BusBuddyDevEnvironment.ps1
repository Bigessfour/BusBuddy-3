#requires -Version 7.5
<#!
.SYNOPSIS
    Rebuilds a clean, reproducible BusBuddy PowerShell development environment.
.DESCRIPTION
    Ensures PowerShell 7.5.2+ semantics, validates .NET SDK (global.json), cleans cached/imported BusBuddy modules,
    reinstalls local module set, and performs health checks. Designed to be idempotent and safe to run repeatedly.
.NOTES
    Run from repository root OR any subdirectory inside the repo. Detects the root automatically.
    Uses only documented PowerShell streams (no Write-Host) and stops on errors.
.EXAMPLE
    ./PowerShell/Setup/Rebuild-BusBuddyDevEnvironment.ps1 -Full -Verbose
#>
[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [switch]$Full,             # Performs extended cleanup (NuGet locals, dotnet clean)
    [switch]$NoRestore,        # Skip dotnet restore (useful if already restored)
    [switch]$SkipBuild,        # Skip build after restore
    [switch]$SkipTests,        # Skip test execution
    [switch]$Force             # Bypass confirmation prompts
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Write-Section([string]$Title) {
    Write-Information "" -InformationAction Continue
    Write-Information "=== $Title ===" -InformationAction Continue
}

function Resolve-BusBuddyRoot {
    $path = (Get-Location).Path
    while ($path -and $path -ne [System.IO.Path]::GetPathRoot($path)) {
        if (Test-Path (Join-Path $path 'BusBuddy.sln')) { return $path }
        $path = Split-Path $path -Parent
    }
    throw 'Unable to locate BusBuddy.sln (run inside the repository).'
}

function Assert-PowerShellVersion {
    if ($PSVersionTable.PSVersion.Major -lt 7 -or ($PSVersionTable.PSVersion.Major -eq 7 -and $PSVersionTable.PSVersion.Minor -lt 5)) {
        throw "PowerShell 7.5.2+ required. Current: $($PSVersionTable.PSVersion)"
    }
}

function Assert-DotNetVersion($ExpectedVersion) {
    $actual = (& dotnet --version 2>$null)
    if (-not $actual) { throw '.NET SDK not found on PATH.' }
    if (-not $actual.StartsWith(($ExpectedVersion -replace '\\.$',''))) {
        Write-Warning "SDK mismatch: expected baseline $ExpectedVersion (global.json) got $actual"
    } else {
        Write-Information "✅ .NET SDK: $actual (matches global.json baseline $ExpectedVersion)" -InformationAction Continue
    }
}

function Clear-BusBuddyImportedModules {
    Get-Module | Where-Object { $_.Name -like 'BusBuddy*' } | ForEach-Object {
        Write-Verbose "Removing loaded module: $($_.Name)"
        Remove-Module $_.Name -Force -ErrorAction SilentlyContinue
    }
}

function Import-BusBuddyLocalModules($Root) {
    $modulesPath = Join-Path $Root 'PowerShell/Modules'
    if (-not (Test-Path $modulesPath)) { throw "Modules directory not found: $modulesPath" }

    Get-ChildItem -Path $modulesPath -Directory | ForEach-Object {
        $psd1 = Get-ChildItem -Path $_.FullName -Filter *.psd1 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($psd1) {
            try {
                Import-Module $psd1.FullName -Force -ErrorAction Stop
                Write-Information "Imported module: $($_.Name)" -InformationAction Continue
            }
            catch { Write-Warning "Failed to import module $($_.Name): $($_.Exception.Message)" }
        }
    }
}

function Invoke-DotNetRestore($Root) {
    if ($NoRestore) { Write-Information 'Skipping restore (NoRestore flag).' -InformationAction Continue; return }
    Write-Section 'dotnet restore'
    & dotnet restore (Join-Path $Root 'BusBuddy.sln') --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
}

function Invoke-DotNetBuild($Root) {
    if ($SkipBuild) { Write-Information 'Skipping build (SkipBuild flag).' -InformationAction Continue; return }
    Write-Section 'dotnet build'
    & dotnet build (Join-Path $Root 'BusBuddy.sln') --configuration Debug --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
}

function Invoke-DotNetTest($Root) {
    if ($SkipTests) { Write-Information 'Skipping tests (SkipTests flag).' -InformationAction Continue; return }
    Write-Section 'dotnet test'
    & dotnet test (Join-Path $Root 'BusBuddy.sln') --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
}

function Use-BusBuddyExtendedCleanup($Root) {
    if (-not $Full) { return }
    Write-Section 'Extended Cleanup'
    Write-Information 'Clearing NuGet caches...' -InformationAction Continue
    & dotnet nuget locals all --clear | Out-Null
    Write-Information 'Running dotnet clean...' -InformationAction Continue
    & dotnet clean (Join-Path $Root 'BusBuddy.sln') --verbosity minimal | Out-Null
}

function Show-LoadedBusBuddyModules {
    Write-Section 'Loaded BusBuddy Modules'
    Get-Module | Where-Object { $_.Name -like 'BusBuddy*' } | Select-Object Name, Version | Format-Table -AutoSize | Out-String | ForEach-Object { Write-Information $_ -InformationAction Continue }
}

function Invoke-BusBuddyAnalyzer($Root) {
    Write-Section 'PSScriptAnalyzer'
    $settings = Join-Path $Root 'PowerShell/Config/PSScriptAnalyzerSettings.psd1'
    if (-not (Test-Path $settings)) { Write-Warning 'Analyzer settings not found – skipping.'; return }
    $results = Invoke-ScriptAnalyzer -Path (Join-Path $Root 'PowerShell') -Settings $settings -Recurse -ErrorAction SilentlyContinue
    if (-not $results) { Write-Information 'No issues found (or analyzer produced no output).' -InformationAction Continue; return }
    $errors = $results | Where-Object Severity -eq 'Error'
    $warnings = $results | Where-Object Severity -eq 'Warning'
    Write-Information ("Errors: {0}  Warnings: {1}" -f $errors.Count, $warnings.Count) -InformationAction Continue
    if ($errors.Count -gt 0) { Write-Warning 'Analyzer errors detected.' }
}

# MAIN
try {
    Write-Section 'Environment Validation'
    Assert-PowerShellVersion
    $root = Resolve-BusBuddyRoot
    $globalConfig = Get-Content (Join-Path $root 'global.json') -Raw | ConvertFrom-Json
    $expectedSdk = $globalConfig.sdk.version
    Assert-DotNetVersion -ExpectedVersion $expectedSdk

    Clear-BusBuddyImportedModules
    Use-BusBuddyExtendedCleanup -Root $root
    Invoke-DotNetRestore -Root $root
    Invoke-DotNetBuild -Root $root
    Invoke-DotNetTest -Root $root

    Write-Section 'Module Import'
    Import-BusBuddyLocalModules -Root $root
    Show-LoadedBusBuddyModules

    Invoke-BusBuddyAnalyzer -Root $root

    Write-Section 'Health Commands'
    if (Get-Command bb-test-status -ErrorAction SilentlyContinue) { & bb-test-status | Out-Null }
    if (Get-Command bb-test-compliance -ErrorAction SilentlyContinue) { & bb-test-compliance | Out-Null }

    Write-Information '✅ Rebuild complete.' -InformationAction Continue
}
catch {
    Write-Error "Rebuild failed: $($_.Exception.Message)"
    exit 1
}
