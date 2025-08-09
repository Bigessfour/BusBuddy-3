#requires -Version 7.5
Set-StrictMode -Version 3.0

# Load MinimalOutputCapture.ps1 into module scope
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$minimalOutputPath = Join-Path $workspaceRoot 'PowerShell\Functions\Utilities\MinimalOutputCapture.ps1'
if (Test-Path $minimalOutputPath) {
    . $minimalOutputPath
}

Export-ModuleMember -Function * -Alias *
