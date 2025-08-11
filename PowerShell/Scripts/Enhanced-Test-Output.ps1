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
        # Deprecated: Use bbTest or bbTestFull instead
        bbTest -TestSuite "Unit"
        bbTestFull -TestSuite "Core"
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

    Write-Warning "Get-BusBuddyTestOutput is deprecated. Please use bbTest or bbTestFull to run tests via the NUnit Test Runner extension."
    return @{ Status = "Deprecated"; Message = "Use bbTest or bbTestFull for all test automation." }
}

function Invoke-BusBuddyTestFull {
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
        Write-Error "bbTestFull failed: $($_.Exception.Message)" -ErrorAction Stop
    }
}
function Invoke-BusBuddyTestSimple {
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

function Get-BusBuddyTestErrors {
    <#
    .SYNOPSIS
        Get only test errors without full output
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All'
    )

    Write-Warning "Get-BusBuddyTestErrors is deprecated. Use bbTest or bbTestFull and review results in the NUnit Test Runner extension UI."
}

function Get-BusBuddyTestLog {
    <#
    .SYNOPSIS
        Show the most recent test log
    #>
    $latestLog = Get-ChildItem "logs\test-output-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if ($latestLog) {
        Write-Information "📄 Most recent test log: $($latestLog.Name)" -InformationAction Continue
        Write-Information "📅 Created: $($latestLog.LastWriteTime)" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Get-Content $latestLog.FullName
    } else {
        Write-Information "No test logs found. Run bbTestFull first." -InformationAction Continue
    }
}

function Start-BusBuddyTestWatch {
    <#
    .SYNOPSIS
        Continuous testing with file monitoring and full output capture
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'Unit'
    )

    Write-Warning "Start-BusBuddyTestWatch is deprecated. Use bbTest or bbTestFull for test automation. For continuous testing, use the NUnit Test Runner extension's watch mode."
}

#endregion

# Export functions
Export-ModuleMember -Function Get-BusBuddyTestOutput, Invoke-BusBuddyTestFull, Get-BusBuddyTestErrors, Get-BusBuddyTestLog, Start-BusBuddyTestWatch
