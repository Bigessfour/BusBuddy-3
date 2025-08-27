# Debug Database Connection for BusBuddy Startup Issue
Write-Information "🔍 Investigating BusBuddy startup issue..." -InformationAction Continue
Write-Information "============================================" -InformationAction Continue

try {
    # Read the current configuration
    $appsettings = Get-Content "appsettings.json" | ConvertFrom-Json
    $connectionString = $appsettings.ConnectionStrings.AzureConnection
    $provider = $appsettings.DatabaseProvider

    Write-Information "Configuration Details:" -InformationAction Continue
    Write-Information "Database Provider: $provider" -InformationAction Continue
    Write-Information "Connection String: $($connectionString.Substring(0, 50))..." -InformationAction Continue

    # Test basic connection
    Write-Information "`n🔌 Testing basic database connection..." -InformationAction Continue
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Information "✅ Basic connection successful" -InformationAction Continue

    # Test if database has any tables (EF might be hanging on migration)
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
    $tableCount = $command.ExecuteScalar()

    Write-Information "📊 Database has $tableCount tables" -InformationAction Continue

    if ($tableCount -eq 0) {
        Write-Information "⚠️ Database is empty - Entity Framework might be hanging on initial migration" -InformationAction Continue
        Write-Information "This could explain why the application is stuck on loading screen" -InformationAction Continue
    } else {
        Write-Information "✅ Database has tables - migration issue unlikely" -InformationAction Continue
    }

    # Check if we can run a simple query
    $command.CommandText = "SELECT @@VERSION as Version, GETDATE() as CurrentTime"
    $reader = $command.ExecuteReader()

    if ($reader.Read()) {
        Write-Information "📋 SQL Server Version: $($reader['Version'].ToString().Split(' ')[0..3] -join ' ')" -InformationAction Continue
        Write-Information "⏰ Server Time: $($reader['CurrentTime'])" -InformationAction Continue
    }

    $reader.Close()
    $connection.Close()

    Write-Information "`n🧪 Recommendations:" -InformationAction Continue
    if ($tableCount -eq 0) {
        Write-Information "1. The app might be hanging during Entity Framework migration" -InformationAction Continue
        Write-Information "2. Try running migration manually: dotnet ef database update" -InformationAction Continue
        Write-Information "3. Check if AutoMigrateDatabase is causing the hang" -InformationAction Continue
    } else {
        Write-Information "1. Database connection is working fine" -InformationAction Continue
        Write-Information "2. Issue might be in Syncfusion UI component initialization" -InformationAction Continue
        Write-Information "3. Check for missing Syncfusion assemblies or theme resources" -InformationAction Continue
    }

} catch {
    Write-Error "❌ Database connection failed: $($_.Exception.Message)"
    Write-Information "This explains why the application is stuck!" -InformationAction Continue
}
