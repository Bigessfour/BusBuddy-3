<#
.SYNOPSIS
Core BusBuddy helper functions for .NET/WPF devops (build, run, test, health).
Standards: PowerShell 7.5+, StrictMode 3.0, no Write-Host, Write-Information logging.
Refs: dotnet CLI[](https://learn.microsoft.com/dotnet/core/tools/), Syncfusion WPF[](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf), Azure SQL[](https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql).
#>

# Helper to resolve repo root by finding BusBuddy.sln (up to 5 levels).
function Resolve-BusBuddyRepoRoot {
    [CmdletBinding()]
    param()
    $current = $PSScriptRoot
    for ($i = 0; $i -lt 5; $i++) {
        if ([string]::IsNullOrEmpty($current)) { break }
        $sln = Join-Path $current 'BusBuddy.sln'
        if (Test-Path -LiteralPath $sln) { return $current }
        $current = Split-Path $current -Parent
    }
    return (Get-Location).Path
}

function invokeBusBuddyBuild {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [string]$Solution = 'BusBuddy.sln',
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')][string]$Verbosity = 'minimal'
    )
    if (-not (Test-Path $Solution)) { Write-Error "Solution not found: $Solution"; return 1 }
    if ($PSCmdlet.ShouldProcess($Solution, "Build ($Configuration, $Verbosity)")) {
        Write-Information "Building $Solution..." -InformationAction Continue
        & dotnet build $Solution --configuration $Configuration -v $Verbosity
        return $LASTEXITCODE
    }
}

# Add other functions like invokeBusBuddyRun, invokeBusBuddyTest, etc., as in previous responses (truncated for brevity; copy full from earlier).

Export-ModuleMember -Function invokeBusBuddyBuild, invokeBusBuddyRun, invokeBusBuddyTest, invokeBusBuddyHealthCheck, invokeBusBuddyRestore, invokeBusBuddyClean, invokeBusBuddyAntiRegression, invokeBusBuddyXamlValidation, testBusBuddyMvpReadiness