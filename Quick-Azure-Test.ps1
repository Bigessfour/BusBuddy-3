# Quick Azure SQL Database Test
param(
    [string]$Server = "tcp:busbuddy-server-sm2.database.windows.net,1433",
    [string]$Database = "BusBuddyDB",
    [string]$Username = $env:AZURE_SQL_USER,
    [string]$Password = $env:AZURE_SQL_PASSWORD
)

Write-Host "Testing Azure SQL Connection..." -ForegroundColor Cyan

try {
    # Import SqlServer module if available
    if (Get-Module -ListAvailable -Name SqlServer) {
        Import-Module SqlServer -Force
        Write-Host "Using SqlServer PowerShell module" -ForegroundColor Green

        $connectionString = "Server=$Server;Database=$Database;User Id=$Username;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

        # Test connection to master database first
        $masterConnectionString = "Server=$Server;Database=master;User Id=$Username;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

        Write-Host "Testing connection to master database..." -ForegroundColor Yellow
        $masterResult = Invoke-Sqlcmd -ConnectionString $masterConnectionString -Query "SELECT 1 as TestValue" -ErrorAction Stop
        Write-Host "✅ Master database connection successful" -ForegroundColor Green

        # Check if BusBuddyDB exists
        Write-Host "Checking if BusBuddyDB exists..." -ForegroundColor Yellow
        $dbCheck = Invoke-Sqlcmd -ConnectionString $masterConnectionString -Query "SELECT name FROM sys.databases WHERE name = 'BusBuddyDB'" -ErrorAction Stop

        if ($dbCheck) {
            Write-Host "✅ BusBuddyDB database exists" -ForegroundColor Green

            # Test connection to BusBuddyDB
            Write-Host "Testing connection to BusBuddyDB..." -ForegroundColor Yellow
            $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query "SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'" -ErrorAction Stop
            Write-Host "✅ BusBuddyDB connection successful - Found $($result.TableCount) tables" -ForegroundColor Green

            if ($result.TableCount -eq 0) {
                Write-Host "⚠️  Database is empty - will need to create tables via EF migrations" -ForegroundColor Yellow
            }
        } else {
            Write-Host "❌ BusBuddyDB database does not exist" -ForegroundColor Red
            Write-Host "🔧 Creating BusBuddyDB database..." -ForegroundColor Yellow
            Invoke-Sqlcmd -ConnectionString $masterConnectionString -Query "CREATE DATABASE BusBuddyDB" -ErrorAction Stop
            Write-Host "✅ BusBuddyDB database created successfully" -ForegroundColor Green
        }

    } else {
        Write-Host "❌ SqlServer PowerShell module not available" -ForegroundColor Red
        Write-Host "Install with: Install-Module -Name SqlServer -Force" -ForegroundColor Yellow
    }

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    return $false
}

return $true
