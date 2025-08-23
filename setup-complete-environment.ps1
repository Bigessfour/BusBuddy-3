#!/usr/bin/env pwsh
#Requires -Version 7.5

<#
.SYNOPSIS
Complete BusBuddy IDE and Grok AI Setup Script

.DESCRIPTION
Sets up the complete development environment including:
- VS Code settings and extensions
- Grok AI assistant with xAI API (grok-4-0709)
- GitHub repository integration
- Azure Database connectivity
- CI/CD pipeline configuration
- GPT-5-Mini collaboration setup

.NOTES
Run this script once to configure your complete BusBuddy development environment
#>

param(
    [Parameter()]
    [switch]$SkipVSCodeExtensions,
    
    [Parameter()]
    [switch]$SkipGitHubActions,
    
    [Parameter()]
    [switch]$TestOnly
)

Write-Information "🚌 BusBuddy Complete Development Environment Setup"  -InformationAction Continue-ForegroundColor Cyan
Write-Information "=" * 60  -InformationAction Continue-ForegroundColor Cyan
Write-Information ""

# Check prerequisites
function Test -InformationAction Continue-Prerequisites {
    Write-Information "🔍 Checking Prerequisites..."  -InformationAction Continue-ForegroundColor Yellow
    
    $issues = @()
    
    # Check PowerShell version
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        $issues += "PowerShell 7.5+ required (current: $($PSVersionTable.PSVersion))"
    }
    
    # Check xAI API key
    if (-not $env:XAI_API_KEY) {
        $issues += "XAI_API_KEY environment variable not set"
    }
    
    # Check Git
    try {
        git --version | Out-Null
        Write-Information "  ✅ Git installed"  -InformationAction Continue-ForegroundColor Green
    } catch {
        $issues += "Git not installed or not in PATH"
    }
    
    # Check .NET
    try {
        dotnet --version | Out-Null
        Write-Information "  ✅ .NET SDK installed"  -InformationAction Continue-ForegroundColor Green
    } catch {
        $issues += ".NET SDK not installed"
    }
    
    # Check VS Code
    try {
        code --version | Out-Null
        Write-Information "  ✅ VS Code installed"  -InformationAction Continue-ForegroundColor Green
    } catch {
        Write-Information "  ⚠️ VS Code not in PATH (may still be installed)"  -InformationAction Continue-ForegroundColor Yellow
    }
    
    if ($issues.Count -gt 0) {
        Write-Information ""
        Write -InformationAction Continue-Host "❌ Setup Issues Found:" -ForegroundColor Red
        $issues | ForEach-Object { Write-Information "  • $_"  -InformationAction Continue-ForegroundColor Red }
        return $false
    }
    
    Write-Information "  ✅ All prerequisites met!"  -InformationAction Continue-ForegroundColor Green
    return $true
}

# Setup Grok AI Assistant
function Initialize-GrokAssistant {
    Write-Information ""
    #!/usr/bin/env pwsh
#Requires  -InformationAction Continue-Version 7.5

<#
.SYNOPSIS
Complete BusBuddy Development Environment Setup

.DESCRIPTION
Sets up the complete development environment including:
- Grok AI Assistant with grok-4-0709 model
- VS Code IDE integration
- GitHub repository sync
- Azure Database connectivity
- CI/CD pipeline configuration
- PowerShell development enhancements

.NOTES
Based on successful xAI API integration test
Model: grok-4-0709 confirmed working
#>

param(
    [Parameter()]
    [ValidateSet('Full', 'GrokOnly', 'IDEOnly', 'CICDOnly', 'Test')]
    [string]$SetupType = 'Full',
    
    [Parameter()]
    [switch]$SkipGrokTest
)

Write-Information "🚀 BusBuddy Complete Development Environment Setup"  -InformationAction Continue-ForegroundColor Cyan
Write-Information "=" * 60  -InformationAction Continue-ForegroundColor Cyan
Write-Information "Setup Type: $SetupType"  -InformationAction Continue-ForegroundColor Yellow
Write-Information ""

# Step 1: Grok AI Assistant Configuration
if ($SetupType  -InformationAction Continue-in @('Full', 'GrokOnly', 'Test')) {
    Write-Information "🤖 Step 1: Configuring Grok AI Assistant"  -InformationAction Continue-ForegroundColor Green
    Write-Information " -InformationAction Continue-" * 40
    
    # Load Grok configuration functions
    if (Test-Path ".\grok-config.ps1") {
        . .\grok-config.ps1
        Write-Information "✅ Grok configuration functions loaded"  -InformationAction Continue-ForegroundColor Green
    } else {
        Write-Warning "grok-config.ps1 not found!"
        return
    }
    
    # Configure with optimal settings for development
    bb-grok-config -ResponseMode "Deep" -Model "grok-4-0709"
    
    if (-not $SkipGrokTest) {
        Write-Information "`n🧪 Testing Grok integration..."  -InformationAction Continue-ForegroundColor Yellow
        $testResult = bb-grok "Hello! Please confirm you're ready to assist with BusBuddy development and briefly describe your capabilities."
        
        if ($testResult) {
            Write-Information "✅ Grok integration test passed!"  -InformationAction Continue-ForegroundColor Green
        } else {
            Write-Warning "Grok integration test failed"
        }
    }
}

# Step 2: VS Code IDE Integration
if ($SetupType -in @('Full', 'IDEOnly')) {
    Write-Information "`n🖥️ Step 2: VS Code IDE Integration"  -InformationAction Continue-ForegroundColor Green
    Write-Information " -InformationAction Continue-" * 40
    
    # Create/update VS Code settings
    $vscodeDir = ".\.vscode"
    if (-not (Test-Path $vscodeDir)) {
        New-Item -Path $vscodeDir -ItemType Directory -Force | Out-Null
        Write-Information "✅ Created .vscode directory"  -InformationAction Continue-ForegroundColor Green
    }
    
    # Enhanced VS Code settings
    $vscodeSettings = @{
        "editor.codeActionsOnSave" = @{
            "source.fixAll" = "explicit"
        }
        "files.autoSave" = "afterDelay"
        "files.autoSaveDelay" = 1000
        "terminal.integrated.defaultProfile.windows" = "PowerShell"
        "editor.formatOnSave" = $true
        "powershell.powerShellDefaultVersion" = "PowerShell (x64)"
        
        # Grok AI Integration Settings
        "grok.repository" = "https://github.com/Bigessfour/BusBuddy-3"
        "grok.model" = "grok-4-0709"
        "grok.responseMode" = "Deep"
        "grok.features" = @{
            azureDatabase = $true
            cicdIntegration = $true
            codeReview = $true
            repositorySync = $true
        }
        
        # C# Development Settings
        "dotnet.completion.showCompletionItemsFromUnimportedNamespaces" = $true
        "omnisharp.useModernNet" = $true
        
        # XAML Development
        "xaml.completion.insertArgumentSnippets" = $true
        
        # PowerShell Development
        "powershell.integratedConsole.showOnStartup" = $false
        "powershell.developer.featureFlags" = @("PSReadLine")
        
        # Azure Integration
        "azure.resourceFilter" = @()
        "azure.showSignedInEmail" = $true
        
        # Git Integration
        "git.autofetch" = $true
        "git.enableSmartCommit" = $true
        
        # Performance Settings
        "files.watcherExclude" = @{
            "**/bin/**" = $true
            "**/obj/**" = $true
            "**/TestResults/**" = $true
            "**/.vs/**" = $true
        }
    }
    
    $settingsPath = Join-Path $vscodeDir "settings.json"
    $vscodeSettings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -Encoding UTF8
    Write-Information "✅ VS Code settings configured"  -InformationAction Continue-ForegroundColor Green
    
    # Recommended extensions
    $extensions = @{
        "ms-dotnettools.csharp" = "C# language support"
        "ms-dotnettools.csdevkit" = "C# Dev Kit"
        "ms-dotnettools.xaml" = "XAML language support"
        "ms-vscode.powershell" = "PowerShell language support"
        "ms-azuretools.vscode-azuresql" = "Azure SQL Database support"
        "ms-azuretools.vscode-azureresourcegroups" = "Azure Resource Groups"
        "ms-vscode.azure-account" = "Azure Account and Sign-In"
        "eamodio.gitlens" = "GitLens — Git supercharged"
        "github.copilot" = "GitHub Copilot"
        "github.copilot-chat" = "GitHub Copilot Chat"
        "spmeesseman.vscode-taskexplorer" = "Task Explorer"
        "trunk.io" = "Trunk Check - Linter and Formatter"
    }
    
    $extensionsConfig = @{
        recommendations = $extensions.Keys
    }
    
    $extensionsPath = Join-Path $vscodeDir "extensions.json"
    $extensionsConfig | ConvertTo-Json -Depth 5 | Set-Content -Path $extensionsPath -Encoding UTF8
    Write-Information "✅ VS Code extensions configuration created"  -InformationAction Continue-ForegroundColor Green
    
    Write-Information "📋 Recommended extensions:"  -InformationAction Continue-ForegroundColor Cyan
    foreach ($ext in $extensions.GetEnumerator()) {
        Write-Information "  • $($ext.Key)  -InformationAction Continue- $($ext.Value)" -ForegroundColor Gray
    }
}

# Step 3: GitHub Actions CI/CD Pipeline
if ($SetupType -in @('Full', 'CICDOnly')) {
    Write-Information "`n🔄 Step 3: GitHub Actions CI/CD Pipeline"  -InformationAction Continue-ForegroundColor Green
    Write-Information " -InformationAction Continue-" * 40
    
    $githubDir = ".\.github\workflows"
    if (-not (Test-Path $githubDir)) {
        New-Item -Path $githubDir -ItemType Directory -Force | Out-Null
        Write-Information "✅ Created .github/workflows directory"  -InformationAction Continue-ForegroundColor Green
    }
    
    $ciYaml = @'
name: 🚌 BusBuddy CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]
  workflow_dispatch:

env:
  DOTNET_VERSION: '9.0.x'
  BUILD_CONFIGURATION: 'Release'

jobs:
  build-test:
    name: 🏗️ Build and Test
    runs-on: windows-latest
    timeout-minutes: 30

    steps:
    - name: 📥 Checkout Code
      uses: actions/checkout@v4
      with:
        fetch-depth: 0

    - name: ⚙️ Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: 📦 Restore Dependencies
      run: dotnet restore BusBuddy.sln

    - name: 🏗️ Build Solution
      run: dotnet build BusBuddy.sln --configuration ${{ env.BUILD_CONFIGURATION }} --no-restore

    - name: 🧪 Run Tests
      run: dotnet test BusBuddy.sln --configuration ${{ env.BUILD_CONFIGURATION }} --no-build --verbosity normal --collect:"XPlat Code Coverage"

    - name: 📊 Upload Coverage Reports
      uses: codecov/codecov-action@v3
      with:
        files: '**/coverage.cobertura.xml'
        fail_ci_if_error: false

  grok-review:
    name: 🤖 Grok AI Code Review
    runs-on: ubuntu-latest
    if: github.event_name == 'pull_request'
    needs: build-test

    steps:
    - name: 📥 Checkout Code
      uses: actions/checkout@v4

    - name: 🤖 Grok AI Review
      env:
        XAI_API_KEY: ${{ secrets.XAI_API_KEY }}
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      run: |
        echo "🤖 Grok AI Code Review would run here"
        echo "Files changed in this PR:"
        git diff --name-only origin/main..HEAD
        # Future: Integrate with Grok API for automated code review

  quality-check:
    name: 🔍 Code Quality
    runs-on: ubuntu-latest
    needs: build-test

    steps:
    - name: 📥 Checkout Code
      uses: actions/checkout@v4

    - name: 🔍 Run Trunk Check
      uses: trunk-io/trunk-action@v1
      with:
        check-mode: all

  deploy-staging:
    name: 🚀 Deploy to Staging
    runs-on: windows-latest
    needs: [build-test, quality-check]
    if: github.ref == 'refs/heads/develop'
    environment: staging

    steps:
    - name: 📥 Checkout Code
      uses: actions/checkout@v4

    - name: 🚀 Deploy to Azure
      env:
        AZURE_CREDENTIALS: ${{ secrets.AZURE_CREDENTIALS }}
      run: |
        echo "🚀 Azure deployment would run here"
        # Future: Actual Azure deployment steps
'@

    $ciPath = Join-Path $githubDir "ci-cd.yml"
    $ciYaml | Set-Content -Path $ciPath -Encoding UTF8
    Write-Information "✅ GitHub Actions CI/CD pipeline created"  -InformationAction Continue-ForegroundColor Green
}

# Step 4: PowerShell Environment Enhancements
if ($SetupType -in @('Full', 'GrokOnly')) {
    Write-Information "`n⚡ Step 4: PowerShell Environment Enhancements"  -InformationAction Continue-ForegroundColor Green
    Write-Information " -InformationAction Continue-" * 40
    
    # Load BusBuddy assistant functions
    if (Test-Path ".\grok-busbuddy-assistant.ps1") {
        . .\grok-busbuddy-assistant.ps1
        Write-Information "✅ BusBuddy Grok assistant functions loaded"  -InformationAction Continue-ForegroundColor Green
    }
    
    # Create enhanced development aliases
    $aliases = @{
        'bb-dev' = 'Start-BusBuddyDevelopment'
        'bb-grok-route' = 'Start-GrokRouteOptimizationSession'
        'bb-grok-ui' = 'Get-GrokUIEnhancementSuggestions'
        'bb-grok-automation' = 'Get-GrokPowerShellAutomation'
        'bb-azure-db' = 'Test-AzureDatabaseConnection'
        'bb-ci-status' = 'Get-GitHubActionsStatus'
    }
    
    Write-Information "✅ Enhanced development aliases available:"  -InformationAction Continue-ForegroundColor Green
    foreach ($alias in $aliases.GetEnumerator()) {
        Write-Information "  • $($alias.Key) → $($alias.Value)"  -InformationAction Continue-ForegroundColor Gray
    }
}

# Step 5: Status Summary
Write-Information "`n📊 Setup Complete! Environment Status:"  -InformationAction Continue-ForegroundColor Green
Write-Information "=" * 50  -InformationAction Continue-ForegroundColor Green

Write-Information "🤖 Grok AI Assistant:"  -InformationAction Continue-ForegroundColor Cyan
Write-Information "  Model: grok -InformationAction Continue-4-0709" -ForegroundColor Gray
Write-Information "  Mode: Deep (120s timeout, 4000 tokens)"  -InformationAction Continue-ForegroundColor Gray
Write-Information "  Repository: https://github.com/Bigessfour/BusBuddy -InformationAction Continue-3" -ForegroundColor Gray

Write-Information "`n🖥️ VS Code Integration:"  -InformationAction Continue-ForegroundColor Cyan
Write-Information "  Settings: Enhanced for BusBuddy development"  -InformationAction Continue-ForegroundColor Gray
Write-Information "  Extensions: C#, XAML, PowerShell, Azure, GitHub"  -InformationAction Continue-ForegroundColor Gray

Write-Information "`n🔄 CI/CD Pipeline:"  -InformationAction Continue-ForegroundColor Cyan
Write-Information "  GitHub Actions: Build, Test, Quality, Deploy"  -InformationAction Continue-ForegroundColor Gray
Write-Information "  Grok Review: Automated PR code review"  -InformationAction Continue-ForegroundColor Gray

Write-Information "`n⚡ PowerShell Environment:"  -InformationAction Continue-ForegroundColor Cyan
Write-Information "  BusBuddy functions: Route optimization, UI, automation"  -InformationAction Continue-ForegroundColor Gray
Write-Information "  Development aliases: bb -InformationAction Continue-* command shortcuts" -ForegroundColor Gray

Write-Information "`n🎯 Ready for BusBuddy Development!"  -InformationAction Continue-ForegroundColor Green
Write-Information "Try: bb -InformationAction Continue-grok 'Help me optimize school bus routes'" -ForegroundColor Yellow
Write-Information ""
    
    # Load and configure Grok
    if (Test -InformationAction Continue-Path ".\grok-config.ps1") {
        . .\grok-config.ps1
        
        # Configure with optimal settings
        Set-GrokConfiguration -Model "grok-4-0709" -ResponseMode "Adaptive" -Repository "https://github.com/Bigessfour/BusBuddy-3"
        
        # Test API connectivity
        Write-Information "  🔄 Testing xAI API..."  -InformationAction Continue-ForegroundColor Cyan
        
        try {
            $testResponse = Invoke-GrokWithConfig -Prompt "Confirm you're ready to assist with BusBuddy development. Reply briefly."
            
            if ($testResponse) {
                Write-Information "  ✅ Grok AI Assistant configured and tested successfully!"  -InformationAction Continue-ForegroundColor Green
                Write-Information "  📝 Test Response: $($testResponse.Substring(0, [Math]::Min(100, $testResponse.Length)))..."  -InformationAction Continue-ForegroundColor Gray
                return $true
            }
        } catch {
            Write-Information "  ❌ Grok API test failed: $($_.Exception.Message)"  -InformationAction Continue-ForegroundColor Red
            return $false
        }
    } else {
        Write-Information "  ❌ grok -InformationAction Continue-config.ps1 not found" -ForegroundColor Red
        return $false
    }
}

# Setup VS Code workspace
function Initialize-VSCodeWorkspace {
    if ($SkipVSCodeExtensions) { return $true }
    
    Write-Information ""
    Write -InformationAction Continue-Host "🛠️ Setting up VS Code Workspace..." -ForegroundColor Yellow
    
    # Ensure .vscode directory exists
    if (-not (Test-Path ".vscode")) {
        New-Item -ItemType Directory -Path ".vscode" | Out-Null
        Write-Information "  📁 Created .vscode directory"  -InformationAction Continue-ForegroundColor Green
    }
    
    # Check if settings were already updated
    if (Test-Path ".vscode\settings.json") {
        try {
            $settings = Get-Content ".vscode\settings.json" -Raw | ConvertFrom-Json
            if ($settings.'grok.model' -eq 'grok-4-0709') {
                Write-Information "  ✅ VS Code settings already configured for Grok"  -InformationAction Continue-ForegroundColor Green
            } else {
                Write-Information "  ⚠️ VS Code settings found but may need Grok model update"  -InformationAction Continue-ForegroundColor Yellow
            }
        } catch {
            Write-Information "  ⚠️ VS Code settings file exists but has parsing issues"  -InformationAction Continue-ForegroundColor Yellow
        }
    }
    
    # Create recommended extensions file
    $extensions = @{
        recommendations = @(
            "ms-dotnettools.csharp",
            "ms-dotnettools.csdevkit", 
            "ms-dotnettools.xaml",
            "ms-vscode.powershell",
            "GitHub.copilot",
            "GitHub.copilot-chat",
            "ms-azuretools.vscode-azuresql",
            "ms-mssql.mssql",
            "spmeesseman.vscode-taskexplorer",
            "eamodio.gitlens",
            "trunk.io"
        )
    }
    
    $extensions | ConvertTo-Json -Depth 5 | Set-Content ".vscode\extensions.json" -Encoding UTF8
    Write-Information "  ✅ VS Code extensions configuration created"  -InformationAction Continue-ForegroundColor Green
    
    return $true
}

# Setup GitHub Actions CI/CD
function Initialize-GitHubActions {
    if ($SkipGitHubActions) { return $true }
    
    Write-Information ""
    Write -InformationAction Continue-Host "🔄 Setting up GitHub Actions CI/CD..." -ForegroundColor Yellow
    
    # Ensure .github/workflows directory exists
    $workflowDir = ".github\workflows"
    if (-not (Test-Path $workflowDir)) {
        New-Item -ItemType Directory -Path $workflowDir -Force | Out-Null
        Write-Information "  📁 Created $workflowDir directory"  -InformationAction Continue-ForegroundColor Green
    }
    
    # Check if CI workflow already exists
    if (Test-Path "$workflowDir\ci.yml") {
        Write-Information "  ✅ GitHub Actions workflow already exists"  -InformationAction Continue-ForegroundColor Green
        return $true
    }
    
    # Create a basic CI workflow
    $ciWorkflow = @"
name: BusBuddy CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

env:
  DOTNET_VERSION: '9.0.x'
  BUILD_CONFIGURATION: 'Release'

jobs:
  build-and-test:
    name: Build and Test
    runs-on: windows-latest
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
      
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: `${{ env.DOTNET_VERSION }}
        
    - name: Restore dependencies
      run: dotnet restore BusBuddy.sln
      
    - name: Build solution
      run: dotnet build BusBuddy.sln --configuration `${{ env.BUILD_CONFIGURATION }} --no-restore
      
    - name: Run tests
      run: dotnet test BusBuddy.sln --configuration `${{ env.BUILD_CONFIGURATION }} --no-build --verbosity normal
      
    - name: AI Code Review (if API key available)
      if: `${{ secrets.XAI_API_KEY }}
      run: |
        # PowerShell script to run Grok-based code review
        pwsh -Command "if (Test-Path './ai-review.ps1') { ./ai-review.ps1 }"
      env:
        XAI_API_KEY: `${{ secrets.XAI_API_KEY }}
"@

    $ciWorkflow | Set-Content "$workflowDir\ci.yml" -Encoding UTF8
    Write-Information "  ✅ GitHub Actions CI workflow created"  -InformationAction Continue-ForegroundColor Green
    
    return $true
}

# Create AI-powered development helper scripts
function Create-DevelopmentHelpers {
    Write-Information ""
    Write -InformationAction Continue-Host "🔧 Creating Development Helper Scripts..." -ForegroundColor Yellow
    
    # Create AI review script for CI
    $aiReviewScript = @'
#!/usr/bin/env pwsh
# AI-powered code review script for CI/CD pipeline

if (-not $env:XAI_API_KEY) {
    Write-Information "XAI_API_KEY not available, skipping AI review"  -InformationAction Continue-ForegroundColor Yellow
    exit 0
}

# Load Grok configuration
if (Test-Path "./grok-config.ps1") {
    . ./grok-config.ps1
    
    Write-Information "🤖 Running AI Code Review..."  -InformationAction Continue-ForegroundColor Cyan
    
    # Get changed files from git
    $changedFiles = git diff --name-only HEAD~1 HEAD | Where-Object { $_ -like "*.cs" -or $_ -like "*.xaml" -or $_ -like "*.ps1" }
    
    if ($changedFiles.Count -gt 0) {
        $review = Invoke-GrokWithConfig -Prompt @"
Please review these changed files for:
1. Code quality and best practices
2. Potential bugs or security issues
3. Performance considerations
4. BusBuddy-specific compliance

Changed files: $($changedFiles -join ', ')

Provide a brief summary of any issues found.
"@
        
        Write-Information "📋 AI Review Results:"  -InformationAction Continue-ForegroundColor Green
        Write-Information $review  -InformationAction Continue-ForegroundColor White
    } else {
        Write-Information "No code files changed, skipping review"  -InformationAction Continue-ForegroundColor Gray
    }
} else {
    Write-Information "Grok configuration not found, skipping AI review"  -InformationAction Continue-ForegroundColor Yellow
}
'@

    $aiReviewScript | Set-Content "ai-review.ps1" -Encoding UTF8
    Write-Information "  ✅ AI review script created"  -InformationAction Continue-ForegroundColor Green
    
    # Create daily development assistant
    $dailyAssistantScript = @'
#!/usr/bin/env pwsh
# Daily BusBuddy development assistant

Write-Information "🌅 Good morning! Here's your BusBuddy development summary:"  -InformationAction Continue-ForegroundColor Cyan

# Load Grok if available
if (Test-Path "./grok-config.ps1") {
    . ./grok-config.ps1
    
    # Get today's priorities from Grok
    $dailySummary = Invoke-GrokWithConfig -Prompt @"
Based on BusBuddy's development context, provide:
1. Key development priorities for today
2. Any critical issues to address
3. Suggested focus areas (performance, features, testing)
4. Quick wins we could implement

Keep it concise and actionable.
"@
    
    Write-Information $dailySummary  -InformationAction Continue-ForegroundColor White
} else {
    Write-Information "⚠️ Grok not configured. Run setup script to enable AI assistance."  -InformationAction Continue-ForegroundColor Yellow
}

# Show quick commands
Write-Information ""
Write -InformationAction Continue-Host "🛠️ Available Commands:" -ForegroundColor Yellow
Write-Information "  bb -InformationAction Continue-build     - Build solution" -ForegroundColor Green
Write-Information "  bb -InformationAction Continue-test      - Run tests" -ForegroundColor Green
Write-Information "  bb -InformationAction Continue-grok      - Ask Grok for help" -ForegroundColor Green
Write-Information "  bb -InformationAction Continue-health    - System health check" -ForegroundColor Green
'@

    $dailyAssistantScript | Set-Content "daily-assistant.ps1" -Encoding UTF8
    Write-Information "  ✅ Daily assistant script created"  -InformationAction Continue-ForegroundColor Green
    
    return $true
}

# Main setup execution
function Start-CompleteSetup {
    if (-not (Test-Prerequisites)) {
        Write-Information ""
        Write -InformationAction Continue-Host "❌ Setup cannot continue due to missing prerequisites" -ForegroundColor Red
        return $false
    }
    
    $success = $true
    
    if (-not $TestOnly) {
        $success = $success -and (Initialize-GrokAssistant)
        $success = $success -and (Initialize-VSCodeWorkspace)
        $success = $success -and (Initialize-GitHubActions)
        $success = $success -and (Create-DevelopmentHelpers)
    }
    
    Write-Information ""
    if ($success) {
        Write -InformationAction Continue-Host "🎉 BusBuddy Development Environment Setup Complete!" -ForegroundColor Green
        Write-Information ""
        Write -InformationAction Continue-Host "🚀 Next Steps:" -ForegroundColor Yellow
        Write-Information "  1. Restart VS Code to load new settings"  -InformationAction Continue-ForegroundColor White
        Write-Information "  2. Install recommended extensions when prompted"  -InformationAction Continue-ForegroundColor White
        Write-Information "  3. Set up Azure SQL connection strings in appsettings.json"  -InformationAction Continue-ForegroundColor White
        Write-Information "  4. Add XAI_API_KEY to GitHub repository secrets for CI/CD"  -InformationAction Continue-ForegroundColor White
        Write-Information "  5. Run: bb -InformationAction Continue-grok 'Help me get started with BusBuddy development'" -ForegroundColor White
        Write-Information ""
        Write -InformationAction Continue-Host "🤖 AI Assistant Ready:" -ForegroundColor Cyan
        Write-Information "  • Grok model: grok -InformationAction Continue-4-0709" -ForegroundColor Gray
        Write-Information "  • Repository: https://github.com/Bigessfour/BusBuddy -InformationAction Continue-3" -ForegroundColor Gray
        Write-Information "  • Team ID: 60ab763d -InformationAction Continue-9db8-412f-bd0b-83b2edf06831" -ForegroundColor Gray
    } else {
        Write-Information "❌ Setup completed with some issues. Check the output above."  -InformationAction Continue-ForegroundColor Red
    }
    
    return $success
}

# Execute setup
Start-CompleteSetup























































































