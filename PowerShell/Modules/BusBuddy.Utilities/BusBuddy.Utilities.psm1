#requires -Version 7.5
Set-StrictMode -Version 3.0

# Provide wrapper to execute MinimalOutputCapture on demand only
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$minimalOutputPath = Join-Path $workspaceRoot 'PowerShell\Scripts\MinimalOutputCapture.ps1'

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
function Invoke-BusBuddyMinimalOutputCapture {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [object[]] $Args
    )
    if (-not (Test-Path -LiteralPath $minimalOutputPath)) {
        Write-Error "MinimalOutputCapture.ps1 not found at: $minimalOutputPath"
        return
    }
    & $minimalOutputPath @Args
}

Export-ModuleMember -Function Invoke-BusBuddyMinimalOutputCapture
