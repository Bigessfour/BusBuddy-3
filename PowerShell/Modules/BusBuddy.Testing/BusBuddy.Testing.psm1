<#
.SYNOPSIS
Testing helpers for BusBuddy.
#>

function invokeBusBuddyMvpCheck {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param()

    if ($PSCmdlet.ShouldProcess('Run MVP check')) {
        Write-Information 'Running MVP checks...' -InformationAction Continue
        # placeholder - in repo this would call specific dotnet test with filters
        & dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj --filter "TestCategory=Core" --no-build
        return $LASTEXITCODE
    }
}

Export-ModuleMember -Function invokeBusBuddyMvpCheck
