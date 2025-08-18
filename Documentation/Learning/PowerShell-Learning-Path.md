# ⚡ PowerShell Learning Path - From Zero to BusBuddy Hero

Master PowerShell for BusBuddy development with this comprehensive, hands-on learning path. Perfect for beginners and those wanting to level up their automation skills!

---

## 🎯 **Learning Objectives**

By the end of this path, you'll be able to:

- ✅ Use PowerShell confidently for BusBuddy development
- ✅ Automate build, test, and deployment processes
- ✅ Create and customize PowerShell functions
- ✅ Integrate PowerShell with .NET and Azure
- ✅ Troubleshoot and debug PowerShell scripts

---

## 📚 **Learning Path Overview**

### **📈 Skill Progression**

```
Beginner (Week 1-2)     Intermediate (Week 3-4)     Advanced (Week 5-6)
      ↓                         ↓                         ↓
• Basic commands        • Functions & modules     • Advanced automation
• Variables & objects   • Error handling          • Azure integration
• Pipeline basics       • Script organization     • Custom modules
• File operations       • .NET integration        • Enterprise patterns
```

---

## 🌱 **LEVEL 1: PowerShell Basics (Week 1-2)**

### **Day 1-2: PowerShell Fundamentals**

#### **🎯 Goal**: Get comfortable with the PowerShell environment

#### **Essential Commands to Master**

```powershell
# Help system - your best friend!
Get-Help                    # General help
Get-Help Get-Process        # Help for specific command
Get-Help Get-Process -Examples  # See examples

# Discovery commands
Get-Command                 # List all commands
Get-Command *process*       # Find commands containing "process"
Get-Member                  # Show object properties and methods

# Navigation
Get-Location               # Where am I?
Set-Location C:\BusBuddy   # Go somewhere
Get-ChildItem              # List files (like "ls" or "dir")
```

#### **🔬 Hands-On Exercise**

```powershell
# Try these commands in your BusBuddy directory
cd C:\Users\[YourName]\Desktop\BusBuddy\BusBuddy

# Explore the project structure
Get-ChildItem              # See all files and folders
Get-ChildItem -Recurse *.ps1   # Find all PowerShell scripts
Get-ChildItem *.cs | Measure-Object  # Count C# files

# Get help on commands you just used
Get-Help Get-ChildItem -Examples
```

### **Day 3-4: Variables and Data Types**

#### **🎯 Goal**: Understand how PowerShell handles data

#### **Variables and Assignment**

```powershell
# Simple variables
$projectName = "BusBuddy"
$version = "1.0"
$isReady = $true

# Complex data types
$buildDate = Get-Date
$files = Get-ChildItem *.cs
$projectInfo = @{
    Name = "BusBuddy"
    Version = "1.0"
    Language = "C#"
}

# Arrays
$technologies = @("PowerShell", "WPF", "Entity Framework", "Azure")
$technologies += "Syncfusion"  # Add to array
```

#### **🔬 Hands-On Exercise**

```powershell
# Create a BusBuddy project summary
$summary = @{
    ProjectName = "BusBuddy"
    StartDate = Get-Date "2025-01-01"
    Technologies = @("PowerShell", "WPF", "C#", "Entity Framework")
    FileCount = (Get-ChildItem -Recurse *.cs).Count
    Status = "In Development"
}

# Display your summary
$summary

# Access specific properties
Write-Host "Project: $($summary.ProjectName)"
Write-Host "Files: $($summary.FileCount)"
```

### **Day 5-7: The Pipeline (PowerShell's Superpower)**

#### **🎯 Goal**: Master the pipeline for data processing

#### **Pipeline Basics**

```powershell
# Basic pipeline operations
Get-ChildItem                     # Get files
Get-ChildItem | Where-Object {$_.Extension -eq ".cs"}  # Filter C# files
Get-ChildItem *.cs | Sort-Object Name                  # Sort by name
Get-ChildItem *.cs | Select-Object Name, Length        # Select properties

# The $_ automatic variable (current pipeline object)
Get-ChildItem *.cs | Where-Object {$_.Length -gt 1KB}
Get-ChildItem *.cs | ForEach-Object {$_.Name.ToUpper()}
```

#### **🔬 BusBuddy-Specific Exercise**

```powershell
# Analyze BusBuddy source code
Get-ChildItem -Recurse *.cs |
    Where-Object {$_.Directory.Name -eq "Services"} |
    Sort-Object Length -Descending |
    Select-Object Name, Length, Directory |
    Format-Table

# Find large files that might need optimization
Get-ChildItem -Recurse *.cs |
    Where-Object {$_.Length -gt 10KB} |
    Select-Object Name, @{Name="Size(KB)"; Expression={[math]::Round($_.Length/1KB, 2)}}
```

---

## 🚀 **LEVEL 2: Intermediate PowerShell (Week 3-4)**

### **Day 1-3: Functions and Modules**

#### **🎯 Goal**: Write reusable PowerShell code

#### **Creating Functions**

```powershell
# Simple function
function Get-BusBuddyStatus {
    if (Test-Path "BusBuddy.sln") {
        Write-Host "BusBuddy project found!" -ForegroundColor Green
    } else {
        Write-Host "BusBuddy project not found!" -ForegroundColor Red
    }
}

# Function with parameters
function Build-BusBuddyProject {
    param(
        [string]$Configuration = "Debug",
        [switch]$Clean
    )

    if ($Clean) {
        Write-Host "Cleaning project..."
        dotnet clean
    }

    Write-Host "Building project in $Configuration mode..."
    dotnet build --configuration $Configuration
}

# Advanced function with validation
function Deploy-BusBuddy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet("Development", "Staging", "Production")]
        [string]$Environment,

        [Parameter()]
        [string]$Version = "1.0.0"
    )

    Write-Host "Deploying BusBuddy v$Version to $Environment" -ForegroundColor Cyan
    # Deployment logic here
}
```

#### **🔬 Module Creation Exercise**

Create your own BusBuddy helper module:

```powershell
# Create MyBusBuddyHelpers.psm1
function Get-ProjectSummary {
    $csFiles = Get-ChildItem -Recurse *.cs
    $xamlFiles = Get-ChildItem -Recurse *.xaml

    [PSCustomObject]@{
        CSharpFiles = $csFiles.Count
        XAMLFiles = $xamlFiles.Count
        TotalSize = ($csFiles | Measure-Object Length -Sum).Sum
        LastModified = ($csFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
    }
}

function Find-TODOComments {
    Get-ChildItem -Recurse *.cs |
        Select-String "TODO|FIXME|HACK" |
        ForEach-Object {
            [PSCustomObject]@{
                File = $_.Filename
                Line = $_.LineNumber
                Comment = $_.Line.Trim()
            }
        }
}

# Export functions
Export-ModuleMember -Function Get-ProjectSummary, Find-TODOComments
```

### **Day 4-5: Error Handling and Debugging**

#### **🎯 Goal**: Write robust PowerShell scripts

#### **Error Handling Techniques**

```powershell
# Try-Catch for exception handling
function Safe-Build {
    try {
        Write-Host "Starting build..." -ForegroundColor Yellow
        dotnet build BusBuddy.sln

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Build successful!" -ForegroundColor Green
        } else {
            Write-Warning "Build completed with warnings"
        }
    }
    catch {
        Write-Error "Build failed: $($_.Exception.Message)"
        return $false
    }
    return $true
}

# Error handling with detailed information
function Robust-FileOperation {
    param([string]$FilePath)

    try {
        if (-not (Test-Path $FilePath)) {
            throw "File not found: $FilePath"
        }

        $content = Get-Content $FilePath
        return $content
    }
    catch [System.UnauthorizedAccessException] {
        Write-Error "Access denied to file: $FilePath"
    }
    catch [System.IO.FileNotFoundException] {
        Write-Error "File not found: $FilePath"
    }
    catch {
        Write-Error "Unexpected error: $($_.Exception.Message)"
    }
}
```

#### **🔬 Debugging Exercise**

```powershell
# Create a function with intentional bugs, then fix them
function Debug-Exercise {
    param([string]$ProjectPath)

    # Bug 1: No parameter validation
    # Bug 2: No error handling
    # Bug 3: Hardcoded paths

    $files = Get-ChildItem $ProjectPath -Recurse *.cs
    $totalLines = 0

    foreach ($file in $files) {
        $lines = Get-Content $file.FullName
        $totalLines += $lines.Count
    }

    Write-Host "Total lines of code: $totalLines"
}

# Fixed version
function Get-CodeLineCount {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({Test-Path $_})]
        [string]$ProjectPath
    )

    try {
        $files = Get-ChildItem $ProjectPath -Recurse -Filter "*.cs" -ErrorAction Stop
        $totalLines = 0

        foreach ($file in $files) {
            try {
                $content = Get-Content $file.FullName -ErrorAction Stop
                $totalLines += $content.Count
            }
            catch {
                Write-Warning "Could not read file: $($file.Name)"
            }
        }

        Write-Host "Total lines of C# code: $totalLines" -ForegroundColor Green
        return $totalLines
    }
    catch {
        Write-Error "Error analyzing project: $($_.Exception.Message)"
    }
}
```

### **Day 6-7: Working with .NET and External Tools**

#### **🎯 Goal**: Integrate PowerShell with .NET and external tools

#### **.NET Integration**

```powershell
# Using .NET classes directly (preferred approach)
$date = [System.DateTime]::Now
$guid = [System.Guid]::NewGuid()
$path = [System.IO.Path]::Combine("C:", "BusBuddy", "output.txt")

# Working with .NET collections
$list = New-Object System.Collections.Generic.List[string]
$list.Add("PowerShell")
$list.Add("WPF")
$list.Add("Entity Framework")

# JSON operations (crucial for configuration)
$config = @{
    Environment = "Development"
    Database = "LocalDB"
    LogLevel = "Debug"
}
$json = $config | ConvertTo-Json
$json | Out-File "config.json"

# Reading JSON back
$loadedConfig = Get-Content "config.json" | ConvertFrom-Json
```

#### **🔬 BusBuddy Integration Exercise**

```powershell
# Create a BusBuddy configuration manager
function Get-BusBuddyConfig {
    param([string]$ConfigFile = "appsettings.json")

    if (-not (Test-Path $ConfigFile)) {
        throw "Configuration file not found: $ConfigFile"
    }

    try {
        $config = Get-Content $ConfigFile | ConvertFrom-Json
        return $config
    }
    catch {
        throw "Invalid JSON in configuration file: $($_.Exception.Message)"
    }
}

function Set-BusBuddyEnvironment {
    param(
        [ValidateSet("Development", "Staging", "Production")]
        [string]$Environment
    )

    $config = Get-BusBuddyConfig
    $config.Environment = $Environment
    $config | ConvertTo-Json -Depth 10 | Set-Content "appsettings.json"

    Write-Host "Environment set to: $Environment" -ForegroundColor Green
}
```

---

## 🏆 **LEVEL 3: Advanced PowerShell (Week 5-6)**

### **Day 1-3: PowerShell 7.5 Features and Performance**

#### **🎯 Goal**: Leverage cutting-edge PowerShell features

#### **PowerShell 7.5 Enhancements**

```powershell
# Enhanced array operations (98% faster!)
$buildErrors = @()
foreach ($project in $projects) {
    $buildErrors += Get-BuildErrors $project  # Much faster in PS 7.5!
}

# New ConvertTo-CliXml and ConvertFrom-CliXml
function Export-BusBuddyReport {
    param($BuildResults)

    $BuildResults | ConvertTo-CliXml | Out-File "build-report.xml"
    Write-Host "Build report exported" -ForegroundColor Green
}

# Enhanced JSON handling with comments
function Test-BusBuddyConfig {
    param([string]$ConfigPath)

    # PS 7.5 allows comments in JSON validation
    $isValid = Test-Json -Path $ConfigPath -IgnoreComments -AllowTrailingCommas

    if ($isValid) {
        $config = Get-Content $ConfigPath | ConvertFrom-Json -DateKind Local
        return $config
    }
    return $null
}
```

#### **🔬 Performance Optimization Exercise**

```powershell
# Compare PowerShell 7.5 vs older versions
function Measure-ArrayPerformance {
    $iterations = 10000

    # Old way (slower)
    $oldWay = Measure-Command {
        $array = @()
        for ($i = 0; $i -lt $iterations; $i++) {
            $array += "Item $i"
        }
    }

    # Better way (faster)
    $betterWay = Measure-Command {
        $list = [System.Collections.Generic.List[string]]::new()
        for ($i = 0; $i -lt $iterations; $i++) {
            $list.Add("Item $i")
        }
        $array = $list.ToArray()
    }

    Write-Host "Old way: $($oldWay.TotalMilliseconds)ms"
    Write-Host "Better way: $($betterWay.TotalMilliseconds)ms"
    Write-Host "Improvement: $([math]::Round(($oldWay.TotalMilliseconds / $betterWay.TotalMilliseconds), 2))x faster"
}
```

### **Day 4-5: Azure and Cloud Integration**

#### **🎯 Goal**: Automate cloud operations with PowerShell

#### **Azure PowerShell Basics**

```powershell
# Install Azure modules (run once)
Install-Module Az -Force -AllowClobber

# Connect to Azure
Connect-AzAccount

# BusBuddy Azure operations
function Deploy-BusBuddyToAzure {
    param(
        [string]$ResourceGroupName = "BusBuddy-RG",
        [string]$AppServiceName = "BusBuddy-App",
        [string]$Environment = "Staging"
    )

    try {
        # Check if resource group exists
        $rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
        if (-not $rg) {
            Write-Host "Creating resource group: $ResourceGroupName"
            New-AzResourceGroup -Name $ResourceGroupName -Location "East US"
        }

        # Deploy app service
        Write-Host "Deploying to App Service: $AppServiceName"
        # Deployment logic here

        Write-Host "Deployment completed successfully!" -ForegroundColor Green
    }
    catch {
        Write-Error "Deployment failed: $($_.Exception.Message)"
    }
}

# Configuration management
function Update-BusBuddyAzureConfig {
    param(
        [string]$AppServiceName,
        [hashtable]$AppSettings
    )

    foreach ($setting in $AppSettings.GetEnumerator()) {
        Set-AzWebApp -ResourceGroupName "BusBuddy-RG" -Name $AppServiceName -AppSettings @{$setting.Key = $setting.Value}
    }

    Write-Host "Configuration updated successfully!" -ForegroundColor Green
}
```

### **Day 6-7: Creating Production-Ready Modules**

#### **🎯 Goal**: Build enterprise-grade PowerShell modules

#### **Advanced Module Structure**

```powershell
# BusBuddyDevOps.psm1 - Production module example

#region Module Configuration
$ModuleRoot = $PSScriptRoot
$PrivateFunctions = Get-ChildItem -Path "$ModuleRoot\Private" -Filter "*.ps1" -Recurse
$PublicFunctions = Get-ChildItem -Path "$ModuleRoot\Public" -Filter "*.ps1" -Recurse

# Import all functions
@($PrivateFunctions + $PublicFunctions) | ForEach-Object {
    try {
        . $_.FullName
    }
    catch {
        Write-Error "Failed to import function $($_.Name): $($_.Exception.Message)"
    }
}
#endregion

#region Public Functions
function Start-BusBuddyDeployment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet("Development", "Staging", "Production")]
        [string]$Environment,

        [Parameter(Mandatory)]
        [string]$Version,

        [switch]$WhatIf
    )

    begin {
        Write-Verbose "Starting BusBuddy deployment to $Environment"
        Initialize-DeploymentEnvironment
    }

    process {
        try {
            if ($WhatIf) {
                Write-Host "WHAT IF: Would deploy version $Version to $Environment"
                return
            }

            # Pre-deployment validation
            Test-DeploymentReadiness -Environment $Environment

            # Build and package
            Invoke-BuildProcess -Configuration $Environment

            # Deploy
            Invoke-DeploymentProcess -Environment $Environment -Version $Version

            # Post-deployment validation
            Test-DeploymentHealth -Environment $Environment

            Write-Host "Deployment completed successfully!" -ForegroundColor Green
        }
        catch {
            Write-Error "Deployment failed: $($_.Exception.Message)"
            throw
        }
    }

    end {
        Write-Verbose "Deployment process completed"
    }
}
#endregion

# Export only public functions
Export-ModuleMember -Function Start-BusBuddyDeployment, Get-BusBuddyStatus, Test-BusBuddyHealth
```

---

## 🎯 **BusBuddy-Specific PowerShell Mastery**

### **Understanding Our Module Structure**

```powershell
# Explore the BusBuddy PowerShell module
Import-Module .\PowerShell\BusBuddy.psm1 -Force

# Core functions you should master
bb-build          # Build automation
bb-run            # Application launching
bb-test           # Testing automation
bb-health         # Environment validation
bb-dev-session    # Complete development session

# Advanced functions
bb-mentor         # Learning assistance
bb-docs           # Documentation search
bb-ref            # Quick reference
```

### **Customizing Your Development Environment**

```powershell
# Create your personal BusBuddy profile
function Initialize-MyBusBuddyEnvironment {
    # Set up your preferred directories
    Set-Location "C:\Projects\BusBuddy"

    # Load modules
    Import-Module .\PowerShell\BusBuddy.psm1

    # Set environment variables
    $env:BUSBUDDY_ENV = "Development"
    $env:BUSBUDDY_LOG_LEVEL = "Debug"

    # Display welcome message
    Write-Host "🚌 Welcome back to BusBuddy development!" -ForegroundColor Cyan
    bb-health
}

# Add to your PowerShell profile
Add-Content $PROFILE "Initialize-MyBusBuddyEnvironment"
```

---

## 📊 **Assessment and Practice**

### **Weekly Challenges**

#### **Week 1 Challenge: Basic Automation**

Create a PowerShell script that:

1. Counts all C# and XAML files in BusBuddy
2. Finds the largest file
3. Creates a summary report
4. Saves results to a text file

#### **Week 2 Challenge: Build Automation**

Create a function that:

1. Checks if the solution builds successfully
2. Runs any available tests
3. Generates a build report with timestamps
4. Handles errors gracefully

#### **Week 3 Challenge: Configuration Management**

Build a configuration manager that:

1. Reads appsettings.json
2. Validates required settings
3. Can switch between environments
4. Backs up configurations before changes

#### **Week 4 Challenge: Advanced Module**

Create a personal productivity module with:

1. Custom functions for your workflow
2. Proper error handling
3. Help documentation
4. Export configuration

---

## 🏆 **Graduation Project: BusBuddy DevOps Assistant**

### **Final Challenge**: Create a comprehensive DevOps assistant with these features:

```powershell
# Your graduation project should include:

function Start-BusBuddyWorkday {
    # Check environment health
    # Pull latest changes
    # Build and test
    # Show current branch status
    # Display today's tasks/issues
}

function Deploy-BusBuddyFeature {
    param([string]$FeatureBranch, [string]$TargetEnvironment)
    # Validate branch
    # Run tests
    # Deploy to environment
    # Verify deployment
    # Notify team
}

function Get-BusBuddyMetrics {
    # Code coverage
    # Build times
    # Test results
    # Performance metrics
}
```

---

## 📚 **Additional Resources**

### **Official Documentation**

- **[PowerShell Documentation](https://learn.microsoft.com/en-us/powershell/)** — Complete reference
- **[PowerShell Gallery](https://www.powershellgallery.com/)** — Module repository
- **[PowerShell 7.5 Features](../Documentation/POWERSHELL-7.5-FEATURES.md)** — Latest enhancements

### **BusBuddy-Specific Resources**

- **[PowerShell Module API](../API/PowerShell-Module-API.md)** — Our function reference
- **[System Architecture](../Architecture/System-Architecture.md)** — Understanding the big picture
- **[Coding Standards](../Standards/MASTER-STANDARDS.md)** — How we write code

### **Practice Resources**

- **[PowerShell Challenges](https://github.com/PowerShell/PowerShell/tree/master/docs/learning-powershell)** — External practice
- **[Azure PowerShell Labs](https://github.com/Azure/azure-powershell)** — Cloud automation practice

---

## 🎉 **Completion Certificate**

### **You've Mastered PowerShell When You Can:**

- ✅ Write functions with proper parameter validation
- ✅ Handle errors gracefully and provide meaningful messages
- ✅ Use the pipeline effectively for data processing
- ✅ Create and distribute PowerShell modules
- ✅ Integrate PowerShell with .NET and external tools
- ✅ Automate BusBuddy build, test, and deployment processes
- ✅ Leverage PowerShell 7.5 performance improvements
- ✅ Contribute to our PowerShell automation infrastructure

### **Next Steps After Mastery**

1. **Contribute to BusBuddy Automation**: Help improve our PowerShell modules
2. **Teach Others**: Share your knowledge with newcomers
3. **Explore Advanced Topics**: PowerShell DSC, Classes, Cloud integration
4. **Join the Community**: Contribute to open-source PowerShell projects

---

**🎯 Remember**: PowerShell mastery is a journey, not a destination. Keep practicing, keep learning, and most importantly, keep automating! 🚀\*\*

---

_Need help during your learning journey? Use `bb-mentor PowerShell` for guidance, or check our [AI Mentor System](AI-Mentor-System.md) for interactive learning!_
