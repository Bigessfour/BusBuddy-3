# One-Command-Database-Setup.ps1 - Ultimate speed setup
# Usage: .\One-Command-Database-Setup.ps1 [Local|Azure]

param([string]$Target = "Local")

$ErrorActionPreference = 'Stop'

Write-Host "🚀 BusBuddy $Target Database - One Command Setup" -ForegroundColor Cyan

switch ($Target) {
    "Local" {
        Write-Host "⚡ Setting up local database..." -ForegroundColor Yellow

        # Build + Migrate + Seed in one go
        & bb-build
        & dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF --no-build
        & .\Set-SampleWaypoints.ps1 -Force

        Write-Host "✅ Local database ready!" -ForegroundColor Green
        Write-Host "🎯 Tables created: Students, Routes, Drivers, Vehicles, RouteStops" -ForegroundColor Cyan
    }

    "Azure" {
        Write-Host "☁️ Setting up Azure database..." -ForegroundColor Yellow

        # Quick Azure setup
        & .\Azure-Database-Setup.ps1 -SkipInfrastructure -UpdateFirewall
        & .\Set-SampleWaypoints.ps1 -UseAzCliAuth -Force

        Write-Host "✅ Azure database ready!" -ForegroundColor Green
    }
}

Write-Host "🎉 All tables created and seeded!" -ForegroundColor Green
Write-Host "💡 Run 'bb-run' to start the application" -ForegroundColor Cyan
