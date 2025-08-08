# Quick Local Database Fallback
# This allows you to continue development while fixing Azure SQL firewall

Write-Host "🚌 Setting up Local Database Fallback" -ForegroundColor Cyan

# Check current database provider
$appsettingsPath = "appsettings.azure.json"
if (Test-Path $appsettingsPath) {
    $config = Get-Content $appsettingsPath | ConvertFrom-Json
    Write-Host "Current DatabaseProvider: $($config.DatabaseProvider)" -ForegroundColor Yellow
}

# Create temporary local configuration
$localConfig = @{
    "DatabaseProvider" = "Local"
    "ConnectionStrings" = @{
        "DefaultConnection" = "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=BusBuddyLocal;Integrated Security=True;MultipleActiveResultSets=True"
    }
    "Logging" = @{
        "LogLevel" = @{
            "Default" = "Information"
            "Microsoft" = "Warning"
        }
    }
}

# Save local configuration
$localConfig | ConvertTo-Json -Depth 3 | Set-Content -Path "appsettings.local.json"
Write-Host "✅ Created appsettings.local.json with LocalDB configuration" -ForegroundColor Green

# Update the main configuration to use local
if (Test-Path $appsettingsPath) {
    $config = Get-Content $appsettingsPath | ConvertFrom-Json
    $config.DatabaseProvider = "Local"
    $config | ConvertTo-Json -Depth 10 | Set-Content -Path $appsettingsPath
    Write-Host "✅ Updated $appsettingsPath to use Local database" -ForegroundColor Green
}

Write-Host "`n🔧 Testing Local Database Setup:" -ForegroundColor Yellow
try {
    # Test LocalDB connection
    $localConnectionString = "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=BusBuddyLocal;Integrated Security=True;MultipleActiveResultSets=True"
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $localConnectionString
    $connection.Open()
    Write-Host "✅ LocalDB connection successful" -ForegroundColor Green
    $connection.Close()
} catch {
    Write-Host "❌ LocalDB connection failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "💡 You may need to install SQL Server LocalDB" -ForegroundColor Yellow
}

Write-Host "`n🚌 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Run: dotnet run --project BusBuddy.WPF" -ForegroundColor White
Write-Host "2. The app will now use LocalDB instead of Azure SQL" -ForegroundColor White
Write-Host "3. Fix Azure SQL firewall when convenient" -ForegroundColor White
Write-Host "4. Switch back to Azure by changing DatabaseProvider to 'Azure'" -ForegroundColor White
