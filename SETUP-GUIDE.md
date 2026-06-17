# 🚀 BusBuddy Setup Guide

**Complete installation and configuration guide for BusBuddy development environment**

## 📋 **Prerequisites**

### **System Requirements**

- **Operating System**: Windows 10/11 (for WPF development)
- **RAM**: 8GB minimum, 16GB recommended
- **Storage**: 2GB free space for development environment
- **Internet**: Required for package downloads and API services (GEE, xAI, NuGet)

### **Required Software**

1. **.NET 9.0 SDK** (9.0.303 or later)
    - Download: https://dotnet.microsoft.com/download/dotnet/9.0
    - Verify: `dotnet --version`

2. **PowerShell 7.5.2+**
    - Download: https://github.com/PowerShell/PowerShell/releases
    - Verify: `$PSVersionTable.PSVersion`

3. **Git** (latest version)
    - Download: https://git-scm.com/download/win
    - Verify: `git --version`

### **Recommended IDE**

- **Visual Studio Code** (preferred) with extensions:
    - C# Dev Kit (`ms-dotnettools.csdevkit`)
    - XAML (`ms-dotnettools.xaml`)
    - PowerShell (`ms-vscode.powershell`)
    - Task Explorer (`spmeesseman.vscode-taskexplorer`)

- **Alternative**: Visual Studio 2022 Community/Professional

## 🔧 **Installation Steps**

### **Step 1: Clone Repository**

```bash
# Clone the repository
git clone https://github.com/Bigessfour/BusBuddy-3.git
cd BusBuddy

# Verify repository structure
ls
```

### **Step 2: Environment Setup**

```powershell
# Import BusBuddy PowerShell module
Import-Module .\PowerShell\Modules\BusBuddy\BusBuddy.psm1

# Verify commands are available
bbCommands

# Run system health check
bbHealth
```

### **Step 2.1: AI Configuration (Optional)**

Configure xAI Grok-4 integration for route optimization and intelligent analysis:

```powershell
# Set xAI API key (required for AI features)
$env:XAI_API_KEY = "your-xai-api-key-here"
[System.Environment]::SetEnvironmentVariable("XAI_API_KEY", "your-xai-api-key-here", "Machine")

# Load AI modules
Import-Module ".\PowerShell\Modules\grok-config.psm1" -Force
Import-Module ".\PowerShell\Modules\BusBuddy-GrokAssistant.psm1" -Force

# Verify AI configuration
$apiKey = Get-ApiKeySecurely
Write-Host "✓ API Key Length: $($apiKey.Length)" -ForegroundColor Green  # Should be 84

$config = grok-config
Write-Host "✓ Model: $($config.DefaultModel)" -ForegroundColor Green     # Should be "grok-4-0709"

# Test AI connection
Test-GrokConnection -Verbose
# Expected: "✅ Grok API connection successful."
```

**AI Features Available:**
- **Route Optimization**: AI-powered route efficiency analysis
- **Maintenance Predictions**: Predictive vehicle maintenance scheduling  
- **Performance Analytics**: Intelligent performance insights
- **Cost Analysis**: AI-driven cost optimization recommendations

**Skip AI Setup:** If you don't have an xAI API key, BusBuddy will work normally without AI features.

### **Step 3: Build and Verify**

```powershell
# Clean and restore packages
bbClean
bbRestore

# Build solution
bbBuild

# Verify MVP readiness
bbMvpCheck
```

### **Step 4: First Run**

```powershell
# Run the application
bbRun
```

**Expected Result**: BusBuddy WPF application should launch with the main dashboard.

## 🌐 **Database Configuration**

### **Local Development (Default)**

BusBuddy uses LocalDB by default:

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True"
    }
}
```

## 🛠️ **Development Environment**

### **PowerShell Development Setup**

```powershell
# Start complete development session
[DEPRECATED - PS dev method retired in favor of WSL] bbDevSession (legacy)

# This will:
# - Load all PowerShell modules
# - Set up development aliases
# - Configure environment variables
# - Prepare debugging tools
```

### **VS Code Configuration**

The repository includes pre-configured VS Code settings:

- **Tasks**: Pre-defined build, run, and test tasks
- **Launch Configurations**: Ready-to-use debugging setups
- **Extensions**: Recommended extensions list
- **Settings**: Optimized for BusBuddy development

### **Available Commands**

```powershell
# Core Development
bbBuild               # Build solution
bbRun                 # Run application
bbTest                # Execute tests
bbHealth              # System health check
bbClean               # Clean build artifacts

# Quality Assurance
bbXamlValidate        # Validate XAML files
bbAntiRegression      # Run compliance checks
bbMvpCheck            # Verify MVP readiness

# Route Optimization
bbRoutes              # XAI route optimization
bbRouteDemo           # Demo with sample data
bbRouteStatus         # Check optimization status
```

## 🧪 **Testing Setup**

### **Running Tests**

```powershell
# Run all tests
bbTest

# For .NET 9 compatibility issues, use:
# VS Code NUnit Test Runner extension (recommended)
# Visual Studio Test Explorer
```

### **Test Categories**

- **Unit Tests**: Core business logic validation
- **Integration Tests**: Database and service integration
- **XAML Validation**: UI component verification
- **MVP Tests**: Essential functionality verification

## 🔍 **Troubleshooting**

### **Common Issues**

#### **Build Failures**

```powershell
# Clean and rebuild
bbClean
bbRestore
bbBuild

# Check system health
bbHealth
```

#### **.NET 9 Test Platform Issues**

- **Issue**: Microsoft.TestPlatform.CoreUtilities compatibility
- **Solution**: Use VS Code NUnit Test Runner extension
- **Alternative**: Use Visual Studio Test Explorer

#### **PowerShell Module Loading**

```powershell
# Force reload PowerShell module
Import-Module .\PowerShell\Modules\BusBuddy\BusBuddy.psm1 -Force

# Verify commands
bbCommands
```

#### **Database Connection Issues**

```powershell
# For LocalDB issues
sqllocaldb info mssqllocaldb
sqllocaldb start mssqllocaldb

# For Azure SQL issues
az account show  # Verify login
```

## 🎯 **Verification Checklist**

After setup, verify everything works:

- [ ] `bbHealth` returns all green checkmarks
- [ ] `bbBuild` completes successfully (0 errors)
- [ ] `bbMvpCheck` reports "MVP READY! You can ship this!"
- [ ] `bbRun` launches the WPF application
- [ ] Application displays student and route management interfaces

## 🚀 **Next Steps**

1. **Explore the Application**: Use `bbRun` to launch and explore features
2. **Review Documentation**: Check `GROK-README.md` for current development status
3. **Start Development**: Use `[DEPRECATED - PS dev method retired in favor of WSL] bbDevSession (legacy)` for full development environment
4. **Run Tests**: Use `bbTest` to verify functionality
5. **Check Commands**: Use `bbCommands` to see all available automation

## 📚 **Additional Resources**

- **Main Documentation**: `README.md`
- **Development Status**: `GROK-README.md`
- **File Reference**: `Documentation/FILE-FETCHABILITY-GUIDE.md`
- **Command Reference**: `COMMAND-REFERENCE.md`
- **API Documentation**: `Documentation/API-REFERENCE.md`

## 🆘 **Getting Help**

- **Health Check**: Run `bbHealth` for diagnostic information
- **Command Help**: Run `bbCommands` for available commands
- **Issues**: Check GitHub Issues for known problems
- **Documentation**: All guides are in the `Documentation/` folder
