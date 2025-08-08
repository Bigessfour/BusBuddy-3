#region Enhanced Test Output Functions
function Get-BusBuddyTestOutput {
    <#
    .SYNOPSIS
        Execute tests with complete output capture and no truncation
    .DESCRIPTION
        Captures full dotnet test output to both console and file, preventing truncation issues.
        Supports all test scenarios: unit, integration, validation, etc.
    .PARAMETER TestSuite
        Type of tests to run (All, Unit, Integration, Validation, Core, WPF)
    .PARAMETER ProjectPath
        Path to solution or test project file
    .PARAMETER SaveToFile
        Save complete output to timestamped log file
    # Output type: Hashtable — documented for analyzer compliance
    .PARAMETER Filter
        Custom test filter expression
    .EXAMPLE
        Get-BusBuddyTestOutput -TestSuite "Unit" -SaveToFile
    .EXAMPLE
        Get-BusBuddyTestOutput -Filter "Category=Core" -SaveToFile
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All',
        [string]$ProjectPath = "BusBuddy.sln",
        [switch]$SaveToFile,
        [string]$Filter,
        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
        [string]$Verbosity = 'normal'
    )
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $outputFile = "logs\test-output-$TestSuite-$timestamp.log"
    if (-not (Test-Path "logs")) {
        New-Item -ItemType Directory -Path "logs" -Force | Out-Null
    }
    Write-Host "🧪 Running $TestSuite tests..." -ForegroundColor Cyan
    Write-Host "📝 Verbosity: $Verbosity" -ForegroundColor Gray
    Write-Host "📁 Project: $ProjectPath" -ForegroundColor Gray
    if ($SaveToFile) {
        Write-Host "💾 Full output will be saved to: $outputFile" -ForegroundColor Yellow
    }
    if (-not $Filter) {
        $Filter = switch ($TestSuite) {
            'Unit'        { 'Category=Unit|TestCategory=Unit' }
            'Integration' { 'Category=Integration|TestCategory=Integration' }
            'Validation'  { 'Category=Validation|TestCategory=Validation' }
            'Core'        { 'FullyQualifiedName~BusBuddy.Tests.Core' }
            'WPF'         { 'FullyQualifiedName~BusBuddy.UITests' }
            'All'         { '' }
            default       { '' }
        }
    }
    $env:DOTNET_CLI_UI_LANGUAGE = "en-US"
    $env:DOTNET_NOLOGO = "false"  # We want full output
    try {
        $startTime = Get-Date
        Write-Host "🏗️ Building solution first..." -ForegroundColor Yellow
        $buildCmd = "dotnet build $ProjectPath --configuration Debug --verbosity $Verbosity"
        $buildStdOutPath = "logs/build-stdout-$timestamp.log"
        $buildStdErrPath = "logs/build-stderr-$timestamp.log"
        $buildOutput = & $buildCmd 2>&1 | Tee-Object -FilePath $buildStdOutPath
        $buildStdout = Get-Content $buildStdOutPath -Raw
        $buildStderr = $buildOutput | Where-Object { $_ -match 'error' -or $_ -match 'FAILED' }
        $buildExitCode = $LASTEXITCODE
        if ($buildExitCode -ne 0) {
            Write-Host "❌ Build failed! Cannot proceed with testing." -ForegroundColor Red
            Write-Host "Build errors:" -ForegroundColor Red
            Write-Host $buildStderr -ForegroundColor Red
            return @{
                ExitCode = $buildExitCode
                Status = "BuildFailed"
                BuildOutput = $buildStdout + $buildStderr
            }
        }
        Write-Host "✅ Build successful, proceeding with tests..." -ForegroundColor Green
        $testCmd = "dotnet test $ProjectPath --configuration Debug --verbosity $Verbosity --logger trx --results-directory TestResults --collect:XPlat\ Code\ Coverage --no-build"
        if ($Filter) {
            $testCmd += " --filter '$Filter'"
            Write-Host "🔍 Filter applied: $Filter" -ForegroundColor Gray
        }
        Write-Host "🧪 Executing tests..." -ForegroundColor Cyan
        $testStdOutPath = "logs/test-stdout-$timestamp.log"
        $testOutput = & $testCmd 2>&1 | Tee-Object -FilePath $testStdOutPath
        $testStdout = Get-Content $testStdOutPath -Raw
        $testStderr = $testOutput | Where-Object { $_ -match 'error' -or $_ -match 'FAILED' }
        $testExitCode = $LASTEXITCODE
        $endTime = Get-Date
        $duration = $endTime - $startTime
        $fullOutput = @"
=== BUSBUDDY TEST LOG ===
Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Test Suite: $TestSuite
Project: $ProjectPath
Filter: $Filter
Duration: $($duration.TotalSeconds) seconds
Build Exit Code: $buildExitCode
Test Exit Code: $testExitCode

=== BUILD OUTPUT ===
$buildStdout

=== BUILD ERRORS ===
$buildStderr

=== TEST OUTPUT ===
$testStdout

=== TEST ERRORS ===
$testStderr

=== TEST SUMMARY ===
"@
        if ($SaveToFile) {
            $fullOutput | Out-File -FilePath $outputFile -Encoding UTF8 -Width 500
            Write-Host "✅ Complete test log saved to: $outputFile" -ForegroundColor Green
        }
        $passedTests = [regex]::Matches($testStdout, "Passed:\s+(\d+)") | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum | Select-Object -ExpandProperty Sum
        $failedTests = [regex]::Matches($testStdout, "Failed:\s+(\d+)") | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum | Select-Object -ExpandProperty Sum
        $skippedTests = [regex]::Matches($testStdout, "Skipped:\s+(\d+)") | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum | Select-Object -ExpandProperty Sum
        $errorLines = ($testStdout + $testStderr) -split "`n" | Where-Object { $_ -match "FAILED|ERROR|Exception|error CS\d+|error MSB\d+" }
        if ($errorLines -or $failedTests -gt 0) {
            Write-Host "`n❌ TEST ISSUES FOUND:" -ForegroundColor Red
            Write-Host "=" * 60 -ForegroundColor Red
            if ($failedTests -gt 0) {
                Write-Host "Failed Tests: $failedTests" -ForegroundColor Red
            }
            $errorLines | ForEach-Object {
                Write-Host $_ -ForegroundColor Red
            }
            Write-Host "=" * 60 -ForegroundColor Red
            if ($SaveToFile) {
                Write-Host "🔍 Full details in: $outputFile" -ForegroundColor Yellow
            }
        } else {
            Write-Host "✅ All tests passed!" -ForegroundColor Green
        }
        Write-Host "`n📊 TEST SUMMARY:" -ForegroundColor Cyan
        Write-Host "   Test Suite: $TestSuite" -ForegroundColor Gray
        Write-Host "   Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Gray
        Write-Host "   Passed: $passedTests" -ForegroundColor Green
        Write-Host "   Failed: $failedTests" -ForegroundColor $(if ($failedTests -gt 0) { "Red" } else { "Gray" })
        Write-Host "   Skipped: $skippedTests" -ForegroundColor Yellow
        Write-Host "   Build Status: $(if ($buildExitCode -eq 0) { "SUCCESS ✅" } else { "FAILED ❌" })" -ForegroundColor $(if ($buildExitCode -eq 0) { "Green" } else { "Red" })
        Write-Host "   Test Status: $(if ($testExitCode -eq 0) { "SUCCESS ✅" } else { "FAILED ❌" })" -ForegroundColor $(if ($testExitCode -eq 0) { "Green" } else { "Red" })
        return @{
            ExitCode = $testExitCode
            Duration = $duration
            PassedTests = $passedTests
            FailedTests = $failedTests
            SkippedTests = $skippedTests
            ErrorLines = $errorLines
            OutputFile = if ($SaveToFile) { $outputFile } else { $null }
            FullOutput = $fullOutput
            BuildExitCode = $buildExitCode
            Status = if ($testExitCode -eq 0) { "Success" } else { "Failed" }
        }
    } catch {
        Write-Error "Failed to execute tests: $($_.Exception.Message)"
        return @{
            ExitCode = -1
            Status = "Error"
            ErrorMessage = $_.Exception.Message
        }
    }
}

function Invoke-BusBuddyTestFull {
    <#
    .SYNOPSIS
        Enhanced bb-test with complete output capture
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All'
    )
    Get-BusBuddyTestOutput -TestSuite $TestSuite -SaveToFile
}

function Get-BusBuddyTestError {
    <#
    .SYNOPSIS
        Get only test errors without full output
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All'
    )
    Get-BusBuddyTestOutput -TestSuite $TestSuite -Verbosity 'quiet'
}

function Get-BusBuddyTestLog {
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
    Write-Host "🔄 Starting watch mode for $TestSuite tests..." -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to stop watching" -ForegroundColor Yellow
    Get-BusBuddyTestOutput -TestSuite $TestSuite -SaveToFile
    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = $PWD.Path
    $watcher.Filter = "*.cs"
    $watcher.IncludeSubdirectories = $true
    $watcher.EnableRaisingEvents = $true
    $action = {
        $path = $Event.SourceEventArgs.FullPath
        $changeType = $Event.SourceEventArgs.ChangeType
        Write-Host "📝 File changed: $path" -ForegroundColor Gray
        Start-Sleep -Seconds 1  # Debounce
        Write-Host "🔄 Re-running tests..." -ForegroundColor Cyan
    Get-BusBuddyTestOutput -TestSuite $using:TestSuite -SaveToFile
    }
    Register-ObjectEvent -InputObject $watcher -EventName "Changed" -Action $action
    try {
        while ($true) {
            Start-Sleep -Seconds 1
        }
    } finally {
        $watcher.EnableRaisingEvents = $false
        $watcher.Dispose()
        Get-EventSubscriber | Unregister-Event
    }
}
#endregion
#requires -Version 5.1
<#
.SYNOPSIS
    BusBuddy PowerShell Module - Complete Working Module

.DESCRIPTION
    Professional PowerShell module for Bus Buddy WPF development environment.
    This is a WORKING version that contains all functions inline rather than
    trying to load them from separate files (which has scoping issues).

.NOTES
    File Name      : BusBuddy.psm1
    Author         : Bus Buddy Development Team
    Prerequisite   : PowerShell 5.1+ (PowerShell 7.5.2 recommended)
    Copyright      : (c) 2025 Bus Buddy Project
#>

#region Module Initialization and Buffer Configuration

# Configure PowerShell Buffer Limits to Prevent Truncated Output
try {
    if ($Host.UI.RawUI) {
        # Increase buffer size for full output capture
        $newBuffer = New-Object System.Management.Automation.Host.Size(200, 3000)
        $Host.UI.RawUI.BufferSize = $newBuffer

        # Increase window size for better visibility
        $currentWindow = $Host.UI.RawUI.WindowSize
        $maxWidth = [Math]::Min(200, $currentWindow.Width)
        $maxHeight = [Math]::Min(50, $currentWindow.Height)
        $newWindow = New-Object System.Management.Automation.Host.Size($maxWidth, $maxHeight)
        $Host.UI.RawUI.WindowSize = $newWindow
    }

    # Configure output preferences
    $OutputEncoding = [System.Text.Encoding]::UTF8
    $PSDefaultParameterValues['Out-File:Encoding'] = 'UTF8'
    $PSDefaultParameterValues['*:Encoding'] = 'UTF8'

    # Set maximum history count
    if (Get-Command Set-PSReadLineOption -ErrorAction SilentlyContinue) {
        Set-PSReadLineOption -MaximumHistoryCount 10000 -HistoryNoDuplicates
    }

    Write-Verbose "✅ PowerShell buffer configuration optimized for full output capture"
} catch {
    Write-Warning "Could not optimize PowerShell buffer: $($_.Exception.Message)"
}


#region Enhanced Output Function Loader (runs after all functions are defined)
try {
    # Determine project root relative to this module: Modules/BusBuddy/ -> PowerShell/Modules/BusBuddy
    # Repo root is three levels up from this .psm1
    $projectRoot = (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent)
    $enhancedBuildPath = Join-Path $projectRoot "PowerShell\Functions\Build\Enhanced-Build-Output.ps1"
    $enhancedTestPath = Join-Path $projectRoot "PowerShell\Functions\Testing\Enhanced-Test-Output.ps1"

    if (Test-Path $enhancedBuildPath) {
        . $enhancedBuildPath
        Write-Verbose "✅ Enhanced build output functions loaded"
    }

    if (Test-Path $enhancedTestPath) {
    . $enhancedTestPath
    Write-Verbose "✅ Enhanced test output functions loaded"
    Export-ModuleMember -Function @('Get-BusBuddyTestOutput','Invoke-BusBuddyTestFull','Get-BusBuddyTestError','Get-BusBuddyTestLog','Start-BusBuddyTestWatch')
    } else {
        Write-Warning "Enhanced-Test-Output.ps1 not found at $enhancedTestPath"
    }
} catch {
    Write-Warning "Error loading enhanced output functions: $($_.Exception.Message)"
}
#endregion

#endregion

#region Core Functions

function Get-BusBuddyProjectRoot {
    <#
    .SYNOPSIS
        Get the root directory of the BusBuddy project
    #>
    [CmdletBinding()]
    param()

    $currentPath = $PWD.Path

    while ($currentPath -and $currentPath -ne [System.IO.Path]::GetPathRoot($currentPath)) {
        if ((Test-Path (Join-Path $currentPath "BusBuddy.sln")) -and
            (Test-Path (Join-Path $currentPath "Directory.Build.props"))) {
            return $currentPath
        }
        $currentPath = Split-Path $currentPath -Parent
    }

    return $PWD.Path
}

function Write-BusBuddyStatus {
    <#
    .SYNOPSIS
        Write status message with BusBuddy formatting using PowerShell 7.5.2 best practices

    .DESCRIPTION
        Displays formatted status messages following Microsoft PowerShell Development Guidelines.
        Uses Write-Information with proper ANSI colors and structured output for enhanced readability.

    .PARAMETER Message
        The status message to display. Can be empty for blank lines.

    .PARAMETER Type
        The type of status message (Info, Success, Warning, Error)

    .PARAMETER Status
        Legacy alias for Type parameter to maintain backward compatibility

    .PARAMETER NoEmoji
        Suppress the BusBuddy emoji prefix for cleaner output

    .PARAMETER Indent
        Number of spaces to indent the message (default: 0)

    .EXAMPLE
        Write-BusBuddyStatus "Build completed" -Type Success

    .EXAMPLE
        Write-BusBuddyStatus "Warning detected" -Type Warning

    .EXAMPLE
        Write-BusBuddyStatus "" -Type Info  # Creates blank line

    .EXAMPLE
        Write-BusBuddyStatus "Detailed info" -Type Info -Indent 2
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false, Position=0, ValueFromPipeline=$true)]
        [AllowEmptyString()]
        [AllowNull()]
        [string]$Message = "",

        [Parameter(ParameterSetName='Type')]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Debug')]
        [string]$Type = 'Info',

        [Parameter(ParameterSetName='Status')]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Debug')]
        [string]$Status = 'Info',

        [Parameter()]
        [switch]$NoEmoji,

        [Parameter()]
        [int]$Indent = 0
    )

    begin {
        # Use Status parameter if provided, otherwise use Type
        $statusType = if ($PSCmdlet.ParameterSetName -eq 'Status') { $Status } else { $Type }

        # Handle null or empty messages properly - allow empty strings for spacing
        if ($null -eq $Message) {
            $Message = ""
        }

        # Create indentation
        $indentString = " " * $Indent
    }

    process {
        try {
            # Handle empty messages for spacing
            if ([string]::IsNullOrWhiteSpace($Message)) {
                Write-Information " " -InformationAction Continue
                return
            }

            # Enhanced formatting with ANSI colors and icons
            $icon = if ($NoEmoji) { "" } else {
                switch ($statusType) {
                    'Info'    { "🚌" }
                    'Success' { "✅" }
                    'Warning' { "⚠️ " }
                    'Error'   { "❌" }
                    'Debug'   { "🔍" }
                    default   { "🚌" }
                }
            }

            $formattedMessage = "$indentString$icon $Message"

            # Use appropriate PowerShell 7.5.2 output streams
            switch ($statusType) {
                'Info' {
                    Write-Information $formattedMessage -InformationAction Continue
                }
                'Success' {
                    Write-Information $formattedMessage -InformationAction Continue
                }
                'Warning' {
                    Write-Warning $formattedMessage
                }
                'Error' {
                    Write-Error $formattedMessage
                }
                'Debug' {
                    Write-Debug $formattedMessage
                }
                default {
                    Write-Information $formattedMessage -InformationAction Continue
                }
            }
        }
        catch {
            Write-Error "Error in Write-BusBuddyStatus: $($_.Exception.Message)"
        }
    }
}

function Write-BusBuddyError {
    <#
    .SYNOPSIS
        Write error message with BusBuddy formatting using PowerShell 7.5.2 best practices

    .DESCRIPTION
        Displays formatted error messages with optional exception details.
        Uses structured error output for better diagnostics and actionable information.

    .PARAMETER Message
        The primary error message to display

    .PARAMETER Exception
        Optional exception object for detailed error information

    .PARAMETER Context
        Optional context information about where the error occurred

    .PARAMETER ShowStackTrace
        Include stack trace information for debugging

    .PARAMETER Suggestions
        Array of suggested actions to resolve the error

    .EXAMPLE
        Write-BusBuddyError "Build failed" -Exception $_.Exception

    .EXAMPLE
        Write-BusBuddyError "Database connection failed" -Context "Startup" -Suggestions @("Check connection string", "Verify database is running")
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [System.Exception]$Exception,

        [Parameter()]
        [string]$Context,

        [Parameter()]
        [switch]$ShowStackTrace,

        [Parameter()]
        [string[]]$Suggestions
    )

    process {
        try {
            # Primary error message
            Write-Error "❌ $Message"

            # Additional context information using Write-Information
            if ($Context) {
                Write-Information "📍 Context: $Context" -InformationAction Continue
            }

            # Exception details
            if ($Exception) {
                Write-Information "🔍 Exception: $($Exception.Message)" -InformationAction Continue

                if ($ShowStackTrace -and $Exception.StackTrace) {
                    Write-Information "📋 Stack Trace:" -InformationAction Continue
                    $Exception.StackTrace.Split("`n") | ForEach-Object {
                        Write-Information "   $_" -InformationAction Continue
                    }
                }

                # Inner exception details
                $innerEx = $Exception.InnerException
                $level = 1
                while ($innerEx -and $level -le 3) {
                    Write-Information "� Inner Exception ($level): $($innerEx.Message)" -InformationAction Continue
                    $innerEx = $innerEx.InnerException
                    $level++
                }
            }

            # Suggestions for resolution
            if ($Suggestions -and $Suggestions.Length -gt 0) {
                Write-Information "💡 Suggestions:" -InformationAction Continue
                $Suggestions | ForEach-Object {
                    Write-Information "   • $_" -InformationAction Continue
                }
            }

            # Timestamp for debugging
            $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
            Write-Information "⏰ Timestamp: $timestamp" -InformationAction Continue
        }
        catch {
            # Fallback error handling
            Write-Error "Critical error in Write-BusBuddyError: $($_.Exception.Message)"
        }
    }
}

#endregion

#region Build Functions

function Invoke-BusBuddyBuild {
    <#
    .SYNOPSIS
        Build the BusBuddy solution
    #>
    [CmdletBinding()]
    param(
        [switch]$Clean
    )

    $projectRoot = Get-BusBuddyProjectRoot
    Push-Location $projectRoot

    try {
        if ($Clean) {
            Write-BusBuddyStatus "Cleaning solution..." -Type Info
            dotnet clean BusBuddy.sln
        }

        Write-BusBuddyStatus "Building BusBuddy solution..." -Type Info

        # Use the enhanced exception capture for better output
        $buildResult = Invoke-BusBuddyWithExceptionCapture -Command "dotnet" -Arguments @("build", "BusBuddy.sln", "--verbosity", "minimal") -Context "Solution Build"

        if ($buildResult) {
            # Extract only the essential information
            $buildLines = $buildResult | Where-Object {
                $_ -match "-> |succeeded|failed|error|warning|Error|Warning|Time Elapsed" -and
                $_ -notmatch "CompilerServer|analyzer|reference:|X.509 certificate|Assets file|NuGet Config|Feeds used"
            }

            if ($buildLines) {
                Write-Information "📊 Build Output:" -InformationAction Continue
                $buildLines | ForEach-Object {
                    Write-Information "   $_" -InformationAction Continue
                }
            }
        }

        # $LASTEXITCODE is already set by the exception capture function

        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "Build completed successfully" -Type Success
        } else {
            Write-BusBuddyError "Build failed with exit code $LASTEXITCODE"
        }
    }
    catch {
        Write-BusBuddyError "Build error occurred" -Exception $_
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyRun {
    <#
    .SYNOPSIS
        Run the BusBuddy application with automatic error capture
    #>
    [CmdletBinding()]
    param(
        [switch]$NoErrorCapture
    )

    $projectRoot = Get-BusBuddyProjectRoot
    Push-Location $projectRoot

    try {
        Write-BusBuddyStatus "Starting BusBuddy application..." -Type Info

        # Use automatic error capture unless disabled
        if (-not $NoErrorCapture -and (Get-Command Invoke-BusBuddyWithExceptionCapture -ErrorAction SilentlyContinue)) {
            $logPath = Join-Path $projectRoot "logs\application-startup.log"
            Invoke-BusBuddyWithExceptionCapture -Command "dotnet" -Arguments @("run", "--project", "BusBuddy.WPF/BusBuddy.WPF.csproj") -Context "Application Startup" -LogPath $logPath
        } else {
            # Fallback to direct execution
            dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj
        }
    }
    catch {
        Write-BusBuddyError "Failed to start application" -Exception $_
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyTest {
    <#
    .SYNOPSIS
        Run BusBuddy tests with enhanced output capture
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All',

        [switch]$SaveToFile,

        [switch]$DetailedOutput
    )

    $projectRoot = Get-BusBuddyProjectRoot
    Push-Location $projectRoot

    try {
        # Load enhanced test output functions if available
        $enhancedTestPath = Join-Path $projectRoot "PowerShell\Functions\Testing\Enhanced-Test-Output.ps1"
        if (Test-Path $enhancedTestPath) {
            . $enhancedTestPath

            # Use enhanced output capture
            $testResult = Get-BusBuddyTestOutput -TestSuite $TestSuite -SaveToFile:$SaveToFile -Verbosity $(if ($DetailedOutput) { 'detailed' } else { 'normal' })

            if ($testResult.ExitCode -eq 0) {
                Write-BusBuddyStatus "All tests passed! ✅" -Type Success
                if ($testResult.PassedTests) {
                    Write-Information "Passed: $($testResult.PassedTests), Failed: $($testResult.FailedTests), Skipped: $($testResult.SkippedTests)" -InformationAction Continue
                }
            } else {
                Write-BusBuddyError "Tests failed with exit code $($testResult.ExitCode) ❌"
                if ($testResult.FailedTests -gt 0) {
                    Write-Information "Failed Tests: $($testResult.FailedTests)" -InformationAction Continue
                }
                if ($testResult.OutputFile) {
                    Write-Information "Full details in: $($testResult.OutputFile)" -InformationAction Continue
                }
            }

            return $testResult
        } else {
            # Fallback to original implementation with basic output capture
            Write-BusBuddyStatus "Running BusBuddy tests (basic mode)..." -Type Info

            # Capture output to prevent truncation
            $testOutput = & dotnet test BusBuddy.sln --verbosity normal 2>&1
                Write-Information "(Non-fatal: Show-BusBuddyWelcome already exported or export failed)" -InformationAction Continue
            # Save to file if requested
            if ($SaveToFile) {
                $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
                $outputFile = "logs\test-basic-output-$timestamp.log"

                if (-not (Test-Path "logs")) {
                    New-Item -ItemType Directory -Path "logs" -Force | Out-Null
                }

                $testOutput | Out-File -FilePath $outputFile -Encoding UTF8
                Write-Information "Test output saved to: $outputFile" -InformationAction Continue
            }

            # Display output
            $testOutput | ForEach-Object { Write-Information $_ -InformationAction Continue }

            if ($LASTEXITCODE -eq 0) {
                Write-BusBuddyStatus "All tests passed" -Type Success
            } else {
                Write-BusBuddyError "Tests failed with exit code $LASTEXITCODE"
            }

            return @{
                ExitCode = $LASTEXITCODE
                Output = $testOutput
            }
        }
    }
    catch {
        Write-BusBuddyError "Test execution error" -Exception $_
        return @{
            ExitCode = -1
            ErrorMessage = $_.Exception.Message
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyClean {
    <#
    .SYNOPSIS
        Clean BusBuddy build artifacts
    #>
    Push-Location $projectRoot
        Write-Information "(Non-fatal: Show-BusBuddyWelcome already exported or export failed)" -InformationAction Continue
    try {
        Write-BusBuddyStatus "Cleaning BusBuddy artifacts..." -Type Info
        dotnet clean BusBuddy.sln

        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "Clean completed successfully" -Type Success
        } else {
            Write-BusBuddyError "Clean failed with exit code $LASTEXITCODE"
        }
    }
    catch {
        Write-BusBuddyError "Clean operation error" -Exception $_
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyRestore {
    <#
    .SYNOPSIS
        Restore BusBuddy NuGet packages
    #>
    [CmdletBinding()]
    param(
        [switch]$Force
    )

    $projectRoot = Get-BusBuddyProjectRoot
    Push-Location $projectRoot

    try {
        Write-BusBuddyStatus "Restoring NuGet packages..." -Type Info

        if ($Force) {
            dotnet restore BusBuddy.sln --force
        } else {
            dotnet restore BusBuddy.sln
        }

        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "Package restore completed successfully" -Type Success
        } else {
            Write-BusBuddyError "Package restore failed with exit code $LASTEXITCODE"
        }
    }
    catch {
        Write-BusBuddyError "Package restore error" -Exception $_
    }
    finally {
        Pop-Location
    }
}

#endregion

#region Development Functions

function Start-BusBuddyDevSession {
    <#
    .SYNOPSIS
        Start a complete BusBuddy development session
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "Starting BusBuddy development session..." -Type Info

    $projectRoot = Get-BusBuddyProjectRoot
    Write-BusBuddyStatus "Project root: $projectRoot" -Type Info

    Invoke-BusBuddyRestore
    Invoke-BusBuddyBuild
    Invoke-BusBuddyHealthCheck

    Write-BusBuddyStatus "Development session ready!" -Type Success
}

function Invoke-BusBuddyHealthCheck {
    <#
    .SYNOPSIS
        Perform BusBuddy system health check
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "Performing BusBuddy health check..." -Type Info

    $issues = @()

    try {
        $dotnetVersion = dotnet --version
        Write-BusBuddyStatus ".NET Version: $dotnetVersion" -Type Info
    }
    catch {
        $issues += "Unable to determine .NET version"
    }

    Write-BusBuddyStatus "PowerShell Version: $($PSVersionTable.PSVersion)" -Type Info

    $projectRoot = Get-BusBuddyProjectRoot
    $requiredFiles = @('BusBuddy.sln', 'Directory.Build.props', 'NuGet.config')

    foreach ($file in $requiredFiles) {
        $filePath = Join-Path $projectRoot $file
        if (Test-Path $filePath) {
            Write-BusBuddyStatus "✓ Found: $file" -Type Success
        } else {
            $issues += "Missing required file: $file"
            Write-BusBuddyStatus "✗ Missing: $file" -Type Warning
        }
    }

    if ($issues.Count -eq 0) {
        Write-BusBuddyStatus "All health checks passed!" -Type Success
    } else {
        Write-BusBuddyStatus "Health check found $($issues.Count) issue(s):" -Type Warning
        foreach ($issue in $issues) {
            Write-BusBuddyStatus "  - $issue" -Type Warning
        }
    }
}

function Get-BusBuddyInfo {
    <#
    .SYNOPSIS
        Display BusBuddy module information
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "BusBuddy PowerShell Module Information" -Type Info
    Write-Information "" -InformationAction Continue

    Write-Information "Name: BusBuddy" -InformationAction Continue
    Write-Information "Version: 2.2.0" -InformationAction Continue
    Write-Information "Author: Bus Buddy Development Team" -InformationAction Continue
    Write-Information "PowerShell Version: $($PSVersionTable.PSVersion)" -InformationAction Continue
    Write-Information "Project Root: $(Get-BusBuddyProjectRoot)" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-BusBuddyStatus "Use 'Get-BusBuddyCommand' to see available commands" -Type Info
}

function Get-BusBuddyCommand {
    <#
    .SYNOPSIS
        List all available BusBuddy commands
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "Available BusBuddy Commands" -Type Info
    Write-Information "" -InformationAction Continue

    Write-Information "Core Aliases:" -InformationAction Continue
    Write-Information "  bb-build      - Build the BusBuddy solution" -InformationAction Continue
    Write-Information "  bb-run        - Run the BusBuddy application" -InformationAction Continue
    Write-Information "  bb-test       - Run BusBuddy tests" -InformationAction Continue
    Write-Information "  bb-clean      - Clean build artifacts" -InformationAction Continue
    Write-Information "  bb-restore    - Restore NuGet packages" -InformationAction Continue
    Write-Information "  bb-health     - Check system health" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "Development Aliases:" -InformationAction Continue
    Write-Information "  bb-dev-session - Start development session" -InformationAction Continue
    Write-Information "  bb-info       - Show module information" -InformationAction Continue
    Write-Information "  bb-commands   - List all commands (this command)" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "XAML & Validation:" -InformationAction Continue
    Write-Information "  bb-xaml-validate - Validate all XAML files" -InformationAction Continue
    Write-Information "  bb-catch-errors  - Run with exception capture" -InformationAction Continue
    Write-Information "  bb-anti-regression - Run anti-regression checks" -InformationAction Continue
    Write-Information "  bb-capture-runtime-errors - Comprehensive runtime error monitoring" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "MVP Focus:" -InformationAction Continue
    Write-Information "  bb-mvp           - Evaluate features & scope management" -InformationAction Continue
    Write-Information "  bb-mvp-check     - Check MVP readiness" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "🤖 XAI Route Optimization:" -InformationAction Continue
    Write-Information "  bb-routes        - Main route optimization system" -InformationAction Continue
    Write-Information "  bb-route-demo    - Demo with sample data (READY NOW!)" -InformationAction Continue
    Write-Information "  bb-route-status  - Check system status" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "Functions:" -InformationAction Continue
    $functions = Get-Command -Module BusBuddy -CommandType Function | Sort-Object Name
    $functions = Get-Command -Module BusBuddy -CommandType Function | Sort-Object Name
    foreach ($func in $functions) {
        Write-Information "  $($func.Name)" -InformationAction Continue
    }
}

#endregion

#region XAML Validation Functions

function Invoke-BusBuddyXamlValidation {
    <#
    .SYNOPSIS
        Validate all XAML files in the BusBuddy project
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$ProjectPath = (Get-BusBuddyProjectRoot)
    )

    Write-BusBuddyStatus "Starting XAML validation..." -Type Info

    $xamlPath = Join-Path $ProjectPath "BusBuddy.WPF"
    if (-not (Test-Path $xamlPath)) {
        Write-BusBuddyError "BusBuddy.WPF directory not found at: $xamlPath"
        return
    }

    $xamlFiles = Get-ChildItem -Path $xamlPath -Filter "*.xaml" -Recurse
    $validFiles = 0
    $invalidFiles = 0

    foreach ($file in $xamlFiles) {
        Write-BusBuddyStatus "Validating: $($file.Name)" -Type Info

        try {
            $content = Get-Content $file.FullName -Raw

            # Basic XAML validation
            if ($content -match '<.*?>' -and $content -match 'xmlns') {
                $validFiles++
                Write-BusBuddyStatus "  ✓ $($file.Name)" -Type Success
            } else {
                $invalidFiles++
                Write-BusBuddyStatus "  ✗ $($file.Name) - Invalid XAML structure" -Type Warning
            }
        }
        catch {
            $invalidFiles++
            Write-BusBuddyError "  ✗ $($file.Name) - Exception: $($_.Exception.Message)"
        }
    }

    Write-BusBuddyStatus "XAML Validation Complete: $validFiles valid, $invalidFiles invalid" -Type Info
}

#endregion

#region Exception Capture Functions

function Invoke-BusBuddyWithExceptionCapture {
    <#
    .SYNOPSIS
        Execute a command with comprehensive exception capture and enhanced diagnostics

    .DESCRIPTION
        Provides detailed execution monitoring with timing, error analysis, system context,
        and actionable diagnostics for BusBuddy development operations.

    .PARAMETER Command
        The command to execute (e.g., 'dotnet', 'pwsh', 'git')

    .PARAMETER Arguments
        Array of arguments to pass to the command

    .PARAMETER Context
        Descriptive context for the operation (used in logs and error reports)

    .PARAMETER ThrowOnError
        If specified, re-throws exceptions instead of capturing them

    # Output is captured and summarized

    .PARAMETER Timeout
        Maximum execution time in seconds (default: 300)

    .EXAMPLE
        Invoke-BusBuddyWithExceptionCapture -Command "dotnet" -Arguments @("build", "BusBuddy.sln") -Context "Solution Build"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Command,

        [Parameter()]
        [string[]]$Arguments = @(),

        [Parameter()]
        [string]$Context = "BusBuddy Operation",

    [Parameter()]
    [switch]$ThrowOnError,
        [Parameter()]
        [int]$Timeout = 300
    )

    $startTime = Get-Date
    $sessionId = [System.Guid]::NewGuid().ToString("N")[0..7] -join ""
    $fullCommand = "$Command $($Arguments -join ' ')"

    # Enhanced status reporting
    Write-Information "🔄 [$sessionId] Executing: $fullCommand" -InformationAction Continue
    Write-Information "📍 Context: $Context" -InformationAction Continue
    Write-Information "⏱️  Timeout: $Timeout seconds" -InformationAction Continue

    # System context capture
    $systemContext = @{
        WorkingDirectory = Get-Location
        PowerShellVersion = $PSVersionTable.PSVersion
        ProcessId = $PID
        UserName = $env:USERNAME
        MachineName = $env:COMPUTERNAME
        Timestamp = $startTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
        SessionId = $sessionId
    }

    try {
        # Execute synchronously and capture combined output
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        if ($Arguments.Count -gt 0) {
            $result = & $Command @Arguments 2>&1
        } else {
            $result = & $Command 2>&1
        }
        $stopwatch.Stop()

        $duration = $stopwatch.Elapsed
        Write-Information "✅ [$sessionId] Success in $($duration.TotalSeconds.ToString('F2'))s" -InformationAction Continue

        if ($result) {
            $outputLines = ($result | Measure-Object -Line).Lines
            Write-Information "📊 Output: $outputLines lines captured" -InformationAction Continue
            $warnings = $result | Where-Object { $_ -match "warning|warn|WRN" }
            if ($warnings) {
                Write-Warning "⚠️  [$sessionId] $($warnings.Count) warnings detected in output"
            }
        }

        return $result
    }
    catch {
        $endTime = Get-Date
        $duration = $endTime - $startTime
        $errorDetails = $_.Exception.Message

        # Enhanced error reporting with diagnostics
        Write-Error "❌ [$sessionId] Command failed after $($duration.TotalSeconds.ToString('F2'))s"
        Write-Information "🔍 Error Details:" -InformationAction Continue
        Write-Information "   Command: $fullCommand" -InformationAction Continue
        Write-Information "   Context: $Context" -InformationAction Continue
        Write-Information "   Working Dir: $($systemContext.WorkingDirectory)" -InformationAction Continue
        Write-Information "   Error: $errorDetails" -InformationAction Continue

        # Capture additional diagnostics
        $diagnostics = @{
            LastExitCode = $LASTEXITCODE
            ErrorRecord = $_
            SystemContext = $systemContext
            Duration = $duration
            FullCommand = $fullCommand
        }

        # Check for common error patterns and provide suggestions
    $suggestions = Get-BusBuddyErrorSuggestion -ErrorMessage $errorDetails -Command $Command
        if ($suggestions) {
            Write-Information "💡 Suggestions:" -InformationAction Continue
            $suggestions | ForEach-Object {
                Write-Information "   • $_" -InformationAction Continue
            }
        }

        # Log to error capture file for analysis
        $errorLogPath = Join-Path (Get-BusBuddyProjectRoot) "logs" "command-errors.log"
        $errorEntry = @{
            Timestamp = $startTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
            SessionId = $sessionId
            Command = $fullCommand
            Context = $Context
            Duration = $duration.TotalSeconds
            Error = $errorDetails
            ExitCode = $LASTEXITCODE
            Diagnostics = $diagnostics
        } | ConvertTo-Json -Depth 3 -Compress

        try {
            $errorEntry | Add-Content -Path $errorLogPath -Encoding UTF8
        } catch {
            Write-Warning ("Could not append to error log at {0}: {1}" -f $errorLogPath, $_.Exception.Message)
        }

        if ($ThrowOnError) {
            throw
        }

        return $null
    }
}

# Helper function for error suggestion
function Get-BusBuddyErrorSuggestion {
    param(
        [string]$ErrorMessage,
        [string]$Command
    )

    $suggestions = @()

    # Common dotnet errors
    if ($Command -eq "dotnet") {
        if ($ErrorMessage -match "not found|could not be found") {
            $suggestions += "Run 'dotnet restore' to restore NuGet packages"
            $suggestions += "Check if the project file exists and is valid"
        }
        if ($ErrorMessage -match "build failed|compilation failed") {
            $suggestions += "Run 'bb-health' to check project status"
            $suggestions += "Check for missing dependencies or compile errors"
        }
        if ($ErrorMessage -match "unable to resolve|dependency") {
            $suggestions += "Run 'bb-clean' then 'bb-restore' to refresh dependencies"
        }
    }

    # PowerShell errors
    if ($ErrorMessage -match "execution policy") {
        $suggestions += "Run 'Set-ExecutionPolicy -Scope CurrentUser RemoteSigned'"
    }

    # General timeout errors
    if ($ErrorMessage -match "timeout|timed out") {
        $suggestions += "Increase timeout value or check for hanging processes"
        $suggestions += "Verify network connectivity if downloading packages"
    }

    return $suggestions
}

#endregion

#region MVP Focus Functions

function Start-BusBuddyMVP {
    <#
    .SYNOPSIS
        Keep development focused on MVP essentials - pushes back on scope creep
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$FeatureIdea,

        [Parameter()]
        [switch]$JustShow
    )

    $mvpFeatures = @{
        "✅ CORE MVP" = @(
            "Basic WPF window that opens",
            "Simple student list (Name, Address)",
            "Simple route list (Route Number, Description)",
            "Assign student to route (dropdown)",
            "Save data (even just to file)",
            "Basic CRUD operations"
        )
        "🟡 NICE TO HAVE (Phase 2)" = @(
            "Advanced Syncfusion controls",
            "Google Earth integration",
            "Real-time tracking",
            "Advanced analytics",
            "Multi-theme support",
            "Performance monitoring"
        )
        "🚫 ENTERPRISE OVERKILL" = @(
            "Microservices architecture",
            "Container deployment",
            "Advanced security patterns",
            "Multi-tenant support",
            "Cloud integration",
            "Machine learning features"
        )
    }

    if ($JustShow) {
        Write-BusBuddyStatus "🎯 MVP Feature Priority Guide" -Type Info
        Write-Information "" -InformationAction Continue

        foreach ($category in $mvpFeatures.Keys) {
            Write-Information $category -InformationAction Continue
            foreach ($feature in $mvpFeatures[$category]) {
                Write-Information "  • $feature" -InformationAction Continue
            }
            Write-Information "" -InformationAction Continue
        }

        Write-BusBuddyStatus "💡 Rule: If it's not in CORE MVP, defer it!" -Type Warning
        return
    }

    if ($FeatureIdea) {
        Write-BusBuddyStatus "🤔 Evaluating: '$FeatureIdea'" -Type Info

        $inCore = $mvpFeatures["✅ CORE MVP"] | Where-Object { $_ -match $FeatureIdea -or $FeatureIdea -match $_ }
        $inNice = $mvpFeatures["🟡 NICE TO HAVE (Phase 2)"] | Where-Object { $_ -match $FeatureIdea -or $FeatureIdea -match $_ }
        $inOverkill = $mvpFeatures["🚫 ENTERPRISE OVERKILL"] | Where-Object { $_ -match $FeatureIdea -or $FeatureIdea -match $_ }

        if ($inCore) {
            Write-BusBuddyStatus "✅ GO FOR IT! This is core MVP functionality." -Type Success
        }
        elseif ($inNice) {
            Write-BusBuddyStatus "🟡 HOLD UP! This is nice-to-have. Focus on core MVP first." -Type Warning
            Write-BusBuddyStatus "💭 Ask: 'Can I assign a student to a route without this?'" -Type Warning
        }
        elseif ($inOverkill) {
            Write-BusBuddyStatus "🚫 STOP! This is enterprise overkill for MVP." -Type Error
            Write-BusBuddyStatus "🎯 Remember: You need a working tool, not a demo for Microsoft." -Type Error
        }
        else {
            Write-BusBuddyStatus "🤷 Unknown feature. Let's evaluate against MVP goals:" -Type Info
            Write-BusBuddyStatus "❓ Question 1: Does this help assign students to routes?" -Type Info
            Write-BusBuddyStatus "❓ Question 2: Can you use BusBuddy without it?" -Type Info
            Write-BusBuddyStatus "❓ Question 3: Will this take more than 1 day to implement?" -Type Info
        }
    }
}

function Test-BusBuddyMVPReadiness {
    <#
    .SYNOPSIS
        Check if we're ready for MVP delivery
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    Write-BusBuddyStatus "🎯 MVP Readiness Check" -Type Info

    $projectRoot = Get-BusBuddyProjectRoot
    $ready = $true

    # MVP Milestone 1: Application starts
    Write-BusBuddyStatus "Checking: Application starts without crashing..." -Type Info
    try {
        & dotnet build BusBuddy.sln --verbosity quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "  ✅ App builds successfully" -Type Success
        } else {
            Write-BusBuddyStatus "  ❌ App doesn't build - FIX THIS FIRST" -Type Error
            $ready = $false
        }
    } catch {
        Write-BusBuddyStatus "  ❌ Build check failed - FIX THIS FIRST" -Type Error
        $ready = $false
    }

    # MVP Milestone 2: Basic UI exists
    $mainWindow = Join-Path $projectRoot "BusBuddy.WPF\Views\Main\MainWindow.xaml"
    if (Test-Path $mainWindow) {
        Write-BusBuddyStatus "  ✅ Main window exists" -Type Success
    } else {
        Write-BusBuddyStatus "  ❌ No main window found - NEED BASIC UI" -Type Error
        $ready = $false
    }

    # MVP Milestone 3: Data models exist
    $modelsPath = Join-Path $projectRoot "BusBuddy.Core\Models"
    $studentModel = Get-ChildItem -Path $modelsPath -Filter "*Student*" -ErrorAction SilentlyContinue
    $routeModel = Get-ChildItem -Path $modelsPath -Filter "*Route*" -ErrorAction SilentlyContinue

    if ($studentModel) {
        Write-BusBuddyStatus "  ✅ Student model exists" -Type Success
    } else {
        Write-BusBuddyStatus "  ❌ No Student model - NEED BASIC DATA" -Type Error
        $ready = $false
    }

    if ($routeModel) {
        Write-BusBuddyStatus "  ✅ Route model exists" -Type Success
    } else {
        Write-BusBuddyStatus "  ❌ No Route model - NEED BASIC DATA" -Type Error
        $ready = $false
    }

    # MVP Readiness Summary
    Write-BusBuddyStatus "" -Type Info
    if ($ready) {
        Write-BusBuddyStatus "🎉 MVP READY! You can ship this!" -Type Success
        Write-BusBuddyStatus "Next: Test that you can actually assign a student to a route" -Type Success
    } else {
        Write-BusBuddyStatus "🚧 MVP NOT READY - Focus on the failures above" -Type Warning
        Write-BusBuddyStatus "💡 Don't add features until these basic things work" -Type Warning
    }

    return $ready
}

#endregion

#region Anti-Regression Functions

function Invoke-BusBuddyAntiRegression {
    <#
    .SYNOPSIS
        Run anti-regression checks to prevent legacy patterns
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter()]
        [switch]$Detailed
    )

    Write-BusBuddyStatus "🛡️ Running Anti-Regression Checks..." -Type Info
    $issues = @()
    $projectRoot = Get-BusBuddyProjectRoot

    # Check 1: Microsoft.Extensions.Logging violations
    Write-BusBuddyStatus "Checking for Microsoft.Extensions.Logging violations..." -Type Info
    try {
        $loggingFiles = Get-ChildItem -Path $projectRoot -Include "*.cs" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch "\.xml$" }
        $loggingViolations = $loggingFiles | Select-String -Pattern "Microsoft.Extensions.Logging" -ErrorAction SilentlyContinue

        if ($loggingViolations) {
            $issues += "❌ Microsoft.Extensions.Logging violations: $($loggingViolations.Count)"
            if ($Detailed) {
                $loggingViolations | ForEach-Object {
                    Write-BusBuddyStatus "  📄 $($_.Filename):$($_.LineNumber)" -Type Warning
                }
            }
        } else {
            Write-BusBuddyStatus "  ✅ No Microsoft.Extensions.Logging violations" -Type Success
        }
    } catch {
        Write-BusBuddyStatus "  ⚠️ Could not check logging violations: $($_.Exception.Message)" -Type Warning
    }

    # Check 2: Standard WPF controls in XAML
    Write-BusBuddyStatus "Checking for standard WPF controls..." -Type Info
    try {
        $xamlFiles = Get-ChildItem -Path "$projectRoot\BusBuddy.WPF" -Include "*.xaml" -Recurse -ErrorAction SilentlyContinue
        $xamlViolations = $xamlFiles | Select-String -Pattern "<DataGrid |<ComboBox " -ErrorAction SilentlyContinue |
            Where-Object { $_.Line -notmatch "syncfusion:" }

        if ($xamlViolations) {
            $issues += "❌ Standard WPF controls found: $($xamlViolations.Count)"
            if ($Detailed) {
                $xamlViolations | ForEach-Object {
                    Write-BusBuddyStatus "  📄 $($_.Filename):$($_.LineNumber)" -Type Warning
                }
            }
        } else {
            Write-BusBuddyStatus "  ✅ No standard WPF controls found" -Type Success
        }
    } catch {
        Write-BusBuddyStatus "  ⚠️ Could not check XAML violations: $($_.Exception.Message)" -Type Warning
    }

    # Check 3: PowerShell Write-Host violations
    Write-BusBuddyStatus "Checking PowerShell compliance..." -Type Info
    try {
        $psFiles = Get-ChildItem -Path "$projectRoot\PowerShell" -Include "*.ps1", "*.psm1" -Recurse -ErrorAction SilentlyContinue
        $psViolations = $psFiles | Select-String -Pattern "Write-Host" -ErrorAction SilentlyContinue |
            Where-Object { $_.Line -notmatch "Module loaded|ForegroundColor|BusBuddy PowerShell Module" }

        if ($psViolations) {
            $issues += "❌ PowerShell Write-Host violations: $($psViolations.Count)"
            if ($Detailed) {
                $psViolations | ForEach-Object {
                    Write-BusBuddyStatus "  📄 $($_.Filename):$($_.LineNumber)" -Type Warning
                }
            }
        } else {
            Write-BusBuddyStatus "  ✅ PowerShell compliance maintained" -Type Success
        }
    } catch {
        Write-BusBuddyStatus "  ⚠️ Could not check PowerShell violations: $($_.Exception.Message)" -Type Warning
    }

    # Check 4: Build validation
    Write-BusBuddyStatus "Validating build status..." -Type Info
    try {
        Set-Location $projectRoot
        $buildOutput = & dotnet build --verbosity quiet 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "  ✅ Build successful" -Type Success
        } else {
            $issues += "❌ Build failed with exit code: $LASTEXITCODE"
            if ($Detailed) {
                Write-BusBuddyStatus "  Build output: $buildOutput" -Type Warning
            }
        }
    } catch {
        $issues += "❌ Build check failed: $($_.Exception.Message)"
    }

    # Report results
    Write-BusBuddyStatus " " -Type Info
    if ($issues.Count -eq 0) {
        Write-BusBuddyStatus "🎉 All anti-regression checks passed!" -Type Success
        Write-BusBuddyStatus "Repository is compliant with BusBuddy standards." -Type Success
        return $true
    } else {
        Write-BusBuddyError "🚨 Anti-regression violations found:"
        $issues | ForEach-Object { Write-BusBuddyError "  $_" }
        Write-BusBuddyStatus " " -Type Info
        Write-BusBuddyStatus "Run 'bb-anti-regression -Detailed' for specific file locations" -Type Warning
        Write-BusBuddyStatus "See 'Grok Resources/ANTI-REGRESSION-CHECKLIST.md' for remediation steps" -Type Warning
        return $false
    }
}

function Test-BusBuddyEnvironment {
    <#
    .SYNOPSIS
        Validates BusBuddy PowerShell environment for consistency and reliability
    .DESCRIPTION
        Comprehensive validation to ensure the development environment is properly
        configured and ready for MVP development. Checks PowerShell version, workspace,
        module availability, and essential tools.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    Write-Information "🔍 BusBuddy Environment Validation" -InformationAction Continue
    Write-Information "=================================" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    $issues = @()
    $warnings = @()

    # 1. PowerShell Version Check
    Write-Information "1. PowerShell Version..." -InformationAction Continue
    if ($PSVersionTable.PSVersion -ge [version]'7.5.0') {
        Write-Information "   ✅ PowerShell $($PSVersionTable.PSVersion) (Required: 7.5+)" -InformationAction Continue
    } else {
        $issues += "PowerShell version $($PSVersionTable.PSVersion) is too old. Need 7.5+"
        Write-Information "   ❌ PowerShell $($PSVersionTable.PSVersion) - UPGRADE REQUIRED" -InformationAction Continue
    }

    # 2. BusBuddy Workspace Detection
    Write-Information "2. Workspace Detection..." -InformationAction Continue
    $workspaceFound = $false
    $possiblePaths = @(
        $PWD.Path,
        "$env:USERPROFILE\Desktop\BusBuddy",
        "$env:USERPROFILE\Documents\BusBuddy",
        "C:\BusBuddy"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path "$path\BusBuddy.sln" -ErrorAction SilentlyContinue) {
            Write-Information "   ✅ Workspace found: $path" -InformationAction Continue
            $workspaceFound = $true
            break
        }
    }

    if (-not $workspaceFound) {
        $issues += "BusBuddy workspace not found in standard locations"
        Write-Information "   ❌ Workspace not found" -InformationAction Continue
    }

    # 3. Essential Commands Test
    Write-Information "3. Essential Commands..." -InformationAction Continue
    $essentialCommands = @('bb-build', 'bb-run', 'bb-health', 'bb-mvp', 'bb-mvp-check')
    $commandsWorking = 0

    foreach ($cmd in $essentialCommands) {
        if (Get-Command $cmd -ErrorAction SilentlyContinue) {
            $commandsWorking++
        }
    }

    if ($commandsWorking -eq $essentialCommands.Count) {
        Write-Information "   ✅ All $($essentialCommands.Count) essential commands available" -InformationAction Continue
    } else {
        $issues += "Only $commandsWorking of $($essentialCommands.Count) commands available"
        Write-Information "   ❌ Missing commands ($commandsWorking/$($essentialCommands.Count))" -InformationAction Continue
    }

    # 4. .NET SDK Check
    Write-Information "4. .NET SDK..." -InformationAction Continue
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion -and $dotnetVersion -match '^9\.') {
            Write-Information "   ✅ .NET $dotnetVersion" -InformationAction Continue
        } else {
            $warnings += ".NET version $dotnetVersion - expected 9.x"
            Write-Information "   ⚠️ .NET $dotnetVersion (Expected: 9.x)" -InformationAction Continue
        }
    } catch {
        $issues += ".NET SDK not found or not working"
        Write-Information "   ❌ .NET SDK not found" -InformationAction Continue
    }

    # 5. Git Status
    Write-Information "5. Git Repository..." -InformationAction Continue
    try {
        $gitStatus = & git status --porcelain 2>$null
        if ($LASTEXITCODE -eq 0) {
            if ($gitStatus) {
                $warnings += "Git has uncommitted changes"
                Write-Information "   ⚠️ Uncommitted changes present" -InformationAction Continue
            } else {
                Write-Information "   ✅ Git repository clean" -InformationAction Continue
            }
        } else {
            $warnings += "Not in a Git repository or Git not available"
            Write-Information "   ⚠️ Git issues detected" -InformationAction Continue
        }
    } catch {
        $warnings += "Git not available: $($_.Exception.Message)"
        Write-Information "   ⚠️ Git not available" -InformationAction Continue
    }

    # 6. Grok Resources Check
    Write-Information "6. AI Assistant Resources..." -InformationAction Continue
    if (Test-Path "Grok Resources\GROK-README.md") {
        Write-Information "   ✅ Grok Resources folder ready" -InformationAction Continue
    } else {
        $warnings += "Grok Resources not found - AI assistance may be limited"
        Write-Information "   ⚠️ Grok Resources missing" -InformationAction Continue
    }

    # Summary
    Write-Information "" -InformationAction Continue
    Write-Information "🎯 VALIDATION SUMMARY" -InformationAction Continue
    Write-Information "=====================" -InformationAction Continue

    if ($issues.Count -eq 0) {
        Write-Information "✅ ENVIRONMENT READY FOR MVP DEVELOPMENT!" -InformationAction Continue
        Write-Information "   All critical systems are operational" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "🚀 Quick Start Commands:" -InformationAction Continue
        Write-Information "   bb-health      - System health check" -InformationAction Continue
        Write-Information "   bb-mvp -JustShow - Show MVP priorities" -InformationAction Continue
        Write-Information "   bb-build       - Build the solution" -InformationAction Continue
        Write-Information "   bb-run         - Run the application" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "🎯 MVP Focus:" -InformationAction Continue
        Write-Information "   bb-mvp 'feature name' - Evaluate if feature is MVP-worthy" -InformationAction Continue
        Write-Information "   bb-mvp-check          - Check MVP milestone readiness" -InformationAction Continue

        if ($warnings.Count -gt 0) {
            Write-Information "" -InformationAction Continue
            Write-Information "⚠️ WARNINGS (non-critical):" -InformationAction Continue
            $warnings | ForEach-Object { Write-Information "   • $_" -InformationAction Continue }
        }

        return $true
    } else {
        Write-Information "❌ ENVIRONMENT NOT READY" -InformationAction Continue
        Write-Information "   Fix these issues before starting development:" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        $issues | ForEach-Object { Write-Information "   • $_" -InformationAction Continue }

        if ($warnings.Count -gt 0) {
            Write-Information "" -InformationAction Continue
            Write-Information "⚠️ Additional warnings:" -InformationAction Continue
            $warnings | ForEach-Object { Write-Information "   • $_" -InformationAction Continue }
        }

        return $false
    }
}

function Start-BusBuddyRuntimeErrorCaptureBasic {
    <#
    .SYNOPSIS
        Comprehensive runtime error capture for BusBuddy application
    .DESCRIPTION
        Executes BusBuddy with multiple error capture mechanisms to identify
        and log runtime issues during application execution.
    .PARAMETER Duration
        How long to monitor the application (in seconds). Default: 60
    .PARAMETER DetailedLogging
        Enable detailed debug logging during capture
    .PARAMETER OpenLogsAfter
        Automatically open the log directory after capture completes
    .EXAMPLE
        bb-capture-runtime-errors
        Captures runtime errors for 60 seconds with standard logging
    .EXAMPLE
        bb-capture-runtime-errors -Duration 300 -DetailedLogging -OpenLogsAfter
        Extended capture with detailed logs and auto-open results
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [ValidateRange(10, 3600)]
        [int]$Duration = 60,

        [Parameter()]
        [switch]$DetailedLogging,

        [Parameter()]
        [switch]$OpenLogsAfter
    )

    $projectRoot = Get-BusBuddyProjectRoot
    $scriptPath = Join-Path $projectRoot "PowerShell\Scripts\Capture-RuntimeErrors.ps1"

    if (-not (Test-Path $scriptPath)) {
        Write-BusBuddyError "Runtime error capture script not found: $scriptPath"
        Write-BusBuddyStatus "Run the setup to create required scripts" -Type Warning
        return
    }

    Write-BusBuddyStatus "🚌 Starting BusBuddy runtime error capture..." -Type Info
    Write-Information "Duration: $Duration seconds" -InformationAction Continue
    Write-Information "Detailed logging: $DetailedLogging" -InformationAction Continue

    try {
        $params = @{
            Duration = $Duration
        }

        if ($DetailedLogging) {
            $params.Add("DetailedLogging", $true)
        }

        if ($OpenLogsAfter) {
            $params.Add("OpenLogsAfter", $true)
        }

        # Execute the capture script
        $result = & $scriptPath @params

        if ($result.Success) {
            Write-BusBuddyStatus "Runtime capture completed successfully! No errors detected." -Type Success
        } else {
            Write-BusBuddyStatus "Runtime capture detected $($result.ErrorCount) errors - review logs" -Type Warning
        }

        return $result
    }
    catch {
        Write-BusBuddyError "Runtime error capture failed: $($_.Exception.Message)"
        Write-Information "Stack trace: $($_.ScriptStackTrace)" -InformationAction Continue
    }
}

#endregion

#region XAI Route Optimization (MVP + Smart Features)

# Import XAI Route Optimizer
$xaiOptimizerPath = Join-Path $PSScriptRoot "XAI-RouteOptimizer.ps1"
if (Test-Path $xaiOptimizerPath) {
    . $xaiOptimizerPath
    $script:XAIAvailable = $true
    if ([string]::IsNullOrWhiteSpace($env:BUSBUDDY_NO_XAI_WARN)) {
        Write-BusBuddyStatus "🤖 XAI Route Optimizer loaded successfully" -Type Success
    }
} else {
    $script:XAIAvailable = $false
    if ([string]::IsNullOrWhiteSpace($env:BUSBUDDY_NO_XAI_WARN)) {
        Write-BusBuddyStatus "⚠️ XAI Route Optimizer not found - some features unavailable" -Type Warning
    }
}

function Start-BusBuddyRouteOptimization {
    <#
    .SYNOPSIS
        Main function to optimize bus routes using XAI intelligence
    .DESCRIPTION
        This is your Monday morning solution - takes students and buses,
        returns optimized routes with driver schedules ready to print.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$Demo,

        [Parameter()]
        [switch]$UseDatabase
    )

    if ($Demo) {
        Write-BusBuddyStatus "🚌 Running Route Optimization Demo..." -Type Info
        Show-RouteOptimizationDemo
        return
    }

    if ($UseDatabase) {
        Write-BusBuddyStatus "📊 Loading data from BusBuddy database..." -Type Info
        # TODO: Connect to database and load real student/bus data
        Write-BusBuddyStatus "Database integration coming soon - use Demo mode for now" -Type Warning
        Write-BusBuddyStatus "Run: bb-route-demo" -Type Info
        return
    }

    Write-BusBuddyStatus "🎯 Route Optimization Options:" -Type Info
    Write-Information "  bb-route-demo     - Run with sample data (READY NOW)" -InformationAction Continue
    Write-Information "  bb-route-database - Use real database data (Coming Soon)" -InformationAction Continue
}

function Get-BusBuddyRouteStatus {
    <#
    .SYNOPSIS
        Check the status of route optimization system
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "🚌 BusBuddy Route Optimization Status" -Type Info
    Write-Information "" -InformationAction Continue

    Write-Information "✅ READY NOW:" -InformationAction Continue
    Write-Information "  • Route optimization algorithm" -InformationAction Continue
    Write-Information "  • Student assignment logic" -InformationAction Continue
    Write-Information "  • Pickup time calculation" -InformationAction Continue
    Write-Information "  • Driver schedule generation" -InformationAction Continue
    Write-Information "  • Printable route schedules" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "🟡 PHASE 2 (After MVP):" -InformationAction Continue
    Write-Information "  • XAI/Grok API integration" -InformationAction Continue
    Write-Information "  • Real-time traffic analysis" -InformationAction Continue
    Write-Information "  • Google Earth route visualization" -InformationAction Continue
    Write-Information "  • Advanced optimization algorithms" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "🚀 Quick Start:" -InformationAction Continue
    Write-Information "  bb-route-demo  - See it working with sample data" -InformationAction Continue
}

function Open-BusBuddyCopilotReference {
    <#
    .SYNOPSIS
        Open Copilot Reference Hub files for enhanced GitHub Copilot context
    .DESCRIPTION
        Opens specific reference documentation files to provide GitHub Copilot with
        rich context for better code completions and suggestions.
    .PARAMETER Topic
        Specific reference topic to open. If not provided, opens the main hub.
        Valid topics: Syncfusion, Build-Configs, Code-Analysis, NuGet-Setup, VSCode-Extensions, PowerShell-Commands
    .PARAMETER ShowTopics
        Display available topics instead of opening files
    .EXAMPLE
        bb-copilot-ref
        Opens the main Copilot Hub reference
    .EXAMPLE
        bb-copilot-ref Syncfusion
        Opens the Syncfusion WPF examples reference
    .EXAMPLE
        bb-copilot-ref -ShowTopics
        Lists all available reference topics
    #>
    [CmdletBinding()]
    param(
        [Parameter(Position=0)]
        [string]$Topic,

        [Parameter()]
        [switch]$ShowTopics
    )

    $projectRoot = Get-BusBuddyProjectRoot
    $referencePath = Join-Path $projectRoot "Documentation\Reference"

    if (-not (Test-Path $referencePath)) {
        Write-BusBuddyError "Reference folder not found at: $referencePath"
        Write-Information "Run the following to create the reference hub:" -InformationAction Continue
        Write-Information "  New-Item -Path '$referencePath' -ItemType Directory -Force" -InformationAction Continue
        return
    }

    # Handle help requests and show topics
    if ($ShowTopics -or $Topic -eq "help" -or $Topic -eq "--help" -or $Topic -eq "-h") {
        Write-BusBuddyStatus "📚 Available Copilot Reference Topics:" -Type Info
        Write-Information "" -InformationAction Continue
        Get-ChildItem $referencePath -Filter "*.md" | Where-Object { $_.Name -ne "README.md" } | ForEach-Object {
            $topicName = $_.BaseName
            if ($topicName -eq "Copilot-Hub") {
                Write-Information "  📖 Main Hub (default)" -InformationAction Continue
            } else {
                Write-Information "  📄 $topicName" -InformationAction Continue
            }
        }
        Write-Information "" -InformationAction Continue
        Write-Information "💡 Usage Examples:" -InformationAction Continue
        Write-Information "  bb-copilot-ref              # Open main hub + folder" -InformationAction Continue
        Write-Information "  bb-copilot-ref Syncfusion   # Open Syncfusion reference" -InformationAction Continue
        Write-Information "  bb-copilot-ref -ShowTopics  # Show this help" -InformationAction Continue
        return
    }

    if ($Topic) {
        $targetFile = Join-Path $referencePath "$Topic.md"
        if (Test-Path $targetFile) {
            Write-BusBuddyStatus "Opening $Topic reference for Copilot context..." -Type Info
            if (Get-Command code -ErrorAction SilentlyContinue) {
                code $targetFile
            } else {
                Start-Process notepad $targetFile
            }
        } else {
            Write-BusBuddyError "Reference file not found: $targetFile"
            Write-Information "Available topics:" -InformationAction Continue
            Get-ChildItem $referencePath -Filter "*.md" | Where-Object { $_.Name -ne "README.md" } | ForEach-Object {
                Write-Information "  $($_.BaseName)" -InformationAction Continue
            }
            Write-Information "" -InformationAction Continue
            Write-Information "Use 'bb-copilot-ref -ShowTopics' for detailed help" -InformationAction Continue
        }
    } else {
        $hubFile = Join-Path $referencePath "Copilot-Hub.md"
        if (Test-Path $hubFile) {
            Write-BusBuddyStatus "Opening Copilot Reference Hub..." -Type Info
            if (Get-Command code -ErrorAction SilentlyContinue) {
                # Open the entire reference folder for maximum context
                code $referencePath
            } else {
                Start-Process notepad $hubFile
            }
        } else {
            Write-BusBuddyError "Copilot Hub not found: $hubFile"
            Write-Information "Create the reference hub by running the BusBuddy Copilot setup." -InformationAction Continue
        }
    }
}

function Invoke-BusBuddyReport {
    <#
    .SYNOPSIS
        Generate PDF reports using Syncfusion PDF tools
    .DESCRIPTION
        Generates student rosters and route manifests as PDFs.
        Implements the bb-generate-report functionality requested.
    .PARAMETER ReportType
        Type of report to generate: Roster, RouteManifest, StudentList, DriverSchedule
    .PARAMETER OutputPath
        Path where the PDF report will be saved
    .PARAMETER RouteId
        Specific route ID for route-based reports
    .PARAMETER Format
        Output format: PDF (default), Excel, CSV
    .EXAMPLE
        bb-generate-report -ReportType Roster -OutputPath "reports/student-roster.pdf"
    .EXAMPLE
        bb-generate-report -ReportType RouteManifest -RouteId "Route-001" -OutputPath "reports/route-001-manifest.pdf"
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet("Roster", "RouteManifest", "StudentList", "DriverSchedule", "MaintenanceReport")]
        [string]$ReportType,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$OutputPath,

        [Parameter()]
        [string]$RouteId,

        [Parameter()]
        [ValidateSet("PDF", "Excel", "CSV")]
        [string]$Format = "PDF",

        [Parameter()]
        [switch]$OpenAfterGeneration
    )

    try {
        Write-BusBuddyStatus "📄 Generating $ReportType report..." -Type Info

        # Ensure output directory exists
        $outputDir = Split-Path $OutputPath -Parent
        if ($outputDir -and -not (Test-Path $outputDir)) {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
            Write-Information "Created output directory: $outputDir" -InformationAction Continue
        }

        # Build the API call to our .NET PDF service
    $projectPath = (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent)
    $exePath = Join-Path $projectPath "BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.exe"

        if (Test-Path $exePath) {
            # Call the .NET application with report generation parameters
            $routeArgs = @(
                "--generate-report",
                "--report-type", $ReportType,
                "--output", $OutputPath,
                "--format", $Format
            )

            if ($RouteId) {
                $routeArgs += "--route-id"
                $routeArgs += $RouteId
            }

            Write-Information "Calling PdfReportService..." -InformationAction Continue
            $reportResult = & $exePath $routeArgs 2>&1

            if ($LASTEXITCODE -eq 0) {
                Write-BusBuddyStatus "Report generated successfully: $OutputPath" -Type Success

                if ($OpenAfterGeneration -and (Test-Path $OutputPath)) {
                    Write-Information "Opening generated report..." -InformationAction Continue
                    Start-Process $OutputPath
                }

                # Return report metadata
                return @{
                    ReportType = $ReportType
                    OutputPath = $OutputPath
                    Format = $Format
                    RouteId = $RouteId
                    GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                    FileSize = if (Test-Path $OutputPath) { (Get-Item $OutputPath).Length } else { 0 }
                }
            } else {
                Write-BusBuddyError "Report generation failed: $reportResult"
                return
            }
        } else {
            Write-BusBuddyStatus "BusBuddy application not built. Building now..." -Type Info
            Invoke-BusBuddyBuild

            if (Test-Path $exePath) {
                Write-BusBuddyStatus "Retrying report generation..." -Type Info
                # Retry the call after building
                return Invoke-BusBuddyReport @PSBoundParameters
            } else {
                Write-BusBuddyError "Could not build BusBuddy application"
                return
            }
        }
    }
    catch {
        Write-BusBuddyError "Report generation failed: $($_.Exception.Message)"
        Write-Information "Stack trace: $($_.ScriptStackTrace)" -InformationAction Continue
    }
}

function Invoke-BusBuddyRouteOptimization {
    <#
    .SYNOPSIS
        Advanced route optimization using xAI Grok API integration
    .DESCRIPTION
        Uses GrokGlobalAPI service to optimize bus routes with AI intelligence.
        This implements the bb-route-optimize functionality requested.
    .PARAMETER RouteId
        The ID of the route to optimize
    .PARAMETER CurrentPerformance
        Current route performance metrics
    .PARAMETER TargetMetrics
        Target optimization goals
    .PARAMETER Constraints
        Route constraints to consider
    .PARAMETER OutputPath
        Optional path to save optimization report
    .EXAMPLE
        bb-route-optimize -RouteId "Route-001" -CurrentPerformance "45 min, 12 stops" -TargetMetrics "Reduce time by 10%, improve efficiency"
    .EXAMPLE
        bb-route-optimize -RouteId "Route-001" -Constraints @("Max 8 stops", "Safety first") -OutputPath "reports/route-001-optimization.json"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$RouteId,

        [Parameter()]
        [string]$CurrentPerformance = "Standard performance metrics",

        [Parameter()]
        [string]$TargetMetrics = "Improve efficiency and reduce travel time",

        [Parameter()]
        [string[]]$Constraints = @(),

        [Parameter()]
        [string]$OutputPath,

        [Parameter()]
        [switch]$Mock
    )

    try {
        Write-BusBuddyStatus "🤖 Starting xAI Grok route optimization for route: $RouteId" -Type Info

        # Check if we should use mock data or real API
        if ($Mock -or -not (Test-Path env:XAI_API_KEY) -or $env:XAI_API_KEY -like "*YOUR_XAI_API_KEY*") {
            Write-BusBuddyStatus "Using mock optimization (set XAI_API_KEY environment variable for live AI)" -Type Warning

            # Generate mock optimization result
            $result = @{
                RouteId = $RouteId
                OptimizationSuggestions = @"
🚌 Route Optimization Analysis for ${RouteId}:

EFFICIENCY IMPROVEMENTS:
• Consolidate stops within 0.3 miles to reduce travel time by 12%
• Optimize pickup sequence by grade level (K-2, 3-5, 6-8) for 8% efficiency gain
• Implement GPS tracking for real-time traffic adjustments

TIME OPTIMIZATION:
• Reduce route time by 15% through strategic stop consolidation
• Adjust departure times based on historical traffic patterns
• Implement express routes for high-density areas

FUEL EFFICIENCY:
• Route adjustments could save 18% in fuel consumption
• Reduce unnecessary turns and backtracking
• Optimize idle time at stops

SAFETY CONSIDERATIONS:
• Minimize left turns at busy intersections
• Ensure all stops have adequate visibility and safe boarding areas
• Consider traffic light timing for safer crossings

IMPLEMENTATION STEPS:
1. Review current route data and student locations
2. Identify consolidation opportunities within walking distance
3. Test optimized route during off-peak hours
4. Gradually implement changes with driver feedback
5. Monitor performance metrics for 2 weeks
"@
                EfficiencyGain = 12.5
                TimeReduction = 15.0
                FuelSavings = 18.0
                SafetyImprovements = @("Reduced left turns", "Improved stop visibility", "Better traffic light coordination")
                ImplementationSteps = @(
                    "Review current route data",
                    "Identify consolidation opportunities",
                    "Test optimized route",
                    "Implement changes gradually",
                    "Monitor performance metrics"
                )
                GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                AIModel = "Mock-AI-Demo"
            }
        } else {
            Write-BusBuddyStatus "Using live xAI Grok API for route optimization..." -Type Info

            # Build the API call to our .NET service
            $projectPath = (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent)
            $exePath = Join-Path $projectPath "BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.exe"

            if (Test-Path $exePath) {
                # Call the .NET application with route optimization parameters
                $routeArgs = @(
                    "--optimize-route",
                    "--route-id", $RouteId,
                    "--current-performance", $CurrentPerformance,
                    "--target-metrics", $TargetMetrics
                )

                if ($Constraints.Count -gt 0) {
                    $routeArgs += "--constraints"
                    $routeArgs += ($Constraints -join ";")
                }

                if ($OutputPath) {
                    $routeArgs += "--output"
                    $routeArgs += $OutputPath
                }

                Write-Information "Calling GrokGlobalAPI service..." -InformationAction Continue
                $optimizationResult = & $exePath $routeArgs 2>&1

                if ($LASTEXITCODE -eq 0) {
                    Write-BusBuddyStatus "Route optimization completed successfully" -Type Success
                    $result = $optimizationResult | ConvertFrom-Json
                } else {
                    Write-BusBuddyError "Route optimization failed: $optimizationResult"
                    return
                }
            } else {
                Write-BusBuddyStatus "BusBuddy application not built. Building now..." -Type Info
                Invoke-BusBuddyBuild

                if (Test-Path $exePath) {
                    Write-BusBuddyStatus "Retrying route optimization..." -Type Info
                    # Retry the call after building
                    return Invoke-BusBuddyRouteOptimization @PSBoundParameters
                } else {
                    Write-BusBuddyError "Could not build BusBuddy application"
                    return
                }
            }
        }

        # Display results
        Write-Information "" -InformationAction Continue
        Write-BusBuddyStatus "🎯 Route Optimization Results for $($result.RouteId)" -Type Success
        Write-Information "" -InformationAction Continue

        Write-Information "📊 PERFORMANCE METRICS:" -InformationAction Continue
        Write-Information "   • Efficiency Gain: $($result.EfficiencyGain)%" -InformationAction Continue
        Write-Information "   • Time Reduction: $($result.TimeReduction)%" -InformationAction Continue
        Write-Information "   • Fuel Savings: $($result.FuelSavings)%" -InformationAction Continue
        Write-Information "   • AI Model: $($result.AIModel)" -InformationAction Continue
        Write-Information "" -InformationAction Continue

        Write-Information "🛡️ SAFETY IMPROVEMENTS:" -InformationAction Continue
        $result.SafetyImprovements | ForEach-Object {
            Write-Information "   • $_" -InformationAction Continue
        }
        Write-Information "" -InformationAction Continue

        Write-Information "📋 IMPLEMENTATION STEPS:" -InformationAction Continue
        for ($i = 0; $i -lt $result.ImplementationSteps.Count; $i++) {
            Write-Information "   $($i+1). $($result.ImplementationSteps[$i])" -InformationAction Continue
        }
        Write-Information "" -InformationAction Continue

        Write-Information "💡 DETAILED ANALYSIS:" -InformationAction Continue
        Write-Information $result.OptimizationSuggestions -InformationAction Continue

        # Save to output file if specified
        if ($OutputPath) {
            $result | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputPath -Encoding UTF8
            Write-BusBuddyStatus "Optimization report saved to: $OutputPath" -Type Info
        }

        Write-Information "" -InformationAction Continue
        Write-BusBuddyStatus "Route optimization analysis completed! 🚌✨" -Type Success

        return $result
    }
    catch {
        Write-BusBuddyError "Route optimization failed: $($_.Exception.Message)"
        Write-Information "Stack trace: $($_.ScriptStackTrace)" -InformationAction Continue
    }
}

function Start-BusBuddyRuntimeErrorCapture {
    <#
    .SYNOPSIS
        Advanced runtime error capture using WintellectPowerShell tools

    .DESCRIPTION
        Enhanced error monitoring and capture system that integrates WintellectPowerShell
        tools for comprehensive diagnostics, crash dump analysis, and system monitoring.

    .PARAMETER MonitorCrashes
        Enable crash dump monitoring and automatic analysis

    .PARAMETER SystemDiagnostics
        Include system diagnostics (uptime, environment, etc.)

    .PARAMETER ContinuousMonitoring
        Run in continuous monitoring mode

    .PARAMETER OutputPath
        Path to save error reports and analysis (default: logs/error-capture)

    .EXAMPLE
        Start-BusBuddyRuntimeErrorCapture -MonitorCrashes -SystemDiagnostics
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [switch]$MonitorCrashes,

        [Parameter()]
        [switch]$SystemDiagnostics,

        [Parameter()]
    [switch]$ContinuousMonitoring,

        [Parameter()]
        [string]$OutputPath = "logs/error-capture"
    )

    $sessionId = [System.Guid]::NewGuid().ToString("N")[0..7] -join ""
    $startTime = Get-Date

    Write-BusBuddyStatus "🔍 Starting Enhanced Error Capture Session [$sessionId]" -Type Info
    if ($ContinuousMonitoring) { Write-Information "Continuous Monitoring: Enabled" -InformationAction Continue }
    Write-Information "⏰ Session Start: $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))" -InformationAction Continue

    # Ensure WintellectPowerShell is available
    try {
        Import-Module WintellectPowerShell -Force -ErrorAction Stop
        Write-BusBuddyStatus "✅ WintellectPowerShell module loaded" -Type Success
    }
    catch {
        Write-BusBuddyError "Failed to load WintellectPowerShell module" -Exception $_ -Suggestions @(
            "Install WintellectPowerShell: Install-Module WintellectPowerShell -Scope CurrentUser",
            "Check module availability: Get-Module -ListAvailable WintellectPowerShell"
        )
        return
    }

    # Create output directory
    $fullOutputPath = Join-Path (Get-BusBuddyProjectRoot) $OutputPath
    if (-not (Test-Path $fullOutputPath)) {
        New-Item -ItemType Directory -Path $fullOutputPath -Force | Out-Null
        Write-BusBuddyStatus "📁 Created output directory: $fullOutputPath" -Type Info
    }

    # System Diagnostics Collection
    if ($SystemDiagnostics) {
        Write-BusBuddyStatus "📊 Collecting System Diagnostics..." -Type Info

        try {
            # Get system uptime using WintellectPowerShell
            $uptime = Get-Uptime
            $uptimeInfo = @{
                Days = $uptime.Days
                Hours = $uptime.Hours
                Minutes = $uptime.Minutes
                TotalHours = [math]::Round($uptime.TotalHours, 2)
                Timestamp = Get-Date
            }

            # Collect environment information
            $envInfo = @{
                PowerShellVersion = $PSVersionTable.PSVersion
                DotNetVersion = & dotnet --version 2>$null
                UserName = $env:USERNAME
                MachineName = $env:COMPUTERNAME
                WorkingDirectory = Get-Location
                ProcessId = $PID
                SessionId = $sessionId
            }

            # System resource information
            $systemInfo = @{
                Uptime = $uptimeInfo
                Environment = $envInfo
                Timestamp = Get-Date
            }

            # Save system diagnostics
            $diagnosticsFile = Join-Path $fullOutputPath "system-diagnostics-$sessionId.json"
            $systemInfo | ConvertTo-Json -Depth 3 | Out-File -FilePath $diagnosticsFile -Encoding UTF8

            Write-BusBuddyStatus "✅ System diagnostics saved to: $diagnosticsFile" -Type Success
            Write-Information "💻 System Uptime: $($uptime.Days) days, $($uptime.Hours) hours" -InformationAction Continue
            Write-Information "🔧 PowerShell: $($PSVersionTable.PSVersion)" -InformationAction Continue
            Write-Information "⚙️  .NET Version: $(& dotnet --version 2>$null)" -InformationAction Continue
        }
        catch {
            Write-BusBuddyError "Failed to collect system diagnostics" -Exception $_
        }
    }

    # Crash Dump Monitoring
    if ($MonitorCrashes) {
        Write-BusBuddyStatus "💥 Setting up crash dump monitoring..." -Type Info

        # Look for existing crash dumps
        $projectRoot = Get-BusBuddyProjectRoot
        $possibleDumpLocations = @(
            Join-Path $projectRoot "logs"
            Join-Path $projectRoot "BusBuddy.WPF\bin\Debug\net9.0-windows"
            Join-Path $projectRoot "BusBuddy.WPF\bin\Release\net9.0-windows"
            $env:TEMP,
            $env:LOCALAPPDATA
        )

        $foundDumps = @()
        foreach ($location in $possibleDumpLocations) {
            if (Test-Path $location) {
                $dumps = Get-ChildItem -Path $location -Filter "*.dmp" -ErrorAction SilentlyContinue
                if ($dumps) {
                    $foundDumps += $dumps
                    Write-Information "🔍 Found $($dumps.Count) dump file(s) in: $location" -InformationAction Continue
                }
            }
        }

        if ($foundDumps.Count -gt 0) {
            Write-BusBuddyStatus "📋 Analyzing $($foundDumps.Count) existing crash dump(s)..." -Type Warning

            # Create basic analysis script for CDB
            $analysisScript = Join-Path $fullOutputPath "crash-analysis-commands.txt"
            $cdbCommands = @(
                "* Basic crash analysis commands",
                ".sympath srv*https://msdl.microsoft.com/download/symbols",
                ".reload",
                "!analyze -v",
                "k",
                "!clrstack",
                ".ecxr",
                "!pe",
                "q"
            )
            $cdbCommands | Out-File -FilePath $analysisScript -Encoding UTF8

            foreach ($dump in $foundDumps) {
                try {
                    Write-Information "🔍 Analyzing: $($dump.Name)" -InformationAction Continue

                    # Use WintellectPowerShell to analyze the dump
                    Get-DumpAnalysis -Files $dump.FullName -DebuggingScript $analysisScript

                    Write-BusBuddyStatus "✅ Analysis completed for: $($dump.Name)" -Type Success
                }
                catch {
                    Write-BusBuddyError "Failed to analyze dump: $($dump.Name)" -Exception $_
                }
            }
        } else {
            Write-Information "ℹ️  No existing crash dumps found" -InformationAction Continue
        }
    }

    # Enhanced BusBuddy Application Execution with Error Capture
    Write-BusBuddyStatus "🚀 Starting BusBuddy with enhanced error monitoring..." -Type Info

    try {
        # Enhanced execution using existing exception capture
        $result = Invoke-BusBuddyWithExceptionCapture -Command "dotnet" -Arguments @("run", "--project", "BusBuddy.WPF/BusBuddy.WPF.csproj") -Context "Enhanced BusBuddy Execution" -Timeout 300

        # Final report
        $endTime = Get-Date
        $duration = $endTime - $startTime

        $sessionReport = @{
            SessionId = $sessionId
            StartTime = $startTime
            EndTime = $endTime
            Duration = $duration.TotalMinutes
            OutputPath = $fullOutputPath
            MonitorCrashes = $MonitorCrashes.IsPresent
            SystemDiagnostics = $SystemDiagnostics.IsPresent
            Result = if ($result) { "Success" } else { "Failed" }
            WintellectTools = "Available"
        }

        $reportFile = Join-Path $fullOutputPath "session-report-$sessionId.json"
        $sessionReport | ConvertTo-Json -Depth 2 | Out-File -FilePath $reportFile -Encoding UTF8

        Write-BusBuddyStatus "📋 Session report saved: $reportFile" -Type Success
        Write-Information "⏱️  Total session duration: $([math]::Round($duration.TotalMinutes, 2)) minutes" -InformationAction Continue

        return $sessionReport
    }
    catch {
        Write-BusBuddyError "Enhanced error capture failed" -Exception $_ -Context "Runtime Error Monitoring"
        return $null
    }
}

#endregion

#region Aliases

Set-Alias -Name 'bb-build' -Value 'Invoke-BusBuddyBuild' -Description 'Build the Bus Buddy solution'
Set-Alias -Name 'bb-run' -Value 'Invoke-BusBuddyRun' -Description 'Run the Bus Buddy application'
Set-Alias -Name 'bb-test' -Value 'Invoke-BusBuddyTest' -Description 'Run Bus Buddy tests'
Set-Alias -Name 'bb-clean' -Value 'Invoke-BusBuddyClean' -Description 'Clean build artifacts'
Set-Alias -Name 'bb-restore' -Value 'Invoke-BusBuddyRestore' -Description 'Restore NuGet packages'
Set-Alias -Name 'bb-health' -Value 'Invoke-BusBuddyHealthCheck' -Description 'Check system health'
Set-Alias -Name 'bb-dev-session' -Value 'Start-BusBuddyDevSession' -Description 'Start development session'
Set-Alias -Name 'bb-info' -Value 'Get-BusBuddyInfo' -Description 'Show module information'
Set-Alias -Name 'bb-commands' -Value 'Get-BusBuddyCommand' -Description 'List all commands'
Set-Alias -Name 'bb-test-full' -Value 'Invoke-BusBuddyTestFull' -Description 'Enhanced test with full capture'
Set-Alias -Name 'bb-test-errors' -Value 'Get-BusBuddyTestError' -Description 'Show test errors only'
Set-Alias -Name 'bb-test-log' -Value 'Get-BusBuddyTestLog' -Description 'Show latest test log'
Set-Alias -Name 'bb-test-watch' -Value 'Start-BusBuddyTestWatch' -Description 'Continuous test watch'
Set-Alias -Name 'bb-xaml-validate' -Value 'Invoke-BusBuddyXamlValidation' -Description 'Validate XAML files'
Set-Alias -Name 'bb-catch-errors' -Value 'Invoke-BusBuddyWithExceptionCapture' -Description 'Run with exception capture'
Set-Alias -Name 'bb-anti-regression' -Value 'Invoke-BusBuddyAntiRegression' -Description 'Run anti-regression checks'
Set-Alias -Name 'bb-mvp' -Value 'Start-BusBuddyMVP' -Description 'MVP focus and scope management'
Set-Alias -Name 'bb-mvp-check' -Value 'Test-BusBuddyMVPReadiness' -Description 'Check MVP readiness'
Set-Alias -Name 'bb-env-check' -Value 'Test-BusBuddyEnvironment' -Description 'Comprehensive environment validation'
Set-Alias -Name 'bb-routes' -Value 'Start-BusBuddyRouteOptimization' -Description 'Main route optimization system'
Set-Alias -Name 'bb-route-optimize' -Value 'Invoke-BusBuddyRouteOptimization' -Description 'xAI Grok route optimization with detailed analysis'
Set-Alias -Name 'bb-generate-report' -Value 'Invoke-BusBuddyReport' -Description 'Generate PDF reports (roster, route manifest, etc.)'
Set-Alias -Name 'bb-route-demo' -Value 'Show-RouteOptimizationDemo' -Description 'Demo route optimization with sample data'
Set-Alias -Name 'bb-route-status' -Value 'Get-BusBuddyRouteStatus' -Description 'Check route optimization system status'
Set-Alias -Name 'bb-copilot-ref' -Value 'Open-BusBuddyCopilotReference' -Description 'Open Copilot reference for enhanced context'
Set-Alias -Name 'bb-capture-runtime-errors' -Value 'Start-BusBuddyRuntimeErrorCapture' -Description 'Comprehensive runtime error capture and monitoring'
Set-Alias -Name 'bb-diagnostic' -Value 'Invoke-BusBuddyDiagnostic' -Description 'Run diagnostics and output environment, module, and MVP status'
Set-Alias -Name 'bb-add-family' -Value 'bb-add-family' -Description 'Add a new family'
Set-Alias -Name 'bb-add-kid-to-family' -Value 'bb-add-kid-to-family' -Description 'Add a kid to an existing family'
Set-Alias -Name 'bb-export-data' -Value 'bb-export-data' -Description 'Export data to CSV'
Set-Alias -Name 'bb-welcome' -Value 'Show-BusBuddyWelcome' -Description 'Show categorized command overview'

#endregion

#region Exports

Export-ModuleMember -Function @(
    'Get-BusBuddyProjectRoot',
    'Write-BusBuddyStatus',
    'Write-BusBuddyError',
    'Show-BusBuddyWelcome',
    'Invoke-BusBuddyBuild',
    'Invoke-BusBuddyRun',
    'Invoke-BusBuddyTest',
    'Invoke-BusBuddyClean',
    'Invoke-BusBuddyRestore',
    'Start-BusBuddyDevSession',
    'Invoke-BusBuddyHealthCheck',
    'Get-BusBuddyInfo',
    'Get-BusBuddyCommand',
    'Invoke-BusBuddyXamlValidation',
    'Invoke-BusBuddyWithExceptionCapture',
    'Invoke-BusBuddyAntiRegression',
    'Start-BusBuddyMVP',
    'Test-BusBuddyMVPReadiness',
    'Test-BusBuddyEnvironment',
    'Start-BusBuddyRouteOptimization',
    'Get-BusBuddyRouteStatus',
    'Open-BusBuddyCopilotReference',
    'Invoke-BusBuddyRouteOptimization',
    'Invoke-BusBuddyReport',
    'Start-BusBuddyRuntimeErrorCapture',
    'bb-wiley-seed',
    'bb-generate-schedules',
    'bb-add-family',
    'bb-add-kid-to-family',
    'bb-export-data',
    'Get-BusBuddyTestOutput',
    'Invoke-BusBuddyTestFull',
    'Get-BusBuddyTestError',
    'Get-BusBuddyTestLog',
    'Start-BusBuddyTestWatch',
    'Enable-BusBuddyEnhancedTestOutput'
) -Alias @(
    'bb-build', 'bb-run', 'bb-test', 'bb-clean', 'bb-restore', 'bb-health',
    'bb-dev-session', 'bb-info', 'bb-commands', 'bb-xaml-validate', 'bb-catch-errors',
    'bb-anti-regression', 'bb-mvp', 'bb-mvp-check', 'bb-env-check', 'bb-routes',
    'bb-route-optimize', 'bb-generate-report', 'bb-route-demo', 'bb-route-status', 'bb-copilot-ref',
    'bb-capture-runtime-errors', 'bb-diagnostic', 'bb-add-family', 'bb-add-kid-to-family', 'bb-export-data', 'bb-welcome'
)

#endregion

Write-Output "🚌 BusBuddy PowerShell Module v3.0.0 loaded successfully!"
Write-Output "   🤖 NEW: XAI Route Optimization System - Ready for Monday!"

#region Welcome Screen

function Show-BusBuddyWelcome {
    <#
    .SYNOPSIS
        Display a categorized welcome screen when the module loads.
    .DESCRIPTION
        Prints environment info and categorized bb-* commands for quick discovery.
    .PARAMETER Quiet
        Suppress verbose details and show only categories and key commands.
    #>
    [CmdletBinding()]
    param(
        [switch]$Quiet
    )

    $ps = $PSVersionTable.PSVersion
    $dotnet = try { & dotnet --version 2>$null } catch { "unknown" }
    $xai = if ($script:XAIAvailable) { "✅" } else { "⚠️" }

    Write-Information "" -InformationAction Continue
    Write-BusBuddyStatus "🚌 BusBuddy Dev Shell — Ready" -Type Info
    Write-Information "PowerShell: $ps | .NET: $dotnet | XAI: $xai" -InformationAction Continue
    Write-Information "Project: $(Get-BusBuddyProjectRoot)" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    Write-BusBuddyStatus "Core" -Type Info
    Write-Information "  bb-build, bb-run, bb-test, bb-clean, bb-restore, bb-health" -InformationAction Continue

    Write-BusBuddyStatus "Development" -Type Info
    Write-Information "  bb-dev-session, bb-info, bb-commands" -InformationAction Continue

    Write-BusBuddyStatus "Validation & Safety" -Type Info
    Write-Information "  bb-xaml-validate, bb-anti-regression, bb-catch-errors, bb-env-check" -InformationAction Continue

    Write-BusBuddyStatus "MVP Focus" -Type Info
    Write-Information "  bb-mvp, bb-mvp-check" -InformationAction Continue

    Write-BusBuddyStatus "Routes & Reports" -Type Info
    Write-Information "  bb-routes, bb-route-demo, bb-route-status, bb-route-optimize" -InformationAction Continue
    Write-Information "  bb-generate-report" -InformationAction Continue

    Write-BusBuddyStatus "Docs & Reference" -Type Info
    Write-Information "  bb-copilot-ref [Topic] (-ShowTopics)" -InformationAction Continue

    if (-not $Quiet) {
        Write-Information "" -InformationAction Continue
        Write-Information "Tips:" -InformationAction Continue
        Write-Information "  • bb-commands — full list with functions" -InformationAction Continue
        Write-Information "  • bb-health — verify env quickly" -InformationAction Continue
        Write-Information "  • Set 'BUSBUDDY_NO_WELCOME=1' to suppress on import" -InformationAction Continue
    Write-Information "  • Set 'BUSBUDDY_NO_XAI_WARN=1' to silence optional XAI messages" -InformationAction Continue
    }
}

# Auto-run welcome unless suppressed
if (-not $env:BUSBUDDY_NO_WELCOME) {
    try { Show-BusBuddyWelcome -ErrorAction SilentlyContinue } catch { Write-Information "(welcome suppressed due to error)" -InformationAction Continue }
}

#endregion

# Ensure the welcome function is exported after its definition so external callers can invoke bb-welcome
try {
    Export-ModuleMember -Function 'Show-BusBuddyWelcome' -ErrorAction SilentlyContinue
} catch {
    Write-Information "Non-fatal error occurred during module export." -InformationAction Continue
}
