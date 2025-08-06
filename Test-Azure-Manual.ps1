# Manual Azure SQL Test (for troubleshooting)
param(
    [string]$Username = $env:AZURE_SQL_USER,
    [string]$Password = $null,
    [string]$Server = "tcp:busbuddy-server-sm2.database.windows.net,1433"
)

if (-not $Password) {
    $SecurePassword = Read-Host -Prompt "Enter password" -AsSecureString
    $Password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword))
}

Write-Host "Testing Azure SQL with manual credentials..." -ForegroundColor Cyan
Write-Host "Username: $Username" -ForegroundColor Yellow
Write-Host "Server: $Server" -ForegroundColor Yellow

try {
    $connectionString = "Server=$Server;Database=master;User Id=$Username;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    Write-Host "Attempting connection..." -ForegroundColor Yellow
    $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query "SELECT 1 as TestValue" -ErrorAction Stop
    Write-Host "✅ SUCCESS: Azure SQL connection working!" -ForegroundColor Green

    # Test BusBuddyDB specifically
    $dbConnectionString = "Server=$Server;Database=BusBuddyDB;User Id=$Username;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    $dbResult = Invoke-Sqlcmd -ConnectionString $dbConnectionString -Query "SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'" -ErrorAction Stop
    Write-Host "✅ BusBuddyDB connection: Found $($dbResult.TableCount) tables" -ForegroundColor Green

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception.Message -like "*Login failed*") {
        Write-Host "🔧 Login failed - check username/password in Azure Portal" -ForegroundColor Yellow
    } elseif ($_.Exception.Message -like "*timeout*") {
        Write-Host "🔧 Timeout - check firewall rules" -ForegroundColor Yellow
    }
}
