# Azure-Database-Setup.ps1 - Pure Azure CLI approach for BusBuddy
# Sets up Azure SQL Database and applies EF Core migrations

param(
    [string]$ResourceGroup = "BusBuddyRG",
    [string]$ServerName = "busbuddy-server-$(Get-Random -Minimum 1000 -Maximum 9999)",
    [string]$DatabaseName = "BusBuddyDB",
    [string]$Location = "eastus",
    [string]$AdminUser = "sqladmin",
    [string]$AdminPassword,
    [switch]$SkipInfrastructure,
    [switch]$UpdateFirewall
)

$ErrorActionPreference = 'Stop'

Write-Host "☁️ Azure SQL Database Setup for BusBuddy" -ForegroundColor Cyan

# Step 1: Verify Azure CLI
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI not found. Install from: https://aka.ms/installazurecliwindows"
}

# Check if logged in
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "🔐 Please log in to Azure..." -ForegroundColor Yellow
    az login
}

Write-Host "✅ Logged in as: $($account.user.name)" -ForegroundColor Green

# Step 2: Create Azure resources (if needed)
if (-not $SkipInfrastructure) {
    Write-Host "🏗️ Creating Azure resources..." -ForegroundColor Yellow

    # Create resource group
    $rgExists = az group exists --name $ResourceGroup
    if ($rgExists -eq "false") {
        Write-Host "📁 Creating resource group: $ResourceGroup" -ForegroundColor Yellow
        az group create --name $ResourceGroup --location $Location
    }

    # Create SQL Server
    Write-Host "🖥️ Creating SQL Server: $ServerName" -ForegroundColor Yellow
    if (-not $AdminPassword) {
        $AdminPassword = Read-Host "Enter SQL Admin Password" -AsSecureString | ConvertFrom-SecureString -AsPlainText
    }

    az sql server create `
        --name $ServerName `
        --resource-group $ResourceGroup `
        --location $Location `
        --admin-user $AdminUser `
        --admin-password $AdminPassword

    # Create database
    Write-Host "💾 Creating database: $DatabaseName" -ForegroundColor Yellow
    az sql db create `
        --resource-group $ResourceGroup `
        --server $ServerName `
        --name $DatabaseName `
        --service-objective S0

    Write-Host "✅ Azure infrastructure created" -ForegroundColor Green
}

# Step 3: Configure firewall
if ($UpdateFirewall) {
    Write-Host "🔥 Configuring firewall rules..." -ForegroundColor Yellow

    # Allow Azure services
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $ServerName `
        --name "AllowAzureServices" `
        --start-ip-address 0.0.0.0 `
        --end-ip-address 0.0.0.0

    # Allow current IP
    $myIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $ServerName `
        --name "AllowCurrentIP" `
        --start-ip-address $myIp `
        --end-ip-address $myIp

    Write-Host "✅ Firewall configured for IP: $myIp" -ForegroundColor Green
}

# Step 4: Update connection string and apply migrations
Write-Host "🔗 Getting connection string..." -ForegroundColor Yellow
$connectionString = az sql db show-connection-string `
    --client ado.net `
    --name $DatabaseName `
    --server $ServerName `
    --output tsv

# Replace placeholders
$connectionString = $connectionString.Replace("<username>", $AdminUser).Replace("<password>", $AdminPassword)

Write-Host "📝 Connection string ready" -ForegroundColor Green
Write-Host "🔄 Applying EF Core migrations to Azure..." -ForegroundColor Yellow

# Set environment variable for EF Core
$env:ConnectionStrings__DefaultConnection = $connectionString

# Apply migrations to Azure
& dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF --no-build

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Migrations applied to Azure SQL Database" -ForegroundColor Green
    Write-Host "🎉 Setup complete!" -ForegroundColor Cyan
    Write-Host "💡 Update your appsettings.azure.json with this connection string:" -ForegroundColor Yellow
    Write-Host $connectionString -ForegroundColor Gray
} else {
    throw "Migration to Azure failed"
}
