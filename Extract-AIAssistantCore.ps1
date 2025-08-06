#Requires -Version 7.5

<#
.SYNOPSIS
    AI-Assistant Core Extraction Script

.DESCRIPTION
    Extracts the BusBuddy AI-Assistant into a reusable, project-agnostic package.
    Creates the core modules and project-specific adapters.

.PARAMETER OutputPath
    Where to create the extracted AI-Assistant package

.PARAMETER IncludeProjectAdapter
    Whether to create a BusBuddy-specific adapter

.EXAMPLE
    .\Extract-AIAssistantCore.ps1 -OutputPath "C:\Dev\AI-Assistant-Core"
#>

param(
    [Parameter(Mandatory)]
    [string]$OutputPath,

    [switch]$IncludeProjectAdapter
)

Write-Host @"
🤖 AI-ASSISTANT CORE EXTRACTION
═══════════════════════════════════════════════════════════════════════
Extracting reusable AI-Assistant components from BusBuddy
Output: $OutputPath
═══════════════════════════════════════════════════════════════════════
"@ -ForegroundColor Cyan

# Ensure output directory exists
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "✅ Created output directory: $OutputPath" -ForegroundColor Green
}

#region Core Package Structure Creation

function New-CorePackageStructure {
    param([string]$BasePath)

    $structure = @(
        "Core/Modules",
        "Core/Config",
        "Core/Templates",
        "Scripts",
        "Documentation",
        "Tests",
        "Examples"
    )

    foreach ($dir in $structure) {
        $fullPath = Join-Path $BasePath $dir
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "📁 Created: $dir" -ForegroundColor Gray
    }
}

function New-ProjectAdapterStructure {
    param([string]$BasePath, [string]$ProjectName)

    $adapterPath = Join-Path $BasePath "$ProjectName-AI-Assistant"
    $structure = @(
        "Config",
        "Scripts",
        ".vscode",
        "Tools"
    )

    foreach ($dir in $structure) {
        $fullPath = Join-Path $adapterPath $dir
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "📁 Created: $ProjectName-AI-Assistant/$dir" -ForegroundColor Gray
    }

    return $adapterPath
}

#endregion

#region Core Module Extraction

function Extract-CoreModule {
    param([string]$OutputPath)

    $coreModulePath = Join-Path $OutputPath "Core/Modules/AIAssistantCore.psm1"

    $coreModuleContent = @"
#Requires -Version 7.5

<#
.SYNOPSIS
    AI-Assistant Core Module - Universal Development Environment Framework

.DESCRIPTION
    Core functionality for AI-Assistant that works across different project types.
    Provides universal development environment setup, health checking, and tool integration.

.NOTES
    Version: 2.0
    Extracted from BusBuddy AI-Assistant
    Project-agnostic and reusable
#>

# Module variables
`$script:ModuleRoot = `$PSScriptRoot
`$script:ConfigCache = @{}
`$script:RegisteredCommands = @{}

#region Core Functions

function Initialize-DevelopmentEnvironment {
    <#
    .SYNOPSIS
        Initializes AI-Assistant development environment for any project
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]`$ProjectPath,

        [string]`$ProjectType = "Auto",

        [hashtable]`$CustomConfig = @{}
    )

    Write-Host "🤖 Initializing AI-Assistant Development Environment" -ForegroundColor Cyan

    # Detect project type if not specified
    if (`$ProjectType -eq "Auto") {
        `$ProjectType = Get-ProjectType -Path `$ProjectPath
    }

    # Load base configuration
    `$config = Get-ProjectConfiguration -ProjectType `$ProjectType -CustomConfig `$CustomConfig

    # Set up environment variables
    Set-ProjectEnvironmentVariables -Config `$config -ProjectPath `$ProjectPath

    # Register core commands
    Register-CoreCommands -Config `$config

    Write-Host "✅ Environment initialized for `$ProjectType project" -ForegroundColor Green
    return `$config
}

function Get-ProjectType {
    <#
    .SYNOPSIS
        Automatically detects project type based on files and structure
    #>
    [CmdletBinding()]
    param([string]`$Path)

    `$detectionRules = @{
        "DotNet" = @("*.sln", "*.csproj", "*.fsproj", "*.vbproj")
        "NodeJS" = @("package.json", "node_modules")
        "Python" = @("requirements.txt", "setup.py", "pyproject.toml")
        "PowerShell" = @("*.psd1", "*.psm1")
        "Web" = @("index.html", "package.json", "webpack.config.js")
    }

    foreach (`$type in `$detectionRules.Keys) {
        foreach (`$pattern in `$detectionRules[`$type]) {
            if (Get-ChildItem -Path `$Path -Filter `$pattern -Recurse -ErrorAction SilentlyContinue) {
                Write-Host "🔍 Detected project type: `$type" -ForegroundColor Yellow
                return `$type
            }
        }
    }

    Write-Host "⚠️ Unknown project type, using Generic" -ForegroundColor Yellow
    return "Generic"
}

function Test-ProjectHealth {
    <#
    .SYNOPSIS
        Universal project health check that adapts to project type
    #>
    [CmdletBinding()]
    param(
        [string]`$ProjectPath = (Get-Location),
        [string]`$ProjectType = "Auto"
    )

    Write-Host "🔍 Running Project Health Check" -ForegroundColor Cyan

    `$issues = @()
    `$checks = @()

    # Universal checks
    `$checks += @{
        Name = "PowerShell Version"
        Test = { `$PSVersionTable.PSVersion.Major -ge 7 }
        Message = "PowerShell 7+ required"
    }

    `$checks += @{
        Name = "Git Repository"
        Test = { Test-Path (Join-Path `$ProjectPath ".git") }
        Message = "Not a git repository"
    }

    # Project-specific checks
    switch (`$ProjectType) {
        "DotNet" {
            `$checks += @{
                Name = ".NET SDK"
                Test = { try { dotnet --version; `$true } catch { `$false } }
                Message = ".NET SDK not found"
            }
        }
        "NodeJS" {
            `$checks += @{
                Name = "Node.js"
                Test = { try { node --version; `$true } catch { `$false } }
                Message = "Node.js not found"
            }
        }
    }

    # Run checks
    foreach (`$check in `$checks) {
        try {
            `$result = & `$check.Test
            if (`$result) {
                Write-Host "✅ `$(`$check.Name)" -ForegroundColor Green
            } else {
                Write-Host "❌ `$(`$check.Name): `$(`$check.Message)" -ForegroundColor Red
                `$issues += `$check.Message
            }
        } catch {
            Write-Host "❌ `$(`$check.Name): Error during check" -ForegroundColor Red
            `$issues += "`$(`$check.Name): `$(`$_.Exception.Message)"
        }
    }

    if (`$issues.Count -eq 0) {
        Write-Host "🎉 Project health check passed!" -ForegroundColor Green
        return `$true
    } else {
        Write-Host "⚠️ Found `$(`$issues.Count) issues" -ForegroundColor Yellow
        return `$false
    }
}

function Register-ProjectCommand {
    <#
    .SYNOPSIS
        Registers a project-specific command with AI-Assistant
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]`$Name,

        [Parameter(Mandatory)]
        [scriptblock]`$ScriptBlock,

        [string]`$Description = "",

        [string[]]`$Aliases = @()
    )

    # Register the command
    `$script:RegisteredCommands[`$Name] = @{
        ScriptBlock = `$ScriptBlock
        Description = `$Description
        Aliases = `$Aliases
    }

    # Create function dynamically
    `$functionName = "ai-`$Name"
    New-Item -Path "Function:\`$functionName" -Value `$ScriptBlock -Force | Out-Null

    # Create aliases
    foreach (`$alias in `$Aliases) {
        New-Alias -Name `$alias -Value `$functionName -Force -Scope Global
    }

    Write-Host "📝 Registered command: `$functionName" -ForegroundColor Gray
}

function Get-RegisteredCommands {
    <#
    .SYNOPSIS
        Lists all registered AI-Assistant commands
    #>
    Write-Host "🤖 AI-ASSISTANT REGISTERED COMMANDS" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

    foreach (`$cmd in `$script:RegisteredCommands.Keys) {
        `$info = `$script:RegisteredCommands[`$cmd]
        Write-Host "ai-`$cmd" -ForegroundColor Yellow -NoNewline
        if (`$info.Aliases.Count -gt 0) {
            Write-Host " (`$(`$info.Aliases -join ', '))" -ForegroundColor Gray -NoNewline
        }
        Write-Host ""
        if (`$info.Description) {
            Write-Host "  `$(`$info.Description)" -ForegroundColor Gray
        }
        Write-Host ""
    }
}

#endregion

#region Configuration Management

function Get-ProjectConfiguration {
    param(
        [string]`$ProjectType,
        [hashtable]`$CustomConfig = @{}
    )

    # Base configuration
    `$baseConfig = @{
        ProjectType = `$ProjectType
        Version = "2.0"
        AutoFormat = `$true
        PreferTools = `$true
        LogLevel = "INFO"
    }

    # Project-type specific configuration
    `$typeConfig = switch (`$ProjectType) {
        "DotNet" { @{
            BuildCommand = "dotnet build"
            TestCommand = "dotnet test"
            FormatCommand = "dotnet format"
            FileExtensions = @(".cs", ".xaml", ".csproj", ".sln")
        }}
        "NodeJS" { @{
            BuildCommand = "npm run build"
            TestCommand = "npm test"
            FormatCommand = "npm run format"
            FileExtensions = @(".js", ".ts", ".json", ".md")
        }}
        "Python" { @{
            BuildCommand = "python setup.py build"
            TestCommand = "pytest"
            FormatCommand = "black ."
            FileExtensions = @(".py", ".pyi", ".pyx")
        }}
        default { @{
            BuildCommand = "echo 'No build command configured'"
            TestCommand = "echo 'No test command configured'"
            FormatCommand = "echo 'No format command configured'"
            FileExtensions = @("*")
        }}
    }

    # Merge configurations
    `$config = `$baseConfig + `$typeConfig + `$CustomConfig
    return `$config
}

function Set-ProjectEnvironmentVariables {
    param(
        [hashtable]`$Config,
        [string]`$ProjectPath
    )

    # Set universal AI-Assistant variables
    `$env:AI_ASSISTANT_VERSION = `$Config.Version
    `$env:AI_ASSISTANT_PROJECT_TYPE = `$Config.ProjectType
    `$env:AI_ASSISTANT_PROJECT_PATH = `$ProjectPath
    `$env:AI_ASSISTANT_AUTO_FORMAT = `$Config.AutoFormat
    `$env:AI_ASSISTANT_PREFER_TOOLS = `$Config.PreferTools

    Write-Host "🔧 Set AI-Assistant environment variables" -ForegroundColor Gray
}

#endregion

#region Command Registration

function Register-CoreCommands {
    param([hashtable]`$Config)

    # Health check command
    Register-ProjectCommand -Name "health" -Description "Run project health check" -ScriptBlock {
        Test-ProjectHealth -ProjectPath `$env:AI_ASSISTANT_PROJECT_PATH -ProjectType `$env:AI_ASSISTANT_PROJECT_TYPE
    } -Aliases @("health-check", "status")

    # Help command
    Register-ProjectCommand -Name "help" -Description "Show all available commands" -ScriptBlock {
        Get-RegisteredCommands
    } -Aliases @("commands", "?")

    # Build command (project-specific)
    Register-ProjectCommand -Name "build" -Description "Build the project" -ScriptBlock {
        `$buildCmd = `$Config.BuildCommand
        Write-Host "🔨 Building project: `$buildCmd" -ForegroundColor Cyan
        Invoke-Expression `$buildCmd
    }

    # Test command (project-specific)
    Register-ProjectCommand -Name "test" -Description "Run project tests" -ScriptBlock {
        `$testCmd = `$Config.TestCommand
        Write-Host "🧪 Running tests: `$testCmd" -ForegroundColor Cyan
        Invoke-Expression `$testCmd
    }

    # Format command (project-specific)
    Register-ProjectCommand -Name "format" -Description "Format project files" -ScriptBlock {
        `$formatCmd = `$Config.FormatCommand
        Write-Host "🎨 Formatting files: `$formatCmd" -ForegroundColor Cyan
        Invoke-Expression `$formatCmd
    }
}

#endregion

# Export functions
Export-ModuleMember -Function @(
    'Initialize-DevelopmentEnvironment',
    'Get-ProjectType',
    'Test-ProjectHealth',
    'Register-ProjectCommand',
    'Get-RegisteredCommands'
)
"@

    $coreModuleContent | Out-File -FilePath $coreModulePath -Encoding UTF8
    Write-Host "✅ Created AIAssistantCore.psm1" -ForegroundColor Green
}

function Extract-ProjectEnvironmentModule {
    param([string]$OutputPath)

    $modulePath = Join-Path $OutputPath "Core/Modules/ProjectEnvironment.psm1"

    $moduleContent = @"
#Requires -Version 7.5

<#
.SYNOPSIS
    Project Environment Module - Project-specific environment management

.DESCRIPTION
    Handles project-specific environment setup, dependency management, and build system integration.
#>

function Initialize-DotNetEnvironment {
    param([string]`$ProjectPath)

    Write-Host "🔷 Initializing .NET Environment" -ForegroundColor Blue

    # Verify .NET SDK
    try {
        `$version = dotnet --version
        Write-Host "✅ .NET SDK: `$version" -ForegroundColor Green
    } catch {
        Write-Host "❌ .NET SDK not found" -ForegroundColor Red
        return `$false
    }

    # Restore packages
    if (Get-ChildItem -Path `$ProjectPath -Filter "*.sln" -Recurse) {
        Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
        dotnet restore "`$ProjectPath"
    }

    return `$true
}

function Initialize-NodeEnvironment {
    param([string]`$ProjectPath)

    Write-Host "💚 Initializing Node.js Environment" -ForegroundColor Green

    # Verify Node.js
    try {
        `$version = node --version
        Write-Host "✅ Node.js: `$version" -ForegroundColor Green
    } catch {
        Write-Host "❌ Node.js not found" -ForegroundColor Red
        return `$false
    }

    # Install dependencies
    if (Test-Path (Join-Path `$ProjectPath "package.json")) {
        Write-Host "📦 Installing npm packages..." -ForegroundColor Yellow
        Push-Location `$ProjectPath
        npm install
        Pop-Location
    }

    return `$true
}

Export-ModuleMember -Function @(
    'Initialize-DotNetEnvironment',
    'Initialize-NodeEnvironment'
)
"@

    $moduleContent | Out-File -FilePath $modulePath -Encoding UTF8
    Write-Host "✅ Created ProjectEnvironment.psm1" -ForegroundColor Green
}

#endregion

#region Configuration Templates

function Create-ConfigurationTemplates {
    param([string]$OutputPath)

    # Default settings template
    $defaultSettings = @{
        Version            = "2.0"
        AutoUpdate         = $true
        LogLevel           = "INFO"
        DefaultProjectType = "Auto"
        GlobalSettings     = @{
            AutoFormat        = $true
            PreferTools       = $true
            ShowVerboseOutput = $false
        }
        ProjectTypes       = @{
            DotNet = @{
                BuildCommand   = "dotnet build"
                TestCommand    = "dotnet test"
                FormatCommand  = "dotnet format"
                FileExtensions = @(".cs", ".xaml", ".csproj", ".sln")
                RequiredTools  = @("dotnet")
            }
            NodeJS = @{
                BuildCommand   = "npm run build"
                TestCommand    = "npm test"
                FormatCommand  = "npm run format"
                FileExtensions = @(".js", ".ts", ".json", ".md")
                RequiredTools  = @("node", "npm")
            }
        }
    }

    $settingsPath = Join-Path $OutputPath "Core/Config/DefaultSettings.json"
    $defaultSettings | ConvertTo-Json -Depth 10 | Out-File -FilePath $settingsPath -Encoding UTF8
    Write-Host "✅ Created DefaultSettings.json" -ForegroundColor Green
}

#endregion

#region Installation Script

function Create-InstallationScript {
    param([string]$OutputPath)

    $installScriptPath = Join-Path $OutputPath "Scripts/Install-AIAssistant.ps1"

    $installScript = @"
#Requires -Version 7.5

<#
.SYNOPSIS
    AI-Assistant Installation Script

.DESCRIPTION
    Installs AI-Assistant Core package and sets up project integration.

.PARAMETER ProjectPath
    Path to the project where AI-Assistant should be installed

.PARAMETER ProjectType
    Type of project (DotNet, NodeJS, Python, etc.)

.EXAMPLE
    .\Install-AIAssistant.ps1 -ProjectPath "C:\Dev\MyProject" -ProjectType "DotNet"
#>

param(
    [Parameter(Mandatory)]
    [string]`$ProjectPath,

    [string]`$ProjectType = "Auto",

    [switch]`$CreateProfile,

    [switch]`$SetupVSCode
)

Write-Host @"
🤖 AI-ASSISTANT INSTALLATION
═══════════════════════════════════════════════════════════════════════
Installing AI-Assistant Core for project: `$ProjectPath
Project Type: `$ProjectType
═══════════════════════════════════════════════════════════════════════
"@ -ForegroundColor Cyan

    # Import AI-Assistant Core
    Import-Module "`$PSScriptRoot\..\Core\Modules\AIAssistantCore.psm1" -Force

    # Initialize project
    `$config = Initialize-DevelopmentEnvironment -ProjectPath `$ProjectPath -ProjectType `$ProjectType

    if (`$CreateProfile) {
        Write-Host "📝 Creating project profile..." -ForegroundColor Yellow
        # Create project-specific profile
        `$profileContent = @"
# AI-Assistant Project Profile
# Auto-generated for `$(Split-Path `$ProjectPath -Leaf)

Import-Module "`$PSScriptRoot\..\AI-Assistant-Core\Core\Modules\AIAssistantCore.psm1" -Force

# Initialize project environment
Initialize-DevelopmentEnvironment -ProjectPath "`$ProjectPath" -ProjectType "`$ProjectType"

Write-Host "🤖 AI-Assistant loaded for `$(Split-Path `$ProjectPath -Leaf)" -ForegroundColor Cyan
"@

        `$profilePath = Join-Path `$ProjectPath "load-ai-assistant-profile.ps1"
        `$profileContent | Out-File -FilePath `$profilePath -Encoding UTF8
        Write-Host "✅ Created project profile: `$profilePath" -ForegroundColor Green
    }

    if (`$SetupVSCode) {
        Write-Host "🔧 Setting up VS Code integration..." -ForegroundColor Yellow

        `$vscodeDir = Join-Path `$ProjectPath ".vscode"
        if (-not (Test-Path `$vscodeDir)) {
            New-Item -ItemType Directory -Path `$vscodeDir -Force | Out-Null
        }

        # Create tasks.json for AI-Assistant commands
        `$tasks = @{
            version = "2.0.0"
            tasks   = @(
                @{
                    label   = "AI: Health Check"
                    type    = "shell"
                    command = "pwsh"
                    args    = @("-Command", "ai-health")
                    group   = "test"
                },
                @{
                    label   = "AI: Build Project"
                    type    = "shell"
                    command = "pwsh"
                    args    = @("-Command", "ai-build")
                    group   = "build"
                },
                @{
                    label   = "AI: Format Files"
                    type    = "shell"
                    command = "pwsh"
                    args    = @("-Command", "ai-format")
                    group   = "build"
                }
            )
        }

        `$tasksPath = Join-Path `$vscodeDir "tasks.json"
        `$tasks | ConvertTo-Json -Depth 10 | Out-File -FilePath `$tasksPath -Encoding UTF8
        Write-Host "✅ Created VS Code tasks: `$tasksPath" -ForegroundColor Green
    }

    Write-Host "🎉 AI-Assistant installation complete!" -ForegroundColor Green
    Write-Host "Run 'ai-help' to see available commands." -ForegroundColor Yellow
    "@

    $installScript | Out-File -FilePath $installScriptPath -Encoding UTF8
    Write-Host "✅ Created Install-AIAssistant.ps1" -ForegroundColor Green
}

#endregion

#region Main Execution

Write-Host "🏗️ Creating core package structure..." -ForegroundColor Yellow
New-CorePackageStructure -BasePath $OutputPath

Write-Host "📦 Extracting core modules..." -ForegroundColor Yellow
Extract-CoreModule -OutputPath $OutputPath
Extract-ProjectEnvironmentModule -OutputPath $OutputPath

Write-Host "⚙️ Creating configuration templates..." -ForegroundColor Yellow
Create-ConfigurationTemplates -OutputPath $OutputPath

Write-Host "🔧 Creating installation scripts..." -ForegroundColor Yellow
Create-InstallationScript -OutputPath $OutputPath

# Create BusBuddy project adapter if requested
if ($IncludeProjectAdapter) {
    Write-Host "🚌 Creating BusBuddy project adapter..." -ForegroundColor Yellow
    $adapterPath = New-ProjectAdapterStructure -BasePath $OutputPath -ProjectName "BusBuddy"

    # Copy BusBuddy-specific configuration
    $busBuddyConfig = @{
        ProjectName = "BusBuddy"
        ProjectType = "DotNetWPF"
        Framework = "net8.0-windows"
        BuildCommand = "dotnet build BusBuddy.sln"
        RunCommand = "dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj"
        TestCommand = "dotnet test BusBuddy.sln"
        FormatCommand = "dotnet format BusBuddy.sln"
        CustomTools = @{
            FileDebugger = "Tools/Scripts/BusBuddy-File-Debugger.ps1"
            GitHubAutomation = "Tools/Scripts/GitHub/BusBuddy-GitHub-Automation.ps1"
        }
        Environment = @{
            PreferTools = $true
            AutoFormat = $true
            SyncfusionLicense = $true
        }
    }

    $configPath = Join-Path $adapterPath "Config/busbuddy-config.json"
    $busBuddyConfig | ConvertTo-Json -Depth 10 | Out-File -FilePath $configPath -Encoding UTF8
    Write-Host "✅ Created BusBuddy adapter configuration" -ForegroundColor Green
}

# Create README
$readmePath = Join-Path $OutputPath "README.md"
$readmeContent = @"
    # AI-Assistant Core Package

    A reusable development environment framework extracted from the BusBuddy project.

    ## Quick Start

    1. Install AI-Assistant in your project:
    ``````powershell
    .\Scripts\Install-AIAssistant.ps1 -ProjectPath "C:\Dev\YourProject" -CreateProfile -SetupVSCode
    ``````

    2. Load the profile in your project:
    ``````powershell
    .\load-ai-assistant-profile.ps1
    ``````

    3. Use AI-Assistant commands:
    ``````powershell
    ai-help     # Show all commands
    ai-health   # Check project health
    ai-build    # Build project
    ai-format   # Format files
    ``````

    ## Supported Project Types

    - .NET (C#, F#, VB.NET)
    - Node.js (JavaScript, TypeScript)
    - Python
    - PowerShell
    - Generic projects

    ## Documentation

    - [Integration Guide](Documentation/INTEGRATION-GUIDE.md)
    - [API Reference](Documentation/API-REFERENCE.md)

    ## Version

    2.0 - Extracted from BusBuddy AI-Assistant
    "@

$readmeContent | Out-File -FilePath $readmePath -Encoding UTF8

Write-Host @"

    🎉 AI-ASSISTANT CORE EXTRACTION COMPLETE!
    ═══════════════════════════════════════════════════════════════════════

    📦 Package created at: $OutputPath

    📋 Next Steps:
    1. Review the extracted modules in Core/Modules/
    2. Test the installation script with a sample project
    3. Customize configuration templates as needed
    4. Add project-specific adapters for other project types

    🚀 To use in a new project:
    cd YourNewProject
    $OutputPath\Scripts\Install-AIAssistant.ps1 -ProjectPath . -CreateProfile -SetupVSCode

    ═══════════════════════════════════════════════════════════════════════
    "@ -ForegroundColor Green

#endregion
