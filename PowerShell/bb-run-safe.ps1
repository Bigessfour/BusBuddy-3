# BusBuddy Safe Run Command
# Safe execution with error capture and monitoring

# Import the exception capture module
$modulePath = Join-Path -Path $PSScriptRoot -ChildPath "Modules\BusBuddy.ExceptionCapture.psm1"
Import-Module $modulePath -Force

# Parse any command-line arguments
param(
    [Parameter()]
    [int]$MonitorDuration = 15,  # Default to 15 minutes of monitoring

    [Parameter()]
    [switch]$SkipTests,          # Optional flag to skip tests

    [Parameter()]
    [switch]$CleanBuild          # Optional flag for clean build
)

# Get the project root directory
$projectRoot = Split-Path -Parent $PSScriptRoot

# Run BusBuddy with error capture
Write-Host "🚌 Starting BusBuddy with error capture (monitoring for $MonitorDuration minutes)..." -ForegroundColor Cyan

if ($CleanBuild) {
    Write-Host "🧹 Performing clean build..." -ForegroundColor Cyan
    Push-Location $projectRoot
    dotnet clean BusBuddy.sln
    Pop-Location
}

if (-not $SkipTests) {
    Write-Host "🧪 Running tests..." -ForegroundColor Cyan
    Push-Location $projectRoot
    dotnet test BusBuddy.sln
    Pop-Location
}

# Start the application with error capture
Start-BusBuddyWithCapture -MonitorDuration $MonitorDuration -ProjectPath $projectRoot

# Check for error report
Write-Host "📊 Checking recent error reports..." -ForegroundColor Cyan
$report = Get-BusBuddyErrorReport -Days 1

if ($report -and $report.TotalErrors -gt 0) {
    Write-Host "⚠️ Found $($report.TotalErrors) errors in recent execution." -ForegroundColor Yellow
    if ($report.HighPriorityErrors -gt 0) {
        Write-Host "❗ High Priority Errors: $($report.HighPriorityErrors)" -ForegroundColor Red
    }
    Write-Host "📝 Most recent error occurred in: $($report.MostRecentError.File)" -ForegroundColor Yellow
} else {
    Write-Host "✅ No errors detected in recent execution!" -ForegroundColor Green
}

Write-Host "✅ BusBuddy safe run completed." -ForegroundColor Green
