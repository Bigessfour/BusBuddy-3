# BusBuddy.ProfileTools - Agent Helper Functions

## Overview
Robust, always-available PowerShell module for Copilot agents working with BusBuddy repository.

## Key Features
- **Safe**: Never spawns external terminals during import
- **Idempotent**: Safe to call repeatedly  
- **Guarded**: Avoids unguarded DMTF datetime conversions
- **Interactive Safety**: Preview mode and explicit confirmation for process operations

## Functions

### Initialize-BusBuddyProfile / Ensure-BusBuddyProfileLoaded
```powershell
Initialize-BusBuddyProfile  # Approved verb wrapper
Ensure-BusBuddyProfileLoaded  # Direct function
```
- Idempotent profile initialization
- Sets `$env:BUSBUDDY_REPO_ROOT` and `$env:BUSBUDDY_PROFILE_LOADED='1'`
- Adds BusBuddy modules to PSModulePath
- Returns `$true` if successful, `$false` if repo root not found

### Get-BusBuddyPwshProcesses
```powershell
Get-BusBuddyPwshProcesses -Minutes 30  # Recent processes only
Get-BusBuddyPwshProcesses -Minutes 0   # All pwsh processes
```
- Lists pwsh.exe processes with guarded CreationDate conversion
- Returns PID, CommandLine, Started (nullable DateTime)
- Never throws DMTF conversion exceptions

### Stop-BusBuddyPwshProcesses
```powershell
Stop-BusBuddyPwshProcesses -Preview                    # Preview only
Stop-BusBuddyPwshProcesses -Minutes 30                 # Interactive stop recent
Stop-BusBuddyPwshProcesses -Pids @(12345, 67890)      # Stop specific PIDs
```
- Safely stops external pwsh processes
- Excludes current session (`$PID`)
- Requires explicit 'yes' confirmation
- Supports `-WhatIf` and `-Confirm` parameters

## Usage Examples

### For Copilot Agents
```powershell
# Ensure environment is ready
Import-Module BusBuddy.ProfileTools -Force
Initialize-BusBuddyProfile

# Check for orphaned external terminals
Get-BusBuddyPwshProcesses -Minutes 60 | Where-Object { $_.CommandLine -match '-NoExit' }

# Clean up safely (preview first)
Stop-BusBuddyPwshProcesses -Preview
Stop-BusBuddyPwshProcesses  # Interactive confirmation
```

### Integration with Other Scripts
```powershell
# In automation scripts
if (-not (Initialize-BusBuddyProfile)) {
    Write-Error "BusBuddy repository not found"
    exit 1
}

# Verify expected environment
if ($env:BUSBUDDY_REPO_ROOT -and (Test-Path "$env:BUSBUDDY_REPO_ROOT\BusBuddy.sln")) {
    Write-Output "✅ BusBuddy environment ready: $env:BUSBUDDY_REPO_ROOT"
} else {
    Write-Warning "⚠️ BusBuddy environment not fully initialized"
}
```

## Auto-Loading
The module is automatically imported by the BusBuddy repository profile:
- `PowerShell/Profiles/Microsoft.PowerShell_profile.ps1`
- Available in all sessions that load the repo profile
- No manual import required for normal BusBuddy development

## Module Location
- **Path**: `PowerShell/Modules/BusBuddy.ProfileTools/`
- **Files**: `BusBuddy.ProfileTools.psd1`, `BusBuddy.ProfileTools.psm1`
- **Version**: 1.0.0

## Error Handling
- All functions use try/catch for robust error handling
- DMTF datetime conversion is guarded to prevent exceptions
- Process operations exclude current session automatically
- User confirmation required for destructive operations

---
*Last updated: August 17, 2025*  
*Compatible with: PowerShell 7.5.2+, BusBuddy repository structure*
