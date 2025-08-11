#requires -Version 7.5
Set-StrictMode -Version 3.0

# Compute key paths without dot-sourcing external scripts to avoid auto-execution on import
$moduleRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$scriptsPath = Join-Path -Path $workspaceRoot -ChildPath 'PowerShell\Scripts'

function Get-BusBuddyVersion {
    [CmdletBinding()]
    param()
    [PSCustomObject]@{
        PSVersion     = $PSVersionTable.PSVersion.ToString()
        PSEdition     = $PSVersionTable.PSEdition
        PwshPath      = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
        WorkspaceRoot = $workspaceRoot
    }
}

function Invoke-BusBuddyRawIndex {
    [CmdletBinding()]
    param(
        [switch] $Open
    )
    $generator = Join-Path $workspaceRoot 'Scripts/Generate-RawLinks.ps1'
    if (-not (Test-Path -LiteralPath $generator)) {
        Write-Error "Generator not found: $generator"
        return
    }
    Push-Location -LiteralPath $workspaceRoot
    try {
        & $generator -Verbose
        if ($Open) {
            Get-ChildItem -LiteralPath $workspaceRoot -Filter 'RAW-LINKS*.txt' -File | ForEach-Object { Invoke-Item $_.FullName }
            Get-ChildItem -LiteralPath $workspaceRoot -Filter 'raw-index.*'   -File | ForEach-Object { Invoke-Item $_.FullName }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyRuntimeCapture {
    [CmdletBinding()]
    param(
        [Parameter()] [int]    $Duration       = 60,
        [Parameter()] [switch] $DetailedLogging,
        [Parameter()] [switch] $OpenLogsAfter,
        [Parameter()] [string] $OutputDirectory = 'logs\runtime-capture'
    )
    $scriptPath = Join-Path $scriptsPath 'Capture-RuntimeErrors.ps1'
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        Write-Error "Runtime capture script not found: $scriptPath"
        return
    }
    & $scriptPath @PSBoundParameters
}

function Start-BusBuddyRuntimeMonitor {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [object[]] $Args
    )
    $scriptPath = Join-Path $scriptsPath 'Runtime-Capture-Monitor.ps1'
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        Write-Error "Runtime monitor script not found: $scriptPath"
        return
    }
    & $scriptPath @Args
}

function Test-BusBuddyDatabaseConnections {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [object[]] $Args
    )
    $scriptPath = Join-Path $scriptsPath 'Test-DatabaseConnections.ps1'
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        Write-Error "Database connections test script not found: $scriptPath"
        return
    }
    & $scriptPath @Args
}

function Invoke-BusBuddyDIContainerDebug {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [object[]] $Args
    )
    $scriptPath = Join-Path $scriptsPath 'Debug-DIContainer.ps1'
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        Write-Error "DI container debug script not found: $scriptPath"
        return
    }
    & $scriptPath @Args
}

# Friendly aliases (do not trigger unapproved verb warnings)
Set-Alias -Name bb-version  -Value Get-BusBuddyVersion -Scope Local
Set-Alias -Name bb-rawindex -Value Invoke-BusBuddyRawIndex -Scope Local

# Explicit exports — export only approved-verb functions and friendly aliases
Export-ModuleMember -Function Get-BusBuddyVersion, Invoke-BusBuddyRawIndex, Invoke-BusBuddyRuntimeCapture, Start-BusBuddyRuntimeMonitor, Test-BusBuddyDatabaseConnections, Invoke-BusBuddyDIContainerDebug -Alias bb-version, bb-rawindex
