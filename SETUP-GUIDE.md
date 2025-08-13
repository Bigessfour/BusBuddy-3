# 🚀 BusBuddy Setup Guide

**Complete installation and configuration guide for BusBuddy development environment**

## 📋 **Prerequisites**

### **System Requirements**
- **Operating System**: Windows 10/11 (for WPF development)
- **RAM**: 8GB minimum, 16GB recommended
- **Storage**: 2GB free space for development environment
- **Internet**: Required for package downloads and Azure services

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

### **Azure SQL Database (Production)**
For Azure SQL Database connection:
1. Update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "BusBuddyDb": "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Authentication=Active Directory Default;Encrypt=True;"
  }
}
```

2. Ensure Azure CLI is logged in:
```bash
az login
```

3. Run database migrations:
```powershell
dotnet ef database update --project BusBuddy.Core
```

## 🛠️ **Development Environment**

### **PowerShell Development Setup**
```powershell
# Start complete development session
bbDevSession

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

#### Unified Scheduler Tests
To run only the Unified Scheduler tests (for the merged Sports + Activities scheduler):

```powershell
# If your bbTest supports filters
bb-test -Filter "TestCategory=Scheduler"

# Or use the .NET CLI filter directly
dotnet test "BusBuddy.Tests/BusBuddy.Tests.csproj" -v m --filter TestCategory=Scheduler
```

Tests are located under `BusBuddy.Tests/SchedulerTests/` and are self-contained (use EF Core InMemory/mocks).

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
3. **Start Development**: Use `bbDevSession` for full development environment
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
