# BusBuddy Route Testing PowerShell Script
# Optimized for bb-test integration and rapid validation

[CmdletBinding()]
param(
    [ValidateSet("Unit", "Integration", "UI", "All")]
    [string]$TestSuite = "Unit",

    [switch]$GenerateReport,

    [switch]$WatchMode
)

# Set error handling
$ErrorActionPreference = "Stop"

# Import BusBuddy testing module
Import-Module "$PSScriptRoot\..\PowerShell\BusBuddy.Testing.psm1" -Force

Write-Host "🚌 BusBuddy Route Testing Suite" -ForegroundColor Cyan
Write-Host "Test Suite: $TestSuite" -ForegroundColor Yellow

try {
    switch ($TestSuite) {
        "Unit" {
            Write-Host "🧪 Running Unit Tests for RouteService..." -ForegroundColor Green

            # Run RouteService unit tests
            dotnet test "BusBuddy.Tests\Core\RouteServiceTests.cs" --verbosity normal --logger "console;verbosity=detailed"

            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Unit tests passed!" -ForegroundColor Green
            } else {
                Write-Host "❌ Unit tests failed!" -ForegroundColor Red
                exit 1
            }
        }

        "Integration" {
            Write-Host "🔗 Running Integration Tests..." -ForegroundColor Green

            # Test database connectivity
            Write-Host "📊 Testing database integration..." -ForegroundColor Yellow
            # Integration tests are filtered by category to avoid running unrelated tests
            dotnet test "BusBuddy.Tests\Core\DataLayerTests.cs" --filter "Category=Integration"

            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Integration tests passed!" -ForegroundColor Green
            } else {
                Write-Host "❌ Integration tests failed!" -ForegroundColor Red
                exit 1
            }
        }

        "UI" {
            Write-Host "🎨 Running UI Tests..." -ForegroundColor Green

            # Test RouteAssignmentView
            Write-Host "🖥️ Testing RouteAssignmentView..." -ForegroundColor Yellow
            dotnet test "BusBuddy.Tests\UI" --filter "Category=UI"

            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ UI tests passed!" -ForegroundColor Green
            } else {
                Write-Host "❌ UI tests failed!" -ForegroundColor Red
                exit 1
            }
        }

        "All" {
            Write-Host "🚀 Running Complete Test Suite..." -ForegroundColor Green

            # Run all tests
            dotnet test "BusBuddy.sln" --verbosity normal

            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ All tests passed!" -ForegroundColor Green
            } else {
                Write-Host "❌ Some tests failed!" -ForegroundColor Red
                exit 1
            }
        }
    }

    # Generate report if requested
    if ($GenerateReport) {
        Write-Host "📄 Generating test report..." -ForegroundColor Yellow

        $reportPath = "TestResults\RouteService-TestReport-$(Get-Date -Format 'yyyyMMdd-HHmmss').xml"
        dotnet test "BusBuddy.sln" --logger "trx;LogFileName=$reportPath"

        Write-Host "📄 Report generated: $reportPath" -ForegroundColor Green
    }

    # Watch mode for continuous testing
    if ($WatchMode) {
        Write-Host "👀 Starting watch mode..." -ForegroundColor Yellow
        dotnet watch test "BusBuddy.Tests" --project "BusBuddy.Tests\BusBuddy.Tests.csproj"
    }

} catch {
    Write-Host "💥 Test execution failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host "🎉 Route testing completed successfully!" -ForegroundColor Green
