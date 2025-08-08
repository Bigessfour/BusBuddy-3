# 🔒 PowerShell Profile & File Lock Management Guide

## 🎯 Overview

This guide explains the BusBuddy PowerShell profile management strategy designed to prevent .NET assembly file locks while maintaining development productivity.

## 🚨 The File Lock Problem

### Root Cause
When PowerShell profiles auto-load, they import modules that initialize .NET assemblies:
- **Serilog**: For structured logging
- **Entity Framework**: For database operations  
- **MSBuild APIs**: For build operations

These assemblies create persistent file handles to DLLs in `bin/Debug/` directories, causing "file locked" errors during subsequent builds.

### Symptoms
```
Error MSB3027: Could not copy "file.dll" to "bin\Debug\file.dll". 
The process cannot access the file because it is being used by another process.
```

## ✅ Solution: Conditional Profile Loading

### 1. Environment Variable Control

The profile now checks for `$env:NoBusBuddyProfile`:

```powershell
# In Microsoft.PowerShell_profile.ps1
if ($env:NoBusBuddyProfile -eq "true") {
    Write-Host "⚠️ BusBuddy profile loading skipped (NoBusBuddyProfile=true)" -ForegroundColor Yellow
    Write-Host "💡 This prevents .NET assembly locks during build operations" -ForegroundColor Cyan
    return
}
```

### 2. Build Operations Use -NoProfile

All build tasks now use `-NoProfile` flag:

```powershell
# Build without profile loading
pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "build-script.ps1"
```

### 3. Interactive Sessions Load Profiles

Interactive development sessions continue to load profiles for `bb-*` commands:

```powershell
# Interactive session - loads profile
pwsh.exe  # Auto-loads profile with bb-health, bb-build, etc.
```

## 📋 Usage Patterns

### ✅ Recommended: Build Operations
```powershell
# Option 1: Use -NoProfile flag
pwsh.exe -NoProfile -File "PowerShell\Scripts\Build\build-no-profile.ps1"

# Option 2: Set environment variable
$env:NoBusBuddyProfile = "true"
pwsh.exe -File "build-script.ps1"

# Option 3: Use dedicated no-profile build task
# VS Code: Run Task → "🔒 BB: No-Profile Build (File Lock Safe)"
```

### ✅ Recommended: Interactive Development
```powershell
# Load profile for development commands
pwsh.exe  # Auto-loads profile

# Use bb-* commands
bb-health    # Environment health check
bb-build     # Interactive build with profile
bb-run       # Run application
bb-clean     # Clean artifacts
```

### ✅ Recommended: Testing Profile Loading
```powershell
# Test conditional loading
$env:NoBusBuddyProfile = "true"
pwsh.exe  # Should skip profile loading

# Test normal loading  
Remove-Item env:NoBusBuddyProfile -ErrorAction SilentlyContinue
pwsh.exe  # Should load profile normally
```

## 🛠️ Available VS Code Tasks

### Build Tasks (File Lock Safe)
- **🔒 BB: No-Profile Build (File Lock Safe)** - Recommended for clean builds
- **🚌 BB: Profile-Aware Build** - Now uses `-NoProfile` 
- **🏗️ BB: Comprehensive Build & Run Pipeline** - Now uses `-NoProfile`
- **PS Fixed: Build Solution** - Already uses `-NoProfile`

### Interactive Tasks (Profile Enabled)
- **BB: Run App** - Loads profiles for `bb-run` command
- **PS Fixed: Health Check** - Uses profiles for `bb-health`
- **PS Fixed: Advanced Diagnostics** - Uses profiles for `bb-diagnostic`

## 🔧 Build Scripts

### New: build-no-profile.ps1
Located at: `PowerShell\Scripts\Build\build-no-profile.ps1`

Features:
- Sets `$env:NoBusBuddyProfile = "true"`
- Uses `-NoProfile` approach
- Prevents file locks
- Includes comprehensive error handling
- Cleans up environment variables

Usage:
```powershell
.\PowerShell\Scripts\Build\build-no-profile.ps1           # Standard build
.\PowerShell\Scripts\Build\build-no-profile.ps1 -Clean    # Clean build
.\PowerShell\Scripts\Build\build-no-profile.ps1 -Clean -Configuration Release
```

## 🧪 Testing the Setup

### Test 1: Verify Conditional Loading
```powershell
# Test profile skipping
$env:NoBusBuddyProfile = "true"
pwsh.exe
# Should see: "⚠️ BusBuddy profile loading skipped (NoBusBuddyProfile=true)"

# Test normal loading
Remove-Item env:NoBusBuddyProfile -ErrorAction SilentlyContinue  
pwsh.exe
# Should see: "🚌 Loading BusBuddy PowerShell Environment..."
```

### Test 2: Verify Build Without Locks
```powershell
# Run a clean build that previously had lock issues
pwsh.exe -NoProfile -File "PowerShell\Scripts\Build\build-no-profile.ps1" -Clean
# Should complete without "file locked" errors
```

### Test 3: Verify Interactive Commands
```powershell
# Start interactive session
pwsh.exe
# Test commands are available
bb-health
bb-build
bb-clean
```

## 🚀 Best Practices

### ✅ Do This
- Use `-NoProfile` for all automated builds
- Use profile-enabled sessions for interactive development
- Set `$env:NoBusBuddyProfile = "true"` in CI/CD pipelines
- Use the dedicated "🔒 BB: No-Profile Build" task for clean builds

### ❌ Avoid This
- Loading profiles in build scripts that call `dotnet build`
- Running builds in sessions that have imported .NET modules
- Mixing profile-enabled and profile-disabled approaches in the same session

## 📊 Performance Benefits

### File Lock Prevention
- ✅ Eliminates MSB3027/MSB3021 errors
- ✅ Allows concurrent build operations
- ✅ Prevents "file in use" build failures

### Build Reliability  
- ✅ Consistent builds across environments
- ✅ CI/CD pipeline compatibility
- ✅ Reduced dependency on session state

### Development Productivity
- ✅ Fast interactive commands available (`bb-*`)
- ✅ Clean separation of concerns
- ✅ Best of both worlds approach

## 🔍 Troubleshooting

### Issue: Profile Loading Warnings
```
⚠️ Bus Buddy profile loader not found in current directory or parents
```
**Solution**: This is normal when using `-NoProfile`. The warning indicates the profile check worked.

### Issue: `bb-*` Commands Not Available
**Cause**: Running in `-NoProfile` mode  
**Solution**: Start interactive session without `-NoProfile` flag

### Issue: File Still Locked
**Cause**: Previous PowerShell session still has assemblies loaded  
**Solution**: Close all PowerShell sessions and restart, or use `bb-stop` command

### Issue: Build Task Still Has Locks
**Cause**: Task not updated to use `-NoProfile`  
**Solution**: Verify task configuration includes `-NoProfile` flag

## 📝 Migration Notes

### Changed Tasks
All build-related tasks now include `-NoProfile` flag:
- 🚌 BB: Profile-Aware Build
- 🏗️ BB: Comprehensive Build & Run Pipeline
- 🔒 BB: No-Profile Build (File Lock Safe) - **NEW**

### Unchanged Tasks  
Interactive tasks continue to use profiles:
- BB: Run App (explicitly enables profiles)
- Health Check tasks
- Diagnostic tasks

### Updated Profile
`PowerShell\Profiles\Microsoft.PowerShell_profile.ps1` now includes:
- Conditional loading logic
- Environment variable check
- Descriptive skip messages

This approach ensures build reliability while maintaining development productivity! 🚀
