#requires -Version 7.5
Set-StrictMode -Version 3.0

# Avoid auto-executing on import — provide wrapper to run the script when requested
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$enhancedTestPath = Join-Path $workspaceRoot 'PowerShell\Scripts\Enhanced-Test-Output.ps1'

<#
.SYNOPSIS
Short description

.DESCRIPTION
Long description

.PARAMETER Args
Parameter description

.EXAMPLE
An example

.NOTES
General notes
#>
function Invoke-BusBuddyEnhancedTestOutput {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [object[]] $Args
    )
    if (-not (Test-Path -LiteralPath $enhancedTestPath)) {
        Write-Error "Enhanced-Test-Output.ps1 not found at: $enhancedTestPath"
        return
    }
    & $enhancedTestPath @Args
}

Export-ModuleMember -Function Invoke-BusBuddyEnhancedTestOutput
