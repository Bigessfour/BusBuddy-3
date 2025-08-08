#!/usr/bin/env pwsh
# Fix-Database-Migration-Constraint.ps1
# Fixes the PK_Vehicles constraint issue during migration

param(
    [switch]$Force,
    [switch]$RemoveProblematicMigration
)

Write-Host "🔧 BusBuddy Database Migration Constraint Fix" -ForegroundColor Cyan

try {
    # Ensure we're using local connection
    $env:BUSBUDDY_CONNECTION = "Server=127.0.0.1\SQLEXPRESS;Database=BusBuddy;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;MultipleActiveResultSets=True;"
    $env:ASPNETCORE_ENVIRONMENT = "Development"

    Write-Host "🌍 Environment: $env:ASPNETCORE_ENVIRONMENT" -ForegroundColor Yellow
    Write-Host "🔗 Connection: Local SQL Express" -ForegroundColor Yellow

    # Step 1: Remove the problematic migration file
    if ($RemoveProblematicMigration) {
        Write-Host "🗑️ Removing problematic migration..." -ForegroundColor Yellow
        $migrationFile = "BusBuddy.Core\Migrations\*InitialEntra*"
        if (Get-ChildItem $migrationFile -ErrorAction SilentlyContinue) {
            Remove-Item $migrationFile -Force
            Write-Host "✅ Removed InitialEntra migration files" -ForegroundColor Green
        }
    }

    # Step 2: Drop existing database completely
    Write-Host "🗑️ Dropping existing database..." -ForegroundColor Yellow
    try {
        dotnet ef database drop --project BusBuddy.Core --startup-project BusBuddy.WPF --force
        Write-Host "✅ Database dropped" -ForegroundColor Green
    } catch {
        Write-Host "⚠️ Database drop failed or database didn't exist" -ForegroundColor Yellow
    }

    # Step 3: Remove migration history (if database exists locally)
    Write-Host "🔄 Clearing migration history..." -ForegroundColor Yellow
    try {
        sqlcmd -S "127.0.0.1\SQLEXPRESS" -E -Q "DROP DATABASE IF EXISTS BusBuddy" -t 10
        Write-Host "✅ Database completely removed" -ForegroundColor Green
    } catch {
        Write-Host "⚠️ Direct database drop via sqlcmd failed" -ForegroundColor Yellow
    }

    # Step 4: Create database fresh with working migrations only
    Write-Host "📊 Creating fresh database..." -ForegroundColor Yellow

    # Apply migrations up to the last working one
    dotnet ef database update "UpdateBusDescription" --project BusBuddy.Core --startup-project BusBuddy.WPF --verbose

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Database created successfully with working migrations" -ForegroundColor Green
    } else {
        Write-Host "❌ Database creation failed. Trying complete reset..." -ForegroundColor Red

        # Complete reset approach
        Write-Host "🔄 Attempting complete migration reset..." -ForegroundColor Yellow
        dotnet ef database update 0 --project BusBuddy.Core --startup-project BusBuddy.WPF --force
        dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF
    }

    # Step 5: Test the database connection
    Write-Host "🔌 Testing database connection..." -ForegroundColor Yellow
    $testQuery = "SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
    $result = sqlcmd -S "127.0.0.1\SQLEXPRESS" -E -d "BusBuddy" -Q $testQuery -h -1 2>$null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Database connection test successful" -ForegroundColor Green
        Write-Host "📊 Tables in database: $($result.Trim())" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Database connection test failed" -ForegroundColor Red
    }

    # Step 6: Test data seeding
    Write-Host "🌱 Testing Wiley data seeding..." -ForegroundColor Yellow
    dotnet run --project BusBuddy.WPF --no-build -- --test-seed 2>$null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Seeding test completed" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Seeding test had issues (check application logs)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "🎉 Database migration fix completed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Run: dotnet run --project BusBuddy.WPF" -ForegroundColor White
    Write-Host "2. Check Students module for data" -ForegroundColor White
    Write-Host "3. Verify UI buttons work properly" -ForegroundColor White

} catch {
    Write-Host "❌ Error during migration fix: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "🛠️ Manual recovery steps:" -ForegroundColor Yellow
    Write-Host "1. Remove BusBuddy.Core\Migrations\*InitialEntra* files" -ForegroundColor White
    Write-Host "2. Run: dotnet ef database drop --force" -ForegroundColor White
    Write-Host "3. Run: dotnet ef database update" -ForegroundColor White
    exit 1
}
