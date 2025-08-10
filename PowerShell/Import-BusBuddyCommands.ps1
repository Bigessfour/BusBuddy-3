<#
	Centralized, quiet-by-default loader for all BusBuddy PowerShell modules.
	- Adds the repo Modules folder to PSModulePath for the current session
	- Imports all modules found under PowerShell/Modules (manifest first, then .psm1)
	- Emits no output unless BUSBUDDY_VERBOSE=1 or -Verbose is used

	Usage (from repo root):
	  . PowerShell/Import-BusBuddyCommands.ps1

	Docs:
	  - Import-Module: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/import-module
	  - about_Profiles: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Profiles
#>

[CmdletBinding()]
param()

$VerboseEnabled = $PSBoundParameters.ContainsKey('Verbose') -or $env:BUSBUDDY_VERBOSE -eq '1'

try {
	# Resolve repo root from this script location
	$repoRoot = (Resolve-Path -Path (Join-Path $PSScriptRoot '..')).Path
	$modulesPath = Join-Path $repoRoot 'PowerShell\Modules'

	if (-not (Test-Path $modulesPath)) {
		if ($VerboseEnabled) { Write-Verbose "Modules path not found: $modulesPath" }
		return
	}

	# Ensure Modules path is available for module discovery (session only)
	if ($env:PSModulePath -notlike "*${modulesPath}*") {
		$env:PSModulePath = "$modulesPath;$($env:PSModulePath)"
		if ($VerboseEnabled) { Write-Verbose "Added to PSModulePath: $modulesPath" }
	}

	$available = Get-ChildItem -Path $modulesPath -Directory -ErrorAction SilentlyContinue
	$imported = 0
	foreach ($dir in $available) {
		$manifest = Join-Path $dir.FullName ("{0}.psd1" -f $dir.Name)
		$modulePs = Join-Path $dir.FullName ("{0}.psm1" -f $dir.Name)

		$importedThis = $false
		if (Test-Path $manifest) {
			try {
				Import-Module $manifest -Force -Global -ErrorAction Stop
				$importedThis = $true
			} catch {
				if ($VerboseEnabled) { Write-Verbose "Manifest import failed for $($dir.Name): $($_.Exception.Message)" }
			}
		}
		if (-not $importedThis -and (Test-Path $modulePs)) {
			try {
				Import-Module $modulePs -Force -Global -ErrorAction Stop
				$importedThis = $true
			} catch {
				if ($VerboseEnabled) { Write-Verbose "Direct import failed for $($dir.Name): $($_.Exception.Message)" }
			}
		}
		if ($importedThis) { $imported++ }
	}

	if ($VerboseEnabled) {
		$bb = Get-Command -Name 'bb-*' -ErrorAction SilentlyContinue
		$bbCount = if ($bb) { $bb.Count } else { 0 }
		Write-Verbose ("BusBuddy modules imported: {0}; bb-* commands available: {1}" -f $imported, $bbCount)
	}
}
catch {
	# Keep failures quiet by default; surface details only when verbose is enabled
	if ($VerboseEnabled) {
		Write-Verbose ("Loader failure: {0}" -f $_.Exception.Message)
	}
}
