# Quick Database Connection Test
Write-Host "🔐 Testing Database Connection with Updated Password" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

$server = "busbuddy-server-sm2.database.windows.net"
$database = "BusBuddyDB"
$username = "azure_user"

# Prompt for password securely
$securePassword = Read-Host "Enter your updated password for azure_user" -AsSecureString
$password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword))

Write-Host "`nTesting connection..." -ForegroundColor Yellow

$connectionString = "Server=tcp:$server,1433;Initial Catalog=$database;User ID=$username;Password=$password;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Host "✅ SUCCESS! Connected to Azure SQL Database!" -ForegroundColor Green

    # Test a simple query
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT DB_NAME() as DatabaseName, GETDATE() as CurrentTime"
    $reader = $command.ExecuteReader()

    while ($reader.Read()) {
        Write-Host "📊 Database: $($reader['DatabaseName'])" -ForegroundColor Cyan
        Write-Host "🕒 Server Time: $($reader['CurrentTime'])" -ForegroundColor Cyan
    }
    $reader.Close()
    $connection.Close()

    # Update environment variable for future use
    Write-Host "`n💾 Updating environment variable for future use..." -ForegroundColor Yellow
    $env:AZURE_SQL_PASSWORD = $password
    Write-Host "✅ Environment variable updated!" -ForegroundColor Green

    Write-Host "`n🎉 Your BusBuddy database is ready to use!" -ForegroundColor Green

} catch {
    Write-Host "❌ Connection failed: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception.Message -match "Login failed") {
        Write-Host "🔍 This suggests:" -ForegroundColor Yellow
        Write-Host "• Password is still incorrect" -ForegroundColor White
        Write-Host "• User account may be disabled" -ForegroundColor White
        Write-Host "• Check Azure Portal for user status" -ForegroundColor White
    } elseif ($_.Exception.Message -match "server was not found") {
        Write-Host "🔍 Server connectivity issue" -ForegroundColor Yellow
    } else {
        Write-Host "🔍 Other authentication issue - check Azure Portal" -ForegroundColor Yellow
    }
}

# Clear password from memory
$password = $null
[System.GC]::Collect()
