# simple-azure-db.ps1 - Direct database setup assuming Azure CLI is ready
Write-Host "🚀 Creating BusBuddy Database in Azure" -ForegroundColor Cyan

$ResourceGroup = "BusBuddy-RG"
$ServerName = "busbuddy-server-sm2"
$DatabaseName = "BusBuddyDB"

# Step 1: Check if database exists, create if needed
Write-Host "💾 Checking/creating database..." -ForegroundColor Yellow
$dbExists = az sql db show --name $DatabaseName --server $ServerName --resource-group $ResourceGroup 2>$null
if (-not $dbExists) {
    Write-Host "Creating database: $DatabaseName" -ForegroundColor Yellow
    az sql db create --resource-group $ResourceGroup --server $ServerName --name $DatabaseName --service-objective S0
    Write-Host "✅ Database created" -ForegroundColor Green
} else {
    Write-Host "✅ Database already exists" -ForegroundColor Green
}

# Step 2: Update firewall for current IP
Write-Host "🔥 Updating firewall rules..." -ForegroundColor Yellow
$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
az sql server firewall-rule create --resource-group $ResourceGroup --server $ServerName --name "CurrentIP" --start-ip-address $myIp --end-ip-address $myIp 2>$null
Write-Host "✅ Firewall updated for IP: $myIp" -ForegroundColor Green

# Step 3: Apply EF Core migrations to Azure
Write-Host "🔄 Applying EF Core migrations to Azure..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Azure"
dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF --verbose

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Migrations applied successfully!" -ForegroundColor Green
    Write-Host "📊 Tables created: Students, Routes, Drivers, Vehicles, RouteStops" -ForegroundColor Green

    # Step 4: Set sample waypoints
    Write-Host "🌱 Setting up sample waypoints..." -ForegroundColor Yellow
    .\Set-SampleWaypoints.ps1 -UseAzCliAuth -Force

    Write-Host "🎉 Azure database setup complete!" -ForegroundColor Cyan
} else {
    Write-Host "❌ Migration failed" -ForegroundColor Red
}
