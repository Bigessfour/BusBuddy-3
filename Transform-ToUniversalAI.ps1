#Requires -Version 7.5

<#
.SYNOPSIS
    Transform BusBuddy AI-Assistant to Universal AI-Assistant

.DESCRIPTION
    Preserves the AI-Assistant system while removing BusBuddy project files.
    Transforms BusBuddy-specific tools into universal development tools.

.PARAMETER WorkspacePath
    Path where the universal AI-Assistant workspace will be created

.PARAMETER KeepArchive
    Keep an archive of the BusBuddy project for reference

.EXAMPLE
    .\Transform-ToUniversalAI.ps1 -WorkspacePath "C:\Dev\AI-Assistant-Workspace" -KeepArchive
#>

param(
    [Parameter(Mandatory)]
    [string]$WorkspacePath,

    [switch]$KeepArchive
)

Write-Host @"
🔄 BUSBUDDY TO UNIVERSAL AI-ASSISTANT TRANSFORMATION
═══════════════════════════════════════════════════════════════════════
Preserving AI-Assistant | Removing BusBuddy Project | Creating Universal Tools
Target: $WorkspacePath
═══════════════════════════════════════════════════════════════════════
"@ -ForegroundColor Cyan

$currentPath = Get-Location

#region Phase 1: Backup and Archive

Write-Host "📦 Phase 1: Creating backups and archives..." -ForegroundColor Yellow

# Create target workspace
if (-not (Test-Path $WorkspacePath)) {
    New-Item -ItemType Directory -Path $WorkspacePath -Force | Out-Null
    Write-Host "✅ Created workspace: $WorkspacePath" -ForegroundColor Green
}

# Backup AI-Assistant
$aiBackupPath = Join-Path $currentPath "AI-Assistant-Backup"
if (Test-Path "AI-Assistant") {
    Copy-Item -Path "AI-Assistant" -Destination $aiBackupPath -Recurse -Force
    Write-Host "✅ AI-Assistant backed up to: $aiBackupPath" -ForegroundColor Green
}

# Archive BusBuddy project if requested
if ($KeepArchive) {
    Write-Host "📚 Creating BusBuddy project archive..." -ForegroundColor Yellow

    $archiveItems = @()
    $busBuddyFiles = @(
        "BusBuddy.sln", "*.csproj", "BusBuddy.WPF", "BusBuddy.Core",
        "BusBuddy.Tests", "README.md", "ENHANCED-PROFILE-GUIDE.md",
        "BUS-*.md", "CRUD-*.md", "*.json", "Directory.Build.props"
    )

    foreach ($item in $busBuddyFiles) {
        if (Test-Path $item) {
            $archiveItems += $item
        }
    }

    if ($archiveItems.Count -gt 0) {
        $archivePath = Join-Path $WorkspacePath "BusBuddy-Project-Archive.zip"
        Compress-Archive -Path $archiveItems -DestinationPath $archivePath -Force
        Write-Host "✅ BusBuddy archived to: $archivePath" -ForegroundColor Green
    }
}

#endregion

#region Phase 2: Copy and Transform AI-Assistant

Write-Host "🔄 Phase 2: Transforming AI-Assistant to universal..." -ForegroundColor Yellow

# Copy AI-Assistant to new workspace
$targetAIPath = Join-Path $WorkspacePath "AI-Assistant"
Copy-Item -Path "AI-Assistant" -Destination $targetAIPath -Recurse -Force
Write-Host "✅ AI-Assistant copied to workspace" -ForegroundColor Green

# Copy Tools directory
$targetToolsPath = Join-Path $WorkspacePath "Tools"
if (Test-Path "Tools") {
    Copy-Item -Path "Tools" -Destination $targetToolsPath -Recurse -Force
    Write-Host "✅ Tools copied to workspace" -ForegroundColor Green
}

# Transform profile script
$oldProfilePath = Join-Path $targetAIPath "Scripts\load-bus-buddy-profile.ps1"
$newProfilePath = Join-Path $targetAIPath "Scripts\load-ai-assistant-profile.ps1"

if (Test-Path $oldProfilePath) {
    # Read and transform profile content
    $profileContent = Get-Content $oldProfilePath -Raw

    # Replace BusBuddy-specific references with generic ones
    $transformedContent = $profileContent `
        -replace 'BusBuddy Enhanced Development Profile', 'Universal AI-Assistant Profile' `
        -replace 'BusBuddy development', 'project development' `
        -replace 'BusBuddy project', 'current project' `
        -replace 'bb-', 'ai-' `
        -replace 'BUSBUDDY', 'AI-ASSISTANT' `
        -replace 'BusBuddy', 'AI-Assistant'

    # Save transformed profile
    $transformedContent | Out-File -FilePath $newProfilePath -Encoding UTF8
    Remove-Item -Path $oldProfilePath -Force
    Write-Host "✅ Profile transformed: load-ai-assistant-profile.ps1" -ForegroundColor Green
}

# Transform file debugger to universal
$oldDebuggerPath = Join-Path $targetToolsPath "Scripts\BusBuddy-File-Debugger.ps1"
$newDebuggerPath = Join-Path $targetToolsPath "Scripts\Universal-File-Debugger.ps1"

if (Test-Path $oldDebuggerPath) {
    $debuggerContent = Get-Content $oldDebuggerPath -Raw

    $transformedDebugger = $debuggerContent `
        -replace 'BusBuddy File Debugger', 'Universal File Debugger' `
        -replace 'BusBuddy development', 'project development' `
        -replace 'bb-debug-files', 'ai-debug-files' `
        -replace 'BUSBUDDY', 'UNIVERSAL'

    $transformedDebugger | Out-File -FilePath $newDebuggerPath -Encoding UTF8
    Remove-Item -Path $oldDebuggerPath -Force
    Write-Host "✅ File debugger transformed: Universal-File-Debugger.ps1" -ForegroundColor Green
}

#endregion

#region Phase 3: Create Universal Configuration

Write-Host "⚙️ Phase 3: Creating universal configuration..." -ForegroundColor Yellow

# Create universal VS Code settings
$vscodeDir = Join-Path $WorkspacePath ".vscode"
New-Item -ItemType Directory -Path $vscodeDir -Force | Out-Null

$universalSettings = @{
    "terminal.integrated.profiles.windows"       = @{
        "AI-Assistant PowerShell" = @{
            path = "pwsh.exe"
            args = @("-NoProfile", "-NoExit", "-Command", "& '.\AI-Assistant\Scripts\load-ai-assistant-profile.ps1';")
        }
    }
    "terminal.integrated.defaultProfile.windows" = "AI-Assistant PowerShell"
    "powershell.scriptAnalysis.enable"           = $true
    "powershell.codeFormatting.preset"           = "OTBS"
    "files.autoSave"                             = "afterDelay"
    "files.autoSaveDelay"                        = 1000
}

$settingsPath = Join-Path $vscodeDir "settings.json"
$universalSettings | ConvertTo-Json -Depth 10 | Out-File -FilePath $settingsPath -Encoding UTF8
Write-Host "✅ Universal VS Code settings created" -ForegroundColor Green

# Create universal tasks
$universalTasks = @{
    version = "2.0.0"
    tasks   = @(
        @{
            label   = "AI: Health Check"
            type    = "shell"
            command = "pwsh"
            args    = @("-Command", "ai-health")
            group   = "test"
            detail  = "Check development environment health"
        },
        @{
            label   = "AI: Debug Files"
            type    = "shell"
            command = "pwsh"
            args    = @("-Command", "ai-debug-files -AutoFix -Verbose")
            group   = "build"
            detail  = "Debug and format project files"
        },
        @{
            label   = "AI: Format Files"
            type    = "shell"
            command = "pwsh"
            args    = @("-Command", "ai-format-files")
            group   = "build"
            detail  = "Format project files"
        },
        @{
            label   = "AI: Build Project"
            type    = "shell"
            command = "pwsh"
            args    = @("-Command", "ai-build")
            group   = "build"
            detail  = "Build current project"
        },
        @{
            label   = "AI: Test Project"
            type    = "shell"
            command = "pwsh"
            args    = @("-Command", "ai-test")
            group   = "test"
            detail  = "Run project tests"
        }
    )
}

$tasksPath = Join-Path $vscodeDir "tasks.json"
$universalTasks | ConvertTo-Json -Depth 10 | Out-File -FilePath $tasksPath -Encoding UTF8
Write-Host "✅ Universal VS Code tasks created" -ForegroundColor Green

#endregion

#region Phase 4: Create Documentation

Write-Host "📚 Phase 4: Creating universal documentation..." -ForegroundColor Yellow

# Create main README
$readmeContent = @"
# Universal AI-Assistant Development Environment

A powerful, reusable development environment framework that adapts to any project type.

## 🚀 Quick Start

1. **Load AI-Assistant in any project:**
   ``````powershell
   # Navigate to your project
   cd "C:\Dev\YourProject"

   # Load AI-Assistant
   . "path\to\AI-Assistant-Workspace\AI-Assistant\Scripts\load-ai-assistant-profile.ps1"
   ``````

2. **Initialize AI-Assistant for your project:**
   ``````powershell
   ai-init-project -ProjectType "DotNet"  # or NodeJS, Python, etc.
   ``````

3. **Use universal commands:**
   ``````powershell
   ai-health        # Check development environment
   ai-debug-files   # Debug and format files (adapts to language)
   ai-build         # Build project (uses appropriate build system)
   ai-format        # Format files (uses language-specific formatters)
   ai-test          # Run tests (uses project test framework)
   ai-help          # Show all available commands
   ``````

## 🎯 Supported Project Types

- **.NET** (C#, F#, VB.NET) - Uses dotnet CLI, C#/XAML debugging
- **Node.js** (JavaScript, TypeScript) - Uses npm/yarn, ESLint/Prettier
- **Python** - Uses pip, Black formatter, pytest
- **PowerShell** - Uses PSScriptAnalyzer, PowerShell formatting
- **Generic** - Basic file operations and structure

## 🔧 Features

### Universal File Debugging
- **Language Detection**: Automatically detects file types and applies appropriate tools
- **Auto-Formatting**: Uses best-practice formatters for each language
- **Error Detection**: Identifies and suggests fixes for common issues
- **Tool Integration**: Integrates with language-specific linters and analyzers

### Project Environment Setup
- **Auto-Detection**: Identifies project type and sets up appropriate environment
- **Dependency Management**: Manages packages, modules, and dependencies
- **Build System Integration**: Configures build, test, and run commands
- **VS Code Integration**: Sets up tasks, settings, and debugging

### GitHub Integration
- **Automated Workflows**: Smart commit, push, and pull request management
- **Code Quality**: Pre-commit hooks and automated formatting
- **Branch Management**: Intelligent branching and merging strategies

## 📁 Directory Structure

``````
AI-Assistant-Workspace/
├── AI-Assistant/           # Core AI-Assistant functionality
├── Tools/                  # Universal development tools
├── .vscode/               # VS Code integration
├── Examples/              # Project setup examples
└── Documentation/         # Guides and references
``````

## 🎯 Project Integration Examples

### New .NET Project
``````powershell
cd "C:\Dev"
ai-new-dotnet -ProjectName "MyApp" -Template "wpf"
ai-health  # Verifies .NET SDK, sets up build system
ai-debug-files -Pattern "**/*.cs" -AutoFix
``````

### Existing Node.js Project
``````powershell
cd "C:\Dev\MyExistingNodeApp"
ai-init-project -ProjectType "NodeJS"
ai-health  # Verifies Node.js, npm, installs dependencies
ai-debug-files -Pattern "**/*.js" -AutoFix
``````

## 📖 Documentation

- [Integration Guide](Documentation/INTEGRATION-GUIDE.md) - How to integrate with projects
- [Command Reference](Documentation/COMMAND-REFERENCE.md) - All available commands
- [Project Templates](Documentation/PROJECT-TEMPLATES.md) - Example project setups

---

**Origin**: Extracted from BusBuddy AI-Assistant development
**Version**: Universal 2.0
**Supports**: Multi-language, cross-project development environments
"@

$readmePath = Join-Path $WorkspacePath "README.md"
$readmeContent | Out-File -FilePath $readmePath -Encoding UTF8
Write-Host "✅ Universal README created" -ForegroundColor Green

# Create integration guide
$integrationContent = @"
# AI-Assistant Integration Guide

## Adding AI-Assistant to Existing Projects

### Step 1: Load AI-Assistant
``````powershell
# From your project directory
. "path\to\AI-Assistant-Workspace\AI-Assistant\Scripts\load-ai-assistant-profile.ps1"
``````

### Step 2: Initialize for Your Project Type
``````powershell
ai-init-project -ProjectType "DotNet"     # .NET projects
ai-init-project -ProjectType "NodeJS"     # Node.js projects
ai-init-project -ProjectType "Python"     # Python projects
ai-init-project -ProjectType "Generic"    # Other projects
``````

### Step 3: Verify Setup
``````powershell
ai-health    # Should show all green checkmarks
ai-help      # Shows available commands for your project type
``````

## Project-Specific Features

### .NET Projects
- Automatic solution/project detection
- NuGet package management
- C# and XAML file debugging
- dotnet CLI integration
- MSBuild integration

### Node.js Projects
- package.json detection
- npm/yarn support
- JavaScript/TypeScript debugging
- ESLint and Prettier integration
- Webpack/Vite support

### Python Projects
- requirements.txt/setup.py detection
- pip package management
- Python file debugging
- Black formatter integration
- pytest support

## Command Mapping

| Universal Command | .NET | Node.js | Python |
|------------------|------|---------|--------|
| ai-build | dotnet build | npm run build | python setup.py build |
| ai-test | dotnet test | npm test | pytest |
| ai-format | dotnet format | prettier . | black . |
| ai-debug-files | C#/XAML debugging | JS/TS debugging | Python debugging |
"@

$docsDir = Join-Path $WorkspacePath "Documentation"
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
$integrationPath = Join-Path $docsDir "INTEGRATION-GUIDE.md"
$integrationContent | Out-File -FilePath $integrationPath -Encoding UTF8
Write-Host "✅ Integration guide created" -ForegroundColor Green

#endregion

#region Phase 5: Clean Up Original Directory

Write-Host "🧹 Phase 5: Cleaning up original directory..." -ForegroundColor Yellow

# Remove BusBuddy project files from original location
$busBuddyItems = @(
    "BusBuddy.sln", "*.csproj", "BusBuddy.WPF", "BusBuddy.Core",
    "BusBuddy.Tests", "bin", "obj", "TestResults",
    "BUS-*.md", "CRUD-*.md", "Properties"
)

foreach ($item in $busBuddyItems) {
    if (Test-Path $item) {
        Remove-Item -Path $item -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "🗑️ Removed: $item" -ForegroundColor Gray
    }
}

Write-Host "✅ BusBuddy project files removed from original location" -ForegroundColor Green

#endregion

# Final summary
Write-Host @"

🎉 TRANSFORMATION COMPLETE!
═══════════════════════════════════════════════════════════════════════

📁 Universal AI-Assistant Workspace: $WorkspacePath

✅ What's Ready:
• Universal AI-Assistant profile (ai- commands)
• Language-agnostic file debugger
• VS Code integration with universal tasks
• Project-type detection and adaptation
• GitHub automation tools
• Comprehensive documentation

🚀 Next Steps:
1. Open workspace: code "$WorkspacePath"
2. Test with a new project: ai-init-project -ProjectType "DotNet"
3. Use universal commands: ai-health, ai-debug-files, ai-build

🚌 BusBuddy Status:
• Project files removed from this machine
• Archive created: $(if($KeepArchive){"BusBuddy-Project-Archive.zip"}else{"Not created"})
• Continue development on other laptop via Google Drive sync

📚 Documentation:
• README.md - Getting started guide
• Documentation/INTEGRATION-GUIDE.md - Project integration
• AI-Assistant/ - Core functionality and scripts

═══════════════════════════════════════════════════════════════════════
"@ -ForegroundColor Green

Write-Host "🎯 Your machine is now ready for universal AI-Assistant development!" -ForegroundColor Cyan
