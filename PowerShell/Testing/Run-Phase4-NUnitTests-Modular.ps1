<#
Thin wrapper maintained for backward compatibility.
Prefer: Start-BusBuddyPhase4TestAdvanced (module function).
Parameters mirror advanced function subset.
#>
param(
    [ValidateSet('All','Unit','Integration','Validation','Core','WPF')][string]$TestSuite='All',
    [switch]$Detailed,
    [switch]$CollectCoverage,
    [switch]$SaveFullOutput
)

Import-Module (Join-Path (Join-Path $PSScriptRoot '..') 'Modules/BusBuddy.Testing/BusBuddy.Testing.psd1') -Force
return Start-BusBuddyPhase4TestAdvanced -TestSuite $TestSuite -Detailed:$Detailed -CollectCoverage:$CollectCoverage -SaveFullOutput:$SaveFullOutput
<# Archived (2025-08-12): Modular NUnit bridge replaced by bbTest. See Documentation/Archive/LegacyScripts/Run-Phase4-NUnitTests-Modular.ps1 #>
throw "Run-Phase4-NUnitTests-Modular.ps1 archived. Use bbTest"

<#
# legacy snippet (unreachable)
Write-Information "`n=== TEST OUTPUT ===" -InformationAction Continue
        $testStdout -split "`n" | ForEach-Object {
            if ($_ -match "Passed|✓") {
                Write-Output $_
            } elseif ($_ -match "Failed|✗|ERROR") {
                Write-Error $_
            } elseif ($_ -match "Skipped") {
                Write-Warning $_
            } else {
                Write-Information $_ -InformationAction Continue
            }
        }

        if ($testStderr) {
            Write-Error "`n=== TEST ERRORS ==="
            Write-Error $testStderr
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
#>

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
        Write-Information ("Initial test run " + (if ($success) { "succeeded ✅" } else { "failed ❌" })) -InformationAction Continue

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
