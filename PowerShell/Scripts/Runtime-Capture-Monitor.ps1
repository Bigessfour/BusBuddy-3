#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Wrapper for runtime error capture with proper waiting and output monitoring
.DESCRIPTION
    This script properly waits for the runtime error capture to complete and monitors output
.EXAMPLE
    .\Runtime-Capture-Monitor.ps1 -Duration 60
#>

param(
    [int]$Duration = 30,
    [switch]$DetailedLogging
)

function Wait-ForProcessCompletion {
    param(
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 600,
        [switch]$ShowProgress
    )

    $startTime = Get-Date
    $lastProgressTime = Get-Date

    while (-not $Process.HasExited) {
        $elapsed = [int]((Get-Date) - $startTime).TotalSeconds

        if ($ShowProgress -and ((Get-Date) - $lastProgressTime).TotalSeconds -ge 2) {
            Write-Host "⏳ Waiting for completion... Elapsed: $elapsed seconds" -ForegroundColor Yellow
            $lastProgressTime = Get-Date
        }

        if ($elapsed -ge $TimeoutSeconds) {
            Write-Warning "Process timeout after $TimeoutSeconds seconds"
            return $false
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "✅ Process completed in $elapsed seconds" -ForegroundColor Green
    return $true
}

function Start-RuntimeCaptureWithMonitoring {
    param(
        [int]$Duration,
        [switch]$DetailedLogging
    )

    $scriptPath = Join-Path $PSScriptRoot "Capture-RuntimeErrors.ps1"

    if (-not (Test-Path $scriptPath)) {
        Write-Error "Runtime capture script not found at: $scriptPath"
        return $false
    }

    Write-Host "🚌 Starting BusBuddy Runtime Error Capture" -ForegroundColor Cyan
    Write-Host "📝 Duration: $Duration seconds" -ForegroundColor White
    Write-Host "📋 Detailed Logging: $DetailedLogging" -ForegroundColor White
    Write-Host "🕐 Start Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
    Write-Host ""

    # Build arguments
    $arguments = @("-File", $scriptPath, "-Duration", $Duration)
    if ($DetailedLogging) {
        $arguments += "-DetailedLogging"
    }

    # Start the capture process
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "pwsh"
    $psi.Arguments = $arguments -join " "
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $false
    $psi.WorkingDirectory = (Get-Location).Path

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    try {
        Write-Host "🚀 Launching capture process..." -ForegroundColor Green
        $process.Start()

        Write-Host "✅ Process started with PID: $($process.Id)" -ForegroundColor Green
        Write-Host "⏳ Waiting for completion (estimated: $($Duration + 10) seconds)..." -ForegroundColor Yellow
        Write-Host ""

        # Wait for completion with progress
        $completed = Wait-ForProcessCompletion -Process $process -TimeoutSeconds ($Duration + 30) -ShowProgress

        if ($completed) {
            # Read output
            $output = $process.StandardOutput.ReadToEnd()
            $errors = $process.StandardError.ReadToEnd()

            Write-Host "📊 CAPTURE COMPLETED" -ForegroundColor Green
            Write-Host "=" * 50 -ForegroundColor Green

            if ($output) {
                Write-Host "📤 Standard Output:" -ForegroundColor Cyan
                Write-Host $output -ForegroundColor White
                Write-Host ""
            }

            if ($errors) {
                Write-Host "⚠️ Standard Error:" -ForegroundColor Red
                Write-Host $errors -ForegroundColor White
                Write-Host ""
            }

            Write-Host "🏁 Exit Code: $($process.ExitCode)" -ForegroundColor $(if ($process.ExitCode -eq 0) { "Green" } else { "Red" })

            return $true
        } else {
            Write-Error "Process did not complete within timeout"
            return $false
        }

    } catch {
        Write-Error "Error running capture process: $($_.Exception.Message)"
        return $false
    } finally {
        if (-not $process.HasExited) {
            Write-Warning "Forcibly stopping process..."
            $process.Kill()
        }
        $process.Dispose()
    }
}

function Show-CaptureResults {
    $captureDir = "logs\runtime-capture"

    if (-not (Test-Path $captureDir)) {
        Write-Warning "No capture directory found at: $captureDir"
        return
    }

    # Find most recent files
    $summaryFiles = Get-ChildItem -Path $captureDir -Filter "*summary.md" | Sort-Object LastWriteTime -Descending

    if ($summaryFiles.Count -eq 0) {
        Write-Warning "No summary files found in capture directory"
        return
    }

    $latestSummary = $summaryFiles[0]
    Write-Host "📋 Latest Capture Summary: $($latestSummary.Name)" -ForegroundColor Cyan
    Write-Host "🕐 Created: $($latestSummary.LastWriteTime)" -ForegroundColor White
    Write-Host ""

    # Show summary content
    $content = Get-Content $latestSummary.FullName -Raw
    Write-Host $content -ForegroundColor White
}

# Main execution
function Invoke-RuntimeCaptureMonitor {
    <#
    .SYNOPSIS
        Execute the runtime capture monitoring process
    .PARAMETER Duration
        Duration in seconds to run the capture
    .PARAMETER DetailedLogging
        Enable detailed logging output
    #>
    [CmdletBinding()]
    param(
        [int]$Duration = 30,
        [switch]$DetailedLogging
    )

try {
    Write-Host "🔧 BusBuddy Runtime Capture Monitor v1.0" -ForegroundColor Magenta
    Write-Host "=" * 60 -ForegroundColor Magenta
    Write-Host ""

    $success = Start-RuntimeCaptureWithMonitoring -Duration $Duration -DetailedLogging:$DetailedLogging

    if ($success) {
        Write-Host ""
        Write-Host "📊 Showing capture results..." -ForegroundColor Cyan
        Show-CaptureResults
    } else {
        Write-Host "❌ Capture process failed" -ForegroundColor Red
    }

} catch {
    Write-Error "Monitor error: $($_.Exception.Message)"
} finally {
    Write-Host ""
    Write-Host "🏁 Monitor session completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Magenta
}
}

# Only run automatically if script is invoked directly (not dot-sourced)
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-RuntimeCaptureMonitor -Duration $Duration -DetailedLogging:$DetailedLogging
}
