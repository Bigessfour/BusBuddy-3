#requires -Version 7.5
Set-StrictMode -Version 3.0

# Paths only — do not dot-source on import
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$validationPath = Join-Path $workspaceRoot 'PowerShell' 'Validation'
$envValidationPath = Join-Path $validationPath 'Environment-Validation.ps1'
$xamlValidationPath = Join-Path $validationPath 'Validate-XamlFiles.ps1'

function Test-BusBuddyEnvironment {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [object[]] $Args
    )
    if (-not (Test-Path -LiteralPath $envValidationPath)) {
        Write-Error "Environment-Validation.ps1 not found at: $envValidationPath"
        return
    }
    & $envValidationPath @Args
}

function Test-BusBuddyXamlFiles {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [object[]] $Args
    )
    if (-not (Test-Path -LiteralPath $xamlValidationPath)) {
        Write-Error "Validate-XamlFiles.ps1 not found at: $xamlValidationPath"
        return
    }
    & $xamlValidationPath @Args
}

Export-ModuleMember -Function Test-BusBuddyEnvironment, Test-BusBuddyXamlFiles
