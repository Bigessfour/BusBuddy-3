# az-database-setup.ps1 - Pure Azure CLI approach for BusBuddy database setup
# No PowerShell complications, just Azure CLI commands that work

param(
    [string]$ResourceGroup = "BusBuddy-RG",  # Correct resource group name with hyphen
    [string]$ServerName = "busbuddy-server-sm2", # Use your existing server
    [string]$DatabaseName = "BusBuddyDB",
    [string]$Location = "centralus"  # Server is in centralus, not eastus
)

Write-Host "🚀 Azure CLI Database Setup for BusBuddy" -ForegroundColor Cyan

# Step 1: Check Azure CLI and login
try {
    $account = az account show 2>$null | ConvertFrom-Json
    Write-Host "✅ Logged in as: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "🔐 Logging in to Azure..." -ForegroundColor Yellow
    az login
}

# Step 2: Ensure resource group exists
Write-Host "📁 Checking resource group..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "false") {
    Write-Host "Creating resource group: $ResourceGroup" -ForegroundColor Yellow
    az group create --name $ResourceGroup --location $Location
} else {
    Write-Host "✅ Resource group exists" -ForegroundColor Green
}

# Step 3: Check if server exists, create if needed
Write-Host "🖥️ Checking SQL Server..." -ForegroundColor Yellow
$serverExists = az sql server show --name $ServerName --resource-group $ResourceGroup 2>$null
if (-not $serverExists) {
    Write-Host "❌ Server $ServerName not found. Please create it first or use existing server name." -ForegroundColor Red
    Write-Host "💡 Your appsettings.azure.json shows: busbuddy-server-sm2" -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "✅ Server $ServerName exists" -ForegroundColor Green
}

# Step 4: Check if database exists, create if needed
Write-Host "💾 Checking database..." -ForegroundColor Yellow
$dbExists = az sql db show --name $DatabaseName --server $ServerName --resource-group $ResourceGroup 2>$null
if (-not $dbExists) {
    Write-Host "Creating database: $DatabaseName" -ForegroundColor Yellow
    az sql db create --resource-group $ResourceGroup --server $ServerName --name $DatabaseName --service-objective S0
    Write-Host "✅ Database created" -ForegroundColor Green
} else {
    Write-Host "✅ Database exists" -ForegroundColor Green
}

# Step 5: Update firewall for current IP
Write-Host "🔥 Updating firewall rules..." -ForegroundColor Yellow
$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
az sql server firewall-rule create --resource-group $ResourceGroup --server $ServerName --name "CurrentIP" --start-ip-address $myIp --end-ip-address $myIp 2>$null
Write-Host "✅ Firewall updated for IP: $myIp" -ForegroundColor Green

# Step 6: Apply EF Core migrations directly to Azure
Write-Host "🔄 Applying EF Core migrations to Azure..." -ForegroundColor Yellow

# Use your existing Azure connection string from appsettings.azure.json
$env:ASPNETCORE_ENVIRONMENT = "Azure"
$env:DatabaseProvider = "Azure"

# Run EF migrations
dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF --configuration Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Migrations applied successfully!" -ForegroundColor Green

    # Step 7: Run your waypoints script for Azure
    Write-Host "🌱 Setting up sample waypoints..." -ForegroundColor Yellow
    .\Set-SampleWaypoints.ps1 -UseAzCliAuth -Force

    Write-Host "🎉 Azure database setup complete!" -ForegroundColor Cyan
    Write-Host "📊 Tables created: Students, Routes, Drivers, Vehicles, RouteStops" -ForegroundColor Green
    Write-Host "💡 Run with: bb-run" -ForegroundColor Yellow
} else {
    Write-Host "❌ Migration failed. Check your connection string in appsettings.azure.json" -ForegroundColor Red
    exit 1
}
