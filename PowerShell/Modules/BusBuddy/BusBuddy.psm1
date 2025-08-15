<#
.SYNOPSIS
Core BusBuddy helper functions (build, run, test) implemented as PowerShell functions.

.DOCUMENTATION
- New-ModuleManifest: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/new-modulemanifest
- Publish-Module: https://learn.microsoft.com/powershell/module/powershellget/publish-module

NOTE: Follow project standards: PowerShell 7.5.2, avoid Write-Host, prefer Write-Information/Write-Output.
#>

function invokeBusBuddyBuild {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [string] $solution = 'BusBuddy.sln'
    )

    if ($PSCmdlet.ShouldProcess("Build solution: $solution")) {
        Write-Information "Starting build: $solution" -InformationAction Continue
        & dotnet build $solution -v minimal
        return $LASTEXITCODE
    }
}

function invokeBusBuddyRun {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Low')]
    param(
        [string] $project = 'BusBuddy.csproj'
    )

    if ($PSCmdlet.ShouldProcess("Run project: $project")) {
        Write-Information "Running project: $project" -InformationAction Continue
        & dotnet run --project $project
        return $LASTEXITCODE
    }
}

function invokeBusBuddyTest {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [string] $solutionOrProject = 'BusBuddy.sln'
    )

    if ($PSCmdlet.ShouldProcess("Test: $solutionOrProject")) {
        Write-Information "Running tests: $solutionOrProject" -InformationAction Continue
        & dotnet test $solutionOrProject --no-build
        return $LASTEXITCODE
    }
}

Export-ModuleMember -Function invokeBusBuddyBuild, invokeBusBuddyRun, invokeBusBuddyTest
