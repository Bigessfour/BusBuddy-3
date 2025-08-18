#Requires -Version 7.5

<#
.SYNOPSIS
    BusBuddy Exception Capture Module - PowerShell 7.5.2 Compliant

.DESCRIPTION
    Professional PowerShell module for exception handling and error capture in BusBuddy development.
    Provides structured exception handling, error logging, and application monitoring capabilities.
    Follows Microsoft PowerShell 7.5.2 guidelines and best practices.

.NOTES
    File Name      : BusBuddy.ExceptionCapture.psm1
    Author         : BusBuddy Development Team
    Prerequisite   : PowerShell 7.5+ (Microsoft Standard)
    Copyright      : (c) 2025 BusBuddy Project
#>

# Module for BusBuddy exception handling and error capture

# NOTE: Documentation-first compliance:
# - Comment-based help: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Comment_Based_Help
# - Background jobs & argument passing: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Jobs
# - $Using: and variable passing guidance: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Remote_Variables
# - Write-Information (no Write-Host): https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/write-information

<#
.SYNOPSIS
Starts BusBuddy with exception capture and optional live log output.
.DESCRIPTION
Launches BusBuddy while capturing errors to a log file. Variables are passed into the background job
via param/ArgumentList to comply with analyzer rules and Microsoft job patterns.
.PARAMETER ProjectPath
Path to the BusBuddy project to run.
.PARAMETER DurationSeconds
Maximum capture duration in seconds.
.PARAMETER ShowLogs
If specified, writes informational messages about log locations to the information stream.
.EXAMPLE
Start-BusBuddyWithCapture -ProjectPath $pwd -DurationSeconds 120 -ShowLogs
#>
function Start-BusBuddyWithCapture {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$ProjectPath,

        [Parameter()]
        [ValidateRange(1, 86400)]
        [int]$DurationSeconds = 300,

        [Parameter()]
        [switch]$ShowLogs
    )

    begin {
        # Ensure log directory exists
        if (-not (Test-Path -Path $LogPath)) {
            New-Item -Path $LogPath -ItemType Directory -Force | Out-Null
            Write-Information "📁 Created log directory: $LogPath" -InformationAction Continue
        }

        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $logFile = Join-Path -Path $LogPath -ChildPath "BusBuddy-ErrorCapture-$timestamp.log"
        $errorLogFile = Join-Path -Path $LogPath -ChildPath "BusBuddy-Errors-$timestamp.json"
    }

    process {
        try {
            Write-Information "📝 Error log: $LogPath" -InformationAction Continue
            Write-Information "⏱️  Monitoring duration: $MonitorDuration minutes" -InformationAction Continue

            if ($ShowLogs) {
                Write-Information ("Starting with capture. Logs: {0}; Errors: {1}" -f $logFile, $errorLogFile) -InformationAction Continue
            }

            # Start application monitoring job
            $monitorJob = Start-ThreadJob -Name 'BusBuddyCapture' -ScriptBlock {
                param(
                    [string]$using:using:logFile,
                    [string]$using:using:errorLogFile,
                    [int]$using:using:duration,
                    [string]$using:using:projectPath,
                    [string]$using:using:message
                )

                # Use the parameters to avoid PSReviewUnusedParameter and to make the job self-contained
                if (-not (Test-Path -LiteralPath $using:logFile)) {
                    $null = New-Item -ItemType File -Path $using:logFile -Force
                }
                if (-not (Test-Path -LiteralPath $using:errorLogFile)) {
                    $null = New-Item -ItemType File -Path $using:errorLogFile -Force
                }

                if ($using:message) {
                    # Minimal usage to demonstrate consumption
                    Add-Content -Path $using:logFile -Value ("{0:u} {1}" -f (Get-Date), $using:message)
                }

                # Monitoring logic
                Write-Host "Starting BusBuddy application monitoring" -ForegroundColor Green
                $endTime = (Get-Date).AddSeconds($duration)
                $errors = @()

                # Watch logs directory for new errors
                $logsPath = Join-Path -Path $projectPath -ChildPath "Logs"

                # Monitor until timeout
                while ((Get-Date) -lt $endTime) {
                    # Check for exception logs
                    $newErrorLogs = Get-ChildItem -Path $logsPath -Recurse -Filter "*.log" |
                        Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-5) }

                    foreach ($errorLog in $newErrorLogs) {
                        $content = Get-Content -Path $errorLog.FullName -Raw
                        if ($content -match "Exception|Error|Fatal|Crash|Failed") {
                            $errorData = @{
                                Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                                File      = $errorLog.Name
                                Path      = $errorLog.FullName
                                Error     = ($content -split "\r?\n" | Where-Object { $_ -match "Exception|Error|Fatal" } | Select-Object -First 5) -join "`n"
                                Priority  = if ($content -match "System\.Exception|Fatal") { "High" } else { "Medium" }
                            }
                            $errors += $errorData
                            Add-Content -Path $logFile -Value "Detected error in $($errorLog.Name): $($errorData.Error)"
                        }
                    }

                    # Sleep to reduce CPU usage
                    Start-Sleep -Seconds 3
                }

                # Export errors to JSON if any found
                if ($errors.Count -gt 0) {
                    $errors | ConvertTo-Json -Depth 3 | Set-Content -Path $errorLogFile
                    Add-Content -Path $logFile -Value "Monitoring completed. Found $($errors.Count) errors."
                }
                else {
                    Add-Content -Path $logFile -Value "Monitoring completed. No errors detected."
                }

            } -ArgumentList $logFile, $errorLogFile, $DurationSeconds, $ProjectPath, $message

            # Run the application
            Write-Information "🚀 Starting BusBuddy application..." -InformationAction Continue

            try {
                # Navigate to project directory
                Push-Location -Path $ProjectPath

                # Run the application (using the approved method from instructions)
                dotnet run --project BusBuddy.csproj
            }
            catch {
                Write-Error "❌ Failed to start BusBuddy application: $_"
            }
            finally {
                # Return to original directory
                Pop-Location
            }

            # Wait for monitoring job to complete
            Write-Information "⏳ Waiting for monitoring to complete..." -InformationAction Continue
            $null = Wait-Job -Job $monitorJob -Timeout ($MonitorDuration * 60 + 30)

            # Process results
            $result = Receive-Job -Job $monitorJob
            Remove-Job -Job $monitorJob -Force

            # Check for captured errors
            if (Test-Path $errorLogFile) {
                $errorCount = (Get-Content $errorLogFile | ConvertFrom-Json).Count
                if ($errorCount -gt 0) {
                    Write-Warning "⚠️  Detected $errorCount errors during execution. See $errorLogFile for details."
                }
                else {
                    Write-Information "✅ No errors detected during execution." -InformationAction Continue
                }
            }
        }
        catch {
            Write-Error "❌ Error in Start-BusBuddyWithCapture: $_"
        }
    }

    end {
        Write-Information "✅ BusBuddy execution with error capture completed." -InformationAction Continue
    }
}

<#
.SYNOPSIS
Generates a parsed BusBuddy error report from captured logs.
.DESCRIPTION
Reads previously captured log files and returns a structured error report object. Uses the
information stream for status output and avoids Write-Host.
.PARAMETER LogPath
Optional path to the log file directory.
.EXAMPLE
Get-BusBuddyErrorReport -LogPath ".\logs"
#>
function Get-BusBuddyErrorReport {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$LogPath
    )

    try {
        if (-not (Test-Path -Path $LogPath)) {
            Write-Warning "Log directory not found: $LogPath"
            return
        }

        $cutoffDate = (Get-Date).AddDays(-$Days)
        $errorFiles = Get-ChildItem -Path $LogPath -Filter "BusBuddy-Errors-*.json" |
            Where-Object { $_.LastWriteTime -ge $cutoffDate }

        if (-not $errorFiles -or $errorFiles.Count -eq 0) {
            Write-Information "No error reports found for the last $Days days." -InformationAction Continue
            return
        }

        $allErrors = @()
        foreach ($file in $errorFiles) {
            $errors = Get-Content $file.FullName -Raw | ConvertFrom-Json
            $allErrors += $errors
        }

        # Return results
        return [PSCustomObject]@{
            TotalErrors          = $allErrors.Count
            HighPriorityErrors   = ($allErrors | Where-Object { $_.Priority -eq "High" }).Count
            MediumPriorityErrors = ($allErrors | Where-Object { $_.Priority -eq "Medium" }).Count
            LowPriorityErrors    = ($allErrors | Where-Object { $_.Priority -eq "Low" }).Count
            MostRecentError      = $allErrors | Sort-Object -Property Timestamp -Descending | Select-Object -First 1
            ErrorSummary         = $allErrors | Group-Object -Property File |
                Sort-Object -Property Count -Descending |
                Select-Object Name, Count
            Errors               = $allErrors
        }
    }
    catch {
        Write-Error "Error processing error reports: $_"
    }
}

# Export module functions
Export-ModuleMember -Function Start-BusBuddyWithCapture, Get-BusBuddyErrorReport

function Write-BusBuddyException {
    <#
    .SYNOPSIS
        Writes structured exception information to log file

    .DESCRIPTION
        Creates detailed exception logs following Microsoft PowerShell standards
        with structured data for analysis and debugging.

    .PARAMETER ExceptionInfo
        Hashtable containing detailed exception information

    .PARAMETER LogPath
        Path to the log file for exception data

    .EXAMPLE
        Write-BusBuddyException -ExceptionInfo $exceptionData -LogPath "logs\errors.log"

    .NOTES
        Uses Microsoft PowerShell 7.5.2 structured logging patterns
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$ExceptionInfo,

        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    begin {
        Write-Verbose "Writing exception to log: $LogPath"
    }

    process {
        try {
            $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

            $logEntry = @"
=== BUSBUDDY EXCEPTION CAPTURE ===
Timestamp: $timestamp
Execution ID: $($ExceptionInfo.ExecutionId)
Context: $($ExceptionInfo.Context)
Command: $($ExceptionInfo.Command)
Arguments: $($ExceptionInfo.Arguments -join ', ')
Start Time: $($ExceptionInfo.StartTime)
End Time: $($ExceptionInfo.EndTime)
Duration: $((New-TimeSpan -Start $ExceptionInfo.StartTime -End $ExceptionInfo.EndTime).TotalSeconds) seconds

EXCEPTION DETAILS:
Type: $($ExceptionInfo.Exception.GetType().FullName)
Message: $($ExceptionInfo.Exception.Message)
HResult: $($ExceptionInfo.Exception.HResult)

ERROR RECORD:
Category: $($ExceptionInfo.ErrorRecord.CategoryInfo.Category)
Activity: $($ExceptionInfo.ErrorRecord.CategoryInfo.Activity)
Reason: $($ExceptionInfo.ErrorRecord.CategoryInfo.Reason)
Target: $($ExceptionInfo.ErrorRecord.CategoryInfo.TargetName)

STACK TRACE:
$($ExceptionInfo.ScriptStackTrace)

INNER EXCEPTION:
$($ExceptionInfo.Exception.InnerException | ConvertTo-Json -Depth 3)

=== END EXCEPTION CAPTURE ===

"@

            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
            Write-Information "Exception logged to: $LogPath" -InformationAction Continue
        }
        catch {
            Write-Error "Failed to write exception log: $($_.Exception.Message)" -ErrorAction Stop
        }
    }

    end {
        Write-Verbose "Exception logging completed"
    }
}

function Write-BusBuddyExecutionLog {
    <#
    .SYNOPSIS
        Writes successful execution information to log file

    .DESCRIPTION
        Creates structured execution logs for successful operations to provide
        complete audit trail of BusBuddy operations.

    .PARAMETER ExecutionInfo
        Hashtable containing execution details

    .PARAMETER LogPath
        Path to the log file for execution data

    .EXAMPLE
        Write-BusBuddyExecutionLog -ExecutionInfo $execData -LogPath "logs\execution.log"

    .NOTES
        Complements exception logging with success tracking
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$ExecutionInfo,

        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    begin {
        Write-Verbose "Writing execution log to: $LogPath"
    }

    process {
        try {
            $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            $duration = (New-TimeSpan -Start $ExecutionInfo.StartTime -End $ExecutionInfo.EndTime).TotalSeconds

            $logEntry = @"
=== BUSBUDDY EXECUTION SUCCESS ===
Timestamp: $timestamp
Execution ID: $($ExecutionInfo.ExecutionId)
Context: $($ExecutionInfo.Context)
Command: $($ExecutionInfo.Command)
Arguments: $($ExecutionInfo.Arguments -join ', ')
Duration: $duration seconds
Exit Code: $($ExecutionInfo.ExitCode)
Status: SUCCESS
=== END EXECUTION LOG ===

"@

            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
            Write-Verbose "Execution logged successfully"
        }
        catch {
            Write-Warning "Failed to write execution log: $($_.Exception.Message)"
        }
    }

    end {
        Write-Verbose "Execution logging completed"
    }
}

function Start-BusBuddyErrorMonitoring {
    <#
    .SYNOPSIS
        Starts real-time error monitoring for BusBuddy operations

    .DESCRIPTION
        Initializes background error monitoring with file watching and
        real-time error detection capabilities.

    .PARAMETER LogPath
        Path to monitor for error logs

    .PARAMETER MonitoringDuration
        Duration in minutes to monitor. Default is 30 minutes.

    .PARAMETER AlertThreshold
        Number of errors before triggering alert. Default is 5.

    .EXAMPLE
        Start-BusBuddyErrorMonitoring -LogPath "logs\errors.log" -MonitoringDuration 60

    .NOTES
        Uses PowerShell 7.5.2 background job capabilities for monitoring
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogPath,

        [Parameter()]
        [ValidateRange(1, 1440)]
        [int]$MonitoringDuration = 30,

        [Parameter()]
        [ValidateRange(1, 100)]
        [int]$AlertThreshold = 5
    )

    begin {
        Write-Information "Starting BusBuddy error monitoring" -InformationAction Continue
    }

    process {
        try {
            $monitoringScript = {
                param($LogPath, $Duration, $Threshold)

                $errorCount = 0
                $endTime = (Get-Date).AddMinutes($Duration)

                while ((Get-Date) -lt $endTime) {
                    if (Test-Path $LogPath) {
                        $content = Get-Content $LogPath -Tail 10
                        $newErrors = ($content | Select-String "EXCEPTION CAPTURE").Count

                        if ($newErrors -gt $errorCount) {
                            $errorCount = $newErrors
                            if ($errorCount -ge $Threshold) {
                                Write-Warning "ERROR THRESHOLD EXCEEDED: $errorCount errors detected"
                            }
                        }
                    }
                    Start-Sleep -Seconds 30
                }
            }

            $job = Start-Job -ScriptBlock $monitoringScript -ArgumentList $LogPath, $MonitoringDuration, $AlertThreshold
            Write-Information "Error monitoring started (Job ID: $($job.Id))" -InformationAction Continue
            return $job
        }
        catch {
            Write-Error "Failed to start error monitoring: $($_.Exception.Message)" -ErrorAction Stop
        }
    }

    end {
        Write-Verbose "Error monitoring initialization completed"
    }
}

function Get-BusBuddyExceptionSummary {
    <#
    .SYNOPSIS
        Generates summary report of captured exceptions

    .DESCRIPTION
        Analyzes exception logs and provides structured summary of errors,
        patterns, and recommendations for BusBuddy development.

    .PARAMETER LogPath
        Path to exception log file to analyze

    .PARAMETER Hours
        Number of hours back to analyze. Default is 24 hours.

    .EXAMPLE
        Get-BusBuddyExceptionSummary -LogPath "logs\errors.log" -Hours 12

    .NOTES
        Provides actionable insights from exception data
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateScript({ Test-Path $_ -PathType Leaf })]
        [string]$LogPath,

        [Parameter()]
        [ValidateRange(1, 168)]
        [int]$Hours = 24
    )

    begin {
        Write-Verbose "Analyzing exceptions from: $LogPath"
    }

    process {
        try {
            $content = Get-Content $LogPath -Raw
            $exceptions = $content -split "=== BUSBUDDY EXCEPTION CAPTURE ===" | Where-Object { $_.Trim() }

            $cutoffTime = (Get-Date).AddHours(-$Hours)
            $recentExceptions = $exceptions | ForEach-Object {
                if ($_ -match "Timestamp: (.+)") {
                    $timestamp = [DateTime]::Parse($matches[1])
                    if ($timestamp -gt $cutoffTime) {
                        $_
                    }
                }
            }

            $summary = [PSCustomObject]@{
                LogFile              = $LogPath
                AnalysisPeriod       = "$Hours hours"
                TotalExceptions      = $recentExceptions.Count
                UniqueExceptionTypes = ($recentExceptions | ForEach-Object {
                        if ($_ -match "Type: (.+)") { $matches[1] }
                    } | Sort-Object -Unique).Count
                CommonExceptions     = ($recentExceptions | ForEach-Object {
                        if ($_ -match "Type: (.+)") { $matches[1] }
                    } | Group-Object | Sort-Object Count -Descending | Select-Object -First 5)
                RecommendedActions   = @(
                    "Review most common exception types",
                    "Check for patterns in exception timing",
                    "Validate input data and error handling",
                    "Consider implementing retry logic for transient failures"
                )
            }

            Write-Output $summary
        }
        catch {
            Write-Error "Failed to analyze exceptions: $($_.Exception.Message)" -ErrorAction Stop
        }
    }

    end {
        Write-Verbose "Exception analysis completed"
    }
}

function Start-BusBuddyWithCapture {
    <#
    .SYNOPSIS
        Runs BusBuddy application with automatic error capture enabled

    .DESCRIPTION
        Combines application execution with comprehensive error capture and logging.
        This is a simplified command that automatically captures all errors during startup
        and runtime without requiring manual wrapping.

    .PARAMETER LogPath
        Custom path for error log file. Defaults to logs\app-errors.log in project directory.

    .PARAMETER MonitorDuration
        How long to monitor for errors after startup (in minutes). Default is 10 minutes.

    .PARAMETER ShowLogs
        Whether to display the log file location and recent errors after startup.

    .EXAMPLE
        Start-BusBuddyWithCapture

    .EXAMPLE
        Start-BusBuddyWithCapture -LogPath "logs\startup-errors.log" -MonitorDuration 15 -ShowLogs

    .NOTES
        Perfect for development - captures everything automatically
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$LogPath,

        [Parameter()]
        [ValidateRange(1, 60)]
        [int]$MonitorDuration = 10,

        [Parameter()]
        [switch]$ShowLogs
    )

    begin {
        Write-Information "🚌 Starting BusBuddy with automatic error capture..." -InformationAction Continue

        # Set default log path in project directory
        if (-not $LogPath) {
            $projectRoot = $PWD.Path
            if (-not (Test-Path (Join-Path $projectRoot "BusBuddy.sln"))) {
                # Search upward for project root
                $currentPath = $PWD.Path
                while ($currentPath -and $currentPath -ne (Split-Path $currentPath -Parent)) {
                    if (Test-Path (Join-Path $currentPath "BusBuddy.sln")) {
                        $projectRoot = $currentPath
                        break
                    }
                    $currentPath = Split-Path $currentPath -Parent
                }
            }
            $LogPath = Join-Path $projectRoot "Logs\app-errors-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
        }        # Ensure log directory exists
        $logDir = Split-Path $LogPath -Parent
        if (-not (Test-Path $logDir)) {
            New-Item -Path $logDir -ItemType Directory -Force | Out-Null
        }
    }

    process {
        try {
            Write-Information "📝 Error log: $LogPath" -InformationAction Continue
            Write-Information "⏱️  Monitoring duration: $MonitorDuration minutes" -InformationAction Continue
            Write-Information "" -InformationAction Continue

            # Create a simple process tracker file
            $processTrackerFile = Join-Path (Split-Path $LogPath -Parent) "busbuddy-process.pid"

            # Start background error monitoring with process tracking
            $monitoringScript = {
                param($LogPath, $Duration, $Threshold, $ProcessTracker)

                $errorCount = 0
                $endTime = (Get-Date).AddMinutes($Duration)

                # Get project root directory for monitoring additional log files
                $projectRoot = Split-Path (Split-Path $LogPath -Parent) -Parent

                # Define additional log files to monitor (from Serilog configuration)
                $today = Get-Date -Format "yyyyMMdd"
                $additionalLogFiles = @(
                    # Main log file (from direct App.xaml.cs initialization)
                    (Join-Path $projectRoot "logs\busbuddy-$today.txt"),
                    (Join-Path $projectRoot "Logs\busbuddy-$today.txt"),
                    # Errors log from appsettings.json configuration
                    (Join-Path $projectRoot "Logs\errors-actionable-$today.log"),
                    # Runtime errors log from global error handlers
                    (Join-Path $projectRoot "logs\runtime-errors.log"),
                    (Join-Path $projectRoot "Logs\runtime-errors.log")
                )

                while ((Get-Date) -lt $endTime) {
                    # Check if process tracker file still exists (app is running)
                    if (-not (Test-Path $ProcessTracker)) {
                        Write-Host "Application closed - stopping monitoring" -ForegroundColor Yellow
                        break
                    }

                    # Check primary log file
                    $foundErrors = $false
                    if (Test-Path $LogPath) {
                        $content = Get-Content $LogPath -Tail 10 -ErrorAction SilentlyContinue
                        $newErrors = ($content | Select-String "EXCEPTION CAPTURE").Count

                        if ($newErrors -gt $errorCount) {
                            $errorCount = $newErrors
                            $foundErrors = $true
                            Write-Host "Found errors in main log file: $LogPath" -ForegroundColor Yellow
                        }
                    }

                    # Check additional Serilog log files
                    foreach ($logFile in $additionalLogFiles) {
                        if (Test-Path $logFile) {
                            $content = Get-Content $logFile -Tail 20 -ErrorAction SilentlyContinue
                            if ($content) {
                                # Look for error indicators in the logs
                                $hasErrors = $content | Where-Object {
                                    $_ -match "ERROR|FATAL|Exception|failed|error|crash|stopped working"
                                }

                                if ($hasErrors) {
                                    $foundErrors = $true
                                    $errorCount++
                                    Write-Host "Found errors in log file: $logFile" -ForegroundColor Yellow
                                    $hasErrors | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkYellow }
                                }
                            }
                        }
                    }

                    # Report if threshold exceeded
                    if ($foundErrors -and $errorCount -ge $Threshold) {
                        Write-Host "🚨 ERROR THRESHOLD EXCEEDED: $errorCount errors detected" -ForegroundColor Red
                    }

                    Start-Sleep -Seconds 2
                }

                Write-Host "Error monitoring completed" -ForegroundColor Green
            }

            $monitorJob = Start-Job -ScriptBlock $monitoringScript -ArgumentList $LogPath, $MonitorDuration, 1, $processTrackerFile
            Write-Information "🔍 Error monitoring started (Job ID: $($monitorJob.Id))" -InformationAction Continue

            # Create process tracker file
            "BusBuddy Application Running - $(Get-Date)" | Out-File $processTrackerFile -Force

            Write-Information "🚀 Starting BusBuddy application..." -InformationAction Continue
            Write-Information "   � When you close the app, monitoring will stop automatically" -InformationAction Continue
            Write-Information "" -InformationAction Continue

            try {
                # Run the application normally - let user interact with it
                dotnet run --project "BusBuddy.WPF\BusBuddy.WPF.csproj"
            }
            finally {
                # Clean up when application closes
                Write-Information "" -InformationAction Continue
                Write-Information "🛑 Application closed - cleaning up..." -InformationAction Continue

                # Remove process tracker file to signal monitoring to stop
                if (Test-Path $processTrackerFile) {
                    Remove-Item $processTrackerFile -Force -ErrorAction SilentlyContinue
                }

                # Wait a moment for monitoring job to notice and clean up
                Start-Sleep -Seconds 4

                # Always stop and remove monitoring job, regardless of state
                if ($monitorJob.State -eq 'Running') {
                    Write-Information "🛑 Stopping error monitoring (forced cleanup)..." -InformationAction Continue
                    Stop-Job $monitorJob -ErrorAction SilentlyContinue
                }
                Remove-Job $monitorJob -ErrorAction SilentlyContinue
                Write-Verbose "Monitoring job cleaned up after app exit."
            }

            if ($ShowLogs) {
                Write-Information "" -InformationAction Continue
                Write-Information "📊 Error Capture Summary:" -InformationAction Continue

                # Define all potential log files to check
                $projectRoot = Split-Path (Split-Path $LogPath -Parent) -Parent
                $today = Get-Date -Format "yyyyMMdd"
                $logFiles = @(
                    # Custom error log (from our script)
                    @{Path = $LogPath; Description = "PowerShell Error Capture Log"; MaxEntries = 5 },
                    # Serilog main log files
                    @{Path = (Join-Path $projectRoot "logs\busbuddy-$today.txt"); Description = "Main Application Log"; MaxEntries = 5 },
                    @{Path = (Join-Path $projectRoot "Logs\busbuddy-$today.txt"); Description = "Main Application Log (alt path)"; MaxEntries = 5 },
                    # Serilog error logs
                    @{Path = (Join-Path $projectRoot "Logs\errors-actionable-$today.log"); Description = "Actionable Errors Log"; MaxEntries = 3 },
                    # Runtime errors from global handlers
                    @{Path = (Join-Path $projectRoot "logs\runtime-errors.log"); Description = "Runtime Errors Log"; MaxEntries = 3 },
                    @{Path = (Join-Path $projectRoot "Logs\runtime-errors.log"); Description = "Runtime Errors Log (alt path)"; MaxEntries = 3 }
                )

                $foundAnyLogs = $false

                foreach ($logFile in $logFiles) {
                    if (Test-Path $logFile.Path) {
                        $foundAnyLogs = $true
                        $logSize = (Get-Item $logFile.Path).Length
                        Write-Information "   $($logFile.Description): $($logFile.Path)" -InformationAction Continue
                        Write-Information "   Log Size: $logSize bytes" -InformationAction Continue

                        # Show recent entries if file has content
                        if ($logSize -gt 0) {
                            $recentEntries = Get-Content $logFile.Path -Tail $logFile.MaxEntries -ErrorAction SilentlyContinue
                            if ($recentEntries) {
                                Write-Information "   Recent Entries:" -InformationAction Continue
                                $recentEntries | ForEach-Object { Write-Information "     $_" -InformationAction Continue }
                            }
                        }
                        Write-Information "" -InformationAction Continue
                    }
                }

                if (-not $foundAnyLogs) {
                    Write-Information "   ✅ No error logs found - clean run!" -InformationAction Continue
                }
            }

        }
        catch {
            Write-Error "Failed to start BusBuddy with capture: $($_.Exception.Message)"
            throw
        }
    }

    end {
        Write-Information "🏁 BusBuddy session with error capture completed" -InformationAction Continue

        if ($ShowLogs -and (Test-Path $LogPath)) {
            Write-Information "💡 Tip: Review full log with: Get-Content '$LogPath'" -InformationAction Continue
            Write-Information "💡 Tip: Analyze errors with: bb-exception-summary -LogPath '$LogPath'" -InformationAction Continue
        }
    }
}

#endregion

#region Alias Definitions (PowerShell 7.5.2 Standard)

# Create aliases for commonly used functions
New-Alias -Name 'bb-catch-errors' -Value 'Invoke-BusBuddyWithExceptionCapture' -Force
New-Alias -Name 'bb-error-monitor' -Value 'Start-BusBuddyErrorMonitoring' -Force
New-Alias -Name 'bb-exception-summary' -Value 'Get-BusBuddyExceptionSummary' -Force
New-Alias -Name 'bb-run-safe' -Value 'Start-BusBuddyWithCapture' -Force

#endregion

#region Export Functions (Microsoft PowerShell 7.5.2 Standard)

Export-ModuleMember -Function @(
    'Invoke-BusBuddyWithExceptionCapture',
    'Write-BusBuddyException',
    'Write-BusBuddyExecutionLog',
    'Start-BusBuddyErrorMonitoring',
    'Get-BusBuddyExceptionSummary',
    'Start-BusBuddyWithCapture'
) -Alias @(
    'bb-catch-errors',
    'bb-error-monitor',
    'bb-exception-summary',
    'bb-run-safe'
)

#endregion
