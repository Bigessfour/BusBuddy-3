# BusBuddy.Workflow.psm1
# PowerShell module for workflow operations
#requires -Version 7.5
[CmdletBinding()]
param()

# Minimal function to prevent null script errors
function Get-WorkflowStatus {
    [CmdletBinding()]
    param(
        [string]$WorkflowName = "Default"
    )

    Write-Information "Getting status for workflow: $WorkflowName" -InformationAction Continue
    return @{
        Name = $WorkflowName
        Status = "Ready"
        LastRun = Get-Date
    }
}

# Export module members
Export-ModuleMember -Function Get-WorkflowStatus
