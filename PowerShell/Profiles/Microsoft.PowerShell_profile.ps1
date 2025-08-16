<#
    BusBuddy Auto-Load Wrapper for CurrentUserCurrentHost
    - Locates the BusBuddy repo root by finding BusBuddy.sln and loads the profile.

    Docs (required references):
    - PowerShell profiles — https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Profiles
    - PowerShell output streams — https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
#>

# Require PowerShell Core 7.5.2 — warn if mismatch (docs: about_Automatic_Variables → $PSVersionTable)
# https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Automatic_Variables#psversiontable
$requiredVersion = [Version]'7.5.2'
$currentVersion = $PSVersionTable.PSVersion
if ($PSVersionTable.PSEdition -ne 'Core' -or $currentVersion -ne $requiredVersion) {
    Write-Warning "BusBuddy expects PowerShell Core 7.5.2. Current: $($PSVersionTable.PSEdition) $currentVersion. Some commands may be unavailable."
}

# Optional: enforce StrictMode for cleaner scripts as per project standards
try { Set-StrictMode -Version 3.0 } catch { Write-Verbose "StrictMode not set: $_" }

# Idempotent guard to prevent multiple executions in the same session
if ($env:BUSBUDDY_PROFILE_LOADED -eq '1') {
    Write-Information "BusBuddy profile already loaded; skipping reload." -InformationAction Continue
    return
}

# Start from profile script directory; fall back to current location if unavailable
$probe = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

# Walk up the directory tree to locate the repo root (contains BusBuddy.sln)
while ($probe -and -not (Test-Path (Join-Path $probe 'BusBuddy.sln'))) {
    $next = Split-Path $probe -Parent
    if (-not $next -or $next -eq $probe) { $probe = $null; break }
    $probe = $next
}

if (-not $probe) {
    Write-Warning "BusBuddy repo root not found. Check setup for Azure SQL: https://learn.microsoft.com/azure/azure-sql/?view=azuresql and Syncfusion WPF: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf"
    return
}

# Expose discovered repo root to downstream profile(s)
$env:BUSBUDDY_REPO_ROOT = $probe

# Load profile from PowerShell\Profiles subdirectory in the repo
$profilePath = Join-Path $probe 'PowerShell\Profiles\BusBuddyProfile.ps1'
if (Test-Path $profilePath) {
    . $profilePath
    $env:BUSBUDDY_PROFILE_LOADED = '1'
    Write-Information "BusBuddy profile loaded successfully from: $profilePath" -InformationAction Continue
}
else {
    Write-Warning "BusBuddyProfile.ps1 not found under $probe. Verify repo structure and docs: Azure SQL https://learn.microsoft.com/azure/azure-sql/?view=azuresql and Syncfusion WPF https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf"
}
