# Minimal BusBuddy PowerShell loader for just bbClean, bbRun, bbRestore
# Loads only the core module and exposes only the required commands/aliases

# Path to the core module
$busBuddyModule = Join-Path $PSScriptRoot '../PowerShell/Modules/BusBuddy/BusBuddy.psm1'

if (Test-Path $busBuddyModule) {
    Import-Module $busBuddyModule -Force -DisableNameChecking
    # Remove all but the core aliases
    $allAliases = 'bbBuild','bbRun','bbTest','bbClean','bbRestore','bb-build','bb-run','bb-test','bb-clean','bb-restore'
    Get-Alias | Where-Object { $_.Name -notin $allAliases } | ForEach-Object { Remove-Item "Alias:\$($_.Name)" -ErrorAction SilentlyContinue }
} else {
    Write-Warning "BusBuddy core module not found at $busBuddyModule"
}

# Import Azure authentication module
$azureAuthModule = Join-Path $PSScriptRoot '../Modules/BusBuddy.AzureAuth.psm1'
if (Test-Path $azureAuthModule) {
    Import-Module $azureAuthModule -Force -DisableNameChecking
}
