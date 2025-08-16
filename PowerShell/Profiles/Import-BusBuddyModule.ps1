#!/usr/bin/env pwsh
#requires -Version 7.5 -PSEdition Core
<#
.SYNOPSIS
    Import BusBuddy PowerShell modules for CI/CD workflows
.DESCRIPTION
    This script imports the necessary BusBuddy PowerShell modules and functions
    for use in GitHub Actions CI/CD pipelines. It provides the bb-* aliases
    and functions needed for build, test, and quality gate operations.
.NOTES
    Referenced by .github/workflows/ci-pr-gate.yml
    Returns $true on success, $false on failure
#>

param(
    [switch]$Quiet
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

try {
    # Determine repo root
    $repoRoot = $PSScriptRoot
    while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot 'BusBuddy.sln'))) {
        $parent = Split-Path $repoRoot -Parent
        if (-not $parent -or $parent -eq $repoRoot) {
            $repoRoot = $null
            break
        }
        $repoRoot = $parent
    }

    if (-not $repoRoot) {
        throw "BusBuddy repo root not found. Ensure BusBuddy.sln exists in repo root."
    }

    # Set environment variables
    $env:BUSBUDDY_REPO_ROOT = $repoRoot
    $env:DOTNET_VERSION = "9.0.108"
    $env:BUILD_CONFIGURATION = "Release"
    $env:SOLUTION_FILE = "BusBuddy.sln"

    # Import the main BusBuddy module
    $busBuddyModulePath = Join-Path $repoRoot 'PowerShell\Modules\BusBuddy'
    if (Test-Path $busBuddyModulePath) {
        Import-Module $busBuddyModulePath -Force -DisableNameChecking -ErrorAction Stop
        if (-not $Quiet) {
            Write-Host "✅ BusBuddy core module loaded (bb* aliases available)" -ForegroundColor Green
        }
    } else {
        Write-Warning "BusBuddy core module not found at: $busBuddyModulePath"
        return $false
    }

    # Import testing module if available
    $testingModulePath = Join-Path $repoRoot 'PowerShell\Modules\BusBuddy.Testing'
    if (Test-Path $testingModulePath) {
        Import-Module $testingModulePath -Force -DisableNameChecking -ErrorAction Stop
        if (-not $Quiet) {
            Write-Host "✅ BusBuddy testing module loaded" -ForegroundColor Green
        }
    }

    # Import CLI module if available
    $cliModulePath = Join-Path $repoRoot 'PowerShell\Modules\BusBuddy.CLI'
    if (Test-Path $cliModulePath) {
        Import-Module $cliModulePath -Force -DisableNameChecking -ErrorAction Stop
        if (-not $Quiet) {
            Write-Host "✅ BusBuddy CLI module loaded" -ForegroundColor Green
        }
    }

    # Verify critical commands are available
    $requiredCommands = @('bbAntiRegression', 'bbMvpCheck', 'bbBuild', 'bbTest')
    $missingCommands = @()
    
    foreach ($cmd in $requiredCommands) {
        if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
            $missingCommands += $cmd
        }
    }

    if ($missingCommands.Count -gt 0) {
        Write-Warning "Missing required commands: $($missingCommands -join ', ')"
        if (-not $Quiet) {
            Write-Host "Available bb* commands:" -ForegroundColor Yellow
            Get-Command bb* -ErrorAction SilentlyContinue | Format-Table Name, Source -AutoSize
        }
        return $false
    }

    if (-not $Quiet) {
        Write-Host "✅ All required BusBuddy commands available:" -ForegroundColor Green
        Get-Command bbAntiRegression, bbMvpCheck, bbBuild, bbTest -ErrorAction SilentlyContinue | 
            Format-Table Name, Source -AutoSize
    }    # Set guard to prevent multiple executions
    $env:BUSBUDDY_MODULES_LOADED = '1'

    return $true
}
catch {
    Write-Error "Failed to import BusBuddy modules: $($_.Exception.Message)"
    return $false
}
