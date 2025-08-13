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
        [int]$DebounceSeconds = 2
    )
    if ($script:BusBuddyTestWatchHandle) { Write-Warning 'Test watch already running.'; return }
    $root = Get-Location
    Write-Information "🔄 Watching $root for changes (suite=$TestSuite, debounce=${DebounceSeconds}s)" -InformationAction Continue
    $watcher = New-Object System.IO.FileSystemWatcher -Property @{ Path = $root; Filter = '*.*'; IncludeSubdirectories = $true; EnableRaisingEvents = $true }
    $script:BusBuddyTestWatchLastRun = Get-Date 0
    $action = {
        $ext = [IO.Path]::GetExtension($Event.SourceEventArgs.FullPath)
        if ($ext -notin '.cs','.xaml') { return }
        $now = Get-Date
        if (($now - $script:BusBuddyTestWatchLastRun).TotalSeconds -lt $using:DebounceSeconds) { return }
        $script:BusBuddyTestWatchLastRun = $now
        Write-Information "🧪 Change detected: $($Event.SourceEventArgs.Name) -> running $using:TestSuite tests" -InformationAction Continue
        try { Get-BusBuddyTestOutput -TestSuite $using:TestSuite -SaveToFile | Out-Null } catch { Write-Warning "Test watch run failed: $($_.Exception.Message)" }
    }
    $handler = Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $action
    $script:BusBuddyTestWatchHandle = @{ Watcher = $watcher; Handler = $handler }
    Write-Information 'Press Ctrl+C or run Stop-BusBuddyTestWatchAdvanced to stop.' -InformationAction Continue
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
