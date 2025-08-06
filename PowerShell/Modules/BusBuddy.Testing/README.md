# 🧪 BusBuddy.Testing PowerShell Module

## Overview

The BusBuddy.Testing module provides Microsoft PowerShell compliant testing automation for Phase 4 of the BusBuddy School Transportation Management System. This module replaces the monolithic script approach with a properly structured, standards-compliant PowerShell module.

## 🎯 Features

- **Microsoft PowerShell Standards Compliant**: Follows official PowerShell development guidelines
- **NUnit Test Automation**: Integrates with VS Code NUnit Test Runner extension
- **Category-Based Testing**: Supports Unit, Integration, Validation, Core, and WPF test suites
- **Comprehensive Reporting**: Generates detailed markdown reports with test results
- **Watch Mode**: Continuous testing with file system monitoring
- **Error Handling**: Robust exception management with actionable feedback
- **VS Code Integration**: Seamless task runner and extension integration

## 📦 Module Structure

```
PowerShell/Modules/BusBuddy.Testing/
├── BusBuddy.Testing.psd1           # Module manifest
├── BusBuddy.Testing.psm1           # Module implementation
├── Initialize-BusBuddyTesting.ps1  # Module initialization script
├── Profile-Integration.ps1         # PowerShell profile integration
└── README.md                       # This documentation
```

## 🚀 Quick Start

### 1. Load the Module

```powershell
# Navigate to BusBuddy workspace
cd "C:\Path\To\BusBuddy"

# Initialize the testing module
.\PowerShell\Modules\BusBuddy.Testing\Initialize-BusBuddyTesting.ps1
```

### 2. Run Tests

```powershell
# Run all tests with report generation
Invoke-BusBuddyTests -TestSuite All -GenerateReport

# Run specific test suite
bb-test Unit

# Start continuous testing
bb-test-watch
```

### 3. VS Code Integration

Use the Task Explorer or Command Palette:
- **🧪 BB: Phase 4 Modular Tests** - Run all tests with modular approach
- **🔄 BB: Phase 4 Test Watch** - Start continuous testing

## 📋 Available Functions

### Core Functions

| Function | Description | Alias |
|----------|-------------|-------|
| `Invoke-BusBuddyTests` | Execute NUnit tests with filtering and reporting | `bb-test` |
| `Start-BusBuddyTestWatch` | Start continuous test monitoring | `bb-test-watch` |
| `Export-BusBuddyTestReport` | Generate comprehensive test reports | `bb-test-report` |
| `Test-BusBuddyNUnit` | Validate testing infrastructure | - |

### Profile Functions

| Function | Description | Alias |
|----------|-------------|-------|
| `Get-BusBuddyInfo` | Display BusBuddy environment information | `bb-info` |
| `Start-BusBuddyDev` | Navigate to BusBuddy workspace | `bb-dev`, `bb-cd` |
| `Initialize-BusBuddyTesting` | Load testing environment | - |

## 🎛️ Function Parameters

### Invoke-BusBuddyTests

```powershell
Invoke-BusBuddyTests 
    [-TestSuite] <String>           # All, Unit, Integration, Validation, Core, WPF
    [-WorkspacePath] <String>       # Path to BusBuddy workspace (auto-detected)
    [-GenerateReport]               # Generate markdown test report
    [-OutputPath] <String>          # Custom output directory
```

### Start-BusBuddyTestWatch

```powershell
Start-BusBuddyTestWatch
    [-TestSuite] <String>           # Test suite to monitor (default: Unit)
    [-WorkspacePath] <String>       # Workspace path for monitoring
```

## 📊 Test Categories

The module supports category-based test filtering:

| Category | Filter | Description |
|----------|--------|-------------|
| **All** | *(no filter)* | Execute all available tests |
| **Unit** | `Category=UnitTest` | Isolated component testing |
| **Integration** | `Category=IntegrationTest` | Cross-component validation |
| **Validation** | `Category=ValidationTest` | Phase compliance checks |
| **Core** | `Category=CoreTest` | Business logic validation |
| **WPF** | `Category=UITest` | User interface testing |

## 📄 Report Generation

The module generates comprehensive markdown reports including:

- **Test Results Summary**: Pass/fail statistics with percentages
- **Failed Test Details**: Specific error messages and stack traces
- **Phase 4 Compliance Status**: Infrastructure validation results
- **Execution Timing**: Performance metrics and duration analysis

### Example Report Structure

```markdown
# 🧪 BusBuddy Test Report - All

**Generated**: 2025-08-02 14:30:45
**Test Suite**: All
**Project**: BusBuddy School Transportation Management System

## 📊 Test Results Summary

| Metric | Count | Percentage |
|--------|-------|------------|
| **Total Tests** | 25 | 100% |
| **Passed** | 23 | 92% |
| **Failed** | 2 | 8% |
| **Skipped** | 0 | 0% |
```

## 🔧 Configuration

### VS Code Settings

Ensure your `.vscode/settings.json` includes:

```json
{
  "nunit.searchTasks": true,
  "nunit.fsWatcher": true,
  "nunit.testRunner": "dotnet",
  "terminal.integrated.defaultProfile.windows": "PowerShell 7.5.2"
}
```

### PowerShell Profile Integration

Add to your PowerShell profile:

```powershell
# Load BusBuddy testing environment
if (Test-Path "C:\Path\To\BusBuddy\PowerShell\Modules\BusBuddy.Testing\Profile-Integration.ps1") {
    . "C:\Path\To\BusBuddy\PowerShell\Modules\BusBuddy.Testing\Profile-Integration.ps1"
}
```

## 🛠️ Troubleshooting

### Common Issues

1. **Module Not Found**
   ```powershell
   # Ensure PSModulePath includes BusBuddy modules
   $env:PSModulePath += ";C:\Path\To\BusBuddy\PowerShell\Modules"
   ```

2. **VS Code Extension Missing**
   - Install "NUnit Test Runner" extension
   - Restart VS Code after installation

3. **Test Project Build Failures**
   ```powershell
   # Clean and restore packages
   dotnet clean BusBuddy.sln
   dotnet restore BusBuddy.sln --force
   dotnet build BusBuddy.sln
   ```

4. **Permission Issues**
   ```powershell
   # Set execution policy
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```

### Validation Commands

```powershell
# Validate testing infrastructure
Test-BusBuddyNUnit

# Check module loading
Get-Module BusBuddy.Testing

# Verify workspace structure
Get-BusBuddyInfo
```

## 📚 Standards Compliance

This module follows:

- **Microsoft PowerShell Development Guidelines**
- **PowerShell Module Best Practices**
- **BusBuddy Project Coding Standards**
- **Phase 4 Testing Requirements**

### Key Compliance Features

- ✅ Proper parameter validation with `ValidateSet` attributes
- ✅ Comprehensive error handling with structured exceptions
- ✅ Output streams (`Write-Information`, `Write-Warning`, `Write-Error`)
- ✅ Module manifest with complete metadata
- ✅ Export declarations for functions and aliases
- ✅ Help documentation with examples
- ✅ Strict mode and error action preferences

## 🔄 Migration from Monolithic Script

### Legacy Script Compatibility

The original `Run-Phase4-NUnitTests.ps1` is replaced by:
- `Run-Phase4-NUnitTests-Modular.ps1` - Compatibility bridge
- `BusBuddy.Testing` module - New modular implementation

### Migration Steps

1. **Update VS Code tasks** to use modular scripts
2. **Import BusBuddy.Testing module** in PowerShell sessions
3. **Use new function names** or aliases for testing
4. **Leverage improved error handling** and reporting

## 🎯 Phase 4 Objectives - Completed

- ✅ **Microsoft PowerShell Standards Compliance**
- ✅ **Modular Architecture Implementation**
- ✅ **NUnit Test Runner Integration**
- ✅ **Comprehensive Error Handling**
- ✅ **VS Code Task Integration**
- ✅ **Continuous Testing Support**
- ✅ **Advanced Reporting System**

## 🚀 Next Steps

### Phase 5 Preparation

- **Performance Testing Integration**: Benchmark test execution
- **Advanced Test Patterns**: Sophisticated testing scenarios
- **CI/CD Integration**: GitHub Actions integration
- **Coverage Analysis**: Enhanced code coverage reporting

### Continuous Improvement

- **Documentation Updates**: Keep help content current
- **Feature Enhancements**: Based on developer feedback
- **Standards Validation**: Regular compliance checks
- **Performance Optimization**: Module loading and execution speed

---

## 📞 Support

For issues or questions:

1. **Run validation**: `Test-BusBuddyNUnit`
2. **Check documentation**: This README and function help
3. **Review standards**: PowerShell development guidelines
4. **Test infrastructure**: VS Code extension and .NET SDK

---

*BusBuddy.Testing PowerShell Module*  
*Phase 4 Testing and Validation Infrastructure*  
*Microsoft PowerShell Standards Compliant*
