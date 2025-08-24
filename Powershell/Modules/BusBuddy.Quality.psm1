# BusBuddy.Quality.psm1
# Quality checks and formatting for BusBuddy
# Microsoft PowerShell Module Guidelines compliant

function Invoke-BusBuddyQualityCheck {
    [CmdletBinding()]
    param()
    try {
        Write-Information "Running Trunk quality checks..." -InformationAction Continue
        trunk check --all --fix
        Write-Output "Quality checks completed."
    } catch {
        Write-Error "Quality check failed: $($_.Exception.Message)" -Category OperationStopped
    }
}

function Invoke-BusBuddyScriptAnalyzer {
    [CmdletBinding()]
    param()
    try {
        Write-Information "Running ScriptAnalyzer..." -InformationAction Continue
        Invoke-ScriptAnalyzer -Path "PowerShell" -Recurse
        Write-Output "ScriptAnalyzer completed."
    } catch {
        Write-Error "ScriptAnalyzer failed: $($_.Exception.Message)" -Category OperationStopped
    }
}

Export-ModuleMember -Function Invoke-BusBuddyQualityCheck, Invoke-BusBuddyScriptAnalyzer
