# 🔄 BusBuddy Google Drive Quick Setup

Since your BusBuddy folder is synced to Google Drive, setting up on your other laptop is incredibly simple!

## ⚡ Super Quick Setup (5 minutes)

### Step 1: Install Prerequisites
```powershell
# Download and install these (if not already installed):
# - PowerShell 7.5.2: https://github.com/PowerShell/PowerShell/releases/tag/v7.5.2
# - .NET 8.0 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
# - VS Code: https://code.visualstudio.com/
# - Google Drive Desktop: https://drive.google.com/drive/download/
```

### Step 2: Wait for Google Drive Sync
```powershell
# Make sure Google Drive has fully synced the BusBuddy folder
# Check: File Explorer → Google Drive → Look for BusBuddy folder
# Verify: All files are downloaded (no cloud icons)
```

### Step 3: Navigate and Run Setup
```powershell
# Open PowerShell and navigate to your synced folder
cd "C:\Users\[YOUR_USERNAME]\Google Drive\BusBuddy"
# OR if using Google Drive for Desktop:
cd "G:\My Drive\BusBuddy"

# Run the automated setup
pwsh -ExecutionPolicy Bypass -File "setup-environment.ps1"
```

### Step 4: Open VS Code
```powershell
# Open VS Code in the BusBuddy directory
code .
```

### Step 5: Test Everything Works
```powershell
# Open a new VS Code terminal and run:
bbHealth
bbCommands

# You should see:
# 🚌 BusBuddy Enhanced Development Profile v2.0
#    Tool-First Development Environment
```

## 🎯 What You Get Automatically

Because everything is synced via Google Drive:

✅ **All configuration files** - `.vscode/tasks.json`, `.vscode/settings.json`
✅ **Enhanced PowerShell profile** - `AI-Assistant/Scripts/load-bus-buddy-profile.ps1`
✅ **File debugging tools** - `Tools/Scripts/BusBuddy-File-Debugger.ps1`
✅ **Build configurations** - `Directory.Build.props`, `global.json`
✅ **All documentation** - This guide, enhanced guide, etc.
✅ **Project files** - Solution, projects, source code

## 🚀 Ready to Go Commands

After setup, you'll have these commands immediately available:

```powershell
# Development workflow
bb-build -FormatFirst           # Enhanced build with auto-formatting
bb-run -BuildFirst -FormatFirst # Complete workflow: format → build → run
# Debug utilities
bbCatchErrors                   # Enhanced error capture
bbCaptureRuntimeErrors          # Comprehensive runtime error monitoring

# Utilities
bbHealth                        # Check environment health
bbCommands                      # Show all available commands
```

## 🔧 If You Need to Install VS Code Extensions

```powershell
# Essential extensions (run these if needed)
code --install-extension ms-vscode.powershell
code --install-extension ms-dotnettools.csharp
code --install-extension spmeesseman.vscode-taskexplorer
code --install-extension ms-vscode.vscode-xml
```

## 🎉 Success Indicators

You'll know it's working when:

1. **VS Code terminal shows**: `🚌 BusBuddy Enhanced Development Profile v2.0`
2. **`bbHealth` passes**: All green checkmarks
3. **Task Explorer shows**: Enhanced BB tasks with 🔧, 🎨, ✅, 🚀 icons
4. **Commands work**: `bbCommands` shows all available functions

## 💡 Pro Tips

- **Keep Google Drive running** for continuous sync
- **Use `bb-` commands** instead of manual fixes (tool-first approach)
- **Check `.vscode/tasks.json`** for all available enhanced tasks
- **Commit changes** to keep both machines in sync

---

**Total Setup Time**: ~5 minutes (mostly waiting for software downloads)
**Sync Method**: Google Drive (automatic)
**Result**: Identical development environment on both machines! 🚌✨
