#requires -Version 7.5

<#
.SYNOPSIS
    Initialize BusBuddy Testing Module

.DESCRIPTION
    Validates and initializes the BusBuddy Testing module, ensuring all dependencies
    and configuration are properly set up for VS Code NUnit Test Runner integration.

.EXAMPLE
    .\Initialize-BusBuddyTesting.ps1
    Initializes the testing module and validates the environment
#>

[CmdletBinding()]
param()

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Write-InitHeader {
    [CmdletBinding()]
    param()

    $header = @"
🚌 BusBuddy Testing Module Initialization
==========================================
Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
PowerShell: $($PSVersionTable.PSVersion)
Module Path: $PSScriptRoot
"@

    Write-Host $header -ForegroundColor Cyan
    Write-Host ""
}

function Test-Prerequisites {
    [CmdletBinding()]
    param()

    Write-Host "🔍 Checking prerequisites..." -ForegroundColor Yellow

    $checks = @()

    # PowerShell version
    if ($PSVersionTable.PSVersion -ge [version]"7.5.0") {
        $checks += @{ Name = "PowerShell 7.5+"; Status = "✅"; Details = "v$($PSVersionTable.PSVersion)" }
    } else {
        $checks += @{ Name = "PowerShell 7.5+"; Status = "❌"; Details = "Current: v$($PSVersionTable.PSVersion)" }
    }

    # .NET CLI
    try {
        $dotnetVersion = dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            $checks += @{ Name = ".NET CLI"; Status = "✅"; Details = "v$dotnetVersion" }
        } else {
            $checks += @{ Name = ".NET CLI"; Status = "❌"; Details = "Not found" }
        }
    } catch {
        $checks += @{ Name = ".NET CLI"; Status = "❌"; Details = "Not available" }
    }

    # BusBuddy workspace
    $workspaceRoot = $null
    $currentPath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    while ($currentPath -and $currentPath -ne [System.IO.Path]::GetPathRoot($currentPath)) {
        $solutionPath = Join-Path $currentPath "BusBuddy.sln"
        if (Test-Path $solutionPath) {
            $workspaceRoot = $currentPath
            break
        }
        $currentPath = Split-Path $currentPath -Parent
    }

    if ($workspaceRoot) {
        $checks += @{ Name = "BusBuddy Workspace"; Status = "✅"; Details = $workspaceRoot }
    } else {
        $checks += @{ Name = "BusBuddy Workspace"; Status = "❌"; Details = "Not found" }
    }

    # Display results
    foreach ($check in $checks) {
        Write-Host "  $($check.Status) $($check.Name): $($check.Details)" -ForegroundColor White
    }

    $failedChecks = $checks | Where-Object { $_.Status -eq "❌" }
    if ($failedChecks.Count -gt 0) {
        Write-Host ""
        Write-Host "❌ Prerequisites not met. Please resolve the issues above." -ForegroundColor Red
        return $false
    }

    Write-Host ""
    Write-Host "✅ All prerequisites satisfied!" -ForegroundColor Green
    return $true
}

function Import-BusBuddyTestingModule {
    [CmdletBinding()]
    param()

    Write-Host "📦 Loading BusBuddy.Testing module..." -ForegroundColor Yellow

    try {
        $modulePath = Join-Path $PSScriptRoot "BusBuddy.Testing.psd1"

        if (-not (Test-Path $modulePath)) {
            throw "Module manifest not found: $modulePath"
        }

        # Remove existing module if loaded
        if (Get-Module BusBuddy.Testing -ErrorAction SilentlyContinue) {
            Remove-Module BusBuddy.Testing -Force
        }

        # Import module
        Import-Module $modulePath -Force -PassThru | Out-Null

        # Verify import
        $module = Get-Module BusBuddy.Testing
        if ($module) {
            Write-Host "  ✅ Module loaded: BusBuddy.Testing v$($module.Version)" -ForegroundColor White

            # Check exported functions
            $functions = Get-Command -Module BusBuddy.Testing
            Write-Host "  📋 Functions exported: $($functions.Count)" -ForegroundColor White

            foreach ($function in $functions | Sort-Object Name) {
                Write-Host "     • $($function.Name)" -ForegroundColor Gray
            }

            # Check aliases
            $aliases = Get-Alias | Where-Object { $_.Source -eq 'BusBuddy.Testing' }
            if ($aliases.Count -gt 0) {
                Write-Host "  🏷️ Aliases available: $($aliases.Count)" -ForegroundColor White
                foreach ($alias in $aliases | Sort-Object Name) {
                    Write-Host "     • $($alias.Name) -> $($alias.Definition)" -ForegroundColor Gray
                }
            }

            return $true
        } else {
            throw "Module import failed"
        }

    } catch {
        Write-Host "  ❌ Module import failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Test-ModuleFunctionality {
    [CmdletBinding()]
    param()

    Write-Host "🧪 Testing module functionality..." -ForegroundColor Yellow

    try {
        # Test initialization
        Write-Host "  🔧 Testing environment initialization..." -ForegroundColor White
        $initResult = Initialize-BusBuddyTestEnvironment

        if ($initResult) {
            Write-Host "  ✅ Environment initialization successful" -ForegroundColor Green
        } else {
            Write-Host "  ❌ Environment initialization failed" -ForegroundColor Red
            return $false
        }

        # Test compliance check
        Write-Host "  📋 Testing compliance validation..." -ForegroundColor White
        $complianceResult = Test-BusBuddyCompliance

        if ($complianceResult) {
            Write-Host "  ✅ Compliance validation successful" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️ Compliance issues detected (see details above)" -ForegroundColor Yellow
        }

        # Test status function
        Write-Host "  📊 Testing status function..." -ForegroundColor White
        Get-BusBuddyTestStatus | Out-Null
        Write-Host "  ✅ Status function working" -ForegroundColor Green

        return $true

    } catch {
        Write-Host "  ❌ Functionality test failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Show-QuickStart {
    [CmdletBinding()]
    param()

    $quickStart = @"

🚀 BusBuddy Testing Quick Start
===============================

Essential Commands:
  bb-test                    # Run all tests
  bb-test -TestSuite Unit    # Run unit tests only
  bb-test-watch              # Start continuous testing
  bb-test-report             # Generate test report
  bb-test-status             # Check current status
  bb-test-init               # Validate environment

Advanced Usage:
  Start-BusBuddyTest -TestSuite Integration -Detailed
  Start-BusBuddyTestWatch -TestSuite Core
  New-BusBuddyTestReport

VS Code Integration:
  • Use Task Explorer to run "Phase 4 Modular Tests"
  • Install NUnit Test Runner extension for enhanced experience
  • Tests are automatically discovered and displayed in Test Explorer

For detailed help on any function:
  Get-Help Start-BusBuddyTest -Full
  Get-Help Start-BusBuddyTestWatch -Examples
"@

    Write-Host $quickStart -ForegroundColor Cyan
}

# Main execution
try {
    Write-InitHeader

    # Check prerequisites
    if (-not (Test-Prerequisites)) {
        exit 1
    }

    Write-Host ""

    # Import module
    if (-not (Import-BusBuddyTestingModule)) {
        exit 1
    }

    Write-Host ""

    # Test functionality
    if (-not (Test-ModuleFunctionality)) {
        Write-Host ""
        Write-Host "⚠️ Some functionality tests failed, but basic module is loaded." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "🎉 BusBuddy Testing Module initialized successfully!" -ForegroundColor Green

    # Show quick start guide
    Show-QuickStart

} catch {
    Write-Host ""
    Write-Host "❌ Initialization failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
