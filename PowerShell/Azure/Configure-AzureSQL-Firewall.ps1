# Configure-AzureSQL-Firewall.ps1
# Based on Microsoft Azure SQL Documentation
# https://learn.microsoft.com/en-us/azure/azure-sql/database/azure-sql-dotnet-quickstart

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId = "c1c3c2c7-6f3e-4d3e-8f3e-6f3e4d3e8f3e",

    [Parameter(Mandatory=$false)]
    [string]$ServerName = "busbuddy-server-sm2",

    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "busbuddy-rg"
)

Write-Information "🔧 Azure SQL Firewall Configuration for BusBuddy Production Database" -InformationAction Continue
Write-Information "📚 Following Microsoft Azure SQL Documentation guidance" -InformationAction Continue

# Step 1: Get your current public IP address
Write-Information "🌐 Detecting your current public IP address..." -InformationAction Continue
try {
    $publicIP = (Invoke-RestMethod -Uri "https://api.ipify.org" -UseBasicParsing).Trim()
    Write-Information "✅ Your public IP address: $publicIP" -InformationAction Continue
} catch {
    Write-Error "❌ Failed to detect public IP: $($_.Exception.Message)"
    Write-Information "💡 You can manually find your IP at: https://whatismyipaddress.com/" -InformationAction Continue
    return
}

# Step 2: Azure CLI login check
Write-Information "🔐 Checking Azure CLI authentication..." -InformationAction Continue
try {
    $currentAccount = az account show --query "user.name" -o tsv 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Information "🔑 Azure CLI login required. Opening browser..." -InformationAction Continue
        az login
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Azure login failed"
            return
        }
    } else {
        Write-Information "✅ Already logged in as: $currentAccount" -InformationAction Continue
    }
} catch {
    Write-Error "❌ Azure CLI not available. Please install Azure CLI first."
    Write-Information "💡 Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -InformationAction Continue
    return
}

# Step 3: Set correct subscription
Write-Information "🎯 Setting Azure subscription..." -InformationAction Continue
try {
    az account set --subscription $SubscriptionId
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Failed to set subscription: $SubscriptionId"
        return
    }
    Write-Information "✅ Subscription set successfully" -InformationAction Continue
} catch {
    Write-Error "❌ Error setting subscription: $($_.Exception.Message)"
    return
}

# Step 4: Add firewall rule for your IP
Write-Information "🛡️ Adding firewall rule for your IP address..." -InformationAction Continue
$ruleName = "BusBuddy-LocalDev-$(Get-Date -Format 'yyyyMMdd')"

try {
    $result = az sql server firewall-rule create `
        --resource-group $ResourceGroupName `
        --server $ServerName `
        --name $ruleName `
        --start-ip-address $publicIP `
        --end-ip-address $publicIP `
        --output json

    if ($LASTEXITCODE -eq 0) {
        Write-Information "✅ Firewall rule '$ruleName' created successfully!" -InformationAction Continue
        Write-Information "🔓 Your IP $publicIP can now connect to Azure SQL" -InformationAction Continue
    } else {
        Write-Error "❌ Failed to create firewall rule"
        return
    }
} catch {
    Write-Error "❌ Error creating firewall rule: $($_.Exception.Message)"
    return
}

# Step 5: Verify firewall rules
Write-Information "🔍 Verifying current firewall rules..." -InformationAction Continue
try {
    $rules = az sql server firewall-rule list `
        --resource-group $ResourceGroupName `
        --server $ServerName `
        --output table

    Write-Information "📋 Current firewall rules:" -InformationAction Continue
    Write-Output $rules
} catch {
    Write-Warning "⚠️ Could not list firewall rules, but creation appeared successful"
}

# Step 6: Test connection
Write-Information "🧪 Testing Azure SQL connection..." -InformationAction Continue
Write-Information "⏱️ Please wait 30-60 seconds for firewall changes to propagate..." -InformationAction Continue

# Wait for propagation
Start-Sleep -Seconds 10

try {
    & "$PSScriptRoot\Test-AzureConnection.ps1"
} catch {
    Write-Warning "⚠️ Test script not found, you can manually test connection now"
}

Write-Information "🎉 Firewall configuration complete!" -InformationAction Continue
Write-Information "📝 Next steps:" -InformationAction Continue
Write-Information "   1. Test the connection with bb-test-azure" -InformationAction Continue
Write-Information "   2. Run Entity Framework migrations" -InformationAction Continue
Write-Information "   3. Start the BusBuddy application" -InformationAction Continue
Write-Information "" -InformationAction Continue
Write-Information "💡 If connection still fails, wait 2-3 minutes for Azure to propagate the firewall changes" -InformationAction Continue
