#!/usr/bin/env pwsh
# Set-BusBuddy-Environment.ps1
# Configures environment variables for BusBuddy database connectivity

param(
    [ValidateSet("Local", "Azure")]
    [string]$Provider = "Local",
    [string]$SyncfusionLicenseKey = "",
    [switch]$ShowCurrent
)

if ($ShowCurrent) {
    Write-Host "🔍 Current BusBuddy Environment Variables:" -ForegroundColor Cyan
    Get-ChildItem Env: | Where-Object {
        $_.Name -like "*AZURE*" -or
        $_.Name -like "*SQL*" -or
        $_.Name -like "*SYNCFUSION*" -or
        $_.Name -like "*BUSBUDDY*" -or
        $_.Name -eq "ASPNETCORE_ENVIRONMENT"
    } | Format-Table Name, Value -AutoSize
    return
}

Write-Host "🌍 Setting BusBuddy Environment - Provider: $Provider" -ForegroundColor Cyan

try {
    if ($Provider -eq "Azure") {
        # Azure SQL configuration
        Write-Host "🌐 Configuring for Azure SQL..." -ForegroundColor Blue
        $env:ASPNETCORE_ENVIRONMENT = "Production"
        $env:DatabaseProvider = "Azure"

        # Verify Azure credentials
        if (-not $env:AZURE_SQL_USER) {
            Write-Host "⚠️ AZURE_SQL_USER not set. Using default: busbuddy_admin" -ForegroundColor Yellow
            $env:AZURE_SQL_USER = "busbuddy_admin"
        }

        if (-not $env:AZURE_SQL_PASSWORD) {
            Write-Host "❌ AZURE_SQL_PASSWORD not set. This is required for Azure SQL." -ForegroundColor Red
            Write-Host "Please set manually: `$env:AZURE_SQL_PASSWORD = 'your-password'" -ForegroundColor Yellow
        } else {
            Write-Host "✅ Azure SQL credentials configured" -ForegroundColor Green
        }

        # Set connection string with substitution
        $connectionString = "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;User ID=$env:AZURE_SQL_USER;Password=$env:AZURE_SQL_PASSWORD;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
        $env:BUSBUDDY_CONNECTION = $connectionString

    } else {
        # Local SQL Express configuration
        Write-Host "🏠 Configuring for Local SQL Express..." -ForegroundColor Blue
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:DatabaseProvider = "Local"

        # Test SQL Express connectivity
        $sqlService = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
        if ($sqlService -and $sqlService.Status -eq "Running") {
            Write-Host "✅ SQL Server Express is running" -ForegroundColor Green
        } else {
            Write-Host "⚠️ SQL Server Express service not found or not running" -ForegroundColor Yellow
        }

        $env:BUSBUDDY_CONNECTION = "Server=.\\SQLEXPRESS;Database=BusBuddy;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=30;"
    }

    # Syncfusion license
    if ($SyncfusionLicenseKey) {
        $env:SYNCFUSION_LICENSE_KEY = $SyncfusionLicenseKey
        Write-Host "✅ Syncfusion license key configured" -ForegroundColor Green
    } elseif (-not $env:SYNCFUSION_LICENSE_KEY) {
        Write-Host "⚠️ SYNCFUSION_LICENSE_KEY not set. Some UI features may show trial messages." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "🎯 Environment Configuration Summary:" -ForegroundColor Cyan
    Write-Host "Provider: $Provider" -ForegroundColor White
    Write-Host "Environment: $env:ASPNETCORE_ENVIRONMENT" -ForegroundColor White
    Write-Host "Database Provider: $env:DatabaseProvider" -ForegroundColor White
    Write-Host "Connection configured: $($env:BUSBUDDY_CONNECTION -ne $null)" -ForegroundColor White
    Write-Host ""
    Write-Host "✅ Environment configured for $Provider provider" -ForegroundColor Green

} catch {
    Write-Host "❌ Error configuring environment: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
