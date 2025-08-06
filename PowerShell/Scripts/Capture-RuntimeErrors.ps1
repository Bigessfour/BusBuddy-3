#Requires -Version 7.5

<#
.SYNOPSIS
    Comprehensive runtime error capture for BusBuddy application
.DESCRIPTION
    Executes BusBuddy with multiple error capture mechanisms to identify
    and log runtime issues during application execution.
.EXAMPLE
    .\Capture-RuntimeErrors.ps1
.EXAMPLE
    .\Capture-RuntimeErrors.ps1 -Duration 300 -DetailedLogging
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int]$Duration = 60,  # Run for 60 seconds by default

    [Parameter()]
    [switch]$DetailedLogging,

    [Parameter()]
    [switch]$OpenLogsAfter,

    [Parameter()]
    [string]$OutputDirectory = "logs\runtime-capture"
)

# Import required modules
Import-Module (Join-Path $PSScriptRoot "..\Modules\BusBuddy.ExceptionCapture.psm1") -Force

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$sessionName = "runtime-capture-$timestamp"

# Ensure output directory exists
$fullOutputPath = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) $OutputDirectory
if (-not (Test-Path $fullOutputPath)) {
    New-Item -Path $fullOutputPath -ItemType Directory -Force | Out-Null
}

$logFiles = @{
    MainLog = Join-Path $fullOutputPath "$sessionName-main.log"
    ErrorLog = Join-Path $fullOutputPath "$sessionName-errors.log"
    DebugLog = Join-Path $fullOutputPath "$sessionName-debug.log"
    SummaryReport = Join-Path $fullOutputPath "$sessionName-summary.md"
}

Write-Output "🚌 BusBuddy Runtime Error Capture Session"
Write-Output "========================================"
Write-Output "Session ID: $sessionName"
Write-Output "Duration: $Duration seconds"
Write-Output "Output Directory: $fullOutputPath"
Write-Output ""

# Start error monitoring
Write-Output "📊 Starting comprehensive error monitoring..."

try {
    # Step 1: Build the application first
    Write-Output "🏗️ Building BusBuddy application..."
    $buildResult = Invoke-BusBuddyWithExceptionCapture -Command "dotnet" -Arguments @("build", "BusBuddy.sln") -Context "Pre-run build"

    if ($LASTEXITCODE -ne 0) {
        Write-Output "❌ Build failed. Cannot proceed with runtime testing."
        if ($buildResult.ErrorOutput) {
            Write-Output "Build errors: $($buildResult.ErrorOutput)"
        }
        return
    }

    Write-Output "✅ Build completed successfully."

    Write-Output "✅ Build successful. Starting runtime capture..."

    # Step 2: Start application with comprehensive error capture
    $appArgs = @(
        "run",
        "--project", "BusBuddy.WPF\BusBuddy.WPF.csproj"
    )

    if ($DetailedLogging) {
        $appArgs += @("--", "--verbose", "--debug-mode")
    }

    Write-Output "🚀 Launching BusBuddy with error capture..."
    Write-Output "   Command: dotnet $($appArgs -join ' ')"
    Write-Output "   Timeout: $Duration seconds"
    Write-Output ""
    Write-Output "📝 Monitoring logs:"
    Write-Output "   Main: $($logFiles.MainLog)"
    Write-Output "   Errors: $($logFiles.ErrorLog)"
    Write-Output "   Debug: $($logFiles.DebugLog)"
    Write-Output ""

    # Step 3: Execute with proper process capture and real-time logging
    Write-Output "🚀 Launching BusBuddy with comprehensive capture..."
    Write-Output "   Command: dotnet $($appArgs -join ' ')"
    Write-Output "   Timeout: $Duration seconds"
    Write-Output ""
    Write-Output "📝 Real-time logging to:"
    Write-Output "   Main: $($logFiles.MainLog)"
    Write-Output "   Errors: $($logFiles.ErrorLog)"
    Write-Output ""

    # Initialize log files
    "=== BusBuddy Runtime Capture Started at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" | Out-File -FilePath $logFiles.MainLog -Encoding UTF8
    "=== Error Stream Capture Started at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" | Out-File -FilePath $logFiles.ErrorLog -Encoding UTF8

    # Start the process with proper stream capture
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "dotnet"
        $psi.Arguments = $appArgs -join " "
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $false  # Allow window to show
        $psi.WorkingDirectory = $PWD.Path

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $psi

        # Start the process
        Write-Output "🚀 Starting BusBuddy application..."
        $process.Start()

        Write-Output "✅ Application started with PID: $($process.Id)"
        Write-Output "⏱️ Monitoring for $Duration seconds..."
        Write-Output ""

        $startTime = Get-Date
        $errorCount = 0
        $outputLines = 0

        # Monitor the process for the specified duration
        while (-not $process.HasExited -and ((Get-Date) - $startTime).TotalSeconds -lt $Duration) {
            # Read available output
            if (-not $process.StandardOutput.EndOfStream) {
                $line = $process.StandardOutput.ReadLine()
                if ($line) {
                    $timestamp = Get-Date -Format 'HH:mm:ss.fff'
                    $logEntry = "[$timestamp] STDOUT: $line"
                    Add-Content -Path $logFiles.MainLog -Value $logEntry
                    $outputLines++

                    # Show progress
                    if ($outputLines % 10 -eq 0) {
                        Write-Output "📄 Captured $outputLines lines of output..."
                    }
                }
            }

            # Read available errors
            if (-not $process.StandardError.EndOfStream) {
                $line = $process.StandardError.ReadLine()
                if ($line) {
                    $timestamp = Get-Date -Format 'HH:mm:ss.fff'
                    $logEntry = "[$timestamp] STDERR: $line"
                    Add-Content -Path $logFiles.ErrorLog -Value $logEntry
                    $errorCount++
                    Write-Output "⚠️ Error detected: $line"
                }
            }

            # Show progress indicator
            $elapsed = [int]((Get-Date) - $startTime).TotalSeconds
            if ($elapsed % 5 -eq 0) {
                Write-Progress -Activity "Runtime Error Monitoring" -Status "Elapsed: $elapsed seconds, Output: $outputLines lines, Errors: $errorCount" -PercentComplete (($elapsed / $Duration) * 100)
            }

            Start-Sleep -Milliseconds 100
        }

        Write-Progress -Activity "Runtime Error Monitoring" -Completed

        # Final output read
        while (-not $process.StandardOutput.EndOfStream) {
            $line = $process.StandardOutput.ReadLine()
            if ($line) {
                $timestamp = Get-Date -Format 'HH:mm:ss.fff'
                Add-Content -Path $logFiles.MainLog -Value "[$timestamp] STDOUT: $line"
                $outputLines++
            }
        }

        while (-not $process.StandardError.EndOfStream) {
            $line = $process.StandardError.ReadLine()
            if ($line) {
                $timestamp = Get-Date -Format 'HH:mm:ss.fff'
                Add-Content -Path $logFiles.ErrorLog -Value "[$timestamp] STDERR: $line"
                $errorCount++
            }
        }

        # Stop the process if still running
        if (-not $process.HasExited) {
            Write-Output "🛑 Stopping application after $Duration seconds..."
            $process.CloseMainWindow()
            if (-not $process.WaitForExit(5000)) {
                $process.Kill()
            }
        }

        $actualDuration = [int]((Get-Date) - $startTime).TotalSeconds
        $exitCode = if ($process.HasExited) { $process.ExitCode } else { "N/A" }

        Write-Output ""
        Write-Output "📊 Capture Summary:"
        Write-Output "   Duration: $actualDuration seconds"
        Write-Output "   Output lines: $outputLines"
        Write-Output "   Error count: $errorCount"
        Write-Output "   Exit code: $exitCode"
        Write-Output "   Process exited: $($process.HasExited)"

        $process.Dispose()
    }
    catch {
        Write-Output "❌ Error during process execution: $($_.Exception.Message)"
        $errorCount++
        Add-Content -Path $logFiles.ErrorLog -Value "[$(Get-Date -Format 'HH:mm:ss.fff')] PROCESS_ERROR: $($_.Exception.Message)"
    }

    # Step 4: Generate comprehensive summary report
    Write-Output ""
    Write-Output "📋 Generating comprehensive summary report..."

    # Analyze captured logs
    $mainLogExists = Test-Path $logFiles.MainLog
    $errorLogExists = Test-Path $logFiles.ErrorLog
    $mainLogSize = if ($mainLogExists) { (Get-Item $logFiles.MainLog).Length } else { 0 }
    $errorLogSize = if ($errorLogExists) { (Get-Item $logFiles.ErrorLog).Length } else { 0 }

    # Count actual lines in logs
    $mainLines = if ($mainLogExists) { (Get-Content $logFiles.MainLog -ErrorAction SilentlyContinue).Count } else { 0 }
    $errorLines = if ($errorLogExists) { (Get-Content $logFiles.ErrorLog -ErrorAction SilentlyContinue).Count } else { 0 }

    # Extract error patterns
    $errorSummary = ""
    if ($errorLogExists -and $errorLines -gt 1) {
        $errorContent = Get-Content $logFiles.ErrorLog -ErrorAction SilentlyContinue
        $actualErrors = $errorContent | Where-Object { $_ -notmatch "Error Stream Capture Started" -and $_ -notmatch "^\s*$" }

        if ($actualErrors.Count -gt 0) {
            $errorSummary = "### Captured Errors ($($actualErrors.Count) total):`n`n"
            $actualErrors | ForEach-Object { $errorSummary += "- ``$_```n" }
        } else {
            $errorSummary = "✅ No actual errors detected in error stream"
        }
    } else {
        $errorSummary = "✅ No error log file created or no errors detected"
    }

    $summaryContent = @"
# BusBuddy Runtime Error Capture Report
**Session ID:** $sessionName
**Date:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Duration:** $actualDuration seconds (Target: $Duration)
**Total Errors Detected:** $errorCount

## Session Details
- **Build Status:** ✅ Successful
- **Application Launch:** $(if ($outputLines -gt 0) { "✅ Successful" } else { "❌ Failed or No Output" })
- **Monitoring Duration:** $actualDuration seconds
- **Output Lines Captured:** $mainLines
- **Error Lines Captured:** $errorLines
- **Main Log Size:** $mainLogSize bytes
- **Error Log Size:** $errorLogSize bytes
- **Process Exit Code:** $exitCode
- **Process Exited Cleanly:** $($process.HasExited)

## Log Files Generated
1. **Main Log:** ``$($logFiles.MainLog)`` ($mainLogSize bytes)
2. **Error Log:** ``$($logFiles.ErrorLog)`` ($errorLogSize bytes)
3. **Debug Log:** ``$($logFiles.DebugLog)`` (Reserved for future use)

## Capture Analysis
$(if ($outputLines -eq 0 -and $errorLines -le 1) {
    "❌ **NO APPLICATION OUTPUT CAPTURED**
This suggests the application failed to start, crashed immediately, or the capture mechanism failed.

**Troubleshooting Steps:**
1. Run ``dotnet run --project BusBuddy.WPF\BusBuddy.WPF.csproj`` manually
2. Check if the application window opens
3. Review build errors with ``dotnet build BusBuddy.sln``
4. Check Syncfusion license configuration
5. Verify all dependencies are installed"
} elseif ($outputLines -gt 0 -and $errorCount -eq 0) {
    "✅ **APPLICATION OUTPUT CAPTURED SUCCESSFULLY**
The application generated $outputLines lines of output with no errors detected."
} else {
    "⚠️ **MIXED RESULTS**
Application generated $outputLines lines of output but $errorCount errors were detected."
})

## Error Summary
$errorSummary

## Recommendations
$(if ($outputLines -eq 0) {
    "🚨 **IMMEDIATE ACTION REQUIRED**
The application appears to have failed to start or crashed immediately.
1. Test manual startup: ``dotnet run --project BusBuddy.WPF\BusBuddy.WPF.csproj``
2. Check for missing dependencies or configuration issues
3. Review Syncfusion license setup
4. Run ``bb-anti-regression`` to check for violations"
} elseif ($errorCount -eq 0) {
    "✅ **EXCELLENT!** Application ran cleanly with proper output capture.
1. Review main log for application behavior analysis
2. Continue with normal development workflow
3. Consider running longer captures for extended testing"
} else {
    "⚠️ **ERRORS DETECTED** - Review and fix issues:
1. Analyze error patterns in ``$($logFiles.ErrorLog)``
2. Fix identified issues
3. Re-run capture to validate fixes
4. Run ``bb-anti-regression`` to check for violations"
})

## Next Steps
1. **Review Logs:** Check main log: ``$($logFiles.MainLog)``
2. **Manual Test:** Run ``dotnet run --project BusBuddy.WPF\BusBuddy.WPF.csproj``
3. **Build Check:** Run ``bb-build`` to ensure clean build
4. **Compliance:** Run ``bb-anti-regression`` to check standards
5. **Re-test:** Run capture again after fixes

---
*Generated by BusBuddy Runtime Error Capture System v2.0*
*Comprehensive logging and analysis enabled*
"@

    $summaryContent | Out-File -FilePath $logFiles.SummaryReport -Encoding UTF8

    # Step 5: Final comprehensive report
    Write-Output ""
    Write-Output "✅ Runtime error capture completed!"
    Write-Output ""
    Write-Output "📊 **COMPREHENSIVE SUMMARY:**"
    Write-Output "   • Actual Duration: $actualDuration seconds"
    Write-Output "   • Output Lines Captured: $outputLines"
    Write-Output "   • Errors Detected: $errorCount"
    Write-Output "   • Main Log Size: $mainLogSize bytes"
    Write-Output "   • Exit Code: $exitCode"
    Write-Output ""

    if ($outputLines -eq 0 -and $errorCount -eq 0) {
        Write-Output "🚨 **CRITICAL ISSUE:** No application output captured!"
        Write-Output "   This indicates the application failed to start or crashed immediately."
        Write-Output "   Manual test required: dotnet run --project BusBuddy.WPF\\BusBuddy.WPF.csproj"
    } elseif ($outputLines -gt 0 -and $errorCount -eq 0) {
        Write-Output "🎉 **EXCELLENT!** Application ran with proper output capture."
        Write-Output "   $outputLines lines of output captured with no errors!"
    } else {
        Write-Output "⚠️ **MIXED RESULTS:** $outputLines lines captured, $errorCount errors detected."
        Write-Output "   Review error log: $($logFiles.ErrorLog)"
    }

    Write-Output ""
    Write-Output "📁 **All logs saved to:** $fullOutputPath"
    Write-Output "📋 **Summary report:** $($logFiles.SummaryReport)"

    if ($OpenLogsAfter) {
        Write-Output "📂 Opening log directory..."
        Start-Process explorer $fullOutputPath
    }

    # Return comprehensive summary object
    return @{
        SessionId = $sessionName
        Duration = $actualDuration
        TargetDuration = $Duration
        ErrorCount = $errorCount
        OutputLines = $outputLines
        MainLogSize = $mainLogSize
        ErrorLogSize = $errorLogSize
        ExitCode = $exitCode
        ProcessExited = if ($process) { $process.HasExited } else { $false }
        LogFiles = $logFiles
        Success = ($outputLines -gt 0 -and $errorCount -eq 0)
        CriticalIssue = ($outputLines -eq 0 -and $errorCount -eq 0)
    }
}
catch {
    Write-Output "❌ Error during runtime capture: $($_.Exception.Message)"
    Write-Output "Stack trace: $($_.ScriptStackTrace)"
}
finally {
    Write-Output ""
    Write-Output "🏁 Runtime error capture session completed."
}
