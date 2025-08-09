#requires -Version 7.5
Set-StrictMode -Version 3.0

# Load profile utility scripts into module scope
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$scriptsPath = Join-Path -Path $workspaceRoot -ChildPath 'PowerShell\Scripts'
$profileScripts = @(
    Join-Path -Path $scriptsPath -ChildPath 'Capture-RuntimeErrors.ps1'
    Join-Path -Path $scriptsPath -ChildPath 'Debug-DIContainer.ps1'
    Join-Path -Path $scriptsPath -ChildPath 'Runtime-Capture-Monitor.ps1'
    Join-Path -Path $scriptsPath -ChildPath 'Test-DatabaseConnections.ps1'
)
foreach ($script in $profileScripts) {
    if (Test-Path $script) {
        . $script
    }
}

Export-ModuleMember -Function * -Alias *
