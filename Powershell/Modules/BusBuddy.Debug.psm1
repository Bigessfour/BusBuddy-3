# BusBuddy.Debug.psm1
# Debug helpers for BusBuddy
# Microsoft PowerShell Module Guidelines compliant

function Invoke-BusBuddyDebugSession {
    [CmdletBinding()]
    param()
    try {
        Write-Information "Starting BusBuddy debug session..." -InformationAction Continue
        bb-debug-start
        Write-Output "Debug session started."
    } catch {
        Write-Error "Debug session failed: $($_.Exception.Message)" -Category OperationStopped
    }
}

Export-ModuleMember -Function Invoke-BusBuddyDebugSession
