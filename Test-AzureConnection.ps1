# Test Azure SQL Connection Script
Write-Host "🚌 Testing Azure SQL Database Connection..." -ForegroundColor Cyan

# Get environment variables
$userId = $env:AZURE_SQL_USER
$password = $env:AZURE_SQL_PASSWORD

Write-Host "User: $userId" -ForegroundColor Yellow
Write-Host "Password Set: $($password -ne $null -and $password -ne '')" -ForegroundColor Yellow

if (-not $userId -or -not $password) {
    Write-Host "❌ Azure SQL credentials not set!" -ForegroundColor Red
    Write-Host "Please set AZURE_SQL_USER and AZURE_SQL_PASSWORD environment variables" -ForegroundColor Yellow
    exit 1
}

# Build connection string
$connectionString = "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID=$userId;Password=$password;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

try {
    # Load SqlClient assembly
    Add-Type -AssemblyName "System.Data.SqlClient"

    Write-Host "🔗 Attempting connection to Azure SQL..." -ForegroundColor Cyan

    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Host "✅ Connection successful!" -ForegroundColor Green

    # Test basic query
    $command = New-Object System.Data.SqlClient.SqlCommand("SELECT DB_NAME()", $connection)
    $dbName = $command.ExecuteScalar()
    Write-Host "✅ Connected to database: $dbName" -ForegroundColor Green

    # Test if we can see any tables
    $tablesCommand = New-Object System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES", $connection)
    $tableCount = $tablesCommand.ExecuteScalar()
    Write-Host "✅ Database has $tableCount tables" -ForegroundColor Green

    $connection.Close()
    Write-Host "🎯 Azure SQL connection test completed successfully!" -ForegroundColor Green

} catch {
    Write-Host "❌ Connection failed: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception -is [System.Data.SqlClient.SqlException]) {
        $sqlEx = $_.Exception
        Write-Host "SQL Error Number: $($sqlEx.Number)" -ForegroundColor Yellow
        Write-Host "SQL Error State: $($sqlEx.State)" -ForegroundColor Yellow
        Write-Host "SQL Error Class: $($sqlEx.Class)" -ForegroundColor Yellow

        switch ($sqlEx.Number) {
            4060 { Write-Host "💡 Database doesn't exist or access denied" -ForegroundColor Cyan }
            18456 { Write-Host "💡 Login failed - check username/password" -ForegroundColor Cyan }
            2 { Write-Host "💡 Server not found - check server name" -ForegroundColor Cyan }
            53 { Write-Host "💡 Network path not found - check firewall" -ForegroundColor Cyan }
        }
    }
    exit 1
}
