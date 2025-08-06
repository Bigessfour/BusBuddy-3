# 🚌 BusBuddy Migration Reset Script
# Use this if migrations are "all wrong" and need a clean slate

Write-Host "🚌 BusBuddy Migration Reset" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

Write-Host "⚠️  WARNING: This will reset all migrations!" -ForegroundColor Yellow
Write-Host "This will:" -ForegroundColor White
Write-Host "  1. Remove existing migration files" -ForegroundColor White
Write-Host "  2. Create a new initial migration" -ForegroundColor White
Write-Host "  3. You'll need to manually drop tables in Azure SQL" -ForegroundColor White
Write-Host ""

$confirm = Read-Host "Continue? (y/N)"
if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "❌ Operation cancelled" -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "🧹 Step 1: Removing existing migrations..." -ForegroundColor Yellow
if (Test-Path "BusBuddy.Core/Migrations") {
    Remove-Item "BusBuddy.Core/Migrations" -Recurse -Force
    Write-Host "   ✅ Migration files removed" -ForegroundColor Green
} else {
    Write-Host "   ℹ️  No existing migrations found" -ForegroundColor Blue
}

Write-Host ""
Write-Host "🏗️ Step 2: Creating new initial migration..." -ForegroundColor Yellow
try {
    dotnet ef migrations add InitialCreate --project BusBuddy.Core --startup-project BusBuddy.WPF

    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Initial migration created" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Migration creation failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ❌ Migration error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📋 Manual Steps Required:" -ForegroundColor Cyan
Write-Host "1. In Azure Query Editor, run these commands:" -ForegroundColor White
Write-Host "   DROP TABLE IF EXISTS __EFMigrationsHistory;" -ForegroundColor Gray
Write-Host "   DROP TABLE IF EXISTS Students;" -ForegroundColor Gray
Write-Host "   DROP TABLE IF EXISTS Routes;" -ForegroundColor Gray
Write-Host "   DROP TABLE IF EXISTS Vehicles;" -ForegroundColor Gray
Write-Host "   DROP TABLE IF EXISTS Drivers;" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Then run: ./Setup-Azure-SQL-Owner.ps1" -ForegroundColor White
Write-Host ""
Write-Host "🎯 Migration reset complete!" -ForegroundColor Green
