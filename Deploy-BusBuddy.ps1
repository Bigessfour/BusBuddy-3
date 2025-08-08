# BusBuddy Production Deployment Script
# Implements production readiness checklist

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Staging", "Production")]
    [string]$Environment,

    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0-mvp",

    [Parameter(Mandatory=$false)]
    [switch]$SkipTests = $false,

    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild = $false
)

Write-Information "🚀 BusBuddy $Environment Deployment Script" -InformationAction Continue
Write-Information "📦 Version: $Version" -InformationAction Continue
Write-Information "🕐 Started: $(Get-Date)" -InformationAction Continue

$ErrorActionPreference = "Stop"

try {
    # Step 1: Pre-deployment validation
    Write-Information "🔍 Step 1: Pre-deployment validation" -InformationAction Continue

    if (-not $SkipTests) {
        Write-Information "🧪 Running health checks..." -InformationAction Continue
        $healthResult = & .\run-bb-diagnostics.ps1
        if ($LASTEXITCODE -ne 0) {
            throw "Health check failed. Deployment aborted."
        }
        Write-Information "✅ Health checks passed" -InformationAction Continue
    }

    # Step 2: Environment setup
    Write-Information "🔧 Step 2: Environment setup for $Environment" -InformationAction Continue

    # Set environment-specific variables
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $env:DOTNET_ENVIRONMENT = $Environment

    Write-Information "📋 Environment variables configured" -InformationAction Continue

    # Step 3: Build application
    if (-not $SkipBuild) {
        Write-Information "🏗️ Step 3: Building application for $Environment" -InformationAction Continue

        # Clean previous builds
        dotnet clean BusBuddy.sln --configuration Release
        if ($LASTEXITCODE -ne 0) { throw "Clean failed" }

        # Restore packages
        dotnet restore BusBuddy.sln --force --no-cache
        if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

        # Build solution
        dotnet build BusBuddy.sln --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }

        Write-Information "✅ Build completed successfully" -InformationAction Continue
    }

    # Step 4: Run tests
    if (-not $SkipTests) {
        Write-Information "🧪 Step 4: Running test suite" -InformationAction Continue

        dotnet test BusBuddy.sln --configuration Release --no-build --verbosity normal
        if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

        Write-Information "✅ All tests passed" -InformationAction Continue
    }

    # Step 5: Publish application
    Write-Information "📦 Step 5: Publishing application" -InformationAction Continue

    $publishPath = ".\publish-$Environment"
    if (Test-Path $publishPath) {
        Remove-Item $publishPath -Recurse -Force
    }

    dotnet publish BusBuddy.WPF\BusBuddy.WPF.csproj --configuration Release --output $publishPath --no-build
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

    # Copy environment-specific configuration
    $configFile = "appsettings.$($Environment.ToLower()).json"
    if (Test-Path $configFile) {
        Copy-Item $configFile "$publishPath\appsettings.json" -Force
        Write-Information "📋 Environment configuration copied: $configFile" -InformationAction Continue
    }

    Write-Information "✅ Application published to $publishPath" -InformationAction Continue

    # Step 6: Create deployment package
    Write-Information "📁 Step 6: Creating deployment package" -InformationAction Continue

    $packageName = "BusBuddy-$Version-$Environment-$(Get-Date -Format 'yyyyMMdd-HHmmss').zip"
    Compress-Archive -Path "$publishPath\*" -DestinationPath $packageName -Force

    Write-Information "✅ Deployment package created: $packageName" -InformationAction Continue

    # Step 7: Deployment summary
    Write-Information "📊 Step 7: Deployment Summary" -InformationAction Continue
    Write-Information "═══════════════════════════════════════" -InformationAction Continue
    Write-Information "🎯 Environment: $Environment" -InformationAction Continue
    Write-Information "📦 Version: $Version" -InformationAction Continue
    Write-Information "📁 Package: $packageName" -InformationAction Continue
    Write-Information "📂 Publish Path: $publishPath" -InformationAction Continue
    Write-Information "🕐 Completed: $(Get-Date)" -InformationAction Continue
    Write-Information "═══════════════════════════════════════" -InformationAction Continue

    if ($Environment -eq "Staging") {
        Write-Information "🎯 Next Steps for Staging:" -InformationAction Continue
        Write-Information "1. Deploy package to staging environment" -InformationAction Continue
        Write-Information "2. Run UAT test scenarios" -InformationAction Continue
        Write-Information "3. Collect user feedback" -InformationAction Continue
        Write-Information "4. Validate performance metrics" -InformationAction Continue
    } else {
        Write-Information "🎯 Next Steps for Production:" -InformationAction Continue
        Write-Information "1. Deploy package to production environment" -InformationAction Continue
        Write-Information "2. Run smoke tests" -InformationAction Continue
        Write-Information "3. Monitor Application Insights dashboard" -InformationAction Continue
        Write-Information "4. Verify all critical features" -InformationAction Continue
    }

    Write-Information "🎉 Deployment preparation completed successfully!" -InformationAction Continue

} catch {
    Write-Error "❌ Deployment failed: $($_.Exception.Message)"
    Write-Error "📋 Check logs and resolve issues before retrying"
    exit 1
}
