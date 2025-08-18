# PowerShell Buffer Configuration for BusBuddy Development
# Fixes truncated output issues for ALL scenarios (builds, tests, tasks, etc.)

#region Core Buffer Configuration

# Increase PowerShell buffer size
try {
    if ($Host.UI.RawUI) {
        $Host.UI.RawUI.BufferSize = New-Object System.Management.Automation.Host.Size(200, 5000)
        $Host.UI.RawUI.WindowSize = New-Object System.Management.Automation.Host.Size(200, 50)
    }
}
catch {
    Write-Warning "Could not set buffer size: $($_.Exception.Message)"
}

# Set maximum history count
Set-PSReadLineOption -MaximumHistoryCount 10000

# Configure output preferences for better visibility
$OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['Out-File:Encoding'] = 'UTF8'
$PSDefaultParameterValues['*:Encoding'] = 'UTF8'

# Disable paging for ALL dotnet commands
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"
$env:DOTNET_NOLOGO = "true"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "true"

#endregion

#region Enhanced Output Capture Functions

function Invoke-WithFullOutput {
    <#
    .SYNOPSIS
        Execute any command with full output capture, no truncation

    .PARAMETER Command
        The command to execute

    .PARAMETER Arguments
        Arguments for the command

    .PARAMETER SaveLog
        Save output to timestamped log file

    .PARAMETER LogPrefix
        Prefix for log file names (e.g., 'build', 'test', 'task')
    #>
    [CmdletBinding()]
    param(
        [string]$Command,
        [string[]]$Arguments = @(),
        [switch]$SaveLog,
        [string]$LogPrefix = "output"
    )

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $logFile = "logs\$LogPrefix-$timestamp.log"

    # Ensure logs directory exists
    if (-not (Test-Path "logs")) {
        New-Item -ItemType Directory -Path "logs" -Force | Out-Null
    }

    Write-Information "🚀 Executing: $Command $($Arguments -join ' ')" -InformationAction Continue

    if ($SaveLog) {
        Write-Information "💾 Full output will be saved to: $logFile" -InformationAction Continue
    }

    try {
        $startTime = Get-Date

        # Use Start-Process for complete output capture
        $processInfo = @{
            FilePath               = $Command
            ArgumentList           = $Arguments
            RedirectStandardOutput = $true
            RedirectStandardError  = $true
            UseShellExecute        = $false
            CreateNoWindow         = $true
        }

        $process = Start-Process @processInfo -PassThru

        # Read output in real-time
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()

        $process.WaitForExit()
        $exitCode = $process.ExitCode

        $endTime = Get-Date
        $duration = $endTime - $startTime

        # Combine all output
        $fullOutput = @"
=== BUSBUDDY COMMAND OUTPUT ===
Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Command: $Command $($Arguments -join ' ')
Duration: $($duration.TotalSeconds) seconds
Exit Code: $exitCode

=== STANDARD OUTPUT ===
$stdout

=== STANDARD ERROR ===
$stderr

=== EXECUTION SUMMARY ===
"@

        # Save to file if requested
        if ($SaveLog) {
            $fullOutput | Out-File -FilePath $logFile -Encoding UTF8 -Width 1000
            Write-Information "✅ Complete output saved to: $logFile" -InformationAction Continue
        }

        # Display output with proper formatting
        if ($stdout) {
            Write-Output $stdout
        }

        if ($stderr) {
            Write-Warning $stderr
        }

        # Parse and display errors prominently
        $errorLines = ($stdout + $stderr) -split "`n" | Where-Object {
            $_ -match "error|Error|ERROR|\s+CS\d+|\s+MSB\d+|Failed|FAILED|Exception"
        }

        if ($errorLines) {
            Write-Warning "`n❌ ERRORS/ISSUES FOUND:"
            Write-Warning ("=" * 60)
            $errorLines | ForEach-Object {
                Write-Error $_
            }
            Write-Warning ("=" * 60)
        }

        # Display summary
        Write-Information "`n📊 EXECUTION SUMMARY:" -InformationAction Continue
        Write-Information "   Duration: $($duration.TotalSeconds) seconds" -InformationAction Continue
        Write-Information "   Exit Code: $exitCode" -InformationAction Continue

        if ($exitCode -eq 0) {
            Write-Information "   Status: SUCCESS ✅" -InformationAction Continue
        }
        else {
            Write-Error "   Status: FAILED ❌"
        }

        return @{
            ExitCode       = $exitCode
            Duration       = $duration
            ErrorLines     = $errorLines
            LogFile        = if ($SaveLog) { $logFile } else { $null }
            FullOutput     = $fullOutput
            StandardOutput = $stdout
            StandardError  = $stderr
        }

    }
    catch {
        Write-Error "Failed to execute command: $($_.Exception.Message)"
        return $null
    }
}
