# BusBuddy.Workflow.psm1
# Workflow orchestration for BusBuddy
# Microsoft PowerShell Module Guidelines compliant

function Start-BusBuddyDevSession {
    [CmdletBinding()]
    param()
    try {
        Write-Information "Starting BusBuddy development session..." -InformationAction Continue
        Invoke-BusBuddyBuild
        Invoke-BusBuddyQualityCheck
        Write-Output "Development session started."
    } catch {
        Write-Error "Dev session failed: $($_.Exception.Message)" -Category OperationStopped
    }
}

Export-ModuleMember -Function Start-BusBuddyDevSession
