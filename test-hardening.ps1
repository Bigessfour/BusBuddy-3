# Test the PowerShell hardening features
$env:BUSBUDDY_REPO_ROOT = (Get-Location).Path
Import-Module '.\PowerShell\Modules\BusBuddy\BusBuddy.psd1' -Force

Write-Output "=== Testing BusBuddy Module Loading Hardening ==="

# Test module detection
$repoRoot = $env:BUSBUDDY_REPO_ROOT
$modulesPath = Join-Path $repoRoot 'PowerShell\Modules'
$availableModules = Get-ChildItem $modulesPath -Directory | ForEach-Object { $_.Name }
$loadedModules = Get-Module | Where-Object { $_.Name -like "*BusBuddy*" } | ForEach-Object { $_.Name }

Write-Output "Available BusBuddy modules: $($availableModules -join ', ')"
Write-Output "Loaded BusBuddy modules: $($loadedModules -join ', ')"

# Test PSModulePath hardening
Write-Output "`n=== PSModulePath Hardening Test ==="
$currentPSModulePath = $env:PSModulePath -split [IO.Path]::PathSeparator
$repoModulesPath = Join-Path $repoRoot 'PowerShell\Modules'

Write-Output "Repo modules path: $repoModulesPath"
if ($repoModulesPath -in $currentPSModulePath) {
    Write-Output "✓ BusBuddy modules path is in PSModulePath"
} else {
    Write-Output "✗ BusBuddy modules path NOT in PSModulePath"
    Write-Output "  → Current PSModulePath entries:"
    $currentPSModulePath | ForEach-Object { Write-Output "    $_" }
}

# Test PowerShell version detection
Write-Output "`n=== PowerShell Context Test ==="
$psVersion = $PSVersionTable.PSVersion
$psEditionType = $PSVersionTable.PSEdition
# Hardening features include restricted module loading, stricter execution policy, and limited script access.
Write-Output "PowerShell Version: $psVersion ($psEditionType)"
Write-Output "Note: Hardening features are fully enabled only on PowerShell 7.5.2. Other versions may have reduced protections."

# Set required PowerShell version here; update as needed for compatibility
$requiredPSVersion = [Version]"7.5.2"
# This version is required for hardening features due to compatibility with BusBuddy module

if ($psVersion -eq $requiredPSVersion) {
    Write-Output "✓ PowerShell $requiredPSVersion detected - hardening features will be active"
} else {
    Write-Output "→ PowerShell $psVersion detected"
}

# Test execution policy
$execPolicy = Get-ExecutionPolicy
Write-Output "Execution Policy: $execPolicy"

Write-Output "`n=== Test Complete ==="
