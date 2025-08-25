# BusBuddy.Debug.psm1
# PowerShell module for debug operations
#requires -Version 7.5
[CmdletBinding()]
param()

# Minimal function to prevent null script errors
function Get-DebugSession {
    [CmdletBinding()]
    param(
        [string]$Component = "General"
    )

    Write-Information "Getting debug session for: $Component" -InformationAction Continue
    return @{
        SessionId = [System.Guid]::NewGuid()
        Component = $Component
        StartTime = Get-Date
    }
}# Export module members
Export-ModuleMember -Function Get-DebugSession
