# Simple Azure SQL Connection Test
param(
    [string]$Server = "busbuddy-server-sm2.database.windows.net",
    [string]$Database = "BusBuddyDB"
)

Write-Host "🔍 Testing Azure SQL Connection..." -ForegroundColor Cyan

# Check environment variables
$azureUser = $env:AZURE_SQL_USER
$azurePassword = $env:AZURE_SQL_PASSWORD

if (-not $azureUser) {
    Write-Host "❌ AZURE_SQL_USER environment variable not set" -ForegroundColor Red
    exit 1
}

if (-not $azurePassword) {
    Write-Host "❌ AZURE_SQL_PASSWORD environment variable not set" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Environment variables found" -ForegroundColor Green
Write-Host "   User: $azureUser" -ForegroundColor Gray
Write-Host "   Password: $('*' * $azurePassword.Length)" -ForegroundColor Gray

# Build connection string
$connectionString = "Server=tcp:$Server,1433;Initial Catalog=$Database;Persist Security Info=False;User ID=$azureUser;Password=$azurePassword;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "🔗 Testing connection to Azure SQL..." -ForegroundColor Cyan

try {
    # Test with SqlClient
    Add-Type -AssemblyName "System.Data.SqlClient"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Host "✅ Successfully connected to Azure SQL Database!" -ForegroundColor Green
    Write-Host "   Server: $($connection.DataSource)" -ForegroundColor Gray
    Write-Host "   Database: $($connection.Database)" -ForegroundColor Gray
    Write-Host "   Server Version: $($connection.ServerVersion)" -ForegroundColor Gray

    # Test a simple query
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES"
    $tableCount = $command.ExecuteScalar()

    Write-Host "✅ Database has $tableCount tables" -ForegroundColor Green

    $connection.Close()
    Write-Host "✅ Connection test completed successfully" -ForegroundColor Green

} catch {
    Write-Host "❌ Connection failed: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception.Message -match "timeout") {
        Write-Host "💡 This might be a network or firewall issue" -ForegroundColor Yellow
    } elseif ($_.Exception.Message -match "login") {
        Write-Host "💡 This might be a credentials issue" -ForegroundColor Yellow
    } elseif ($_.Exception.Message -match "server") {
        Write-Host "💡 This might be a server name issue" -ForegroundColor Yellow
    }

    exit 1
}
