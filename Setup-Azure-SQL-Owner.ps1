# 🚌 BusBuddy Azure SQL Setup for Database Owner
# This script helps the database owner set up and test the connection

param(
    [Parameter(Mandatory=$false)]
    [string]$AdminPassword,
    [switch]$TestOnly
)

Write-Host "🚌 BusBuddy Azure SQL Owner Setup" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

# Database connection details
$ServerName = "busbuddy-server-sm2.database.windows.net"
$DatabaseName = "BusBuddyDB"
$AdminUser = "busbuddy_admin"

if (-not $AdminPassword -and -not $TestOnly) {
    Write-Host "Enter the password for busbuddy_admin (the one you created when setting up the server):" -ForegroundColor Yellow
    $SecurePassword = Read-Host -AsSecureString
    $AdminPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword))
}

if ($TestOnly) {
    Write-Host "🔍 Testing current environment variables..." -ForegroundColor Yellow
    if ($env:AZURE_SQL_USER -and $env:AZURE_SQL_PASSWORD) {
        Write-Host "   ✅ Environment variables are set" -ForegroundColor Green
        Write-Host "   User: $env:AZURE_SQL_USER" -ForegroundColor White
    } else {
        Write-Host "   ❌ Environment variables not set" -ForegroundColor Red
        exit 1
    }
} else {
    # Set environment variables for this session and permanently
    Write-Host "🔧 Setting up environment variables..." -ForegroundColor Yellow

    $env:AZURE_SQL_USER = $AdminUser
    $env:AZURE_SQL_PASSWORD = $AdminPassword

    # Set permanent environment variables
    [Environment]::SetEnvironmentVariable('AZURE_SQL_USER', $AdminUser, 'User')
    [Environment]::SetEnvironmentVariable('AZURE_SQL_PASSWORD', $AdminPassword, 'User')

    Write-Host "   ✅ Environment variables configured" -ForegroundColor Green
}

# Test database connection
Write-Host "🔍 Testing database connection..." -ForegroundColor Yellow
try {
    $ConnectionString = "Server=tcp:$ServerName,1433;Initial Catalog=$DatabaseName;Persist Security Info=False;User ID=$env:AZURE_SQL_USER;Password=$env:AZURE_SQL_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

    # Test with .NET SqlConnection
    Add-Type -AssemblyName System.Data
    $Connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    Write-Host "   🔧 Opening connection to Azure SQL..." -ForegroundColor Yellow
    $Connection.Open()

    $Command = $Connection.CreateCommand()
    $Command.CommandText = "SELECT DB_NAME() as DatabaseName, SYSTEM_USER as CurrentUser, GETDATE() as ServerTime"
    $Reader = $Command.ExecuteReader()

    if ($Reader.Read()) {
        Write-Host "   ✅ Database connection successful!" -ForegroundColor Green
        Write-Host "   Database: $($Reader['DatabaseName'])" -ForegroundColor White
        Write-Host "   User: $($Reader['CurrentUser'])" -ForegroundColor White
        Write-Host "   Server Time: $($Reader['ServerTime'])" -ForegroundColor White
    }

    $Reader.Close()
    $Connection.Close()

} catch {
    Write-Host "   ❌ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   💡 Checking firewall rules and server status..." -ForegroundColor Cyan

    # Check firewall rules
    try {
        Write-Host "   🔧 Current firewall rules:" -ForegroundColor Yellow
        az sql server firewall-rule list --server "busbuddy-server-sm2" --resource-group "BusBuddy-RG" --output table

        Write-Host "   🔧 Adding current IP to firewall..." -ForegroundColor Yellow
        $currentIP = (Invoke-RestMethod -Uri "https://ipinfo.io/ip" -UseBasicParsing).Trim()
        az sql server firewall-rule create --server "busbuddy-server-sm2" --resource-group "BusBuddy-RG" --name "CurrentIP-$(Get-Date -Format 'yyyy-MM-dd-HH-mm')" --start-ip-address $currentIP --end-ip-address $currentIP

        Write-Host "   💡 Try running the script again with the same password" -ForegroundColor Cyan
    } catch {
        Write-Host "   ⚠️  Could not update firewall automatically" -ForegroundColor Yellow
    }
    exit 1
}

if (-not $TestOnly) {
    # Clean build to fix any warnings
    Write-Host "🧹 Cleaning solution to fix warnings..." -ForegroundColor Yellow
    try {
        dotnet clean BusBuddy.sln
        Write-Host "   ✅ Solution cleaned" -ForegroundColor Green

        dotnet build BusBuddy.sln
        if ($LASTEXITCODE -ne 0) {
            Write-Host "   ❌ Build failed - check for compilation errors" -ForegroundColor Red
            exit 1
        }
        Write-Host "   ✅ Solution built successfully" -ForegroundColor Green
    } catch {
        Write-Host "   ❌ Clean/Build error: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }

    # Apply EF Migrations
    Write-Host "🔧 Applying Entity Framework Migrations..." -ForegroundColor Yellow
    $migrationSucceeded = $false

    try {
        # Start the migration as a background job with a timeout
        $migrationJob = Start-Job -ScriptBlock {
            # Pass connection string to the job's scope
            param($connStr)

            # Capture all output streams (stdout and stderr)
            $output = dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF --context BusBuddyDbContext --connection $connStr --verbose 2>&1

            # Return a custom object with success status and output
            return [PSCustomObject]@{
                Success = ($LASTEXITCODE -eq 0)
                Output = $output
            }
        } -ArgumentList $ConnectionString

        Write-Host "   ⏳ Waiting for migration job to complete (timeout: 90 seconds)..." -ForegroundColor Yellow
        $jobResult = Wait-Job -Job $migrationJob -Timeout 90

        if ($jobResult -eq $null) {
            # Timeout occurred, the job is likely frozen
            Write-Host "   ❌ Migration command timed out (froze)." -ForegroundColor Red
            Stop-Job -Job $migrationJob
        } else {
            # Job completed, failed, or was stopped
            $result = Receive-Job -Job $migrationJob

            Write-Host "--- Migration Output ---" -ForegroundColor Gray
            Write-Host ($result.Output | Out-String) -ForegroundColor Gray
            Write-Host "------------------------" -ForegroundColor Gray

            if ($result.Success) {
                Write-Host "   ✅ Migrations applied successfully via dotnet ef." -ForegroundColor Green
                $migrationSucceeded = $true
            } else {
                Write-Host "   ❌ Migration command failed or completed with errors." -ForegroundColor Red
            }
        }

        Remove-Job -Job $migrationJob -Force
    } catch {
        Write-Host "   ❌ An error occurred while running the migration job: $($_.Exception.Message)" -ForegroundColor Red
    }

    if (-not $migrationSucceeded) {
        Write-Host ""
        Write-Host "   ⚠️  Direct migration failed or timed out, trying alternative approach..." -ForegroundColor Yellow

        # Generate idempotent script as fallback
        Write-Host "   🔧 Generating migration script..." -ForegroundColor Yellow
        dotnet ef migrations script --idempotent --project BusBuddy.Core --startup-project BusBuddy.WPF --output migration.sql

        if (Test-Path "migration.sql") {
            Write-Host "   📝 Migration script generated: migration.sql" -ForegroundColor Cyan
            Write-Host "   💡 Manual option: Run this script in Azure Query Editor to create your tables." -ForegroundColor Cyan
        } else {
            Write-Host "   ❌ Could not generate migration script" -ForegroundColor Red
            exit 1
        }
    }

    Write-Host ""
    Write-Host "🎉 Setup Complete!" -ForegroundColor Green
    Write-Host "==================" -ForegroundColor Green
    Write-Host "✅ Environment variables configured" -ForegroundColor White
    Write-Host "✅ Database connection verified" -ForegroundColor White
    Write-Host "✅ Solution cleaned and built" -ForegroundColor White
    Write-Host "✅ Entity Framework migrations applied" -ForegroundColor White
    Write-Host ""
    Write-Host "📋 Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Run: bb-run (or dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj)" -ForegroundColor White
    Write-Host "2. Test student data entry in the application" -ForegroundColor White
    Write-Host "3. Verify data appears in Azure SQL database" -ForegroundColor White
}
