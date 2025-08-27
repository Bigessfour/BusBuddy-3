# Azure Database Access Setup Script
# This script helps regain access to existing Azure SQL Database

Write-Host "🚌 BusBuddy Azure Database Access Setup" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green

# Step 1: Check Azure CLI Authentication
Write-Host "`n1. Checking Azure CLI Authentication..." -ForegroundColor Yellow
try {
    $account = az account show 2>$null | ConvertFrom-Json
    if ($account) {
        Write-Host "✅ Authenticated as: $($account.user.name)" -ForegroundColor Green
        Write-Host "✅ Subscription: $($account.name)" -ForegroundColor Green
        Write-Host "✅ Subscription ID: $($account.id)" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ Not authenticated. Run: az login" -ForegroundColor Red
    exit 1
}

# Step 2: Test Database Connectivity
Write-Host "`n2. Testing Database Connectivity..." -ForegroundColor Yellow
$server = "busbuddy-server-sm2.database.windows.net"
$database = "BusBuddyDB"

Write-Host "Testing connection to: $server" -ForegroundColor White
$connection = Test-NetConnection -ComputerName $server -Port 1433 -WarningAction SilentlyContinue

if ($connection.TcpTestSucceeded) {
    Write-Host "✅ Server is reachable at $($connection.RemoteAddress)" -ForegroundColor Green
} else {
    Write-Host "❌ Cannot reach server" -ForegroundColor Red
}

# Step 3: Check Database Access
Write-Host "`n3. Testing Database Authentication..." -ForegroundColor Yellow
$user = $env:AZURE_SQL_USER
$password = $env:AZURE_SQL_PASSWORD

if ($user -and $password) {
    Write-Host "✅ Credentials found for user: $user" -ForegroundColor Green

    $connectionString = "Server=tcp:$server,1433;Initial Catalog=$database;User ID=$user;Password=$password;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    try {
        $sqlConnection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $sqlConnection.Open()
        Write-Host "✅ Successfully connected to database!" -ForegroundColor Green
        $sqlConnection.Close()
    } catch {
        Write-Host "❌ Database authentication failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "This usually means:" -ForegroundColor Yellow
        Write-Host "• Password has changed" -ForegroundColor White
        Write-Host "• User account was disabled" -ForegroundColor White
        Write-Host "• Need subscription access to reset credentials" -ForegroundColor White
    }
} else {
    Write-Host "❌ Missing credentials (AZURE_SQL_USER or AZURE_SQL_PASSWORD)" -ForegroundColor Red
}

# Step 4: Check Resource Access
Write-Host "`n4. Checking Azure Resource Access..." -ForegroundColor Yellow
Write-Host "Looking for SQL servers in your subscription..." -ForegroundColor White

try {
    $sqlServers = az sql server list 2>$null | ConvertFrom-Json
    if ($sqlServers -and $sqlServers.Count -gt 0) {
        Write-Host "✅ Found SQL servers in your subscription:" -ForegroundColor Green
        foreach ($srv in $sqlServers) {
            Write-Host "  • $($srv.name) (Location: $($srv.location))" -ForegroundColor Cyan
        }
    } else {
        Write-Host "ℹ️ No SQL servers found in current subscription" -ForegroundColor Yellow
        Write-Host "This suggests the database is in a different subscription" -ForegroundColor White
    }
} catch {
    Write-Host "⚠️ Could not list SQL servers (may need permissions)" -ForegroundColor Yellow
}

Write-Host "`n=== SUMMARY ===" -ForegroundColor Green
Write-Host "If the database connection failed but server is reachable:" -ForegroundColor Yellow
Write-Host "1. The database exists but you need access to the subscription that owns it" -ForegroundColor White
Write-Host "2. OR the password for 'azure_user' has changed" -ForegroundColor White
Write-Host "3. OR the database requires Azure AD authentication now" -ForegroundColor White

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "• Contact whoever set up the original database" -ForegroundColor White
Write-Host "• OR create a new database in your new subscription" -ForegroundColor White
Write-Host "• OR migrate data from old database to new one" -ForegroundColor White
