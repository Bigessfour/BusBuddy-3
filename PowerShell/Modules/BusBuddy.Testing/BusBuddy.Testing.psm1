<#
.SYNOPSIS
Testing helpers for BusBuddy (e.g., MVP checks).
Standards: PowerShell 7.5+, StrictMode 3.0.
#>

function invokeBusBuddyMvpCheck {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param()
    if ($PSCmdlet.ShouldProcess('MVP Check')) {
        Write-Information 'Running MVP checks...' -InformationAction Continue
        $root = Resolve-BusBuddyRepoRoot
        $proj = Join-Path $root 'BusBuddy.Tests/BusBuddy.Tests.csproj'
        & dotnet test $proj --filter "TestCategory=Core" --no-build
        return $LASTEXITCODE
    }
}

Export-ModuleMember -Function invokeBusBuddyMvpCheck