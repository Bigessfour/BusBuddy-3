#requires -Version 7.5
<#
.SYNOPSIS
    Phase 4 Modular NUnit Test Runner - VS Code NUnit Test Runner Integration

.DESCRIPTION
    Microsoft PowerShell standards-compliant testing script that integrates with
    VS Code NUnit Test Runner extension for comprehensive test automation.

.PARAMETER TestSuite
    Specifies which test suite to run. Valid options: All, Unit, Integration, Validation, Core, WPF

.PARAMETER GenerateReport
    Generates a detailed markdown report with test results and compliance status

.PARAMETER WatchMode
    Enables continuous testing with file system monitoring

.PARAMETER Detailed
    Provides verbose output with detailed test information

.EXAMPLE
    .\Run-Phase4-NUnitTests-Modular.ps1 -TestSuite All -GenerateReport
    Runs all tests and generates a comprehensive report

.EXAMPLE
    .\Run-Phase4-NUnitTests-Modular.ps1 -TestSuite Unit -WatchMode
    Runs unit tests in continuous watch mode

.NOTES
    Version: 1.0.0
    Author: BusBuddy Development Team
    Requires: PowerShell 7.5.2+, .NET 8.0, NUnit Test Runner Extension
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
    [string]$TestSuite = 'All',

    [Parameter(Mandatory = $false)]
    [switch]$GenerateReport,

    [Parameter(Mandatory = $false)]
    [switch]$WatchMode,

    [Parameter(Mandatory = $false)]
    [switch]$Detailed
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

# Constants
$WORKSPACE_ROOT = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$SOLUTION_FILE = Join-Path $WORKSPACE_ROOT "BusBuddy.sln"
$TEST_RESULTS_DIR = Join-Path $WORKSPACE_ROOT "TestResults"
$REPORTS_DIR = Join-Path $WORKSPACE_ROOT "Documentation\Reports"

# Ensure directories exist
if (-not (Test-Path $TEST_RESULTS_DIR)) {
    New-Item -ItemType Directory -Path $TEST_RESULTS_DIR -Force | Out-Null
}
if (-not (Test-Path $REPORTS_DIR)) {
    New-Item -ItemType Directory -Path $REPORTS_DIR -Force | Out-Null
}

function Write-BusBuddyHeader {
    [CmdletBinding()]
    param()

    $header = @"
🚌 BusBuddy Phase 4 Modular Testing System
=============================================
Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Test Suite: $TestSuite
Watch Mode: $($WatchMode.IsPresent)
Report Generation: $($GenerateReport.IsPresent)
"@

    Write-Information $header -InformationAction Continue
    Write-Information "" -InformationAction Continue
}

function Get-TestFilter {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Suite
    )

    switch ($Suite) {
        'All' { return $null }
        'Unit' { return 'Category=Unit' }
        'Integration' { return 'Category=Integration' }
        'Validation' { return 'Category=Validation' }
        'Core' { return 'TestName~Core' }
        'WPF' { return 'TestName~WPF' }
        default { return $null }
    }
}

function Invoke-TestExecution {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Filter
    )

    try {
        Write-Information "🔍 Discovering tests..." -InformationAction Continue

        # Build solution first with enhanced output capture
        Write-Information "🏗️ Building solution..." -InformationAction Continue

        # Use enhanced build capture via module if available
        $buildOutputModule = Join-Path (Split-Path $PSScriptRoot -Parent) "Modules\BusBuddy.BuildOutput\BusBuddy.BuildOutput.psd1"
        if (Test-Path $buildOutputModule) {
            Import-Module $buildOutputModule -Force -ErrorAction SilentlyContinue
            $buildResult = Get-BusBuddyBuildOutput -ProjectPath $SOLUTION_FILE -Configuration "Debug" -SaveToFile

            if ($buildResult.ExitCode -ne 0) {
                Write-Error "Build failed. Cannot proceed with testing. Check $($buildResult.OutputFile) for details."
                return $false
            }
        } else {
            # Fallback with output capture
            $buildOutput = & dotnet build $SOLUTION_FILE --configuration Debug --verbosity detailed 2>&1

            # Save build output
            $buildLogFile = Join-Path $TEST_RESULTS_DIR "phase4-build-log-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
            $buildOutput | Out-File -FilePath $buildLogFile -Encoding UTF8 -Width 500
            Write-Information "Build output saved to: $buildLogFile" -InformationAction Continue

            if ($LASTEXITCODE -ne 0) {
                Write-Error "Build failed. Cannot proceed with testing. Check $buildLogFile for details."
                return $false
            }
        }

        # Prepare test command with enhanced output capture
        $testArgs = @(
            'test'
            $SOLUTION_FILE
            '--configuration', 'Debug'
            '--logger', 'trx'
            '--results-directory', $TEST_RESULTS_DIR
            '--collect:"XPlat Code Coverage"'
            '--verbosity', 'detailed'  # Always use detailed for Phase 4
            '--no-build'  # Already built above
        )

        if ($Filter) {
            $testArgs += '--filter', $Filter
            Write-Information "📋 Filter applied: $Filter" -InformationAction Continue
        }

        Write-Information "🧪 Executing tests with enhanced output capture..." -InformationAction Continue

        # Execute tests with full output capture
        $testStartTime = Get-Date

        # Prepare output file paths
        $testStdOutPath = Join-Path $TEST_RESULTS_DIR "phase4-test-stdout-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
        $testStdErrPath = Join-Path $TEST_RESULTS_DIR "phase4-test-stderr-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"

        # Use Start-Process for complete output capture
        $testProcess = Start-Process -FilePath "dotnet" -ArgumentList $testArgs -RedirectStandardOutput $testStdOutPath -RedirectStandardError $testStdErrPath -NoNewWindow -PassThru
        $testProcess.WaitForExit()
        $testExitCode = $testProcess.ExitCode

        $testEndTime = Get-Date
        $testDuration = $testEndTime - $testStartTime

        # Read output from files
        $testStdout = if (Test-Path $testStdOutPath) { Get-Content $testStdOutPath -Raw } else { "" }
        $testStderr = if (Test-Path $testStdErrPath) { Get-Content $testStdErrPath -Raw } else { "" }

        # Save complete test output
        $testLogFile = Join-Path $TEST_RESULTS_DIR "phase4-test-log-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
        $completeTestOutput = @"
=== PHASE 4 NUNIT TEST LOG ===
Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Filter: $Filter
Duration: $($testDuration.TotalSeconds) seconds
Exit Code: $testExitCode

=== TEST STDOUT ===
$testStdout

=== TEST STDERR ===
$testStderr

=== TEST SUMMARY ===
"@

        $completeTestOutput | Out-File -FilePath $testLogFile -Encoding UTF8 -Width 500
        Write-Information "Complete test output saved to: $testLogFile" -InformationAction Continue

        # Display output to console (with some formatting)
        Write-Host "`n=== TEST OUTPUT ===" -ForegroundColor Cyan
        $testStdout -split "`n" | ForEach-Object {
            if ($_ -match "Passed|✓") {
                Write-Host $_ -ForegroundColor Green
            } elseif ($_ -match "Failed|✗|ERROR") {
                Write-Host $_ -ForegroundColor Red
            } elseif ($_ -match "Skipped") {
                Write-Host $_ -ForegroundColor Yellow
            } else {
                Write-Host $_
            }
        }

        if ($testStderr) {
            Write-Host "`n=== TEST ERRORS ===" -ForegroundColor Red
            Write-Host $testStderr -ForegroundColor Red
        }

        # Parse results
        $testSuccess = $testExitCode -eq 0

        Write-Information "📊 Test execution completed in $($testDuration.TotalSeconds) seconds" -InformationAction Continue
        Write-Information "Exit Code: $testExitCode" -InformationAction Continue
        Write-Information "Full log: $testLogFile" -InformationAction Continue

        return $testSuccess

    } catch {
        Write-Error "Test execution failed: $($_.Exception.Message)"
        return $false
    }
}

function Start-WatchMode {
    [CmdletBinding()]
    param()

    Write-Information "👀 Starting watch mode..." -InformationAction Continue
    Write-Information "   Monitoring: *.cs, *.xaml files" -InformationAction Continue
    Write-Information "   Press Ctrl+C to exit" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    $lastRun = Get-Date
    $watchPaths = @(
        Join-Path $WORKSPACE_ROOT "BusBuddy.Core"
        Join-Path $WORKSPACE_ROOT "BusBuddy.WPF"
        Join-Path $WORKSPACE_ROOT "BusBuddy.Tests"
    )

    try {
        while ($true) {
            $changed = $false

            foreach ($path in $watchPaths) {
                if (Test-Path $path) {
                    $recentFiles = Get-ChildItem -Path $path -Recurse -Include "*.cs", "*.xaml" |
                        Where-Object { $_.LastWriteTime -gt $lastRun }

                    if ($recentFiles.Count -gt 0) {
                        $changed = $true
                        Write-Information "🔄 Changes detected in $($recentFiles.Count) file(s)" -InformationAction Continue
                        foreach ($file in $recentFiles) {
                            Write-Information "   Modified: $($file.Name)" -InformationAction Continue
                        }
                        break
                    }
                }
            }

            if ($changed) {
                Write-Information "" -InformationAction Continue
                Write-Information "🚀 Re-running tests..." -InformationAction Continue
                $lastRun = Get-Date

                $filter = Get-TestFilter -Suite $TestSuite
                $success = Invoke-TestExecution -Filter $filter

                Write-Information "" -InformationAction Continue
                Write-Information "⏰ Waiting for changes... ($(Get-Date -Format 'HH:mm:ss'))" -InformationAction Continue
            }

            Start-Sleep -Seconds 2
        }
    } catch [System.Management.Automation.HaltCommandException] {
        Write-Information "" -InformationAction Continue
        Write-Information "👋 Watch mode stopped by user" -InformationAction Continue
    }
}

function New-TestReport {
    [CmdletBinding()]
    param()

    try {
        $reportPath = Join-Path $REPORTS_DIR "TestResults-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"

        Write-Information "📝 Generating test report..." -InformationAction Continue

        # Get latest test results
        $testResultFiles = Get-ChildItem -Path $TEST_RESULTS_DIR -Filter "*.trx" | Sort-Object LastWriteTime -Descending

        if ($testResultFiles.Count -eq 0) {
            Write-Warning "No test result files found for report generation"
            return
        }

        $latestResult = $testResultFiles[0]
        $testContent = Get-Content $latestResult.FullName -Raw

        # Parse results
        $passedTests = ([regex]'outcome="Passed"').Matches($testContent).Count
        $failedTests = ([regex]'outcome="Failed"').Matches($testContent).Count
        $totalTests = $passedTests + $failedTests
        $successRate = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 2) } else { 0 }

        $report = @"
# 🧪 BusBuddy Test Report

**Generated**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Test Suite**: $TestSuite
**Result File**: $($latestResult.Name)

## 📊 Summary

| Metric | Value |
|--------|-------|
| Total Tests | $totalTests |
| Passed | $passedTests |
| Failed | $failedTests |
| Success Rate | $successRate% |

## 🎯 Status

$(if ($failedTests -eq 0) { "✅ **All tests passed!**" } else { "❌ **$failedTests test(s) failed**" })

## 📈 Details

- **Solution**: BusBuddy.sln
- **Configuration**: Debug
- **Framework**: .NET 8.0
- **Test Framework**: NUnit 4.6.0
- **VS Code Integration**: NUnit Test Runner Extension

## 🔍 Test Categories

- **Unit Tests**: Core business logic validation
- **Integration Tests**: Database and service interactions
- **Validation Tests**: Input validation and error handling
- **Core Tests**: BusBuddy.Core project tests
- **WPF Tests**: UI and presentation layer tests

---
*Report generated by BusBuddy Phase 4 Modular Testing System*
"@

        Set-Content -Path $reportPath -Value $report -Encoding UTF8
        Write-Information "📄 Report saved to: $reportPath" -InformationAction Continue

    } catch {
        Write-Error "Failed to generate report: $($_.Exception.Message)"
    }
}

# Main execution
try {
    Write-BusBuddyHeader

    # Validate workspace
    if (-not (Test-Path $SOLUTION_FILE)) {
        Write-Error "Solution file not found: $SOLUTION_FILE"
        exit 1
    }

    if ($WatchMode.IsPresent) {
        # Run tests once first, then start watch mode
        $filter = Get-TestFilter -Suite $TestSuite
        $success = Invoke-TestExecution -Filter $filter

        if ($GenerateReport.IsPresent) {
            New-TestReport
        }

        Start-WatchMode
    } else {
        # Single test run
        $filter = Get-TestFilter -Suite $TestSuite
        $success = Invoke-TestExecution -Filter $filter

        if ($GenerateReport.IsPresent) {
            New-TestReport
        }

        # Exit with appropriate code
        exit $(if ($success) { 0 } else { 1 })
    }

} catch {
    Write-Error "Script execution failed: $($_.Exception.Message)"
    exit 1
}
