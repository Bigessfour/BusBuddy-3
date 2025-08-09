#requires -Version 7.5
Set-StrictMode -Version 3.0

# Load profile utility scripts into module scope
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$profileScripts = @(
    Join-Path $workspaceRoot 'PowerShell\Scripts\Capture-RuntimeErrors.ps1',
    Join-Path $workspaceRoot 'PowerShell\Scripts\Debug-DIContainer.ps1',
    Join-Path $workspaceRoot 'PowerShell\Scripts\Runtime-Capture-Monitor.ps1',
    Join-Path $workspaceRoot 'PowerShell\Scripts\Test-DatabaseConnections.ps1'
)
foreach ($script in $profileScripts) {
    if (Test-Path $script) {
        . $script
    }
}

Export-ModuleMember -Function * -Alias *
