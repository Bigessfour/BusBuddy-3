#!/usr/bin/env pwsh
# Fix-Azure-Firewall-IP.ps1
# Adds current public IP to Azure SQL firewall rules

param(
    [string]$ServerName = "busbuddy-server-sm2",
    [string]$ResourceGroup = "BusBuddy-RG",
    [switch]$WhatIf
)

Write-Host "🔥 BusBuddy Azure SQL Firewall Fix" -ForegroundColor Cyan

try {
    # Get current public IP
    Write-Host "📡 Getting current public IP..." -ForegroundColor Yellow
    $currentIP = (Invoke-RestMethod -Uri "http://ipinfo.io/ip").Trim()
    Write-Host "🌐 Current public IP: $currentIP" -ForegroundColor Green

    # Check if Azure CLI is available
    if (-not (Get-Command "az" -ErrorAction SilentlyContinue)) {
        Write-Host "❌ Azure CLI not found. Please install Azure CLI first." -ForegroundColor Red
        exit 1
    }

    # Check Azure login status
    $azAccount = az account show --query "name" -o tsv 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "🔐 Please login to Azure first..." -ForegroundColor Yellow
        az login
    } else {
        Write-Host "✅ Azure login verified: $azAccount" -ForegroundColor Green
    }

    if ($WhatIf) {
        Write-Host "🔍 WhatIf: Would add firewall rule for IP $currentIP to server $ServerName" -ForegroundColor Magenta
        return
    }

    # Add firewall rule
    Write-Host "🛡️ Adding firewall rule for IP $currentIP..." -ForegroundColor Yellow
    $ruleName = "BusBuddy-Dev-$(Get-Date -Format 'yyyyMMdd-HHmm')"

    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $ServerName `
        --name $ruleName `
        --start-ip-address $currentIP `
        --end-ip-address $currentIP

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Firewall rule '$ruleName' created successfully!" -ForegroundColor Green
        Write-Host "⏳ Please wait 5 minutes for the rule to take effect." -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create firewall rule." -ForegroundColor Red
        exit 1
    }

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
