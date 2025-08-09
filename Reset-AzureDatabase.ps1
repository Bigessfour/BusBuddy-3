# Reset Azure SQL Database Schema for BusBuddy MVP
# This script will clean up constraint issues and reapply migrations properly

param(
    [switch]$Force = $false
)

Write-Host "🚌 BusBuddy Azure Database Reset Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Get connection details from environment
$serverName = "busbuddy-server-sm2.database.windows.net"
$databaseName = "BusBuddyDB"
$userName = $env:AZURE_SQL_USER
$password = $env:AZURE_SQL_PASSWORD

if (-not $userName -or -not $password) {
    Write-Error "❌ Azure SQL credentials not found in environment variables"
    Write-Host "Required: AZURE_SQL_USER and AZURE_SQL_PASSWORD"
    exit 1
}

Write-Host "🔧 Server: $serverName" -ForegroundColor Green
Write-Host "🔧 Database: $databaseName" -ForegroundColor Green
Write-Host "🔧 User: $userName" -ForegroundColor Green

# Connection string for Azure SQL
$connectionString = "Server=tcp:$serverName,1433;Initial Catalog=$databaseName;Persist Security Info=False;User ID=$userName;Password=$password;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "`n📊 Checking current database state..." -ForegroundColor Yellow

try {
    # Test connection first
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host "✅ Connected to Azure SQL Database successfully" -ForegroundColor Green

    # Check if __EFMigrationsHistory table exists
    $checkQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory'"
    $command = New-Object System.Data.SqlClient.SqlCommand($checkQuery, $connection)
    $migrationTableExists = $command.ExecuteScalar() -gt 0

    if ($migrationTableExists) {
        Write-Host "📋 Migration history table exists" -ForegroundColor Green

        # Get applied migrations
        $migrationQuery = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId"
        $command = New-Object System.Data.SqlClient.SqlCommand($migrationQuery, $connection)
        $reader = $command.ExecuteReader()

        Write-Host "📝 Applied migrations:" -ForegroundColor Yellow
        while ($reader.Read()) {
            Write-Host "   - $($reader['MigrationId'])" -ForegroundColor Gray
        }
        $reader.Close()
    } else {
        Write-Host "❌ Migration history table does not exist" -ForegroundColor Red
    }

    # Check if Students table exists
    $studentsQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Students'"
    $command = New-Object System.Data.SqlClient.SqlCommand($studentsQuery, $connection)
    $studentsExists = $command.ExecuteScalar() -gt 0

    if ($studentsExists) {
        Write-Host "✅ Students table exists" -ForegroundColor Green
    } else {
        Write-Host "❌ Students table does NOT exist (this is the root cause)" -ForegroundColor Red
    }

    $connection.Close()

} catch {
    Write-Error "❌ Database connection failed: $($_.Exception.Message)"
    exit 1
}

Write-Host "`n🔧 Recommended Actions:" -ForegroundColor Cyan
Write-Host "1. Drop and recreate database schema" -ForegroundColor Yellow
Write-Host "2. Apply all migrations from scratch" -ForegroundColor Yellow
Write-Host "3. Test the Students table creation" -ForegroundColor Yellow

if ($Force) {
    Write-Host "`n⚠️  FORCE MODE: Proceeding with database reset..." -ForegroundColor Red

    # Use EF Core to reset the database
    Write-Host "🗑️  Dropping existing database..." -ForegroundColor Yellow
    $dropResult = & dotnet ef database drop --force --project BusBuddy.Core --startup-project BusBuddy.WPF 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Database dropped successfully" -ForegroundColor Green

        Write-Host "🏗️  Creating fresh database with all migrations..." -ForegroundColor Yellow
        $updateResult = & dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Database created and migrations applied successfully" -ForegroundColor Green
            Write-Host "🎉 BusBuddy MVP database is now ready!" -ForegroundColor Green
        } else {
            Write-Error "❌ Failed to apply migrations: $updateResult"
            exit 1
        }
    } else {
        Write-Error "❌ Failed to drop database: $dropResult"
        exit 1
    }
} else {
    Write-Host "`n💡 To proceed with reset, run: .\Reset-AzureDatabase.ps1 -Force" -ForegroundColor Cyan
}

Write-Host "`n✅ Database analysis complete!" -ForegroundColor Green
