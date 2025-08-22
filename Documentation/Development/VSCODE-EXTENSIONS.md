# 🔌 VS Code Extensions for BusBuddy Development

This document outlines the recommended VS Code extensions for optimal BusBuddy development experience. All extensions are automatically suggested when opening the project via `.vscode/extensions.json`.

## 🚀 Quick Setup

### Automatic Installation (Recommended)

When you open the BusBuddy project in VS Code, you'll see a notification to install recommended extensions. Click "Install All" for the best experience.

### Manual Installation via PowerShell

```powershell
# Import the BusBuddy module
Import-Module .\PowerShell\BusBuddy.psm1

# Install essential extensions
bb-install-extensions -Essential

# Install all recommended extensions
bb-install-extensions

# Install all extensions including advanced tools
bb-install-extensions -Advanced

# List available extensions without installing
bb-install-extensions -ListOnly

# Validate VS Code setup
bb-validate-vscode
```

## 📚 Extension Categories

### ✨ Essential Extensions

These are **required** for basic BusBuddy development:

| Extension                                           | Purpose                  | Why Essential                                  |
| --------------------------------------------------- | ------------------------ | ---------------------------------------------- |
| **C#** (ms-dotnettools.csharp)                      | .NET 9 & WPF development | Core IntelliSense, debugging, and XAML support |
| **PowerShell** (ms-vscode.powershell)               | PowerShell 7.5 support   | Essential for bb- commands and automation      |
| **C# Dev Kit** (ms-dotnettools.csdevkit)            | .NET development tools   | Integrated build/test without CLI spam         |
| **GitLens** (eamodio.gitlens)                       | Git supercharged         | GitHub integration for workflow monitoring     |
| **Task Explorer** (spmeesseman.vscode-taskexplorer) | Task management          | **EXCLUSIVE** method for task execution        |

### 🔧 Core Development Extensions

Recommended for enhanced development experience:

| Extension                                               | Purpose               | Benefits                              |
| ------------------------------------------------------- | --------------------- | ------------------------------------- |
| **.NET Runtime** (ms-dotnettools.vscode-dotnet-runtime) | Runtime management    | Ensures .NET 9 compatibility          |
| **XAML** (ms-dotnettools.xaml)                          | XAML language support | Better WPF development experience     |
| **Roslynator** (josefpihrt-vscode.roslynator)           | C# analysis           | Advanced refactoring and code quality |
| **Prettier** (esbenp.prettier-vscode)                   | Code formatting       | Consistent code style across team     |
| **EditorConfig** (editorconfig.editorconfig)            | Editor configuration  | Team coding standards enforcement     |

### 🧪 Testing & Quality Extensions

For robust code quality and testing:

| Extension                                                      | Purpose               | Impact                             |
| -------------------------------------------------------------- | --------------------- | ---------------------------------- |
| **Test Adapter Converter** (ms-vscode.test-adapter-converter)  | Test integration      | Visual test runner for unit tests  |
| **Code Spell Checker** (streetsidesoftware.code-spell-checker) | Documentation quality | Catches typos in comments and docs |
| **Better Comments** (aaron-bond.better-comments)               | Enhanced comments     | Color-coded comment highlighting   |

### 🤖 AI & Productivity Extensions

For enhanced productivity and AI assistance:

| Extension                                             | Purpose            | Benefits                            |
| ----------------------------------------------------- | ------------------ | ----------------------------------- |
| **GitHub Copilot** (github.copilot)                   | AI code assistance | Intelligent code suggestions        |
| **GitHub Copilot Chat** (github.copilot-chat)         | AI chat interface  | Conversational code help            |
| **PowerShell Preview** (ms-vscode.powershell-preview) | PS 7.5.2 features  | Latest PowerShell language features |

### ☁️ Azure & Cloud Extensions

For Azure integration and cloud development:

| Extension                                                            | Purpose              | Usage                                |
| -------------------------------------------------------------------- | -------------------- | ------------------------------------ |
| **Azure Account** (ms-vscode.azure-account)                          | Azure authentication | Cloud resource management            |
| **Azure Resource Groups** (ms-azuretools.vscode-azureresourcegroups) | Azure resources      | Direct Azure management from VS Code |

### 🚀 Advanced Tools

Specialized tools for advanced scenarios:

| Extension                                                        | Purpose             | When to Use                             |
| ---------------------------------------------------------------- | ------------------- | --------------------------------------- |
| **Syncfusion Extensions** (syncfusioninc.maui-vscode-extensions) | Syncfusion support  | Enhanced Syncfusion control development |
| **Hex Editor** (ms-vscode.hexeditor)                             | Binary file editing | Database files, compiled assets         |
| **Remote SSH** (ms-vscode-remote.remote-ssh)                     | Remote development  | Cloud development, team collaboration   |

## 🛡️ Blocked Extensions

These extensions are explicitly blocked to prevent conflicts:

- **Auto Close/Rename Tag** - Conflicts with XAML formatting
- **TypeScript TSLint** - Conflicts with PowerShell analysis
- **Alternative XML tools** - Conflicts with our XAML workflow
- **npm/Node task runners** - We use Task Explorer exclusively

## 🔧 Configuration Files

### .vscode/extensions.json

Defines recommended and blocked extensions for team consistency:

```json
{
    "recommendations": [
        "ms-dotnettools.csharp",
        "ms-vscode.powershell"
        // ... full list in file
    ],
    "unwantedRecommendations": [
        // Blocked extensions that cause conflicts
    ]
}
```

### .vscode/settings.json

Project-specific settings for optimal development:

- PowerShell terminal integration
- C# formatting preferences
- Task Explorer configuration
- XAML indentation settings

### .vscode/tasks.json

Pre-configured tasks for common operations:

- Build solution
- Run application
- Run tests
- Development workflow

## 🏥 Health Checking

### Validate Your Setup

```powershell
# Quick validation
bb-validate-vscode

# Detailed validation with task and settings check
bb-validate-vscode -ShowDetails -CheckTasks -CheckSettings

# Get overall development environment health
bb-health
```

### Common Issues & Solutions

#### VS Code CLI Not Found

```powershell
# Solution 1: Add VS Code to PATH
# Windows: Add VS Code installation directory to PATH environment variable

# Solution 2: Use integrated terminal
# Open VS Code → Terminal → New Terminal → Run commands there

# Solution 3: Install VS Code with CLI option
# Download from https://code.visualstudio.com and ensure "Add to PATH" is checked
```

#### Extensions Not Installing

```powershell
# Check network connectivity and proxy settings
# Try forcing reinstall
bb-install-extensions -Force

# Check specific extension manually
code --install-extension ms-dotnettools.csharp
```

#### Task Explorer Not Working

```powershell
# Verify Task Explorer is installed
bb-install-extensions -Essential

# Check tasks configuration
bb-validate-vscode -CheckTasks

# Restart VS Code after installation
```

## 🎯 Phase 2 Integration

These extensions directly support Phase 2 objectives:

### UI Testing Enhancement

- **C# Dev Kit**: Integrated test runner for UI tests
- **Test Adapter Converter**: Visual test execution and debugging

### Warning Reduction (Target: <100 warnings)

- **Roslynator**: Advanced C# analysis and quick fixes
- **C#**: Real-time error detection and null safety warnings
- **Prettier**: Consistent formatting reduces style warnings

### GitHub Actions Integration

- **GitLens**: Direct GitHub Actions monitoring
- **GitHub Copilot**: AI assistance for workflow optimization

### Azure SQL & Data Integration

- **Azure Account**: Direct database connection management
- **PowerShell**: Enhanced data scripting capabilities

## 🚀 Getting Started Workflow

1. **Clone Repository**

    ```bash
    git clone https://github.com/Bigessfour/BusBuddy-2.git
    cd BusBuddy-2
    ```

2. **Open in VS Code**

    ```bash
    code .
    ```

3. **Install Extensions** (when prompted)
    - Click "Install All" in the notification
    - Or use `bb-install-extensions` in terminal

4. **Validate Setup**

    ```powershell
    bb-validate-vscode
    ```

5. **Start Development**
    ```powershell
    bb-dev-workflow
    ```

## 📊 Success Metrics

After proper setup, you should see:

- ✅ All essential extensions installed
- ✅ Task Explorer functioning for builds/tests
- ✅ PowerShell 7.5 features working in terminal
- ✅ Real-time C# error detection
- ✅ GitHub integration active
- ✅ Build warnings highlighted inline

## 💡 Tips & Best Practices

### Team Consistency

- Always use the recommended extensions list
- Don't install conflicting extensions
- Keep extensions updated via VS Code

### Performance Optimization

- Disable unused extensions in workspace settings
- Use workspace-specific settings for team projects
- Restart VS Code after major extension changes

### Troubleshooting

- Use `bb-validate-vscode` for diagnostic information
- Check extension compatibility with VS Code version
- Report issues via GitHub with validation report

---

**🚌 Ready for the journey? Your VS Code environment is now optimized for Bus Buddy development!**

For additional help: `bb-mentor "VS Code Extensions"` or `bb-commands` to see all available automation tools.
