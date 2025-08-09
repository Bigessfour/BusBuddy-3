#requires -Version 7.5

<#
.SYNOPSIS
    BusBuddy Testing Module — Microsoft PowerShell standards compliant

.DESCRIPTION
    Comprehensive testing infrastructure for BusBuddy with VS Code NUnit Test Runner integration.
    Provides category-based testing, watch mode, and detailed reporting capabilities.

.NOTES
    Version: 1.0.0
    Author: BusBuddy Development Team
    Requires: PowerShell 7.5.2+, .NET 8.0, VS Code NUnit Test Runner Extension
#>

Set-StrictMode -Version 3.0

# Module-level variables
$Script:WorkspaceRoot = $null
$Script:SolutionFile = $null
$Script:TestResultsDir = $null
$Script:ReportsDir = $null

#region Private Functions

function Initialize-BusBuddyPaths {
    [CmdletBinding()]
    param()

    try {
        # Detect workspace root
        $currentPath = $PWD.Path
        while ($currentPath -and $currentPath -ne [System.IO.Path]::GetPathRoot($currentPath)) {
            $solutionPath = Join-Path $currentPath "BusBuddy.sln"
            if (Test-Path $solutionPath) {
                $Script:WorkspaceRoot = $currentPath
                $Script:SolutionFile = $solutionPath
                break
            }
            $currentPath = Split-Path $currentPath -Parent
        }

        if (-not $Script:WorkspaceRoot) {
            throw "BusBuddy workspace not found. Please run from within the BusBuddy project directory."
        }

        # Set up directories
        $Script:TestResultsDir = Join-Path $Script:WorkspaceRoot "TestResults"
        $Script:ReportsDir = Join-Path $Script:WorkspaceRoot "Documentation\Reports"

        # Ensure directories exist
        @($Script:TestResultsDir, $Script:ReportsDir) | ForEach-Object {
            if (-not (Test-Path $_)) {
                New-Item -ItemType Directory -Path $_ -Force | Out-Null
            }
        }

        Write-Verbose "BusBuddy workspace initialized: $Script:WorkspaceRoot"

    } catch {
        Write-Error "Failed to initialize BusBuddy paths: $($_.Exception.Message)"
        throw
    }
}

function Invoke-BusBuddyBuild {
    [CmdletBinding()]
    param()

    try {
        Write-Information "🏗️ Building BusBuddy solution..." -InformationAction Continue

            # Prefer module-based enhanced build output if available
            $buildOutputModule = Join-Path (Split-Path $PSScriptRoot -Parent) "BusBuddy.BuildOutput" "BusBuddy.BuildOutput.psd1"
            if (Test-Path $buildOutputModule) {
                Import-Module $buildOutputModule -Force -ErrorAction SilentlyContinue
                if (Get-Command -Name Get-BusBuddyBuildOutput -ErrorAction SilentlyContinue) {
                    $buildResult = Get-BusBuddyBuildOutput -ProjectPath $Script:SolutionFile -Configuration "Debug"
                    return ($buildResult.ExitCode -eq 0)
                }
            }

            # Fallback to original with output capture
            $buildArgs = @(
                'build'
                $Script:SolutionFile
                '--configuration', 'Debug'
                '--verbosity', 'detailed'  # More verbose for troubleshooting
            )

            # Capture all output streams
            $buildOutput = & dotnet @buildArgs 2>&1

            # Save build output to file for analysis
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $buildLogFile = Join-Path $Script:TestResultsDir "build-log-$timestamp.txt"
            $buildOutput | Out-File -FilePath $buildLogFile -Encoding UTF8 -Width 500

            Write-Information "Build output saved to: $buildLogFile" -InformationAction Continue

            # Display errors prominently
            $errorLines = $buildOutput | Where-Object { $_ -match "error|Error|ERROR|CS\d+|MSB\d+" }
            if ($errorLines) {
                Write-Warning "Build errors found:"
                $errorLines | ForEach-Object { Write-Warning $_ }
            }

            if ($LASTEXITCODE -ne 0) {
                throw "Build failed with exit code $LASTEXITCODE. See $buildLogFile for details."
            }

        Write-Information "✅ Build completed successfully" -InformationAction Continue
        Write-Information "\u2705 Build completed successfully" -InformationAction Continue
        return $true
    }

    } catch {
        Write-Error "Build failed: $($_.Exception.Message)"
        return $false
    }
}

function Get-TestFilterExpression {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite
    )

    switch ($TestSuite) {
        'All' { return $null }
        'Unit' { return 'Category=Unit' }
        'Integration' { return 'Category=Integration' }
        'Validation' { return 'Category=Validation' }
        'Core' { return 'TestName~Core' }
        'WPF' { return 'TestName~WPF' }
        default { return $null }
    }
}

function Invoke-TestRunner {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Filter,

        [Parameter(Mandatory = $false)]
        [switch]$Detailed
    )

    try {
        Write-Information "🧪 Executing tests..." -InformationAction Continue

        # Prepare test arguments using modern PowerShell syntax
        $verbosity = $Detailed.IsPresent ? 'detailed' : 'normal'

        $testArgs = @(
            'test'
            "`"$($Script:SolutionFile)`""
            '--configuration', 'Debug'
            '--logger', 'trx'
            '--logger', "console;verbosity=$verbosity"
            '--results-directory', "`"$($Script:TestResultsDir)`""
        )

        if ($Filter) {
            $testArgs += '--filter', $Filter
            Write-Information "📋 Filter applied: $Filter" -InformationAction Continue
        }

        Write-Information "DEBUG: Executing 'dotnet $($testArgs -join ' ')'" -InformationAction Continue

        # Execute tests using PowerShell 7.5.2 pipeline chain operators
        $exitCode = 0
        try {
            # Use Invoke-Expression for cleaner execution
            $commandLine = "dotnet " + ($testArgs -join ' ')
            Write-Information "Executing: $commandLine" -InformationAction Continue

            Invoke-Expression $commandLine
            $exitCode = $LASTEXITCODE ?? 0
        }
        catch {
            Write-Error "Test execution failed: $($_.Exception.Message)"
            $exitCode = 1
        }

        # Parse and display results
        $testSummary = Get-LatestTestResults
        if ($testSummary) {
            Write-Information "" -InformationAction Continue
            Write-Information "📈 Test Summary:" -InformationAction Continue
            Write-Information "   Total Tests: $($testSummary.Total)" -InformationAction Continue
            Write-Information "   Passed: $($testSummary.Passed)" -InformationAction Continue
            Write-Information "   Failed: $($testSummary.Failed)" -InformationAction Continue

            if ($testSummary.Failed -eq 0) {
                Write-Information "✅ All tests passed!" -InformationAction Continue
            } else {
                Write-Warning "❌ $($testSummary.Failed) test(s) failed!"
            }
        }

        return $exitCode -eq 0

    } catch {
        Write-Error "Test execution failed: $($_.Exception.Message)"
        return $false
    }
}

function Get-LatestTestResults {
    [CmdletBinding()]
    param()

    try {
        $testResultFiles = Get-ChildItem -Path $Script:TestResultsDir -Filter "*.trx" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending

        if ($testResultFiles.Count -eq 0) {
            Write-Warning "No test result files found"
            return $null
        }

        $latestResult = $testResultFiles[0]
        $testContent = Get-Content $latestResult.FullName -Raw

        # Parse test counts using regex
        $passedTests = ([regex]'outcome="Passed"').Matches($testContent).Count
        $failedTests = ([regex]'outcome="Failed"').Matches($testContent).Count
        $totalTests = $passedTests + $failedTests

        return @{
            Total = $totalTests
            Passed = $passedTests
            Failed = $failedTests
            ResultFile = $latestResult.FullName
            Timestamp = $latestResult.LastWriteTime
        }

    } catch {
        Write-Warning "Failed to parse test results: $($_.Exception.Message)"
        return $null
    }
}

#endregion

#region Public Functions

function Start-BusBuddyTest {
    <#
    .SYNOPSIS
        Executes BusBuddy tests with NUnit Test Runner integration

    .DESCRIPTION
        Runs specified test suite with optional filtering and detailed output.
        Integrates with VS Code NUnit Test Runner extension for enhanced experience.

    .PARAMETER TestSuite
        Specifies which test suite to run: All, Unit, Integration, Validation, Core, WPF

    .PARAMETER Detailed
        Provides verbose output with detailed test information

    .EXAMPLE
        Start-BusBuddyTest -TestSuite All
        Runs all tests in the solution

    .EXAMPLE
        Start-BusBuddyTest -TestSuite Unit -Detailed
        Runs unit tests with detailed output
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All',

        [Parameter(Mandatory = $false)]
        [switch]$Detailed
    )

    try {
        # Check PowerShell version compatibility first (MVP requirement)
        if ($PSVersionTable.PSVersion.Major -lt 7) {
            Write-Error "❌ BusBuddy requires PowerShell 7.5+ for modern syntax support. Current version: $($PSVersionTable.PSVersion)"
            Write-Information "🔧 Quick Fix:" -InformationAction Continue
            Write-Information "   1. Install PowerShell 7: winget install --id Microsoft.PowerShell" -InformationAction Continue
            Write-Information "   2. Set VS Code default: Settings > terminal.integrated.defaultProfile.windows = 'PowerShell'" -InformationAction Continue
            Write-Information "   3. Restart VS Code terminal and verify: `$PSVersionTable" -InformationAction Continue
            return $false
        }

        # Initialize environment
        Initialize-BusBuddyPaths

        Write-Information "🚌 Starting BusBuddy Test Suite: $TestSuite" -InformationAction Continue
        Write-Information "📍 Workspace: $Script:WorkspaceRoot" -InformationAction Continue
        Write-Information "💻 PowerShell: $($PSVersionTable.PSVersion)" -InformationAction Continue
        Write-Information "" -InformationAction Continue

        # Build solution
        $buildSuccess = Invoke-BusBuddyBuild
        if (-not $buildSuccess) {
            Write-Error "Cannot proceed with testing due to build failure"
            return $false
        }

        # Get test filter
        $filter = Get-TestFilterExpression -TestSuite $TestSuite

        # Run tests
        $testSuccess = Invoke-TestRunner -Filter $filter -Detailed:$Detailed

        Write-Information "" -InformationAction Continue
        if ($testSuccess) {
            Write-Information "🎉 Test execution completed successfully!" -InformationAction Continue
        } else {
            Write-Warning "⚠️ Test execution completed with failures"
        }

        return $testSuccess

    } catch {
        Write-Error "Test execution failed: $($_.Exception.Message)"
        return $false
    }
}

function Start-BusBuddyTestWatch {
    <#
    .SYNOPSIS
        Starts continuous testing with file system monitoring

    .DESCRIPTION
        Monitors C# and XAML files for changes and automatically re-runs tests.
        Provides real-time feedback during development.

    .PARAMETER TestSuite
        Specifies which test suite to run: All, Unit, Integration, Validation, Core, WPF

    .EXAMPLE
        Start-BusBuddyTestWatch -TestSuite Unit
        Monitors files and re-runs unit tests on changes
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All'
    )

    try {
        # Initialize environment
        Initialize-BusBuddyPaths

        Write-Information "👀 Starting BusBuddy Test Watch Mode" -InformationAction Continue
        Write-Information "📍 Test Suite: $TestSuite" -InformationAction Continue
        Write-Information "🔍 Monitoring: *.cs, *.xaml files" -InformationAction Continue
        Write-Information "⌨️ Press Ctrl+C to exit" -InformationAction Continue
        Write-Information "" -InformationAction Continue

        # Run tests once initially
        $filter = Get-TestFilterExpression -TestSuite $TestSuite
        Invoke-TestRunner -Filter $filter
        $lastRun = Get-Date
        $watchPaths = @(
            Join-Path $Script:WorkspaceRoot "BusBuddy.Core"
            Join-Path $Script:WorkspaceRoot "BusBuddy.WPF"
            Join-Path $Script:WorkspaceRoot "BusBuddy.Tests"
        )

        Write-Information "⏰ Waiting for changes... ($(Get-Date -Format 'HH:mm:ss'))" -InformationAction Continue

        while ($true) {
            $changed = $false

            foreach ($path in $watchPaths) {
                if (Test-Path $path) {
                    $recentFiles = Get-ChildItem -Path $path -Recurse -Include "*.cs", "*.xaml" -ErrorAction SilentlyContinue |
                        Where-Object { $_.LastWriteTime -gt $lastRun }

                    if ($recentFiles.Count -gt 0) {
                        $changed = $true
                        Write-Information "" -InformationAction Continue
                        Write-Information "🔄 Changes detected in $($recentFiles.Count) file(s)" -InformationAction Continue

                        foreach ($file in $recentFiles | Select-Object -First 5) {
                            Write-Information "   📝 $($file.Name)" -InformationAction Continue
                        }

                        if ($recentFiles.Count -gt 5) {
                            Write-Information "   ... and $($recentFiles.Count - 5) more files" -InformationAction Continue
                        }

                        break
                    }
                }
            }

            if ($changed) {
                Write-Information "" -InformationAction Continue
                Write-Information "🚀 Re-running tests..." -InformationAction Continue
                $lastRun = Get-Date

                Invoke-TestRunner -Filter $filter

                Write-Information "" -InformationAction Continue
                Write-Information "⏰ Waiting for changes... ($(Get-Date -Format 'HH:mm:ss'))" -InformationAction Continue
            }

            Start-Sleep -Seconds 2
        }

    } catch [System.Management.Automation.HaltCommandException] {
        Write-Information "" -InformationAction Continue
        Write-Information "👋 Watch mode stopped by user" -InformationAction Continue
    } catch {
        Write-Error "Watch mode failed: $($_.Exception.Message)"
    }
}

function New-BusBuddyTestReport {
    <#
    .SYNOPSIS
        Generates a detailed markdown test report

    .DESCRIPTION
        Creates a comprehensive test report with results, compliance status,
        and actionable insights for development teams.

    .EXAMPLE
        New-BusBuddyTestReport
        Generates a test report based on latest test results
    #>

    [CmdletBinding()]
    param()

    try {
        # Initialize environment
        Initialize-BusBuddyPaths

        Write-Information "📝 Generating BusBuddy test report..." -InformationAction Continue

        # Get latest test results
        $testSummary = Get-LatestTestResults
        if (-not $testSummary) {
            Write-Warning "No test results found. Please run tests first."
            return
        }

        $reportPath = Join-Path $Script:ReportsDir "TestResults-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"
        $successRate = if ($testSummary.Total -gt 0) {
            [math]::Round(($testSummary.Passed / $testSummary.Total) * 100, 2)
        } else { 0 }

        $statusIcon = if ($testSummary.Failed -eq 0) { "✅" } else { "❌" }
        $statusText = if ($testSummary.Failed -eq 0) { "All tests passed!" } else { "$($testSummary.Failed) test(s) failed" }

        $report = @"
# 🧪 BusBuddy Test Report

**Generated**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Test Results**: $([System.IO.Path]::GetFileName($testSummary.ResultFile))
**Workspace**: $Script:WorkspaceRoot

## 📊 Executive Summary

| Metric | Value |
|--------|-------|
| Total Tests | $($testSummary.Total) |
| Passed | $($testSummary.Passed) |
| Failed | $($testSummary.Failed) |
| Success Rate | $successRate% |
| Last Run | $($testSummary.Timestamp.ToString('yyyy-MM-dd HH:mm:ss')) |

## 🎯 Overall Status

$statusIcon **$statusText**

## 🔧 Test Environment

- **Solution**: BusBuddy.sln
- **Configuration**: Debug
- **Framework**: .NET 8.0
- **Test Framework**: NUnit 4.6.0
- **VS Code Integration**: NUnit Test Runner Extension
- **PowerShell**: $($PSVersionTable.PSVersion)

## 📋 Available Test Suites

| Suite | Description | Filter |
|-------|-------------|--------|
| All | Complete test suite | No filter |
| Unit | Core business logic | Category=Unit |
| Integration | Database/service interactions | Category=Integration |
| Validation | Input validation & error handling | Category=Validation |
| Core | BusBuddy.Core project tests | TestName~Core |
| WPF | UI and presentation layer | TestName~WPF |

## 🚀 Quick Commands

\`\`\`powershell
# Run all tests
Start-BusBuddyTest -TestSuite All

# Run unit tests with detailed output
Start-BusBuddyTest -TestSuite Unit -Detailed

# Start watch mode for continuous testing
Start-BusBuddyTestWatch -TestSuite Unit

# Generate this report
New-BusBuddyTestReport
\`\`\`

## 📈 Recommendations

$(if ($testSummary.Failed -eq 0) {
"- ✅ All tests are passing - excellent work!
- 🔄 Consider running tests in watch mode during development
- 📊 Monitor test coverage to ensure comprehensive testing
- 🎯 Continue adding tests for new features"
} else {
"- ❌ Fix failing tests before proceeding with development
- 🔍 Review test output for specific failure details
- 🧪 Run individual test suites to isolate issues
- 📝 Update tests if business logic has changed"
})

---
*Report generated by BusBuddy Testing Module v1.0.0*
"@

        Set-Content -Path $reportPath -Value $report -Encoding UTF8
        Write-Information "📄 Report saved to: $reportPath" -InformationAction Continue

        return $reportPath

    } catch {
        Write-Error "Failed to generate test report: $($_.Exception.Message)"
    }
}

function Get-BusBuddyTestStatus {
    <#
    .SYNOPSIS
        Gets current test status and results summary

    .DESCRIPTION
        Provides a quick overview of the latest test results without running new tests.

    .EXAMPLE
        Get-BusBuddyTestStatus
        Shows current test status
    #>

    [CmdletBinding()]
    param()

    try {
        # Initialize environment
        Initialize-BusBuddyPaths

        $testSummary = Get-LatestTestResults
        if (-not $testSummary) {
            Write-Information "❓ No test results found. Run tests first with Start-BusBuddyTest" -InformationAction Continue
            return
        }

        $successRate = if ($testSummary.Total -gt 0) {
            [math]::Round(($testSummary.Passed / $testSummary.Total) * 100, 2)
        } else { 0 }

        Write-Information "🚌 BusBuddy Test Status" -InformationAction Continue
        Write-Information "========================" -InformationAction Continue
        Write-Information "📊 Total Tests: $($testSummary.Total)" -InformationAction Continue
        Write-Information "✅ Passed: $($testSummary.Passed)" -InformationAction Continue
        Write-Information "❌ Failed: $($testSummary.Failed)" -InformationAction Continue
        Write-Information "📈 Success Rate: $successRate%" -InformationAction Continue
        Write-Information "⏰ Last Run: $($testSummary.Timestamp.ToString('yyyy-MM-dd HH:mm:ss'))" -InformationAction Continue

        if ($testSummary.Failed -eq 0) {
            Write-Information "🎉 All tests are passing!" -InformationAction Continue
        } else {
            Write-Warning "⚠️ $($testSummary.Failed) test(s) are failing"
        }

        return $testSummary

    } catch {
        Write-Error "Failed to get test status: $($_.Exception.Message)"
    }
}

function Initialize-BusBuddyTestEnvironment {
    <#
    .SYNOPSIS
        Initializes and validates the BusBuddy test environment

    .DESCRIPTION
        Checks all dependencies, configurations, and environment setup
        required for BusBuddy testing infrastructure.

    .EXAMPLE
        Initialize-BusBuddyTestEnvironment
        Validates complete test environment setup
    #>

    [CmdletBinding()]
    param()

    try {
        Write-Information "🔧 Initializing BusBuddy Test Environment" -InformationAction Continue
        Write-Information "===========================================" -InformationAction Continue

        # Check workspace
        Initialize-BusBuddyPaths
        Write-Information "✅ Workspace detected: $Script:WorkspaceRoot" -InformationAction Continue

        # Check .NET CLI
        $dotnetVersion = dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ .NET CLI: $dotnetVersion" -InformationAction Continue
        } else {
            Write-Error "❌ .NET CLI not found or not working"
            return $false
        }

        # Check solution file
        if (Test-Path $Script:SolutionFile) {
            Write-Information "✅ Solution file: BusBuddy.sln" -InformationAction Continue
        } else {
            Write-Error "❌ Solution file not found: $Script:SolutionFile"
            return $false
        }

        # Check test projects
        $testProjects = Get-ChildItem -Path $Script:WorkspaceRoot -Recurse -Filter "*.Tests.csproj" -ErrorAction SilentlyContinue
        if ($testProjects.Count -gt 0) {
            Write-Information "✅ Test projects found: $($testProjects.Count)" -InformationAction Continue
            foreach ($project in $testProjects) {
                Write-Information "   📁 $($project.Name)" -InformationAction Continue
            }
        } else {
            Write-Warning "⚠️ No test projects found"
        }

        # Check directories
        Write-Information "✅ Test results directory: $Script:TestResultsDir" -InformationAction Continue
        Write-Information "✅ Reports directory: $Script:ReportsDir" -InformationAction Continue

        # Check PowerShell version
        if ($PSVersionTable.PSVersion -ge [version]"7.5.0") {
            Write-Information "✅ PowerShell: $($PSVersionTable.PSVersion) (Compatible)" -InformationAction Continue
        } else {
            Write-Error "❌ PowerShell: $($PSVersionTable.PSVersion) (Requires 7.5+)"
            Write-Information "🔧 Install PowerShell 7: winget install --id Microsoft.PowerShell" -InformationAction Continue
            return $false
        }

        Write-Information "" -InformationAction Continue
        Write-Information "🎉 BusBuddy test environment is ready!" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "💡 Quick Start:" -InformationAction Continue
        Write-Information "   Start-BusBuddyTest -TestSuite All" -InformationAction Continue
        Write-Information "   Start-BusBuddyTestWatch -TestSuite Unit" -InformationAction Continue
        Write-Information "   New-BusBuddyTestReport" -InformationAction Continue

        return $true

    } catch {
        Write-Error "Failed to initialize test environment: $($_.Exception.Message)"
        return $false
    }
}

function Test-BusBuddyCompliance {
    <#
    .SYNOPSIS
        Validates BusBuddy testing infrastructure compliance

    .DESCRIPTION
        Checks adherence to Microsoft PowerShell standards and BusBuddy
        testing requirements for quality assurance.

    .EXAMPLE
        Test-BusBuddyCompliance
        Runs comprehensive compliance validation
    #>

    [CmdletBinding()]
    param()

    try {
        Write-Information "🔍 BusBuddy Testing Compliance Check" -InformationAction Continue
        Write-Information "=====================================" -InformationAction Continue

        $complianceResults = @()

        # Check module structure
        $moduleManifest = Join-Path $PSScriptRoot "BusBuddy.Testing.psd1"
        if (Test-Path $moduleManifest) {
            $complianceResults += @{ Check = "Module Manifest"; Status = "✅ Pass"; Details = "BusBuddy.Testing.psd1 found" }
        } else {
            $complianceResults += @{ Check = "Module Manifest"; Status = "❌ Fail"; Details = "Module manifest missing" }
        }

        # Check exported functions
        $exportedFunctions = Get-Command -Module BusBuddy.Testing -ErrorAction SilentlyContinue
        if ($exportedFunctions.Count -ge 6) {
            $complianceResults += @{ Check = "Function Exports"; Status = "✅ Pass"; Details = "$($exportedFunctions.Count) functions exported" }
        } else {
            $complianceResults += @{ Check = "Function Exports"; Status = "❌ Fail"; Details = "Insufficient functions exported" }
        }

        # Check PowerShell version
        if ($PSVersionTable.PSVersion -ge [version]"7.5.0") {
            $complianceResults += @{ Check = "PowerShell Version"; Status = "✅ Pass"; Details = "v$($PSVersionTable.PSVersion)" }
        } else {
            $complianceResults += @{ Check = "PowerShell Version"; Status = "❌ Fail"; Details = "Requires PowerShell 7.5+" }
        }

        # Check workspace
        Initialize-BusBuddyPaths
        if ($Script:WorkspaceRoot) {
            $complianceResults += @{ Check = "Workspace Detection"; Status = "✅ Pass"; Details = "BusBuddy workspace found" }
        } else {
            $complianceResults += @{ Check = "Workspace Detection"; Status = "❌ Fail"; Details = "Workspace not detected" }
        }

        # Check .NET CLI
        $dotnetAvailable = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($dotnetAvailable) {
            $complianceResults += @{ Check = ".NET CLI"; Status = "✅ Pass"; Details = "dotnet command available" }
        } else {
            $complianceResults += @{ Check = ".NET CLI"; Status = "❌ Fail"; Details = ".NET CLI not found" }
        }

        # Display results
        Write-Information "" -InformationAction Continue
        foreach ($result in $complianceResults) {
            Write-Information "$($result.Status) $($result.Check): $($result.Details)" -InformationAction Continue
        }

        $passCount = ($complianceResults | Where-Object { $_.Status -like "*Pass*" }).Count
        $totalCount = $complianceResults.Count
        $complianceRate = [math]::Round(($passCount / $totalCount) * 100, 2)

        Write-Information "" -InformationAction Continue
        Write-Information "📊 Compliance Rate: $complianceRate% ($passCount/$totalCount)" -InformationAction Continue

        if ($complianceRate -eq 100) {
            Write-Information "🎉 Full compliance achieved!" -InformationAction Continue
        } else {
            Write-Warning "⚠️ Compliance issues detected"
        }

        return $complianceRate -eq 100

    } catch {
        Write-Error "Compliance check failed: $($_.Exception.Message)"
        return $false
    }
}

#endregion

#region Aliases

# Dedup policy: core BusBuddy module owns 'bb-test'. Keep secondary bb-test-* helpers here.
New-Alias -Name 'bb-test-watch' -Value 'Start-BusBuddyTestWatch' -Description 'Quick alias for Start-BusBuddyTestWatch'
New-Alias -Name 'bb-test-report' -Value 'New-BusBuddyTestReport' -Description 'Quick alias for New-BusBuddyTestReport'
New-Alias -Name 'bb-test-status' -Value 'Get-BusBuddyTestStatus' -Description 'Quick alias for Get-BusBuddyTestStatus'
New-Alias -Name 'bb-test-init' -Value 'Initialize-BusBuddyTestEnvironment' -Description 'Quick alias for Initialize-BusBuddyTestEnvironment'
New-Alias -Name 'bb-test-compliance' -Value 'Test-BusBuddyCompliance' -Description 'Quick alias for Test-BusBuddyCompliance'

#endregion

#region Module Exports

Export-ModuleMember -Function @(
    'Start-BusBuddyTest'
    'Start-BusBuddyTestWatch'
    'New-BusBuddyTestReport'
    'Get-BusBuddyTestStatus'
    'Initialize-BusBuddyTestEnvironment'
    'Test-BusBuddyCompliance'
) -Alias @(
    'bb-test-watch'
    'bb-test-report'
    'bb-test-status'
    'bb-test-init'
    'bb-test-compliance'
)

#endregion
#endregion
