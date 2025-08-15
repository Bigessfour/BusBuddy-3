<#
Imports BusBuddy PowerShell modules and registers lightweight aliases/wrappers.
Follow project standards: PowerShell 7.5.2, avoid Write-Host, add SupportsShouldProcess on module functions.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest

$repoRoot = Resolve-Path -Path (Join-Path $PSScriptRoot '..')
$modulesPath = Join-Path $repoRoot 'Modules'

if (-not (Test-Path -LiteralPath $modulesPath)) {
    Write-Information "Modules folder not found at: $modulesPath" -InformationAction Continue
    return
}

# Import modules if available
Get-ChildItem -Path $modulesPath -Directory | ForEach-Object {
    $moduleFolder = $_.FullName
    try {
        Write-Information "Importing module from $moduleFolder" -InformationAction Continue
        Import-Module -Name $moduleFolder -Force -ErrorAction Stop
    }
    catch {
        Write-Information "Failed to import module at $($moduleFolder): $($_.Exception.Message)" -InformationAction Continue
    }
}

# Create thin wrapper functions that call module functions. They accept positional args and forward them.
function bbBuild {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        $Args
    )
    if ($PSCmdlet.ShouldProcess('Invoke BusBuddy Build')) {
        & invokeBusBuddyBuild @Args
    }
}

function bbRun {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        $Args
    )
    if ($PSCmdlet.ShouldProcess('Invoke BusBuddy Run')) {
        & invokeBusBuddyRun @Args
    }
}

function bbTest {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        $Args
    )
    if ($PSCmdlet.ShouldProcess('Invoke BusBuddy Test')) {
        & invokeBusBuddyTest @Args
    }
}

function bbMvpCheck {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param()
    if ($PSCmdlet.ShouldProcess('Invoke BusBuddy MVP Check')) {
        & invokeBusBuddyMvpCheck
    }
}

Write-Information 'BusBuddy modules imported and aliases registered: bbBuild, bbRun, bbTest, bbMvpCheck' -InformationAction Continue
