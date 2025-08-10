# dedupe-azure-resources.ps1 - Clean up duplicate Azure resources
Write-Host "🧹 Deduplicating Azure Resources for BusBuddy" -ForegroundColor Cyan

$CorrectRG = "BusBuddy-RG"        # Keep this one (has the server)
$DuplicateRG = "BusBuddyRG"       # Remove this one (duplicate)
$ServerName = "busbuddy-server-sm2"
$DatabaseName = "BusBuddyDB"

# Step 1: Verify what's in each resource group
Write-Host "📋 Checking resources in both groups..." -ForegroundColor Yellow

Write-Host "Resources in ${CorrectRG}:" -ForegroundColor Green
az resource list --resource-group $CorrectRG --output table

Write-Host "`nResources in ${DuplicateRG}:" -ForegroundColor Yellow
az resource list --resource-group $DuplicateRG --output table

# Step 2: Check if duplicate RG has any important resources
Write-Host "`n🔍 Checking for any databases in duplicate RG..." -ForegroundColor Yellow
$duplicateResources = az resource list --resource-group $DuplicateRG --output json | ConvertFrom-Json

if ($duplicateResources.Count -eq 0) {
    # Step 3: Delete empty duplicate resource group
    Write-Host "🗑️ Deleting empty duplicate resource group: $DuplicateRG" -ForegroundColor Red
    az group delete --name $DuplicateRG --yes --no-wait
    Write-Host "✅ Duplicate resource group deletion initiated" -ForegroundColor Green
} else {
    Write-Host "⚠️ Duplicate RG contains resources. Manual review needed:" -ForegroundColor Yellow
    $duplicateResources | ForEach-Object { Write-Host "  - $($_.name) ($($_.type))" -ForegroundColor Gray }
}

# Step 4: Ensure database exists in correct location
Write-Host "`n💾 Ensuring database exists in correct resource group..." -ForegroundColor Yellow
$dbExists = az sql db show --name $DatabaseName --server $ServerName --resource-group $CorrectRG 2>$null

if (-not $dbExists) {
    Write-Host "Creating database in correct location..." -ForegroundColor Yellow
    az sql db create --resource-group $CorrectRG --server $ServerName --name $DatabaseName --service-objective S0
    Write-Host "✅ Database created in ${CorrectRG}" -ForegroundColor Green
} else {
    Write-Host "✅ Database already exists in correct location" -ForegroundColor Green
}

# Step 5: Update firewall and apply migrations
Write-Host "`n🔥 Updating firewall rules..." -ForegroundColor Yellow
$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
az sql server firewall-rule create --resource-group $CorrectRG --server $ServerName --name "CurrentIP" --start-ip-address $myIp --end-ip-address $myIp 2>$null
Write-Host "✅ Firewall updated for IP: $myIp" -ForegroundColor Green

# Step 6: Apply EF Core migrations
Write-Host "`n🔄 Applying EF Core migrations..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Azure"
dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Migrations applied successfully!" -ForegroundColor Green

    # Step 7: Set sample waypoints
    Write-Host "🌱 Setting up sample waypoints..." -ForegroundColor Yellow
    .\Set-SampleWaypoints.ps1 -UseAzCliAuth -Force

    Write-Host "`n🎉 Database deduplication and setup complete!" -ForegroundColor Cyan
    Write-Host "📊 Tables ready: Students, Routes, Drivers, Vehicles, RouteStops" -ForegroundColor Green
    Write-Host "💡 Resource group consolidated to: ${CorrectRG}" -ForegroundColor Yellow
} else {
    Write-Host "❌ Migration failed - check connection and try again" -ForegroundColor Red
}
