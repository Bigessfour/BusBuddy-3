# BusBuddy Run Diagnostics Script
# Clean, readable diagnostic output for bb-run issues

param(
    [switch]$VerboseOutput
)

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-Host "=" * 60 -ForegroundColor DarkGray
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "=" * 60 -ForegroundColor DarkGray
    Write-Host ""
}

function Write-Step {
    param([string]$Message, [int]$Step)
    Write-Host "[$Step] " -ForegroundColor Yellow -NoNewline
    Write-Host $Message -ForegroundColor White
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ " -ForegroundColor Green -NoNewline
    Write-Host $Message -ForegroundColor Green
}

function Write-DiagWarning {
    param([string]$Message)
    Write-Host "⚠️  " -ForegroundColor Yellow -NoNewline
    Write-Host $Message -ForegroundColor Yellow
}

function Write-DiagError {
    param([string]$Message)
    Write-Host "❌ " -ForegroundColor Red -NoNewline
    Write-Host $Message -ForegroundColor Red
}

# Main diagnostic process
try {
    Write-Header "🔍 BusBuddy Run Diagnostics"
    Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
    Write-Host ""

    # Step 1: Load Bus Buddy Profile
    Write-Step "Loading Bus Buddy Profile..." 1
    try {
        if (Test-Path ".\load-bus-buddy-profile.ps1") {
            . ".\load-bus-buddy-profile.ps1" -Quiet
            Write-Success "Profile loaded successfully"
        }
        else {
            Write-DiagWarning "Profile script not found at .\load-bus-buddy-profile.ps1"
        }
    }
    catch {
        Write-DiagError "Failed to load profile: $($_.Exception.Message)"
    }

    Write-Host ""

    # Step 2: Create logs directory
    Write-Step "Preparing log directory..." 2
    if (-not (Test-Path "logs")) {
        New-Item -ItemType Directory -Path "logs" -Force | Out-Null
        Write-Success "Created logs directory"
    }
    else {
        Write-Success "Logs directory exists"
    }

    Write-Host ""

    # Step 3: Environment validation
    Write-Step "Validating .NET environment..." 3
    $dotnetAvailable = $false
    try {
        $dotnetVersion = dotnet --version 2>$null
        if ($dotnetVersion) {
            Write-Success ".NET CLI available (version: $dotnetVersion)"
            $dotnetAvailable = $true
        }
    }
    catch {
        Write-DiagWarning ".NET CLI not found in PATH"
    }

    Write-Host ""

    # Step 4: Run bb-run with output capture
    Write-Step "Running bb-run with output capture..." 4
    $logFile = "logs\bb-run-diagnostic-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

    try {
        if (Get-Command bb-run -ErrorAction SilentlyContinue) {
            Write-Host "   Executing bb-run command..." -ForegroundColor Gray
            if ($dotnetAvailable) {
                $output = bb-run 2>&1 | Tee-Object -FilePath $logFile
                Write-Success "bb-run executed successfully"
            }
            else {
                Write-DiagWarning "Skipping bb-run due to missing .NET CLI"
                "ERROR: .NET CLI not available in PATH" | Out-File -FilePath $logFile
            }
        }
        else {
            Write-DiagWarning "bb-run command not available"
            if ($dotnetAvailable) {
                Write-Host "   Trying direct dotnet run..." -ForegroundColor Gray
                $output = dotnet run --project "BusBuddy.WPF\BusBuddy.WPF.csproj" 2>&1 | Tee-Object -FilePath $logFile
                Write-Success "Direct dotnet run executed"
            }
            else {
                Write-DiagError "Cannot run application - .NET CLI not available"
                "ERROR: .NET CLI not available in PATH" | Out-File -FilePath $logFile
            }
        }
    }
    catch {
        Write-DiagError "Error during execution: $($_.Exception.Message)"
        $_.Exception.Message | Tee-Object -FilePath $logFile
    }

    Write-Host ""

    # Step 5: Analyze results
    Write-Step "Analyzing results..." 5
    if (Test-Path $logFile) {
        Write-Success "Log file created: $logFile"

        $content = Get-Content $logFile -ErrorAction SilentlyContinue
        if ($content) {
            $warnings = $content | Where-Object { $_ -match 'warning|CA\d+' }
            $errors = $content | Where-Object { $_ -match 'error|Error|ERROR' }

            if ($warnings -or $errors) {
                Write-Host ""
                Write-Host "   📋 Issues Found:" -ForegroundColor Yellow

                if ($errors) {
                    Write-Host "      🔴 ERRORS ($($errors.Count)):" -ForegroundColor Red
                    $errors | ForEach-Object { Write-Host "         • $_" -ForegroundColor Red }
                }

                if ($warnings) {
                    Write-Host "      🟡 WARNINGS ($($warnings.Count)):" -ForegroundColor Yellow
                    $warnings | ForEach-Object { Write-Host "         • $_" -ForegroundColor Yellow }
                }
            }
            else {
                Write-Success "No warnings or errors detected in output"
            }
        }
        else {
            Write-DiagWarning "Log file is empty"
        }
    }
    else {
        Write-DiagError "No log file was created"
    }

    Write-Host ""

    # Step 6: Process check
    Write-Step "Checking for running processes..." 6
    $recentTime = (Get-Date).AddMinutes(-2)
    $processes = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -like '*BusBuddy*' -or
        ($_.ProcessName -eq 'dotnet' -and $_.StartTime -gt $recentTime)
    }

    if ($processes) {
        Write-Success "Application processes found:"
        Write-Host ""
        $processes | Select-Object ProcessName, Id, @{N = 'Runtime'; E = { ((Get-Date) - $_.StartTime).ToString('mm\:ss') } } |
        Format-Table -AutoSize | Out-String | ForEach-Object { Write-Host "      $_" -ForegroundColor Green }
    }
    else {
        Write-DiagWarning "No recent BusBuddy or dotnet processes detected"
    }

    Write-Host ""
    Write-Header "🎯 Diagnostic Summary"

    # Summary based on findings
    if ($errors -and $errors.Count -gt 0) {
        Write-DiagError "Application failed to start due to $($errors.Count) error(s)"
        Write-Host "   → Check the errors above and fix before retrying" -ForegroundColor Gray
    }
    elseif (-not $dotnetAvailable) {
        Write-DiagError ".NET CLI not available in PATH"
        Write-Host "   → Install .NET SDK or add to PATH environment variable" -ForegroundColor Gray
        Write-Host "   → Download from: https://dotnet.microsoft.com/download" -ForegroundColor Gray
    }
    elseif ($warnings -and $warnings.Count -gt 0) {
        Write-DiagWarning "Application may have started with $($warnings.Count) warning(s)"
        Write-Host "   → Warnings should be addressed but may not prevent startup" -ForegroundColor Gray
    }
    elseif ($processes) {
        Write-Success "Application appears to be running successfully"
        Write-Host "   → Check your desktop for the BusBuddy application window" -ForegroundColor Gray
    }
    else {
        Write-DiagWarning "Application status unclear - no errors but no processes detected"
        Write-Host "   → Application may have started and closed, or be running silently" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "📄 Full log available at: $logFile" -ForegroundColor Cyan
    Write-Host ""

}
catch {
    Write-DiagError "Diagnostic script failed: $($_.Exception.Message)"
    exit 1
}
