#!/usr/bin/env pwsh
# Fix-SqlExpress-Connection.ps1
# Comprehensive fix for SQL Server Express connectivity issues

param(
    [switch]$EnableBrowser,
    [switch]$EnableTcpIp,
    [switch]$TestConnection,
    [switch]$All
)

Write-Host "🔧 SQL Server Express Connection Fix" -ForegroundColor Cyan

if ($All) {
    $EnableBrowser = $true
    $EnableTcpIp = $true
    $TestConnection = $true
}

try {
    # Check if running as administrator
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Host "⚠️ Some fixes require administrator privileges. Consider running as admin." -ForegroundColor Yellow
    }

    # 1. Check SQL Server services
    Write-Host "📊 Checking SQL Server services..." -ForegroundColor Yellow
    $sqlServices = Get-Service -Name "*SQL*" | Select-Object Name, Status, StartType
    $sqlServices | Format-Table -AutoSize

    $sqlExpress = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
    if (-not $sqlExpress) {
        Write-Host "❌ SQL Server Express (SQLEXPRESS) service not found!" -ForegroundColor Red
        Write-Host "Please install SQL Server Express first." -ForegroundColor Yellow
        exit 1
    }

    if ($sqlExpress.Status -ne "Running") {
        Write-Host "▶️ Starting SQL Server Express..." -ForegroundColor Yellow
        if ($isAdmin) {
            Start-Service -Name "MSSQL`$SQLEXPRESS"
            Write-Host "✅ SQL Server Express started" -ForegroundColor Green
        } else {
            Write-Host "❌ Need admin rights to start SQL Server Express" -ForegroundColor Red
        }
    } else {
        Write-Host "✅ SQL Server Express is running" -ForegroundColor Green
    }

    # 2. Enable SQL Browser (for named instances)
    if ($EnableBrowser) {
        Write-Host "🌐 Configuring SQL Browser..." -ForegroundColor Yellow
        $browser = Get-Service -Name "SQLBrowser" -ErrorAction SilentlyContinue
        if ($browser) {
            if ($isAdmin) {
                Set-Service -Name "SQLBrowser" -StartupType Automatic
                Start-Service -Name "SQLBrowser" -ErrorAction SilentlyContinue
                Write-Host "✅ SQL Browser enabled and started" -ForegroundColor Green
            } else {
                Write-Host "❌ Need admin rights to configure SQL Browser" -ForegroundColor Red
            }
        }
    }

    # 3. Test different connection strings
    if ($TestConnection) {
        Write-Host "🔌 Testing connection methods..." -ForegroundColor Yellow

        $connectionTests = @(
            @{ Name = "LocalDB Default"; Server = "(LocalDB)\MSSQLLocalDB"; Timeout = 5 },
            @{ Name = "SQL Express Named"; Server = ".\SQLEXPRESS"; Timeout = 10 },
            @{ Name = "SQL Express localhost"; Server = "localhost\SQLEXPRESS"; Timeout = 10 },
            @{ Name = "SQL Express 127.0.0.1"; Server = "127.0.0.1\SQLEXPRESS"; Timeout = 10 },
            @{ Name = "SQL Express TCP"; Server = "127.0.0.1,1433"; Timeout = 5 }
        )

        foreach ($test in $connectionTests) {
            Write-Host "Testing: $($test.Name)" -ForegroundColor Cyan
            try {
                $result = sqlcmd -S $test.Server -E -Q "SELECT 'Connected' as Status" -t $test.Timeout 2>$null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  ✅ SUCCESS: $($test.Server)" -ForegroundColor Green
                    $script:WorkingConnection = $test.Server
                } else {
                    Write-Host "  ❌ FAILED: $($test.Server)" -ForegroundColor Red
                }
            } catch {
                Write-Host "  ❌ ERROR: $($test.Server) - $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }

    # 4. Generate fixed connection strings
    Write-Host ""
    Write-Host "🔧 Recommended Connection Strings:" -ForegroundColor Cyan

    if ($script:WorkingConnection) {
        Write-Host "✅ Working server: $script:WorkingConnection" -ForegroundColor Green
        $connString = "Server=$($script:WorkingConnection);Database=BusBuddy;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;MultipleActiveResultSets=True;"
        Write-Host "Connection String:" -ForegroundColor Yellow
        Write-Host $connString -ForegroundColor White
    } else {
        Write-Host "⚠️ No working connection found. Try these alternatives:" -ForegroundColor Yellow
        Write-Host "1. LocalDB: Server=(LocalDB)\MSSQLLocalDB;Database=BusBuddy;Trusted_Connection=True;" -ForegroundColor White
        Write-Host "2. SQL Express: Server=localhost\SQLEXPRESS;Database=BusBuddy;Trusted_Connection=True;" -ForegroundColor White
    }

    # 5. Update appsettings.json if working connection found
    if ($script:WorkingConnection -and (Test-Path "appsettings.json")) {
        Write-Host ""
        Write-Host "🔄 Update appsettings.json? (y/n)" -ForegroundColor Yellow
        $response = Read-Host
        if ($response -eq 'y' -or $response -eq 'Y') {
            $newConnString = "Server=$($script:WorkingConnection);Database=BusBuddy;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;MultipleActiveResultSets=True;"

            $appsettings = Get-Content "appsettings.json" | ConvertFrom-Json
            $appsettings.ConnectionStrings.DefaultConnection = $newConnString
            $appsettings.ConnectionStrings.BusBuddyDb = $newConnString

            $appsettings | ConvertTo-Json -Depth 10 | Set-Content "appsettings.json"
            Write-Host "✅ appsettings.json updated with working connection string" -ForegroundColor Green
        }
    }

    Write-Host ""
    Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Test connection: sqlcmd -S `"$($script:WorkingConnection ?? 'localhost\SQLEXPRESS')`" -E -Q `"SELECT @@VERSION`"" -ForegroundColor White
    Write-Host "2. Run database setup: .\Setup-Database-MVP.ps1 -Provider Local" -ForegroundColor White
    Write-Host "3. Start application: dotnet run --project BusBuddy.WPF" -ForegroundColor White

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
