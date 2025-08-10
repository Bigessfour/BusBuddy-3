# apply-migrations-to-existing-db.ps1 - Apply EF migrations to existing Azure database
Write-Host "🚀 Applying EF Core Migrations to Existing Azure Database" -ForegroundColor Cyan

$ResourceGroup = "BusBuddy-RG"
$ServerName = "busbuddy-server-sm2"
$DatabaseName = "BusBuddyDB-Staging"  # The actual database name from the portal

Write-Host "📊 Target Database: $DatabaseName" -ForegroundColor Yellow
Write-Host "🖥️ Server: $ServerName" -ForegroundColor Yellow
Write-Host "📁 Resource Group: $ResourceGroup" -ForegroundColor Yellow

# Step 1: Update firewall for current IP
Write-Host "`n🔥 Updating firewall rules..." -ForegroundColor Yellow
$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
az sql server firewall-rule create --resource-group $ResourceGroup --server $ServerName --name "CurrentIP" --start-ip-address $myIp --end-ip-address $myIp 2>$null
Write-Host "✅ Firewall updated for IP: $myIp" -ForegroundColor Green

# Step 2: Set environment for Azure and apply EF Core migrations
Write-Host "`n🔄 Applying EF Core migrations..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Azure"
$env:DatabaseProvider = "Azure"

# Apply migrations with full project paths to avoid ambiguity
dotnet ef database update --project BusBuddy.Core\BusBuddy.Core.csproj --startup-project BusBuddy.WPF\BusBuddy.WPF.csproj --verbose

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Migrations applied successfully!" -ForegroundColor Green
    Write-Host "📊 Tables created/updated:" -ForegroundColor Green
    Write-Host "   • Students" -ForegroundColor Cyan
    Write-Host "   • Routes" -ForegroundColor Cyan
    Write-Host "   • Drivers" -ForegroundColor Cyan
    Write-Host "   • Vehicles (Buses)" -ForegroundColor Cyan
    Write-Host "   • RouteStops (Waypoints)" -ForegroundColor Cyan
    Write-Host "   • Families" -ForegroundColor Cyan
    Write-Host "   • Guardians" -ForegroundColor Cyan

    # Step 3: Set sample waypoints data (run without UseAzCliAuth to avoid conflict)
    Write-Host "`n🌱 Setting up sample waypoints data..." -ForegroundColor Yellow
    .\Set-SampleWaypoints.ps1 -Force

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n🎉 Complete! Azure database is ready!" -ForegroundColor Cyan
        Write-Host "💡 You can now run: bb-run" -ForegroundColor Yellow
        Write-Host "🌐 Database URL: https://portal.azure.com/#@bigessfourgmail.onmicrosoft.com/resource/subscriptions/57b297a5-44cf-4abc-9ac4-91a5ed147de1/resourceGroups/BusBuddy-RG/providers/Microsoft.Sql/servers/busbuddy-server-sm2/databases/BusBuddyDB-Staging/overview" -ForegroundColor Gray
    }
} else {
    Write-Host "`n❌ Migration failed" -ForegroundColor Red
    Write-Host "💡 Check your connection string in appsettings.azure.json" -ForegroundColor Yellow
}
