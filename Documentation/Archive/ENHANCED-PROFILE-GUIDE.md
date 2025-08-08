# BusBuddy Enhanced Development Profile - Quick Reference

## Overview

This enhanced PowerShell profile emphasizes **tool-first development** where you should default to using the sophisticated tools you've developed over days rather than attempting manual fixes.

## Core Principle

**🎯 ALWAYS use your developed tools over manual fixes**

You spent days developing sophisticated file debugging and formatting tools. These should be your first choice, not manual code edits.

## Key Commands

### File Operations (Tool-First)

```powershell
# Debug and fix files using your developed tool (PREFERRED approach)
bb-debug-files -AutoFix -Verbose

# Format specific file types
bb-format-files -Pattern "**/*.cs"      # C# files only
bb-format-files -Pattern "**/*.xaml"    # XAML files only
bb-format-files                         # All C# and XAML files

# Validate without changes
bb-validate-files -Verbose

# Debug specific files
bb-debug-files -AutoFix -Pattern "BusBuddy.WPF/**/*.cs"
```

### Enhanced Build & Run

```powershell
# Enhanced build with automatic file formatting (RECOMMENDED)
bb-build -FormatFirst

# Quick build without formatting
bb-build

# Clean build
bb-build -Clean -FormatFirst

# Complete workflow: format → build → run
bb-run -BuildFirst -FormatFirst

# Quick run (if already built)
bb-run
```

### Utilities

```powershell
# Show all commands
bb-help

# Check development environment health
bb-health

# Clean build artifacts
bb-clean

# Restore packages
bb-restore

# Open files/project in VS Code
bb-open                    # Open project root
bb-open MainWindow.xaml    # Open specific file
```

## VS Code Task Integration

The enhanced profile integrates with VS Code tasks. Use **Task Explorer** to run:

### 🔧 File Operations
- **🔧 BB Enhanced: Debug & Format Files** - Uses developed tool to debug and format all files
- **🎨 BB Enhanced: Format Files Only** - Uses developed tool for formatting only
- **✅ BB Enhanced: Validate Files Only** - Uses developed tool to validate without changes

### 🚀 Build & Run
- **🚀 BB Enhanced: Build with Auto-Format** - Auto-formats then builds (DEFAULT)
- **🏃 BB Enhanced: Run with Auto-Format & Build** - Complete workflow

### 🔍 Diagnostics
- **🔍 BB Enhanced: Health Check** - Comprehensive environment check

## File Processing Workflow

When working with C# and XAML files:

1. **FIRST CHOICE**: Use `bb-debug-files -AutoFix` (your developed tool)
2. **Edit files as needed**
3. **BEFORE COMMITTING**: Run `bb-debug-files -AutoFix` again
4. **Build with**: `bb-build -FormatFirst`

## Tool Advantages

Your developed `BusBuddy-File-Debugger.ps1` provides:

- ✅ **Comprehensive C# analysis** - Checks for nullable references, async patterns, disposable usage
- ✅ **Advanced XAML validation** - Resource usage, binding patterns, styling best practices
- ✅ **Automated fixes** - Applies fixes automatically when possible
- ✅ **Integration with dotnet format** - Uses Roslyn analyzers for C# formatting
- ✅ **Detailed reporting** - Generates comprehensive reports with line numbers and suggestions
- ✅ **Best practices enforcement** - Follows documented BusBuddy coding standards

## Profile Features

### Automatic Environment Validation
- Checks PowerShell 7.5+ requirement
- Validates .NET 8.0+ installation
- Verifies project structure
- Confirms tool availability

### Enhanced Error Handling
- Graceful degradation when tools unavailable
- Clear status messages with emoji indicators
- Proper error reporting and recovery

### Performance Optimized
- Fast loading with minimal overhead
- Efficient tool integration
- Background process support

## Quick Start

1. **Load Profile**: `& '.\AI-Assistant\Scripts\load-bus-buddy-profile.ps1'`
2. **Check Health**: `bb-health`
3. **Debug All Files**: `bb-debug-files -AutoFix -Verbose`
4. **Enhanced Build**: `bb-build -FormatFirst`
5. **Run Application**: `bb-run`

## Integration with Existing Workflow

The enhanced profile works with your existing:
- ✅ **BusBuddy-File-Debugger.ps1** - Primary file processing tool
- ✅ **GitHub automation scripts** - Build and deployment workflows
- ✅ **VS Code tasks** - Task Explorer integration
- ✅ **PowerShell 7.5.2 features** - Parallel processing, ternary operators, etc.

## Best Practices

1. **Always run file debugger on edited files**
2. **Use auto-format builds for consistency**
3. **Leverage tool reporting for quality insights**
4. **Check health regularly with bb-health**
5. **Use Task Explorer for consistent execution**

## Troubleshooting

If tools aren't working:

```powershell
# Check environment health
bb-health

# Verify tool paths
Test-Path "Tools\Scripts\BusBuddy-File-Debugger.ps1"

# Reload profile
. '.\AI-Assistant\Scripts\load-bus-buddy-profile.ps1'

# Check PowerShell version
$PSVersionTable.PSVersion
```

Remember: **Your developed tools represent days of sophisticated work. Use them as your primary approach rather than manual fixes.**
