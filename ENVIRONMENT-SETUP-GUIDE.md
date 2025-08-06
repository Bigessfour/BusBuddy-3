# BusBuddy Development Environment Setup Guide

This guide will help you replicate the exact BusBuddy development environment on another machine, preserving all configurations, tools, and workflow optimizations.

## 📋 Prerequisites

### Required Software
1. **PowerShell 7.5.2** - Essential for all development scripts
   - Download: https://github.com/PowerShell/PowerShell/releases/tag/v7.5.2
   - Verify: `pwsh --version` should show 7.5.2

2. **Visual Studio Code** (or VS Code Insiders)
   - Download: https://code.visualstudio.com/
   - Insiders: https://code.visualstudio.com/insiders/

3. **.NET 8.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify: `dotnet --version` should show 8.0.x

4. **Git for Windows**
   - Download: https://git-scm.com/download/win
   - Ensure git is in PATH

### Required VS Code Extensions
Install these extensions for optimal workflow:

```bash
# Core Extensions
code --install-extension ms-vscode.powershell
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.vscode-dotnet-runtime

# XML/XAML Support
code --install-extension ms-vscode.vscode-xml
code --install-extension ms-dotnettools.vscode-dotnet-pack

# Task Management (CRITICAL)
code --install-extension spmeesseman.vscode-taskexplorer

# Optional but Recommended
code --install-extension ms-vscode.vscode-json
code --install-extension GitHub.copilot
code --install-extension GitHub.copilot-chat
```

## 🚀 Environment Setup Steps

### Option A: Google Drive Sync (Recommended - Fastest Setup)

Since your BusBuddy folder is synced to Google Drive, this is the easiest approach:

```powershell
# 1. Ensure Google Drive is installed and synced on new laptop
# 2. Wait for complete sync of BusBuddy folder
# 3. Navigate to the synced folder
cd "C:\Users\[YOUR_USERNAME]\Google Drive\BusBuddy"
# OR if using Google Drive for Desktop:
cd "G:\My Drive\BusBuddy"

# 4. Verify sync completion
Get-ChildItem -Recurse | Measure-Object | Select-Object Count
# Should show same file count as original machine
```

**Google Drive Sync Benefits:**
- ✅ All configuration files already present
- ✅ Enhanced profiles ready to use
- ✅ VS Code settings pre-configured
- ✅ All tools and scripts included
- ✅ No git clone needed

### Option B: Clone the Repository (Alternative)
```bash
# Clone to exact same location for consistency
cd C:\Users\[YOUR_USERNAME]\Desktop
git clone [YOUR_BUSBUDDY_REPO_URL] BusBuddy
cd BusBuddy
```

### Step 2: PowerShell Execution Policy
```powershell
# Set execution policy to allow scripts
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Step 3: Quick Verification (Google Drive Sync)

If using Google Drive sync, verify everything is ready:

```powershell
# Navigate to your synced BusBuddy folder
cd "C:\Users\[YOUR_USERNAME]\Google Drive\BusBuddy"
# OR: cd "G:\My Drive\BusBuddy"

# Quick automated setup check
pwsh -ExecutionPolicy Bypass -File "setup-environment.ps1"

# This will verify:
# ✅ All required files are synced
# ✅ PowerShell profile works
# ✅ Build system functions
# ✅ VS Code configuration is ready
```

### Step 3: VS Code Workspace Configuration

**For Google Drive Sync Users**: Your VS Code settings should already be configured! Just verify they're working.

**For Git Clone Users**: Copy these exact VS Code settings to maintain consistency:

**`.vscode/settings.json`**:
```json
{
  "terminal.integrated.profiles.windows": {
    "PowerShell 7.5.2": {
      "path": "pwsh.exe",
      "args": ["-NoProfile", "-NoExit", "-Command",
        "& '.\\AI-Assistant\\Scripts\\load-bus-buddy-profile.ps1';"]
    }
  },
  "terminal.integrated.defaultProfile.windows": "PowerShell 7.5.2",
  "powershell.scriptAnalysis.enable": true,
  "powershell.codeFormatting.preset": "OTBS",
  "files.autoSave": "afterDelay",
  "files.autoSaveDelay": 1000,
  "dotnet.completion.showCompletionItemsFromUnimportedNamespaces": true,
  "omnisharp.enableEditorConfigSupport": true,
  "omnisharp.enableRoslynAnalyzers": true
}
```

**Key VS Code Tasks** (already configured in `.vscode/tasks.json`):
- 🔧 BB Enhanced: Debug & Format Files
- 🎨 BB Enhanced: Format Files Only
- ✅ BB Enhanced: Validate Files Only
- 🚀 BB Enhanced: Build with Auto-Format
- 🏃 BB Enhanced: Run with Auto-Format & Build
- 🔍 BB Enhanced: Health Check

## 🛠️ Core Files to Verify

### Essential Scripts
Ensure these files exist and are executable:

1. **`AI-Assistant/Scripts/load-bus-buddy-profile.ps1`** - Main development profile
2. **`Tools/Scripts/BusBuddy-File-Debugger.ps1`** - File debugging tool
3. **`Tools/Scripts/GitHub/BusBuddy-GitHub-Automation.ps1`** - GitHub integration
4. **`ENHANCED-PROFILE-GUIDE.md`** - Profile usage guide

### Configuration Files
1. **`.vscode/tasks.json`** - Enhanced task definitions
2. **`.vscode/settings.json`** - VS Code configuration
3. **`Directory.Build.props`** - .NET build properties
4. **`global.json`** - .NET version targeting

## 🎯 Workflow Setup

### 1. Load the Enhanced Profile
```powershell
# Test profile loading
pwsh -ExecutionPolicy Bypass -Command "Set-Location 'C:\Users\[USERNAME]\Desktop\BusBuddy'; . '.\AI-Assistant\Scripts\load-bus-buddy-profile.ps1'"
```

### 2. Verify Environment Health
```powershell
# After profile loads, run health check
bb-health
```

### 3. Test Tool Integration
```powershell
# Test file debugging tool
bb-debug-files -Pattern "**/*.cs" -AutoFix -Verbose

# Test build system
bb-build -FormatFirst

# Test application run
bb-run -BuildFirst
```

## 📁 Directory Structure to Replicate

```
BusBuddy/
├── AI-Assistant/
│   ├── Scripts/
│   │   └── load-bus-buddy-profile.ps1    # CRITICAL: Main profile
│   └── Core/
├── Tools/
│   └── Scripts/
│       ├── BusBuddy-File-Debugger.ps1    # CRITICAL: File tool
│       └── GitHub/
│           └── BusBuddy-GitHub-Automation.ps1
├── BusBuddy.WPF/                         # Main WPF project
├── BusBuddy.Core/                        # Core library
├── .vscode/
│   ├── tasks.json                        # CRITICAL: Enhanced tasks
│   └── settings.json                     # VS Code config
├── BusBuddy.sln                          # Solution file
├── ENHANCED-PROFILE-GUIDE.md             # Profile guide
└── ENVIRONMENT-SETUP-GUIDE.md            # This file
```

## ⚙️ Key Configuration Values

### PowerShell Profile Configuration
The enhanced profile uses these key settings:
- **ProjectRoot**: Auto-detected from script location
- **ToolsPath**: `Tools\Scripts` relative to project root
- **PreferTools**: `$true` (always use developed tools over manual fixes)
- **AutoFormat**: `$true` (auto-format files during builds)

### .NET Configuration
- **Target Framework**: net8.0-windows
- **PowerShell Version**: 7.5+
- **Build Configuration**: Debug (default)

## 🔧 Essential Commands Reference

After setup, these commands should work identically:

### File Operations (Tool-First Approach)
```powershell
bb-debug-files -AutoFix -Verbose    # Debug and fix all files using developed tool
bb-format-files                     # Format files using integrated tool
bb-validate-files                   # Validate files without changes
```

### Build & Run
```powershell
bb-build -FormatFirst               # Enhanced build with auto-formatting
bb-run -BuildFirst -FormatFirst     # Complete workflow: format → build → run
bb-test                            # Run tests
```

### Utilities
```powershell
bb-health                          # Check development environment
bb-help                            # Show all commands
bb-open [file]                     # Open files in VS Code
```

## 🚨 Critical Migration Points

### 1. PowerShell Profile Loading
**MUST WORK**: The profile loading system is the foundation
```powershell
# This command must execute without errors:
. '.\AI-Assistant\Scripts\load-bus-buddy-profile.ps1'
```

### 2. Task Explorer Integration
**REQUIRED**: Install Task Explorer extension and verify tasks appear:
- Open Command Palette (`Ctrl+Shift+P`)
- Type "Task Explorer"
- Verify enhanced BB tasks are visible

### 3. File Debugger Tool
**CRITICAL**: The file debugger must be accessible:
```powershell
# Verify this path exists:
Test-Path "Tools\Scripts\BusBuddy-File-Debugger.ps1"
```

### 4. VS Code Terminal Integration
**ESSENTIAL**: Terminal should auto-load profile:
- Open new VS Code terminal
- Should see: "🚌 BusBuddy Enhanced Development Profile v2.0"
- Commands like `bb-help` should work immediately

## 🔍 Troubleshooting

### Common Issues and Solutions

#### Profile Won't Load
```powershell
# Check execution policy
Get-ExecutionPolicy -List

# Check file paths
Get-ChildItem -Path "AI-Assistant\Scripts\" -Filter "*.ps1"

# Test manual load
pwsh -ExecutionPolicy Bypass -File "AI-Assistant\Scripts\load-bus-buddy-profile.ps1"
```

#### VS Code Tasks Missing
1. Verify Task Explorer extension is installed
2. Check `.vscode/tasks.json` exists
3. Reload VS Code window (`Ctrl+Shift+P` → "Reload Window")

#### File Debugger Not Found
```powershell
# Verify tool exists
ls "Tools\Scripts\BusBuddy-File-Debugger.ps1"

# Check permissions
Get-Acl "Tools\Scripts\BusBuddy-File-Debugger.ps1"
```

#### .NET Build Issues
```powershell
# Verify .NET version
dotnet --version

# Check project files
dotnet build --verbosity diagnostic
```

## 🎯 Verification Checklist

After setup, verify these work:

- [ ] PowerShell 7.5.2 installed and accessible via `pwsh`
- [ ] .NET 8.0 SDK installed (`dotnet --version`)
- [ ] VS Code with required extensions
- [ ] Profile loads: `. '.\AI-Assistant\Scripts\load-bus-buddy-profile.ps1'`
- [ ] Health check passes: `bb-health`
- [ ] File debugger works: `bb-debug-files -AutoFix`
- [ ] Build system works: `bb-build -FormatFirst`
- [ ] Application runs: `bb-run`
- [ ] VS Code tasks appear in Task Explorer
- [ ] Terminal auto-loads profile

## 📚 Additional Resources

### Documentation Files
- `ENHANCED-PROFILE-GUIDE.md` - Detailed profile usage
- `README.md` - Project overview
- `.vscode/tasks.json` - Task definitions reference

### Tool Documentation
- PowerShell 7.5.2: https://docs.microsoft.com/powershell/
- .NET 8.0: https://docs.microsoft.com/dotnet/
- VS Code: https://code.visualstudio.com/docs

## 🎉 Success Indicators

Your environment is correctly set up when:

1. **Opening VS Code terminal shows**:
   ```
   🚌 BusBuddy Enhanced Development Profile v2.0
      Tool-First Development Environment
   ```

2. **`bb-help` command displays**:
   ```
   🚌 BUSBUDDY ENHANCED COMMANDS
   ═══════════════════════════════════════
   ```

3. **Task Explorer shows enhanced BB tasks** with 🔧, 🎨, ✅, 🚀 icons

4. **Health check passes**: `bb-health` returns all green checkmarks

5. **File operations work**: `bb-debug-files` executes without errors

## 🔄 Sync Strategy

To keep environments in sync:

1. **Version Control**: Commit all configuration changes to git
2. **Profile Updates**: Any profile modifications should be committed
3. **Tool Updates**: Keep `Tools/Scripts/` directory synchronized
4. **VS Code Settings**: Commit `.vscode/` directory changes

---

**Last Updated**: July 23, 2025
**Environment Version**: Enhanced Profile v2.0
**PowerShell Version**: 7.5.2 Required
**Target Framework**: .NET 8.0
