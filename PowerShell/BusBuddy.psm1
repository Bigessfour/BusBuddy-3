function bb-wiley-seed {
    [CmdletBinding()]
    param()
    $scriptPath = Join-Path $PSScriptRoot 'Scripts/WileySeed.ps1'
    if (-Not (Test-Path $scriptPath)) {
        Write-Error "WileySeed.ps1 not found at $scriptPath"
        return
    }
    Write-Output "Running Wiley School District seeding script..."
    & $scriptPath
}
