#requires -Version 7.5
<#
.SYNOPSIS
    Initializes the BusBuddy PowerShell module so bb-* commands are available in CI and local terminals.

.DESCRIPTION
    - Ensures PowerShell 7.5+
    - Adds the repository PowerShell/Modules path to PSModulePath for auto-discovery
    - Imports the module manifest (BusBuddy.psd1)
    - Verifies essential bb-* aliases exist for workflows

.NOTES
    Microsoft Docs (Module paths): https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_PSModulePath
    Microsoft Docs (Import-Module): https://learn.microsoft.com/powershell/module/microsoft.powershell.core/import-module
#>
[CmdletBinding()]
param()

try {
    # Validate PowerShell version requirement
    if ($PSVersionTable.PSVersion.Major -lt 7 -or ($PSVersionTable.PSVersion.Major -eq 7 -and $PSVersionTable.PSVersion.Minor -lt 5)) {
        throw "PowerShell 7.5+ required. Detected $($PSVersionTable.PSVersion)."
    }

    # Resolve repository root from this profile script location: PowerShell/Profiles -> repo root
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
    $modulesPath = Join-Path $repoRoot 'PowerShell\Modules'

    # Prepend repo Modules path to PSModulePath if missing
    $sep = [IO.Path]::PathSeparator
    $currentPaths = ($env:PSModulePath -split [regex]::Escape($sep)) | Where-Object { $_ -and $_.Trim() }
    if (-not ($currentPaths | ForEach-Object { $_.TrimEnd('\\') } | Where-Object { $_ -ieq $modulesPath })) {
        $env:PSModulePath = "$modulesPath$sep$env:PSModulePath"
    }

    # Import module manifest explicitly to ensure correct metadata and exports
    $moduleManifest = Join-Path $modulesPath 'BusBuddy\BusBuddy.psd1'
    if (-not (Test-Path $moduleManifest)) { throw "Module manifest not found: $moduleManifest" }

    # Use -Force and -ErrorAction Stop to bubble fatal issues
    Import-Module $moduleManifest -Force -ErrorAction Stop

    # Verify essential aliases exist; if not, create non-global aliases as a safety net
    $need = @(
        @{ Alias='bb-build'; Target='Invoke-BusBuddyBuild' },
        @{ Alias='bb-run';   Target='Invoke-BusBuddyRun' },
        @{ Alias='bb-test';  Target='Invoke-BusBuddyTest' },
        @{ Alias='bb-health';Target='Invoke-BusBuddyHealthCheck' },
        @{ Alias='bb-anti-regression'; Target='Invoke-BusBuddyAntiRegression' },
        @{ Alias='bb-mvp-check';        Target='Test-BusBuddyMVPReadiness' }
    )
    foreach ($n in $need) {
        if (-not (Get-Command $n.Alias -ErrorAction SilentlyContinue)) {
            if (Get-Command $n.Target -ErrorAction SilentlyContinue) {
                Set-Alias -Name $n.Alias -Value $n.Target -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Write-Information 'BusBuddy module initialized (bb-* commands available).' -InformationAction Continue
    $true
} catch {
    Write-Error "Failed to initialize BusBuddy module: $($_.Exception.Message)"
    $false
}
