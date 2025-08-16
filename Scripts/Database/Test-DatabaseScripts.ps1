#Requires -Version 7.0
<#
.SYNOPSIS
    Test runner for database connectivity scripts
.DESCRIPTION
    Demonstrates and tests the database connectivity helper scripts.
    Run this to verify the scripts work in your environment.
.PARAMETER TestAzure
    Test Azure SQL firewall script (requires valid Azure credentials)
.PARAMETER TestLocal
    Test LocalDB installation and configuration scripts
.EXAMPLE
    .\Test-DatabaseScripts.ps1 -TestLocal
.EXAMPLE
    .\Test-DatabaseScripts.ps1 -TestAzure -TestLocal
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$TestAzure,

    [Parameter()]
    [switch]$TestLocal
)

Write-Information "🧪 Testing Database Connectivity Scripts" -InformationAction Continue
Write-Information "=========================================" -InformationAction Continue

if ($TestLocal) {
    Write-Information "" -InformationAction Continue
    Write-Information "📍 Testing LocalDB Scripts..." -InformationAction Continue

    # Test LocalDB detection
    Write-Information "Checking LocalDB availability..." -InformationAction Continue
    if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
        Write-Information "✅ LocalDB command found" -InformationAction Continue

        # List instances
        $instances = sqllocaldb info
        Write-Information "LocalDB instances: $($instances -join ', ')" -InformationAction Continue
    } else {
        Write-Warning "❌ LocalDB not found - Install-LocalDB.ps1 can help with this"
    }

    # Test appsettings update (dry run)
    Write-Information "" -InformationAction Continue
    Write-Information "Testing appsettings update (dry run)..." -InformationAction Continue

    $scriptPath = Join-Path $PSScriptRoot "Update-AppSettingsForLocalDB.ps1"
    try {
        if (Test-Path $scriptPath) {
            & $scriptPath -WhatIf
            Write-Information "✅ Update-AppSettingsForLocalDB.ps1 syntax OK" -InformationAction Continue
        } else {
            Write-Warning "❌ Update-AppSettingsForLocalDB.ps1 not found at: $scriptPath"
        }
    } catch {
        Write-Warning "❌ Update-AppSettingsForLocalDB.ps1 test failed: $($_.Exception.Message)"
    }
}

if ($TestAzure) {
    Write-Information "" -InformationAction Continue
    Write-Information "📍 Testing Azure SQL Scripts..." -InformationAction Continue

    # Test Azure CLI availability
    if (Get-Command az -ErrorAction SilentlyContinue) {
        Write-Information "✅ Azure CLI found" -InformationAction Continue

        # Test authentication (non-intrusive)
        $authStatus = az account show 2>$null
        if ($LASTEXITCODE -eq 0) {
            $account = $authStatus | ConvertFrom-Json
            Write-Information "✅ Authenticated as: $($account.user.name)" -InformationAction Continue
        } else {
            Write-Warning "❌ Not authenticated with Azure - run 'az login' first"
        }
    } else {
        Write-Warning "❌ Azure CLI not found - install from https://docs.microsoft.com/cli/azure/install-azure-cli"
    }

    # Test public IP detection
    Write-Information "" -InformationAction Continue
    Write-Information "Testing public IP detection..." -InformationAction Continue
    try {
        $ip = (Invoke-RestMethod -Uri "https://api.ipify.org/?format=json" -TimeoutSec 5).ip
        Write-Information "✅ Current public IP: $ip" -InformationAction Continue
    } catch {
        Write-Warning "❌ Failed to detect public IP: $($_.Exception.Message)"
    }

    Write-Information "" -InformationAction Continue
    Write-Information "Note: To test Add-AzureSqlFirewallRule.ps1, you need:" -InformationAction Continue
    Write-Information "  - Valid Azure subscription and authentication" -InformationAction Continue
    Write-Information "  - Resource group name and SQL server name" -InformationAction Continue
    Write-Information "  - Contributor permissions on the resource group" -InformationAction Continue
}

Write-Information "" -InformationAction Continue
Write-Information "🎯 Quick Start Commands:" -InformationAction Continue
Write-Information "========================" -InformationAction Continue

if ($TestLocal) {
    Write-Information "" -InformationAction Continue
    Write-Information "For LocalDB development:" -InformationAction Continue
    Write-Information "  .\Scripts\Database\Install-LocalDB.ps1 -TestConnection" -InformationAction Continue
    Write-Information "  .\Scripts\Database\Update-AppSettingsForLocalDB.ps1 -BackupOriginal" -InformationAction Continue
}

if ($TestAzure) {
    Write-Information "" -InformationAction Continue
    Write-Information "For Azure SQL connectivity:" -InformationAction Continue
    Write-Information "  .\Scripts\Database\Add-AzureSqlFirewallRule.ps1 -ResourceGroup 'YourRG' -ServerName 'YourServer'" -InformationAction Continue
}

Write-Information "" -InformationAction Continue
Write-Information "📖 See README.md for detailed usage and troubleshooting" -InformationAction Continue
