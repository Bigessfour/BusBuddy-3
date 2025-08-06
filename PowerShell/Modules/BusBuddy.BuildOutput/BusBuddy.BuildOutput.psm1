# Enhanced Build Commands with No Truncation
# Part of BusBuddy PowerShell Module

#region Enhanced Build Functions

function Get-BusBuddyBuildOutput {
    <#
    .SYNOPSIS
        Get complete build output without truncation

    .DESCRIPTION
        Captures full dotnet build output to both console and file, preventing truncation issues

    .PARAMETER ProjectPath
        Path to solution or project file

    .PARAMETER Configuration
        Build configuration (Debug/Release)

    .PARAMETER SaveToFile
        Save complete output to timestamped file

    .EXAMPLE
        Get-BusBuddyBuildOutput -SaveToFile
    #>
    [CmdletBinding()]
    param(
        [string]$ProjectPath = "BusBuddy.sln",
        [string]$Configuration = "Debug",
        [switch]$SaveToFile,
        [switch]$ErrorsOnly
    )

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $outputFile = "logs\build-output-$timestamp.log"

    # Ensure logs directory exists
    if (-not (Test-Path "logs")) {
        New-Item -ItemType Directory -Path "logs" -Force | Out-Null
    }

    Write-Host "🏗️ Building $ProjectPath..." -ForegroundColor Cyan
    Write-Host "📝 Configuration: $Configuration" -ForegroundColor Gray

    if ($SaveToFile) {
        Write-Host "💾 Full output will be saved to: $outputFile" -ForegroundColor Yellow
    }

    # Configure environment for maximum output
    $env:DOTNET_CLI_UI_LANGUAGE = "en-US"
    $env:DOTNET_NOLOGO = "false"  # We want full output

    try {
        # Capture all output streams with detailed verbosity
        $verbosity = if ($ErrorsOnly) { "quiet" } else { "detailed" }

        $startTime = Get-Date

        # Use Start-Process for complete output capture
        $processInfo = @{
            FilePath = "dotnet"
            ArgumentList = @("build", $ProjectPath, "--configuration", $Configuration, "--verbosity", $verbosity, "--no-restore")
            RedirectStandardOutput = $true
            RedirectStandardError = $true
            UseShellExecute = $false
            CreateNoWindow = $true
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
=== BUSBUDDY BUILD LOG ===
Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Project: $ProjectPath
Configuration: $Configuration
Duration: $($duration.TotalSeconds) seconds
Exit Code: $exitCode

=== STANDARD OUTPUT ===
$stdout

=== STANDARD ERROR ===
$stderr

=== BUILD SUMMARY ===
"@

        # Save to file if requested
        if ($SaveToFile) {
            $fullOutput | Out-File -FilePath $outputFile -Encoding UTF8 -Width 500
            Write-Host "✅ Complete build log saved to: $outputFile" -ForegroundColor Green
        }

        # Parse and display errors prominently
        $errorLines = ($stdout + $stderr) -split "`n" | Where-Object { $_ -match "error|Error|ERROR|\s+CS\d+|\s+MSB\d+" }

        if ($errorLines) {
            Write-Host "`n❌ BUILD ERRORS FOUND:" -ForegroundColor Red
            Write-Host "=" * 50 -ForegroundColor Red
            $errorLines | ForEach-Object {
                Write-Host $_ -ForegroundColor Red
            }
            Write-Host "=" * 50 -ForegroundColor Red

            if ($SaveToFile) {
                Write-Host "🔍 Full details in: $outputFile" -ForegroundColor Yellow
            }
        } else {
            Write-Host "✅ No errors found!" -ForegroundColor Green
        }

        # Display summary
        Write-Host "`n📊 BUILD SUMMARY:" -ForegroundColor Cyan
        Write-Host "   Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Gray
        Write-Host "   Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } else { "Red" })

        if ($exitCode -eq 0) {
            Write-Host "   Status: SUCCESS ✅" -ForegroundColor Green
        } else {
            Write-Host "   Status: FAILED ❌" -ForegroundColor Red
        }

        return @{
            ExitCode = $exitCode
            Duration = $duration
            ErrorLines = $errorLines
            OutputFile = if ($SaveToFile) { $outputFile } else { $null }
            FullOutput = $fullOutput
        }

    } catch {
        Write-Error "Failed to execute build: $($_.Exception.Message)"
        return $null
    }
}

function Start-BusBuddyBuildFull {
    <#
    .SYNOPSIS
        Enhanced bb-build with complete output capture
    #>
    Get-BusBuddyBuildOutput -SaveToFile
}

function Get-BusBuddyBuildErrors {
    <#
    .SYNOPSIS
        Get only build errors without full output
    #>
    Get-BusBuddyBuildOutput -ErrorsOnly
}

function Show-BusBuddyBuildLog {
    <#
    .SYNOPSIS
        Show the most recent build log
    #>
    $latestLog = Get-ChildItem "logs\build-output-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if ($latestLog) {
        Write-Host "📄 Most recent build log: $($latestLog.Name)" -ForegroundColor Cyan
        Write-Host "📅 Created: $($latestLog.LastWriteTime)" -ForegroundColor Gray
        Write-Host ""
        Get-Content $latestLog.FullName
    } else {
        Write-Host "No build logs found. Run Start-BusBuddyBuildFull first." -ForegroundColor Yellow
    }
}

#endregion

# Export functions
Export-ModuleMember -Function Get-BusBuddyBuildOutput, Start-BusBuddyBuildFull, Get-BusBuddyBuildErrors, Show-BusBuddyBuildLog
