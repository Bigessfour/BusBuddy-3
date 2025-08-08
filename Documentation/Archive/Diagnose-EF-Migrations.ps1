# 🚌 BusBuddy EF Migration Diagnostics
# Quick health check for Entity Framework migrations

Write-Host "🚌 BusBuddy EF Migration Diagnostics" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "🔍 1. Checking build status..." -ForegroundColor Yellow
try {
    dotnet build BusBuddy.sln --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Solution builds successfully" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Build failed - check for compilation errors" -ForegroundColor Red
        Write-Host "   💡 Run: dotnet build BusBuddy.sln for details" -ForegroundColor Cyan
    }
} catch {
    Write-Host "   ❌ Build error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "🔍 2. Checking EF Tools..." -ForegroundColor Yellow
try {
    dotnet ef --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ EF Core tools available" -ForegroundColor Green
    } else {
        Write-Host "   ❌ EF Core tools not found" -ForegroundColor Red
        Write-Host "   💡 Install with: dotnet tool install --global dotnet-ef" -ForegroundColor Cyan
    }
} catch {
    Write-Host "   ❌ EF tools check failed" -ForegroundColor Red
}

Write-Host ""
Write-Host "🔍 3. Checking migration files..." -ForegroundColor Yellow
if (Test-Path "BusBuddy.Core/Migrations") {
    $migrations = Get-ChildItem "BusBuddy.Core/Migrations" -Filter "*.cs" | Where-Object { $_.Name -ne "BusBuddyDbContextModelSnapshot.cs" }
    if ($migrations.Count -gt 0) {
        Write-Host "   ✅ Found $($migrations.Count) migration(s)" -ForegroundColor Green
        foreach ($migration in $migrations) {
            Write-Host "      • $($migration.Name)" -ForegroundColor White
        }
    } else {
        Write-Host "   ⚠️  No migration files found" -ForegroundColor Yellow
        Write-Host "   💡 Create with: dotnet ef migrations add InitialCreate --project BusBuddy.Core --startup-project BusBuddy.WPF" -ForegroundColor Cyan
    }
} else {
    Write-Host "   ❌ Migrations folder not found" -ForegroundColor Red
    Write-Host "   💡 Create with: dotnet ef migrations add InitialCreate --project BusBuddy.Core --startup-project BusBuddy.WPF" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "🔍 4. Checking DbContext factory..." -ForegroundColor Yellow
if (Test-Path "BusBuddy.Core/Data/BusBuddyDbContextFactory.cs") {
    Write-Host "   ✅ Design-time factory exists" -ForegroundColor Green
} else {
    Write-Host "   ❌ Design-time factory missing" -ForegroundColor Red
    Write-Host "   💡 This helps avoid WPF startup hangs during migrations" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "🔍 5. Checking environment variables..." -ForegroundColor Yellow
if ($env:AZURE_SQL_USER -and $env:AZURE_SQL_PASSWORD) {
    Write-Host "   ✅ Azure SQL environment variables set" -ForegroundColor Green
    Write-Host "   User: $env:AZURE_SQL_USER" -ForegroundColor White
} else {
    Write-Host "   ❌ Azure SQL environment variables not set" -ForegroundColor Red
    Write-Host "   💡 Run: ./Setup-Azure-SQL-Owner.ps1 to configure" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "📋 Common Solutions:" -ForegroundColor Cyan
Write-Host "• Build errors: Check for duplicate using statements" -ForegroundColor White
Write-Host "• Migration hangs: Use --project and --startup-project parameters" -ForegroundColor White
Write-Host "• Connection issues: Verify Azure firewall rules" -ForegroundColor White
Write-Host "• 'All wrong' migrations: Use ./Reset-Migrations.ps1" -ForegroundColor White
Write-Host ""
Write-Host "🎯 Next Steps:" -ForegroundColor Green
Write-Host "1. Fix any ❌ issues above" -ForegroundColor White
Write-Host "2. Run: ./Setup-Azure-SQL-Owner.ps1" -ForegroundColor White
Write-Host "3. Test: bb-run (or dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj)" -ForegroundColor White
