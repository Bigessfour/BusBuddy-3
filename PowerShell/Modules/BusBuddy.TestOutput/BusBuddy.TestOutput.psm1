#requires -Version 7.5
Set-StrictMode -Version 3.0

# Load existing Enhanced-Test-Output.ps1 into module scope for now (to be refactored later)
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$enhancedTestPath = Join-Path $workspaceRoot 'PowerShell\Functions\Testing\Enhanced-Test-Output.ps1'
if (Test-Path $enhancedTestPath) {
    . $enhancedTestPath
}

Export-ModuleMember -Function * -Alias *
