
# Import bbAntiRegression for profile integration
. "$PSScriptRoot\Modules\BusBuddy\bb-anti-regression.ps1"

function Invoke-BusBuddyWileySeed {
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

function Test-BusBuddyHealth {
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
