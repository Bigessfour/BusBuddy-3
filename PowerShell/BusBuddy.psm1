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

# bb-health: Run the BusBuddy health check script
function bb-health {
    [CmdletBinding()]
    param()
    $healthScript = Join-Path $PSScriptRoot 'Modules/BusBuddy/bb-health.ps1'
    if (-Not (Test-Path $healthScript)) {
        Write-Error "bb-health.ps1 not found at $healthScript"
        return
    }
    Write-Output "Running BusBuddy health check..."
    & $healthScript
}
