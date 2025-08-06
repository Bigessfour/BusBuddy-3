# 🔧 VSCode Extensions Reference - BusBuddy Optimized

**Purpose**: Essential VS Code extensions for BusBuddy development with GitHub Copilot optimization.

**Source**: `.vscode/extensions.json` - Automatically recommended when opening BusBuddy workspace.

## 📦 Recommended Extensions

### C# Development & Corruption Detection
| Extension | ID | Purpose | Copilot Benefit |
|-----------|----|---------| ---------------- |
| **C# for Visual Studio Code** | `ms-dotnettools.csharp` | Core C# language support | Enables accurate IntelliSense for .NET 8 features |
| **C# Dev Kit** | `ms-dotnettools.csdevkit` | Enhanced C# development | Project management & solution explorer integration |
| **Roslynator** | `josefpihrt-vscode.roslynator` | Advanced C# refactoring | Code quality suggestions that Copilot learns from |

### XAML Development & WPF Support
| Extension | ID | Purpose | Copilot Benefit |
|-----------|----|---------| ---------------- |
| **XAML Styler** | `ms-dotnettools.xaml` | XAML formatting & IntelliSense | Syncfusion control auto-completion |
| **NoesisGUI Tools** | `noesistechnologies.noesisgui-tools` | Enhanced XAML editing | Advanced WPF patterns for Copilot |
| **XML Complete** | `rogalmic.vscode-xml-complete` | XML/XAML completion | Better markup suggestions |

### PowerShell Development (Microsoft Standards)
| Extension | ID | Purpose | Copilot Benefit |
|-----------|----|---------| ---------------- |
| **PowerShell** | `ms-vscode.powershell` | Core PowerShell 7.5.2 support | bb-* command completions |
| **PowerShell Preview** | `ms-vscode.powershell-preview` | Latest PS7.5 features | Ternary operators, null conditionals |

### AI-Native Development
| Extension | ID | Purpose | Copilot Benefit |
|-----------|----|---------| ---------------- |
| **GitHub Copilot** | `github.copilot` | AI code completion | Core AI assistance |
| **GitHub Copilot Chat** | `github.copilot-chat` | Conversational AI help | Context-aware code discussions |

### Task Management (EXCLUSIVE METHOD)
| Extension | ID | Purpose | Copilot Benefit |
|-----------|----|---------| ---------------- |
| **Task Explorer** | `spmeesseman.vscode-taskexplorer` | **ONLY** approved task runner | Integrates with bb-* PowerShell commands |

## 🚫 Blocked Extensions (Unwanted)

### Competing Task Runners
- `formulahendry.auto-close-tag` - Conflicts with XAML Styler
- `eg2.vscode-npm-script` - Node.js not used in BusBuddy
- `hbenl.vscode-test-explorer` - Use built-in test explorer instead

### Linting Conflicts
- `ms-vscode.vscode-typescript-tslint-plugin` - Prevents PowerShell 7.5.2 syntax conflicts

## 💡 Copilot Usage Examples

### C# Development
```csharp
// Copilot Prompt: "Create Syncfusion SfDataGrid with MVVM binding"
// Result: Leverages XAML Styler context for accurate control suggestions
```

### XAML Development
```xml
<!-- Copilot Prompt: "Add Syncfusion FluentDark theme resources" -->
<!-- Result: Uses NoesisGUI Tools context for proper ResourceDictionary structure -->
```

### PowerShell Automation
```powershell
# Copilot Prompt: "Create bb-health command following Microsoft standards"
# Result: Uses PowerShell Preview context for modern PS7.5.2 patterns
```

## 🔧 Installation Commands

### Install All Recommended (PowerShell)
```powershell
# Run from BusBuddy root directory
$extensions = @(
    "ms-dotnettools.csharp",
    "ms-dotnettools.csdevkit", 
    "ms-dotnettools.xaml",
    "ms-vscode.powershell",
    "github.copilot",
    "github.copilot-chat",
    "spmeesseman.vscode-taskexplorer"
)

foreach ($ext in $extensions) {
    code --install-extension $ext
}
```

### Validate Installation
```powershell
# Check installed extensions
code --list-extensions | Where-Object { $_ -match "dotnettools|github.copilot|taskexplorer" }
```

## 🎯 BusBuddy-Specific Configuration

### Syncfusion Integration
- **XAML Styler** auto-formats Syncfusion controls
- **NoesisGUI Tools** provides advanced WPF binding IntelliSense
- **Copilot** learns from Syncfusion namespace patterns

### PowerShell Integration  
- **Task Explorer** exclusively manages bb-* commands
- **PowerShell Preview** supports latest PS7.5.2 syntax
- **Copilot** suggests Microsoft-compliant PowerShell patterns

### AI Enhancement
- **GitHub Copilot** references BusBuddy domain context
- **Copilot Chat** provides architectural guidance
- Open Reference folder for maximum context awareness

## 🔄 Maintenance

### Update Commands
```powershell
# Update all extensions
code --update-extensions

# Validate BusBuddy-specific setup
bb-health --check-extensions
```

### Troubleshooting
- **Extension conflicts**: Check unwanted recommendations list
- **Copilot performance**: Restart VS Code after installing multiple extensions
- **PowerShell issues**: Verify PowerShell 7.5.2 is default terminal

---
*Optimized for BusBuddy MVP development with AI-first approach* 🚀
