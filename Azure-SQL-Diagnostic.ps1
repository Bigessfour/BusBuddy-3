# Azure SQL Diagnostic Test - Enhanced Error Detection
param(
    [string]$Server = "tcp:busbuddy-server-sm2.database.windows.net,1433",
    [string]$Database = "BusBuddyDB",
    [string]$Username = $env:AZURE_SQL_USER,
    [string]$Password = $env:AZURE_SQL_PASSWORD
)

Write-Host "🔍 Azure SQL Diagnostic Test" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Credential verification
Write-Host "1. Credential Check:" -ForegroundColor Yellow
if ($Username -and $Password) {
    Write-Host "   ✅ Username: $Username" -ForegroundColor Green
    Write-Host "   ✅ Password: [SET - $($Password.Length) characters]" -ForegroundColor Green
} else {
    Write-Host "   ❌ Missing credentials" -ForegroundColor Red
    exit 1
}

# Server connectivity test
Write-Host "2. Server Connectivity:" -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.ConnectAsync("busbuddy-server-sm2.database.windows.net", 1433).Wait(5000)
    if ($tcpClient.Connected) {
        Write-Host "   ✅ TCP connection to server successful" -ForegroundColor Green
        $tcpClient.Close()
    } else {
        Write-Host "   ❌ TCP connection failed" -ForegroundColor Red
    }
} catch {
    Write-Host "   ❌ Network error: $($_.Exception.Message)" -ForegroundColor Red
}

# SQL Module check
Write-Host "3. SQL Module:" -ForegroundColor Yellow
if (Get-Module -ListAvailable -Name SqlServer) {
    Import-Module SqlServer -Force
    Write-Host "   ✅ SqlServer module loaded" -ForegroundColor Green
} else {
    Write-Host "   ❌ SqlServer module not available" -ForegroundColor Red
    Write-Host "   Install with: Install-Module -Name SqlServer -Force" -ForegroundColor Yellow
    exit 1
}

# Authentication test with detailed error
Write-Host "4. Authentication Test:" -ForegroundColor Yellow
try {
    $connectionString = "Server=$Server;Database=master;User Id=$Username;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=10;"

    Write-Host "   Testing authentication..." -ForegroundColor White
    $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query "SELECT SYSTEM_USER as CurrentUser, @@VERSION as Version" -ErrorAction Stop

    Write-Host "   ✅ Authentication successful!" -ForegroundColor Green
    Write-Host "   ✅ Connected as: $($result.CurrentUser)" -ForegroundColor Green
    Write-Host "   ✅ SQL Server Version: $($result.Version.Split(' ')[0..3] -join ' ')" -ForegroundColor Green

    # Test BusBuddyDB access
    Write-Host "5. Database Access Test:" -ForegroundColor Yellow
    $dbConnectionString = "Server=$Server;Database=$Database;User Id=$Username;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=10;"

    $dbResult = Invoke-Sqlcmd -ConnectionString $dbConnectionString -Query "SELECT DB_NAME() as DatabaseName, COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'" -ErrorAction Stop

    Write-Host "   ✅ Database connection successful!" -ForegroundColor Green
    Write-Host "   ✅ Connected to: $($dbResult.DatabaseName)" -ForegroundColor Green
    Write-Host "   ✅ Tables found: $($dbResult.TableCount)" -ForegroundColor Green

    if ($dbResult.TableCount -eq 0) {
        Write-Host "   ⚠️  Database is empty - ready for EF migrations" -ForegroundColor Yellow
    }

} catch {
    Write-Host "   ❌ Authentication failed!" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red

    # Additional diagnostic info
    if ($_.Exception.Message -like "*Login failed*") {
        Write-Host "   🔍 Possible causes:" -ForegroundColor Yellow
        Write-Host "      - Password recently changed (may take 2-3 minutes to propagate)" -ForegroundColor White
        Write-Host "      - Username/password mismatch" -ForegroundColor White
        Write-Host "      - SQL Authentication not enabled" -ForegroundColor White
        Write-Host "   💡 Try resetting the password in Azure Portal" -ForegroundColor Cyan
    }

    return $false
}

Write-Host "🎉 All tests passed!" -ForegroundColor Green
return $true
