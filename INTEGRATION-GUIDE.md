# 🚀 Universal AI-Assistant Project Integration Guide

Step-by-step guide for integrating Universal AI-Assistant with any development project.

## 📋 Quick Start Checklist

### ✅ Prerequisites
- [ ] PowerShell 7.5+ installed
- [ ] VS Code installed (recommended)
- [ ] Git installed
- [ ] Project-specific tools (see sections below)

### ✅ Initial Setup
1. [ ] Load AI-Assistant profile
2. [ ] Run health check
3. [ ] Initialize for your project type
4. [ ] Test core commands

## 🎯 Project Type Integration

### .NET Projects (C#, F#, VB.NET)

#### Prerequisites
```powershell
# Verify .NET SDK
dotnet --version  # Should be 6.0 or later
```

#### Setup Steps
```powershell
# 1. Load AI-Assistant
. "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"

# 2. Initialize for .NET
ai-init-project -ProjectType "DotNet"

# 3. Health check
ai-health -Verbose

# 4. Test with sample project
ai-debug-files -Pattern "**/*.cs" -ValidateOnly
```

#### Project Structure Support
```
MyProject/
├── src/
│   ├── MyProject.csproj
│   ├── Program.cs
│   └── Models/
├── tests/
│   └── MyProject.Tests.csproj
├── .editorconfig
└── Directory.Build.props
```

#### Available Commands
- `ai-build` - Uses `dotnet build`
- `ai-test` - Uses `dotnet test`
- `ai-debug-files` - C# code analysis with Roslyn
- `ai-format-files` - Uses `dotnet format`

---

### Node.js Projects (JavaScript, TypeScript)

#### Prerequisites
```powershell
# Verify Node.js and npm
node --version  # Should be 18.0 or later
npm --version
```

#### Setup Steps
```powershell
# 1. Load AI-Assistant
. "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"

# 2. Initialize for Node.js
ai-init-project -ProjectType "NodeJS"

# 3. Install dev tools (if not present)
npm install -g eslint prettier typescript

# 4. Test setup
ai-health -ProjectPath "."
ai-debug-files -Pattern "**/*.js" -ValidateOnly
```

#### Project Structure Support
```
my-app/
├── src/
│   ├── index.js (or .ts)
│   └── components/
├── tests/
├── package.json
├── .eslintrc.js
├── .prettierrc
└── tsconfig.json (for TypeScript)
```

#### Available Commands
- `ai-build` - Uses `npm run build` or `yarn build`
- `ai-test` - Uses `npm test` or `yarn test`
- `ai-debug-files` - ESLint + Prettier analysis
- `ai-format-files` - Uses Prettier

---

### Python Projects

#### Prerequisites
```powershell
# Verify Python
python --version  # Should be 3.8 or later
pip --version
```

#### Setup Steps
```powershell
# 1. Load AI-Assistant
. "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"

# 2. Initialize for Python
ai-init-project -ProjectType "Python"

# 3. Install dev tools
pip install flake8 black mypy pytest

# 4. Test setup
ai-health -Verbose
ai-debug-files -Pattern "**/*.py" -ValidateOnly
```

#### Project Structure Support
```
my-python-project/
├── src/
│   ├── __init__.py
│   └── main.py
├── tests/
│   └── test_main.py
├── requirements.txt
├── setup.py
├── .flake8
└── pyproject.toml
```

#### Available Commands
- `ai-build` - Uses `python setup.py build`
- `ai-test` - Uses `pytest`
- `ai-debug-files` - flake8, mypy, black analysis
- `ai-format-files` - Uses black

---

### PowerShell Projects

#### Prerequisites
```powershell
# Already have PowerShell 7.5+ for AI-Assistant
$PSVersionTable.PSVersion
```

#### Setup Steps
```powershell
# 1. Load AI-Assistant
. "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"

# 2. Initialize for PowerShell
ai-init-project -ProjectType "PowerShell"

# 3. Install dev tools
Install-Module -Name PSScriptAnalyzer -Force
Install-Module -Name Pester -Force

# 4. Test setup
ai-health
ai-debug-files -Pattern "**/*.ps1" -ValidateOnly
```

#### Project Structure Support
```
MyModule/
├── MyModule.psd1
├── MyModule.psm1
├── Public/
├── Private/
├── Tests/
│   └── MyModule.Tests.ps1
└── docs/
```

#### Available Commands
- `ai-test` - Uses Pester
- `ai-debug-files` - PSScriptAnalyzer analysis
- `ai-format-files` - PowerShell formatting

---

### Generic Projects

For projects that don't fit standard categories or use custom build systems.

#### Setup Steps
```powershell
# 1. Load AI-Assistant
. "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"

# 2. Initialize as generic
ai-init-project -ProjectType "Generic"

# 3. Configure custom commands
ai-config -Set "BuildCommand" "make build"
ai-config -Set "TestCommand" "make test"
ai-config -Set "FormatCommand" "make format"

# 4. Test setup
ai-health
```

#### Available Commands
- `ai-build` - Uses configured BuildCommand
- `ai-test` - Uses configured TestCommand
- `ai-debug-files` - Basic file validation
- `ai-format-files` - Uses configured FormatCommand

## 🔧 VS Code Integration

### Automatic Task Creation
AI-Assistant can create VS Code tasks for your project:

```powershell
# Generate VS Code tasks.json
ai-init-project -ProjectType "DotNet" -CreateVSTasks
```

### Generated Tasks Include:
- **AI: Health Check** - Run `ai-health`
- **AI: Build Project** - Run `ai-build`
- **AI: Test Project** - Run `ai-test`
- **AI: Debug Files** - Run `ai-debug-files`
- **AI: Format Files** - Run `ai-format-files`

### Custom Task Integration
Add to your existing `.vscode/tasks.json`:

```json
{
    "label": "AI: Debug Files",
    "type": "shell",
    "command": "pwsh.exe",
    "args": [
        "-ExecutionPolicy", "Bypass",
        "-Command",
        ". 'C:\\Dev\\AI-Assistant-Workspace\\load-ai-assistant-profile.ps1'; ai-debug-files -AutoFix -Verbose"
    ],
    "group": "build",
    "detail": "Run AI-Assistant file debugging with auto-fix"
}
```

## 🔄 Workflow Integration

### Git Hooks Integration
Add AI-Assistant to your git hooks for automatic formatting:

**`.git/hooks/pre-commit`** (Windows):
```powershell
#!/usr/bin/env pwsh
. "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"
ai-debug-files -Pattern "**/*.{cs,js,py,ps1}" -AutoFix
if ($LASTEXITCODE -ne 0) { exit 1 }
```

### CI/CD Integration

#### GitHub Actions
```yaml
name: AI-Assistant Quality Check
on: [push, pull_request]

jobs:
  quality-check:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v3
    - name: Setup PowerShell
      uses: microsoft/setup-powershell@v1
    - name: Load AI-Assistant
      run: . "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"
    - name: Run Health Check
      run: ai-health -Verbose
    - name: Debug Files
      run: ai-debug-files -ValidateOnly -Verbose
    - name: Build Project
      run: ai-build
    - name: Run Tests
      run: ai-test
```

#### Azure DevOps
```yaml
trigger:
- main

pool:
  vmImage: 'windows-latest'

steps:
- powershell: |
    . "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"
    ai-health -Verbose
    ai-debug-files -ValidateOnly
    ai-build
    ai-test
  displayName: 'AI-Assistant Quality Pipeline'
```

## 📊 Multi-Project Management

### Working with Multiple Projects
```powershell
# Set up workspace for multiple projects
Set-Location "C:\Dev\Projects"

# Health check all projects
Get-ChildItem -Directory | ForEach-Object {
    Set-Location $_.FullName
    Write-Host "Checking $($_.Name)..." -ForegroundColor Cyan
    ai-health
}

# Format all projects
Get-ChildItem -Directory | ForEach-Object {
    Set-Location $_.FullName
    ai-format-files
}
```

### Profile Management
```powershell
# Save project-specific profiles
Set-Location "C:\Dev\MyWebApp"
ai-init-project -ProjectType "NodeJS"
ai-config -Set "AutoFormat" $true
ai-profile -Save "WebApp"

# Load profiles for different projects
Set-Location "C:\Dev\MyAPI"
ai-profile -Load "WebApp"
```

## ⚙️ Configuration Best Practices

### Project-Specific Configuration
Create `.aiassistant/config.json` in your project root:

```json
{
  "projectType": "DotNet",
  "autoFormat": true,
  "preferTools": true,
  "filePatterns": {
    "source": ["**/*.cs", "**/*.xaml"],
    "test": ["**/*Test.cs", "**/*Tests.cs"],
    "config": ["**/*.json", "**/*.xml"]
  },
  "buildCommand": "dotnet build --configuration Release",
  "testCommand": "dotnet test --verbosity normal",
  "formatCommand": "dotnet format"
}
```

### Team Settings
Create `.aiassistant/team-config.json` for shared team settings:

```json
{
  "codeStyle": {
    "indentSize": 4,
    "useSpaces": true,
    "endOfLine": "crlf"
  },
  "quality": {
    "enforceFormat": true,
    "requireTests": true,
    "codeAnalysis": true
  },
  "tools": {
    "preferredFormatter": "dotnet-format",
    "preferredLinter": "roslyn"
  }
}
```

## 🚨 Troubleshooting Integration Issues

### Common Issues and Solutions

#### ❌ "Command not found: ai-health"
**Solution**: Reload the AI-Assistant profile
```powershell
. "C:\Dev\AI-Assistant-Workspace\load-ai-assistant-profile.ps1"
```

#### ❌ "Project type not detected"
**Solution**: Manually specify project type
```powershell
ai-init-project -ProjectType "DotNet" -Force
```

#### ❌ "Build command failed"
**Solution**: Check project dependencies
```powershell
ai-diagnose -Export "diagnosis.json"
# Review the generated diagnosis file
```

#### ❌ "File debugging not working"
**Solution**: Verify language tools are installed
```powershell
# For .NET
dotnet --version

# For Node.js
node --version
npm list -g eslint prettier

# For Python
python -m pip list | findstr "flake8 black mypy"
```

### Advanced Troubleshooting
```powershell
# Full diagnostic export
ai-diagnose -Export "full-diagnosis.json"

# Reset to defaults
ai-reset -Confirm

# Re-initialize from scratch
ai-init-project -ProjectType "YourType" -Force
```

## 📚 Next Steps

After successful integration:

1. **Read the [Command Reference](COMMAND-REFERENCE.md)** for all available commands
2. **Configure VS Code tasks** for your workflow
3. **Set up git hooks** for automatic quality checks
4. **Create team configuration** for consistent development
5. **Integrate with CI/CD** for automated quality gates

## 🤝 Team Adoption

### Onboarding New Developers
1. Share this integration guide
2. Provide team configuration files
3. Run initial setup together
4. Document project-specific customizations

### Maintaining Consistency
- Use shared configuration files
- Regular `ai-health` checks in team meetings
- Automated quality checks in PR process
- Team training on AI-Assistant commands

---

**💡 Remember**: AI-Assistant adapts to your project - the more you use it, the better it gets at understanding your specific needs!
