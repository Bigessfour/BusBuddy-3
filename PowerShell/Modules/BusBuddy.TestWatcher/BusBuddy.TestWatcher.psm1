# BusBuddy.TestWatcher Module
# Provides debounced file change watching to trigger targeted test runs.

function Start-BusBuddyTestWatchAdvanced {
    <#
    .SYNOPSIS
        Start debounced test watch loop for a specific test suite.
    .DESCRIPTION
        Watches for changes to .cs and .xaml files under repo root and re-runs Get-BusBuddyTestOutput.
    #>
    [CmdletBinding()]
    param(
        [ValidateSet('All','Unit','Integration','Validation','Core','WPF')]
        [string]$TestSuite = 'Unit',
        [int]$DebounceSeconds = 2,
        [switch]$RetryOnFailure,
        [int]$MaxRetries = 2,
        [int]$RetryDelaySeconds = 2
    )
    if ($script:BusBuddyTestWatchHandle) { Write-Warning 'Test watch already running.'; return }
    $root = Get-Location
    Write-Information "🔄 Watching $root for changes (suite=$TestSuite, debounce=${DebounceSeconds}s)" -InformationAction Continue
    $watcher = New-Object System.IO.FileSystemWatcher -Property @{ Path = $root; Filter = '*.*'; IncludeSubdirectories = $true; EnableRaisingEvents = $true }
    $script:BusBuddyTestWatchLastRun = Get-Date 0
    $script:BusBuddyTestWatchIgnored = 0
    $script:BusBuddyTestWatchTriggered = 0
    $script:BusBuddyTestWatchFailures = 0
    $script:BusBuddyTestWatchRetries = 0
    # Correlation ID for this watcher session
    $script:BusBuddyTestWatchCorrelation = [guid]::NewGuid().ToString()
    $action = {
        $full = $Event.SourceEventArgs.FullPath
        $ext = [IO.Path]::GetExtension($full)
        if ($ext -notin '.cs','.xaml') {
            Write-Verbose ("Ignoring change due to unsupported extension: {0}" -f $ext)
            return
        }
        # Ignore build/test artifacts to avoid infinite loops (bin/obj/TestResults/.git/.vs)
        if ($full -match "(?i)\\bin\\|\\obj\\|\\TestResults\\|\\\.git\\|\\\.vs\\") {
            $script:BusBuddyTestWatchIgnored++
            Write-Verbose ("Ignoring change in excluded path: {0}" -f $full)
            return
        }
        $now = Get-Date
        $elapsed = ($now - $script:BusBuddyTestWatchLastRun).TotalSeconds
        if ($elapsed -lt $using:DebounceSeconds) {
            $remaining = [math]::Max([int]([math]::Ceiling($using:DebounceSeconds - $elapsed)), 0)
            Write-Verbose ("Debounce active — event suppressed. Time remaining: {0}s" -f $remaining)
            return
        }
        $script:BusBuddyTestWatchLastRun = $now
        $script:BusBuddyTestWatchTriggered++
        Write-Information "🧪 [$script:BusBuddyTestWatchCorrelation] Change detected: $($Event.SourceEventArgs.Name) -> running $using:TestSuite tests (trigger #$script:BusBuddyTestWatchTriggered, ignored so far=$script:BusBuddyTestWatchIgnored)" -InformationAction Continue
        try {
            Get-BusBuddyTestOutput -TestSuite $using:TestSuite -SaveToFile | Out-Null
        } catch {
            $script:BusBuddyTestWatchFailures++
            Write-Warning "[$script:BusBuddyTestWatchCorrelation] Test watch run failed (#$script:BusBuddyTestWatchFailures): $($_.Exception.Message)"
            if ($using:RetryOnFailure) {
                $attempt = 0
                $delay = [int]$using:RetryDelaySeconds
                while ($attempt -lt [int]$using:MaxRetries) {
                    $attempt++
                    $script:BusBuddyTestWatchRetries++
                    Write-Information ("🔁 [$script:BusBuddyTestWatchCorrelation] Retry trigger #{0} (attempt {1}/{2}) after failure — waiting {3}s..." -f $script:BusBuddyTestWatchRetries, $attempt, [int]$using:MaxRetries, $delay) -InformationAction Continue
                    Start-Sleep -Seconds $delay
                    try {
                        Get-BusBuddyTestOutput -TestSuite $using:TestSuite -SaveToFile | Out-Null
                        Write-Information "✅ [$script:BusBuddyTestWatchCorrelation] Retry succeeded on attempt $attempt" -InformationAction Continue
                        break
                    } catch {
                        Write-Warning ("[$script:BusBuddyTestWatchCorrelation] Retry attempt {0} failed: {1}" -f $attempt, $_.Exception.Message)
                        $delay = [math]::Min($delay * 2, 30)
                    }
                }
            }
        }
    }
    $handler = Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $action
    $script:BusBuddyTestWatchHandle = @{ Watcher = $watcher; Handler = $handler }
    Write-Information "Press Ctrl+C or run Stop-BusBuddyTestWatchAdvanced to stop. CorrelationId=$script:BusBuddyTestWatchCorrelation" -InformationAction Continue
}

function Stop-BusBuddyTestWatchAdvanced {
    [CmdletBinding()] param()
    if (-not $script:BusBuddyTestWatchHandle) { Write-Warning 'No active test watch.'; return }
    try {
        $script:BusBuddyTestWatchHandle.Handler | Unregister-Event -ErrorAction SilentlyContinue
        $script:BusBuddyTestWatchHandle.Watcher.EnableRaisingEvents = $false
        $script:BusBuddyTestWatchHandle.Watcher.Dispose()
        Write-Information '🛑 Test watch stopped.' -InformationAction Continue
    } finally { Remove-Variable BusBuddyTestWatchHandle -Scope Script -ErrorAction SilentlyContinue }
}

Export-ModuleMember -Function Start-BusBuddyTestWatchAdvanced,Stop-BusBuddyTestWatchAdvanced
