# BusBuddy Global Profile - ONE-CLICK INSTALLER
# Run this script to automatically install the global profile with elevation
# No manual steps required - just run and go!

param(
    [switch]$WhatIf,
    [switch]$Silent,
    [switch]$Force
)

# Import the optimized installer
$optimizedScript = Join-Path $PSScriptRoot "global-profile-fix-optimized.ps1"

if (-not (Test-Path $optimizedScript)) {
    Write-Error "❌ Required file not found: $optimizedScript"
    Write-Host "💡 Ensure global-profile-fix-optimized.ps1 is in the same directory" -ForegroundColor Yellow
    exit 1
}

# Load the installer functions
. $optimizedScript

# Run installation with parameters
Write-Host "🚀 BusBuddy Global Profile - One-Click Installer" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

if ($WhatIf) {
    Write-Host "Preview mode - showing what would be installed:" -ForegroundColor Yellow
    Install-BusBuddyGlobalProfile -WhatIf
}
else {
    Write-Host "Installing global profile with auto-elevation..." -ForegroundColor Green
    Install-BusBuddyGlobalProfile -Silent:$Silent -Force:$Force
    
    if (-not $Silent) {
        Write-Host ""
        Write-Host "✅ Installation complete!" -ForegroundColor Green
        Write-Host "🔄 Open a new PowerShell window and run 'bb-env' to test" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Validation commands:" -ForegroundColor White
        Write-Host "  Test-BusBuddyGlobalProfile    # Check installation" -ForegroundColor Gray
        Write-Host "  bb-env                        # Load BusBuddy environment" -ForegroundColor Gray
    }
}
