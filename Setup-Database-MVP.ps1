#!/usr/bin/env pwsh
# Setup-Database-MVP.ps1
# Sets up BusBuddy database for MVP testing with proper schema and seeding

param(
    [ValidateSet("Local", "Azure")]
    [string]$Provider = "Local",
    [switch]$Force,
    [switch]$SeedData
)

Write-Host "🚌 BusBuddy Database Setup - MVP Mode" -ForegroundColor Cyan
Write-Host "Provider: $Provider" -ForegroundColor Yellow

try {
    # Set environment for database provider
    if ($Provider -eq "Azure") {
        $env:ASPNETCORE_ENVIRONMENT = "Production"
        Write-Host "🌐 Using Azure SQL configuration" -ForegroundColor Blue

        # Check Azure environment variables
        if (-not $env:AZURE_SQL_USER -or -not $env:AZURE_SQL_PASSWORD) {
            Write-Host "❌ Azure SQL credentials not found in environment variables." -ForegroundColor Red
            Write-Host "Please set AZURE_SQL_USER and AZURE_SQL_PASSWORD." -ForegroundColor Yellow
            exit 1
        }
        Write-Host "✅ Azure credentials verified" -ForegroundColor Green
    } else {
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        Write-Host "🏠 Using Local SQL Express configuration" -ForegroundColor Blue
    }

    # Check .NET and project structure
    if (-not (Test-Path "BusBuddy.sln")) {
        Write-Host "❌ BusBuddy.sln not found. Please run from project root." -ForegroundColor Red
        exit 1
    }

    # Build the solution first
    Write-Host "🔨 Building solution..." -ForegroundColor Yellow
    dotnet build BusBuddy.sln --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed. Please fix compilation errors first." -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Build successful" -ForegroundColor Green

    # Check if database exists and drop if Force is specified
    if ($Force) {
        Write-Host "🗑️ Dropping existing database..." -ForegroundColor Yellow
        dotnet ef database drop --project BusBuddy.Core --startup-project BusBuddy.WPF --force
    }

    # Create/update database with migrations
    Write-Host "📊 Creating/updating database..." -ForegroundColor Yellow
    dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF --verbose

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Database schema updated successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Database update failed" -ForegroundColor Red
        exit 1
    }

    # Test database connection
    Write-Host "🔌 Testing database connection..." -ForegroundColor Yellow
    dotnet run --project BusBuddy.WPF --no-build -- --test-connection

    if ($SeedData) {
        Write-Host "🌱 Seeding test data..." -ForegroundColor Yellow
        dotnet run --project BusBuddy.WPF --no-build -- --seed-data
    }

    Write-Host "🎉 Database setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Run: dotnet run --project BusBuddy.WPF" -ForegroundColor White
    Write-Host "2. Check Students module for seeded data" -ForegroundColor White
    Write-Host "3. Verify UI buttons are working" -ForegroundColor White

} catch {
    Write-Host "❌ Error during database setup: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
