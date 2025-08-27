# Test New Azure SQL Database Connection
Write-Information "🧪 Testing New Azure SQL Database Connection" -InformationAction Continue
Write-Information "=========================================" -InformationAction Continue

# Read connection string from appsettings.json
$appsettings = Get-Content "appsettings.json" | ConvertFrom-Json
$connectionString = $appsettings.ConnectionStrings.AzureConnection

Write-Information "Server: busbuddy-server-new-7397.database.windows.net" -InformationAction Continue
Write-Information "Database: BusBuddyDB" -InformationAction Continue
Write-Information "User: busbuddy_admin" -InformationAction Continue

try {
    Write-Information "`n🔌 Connecting to database..." -InformationAction Continue
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Information "✅ Connection successful!" -InformationAction Continue

    # Test query
    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT
    SYSTEM_USER as AuthenticatedUser,
    DB_NAME() as DatabaseName,
    @@VERSION as SQLServerVersion,
    GETDATE() as CurrentTime
"@

    $reader = $command.ExecuteReader()

    while ($reader.Read()) {
        Write-Information "`n📊 Database Info:" -InformationAction Continue
        Write-Information "👤 User: $($reader['AuthenticatedUser'])" -InformationAction Continue
        Write-Information "🗄️ Database: $($reader['DatabaseName'])" -InformationAction Continue
        Write-Information "⏰ Time: $($reader['CurrentTime'])" -InformationAction Continue
    }

    $reader.Close()
    $connection.Close()

    Write-Information "`n🎉 Azure SQL Database is ready for BusBuddy!" -InformationAction Continue
    Write-Information "You can now run your application with bb-run" -InformationAction Continue

} catch {
    Write-Error "❌ Connection failed: $($_.Exception.Message)"
}
