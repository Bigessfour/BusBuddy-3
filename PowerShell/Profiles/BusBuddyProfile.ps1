#requires -Version 7.5 -PSEdition Core
# BusBuddy PowerShell Profile — Initializes modules, stubs, and aliases.
# PERFORMANCE: Lazy-loads Az/SqlServer modules only when functions are called (avoids 15+ second startup delay)
# Refs: Azure SQL[](https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql), Syncfusion WPF[](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf).

# Guard: prevent multiple loads in the same session.
# Docs: PowerShell about_Profiles — https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Profiles
if ($env:BUSBUDDY_PROFILE_LOADED -eq '1') { return }
$env:BUSBUDDY_PROFILE_LOADED = '1'

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

# Set default environment variables from BusBuddy CI defaults
$env:DOTNET_VERSION = "9.0.108"  # Latest as of August 2025; aligns with CI defaults and https://dotnet.microsoft.com/en-us/download/dotnet/9.0
$env:BUILD_CONFIGURATION = "Release"
$env:SOLUTION_FILE = "BusBuddy.sln"

# Adjust to your local BusBuddy repo path (clone from https://github.com/Bigessfour/BusBuddy-3)
# Prefer dynamic root set by wrapper; fall back to probing or legacy default when loaded standalone.
$BusBuddyRepoPath = $env:BUSBUDDY_REPO_ROOT
if (-not $BusBuddyRepoPath) {
    Write-Warning "BUSBUDDY_REPO_ROOT not set (expected from wrapper). Falling back to probing or default."
    # Probe upwards from current location for BusBuddy.sln
    $probe = (Get-Location).Path
    while ($probe -and -not (Test-Path (Join-Path $probe 'BusBuddy.sln'))) {
        $next = Split-Path $probe -Parent
        if (-not $next -or $next -eq $probe) { $probe = $null; break }
        $probe = $next
    }
    if ($probe) {
        $BusBuddyRepoPath = $probe
    }
    else {
        $BusBuddyRepoPath = "C:\Dev\BusBuddy-3"  # Legacy default if probing fails
    }
}

# Add BusBuddy PowerShell scripts folder to PSModulePath for easy import (e.g., Enable-AzureSqlAccess.ps1)
if (Test-Path "$BusBuddyRepoPath\PowerShell") {
    $env:PSModulePath = $env:PSModulePath + ";$BusBuddyRepoPath\PowerShell"
}

# Lazy-load modules only when needed (performance optimization)
# Note: Az module import can take 10-15+ seconds, so we defer until Connect-BusBuddySql is called
# Reference: https://learn.microsoft.com/powershell/azure/performance-tips

# Function: Connect to BusBuddy Azure SQL Database using token-based auth (no password required)
# Usage: Connect-BusBuddySql -Query "SELECT TOP 1 * FROM sys.tables"
# Reference: https://learn.microsoft.com/en-us/azure/azure-sql/database/connect-query-powershell?view=azuresql
function Connect-BusBuddySql {
    param (
        [string]$Query = "SELECT TOP 1 name FROM sys.tables ORDER BY name;",
        [string]$Database = "BusBuddyDb"  # Default from CI YAML
    )

    # Lazy-load Az module only when needed (performance optimization)
    if (-not (Get-Module Az -ListAvailable)) {
        Write-Warning "Az module not installed. Run: Install-Module Az -Scope CurrentUser"
        return
    }
    if (-not (Get-Module Az)) {
        Write-Information "Loading Az module (this may take 10-15 seconds)..." -InformationAction Continue
        Import-Module Az -ErrorAction Stop
    }

    # Lazy-load SqlServer module only when needed
    if (-not (Get-Module SqlServer -ListAvailable)) {
        Write-Warning "SqlServer module not installed. Run: Install-Module SqlServer -Scope CurrentUser"
        return
    }
    if (-not (Get-Module SqlServer)) {
        Write-Information "Loading SqlServer module..." -InformationAction Continue
        Import-Module SqlServer -ErrorAction Stop
    }

    # Use environment variables from CI YAML (set these externally)
    $server = if ($env:AZURE_SQL_SERVER) { "$($env:AZURE_SQL_SERVER).database.windows.net" } else { Write-Error "Set AZURE_SQL_SERVER environment variable"; return }

    # Authenticate to Azure and get access token
    Connect-AzAccount -ErrorAction Stop | Out-Null
    $token = (Get-AzAccessToken -ResourceUrl "https://database.windows.net/").Token

    # Execute query
    Invoke-Sqlcmd -ServerInstance $server -Database $Database -AccessToken $token -Query $Query
}

# Function: Enable Azure SQL Firewall Rule for current IP (inspired by CI YAML)
# Usage: Enable-BusBuddyFirewall -ResourceGroup "BusBuddy-RG" -SqlServer "busbuddy-server-sm2"
# Requires Az module and Azure login
function Enable-BusBuddyFirewall {
    param (
        [string]$ResourceGroup = "BusBuddy-RG",
        [string]$SqlServer = "busbuddy-server-sm2",
        [string]$RuleName = "dev-rule-$(Get-Date -Format 'yyyyMMdd')"
    )

    # Lazy-load Az module only when needed
    if (-not (Get-Module Az -ListAvailable)) {
        Write-Warning "Az module not installed. Run: Install-Module Az -Scope CurrentUser"
        return
    }
    if (-not (Get-Module Az)) {
        Write-Information "Loading Az module (this may take 10-15 seconds)..." -InformationAction Continue
        Import-Module Az -ErrorAction Stop
    }

    # Get current public IP (requires internet)
    $ip = (Invoke-RestMethod -Uri "http://ipinfo.io/json").ip

    New-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroup -ServerName $SqlServer -FirewallRuleName $RuleName -StartIpAddress $ip -EndIpAddress $ip
}

# Function: Check and Set Syncfusion WPF License (supports v30.1.42 or latest v30.2.5)
# Registers via code in WPF app (App.xaml.cs), but checks env var for CI/builds
# Reference: https://help.syncfusion.com/wpf/licensing/license-key-registration
function Set-SyncfusionLicense {
    if ($env:SYNCFUSION_LICENSE_KEY) {
        Write-Information "Syncfusion license key detected (set for builds/runtimes)."
    } else {
        Write-Warning "SYNCFUSION_LICENSE_KEY not set. Generate from Syncfusion dashboard and set as env var."
        Write-Information "In WPF app (e.g., BusBuddy.WPF/App.xaml.cs), add: Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(Environment.GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY'));"
    }

    Write-Information "Syncfusion WPF Latest Version: 30.2.5 (update NuGet packages if needed)"  # Reference: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
}

# Functions for common BusBuddy dev tasks (inspired by CI YAML)
function Build-BusBuddy { dotnet build $env:SOLUTION_FILE --configuration $env:BUILD_CONFIGURATION --no-restore }
function Test-BusBuddy { dotnet test $env:SOLUTION_FILE --configuration $env:BUILD_CONFIGURATION --no-build --verbosity normal }
function Restore-BusBuddy { dotnet restore $env:SOLUTION_FILE }

# Run on profile load
Set-SyncfusionLicense
Write-Information "BusBuddy Dev Profile Loaded: Use Connect-BusBuddySql for Azure SQL queries, Build-BusBuddy for builds." -InformationAction Continue

# Example: Uncomment to test connection on load
# Connect-BusBuddySql -Query "SELECT DB_NAME() AS DatabaseName"

Write-Information 'BusBuddy profile loaded.' -InformationAction Continue
