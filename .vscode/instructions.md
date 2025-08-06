# 🚌 BusBuddy - School Transportation Management System

## 🎯 **Project Overview**

BusBuddy is a transportation management system designed to help school districts efficiently manage their bus fleets, drivers, routes, and schedules. Our goal is to create an intuitive application that improves safety, reduces administrative overhead, and optimizes transportation resources.

## 📋 **Table of Contents**

- [Quick Start Guide](#-quick-start-guide)
- [Project Status & Roadmap](#-project-status--roadmap)
- [Development Environment Setup](#-development-environment-setup)
- [Problem Resolution Approaches](#-problem-resolution-approaches)
- [Git & Repository Tips](#-git--repository-tips)
- [VS Code Integration](#-vs-code-integration)
- [Syncfusion Implementation](#-syncfusion-implementation)
- [Debugging & Troubleshooting](#-debugging--troubleshooting)
- [Quick Reference Commands](#-quick-reference-commands)
- [Application Architecture](#-application-architecture)

## 🚀 **Quick Start Guide**

### Basic Development Setup (5 Minutes)

1. **Open the Project**:
   ```
   Open VS Code → Open Folder → Navigate to BusBuddy folder
   ```

2. **Build the Solution**:
   - Use VS Code Task: `⌨️ Ctrl+Shift+P → "Tasks: Run Task" → "Direct: Build Solution (CMD)"`
   - Or use PowerShell: `dotnet build BusBuddy.sln`

3. **Run the Application**:
   - Use VS Code Task: `⌨️ Ctrl+Shift+P → "Tasks: Run Task" → "Direct: Run Application (FIXED)"`
   - Or use PowerShell: `dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj`

4. **PowerShell Helpers** (Optional but Recommended):
   ```powershell
   pwsh -ExecutionPolicy Bypass -File "load-bus-buddy-profiles.ps1"
   bb-health -Quick
   ```

## 🎯 **Project Status & Roadmap**

### Current Status: Phase 2
- ✅ **Phase 1 Completed**: MainWindow → Dashboard → 3 Core Views (Drivers, Vehicles, Activities)
- 🔄 **Phase 2 In Progress**:
  - Enhancing UI/UX with consistent Syncfusion styling
  - Improving MVVM architecture
  - Expanding test coverage
  - Optimizing performance

### Key Features
- **Drivers Management**: Personnel records, qualifications, scheduling
- **Vehicle Fleet**: Bus inventory, maintenance records, assignments
- **Route Planning**: Efficient route creation and management
- **Activity Scheduling**: Field trips, special events, non-standard routes

### Next Milestone Goals
- Complete route management interface
- Implement maintenance scheduling
- Add reporting dashboard
- Enhance data visualization

## � **Development Environment Setup**

BusBuddy emphasizes direct, simple development approaches over complex automation.

### Standard Approach (Recommended)
```powershell
# Build the solution (direct and reliable)
dotnet build BusBuddy.sln

# Run the application (direct execution)
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj

# Clean the solution (when needed)
dotnet clean BusBuddy.sln
```

### Direct Development Workflow
The BusBuddy development workflow prioritizes simplicity and directness:

```powershell
# Direct build approach (no complex automation needed)
dotnet build BusBuddy.sln

# Direct run approach
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj

# Direct test approach (when tests are available)
dotnet test
```

**Note**: Complex PowerShell automation has been simplified in favor of direct .NET CLI commands for reliability and simplicity.

## � **Problem Resolution Approaches**

When encountering issues, you have multiple resolution paths:

### Standard Troubleshooting
1. **Check Build Errors**: Look at specific error messages and line numbers
2. **Use VS Code Debugging**: Set breakpoints and step through code
3. **Review Output Window**: Check for additional diagnostics and logs
4. **Clean and Rebuild**: Clear artifacts with `dotnet clean` and rebuild

### Using PowerShell Helpers
If you've loaded the PowerShell environment, these commands can help:

## � **Problem Resolution Approaches**

When encountering issues, follow these resolution paths:

### REQUIRED: PowerShell-Based Troubleshooting
```powershell
# Health check (always the first step)
bb-health

# Build with detailed output
bb-build -Verbosity detailed

# Run with debug logging
bb-run -EnableDebug

# Export diagnostics (for sharing issues)
bb-debug-export

# Advanced diagnostics
bb-diagnostic
```

### Emergency Fallback Only
Only if PowerShell environment cannot be loaded:
1. **Check Build Errors**: Run `dotnet build BusBuddy.sln` and review errors
2. **Use VS Code Debugging**: Set breakpoints and step through code
3. **Review Output Window**: Check for additional diagnostics and logs
4. **Clean and Rebuild**: Clear artifacts with `dotnet clean` and rebuild

### Problem Resolution Strategy
1. **Try small fixes first** - Target specific errors before large changes
2. **Keep what works** - Build incrementally on working components
3. **Ask for help** - Consult team members for complex issues
4. **Document solutions** - Note what worked for future reference

**Remember**: Start with simple solutions before complex ones.

## 🧰 **Git & Repository Standards**

### Git CLI Best Practices for BusBuddy

**Mandatory Git Workflow Standards:**

```bash
# 1. ALWAYS check status before making changes
git status                                    # Required before any operation

# 2. SELECTIVE STAGING (Preferred over git add .)
git add <specific-files>                      # Stage individual files
git add BusBuddy.Core/Services/              # Stage by component
git add *.cs                                  # Stage by file type
# AVOID: git add . (stages everything, including unwanted files)

# 3. CONVENTIONAL COMMIT MESSAGES (Mandatory)
git commit -m "feat: add driver management API"
git commit -m "fix: resolve CS0103 compilation errors"
git commit -m "docs: update setup instructions"
git commit -m "refactor: organize PowerShell scripts"
git commit -m "style: apply code formatting rules"
git commit -m "test: add integration tests for routes"

# 4. PROPER REMOTE OPERATIONS
git fetch origin                              # Download remote changes
git pull origin main                          # Fetch and merge (preferred)
git push origin main                          # Push to specific branch

# 5. BRANCH MANAGEMENT
git checkout -b feature/bus-routing           # Create feature branch
git checkout main                             # Switch to main
git merge --no-ff feature/bus-routing         # Merge with merge commit
git branch -d feature/bus-routing             # Delete merged branch
```

**PowerShell Git Commands (Windows-Optimized):**

```powershell
# PowerShell-enhanced git operations
git status --porcelain | Where-Object { $_ -match "^.M" }  # Modified files only
git ls-files | Where-Object { $_ -match "\.cs$" }          # Find C# files
(git ls-files | Measure-Object).Count                      # Count tracked files
git log --oneline -10                                      # Last 10 commits
git diff --name-only HEAD~1                                # Files changed in last commit

# File counting and analysis
$TrackedFiles = git ls-files
$CSharpFiles = $TrackedFiles | Where-Object { $_ -like "*.cs" }
Write-Host "Tracked: $($TrackedFiles.Count), C#: $($CSharpFiles.Count)"

# Staged vs unstaged analysis
$StagedFiles = git diff --cached --name-only
$UnstagedFiles = git diff --name-only
Write-Host "Staged: $($StagedFiles.Count), Unstaged: $($UnstagedFiles.Count)"
```

**Forbidden Git Practices:**

```bash
# ❌ NEVER USE THESE COMMANDS
git add .                                     # Too broad, stages unwanted files
git commit -m "update"                        # Non-descriptive message
git commit -m "fix"                           # Too vague
git push                                      # Missing branch specification
git pull                                      # Missing branch specification
git reset --hard HEAD~5                      # Dangerous data loss
git push --force                              # Can overwrite others' work

# ✅ USE THESE INSTEAD
git add BusBuddy.Core/Services/DriverService.cs  # Specific files
git commit -m "feat: implement driver CRUD operations"  # Descriptive
git push origin main                          # Explicit branch
git pull origin main                          # Explicit branch
git reset --soft HEAD~1                      # Safer rollback
git push --force-with-lease                  # Safer force push
```

## 🧰 **VS Code Integration**

VS Code is the primary development environment for BusBuddy. These configurations help streamline development:

### Task Integration
Use VS Code tasks to simplify common operations:

| VS Code Task | Description | How to Access |
|-------------|-------------|---------------|
| `Direct: Build Solution (CMD)` | Build the solution | Ctrl+Shift+P → "Tasks: Run Task" |
| `Direct: Run Application (FIXED)` | Run the application | Ctrl+Shift+P → "Tasks: Run Task" |
| `BB: Run App` | Run with PowerShell helpers | Ctrl+Shift+P → "Tasks: Run Task" |
| `�️ BB: Dependency Security Scan` | Security scan | Ctrl+Shift+P → "Tasks: Run Task" |

### Recommended Extensions
These extensions enhance the development experience:
- **PowerShell** (ms-vscode.powershell)
- **C# Dev Kit** (ms-dotnettools.csdevkit)
- **Task Explorer** (spmeesseman.vscode-taskexplorer)

### Debugging Configuration
For debugging the application, use this launch configuration:

```json
{
  "name": "Debug BusBuddy",
  "type": "coreclr",
  "request": "launch",
  "preLaunchTask": "Build Solution",
  "program": "${workspaceFolder}/BusBuddy.WPF/bin/Debug/net8.0-windows/BusBuddy.WPF.dll",
  "args": [],
  "cwd": "${workspaceFolder}/BusBuddy.WPF",
  "stopAtEntry": false,
  "console": "internalConsole"
}
```

**Tip**: Use F5 to start debugging after adding this to your launch.json.

## 🎨 **Syncfusion Controls Implementation**

Syncfusion provides the UI components for BusBuddy's interface:

### Key Controls
- **DockingManager**: Main layout for dashboard panels
- **DataGrid**: For displaying driver, vehicle, and route data
- **RibbonControl**: Navigation and command interface
- **Charts**: Data visualization for analytics

### Required Setup
```csharp
// In App.xaml.cs - Register license before UI initialization
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR_LICENSE_KEY");
```

### Theme Implementation
```xml
<!-- In App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/Syncfusion.Themes.FluentDark.WPF;component/fluent.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

### Documentation Resources
- [Syncfusion WPF Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
- [Control Gallery](https://help.syncfusion.com/wpf/control-gallery)
- [Theme Documentation](https://help.syncfusion.com/wpf/themes/getting-started)

## 🐛 **Debugging & Troubleshooting**

### 🚨 **CS0103 Error Prevention & Resolution**

**CRITICAL**: CS0103 "The name 'X' does not exist in the current context" errors are common in WPF projects. Here's how to handle them:

#### **Root Causes & Solutions**

| **Error Pattern** | **Root Cause** | **Solution** | **Prevention** |
|---|---|---|---|
| `InitializeComponent()` not found | WPF auto-generated files not created | Clean + Rebuild project | Regular clean builds |
| Missing using directives | Namespace not imported | Add proper using statements | Use IDE suggestions |
| XAML elements not recognized | Designer files out of sync | Rebuild, restart VS Code | Avoid manual XAML edits |
| Syncfusion controls not found | Package references missing | Check project file packages | Verify in Directory.Build.props |

#### **CS0103 Emergency Resolution Protocol**

```powershell
# 1. IMMEDIATE ACTIONS (90% success rate)
dotnet clean BusBuddy.WPF/BusBuddy.WPF.csproj
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj

# 2. IF STILL FAILING (Advanced)
dotnet restore --force --no-cache
dotnet clean
dotnet build --verbosity detailed

# 3. NUCLEAR OPTION (99% success rate)
Remove-Item -Recurse -Force BusBuddy.WPF/bin/
Remove-Item -Recurse -Force BusBuddy.WPF/obj/
dotnet restore
dotnet build
```

#### **WPF-Specific CS0103 Issues**

**Issue**: `InitializeComponent()` CS0103 errors in .xaml.cs files
- **Cause**: WPF auto-generated designer files (.g.cs) not created
- **Solution**: Clean + rebuild (forces regeneration of designer files)
- **Note**: Build succeeds, but IntelliSense shows false errors (OmniSharp limitation)

**Issue**: XAML element names not recognized in code-behind
- **Cause**: x:Name attributes not generating partial class properties
- **Solution**: Verify XAML syntax, rebuild project
- **Prevention**: Use consistent naming conventions

#### **IntelliSense vs Build Truth**

**IMPORTANT**: Due to OmniSharp deprecation for XAML issues:
- ✅ **Trust the build output** - If `dotnet build` succeeds, the code is correct
- ⚠️ **Ignore IntelliSense errors** - Red squiggles may be false positives
- 🎯 **Focus on compile errors** - Only fix actual compilation failures

### Common Issues & Solutions

| Issue Type | Troubleshooting Steps | Resources |
|------------|----------------------|-----------|
| **CS0103 Errors** | Follow CS0103 Emergency Protocol above | Build output, not IntelliSense |
| **Build Errors** | Check exact error message and line number | VS Code Problems panel |
| **UI Rendering** | Verify Syncfusion theme registration in App.xaml.cs | Syncfusion documentation |
| **Runtime Crashes** | Use try/catch blocks and log exceptions | Exception details window |
| **Database Issues** | Check connection string, verify migrations | SQL Server explorer |

### VS Code Debug Techniques

1. **Set breakpoints**: Click in the gutter to the left of line numbers
2. **Use watch window**: Add variables to monitor during execution
3. **Step through code**: Use F10 (step over) and F11 (step into)
4. **Monitor output**: Check the Debug Console for logs and errors

### Advanced Diagnostics

If you've loaded the PowerShell helpers, additional diagnostics are available:

```powershell
# Health check for common issues
bb-health

# Capture runtime errors in real-time
bb-debug-stream

# Export diagnostic information for sharing
bb-debug-export
```

**Remember**: Most issues can be solved with standard VS Code debugging!

---
## 🔑 **Quick Reference Commands**

### Essential Commands
```powershell
# Build and run (standard)
dotnet build BusBuddy.sln
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj

# PowerShell helpers (optional)
pwsh -ExecutionPolicy Bypass -File "load-bus-buddy-profiles.ps1"
bb-health
bb-build
bb-run
```

### Git Essentials
```powershell
git add .
git commit -m "Descriptive message"
git push
```

## 🏗️ **Application Architecture**

BusBuddy follows a layered architecture pattern with clean separation of concerns:

### Project Structure
```
BusBuddy/
├── BusBuddy.Core/          # Business logic layer
│   ├── Models/             # Domain entities
│   ├── Services/           # Business services
│   ├── Data/               # Data access
│   └── Migrations/         # EF Core migrations
├── BusBuddy.WPF/           # Presentation layer
│   ├── ViewModels/         # MVVM ViewModels
│   ├── Views/              # XAML Views
│   ├── Controls/           # Custom controls
│   └── Services/           # UI services
└── BusBuddy.Tests/         # Test projects
```

### Key Design Patterns

#### MVVM Implementation
- **ViewModels**: Handle UI logic and state management
- **Commands**: Use RelayCommand pattern for UI actions
- **Data Binding**: Two-way binding to model properties
- **View Navigation**: Frame or ContentControl-based navigation

#### Core Data Concepts
- **Entity Framework**: Code-first database access
- **Repository Pattern**: Centralized data access
- **Business Services**: Implement domain logic
- **DTOs**: Transfer objects between layers

### Key Components
- **Drivers**: Personnel management
- **Vehicles**: Bus fleet management
- **Routes**: Transportation route planning
- **Activities**: Special events and field trips

### Syncfusion Integration Points
- **Main Dashboard**: DockingManager with multiple panels
- **Data Displays**: SfDataGrid for tabular data
- **Navigation**: RibbonControl for app-wide navigation
- **Visualization**: ChartControl for analytics

### Recommended Resources
- [MVVM Documentation](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm)
- [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [Syncfusion Guides](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)

## � **C# and WPF Coding Standards**

### 🚨 **CS0103 Error Prevention Standards**

**MANDATORY PRACTICES** to prevent CS0103 "name does not exist" errors:

#### **WPF-Specific Standards**

```csharp
// ✅ CORRECT: Always use partial class for WPF code-behind
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent(); // Auto-generated method
        // Your code here
    }
}

// ❌ WRONG: Missing partial keyword
public class DashboardView : UserControl  // Missing 'partial'
```

#### **Using Directive Standards**

```csharp
// ✅ CORRECT: Complete using statements for BusBuddy
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels;

// ❌ WRONG: Missing critical namespaces
using System;  // Insufficient for WPF
```

#### **XAML-CodeBehind Synchronization**

```xml
<!-- ✅ CORRECT: XAML with proper namespace and class -->
<UserControl x:Class="BusBuddy.WPF.Views.DashboardView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <Button x:Name="SaveButton" Click="SaveButton_Click" />
</UserControl>
```

```csharp
// ✅ CORRECT: Matching code-behind
namespace BusBuddy.WPF.Views
{
    public partial class DashboardView : UserControl  // Namespace must match XAML
    {
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // SaveButton is auto-generated from x:Name
        }
    }
}
```

#### **Syncfusion Control Standards**

```csharp
// ✅ CORRECT: Proper Syncfusion usage
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.Licensing;

public partial class DriversView : UserControl
{
    public DriversView()
    {
        InitializeComponent();
        
        // Ensure license is registered in App.xaml.cs
        // Use intellisense-friendly control access
        if (DriversDataGrid != null)
        {
            DriversDataGrid.ItemsSource = viewModel.Drivers;
        }
    }
}
```

#### **Build-Time Validation Standards**

```powershell
# ✅ MANDATORY: Clean build validation before committing
dotnet clean BusBuddy.WPF/BusBuddy.WPF.csproj
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj --verbosity minimal

# ✅ REQUIRED: Verify build success
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build successful - ready to commit" -ForegroundColor Green
} else {
    Write-Host "❌ Build failed - fix CS0103 errors" -ForegroundColor Red
}
```

### **Error Resolution Priority**

1. **Build Errors** (CS0103, CS0246) - CRITICAL, blocks compilation
2. **IntelliSense Warnings** - IGNORE if build succeeds (OmniSharp deprecated)
3. **Runtime Errors** - Handle with try/catch blocks
4. **Performance Issues** - Address in Phase 2

### **File Organization Standards**

```
BusBuddy.WPF/Views/
├── Dashboard/
│   ├── DashboardView.xaml      # XAML must have matching class
│   └── DashboardView.xaml.cs   # Must be partial class
├── Driver/
│   ├── DriversView.xaml
│   └── DriversView.xaml.cs
└── Vehicle/
    ├── VehiclesView.xaml
    └── VehiclesView.xaml.cs
```

### **Commit Standards for CS0103 Prevention**

```bash
# ✅ BEFORE COMMITTING: Validate build
git add .
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj

# ✅ COMMIT MESSAGE STANDARDS
git commit -m "fix: resolve CS0103 errors in DashboardView"
git commit -m "feat: add driver management with proper using directives"
git commit -m "refactor: standardize XAML namespace declarations"
```

## �🔍 **PowerShell Development Environment**

The PowerShell development environment is REQUIRED for all development activities:

```powershell
# REQUIRED: Load the PowerShell environment
pwsh -ExecutionPolicy Bypass -File "load-bus-buddy-profiles.ps1"

# Most useful commands
bb-health      # Check project health
bb-build       # Build the solution
bb-run         # Run the application
bb-diagnostic  # Comprehensive diagnostics
bb-dev-session # Complete development session setup
```

**Remember**: Always use the PowerShell environment for consistent development workflow.
### 📝 **PowerShell Coding Standards**

BusBuddy uses simple, direct PowerShell approaches:

#### 🔄 **Direct PowerShell Approach**

Focus on simple, reliable PowerShell functions:

```powershell
# Direct approach - preferred
function Get-BusBuddyStatus {
    if (Test-Path "BusBuddy.sln") {
        Write-Host "✅ BusBuddy project found" -ForegroundColor Green
        return $true
    } else {
        Write-Host "❌ BusBuddy project not found" -ForegroundColor Red
        return $false
    }
}

# Simple build function
function Build-BusBuddy {
    param([string]$Configuration = "Debug")
    
    Write-Host "🔨 Building BusBuddy in $Configuration mode..." -ForegroundColor Cyan
    $result = & dotnet build BusBuddy.sln --configuration $Configuration
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Build successful" -ForegroundColor Green
        return $true
    } else {
        Write-Host "❌ Build failed" -ForegroundColor Red
        return $false
    }
}
```
function Invoke-BuildProcess { ... }      # NOT Execute-BuildProcess
function Get-SystemHealth { ... }         # NOT Check-SystemHealth
```

#### 🔧 **PSScriptAnalyzer Enforcement**

BusBuddy uses PSScriptAnalyzer to enforce these standards:

```powershell
# Validate script with BusBuddy PSScriptAnalyzer settings
Invoke-ScriptAnalyzer -Path "YourScript.ps1" -Settings ".vscode/PSScriptAnalyzerSettings.psd1"
```

The VS Code task `� BB: Mandatory PowerShell 7.5.2 Syntax Check` will verify all scripts against these standards.
