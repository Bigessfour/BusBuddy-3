<#
This script was moved to PowerShell/Scripts/Enhanced-Test-Output.ps1
Kept as a forwarder to maintain backward compatibility with existing references.
#>
try {
    $currentDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $functionsDir = Split-Path -Parent $currentDir
    $powerShellDir = Split-Path -Parent $functionsDir
    $target = Join-Path $powerShellDir 'Scripts/Enhanced-Test-Output.ps1'
    if (-not (Test-Path -LiteralPath $target)) {
        Write-Error "Forwarded script not found: $target"
        return
    }
    . $target
}
catch {
    Write-Error "Failed to forward to flattened script: $($_.Exception.Message)"
}
