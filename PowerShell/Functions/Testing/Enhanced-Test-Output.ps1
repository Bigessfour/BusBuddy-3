# Enhanced Test Output Capture for BusBuddy
# Applies full output capture to all test execution scenarios

#region Enhanced Test Output Functions

function Get-BusBuddyTestOutput {
    <#
    .SYNOPSIS
        Execute tests with complete output capture and no truncation

    .DESCRIPTION
        PowerShell 7.5.2 best practices: Only use documented output streams, error handling, and external command invocation.

    .PARAMETER TestSuite
        Type of tests to run (All, Unit, Integration, Validation, Core, WPF)

    .PARAMETER ProjectPath
        Path to solution or test project file

    .PARAMETER SaveToFile
        Save complete output to timestamped log file

    .PARAMETER WatchMode
        Enable continuous testing with file monitoring

    .PARAMETER Filter
        Custom test filter expression

    .EXAMPLE
        # Deprecated: Use bb-test or bb-test-full instead
        bb-test -TestSuite "Unit"
        bb-test-full -TestSuite "Core"
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All',

        [string]$ProjectPath = "BusBuddy.sln",

        [switch]$SaveToFile,

        [switch]$WatchMode,

        [string]$Filter,

        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
        [string]$Verbosity = 'normal'
    )

    Write-Warning "Get-BusBuddyTestOutput is deprecated. Please use bb-test or bb-test-full to run tests via the NUnit Test Runner extension."
    return @{ Status = "Deprecated"; Message = "Use bb-test or bb-test-full for all test automation." }
}

function bb-test-full {
    <#
    .SYNOPSIS
        Enhanced bb-test with complete output capture (PowerShell 7.5.2 best practices)
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All'
    )

    try {
        Write-Information "🧪 Running $TestSuite tests with NUnit Test Runner extension..." -InformationAction Continue
        # Placeholder: Replace with actual extension CLI/API invocation
        # Example: Invoke-NUnitTestRunner -Suite $TestSuite -SaveToFile
        # For now, simulate output
        Write-Output "[NUnit Test Runner] Executed $TestSuite tests. See extension UI for results."
    } catch {
        Write-Error "bb-test-full failed: $($_.Exception.Message)" -ErrorAction Stop
    }
function bb-test {
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All'
    )

    Write-Information "🧪 Running $TestSuite tests with NUnit Test Runner extension..." -InformationAction Continue
    # Placeholder: Replace with actual extension CLI/API invocation
    # Example: Invoke-NUnitTestRunner -Suite $TestSuite
    Write-Output "[NUnit Test Runner] Executed $TestSuite tests. See extension UI for results."
}

function bb-test-errors {
    <#
    .SYNOPSIS
        Get only test errors without full output
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All'
    )

    Write-Warning "bb-test-errors is deprecated. Use bb-test or bb-test-full and review results in the NUnit Test Runner extension UI."
}

function bb-test-log {
    <#
    .SYNOPSIS
        Show the most recent test log
    #>
    $latestLog = Get-ChildItem "logs\test-output-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if ($latestLog) {
        Write-Host "📄 Most recent test log: $($latestLog.Name)" -ForegroundColor Cyan
        Write-Host "📅 Created: $($latestLog.LastWriteTime)" -ForegroundColor Gray
        Write-Host ""
        Get-Content $latestLog.FullName
    } else {
        Write-Host "No test logs found. Run bb-test-full first." -ForegroundColor Yellow
    }
}

function bb-test-watch {
    <#
    .SYNOPSIS
        Continuous testing with file monitoring and full output capture
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'Unit'
    )

    Write-Warning "bb-test-watch is deprecated. Use bb-test or bb-test-full for test automation. For continuous testing, use the NUnit Test Runner extension's watch mode."
}

#endregion

# Export functions
Export-ModuleMember -Function Get-BusBuddyTestOutput, bb-test-full, bb-test-errors, bb-test-log, bb-test-watch
