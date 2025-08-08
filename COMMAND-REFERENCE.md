# � BusBuddy PowerShell Command Reference

**Comprehensive guide to all available PowerShell commands for BusBuddy development**

## 📋 **Command Categories**

### **�️ Core Development Commands**

#### **bbBuild**
```powershell
bbBuild
```
- **Purpose**: Build the entire BusBuddy solution
- **What it does**: Runs `dotnet build BusBuddy.sln`
- **Output**: Build status, compilation errors/warnings
- **Success criteria**: 0 errors (warnings acceptable during MVP)

#### **bbRun**
```powershell
bbRun
```
- **Purpose**: Launch the BusBuddy WPF application
- **What it does**: Runs `dotnet run --project BusBuddy.WPF`
- **Output**: Application starts with main dashboard
- **Notes**: Application should display student and route management UI

#### **bbTest**
```powershell
bbTest
```
- **Purpose**: Execute the test suite
- **What it does**: Attempts to run `dotnet test`
- **Known Issue**: .NET 9 compatibility problems with Microsoft.TestPlatform.CoreUtilities
- **Alternative**: Use VS Code NUnit Test Runner extension

#### **bbClean**
```powershell
bbClean
```
- **Purpose**: Clean build artifacts
- **What it does**: Runs `dotnet clean BusBuddy.sln`
- **Use case**: Before fresh builds or when resolving build issues

#### **bbRestore**
```powershell
bbRestore
```
- **Purpose**: Restore NuGet packages
- **What it does**: Runs `dotnet restore BusBuddy.sln`
- **Use case**: After cloning repository or package updates

### **🔍 Quality Assurance Commands**

#### **bbHealth**
```powershell
bbHealth
```
- **Purpose**: Comprehensive system health check
- **What it checks**:
  - .NET SDK version and installation
  - PowerShell version
  - Git status
  - Build health
  - Database connectivity
  - Essential files and directories
- **Success output**: All checks show ✅ green
- **Failure indicators**: ❌ red marks with specific issues

#### **bbMvpCheck**
```powershell
bbMvpCheck
```
- **Purpose**: Verify MVP (Minimum Viable Product) readiness
- **What it validates**:
  - Core student management functionality
  - Route assignment capabilities
  - Database operations
  - Essential UI components
  - Application startup
- **Success message**: `"MVP READY! You can ship this!"`
- **Critical for**: Deployment decisions and release readiness

#### **bbAntiRegression**
```powershell
bbAntiRegression
```
- **Purpose**: Prevent regression to deprecated patterns
- **What it scans for**:
  - ❌ `Microsoft.Extensions.Logging` usage (should use Serilog)
  - ❌ Standard WPF controls like `<DataGrid>` (should use Syncfusion)
  - ❌ `Write-Host` in PowerShell (should use proper output streams)
- **Output**: List of violations with file locations
- **Action required**: Fix violations before committing

#### **bbXamlValidate**
```powershell
bbXamlValidate
```
- **Purpose**: Ensure XAML files use only Syncfusion controls
- **What it validates**:
  - Syncfusion namespace declarations
  - No standard WPF controls in XAML
  - Proper control usage patterns
- **Success criteria**: All XAML files comply with Syncfusion standards

### **🎯 XAI Route Optimization Commands**

#### **bbRoutes**
```powershell
bbRoutes
```
- **Purpose**: Run XAI-powered route optimization
- **What it does**: Optimizes bus routes using AI algorithms
- **Integration**: Uses XAI service for intelligent route planning
- **Output**: Optimized route recommendations

#### **bbRouteDemo**
```powershell
bbRouteDemo
```
- **Purpose**: Demonstrate route optimization with sample data
- **What it does**: Runs route optimization using predefined test data
- **Use case**: Testing and demonstration of XAI capabilities

#### **bbRouteStatus**
```powershell
bbRouteStatus
```
- **Purpose**: Check status of route optimization processes
- **What it displays**: Current optimization job status and progress
- **Use case**: Monitoring long-running optimization tasks

### **🚀 Advanced Workflow Commands**

#### **bbDevSession**
```powershell
bbDevSession
```
- **Purpose**: Start complete development environment
- **What it does**:
  - Loads all PowerShell modules
  - Sets up development aliases
  - Configures environment variables
  - Prepares debugging tools
  - Opens VS Code if available
- **Use case**: Beginning of development session

#### **bbQuickTest**
```powershell
bbQuickTest
```
- **Purpose**: Rapid build-test-validate cycle
- **What it executes**:
  1. Clean build artifacts
  2. Build solution
  3. Run tests (if available)
  4. Validate MVP status
- **Use case**: Quick validation during development

#### **bbDiagnostic**
```powershell
bbDiagnostic
```
- **Purpose**: Comprehensive system analysis
- **What it analyzes**:
  - Environment configuration
  - Package versions
  - Build status
  - Database connectivity
  - File system health
  - PowerShell module status
- **Output**: Detailed diagnostic report

#### **bbReport**
```powershell
bbReport
```
- **Purpose**: Generate comprehensive project status report
- **What it includes**:
  - System health summary
  - Build status
  - MVP readiness
  - Code quality metrics
  - Database status
- **Output**: Formatted report for project stakeholders

### **🛠️ Utility Commands**

#### **bbCommands**
```powershell
bbCommands
```
- **Purpose**: List all available BusBuddy commands
- **Output**: Complete command reference with descriptions
- **Use case**: Discovery and reference

#### **bbOpen**
```powershell
bbOpen
```
- **Purpose**: Open BusBuddy project in VS Code
- **What it does**: Launches `code .` in project directory
- **Detects**: VS Code or VS Code Insiders installation

### **🔧 Debug Commands**

#### **bbDebugStart**
```powershell
bbDebugStart
```
- **Purpose**: Start debug filter and monitoring
- **What it does**: Activates real-time debug output filtering
- **Integration**: Uses DebugHelper.StartAutoFilter() from App.xaml.cs

#### **bbDebugExport**
```powershell
bbDebugExport
```
- **Purpose**: Export debug data to JSON
- **Output**: Structured debug information for analysis
- **Use case**: Troubleshooting and performance analysis

## 📊 **Command Usage Patterns**

### **Daily Development Workflow**
```powershell
# Start development session
bbDevSession

# Verify system health
bbHealth

# Make code changes...

# Validate changes
bbBuild
bbAntiRegression
bbXamlValidate
bbMvpCheck

# Commit when MVP check passes
```

### **Quick Build Cycle**
```powershell
# Quick validation
bbQuickTest

# If issues found:
bbClean
bbBuild
bbMvpCheck
```

### **Route Optimization Workflow**
```powershell
# Run route optimization
bbRoutes

# Check status
bbRouteStatus

# Demo with sample data
bbRouteDemo
```

### **Troubleshooting Workflow**
```powershell
# Comprehensive analysis
bbHealth
bbDiagnostic

# If build issues:
bbClean
bbRestore
bbBuild

# Generate report for support
bbReport
```

## ⚠️ **Important Notes**

### **Command Naming**
- All commands use **camelCase** format (e.g., `bbHealth`, not `bb-health`)
- Prefix `bb` identifies BusBuddy-specific commands
- Commands are case-sensitive in PowerShell

### **MVP Requirements**
- `bbMvpCheck` **must** return "MVP READY!" before any deployment
- All anti-regression checks **must** pass before committing
- Build **must** complete with 0 errors (warnings acceptable)

### **Known Issues**
- **Test Platform**: .NET 9 compatibility issues with test runner
  - **Solution**: Use VS Code NUnit Test Runner extension
- **PowerShell**: Some legacy Write-Host violations remain
  - **Status**: 49 violations fixed, 7 remaining (5.4% improvement)

### **Quality Gates**
1. **bbHealth** - All checks must pass
2. **bbBuild** - 0 errors required
3. **bbAntiRegression** - 0 violations allowed
4. **bbXamlValidate** - Syncfusion compliance required
5. **bbMvpCheck** - Must show "MVP READY!"

## 🎯 **Success Criteria**

### **For Development**
- `bbHealth` shows all ✅ green checkmarks
- `bbBuild` completes with 0 errors
- `bbMvpCheck` reports "MVP READY! You can ship this!"

### **For Deployment**
- All quality gates passed
- No anti-regression violations
- XAML validation successful
- MVP functionality verified

## 📚 **Additional Resources**

- **Setup Guide**: `SETUP-GUIDE.md`
- **Development Guide**: `DEVELOPMENT-GUIDE.md`
- **Project Status**: `GROK-README.md`
- **File Guide**: `Documentation/FILE-FETCHABILITY-GUIDE.md`

## 🆘 **Getting Help**

1. **Command Discovery**: `bbCommands`
2. **System Health**: `bbHealth`
3. **Diagnostic Info**: `bbDiagnostic`
4. **Project Status**: `bbReport`
5. **Documentation**: Check `Documentation/` folder

**Examples**:
```powershell
ai-init-project -ProjectType "DotNet"
ai-init-project -ProjectType "NodeJS" -Force
```

## 🔧 File Operations

### `ai-debug-files`
**Purpose**: Universal file debugging and error detection
**Syntax**: `ai-debug-files [-Pattern <pattern>] [-FilePaths <paths>] [-AutoFix] [-ValidateOnly] [-Verbose]`

**Parameters**:
- `-Pattern`: Glob pattern (e.g., `**/*.cs`, `**/*.js`)
- `-FilePaths`: Specific file paths array
- `-AutoFix`: Automatically apply fixes
- `-ValidateOnly`: Check only, don't modify
- `-Verbose`: Detailed output

**Examples**:
```powershell
ai-debug-files -Pattern "**/*.cs" -AutoFix
ai-debug-files -FilePaths @("app.js", "utils.js") -Verbose
ai-debug-files -ValidateOnly
```

**Language Support**:
- **C#**: Roslyn analyzers, code fixes
- **JavaScript/TypeScript**: ESLint, Prettier
- **Python**: flake8, black, mypy
- **PowerShell**: PSScriptAnalyzer
- **XAML**: XAML formatting and validation

---

### `ai-format-files`
**Purpose**: Format files using language-specific formatters
**Syntax**: `ai-format-files [-Pattern <pattern>] [-FilePaths <paths>]`

**Examples**:
```powershell
ai-format-files                      # Format all supported files
ai-format-files -Pattern "**/*.cs"   # Format only C# files
```

**Formatters Used**:
- **C#**: `dotnet format`
- **JavaScript/TypeScript**: `prettier`
- **Python**: `black`
- **PowerShell**: Built-in formatting
- **JSON**: Built-in JSON formatter

---

### `ai-validate-files`
**Purpose**: Validate files without making changes
**Syntax**: `ai-validate-files [-Pattern <pattern>] [-Verbose]`

**Examples**:
```powershell
ai-validate-files -Verbose
ai-validate-files -Pattern "**/*.py"
```

## 🏗️ Build & Test Commands

### `ai-build`
**Purpose**: Build project using appropriate build system
**Syntax**: `ai-build [-Configuration <config>] [-FormatFirst] [-Clean]`

**Parameters**:
- `-Configuration`: Debug, Release (default: Debug)
- `-FormatFirst`: Format files before building
- `-Clean`: Clean before building

**Examples**:
```powershell
ai-build                              # Basic build
ai-build -Configuration Release       # Release build
ai-build -FormatFirst -Clean         # Clean, format, then build
```

**Build Systems**:
- **.NET**: `dotnet build`
- **Node.js**: `npm run build` or `yarn build`
- **Python**: `python setup.py build` or custom build script
- **Generic**: Configurable build command

---

### `ai-test`
**Purpose**: Run tests using project test framework
**Syntax**: `ai-test [-Filter <filter>] [-Coverage] [-Verbose]`

**Examples**:
```powershell
ai-test                        # Run all tests
ai-test -Filter "UnitTests"    # Run specific test filter
ai-test -Coverage              # Run with coverage report
```

**Test Frameworks**:
- **.NET**: `dotnet test`, xUnit, NUnit, MSTest
- **Node.js**: Jest, Mocha, Jasmine
- **Python**: pytest, unittest
- **PowerShell**: Pester

---

### `ai-clean`
**Purpose**: Clean build artifacts and temporary files
**Syntax**: `ai-clean [-Deep]`

**Examples**:
```powershell
ai-clean              # Standard clean
ai-clean -Deep        # Deep clean including caches
```

## 📁 Project Management

### `ai-open`
**Purpose**: Open files or directories in VS Code
**Syntax**: `ai-open <path>`

**Examples**:
```powershell
ai-open MyFile.cs           # Open specific file
ai-open .                   # Open current directory
ai-open "src/components"    # Open specific folder
```

---

### `ai-new-project`
**Purpose**: Create new project with AI-Assistant integration
**Syntax**: `ai-new-project -Type <type> -Name <name> [-Template <template>]`

**Examples**:
```powershell
ai-new-project -Type "DotNet" -Name "MyApp" -Template "wpf"
ai-new-project -Type "NodeJS" -Name "MyAPI" -Template "express"
ai-new-project -Type "Python" -Name "MyScript" -Template "flask"
```

## 🔧 Advanced Commands

### `ai-config`
**Purpose**: Configure AI-Assistant settings
**Syntax**: `ai-config [-Set <key> <value>] [-Get <key>] [-List]`

**Examples**:
```powershell
ai-config -List                          # Show all settings
ai-config -Get "AutoFormat"              # Get specific setting
ai-config -Set "LogLevel" "Verbose"      # Set configuration value
```

---

### `ai-profile`
**Purpose**: Manage AI-Assistant profiles
**Syntax**: `ai-profile [-Save <name>] [-Load <name>] [-List]`

**Examples**:
```powershell
ai-profile -Save "WebDev"       # Save current config as profile
ai-profile -Load "WebDev"       # Load saved profile
ai-profile -List                # List available profiles
```

---

### `ai-tools`
**Purpose**: Manage and configure development tools
**Syntax**: `ai-tools [-List] [-Install <tool>] [-Update <tool>]`

**Examples**:
```powershell
ai-tools -List                  # List available tools
ai-tools -Install "prettier"    # Install specific tool
ai-tools -Update "eslint"       # Update tool to latest version
```

## 🌐 Git Integration Commands

### `ai-git-status`
**Purpose**: Enhanced git status with AI-Assistant integration
**Syntax**: `ai-git-status [-Detailed]`

---

### `ai-git-commit`
**Purpose**: Smart commit with automatic formatting
**Syntax**: `ai-git-commit [-Message <msg>] [-AutoFormat] [-AutoStage]`

**Examples**:
```powershell
ai-git-commit -Message "Add new feature" -AutoFormat
ai-git-commit -AutoStage -AutoFormat  # Auto-generate commit message
```

## 📊 Reporting Commands

### `ai-report`
**Purpose**: Generate project reports and analytics
**Syntax**: `ai-report [-Type <type>] [-Output <path>]`

**Report Types**:
- `health` - Environment and project health
- `files` - File analysis and statistics
- `dependencies` - Dependency analysis
- `coverage` - Test coverage report

**Examples**:
```powershell
ai-report -Type "health"                    # Health report
ai-report -Type "files" -Output "report.md" # File report to file
```

## ⚙️ Configuration Commands

### Environment Variables
AI-Assistant uses these environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `AI_ASSISTANT_PROJECT_TYPE` | Current project type | Auto-detected |
| `AI_ASSISTANT_AUTO_FORMAT` | Auto-format on build | `true` |
| `AI_ASSISTANT_PREFER_TOOLS` | Prefer tools over manual | `true` |
| `AI_ASSISTANT_LOG_LEVEL` | Logging verbosity | `INFO` |
| `AI_ASSISTANT_CACHE_ENABLED` | Enable result caching | `true` |

### Command Aliases
Common aliases for frequently used commands:

| Alias | Full Command | Description |
|-------|--------------|-------------|
| `ai-h` | `ai-health` | Quick health check |
| `ai-d` | `ai-debug-files` | Quick file debugging |
| `ai-f` | `ai-format-files` | Quick formatting |
| `ai-b` | `ai-build` | Quick build |
| `ai-t` | `ai-test` | Quick test |

## 🔍 Troubleshooting Commands

### `ai-diagnose`
**Purpose**: Run comprehensive diagnostics
**Syntax**: `ai-diagnose [-Export <path>]`

**Examples**:
```powershell
ai-diagnose                      # Run diagnostics
ai-diagnose -Export "diag.json"  # Export results
```

---

### `ai-reset`
**Purpose**: Reset AI-Assistant configuration
**Syntax**: `ai-reset [-Confirm]`

**Examples**:
```powershell
ai-reset -Confirm               # Reset to defaults
```

## 📖 Getting More Help

### Command Help
Every command supports the `-Help` parameter:
```powershell
ai-debug-files -Help
ai-build -Help
```

### Verbose Output
Add `-Verbose` to any command for detailed information:
```powershell
ai-health -Verbose
ai-debug-files -Pattern "**/*.cs" -Verbose
```

### Documentation
- [Integration Guide](INTEGRATION-GUIDE.md)
- [Project Templates](PROJECT-TEMPLATES.md)
- [Configuration Guide](CONFIGURATION-GUIDE.md)

---

**💡 Pro Tip**: Use tab completion! Type `ai-` and press Tab to cycle through available commands.
