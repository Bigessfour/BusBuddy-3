# Quick-Database-Setup.ps1 - Streamlined database setup for BusBuddy
# Based on your existing Set-SampleWaypoints.ps1 but optimized for speed

param(
    [string]$Environment = "Local", # Local, Azure, or Staging
    [switch]$Force,
    [switch]$SkipMigrations,
    [switch]$SeedOnly
)

$ErrorActionPreference = 'Stop'

Write-Host "🚌 BusBuddy Database Setup - $Environment Environment" -ForegroundColor Cyan

# Quick environment setup
switch ($Environment) {
    "Local" {
        $config = "appsettings.json"
        Write-Host "📁 Using local SQL Express database" -ForegroundColor Green
    }
    "Azure" {
        $config = "appsettings.azure.json"
        Write-Host "☁️ Using Azure SQL Database" -ForegroundColor Blue
    }
    "Staging" {
        $config = "appsettings.staging.json"
        Write-Host "🔧 Using staging database" -ForegroundColor Yellow
    }
}

# Step 1: Quick build check
if (-not $SkipMigrations) {
    Write-Host "⚡ Quick build validation..." -ForegroundColor Yellow
    $buildResult = & dotnet build BusBuddy.sln --verbosity quiet --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed. Run 'bb-build' for details."
    }
    Write-Host "✅ Build successful" -ForegroundColor Green
}

# Step 2: Apply migrations (fastest method)
if (-not $SkipMigrations -and -not $SeedOnly) {
    Write-Host "🔄 Applying EF Core migrations..." -ForegroundColor Yellow

    # Use direct dotnet ef commands for speed
    & dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF --configuration $config --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "Migration failed. Check your connection string in $config"
    }
    Write-Host "✅ Migrations applied successfully" -ForegroundColor Green
}

# Step 3: Seed sample data
Write-Host "🌱 Setting up sample waypoints and data..." -ForegroundColor Yellow

# Call your existing script for the data seeding part
$params = @()
if ($Force) { $params += "-Force" }
if ($Environment -eq "Azure") { $params += "-UseAzCliAuth" }

& .\Set-SampleWaypoints.ps1 @params

Write-Host "🎉 Database setup complete!" -ForegroundColor Green
Write-Host "💡 You can now run: bb-run" -ForegroundColor Cyan
