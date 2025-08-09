
# Load bb-* commands from the BusBuddy.Commands module (no dot-sourcing)
try {
    $commandsManifest = Join-Path $PSScriptRoot 'Modules\BusBuddy.Commands\BusBuddy.Commands.psd1'
    if (Test-Path $commandsManifest) {
        Import-Module $commandsManifest -Force -ErrorAction Stop
    } else {
        # Fallback to module by name if manifest path changes
        Import-Module 'BusBuddy.Commands' -ErrorAction SilentlyContinue
    }
} catch {
    Write-Verbose "BusBuddy.Commands module not loaded: $($_.Exception.Message)"
}

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

# Export public functions as required by Microsoft PowerShell standards
Export-ModuleMember -Function 'Invoke-BusBuddyWileySeed', 'Test-BusBuddyHealth'
