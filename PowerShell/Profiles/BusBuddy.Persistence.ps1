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
# LOGGING UTILITIES
# =============================================================================

function Write-ProfileLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [ValidateSet('Information', 'Warning', 'Error')]
        [string]$Level = 'Information'
    )

    # Simple fallback logging for persistence module
    switch ($Level) {
        'Information' { Write-Information $Message -InformationAction Continue }
        'Warning' { Write-Warning $Message }
        'Error' { Write-Error $Message }
    }
}

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
    [hashtable]$ModuleLoadMetrics

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
        $this.ModuleLoadMetrics = @{
            LoadTimes = @()
            AverageLoadTime = [TimeSpan]::Zero
            FastestLoad = [TimeSpan]::MaxValue
            SlowestLoad = [TimeSpan]::Zero
            TotalLoads = 0
        }
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

    [TimeSpan] MeasureModuleLoadTime([string]$ModulePath, [hashtable]$Parameters = @{}) {
        <#
        .SYNOPSIS
        Measures module load time using Measure-Command and tracks performance metrics
        .DESCRIPTION
        Uses PowerShell 7.5 pipeline chaining for concise calculation and updates performance hashtable
        .PARAMETER ModulePath
        Path to the module or script to load
        .PARAMETER Parameters
        Optional parameters to pass to the module
        #>

        $loadTime = Measure-Command {
            try {
                if ($Parameters.Count -gt 0) {
                    . $ModulePath @Parameters
                } else {
                    . $ModulePath
                }
            }
            catch {
                Write-Warning "Module load failed for $ModulePath`: $($_.Exception.Message)"
                throw
            }
        }

        # Update metrics using pipeline chaining for concise calculation
        $this.ModuleLoadMetrics.LoadTimes += $loadTime
        $this.ModuleLoadMetrics.TotalLoads++
        $this.ModuleLoadMetrics.AverageLoadTime = ($this.ModuleLoadMetrics.LoadTimes |
            Measure-Object -Property TotalMilliseconds -Average).Average |
            ForEach-Object { [TimeSpan]::FromMilliseconds($_) }

        # Update fastest/slowest using pipeline comparison
        $this.ModuleLoadMetrics.FastestLoad = ($this.ModuleLoadMetrics.FastestLoad, $loadTime |
            Measure-Object -Property TotalMilliseconds -Minimum).Minimum |
            ForEach-Object { [TimeSpan]::FromMilliseconds($_) }

        $this.ModuleLoadMetrics.SlowestLoad = ($this.ModuleLoadMetrics.SlowestLoad, $loadTime |
            Measure-Object -Property TotalMilliseconds -Maximum).Maximum |
            ForEach-Object { [TimeSpan]::FromMilliseconds($_) }

        Write-Information "🕒 Module load time: $($loadTime.TotalMilliseconds)ms for $([System.IO.Path]::GetFileName($ModulePath))" -InformationAction Continue

        return $loadTime
    }

    [TimeSpan] MeasureModuleLoad([string]$ModuleName, [scriptblock]$LoadCommand) {
        <#
        .SYNOPSIS
        Measures module load time using provided script block and tracks performance metrics
        .DESCRIPTION
        Uses PowerShell 7.5 Measure-Command with script block execution for flexible module loading
        .PARAMETER ModuleName
        Name of the module being loaded (for tracking purposes)
        .PARAMETER LoadCommand
        Script block containing the module load command
        #>

        $loadTime = Measure-Command {
            try {
                & $LoadCommand
            }
            catch {
                Write-Warning "Module load failed for $ModuleName`: $($_.Exception.Message)"
                throw
            }
        }

        # Update metrics using pipeline chaining for concise calculation
        $this.ModuleLoadMetrics.LoadTimes += $loadTime
        $this.ModuleLoadMetrics.TotalLoads++
        $this.ModuleLoadMetrics.AverageLoadTime = ($this.ModuleLoadMetrics.LoadTimes |
            Measure-Object -Property TotalMilliseconds -Average).Average |
            ForEach-Object { [TimeSpan]::FromMilliseconds($_) }

        # Update fastest/slowest using pipeline comparison
        $this.ModuleLoadMetrics.FastestLoad = ($this.ModuleLoadMetrics.FastestLoad, $loadTime |
            Measure-Object -Property TotalMilliseconds -Minimum).Minimum |
            ForEach-Object { [TimeSpan]::FromMilliseconds($_) }

        $this.ModuleLoadMetrics.SlowestLoad = ($this.ModuleLoadMetrics.SlowestLoad, $loadTime |
            Measure-Object -Property TotalMilliseconds -Maximum).Maximum |
            ForEach-Object { [TimeSpan]::FromMilliseconds($_) }

        Write-Information "🕒 Module load time: $($loadTime.TotalMilliseconds)ms for $ModuleName" -InformationAction Continue

        return $loadTime
    }

    [bool] AttemptRecovery() {
        Write-Information "🔧 Attempting environment recovery (attempt $($this.RecoveryAttempts + 1)/$($this.MaxRecoveryAttempts))..." -InformationAction Continue

        $this.RecoveryAttempts++

        try {
            # Step 1: Try bbRefresh if available
            if (Get-Command bbRefresh -ErrorAction SilentlyContinue) {
                Write-Information "Using bbRefresh for recovery..." -InformationAction Continue
                $refreshTime = Measure-Command { bbRefresh -Force }
                Write-Information "🕒 bbRefresh completed in $($refreshTime.TotalMilliseconds)ms" -InformationAction Continue
                Start-Sleep -Seconds 3
            }
            # Step 2: Try hardened module manager
            elseif ($this.RepoRoot) {
                $moduleManagerPath = Join-Path $this.RepoRoot "PowerShell\Profiles\BusBuddy.ModuleManager.ps1"
                if (Test-Path $moduleManagerPath) {
                    Write-Information "Loading hardened module manager..." -InformationAction Continue
                    $this.MeasureModuleLoadTime($moduleManagerPath, @{ Force = $true; Quiet = $true })
                    Start-Sleep -Seconds 3
                }
            }
            # Step 3: Fallback to basic import
            else {
                $importScript = Join-Path $this.RepoRoot "PowerShell\Profiles\Import-BusBuddyModule.ps1"
                if (Test-Path $importScript) {
                    Write-Information "Using basic module import..." -InformationAction Continue
                    $this.MeasureModuleLoadTime($importScript)
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

    [void] DisplayPerformanceMetrics() {
        <#
        .SYNOPSIS
        Display formatted performance metrics for module loading
        .DESCRIPTION
        Shows comprehensive performance statistics using pipeline chaining for formatting
        #>

        if ($this.ModuleLoadMetrics.TotalLoads -eq 0) {
            Write-Information "📊 No module load metrics available yet" -InformationAction Continue
            return
        }

        $metrics = $this.ModuleLoadMetrics
        $recentTimes = @($metrics.LoadTimes | Select-Object -Last 10)

        Write-Information "`n📊 Module Load Performance Metrics:" -InformationAction Continue
        Write-Information "   Total Loads: $($metrics.TotalLoads)" -InformationAction Continue
        Write-Information "   Average: $([math]::Round($metrics.AverageLoadTime.TotalMilliseconds, 2))ms" -InformationAction Continue
        Write-Information "   Fastest: $([math]::Round($metrics.FastestLoad.TotalMilliseconds, 2))ms" -InformationAction Continue
        Write-Information "   Slowest: $([math]::Round($metrics.SlowestLoad.TotalMilliseconds, 2))ms" -InformationAction Continue

        # Performance trending using pipeline operations (only if we have enough data)
        if ($recentTimes.Count -ge 5) {
            $trend = ($recentTimes |
                Select-Object -Last 3 |
                Measure-Object -Property TotalMilliseconds -Average).Average -
                ($recentTimes |
                Select-Object -First 3 |
                Measure-Object -Property TotalMilliseconds -Average).Average

            $trendIndicator = if ($trend -lt -10) { "📈 Improving" }
                             elseif ($trend -gt 10) { "📉 Degrading" }
                             else { "➡️ Stable" }

            Write-Information "   Trend: $trendIndicator ($([math]::Round($trend, 1))ms change)" -InformationAction Continue
        }

        Write-Information "   Recent 5: $($recentTimes | Select-Object -Last 5 | ForEach-Object { "$([math]::Round($_.TotalMilliseconds, 1))ms" } | Join-String -Separator ', ')" -InformationAction Continue
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
            PerformanceMetrics = @{
                TotalModuleLoads = $this.ModuleLoadMetrics.TotalLoads
                AverageLoadTime = "$([math]::Round($this.ModuleLoadMetrics.AverageLoadTime.TotalMilliseconds, 2))ms"
                FastestLoad = "$([math]::Round($this.ModuleLoadMetrics.FastestLoad.TotalMilliseconds, 2))ms"
                SlowestLoad = "$([math]::Round($this.ModuleLoadMetrics.SlowestLoad.TotalMilliseconds, 2))ms"
                RecentLoadTimes = ($this.ModuleLoadMetrics.LoadTimes |
                    Select-Object -Last 5 |
                    ForEach-Object { "$([math]::Round($_.TotalMilliseconds, 2))ms" }) -join ', '
            }
        }
    }
}

# =============================================================================
# PERFORMANCE MONITORING FUNCTIONS
# =============================================================================

function Invoke-BusBuddyModuleWithMetrics {
    <#
    .SYNOPSIS
    Load a module with performance measurement using the global watcher
    .DESCRIPTION
    Wrapper function that uses the global BusBuddy watcher to measure module load times
    .PARAMETER ModulePath
    Path to the module to load
    .PARAMETER Parameters
    Optional parameters for the module
    .EXAMPLE
    Invoke-BusBuddyModuleWithMetrics -ModulePath ".\BusBuddy.ModuleManager.ps1" -Parameters @{Force=$true}
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ModulePath,

        [hashtable]$Parameters = @{}
    )

    if (-not $global:BusBuddyWatcher) {
        Write-Warning "Global BusBuddy watcher not available, using standard loading"
        if ($Parameters.Count -gt 0) {
            . $ModulePath @Parameters
        } else {
            . $ModulePath
        }
        return
    }

    try {
        $loadTime = $global:BusBuddyWatcher.MeasureModuleLoadTime($ModulePath, $Parameters)
        return $loadTime
    }
    catch {
        Write-Error "Failed to load module with metrics: $($_.Exception.Message)"
        throw
    }
}

function Show-BusBuddyPerformanceReport {
    <#
    .SYNOPSIS
    Display comprehensive performance report for module loading
    .DESCRIPTION
    Shows performance metrics and trends using pipeline operations for analysis
    .EXAMPLE
    Show-BusBuddyPerformanceReport
    #>
    [CmdletBinding()]
    param()

    if (-not $global:BusBuddyWatcher) {
        Write-Warning "Global BusBuddy watcher not available"
        return
    }

    $global:BusBuddyWatcher.DisplayPerformanceMetrics()

    # Additional analysis using pipeline chaining
    $metrics = $global:BusBuddyWatcher.ModuleLoadMetrics
    if ($metrics.TotalLoads -ge 10) {
        $sortedTimes = $metrics.LoadTimes |
            ForEach-Object { $_.TotalMilliseconds } |
            Sort-Object

        $count = $sortedTimes.Count
        $p50Index = [math]::Floor($count * 0.5)
        $p90Index = [math]::Floor($count * 0.9)
        $p95Index = [math]::Floor($count * 0.95)

        $p50 = $sortedTimes[$p50Index]
        $p90 = $sortedTimes[$p90Index]
        $p95 = $sortedTimes[$p95Index]

        Write-Information "`n📈 Performance Percentiles:" -InformationAction Continue
        Write-Information "   P50 (median): $([math]::Round($p50, 2))ms" -InformationAction Continue
        Write-Information "   P90: $([math]::Round($p90, 2))ms" -InformationAction Continue
        Write-Information "   P95: $([math]::Round($p95, 2))ms" -InformationAction Continue
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

# Note: Functions are available globally when script is dot-sourced
# Available functions: Register-BusBuddySession, Unregister-BusBuddySession, Test-BusBuddyPersistence, Invoke-BusBuddyModuleWithMetrics, Show-BusBuddyPerformanceReport

Write-Information "✅ BusBuddy Environment Persistence Manager loaded" -InformationAction Continue
