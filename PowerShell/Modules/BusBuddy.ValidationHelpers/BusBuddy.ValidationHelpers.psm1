#requires -Version 7.5
Set-StrictMode -Version 3.0

# Load Environment-Validation.ps1 and Validate-XamlFiles.ps1 into module scope
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$envValidationPath = Join-Path $workspaceRoot 'PowerShell\Validation\Environment-Validation.ps1'
$xamlValidationPath = Join-Path $workspaceRoot 'PowerShell\Validation\Validate-XamlFiles.ps1'
if (Test-Path $envValidationPath) {
    . $envValidationPath
}
if (Test-Path $xamlValidationPath) {
    . $xamlValidationPath
}

Export-ModuleMember -Function * -Alias *
