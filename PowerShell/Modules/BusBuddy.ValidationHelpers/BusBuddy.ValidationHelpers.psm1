#requires -Version 7.5
Set-StrictMode -Version 3.0

# Initialize a flag to ensure Serilog is only configured once
if (-not (Get-Variable -Name 'script:SerilogInitialized' -ErrorAction SilentlyContinue)) {
    $script:SerilogInitialized = $false
}

# Load Environment-Validation.ps1 and Validate-XamlFiles.ps1 into module scope
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$validationPath = Join-Path $workspaceRoot 'PowerShell' 'Validation'
$envValidationPath = Join-Path $validationPath 'Environment-Validation.ps1'
$xamlValidationPath = Join-Path $validationPath 'Validate-XamlFiles.ps1'
if (Test-Path $envValidationPath) {
    . $envValidationPath
}
if (Test-Path $xamlValidationPath) {
    . $xamlValidationPath
}

Export-ModuleMember -Function * -Alias *
