# 📚 AI-Assistant Command Reference

Complete reference for all Universal AI-Assistant commands.

## 🎯 Core Commands

### `ai-health`
**Purpose**: Check development environment health and configuration
**Syntax**: `ai-health [-Verbose] [-ProjectPath <path>]`

**Examples**:
```powershell
ai-health                           # Basic health check
ai-health -Verbose                  # Detailed diagnostics
ai-health -ProjectPath "C:\Dev\App" # Check specific project
```

**What it checks**:
- PowerShell version (7.5+ required)
- Project type detection
- Required tools availability (.NET SDK, Node.js, Python, etc.)
- File structure validation
- Environment variables
- VS Code configuration

---

### `ai-help`
**Purpose**: Display all available commands and quick reference
**Syntax**: `ai-help [command]`

**Examples**:
```powershell
ai-help                    # Show all commands
ai-help ai-debug-files     # Help for specific command
```

---

### `ai-init-project`
**Purpose**: Initialize AI-Assistant for specific project type
**Syntax**: `ai-init-project -ProjectType <type> [-Force]`

**Supported Project Types**:
- `DotNet` - .NET projects (C#, F#, VB.NET)
- `NodeJS` - Node.js projects (JavaScript, TypeScript)
- `Python` - Python projects
- `PowerShell` - PowerShell modules and scripts
- `Generic` - Universal project support

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
