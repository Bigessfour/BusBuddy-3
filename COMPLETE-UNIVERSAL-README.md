# 🤖 Universal AI-Assistant Development Environment

> A powerful, reusable development environment framework that transforms any project into an enhanced development experience. Born from the BusBuddy project's advanced AI-Assistant system.

[![PowerShell](https://img.shields.io/badge/PowerShell-7.5%2B-blue.svg)](https://github.com/PowerShell/PowerShell)
[![VS Code](https://img.shields.io/badge/VS%20Code-Compatible-brightgreen.svg)](https://code.visualstudio.com/)
[![Multi-Language](https://img.shields.io/badge/Languages-.NET%20%7C%20Node.js%20%7C%20Python%20%7C%20More-orange.svg)](#supported-project-types)

## 🚀 Quick Start

### 1. Load AI-Assistant in Any Project
```powershell
# Navigate to your project
cd "C:\Dev\YourProject"

# Load AI-Assistant (adjust path as needed)
. "C:\Dev\AI-Assistant-Workspace\AI-Assistant\Scripts\load-ai-assistant-profile.ps1"
```

### 2. Initialize for Your Project Type
```powershell
ai-init-project -ProjectType "DotNet"     # .NET projects
ai-init-project -ProjectType "NodeJS"     # Node.js projects
ai-init-project -ProjectType "Python"     # Python projects
ai-init-project -ProjectType "Generic"    # Other projects
```

### 3. Start Using Universal Commands
```powershell
ai-health        # Check development environment
ai-help          # Show all available commands
ai-debug-files   # Debug and format files (adapts to language)
ai-build         # Build project (uses appropriate build system)
ai-format        # Format files (uses language-specific formatters)
ai-test          # Run tests (uses project test framework)
```

## 🎯 Supported Project Types

### .NET Projects
- **Languages**: C#, F#, VB.NET
- **Frameworks**: .NET Core, .NET Framework, .NET 8+
- **File Types**: `.cs`, `.xaml`, `.csproj`, `.sln`
- **Tools**: dotnet CLI, MSBuild, NuGet
- **Features**: Automatic solution detection, C#/XAML debugging, Roslyn analyzers

### Node.js Projects
- **Languages**: JavaScript, TypeScript
- **Frameworks**: React, Vue, Angular, Express
- **File Types**: `.js`, `.ts`, `.json`, `.md`
- **Tools**: npm, yarn, webpack, vite
- **Features**: package.json detection, ESLint/Prettier integration

### Python Projects
- **Languages**: Python 3.6+
- **Frameworks**: Django, Flask, FastAPI
- **File Types**: `.py`, `.pyi`, `.pyx`
- **Tools**: pip, poetry, pytest, black
- **Features**: requirements.txt detection, virtual environment support

### PowerShell Projects
- **Languages**: PowerShell 5.1, PowerShell Core 7+
- **File Types**: `.ps1`, `.psm1`, `.psd1`
- **Tools**: PSScriptAnalyzer, Pester
- **Features**: Module detection, script analysis

### Generic Projects
- **Universal file operations**
- **Basic project structure detection**
- **Configurable build commands**
- **Git integration**

## 🔧 Core Features

### 🔍 Universal File Debugging
- **Language Detection**: Automatically identifies file types and applies appropriate tools
- **Auto-Formatting**: Uses best-practice formatters for each language
- **Error Detection**: Identifies and suggests fixes for common issues
- **Tool Integration**: Seamlessly integrates with language-specific linters and analyzers
- **Batch Processing**: Process multiple files with patterns like `**/*.cs` or `**/*.js`

### 🏗️ Project Environment Setup
- **Auto-Detection**: Identifies project type based on file structure and manifests
- **Dependency Management**: Manages packages, modules, and dependencies automatically
- **Build System Integration**: Configures appropriate build, test, and run commands
- **Environment Variables**: Sets up project-specific environment configuration

### 🎨 VS Code Integration
- **Universal Tasks**: Pre-configured tasks that adapt to your project type
- **IntelliSense**: Enhanced autocomplete and error detection
- **Debug Configuration**: Automatic debug setup for supported languages
- **Extensions**: Recommends and configures relevant VS Code extensions

### 📊 GitHub Integration
- **Automated Workflows**: Smart commit, push, and pull request management
- **Code Quality**: Pre-commit hooks and automated formatting
- **Branch Management**: Intelligent branching and merging strategies
- **CI/CD Integration**: GitHub Actions workflow templates

## 📁 Directory Structure

```
AI-Assistant-Workspace/
├── 📁 AI-Assistant/           # Core AI-Assistant functionality
│   ├── 📁 Scripts/            # Profile and initialization scripts
│   ├── 📁 Core/               # Core modules and functionality
│   ├── 📁 Config/             # Configuration templates
│   └── 📁 Templates/          # Project templates
├── 📁 Tools/                  # Universal development tools
│   └── 📁 Scripts/            # File debugger and automation tools
├── 📁 .vscode/               # VS Code integration
│   ├── 📄 tasks.json         # Universal tasks
│   └── 📄 settings.json      # Recommended settings
├── 📁 Documentation/         # Comprehensive guides
│   ├── 📄 INTEGRATION-GUIDE.md
│   ├── 📄 COMMAND-REFERENCE.md
│   └── 📄 PROJECT-TEMPLATES.md
├── 📁 Examples/              # Example project setups
└── 📄 README.md              # This file
```

## 💻 Command Reference

### Core Commands
| Command | Description | Example |
|---------|-------------|---------|
| `ai-health` | Check development environment health | `ai-health` |
| `ai-help` | Show all available commands | `ai-help` |
| `ai-init-project` | Initialize AI-Assistant for project type | `ai-init-project -ProjectType "DotNet"` |

### File Operations
| Command | Description | Example |
|---------|-------------|---------|
| `ai-debug-files` | Debug and format files (language-aware) | `ai-debug-files -Pattern "**/*.cs" -AutoFix` |
| `ai-format-files` | Format files using appropriate formatter | `ai-format-files` |
| `ai-validate-files` | Validate files without making changes | `ai-validate-files -Verbose` |

### Build & Test
| Command | Description | Example |
|---------|-------------|---------|
| `ai-build` | Build project using appropriate build system | `ai-build -FormatFirst` |
| `ai-test` | Run tests using project test framework | `ai-test -Coverage` |
| `ai-clean` | Clean build artifacts | `ai-clean` |

### Project Management
| Command | Description | Example |
|---------|-------------|---------|
| `ai-open` | Open files or projects in VS Code | `ai-open MyFile.cs` |
| `ai-new-project` | Create new project with AI-Assistant | `ai-new-project -Type "DotNet" -Name "MyApp"` |

## 🎯 Project Integration Examples

### Starting a New .NET Project
```powershell
# Create and initialize new .NET project
cd "C:\Dev"
ai-new-dotnet -ProjectName "MyApp" -Template "wpf"
cd "MyApp"

# Load AI-Assistant
. "C:\Dev\AI-Assistant-Workspace\AI-Assistant\Scripts\load-ai-assistant-profile.ps1"

# Verify environment
ai-health  # Checks .NET SDK, sets up build system

# Start development
ai-debug-files -Pattern "**/*.cs" -AutoFix
ai-build -FormatFirst
```

### Integrating with Existing Node.js Project
```powershell
# Navigate to existing project
cd "C:\Dev\MyExistingNodeApp"

# Load AI-Assistant
. "C:\Dev\AI-Assistant-Workspace\AI-Assistant\Scripts\load-ai-assistant-profile.ps1"

# Initialize for Node.js
ai-init-project -ProjectType "NodeJS"

# Verify and enhance
ai-health  # Checks Node.js, npm, installs dependencies
ai-debug-files -Pattern "**/*.js" -AutoFix
ai-format-files
```

### Python Project Setup
```powershell
# Existing Python project
cd "C:\Dev\MyPythonApp"

# Load AI-Assistant
. "C:\Dev\AI-Assistant-Workspace\AI-Assistant\Scripts\load-ai-assistant-profile.ps1"

# Initialize for Python
ai-init-project -ProjectType "Python"

# Development workflow
ai-health  # Checks Python, pip, virtual environment
ai-debug-files -Pattern "**/*.py" -AutoFix
ai-test
```

## 📖 Documentation

### Quick References
- [🚀 Integration Guide](Documentation/INTEGRATION-GUIDE.md) - Step-by-step project integration
- [📚 Command Reference](Documentation/COMMAND-REFERENCE.md) - Complete command documentation
- [🏗️ Project Templates](Documentation/PROJECT-TEMPLATES.md) - Ready-to-use project setups
- [⚙️ Configuration Guide](Documentation/CONFIGURATION-GUIDE.md) - Customization options

### Advanced Topics
- [🔧 Custom Tool Integration](Documentation/CUSTOM-TOOLS.md) - Adding your own tools
- [🌐 Multi-Project Workflows](Documentation/MULTI-PROJECT.md) - Managing multiple projects
- [🔌 VS Code Extensions](Documentation/VSCODE-SETUP.md) - Recommended extensions and setup

## ⚡ Performance Features

### Intelligent Caching
- **Command Results**: Caches health checks and environment detection
- **File Analysis**: Remembers file analysis results for faster subsequent runs
- **Build Optimization**: Incremental builds and dependency tracking

### Background Processing
- **Async Operations**: Non-blocking file operations where possible
- **Parallel Processing**: Multi-threaded file debugging and formatting
- **Smart Batching**: Optimized batch processing for large file sets

### Resource Management
- **Memory Optimization**: Efficient memory usage for large projects
- **Cleanup**: Automatic cleanup of temporary files and caches
- **Logging**: Comprehensive logging with configurable verbosity levels

## 🔒 Security & Best Practices

### Execution Policy
- Uses `RemoteSigned` execution policy for security
- Script signing validation where applicable
- Safe parameter handling and input validation

### Tool Integration
- **Verified Tools**: Only integrates with well-known, trusted development tools
- **Sandboxed Execution**: Isolated execution contexts where possible
- **Permission Checks**: Validates file and directory permissions before operations

## 🛠️ Troubleshooting

### Common Issues

#### "Command not found" Errors
```powershell
# Verify AI-Assistant is loaded
Get-Alias | Where-Object { $_.Name -like "ai-*" }

# Reload profile if needed
. "C:\Dev\AI-Assistant-Workspace\AI-Assistant\Scripts\load-ai-assistant-profile.ps1"
```

#### Project Type Not Detected
```powershell
# Manually specify project type
ai-init-project -ProjectType "DotNet" -Force

# Check project files
ai-health -Verbose
```

#### VS Code Integration Issues
```powershell
# Reload VS Code window
# Ctrl+Shift+P → "Developer: Reload Window"

# Verify tasks are available
# Ctrl+Shift+P → "Tasks: Run Task"
```

### Getting Help
- Run `ai-help` for command overview
- Use `-Verbose` flag for detailed output
- Check logs in the workspace `logs/` directory
- Review documentation in `Documentation/` folder

## 🚀 Roadmap

### Planned Features
- [ ] **Web Development**: Enhanced support for React, Vue, Angular projects
- [ ] **Docker Integration**: Container-based development environments
- [ ] **Cloud Support**: Azure, AWS, GCP project templates
- [ ] **Database Tools**: Database migration and management tools
- [ ] **Testing Frameworks**: Enhanced testing support and coverage reporting
- [ ] **Performance Monitoring**: Built-in performance profiling and optimization

### Version History
- **v2.0** - Universal AI-Assistant extracted from BusBuddy
- **v1.0** - Original BusBuddy AI-Assistant system

## 🤝 Contributing

The Universal AI-Assistant is designed to be extensible and customizable:

### Adding New Project Types
1. Create detection logic in `AI-Assistant/Core/ProjectDetection.ps1`
2. Add configuration template in `AI-Assistant/Config/`
3. Implement tool integration in `Tools/Scripts/`
4. Update documentation

### Custom Tools
1. Place custom tools in `Tools/Scripts/Custom/`
2. Register with AI-Assistant using `Register-CustomTool`
3. Add documentation and examples

## 📄 License

This project maintains the same license as the original BusBuddy project from which it was extracted.

## 🙏 Acknowledgments

- **Origin**: Extracted from the BusBuddy AI-Assistant development
- **Inspiration**: Tool-first development philosophy
- **Community**: PowerShell and VS Code development communities

---

**Origin**: Extracted from BusBuddy AI-Assistant development
**Version**: Universal 2.0
**Supports**: Multi-language, cross-project development environments
**Maintained by**: AI-Assistant Community

> Transform any project into an enhanced development experience with battle-tested tools and workflows! 🚀
