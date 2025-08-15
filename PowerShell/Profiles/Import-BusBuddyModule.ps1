#Requires -PSEdition Core -Version 7.5.2  # Docs: about_Requires
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
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]  # Docs: about_Functions_CmdletBindingAttribute; ShouldProcess
param()

try {
    # Validate PowerShell version requirement (patch-level)
    if ($PSVersionTable.PSVersion -lt [version]'7.5.2') {
        throw "PowerShell 7.5.2+ required. Detected $($PSVersionTable.PSVersion)."
    }
    # Require PowerShell Core edition — Docs: about_PowerShell_Editions
    if ($PSVersionTable.PSEdition -ne 'Core') {
        throw "PowerShell Core required (PSEdition='Core'). Detected '$($PSVersionTable.PSEdition)'."
    }

    # Resolve repository root from this profile script location: PowerShell/Profiles -> repo root
    # Ensure we store a string path — Resolve-Path returns PathInfo. Docs: Resolve-Path returns PathInfo (.Path)
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $modulesPath = Join-Path $repoRoot 'PowerShell\Modules'

    # Prepend repo Modules path to PSModulePath if missing (normalize to avoid dupes)
    $sep = [IO.Path]::PathSeparator
    $currentPaths = ($env:PSModulePath -split [regex]::Escape($sep)) | Where-Object { $_ -and $_.Trim() }
    $normalizedCurrent = $currentPaths | ForEach-Object { $_.Trim().TrimEnd('\','/').ToLowerInvariant() }
    $normalizedModules = $modulesPath.TrimEnd('\','/').ToLowerInvariant()
    if (-not ($normalizedCurrent -contains $normalizedModules)) {
        if ($PSCmdlet.ShouldProcess('PSModulePath', "Prepend '$modulesPath'")) {  # Docs: ShouldProcess
            $newPath = if ([string]::IsNullOrWhiteSpace($env:PSModulePath)) { $modulesPath } else { "$modulesPath$sep$env:PSModulePath" }
            $env:PSModulePath = $newPath  # Docs: about_PSModulePath
        }
    }

    # Import module manifest explicitly to ensure correct metadata and exports
    $moduleManifest = Join-Path $modulesPath 'BusBuddy\BusBuddy.psd1'
    if (-not (Test-Path $moduleManifest)) { throw "Module manifest not found: $moduleManifest" }

    # Use -Force and -ErrorAction Stop to bubble fatal issues. Docs: Import-Module
    if ($PSCmdlet.ShouldProcess("Module 'BusBuddy'", 'Import via manifest')) {
        Import-Module $moduleManifest -Force -ErrorAction Stop  # Docs: Import-Module
    }

    # Verify essential aliases exist; ensure they point to the correct targets
    $need = @(
        @{ Alias='bb-build';           Target='Invoke-BusBuddyBuild' },
        @{ Alias='bb-run';             Target='Invoke-BusBuddyRun' },
        @{ Alias='bb-test';            Target='Invoke-BusBuddyTest' },
        @{ Alias='bb-health';          Target='Invoke-BusBuddyHealthCheck' },
        @{ Alias='bb-anti-regression'; Target='Invoke-BusBuddyAntiRegression' },
        @{ Alias='bb-mvp-check';       Target='Test-BusBuddyMVPReadiness' },
        @{ Alias='bb-xaml-validate';   Target='Invoke-BusBuddyXamlValidation' }
    )
    foreach ($n in $need) {
        $existing = Get-Command -Name $n.Alias -ErrorAction SilentlyContinue
        $targetAvailable = Get-Command -Name $n.Target -ErrorAction SilentlyContinue
        if ($targetAvailable) {
            $needsSet = -not $existing -or ($existing.CommandType -eq 'Alias' -and $existing.Definition -ne $n.Target) -or ($existing.CommandType -ne 'Alias')
            if ($needsSet) {
                if ($PSCmdlet.ShouldProcess("Alias '$($n.Alias)'", "Point to '$($n.Target)' (Scope: Global)")) {
                    Set-Alias -Name $n.Alias -Value $n.Target -Scope Global -Force -ErrorAction SilentlyContinue  # Docs: about_Aliases / Set-Alias
                }
            }
        }
    }

    Write-Information 'BusBuddy module initialized (bb-* commands available).' -InformationAction Continue
    $true
} catch {
    Write-Error -ErrorRecord $_  # Docs: output streams (Write-Error)
    $false
}
