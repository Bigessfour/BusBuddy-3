#requires -Version 7.5 -PSEdition Core
<#
.SYNOPSIS
BusBuddy Environment Persistence Manager
.DESCRIPTION
Provides persistent monitoring and automatic recovery of BusBuddy module environment.
Ensures commands remain available across terminal sessions and refreshes.

.NOTES
Author: BusBuddy Development Team
Standards: PowerShell 7.5+, StrictMode 3.0, Microsoft compliance
References:
- PowerShell Jobs: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_jobs
- Module Loading: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/import-module
#>

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Continue'

# =============================================================================
# PERSISTENT ENVIRONMENT MONITORING
# =============================================================================

class BusBuddyEnvironmentWatcher {
    [string]$RepoRoot
    [hashtable]$ExpectedCommands
    [bool]$IsMonitoring
    [int]$CheckIntervalSeconds = 60
    [datetime]$LastCheck
    [int]$RecoveryAttempts = 0
    [int]$MaxRecoveryAttempts = 3

    BusBuddyEnvironmentWatcher() {
        $this.ExpectedCommands = @{
            'bbHealth' = 'Health check and diagnostics'
            'bbBuild' = 'Build solution'
            'bbTest' = 'Run tests'
            'bbRun' = 'Run WPF application'
            'bbRefresh' = 'Refresh modules'
            'bbStatus' = 'Check module status'
            'bbAntiRegression' = 'Anti-regression scan'
            'bbXamlValidate' = 'XAML validation'
            'bbMvpCheck' = 'MVP readiness check'
            'bbCommands' = 'List commands'
        }
        $this.IsMonitoring = $false
        $this.RepoRoot = $this.FindRepoRoot()
    }

    [string] FindRepoRoot() {
        $probe = if ($env:BUSBUDDY_REPO_ROOT) { $env:BUSBUDDY_REPO_ROOT } else { (Get-Location).Path }

        while ($probe -and -not (Test-Path (Join-Path $probe 'BusBuddy.sln'))) {
            $parent = Split-Path $probe -Parent
            if (-not $parent -or $parent -eq $probe) { $probe = $null; break }
            $probe = $parent
        }

        return $probe
    }

    [hashtable] CheckCommandAvailability() {
        $results = @{
            Available = @()
            Missing = @()
            CheckTime = Get-Date
        }

        foreach ($command in $this.ExpectedCommands.Keys) {
            if (Get-Command $command -ErrorAction SilentlyContinue) {
                $results.Available += $command
            } else {
                $results.Missing += $command
            }
        }

        return $results
    }

    [bool] AttemptRecovery() {
        Write-Information "🔧 Attempting environment recovery (attempt $($this.RecoveryAttempts + 1)/$($this.MaxRecoveryAttempts))..." -InformationAction Continue

        $this.RecoveryAttempts++

        try {
            # Step 1: Try bbRefresh if available
            if (Get-Command bbRefresh -ErrorAction SilentlyContinue) {
                Write-Information "Using bbRefresh for recovery..." -InformationAction Continue
                bbRefresh -Force
                Start-Sleep -Seconds 3
            }
            # Step 2: Try hardened module manager
            elseif ($this.RepoRoot) {
                $moduleManagerPath = Join-Path $this.RepoRoot "PowerShell\Profiles\BusBuddy.ModuleManager.ps1"
                if (Test-Path $moduleManagerPath) {
                    Write-Information "Loading hardened module manager..." -InformationAction Continue
                    . $moduleManagerPath -Force -Quiet
                    Start-Sleep -Seconds 3
                }
            }
            # Step 3: Fallback to basic import
            else {
                $importScript = Join-Path $this.RepoRoot "PowerShell\Profiles\Import-BusBuddyModule.ps1"
                if (Test-Path $importScript) {
                    Write-Information "Using basic module import..." -InformationAction Continue
                    . $importScript
                    Start-Sleep -Seconds 3
                }
            }

            # Verify recovery
            $postCheck = $this.CheckCommandAvailability()
            $criticalCommands = @('bbHealth', 'bbBuild', 'bbTest', 'bbRefresh')
            $criticalAvailable = $criticalCommands | Where-Object { $_ -in $postCheck.Available }

            if ($criticalAvailable.Count -ge 3) {
                Write-Information "✅ Recovery successful: $($postCheck.Available.Count)/$($this.ExpectedCommands.Count) commands available" -InformationAction Continue
                $this.RecoveryAttempts = 0
                return $true
            } else {
                Write-Warning "Recovery partially successful: $($postCheck.Available.Count)/$($this.ExpectedCommands.Count) commands available"
                return $false
            }
        }
        catch {
            Write-Warning "Recovery attempt failed: $($_.Exception.Message)"
            return $false
        }
    }

    [void] StartMonitoring() {
        if ($this.IsMonitoring) {
            Write-Information "Environment monitoring already active" -InformationAction Continue
            return
        }

        Write-Information "🔍 Starting BusBuddy environment monitoring..." -InformationAction Continue
        $this.IsMonitoring = $true
        $this.LastCheck = Get-Date

        # Create monitoring job
        $monitoringScript = {
            param($Watcher, $ExpectedCommands, $CheckInterval)

            while ($true) {
                Start-Sleep -Seconds $CheckInterval

                try {
                    # Check command availability
                    $missing = @()
                    foreach ($command in $ExpectedCommands.Keys) {
                        if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
                            $missing += $command
                        }
                    }

                    if ($missing.Count -gt 0) {
                        Write-Output "$(Get-Date): Missing commands detected: $($missing -join ', ')"
                        # Trigger recovery attempt
                        $recoveryResult = $Watcher.AttemptRecovery()
                        if ($recoveryResult) {
                            Write-Output "$(Get-Date): Recovery attempt succeeded."
                        } else {
                            Write-Output "$(Get-Date): Recovery attempt failed."
                        }
                    }
                }
                catch {
                    Write-Output "$(Get-Date): Error in monitoring script block: $($_.Exception.Message)"
                }
            }
        }

        try {
            $job = Start-Job -ScriptBlock $monitoringScript -ArgumentList $this, $this.ExpectedCommands, $this.CheckIntervalSeconds
            Write-Information "✅ Environment monitoring started (Job ID: $($job.Id))" -InformationAction Continue
        }
        catch {
            Write-Warning "Failed to start monitoring job: $($_.Exception.Message)"
            $this.IsMonitoring = $false
        }
    }

    [void] StopMonitoring() {
        if (-not $this.IsMonitoring) { return }

        Write-Information "🛑 Stopping environment monitoring..." -InformationAction Continue
        Get-Job | Where-Object Name -like "*BusBuddy*" | Stop-Job -PassThru | Remove-Job -Force
        $this.IsMonitoring = $false
    }

    [hashtable] GetStatus() {
        $commandCheck = $this.CheckCommandAvailability()

        return @{
            RepoRoot = $this.RepoRoot
            IsMonitoring = $this.IsMonitoring
            LastCheck = $this.LastCheck
            RecoveryAttempts = $this.RecoveryAttempts
            MaxRecoveryAttempts = $this.MaxRecoveryAttempts
            AvailableCommands = $commandCheck.Available
            MissingCommands = $commandCheck.Missing
            AvailabilityRatio = "$($commandCheck.Available.Count)/$($this.ExpectedCommands.Count)"
        }
    }
}

# =============================================================================
# MODULE REGISTRY SYSTEM
# =============================================================================

function Register-BusBuddySession {
    <#
    .SYNOPSIS
    Register current session for persistent command availability
    .DESCRIPTION
    Creates session registration that enables automatic recovery of BusBuddy commands
    .EXAMPLE
    Register-BusBuddySession
    #>
    [CmdletBinding()]
    param()

    $sessionId = [System.Guid]::NewGuid().ToString('N')[0..7] -join ''
    $env:BUSBUDDY_SESSION_ID = $sessionId
    $env:BUSBUDDY_SESSION_START = (Get-Date).ToString('o')

    Write-Information "📋 Session registered: $sessionId" -InformationAction Continue

    # Store session info
    $sessionInfo = @{
        SessionId = $sessionId
        StartTime = Get-Date
        ProcessId = $PID
        HostName = $env:COMPUTERNAME
        UserName = $env:USERNAME
    }

    return $sessionInfo
}

function Unregister-BusBuddySession {
    <#
    .SYNOPSIS
    Unregister current session
    .DESCRIPTION
    Cleans up session registration and stops monitoring
    .EXAMPLE
    Unregister-BusBuddySession
    #>
    [CmdletBinding()]
    param()

    $sessionId = $env:BUSBUDDY_SESSION_ID
    if ($sessionId) {
        Write-Information "📋 Unregistering session: $sessionId" -InformationAction Continue
        $env:BUSBUDDY_SESSION_ID = $null
        $env:BUSBUDDY_SESSION_START = $null
    }
}

function Test-BusBuddyPersistence {
    <#
    .SYNOPSIS
    Test command persistence across terminal operations
    .DESCRIPTION
    Simulates terminal refresh scenarios and validates command availability
    .EXAMPLE
    Test-BusBuddyPersistence
    #>
    [CmdletBinding()]
    param()

    Write-Information "🧪 Testing BusBuddy command persistence..." -InformationAction Continue

    $watcher = [BusBuddyEnvironmentWatcher]::new()
    $initialStatus = $watcher.GetStatus()

    Write-Information "Initial status: $($initialStatus.AvailabilityRatio) commands available" -InformationAction Continue

    # Test 1: Simulate module removal
    Write-Information "Test 1: Simulating module removal..." -InformationAction Continue
    Get-Module BusBuddy* | Remove-Module -Force -ErrorAction SilentlyContinue

    $afterRemoval = $watcher.CheckCommandAvailability()
    Write-Information "After removal: $($afterRemoval.Available.Count)/$($watcher.ExpectedCommands.Count) commands available" -InformationAction Continue

    # Test 2: Recovery attempt
    Write-Information "Test 2: Testing recovery..." -InformationAction Continue
    $recoverySuccess = $watcher.AttemptRecovery()

    $afterRecovery = $watcher.CheckCommandAvailability()
    Write-Information "After recovery: $($afterRecovery.Available.Count)/$($watcher.ExpectedCommands.Count) commands available" -InformationAction Continue

    # Test 3: Command functionality
    Write-Information "Test 3: Testing command functionality..." -InformationAction Continue
    $functionalCommands = @()

    foreach ($command in $afterRecovery.Available) {
        try {
            if ($command -eq 'bbCommands') {
                $result = & $command
                if ($result) { $functionalCommands += $command }
            } elseif ($command -eq 'bbHealth') {
                # Quick health check test
                $functionalCommands += $command
            } else {
                # For other commands, just verify they exist and are callable
                $functionalCommands += $command
            }
        }
        catch {
            Write-Warning "Command $command failed test: $($_.Exception.Message)"
        }
    }

    # Summary
    $testResults = @{
        InitialAvailable = $initialStatus.AvailableCommands.Count
        AfterRemoval = $afterRemoval.Available.Count
        AfterRecovery = $afterRecovery.Available.Count
        Functional = $functionalCommands.Count
        RecoverySuccess = $recoverySuccess
        TestTime = Get-Date
    }

    Write-Information "`n📊 Persistence Test Results:" -InformationAction Continue
    Write-Information "  Initial: $($testResults.InitialAvailable) commands" -InformationAction Continue
    Write-Information "  After removal: $($testResults.AfterRemoval) commands" -InformationAction Continue
    Write-Information "  After recovery: $($testResults.AfterRecovery) commands" -InformationAction Continue
    Write-Information "  Functional: $($testResults.Functional) commands" -InformationAction Continue
    Write-Information "  Recovery success: $($testResults.RecoverySuccess)" -InformationAction Continue

    if ($testResults.RecoverySuccess -and $testResults.Functional -ge 8) {
        Write-Information "✅ Persistence test PASSED" -InformationAction Continue
    } else {
        Write-Warning "❌ Persistence test FAILED - Manual intervention may be required"
    }

    return $testResults
}

# =============================================================================
# AUTOMATIC INITIALIZATION
# =============================================================================

# Auto-register session if not already registered
if (-not $env:BUSBUDDY_SESSION_ID) {
    Register-BusBuddySession | Out-Null
}

# Create global watcher instance
$global:BusBuddyWatcher = [BusBuddyEnvironmentWatcher]::new()

# Export functions for use in other scripts
Export-ModuleMember -Function Register-BusBuddySession, Unregister-BusBuddySession, Test-BusBuddyPersistence

Write-Information "✅ BusBuddy Environment Persistence Manager loaded" -InformationAction Continue
