# VS Code Debug Configuration Enhancements

## Overview

This document outlines the improvements made to the VS Code launch configurations based on the analysis and suggestions for enhanced debugging capabilities, PowerShell integration, and test coverage support.

## Enhanced Launch Configurations

### ✅ **Post-Debug Cleanup Integration**
All primary debug configurations now include `"postDebugTask": "Clean Solution"` for automatic cleanup after debugging sessions, preventing build artifacts from accumulating.

### ✅ **New PowerShell Debugging Configuration**
```json
{
    "name": "Debug PowerShell Workflows",
    "type": "PowerShell",
    "request": "launch",
    "script": "${workspaceFolder}/.vscode/BusBuddy-Advanced-Workflows.ps1",
    "args": [],
    "cwd": "${workspaceFolder}",
    "createTemporaryIntegratedConsole": false
}
```

**Benefits:**
- Direct debugging of PowerShell workflows
- Integration with existing BusBuddy-Advanced-Workflows.ps1
- Seamless workflow troubleshooting

### ✅ **Enhanced Test Coverage Configuration**
```json
{
    "name": "Debug Tests with Coverage",
    "type": "coreclr",
    "request": "launch",
    "preLaunchTask": "Build Solution",
    "postDebugTask": "Clean Solution",
    "program": "dotnet",
    "args": [
        "test",
        "${workspaceFolder}/BusBuddy.Tests/BusBuddy.Tests.csproj",
        "--settings", "testsettings.runsettings",
        "--collect", "XPlat Code Coverage",
        "--logger", "console;verbosity=detailed"
    ],
    "cwd": "${workspaceFolder}",
    "console": "internalConsole",
    "justMyCode": false
}
```

**Benefits:**
- Automated code coverage collection during test debugging
- Uses comprehensive test settings configuration
- Detailed logging for thorough test analysis

### ✅ **Enhanced Compound Configurations**
```json
{
    "name": "Debug with Coverage Analysis",
    "configurations": ["Run and Debug BusBuddy.WPF", "Debug Tests with Coverage"]
},
{
    "name": "Full Development Debug Session",
    "configurations": ["Debug PowerShell Workflows", "Run and Debug BusBuddy.WPF", "Debug Tests with Coverage"]
}
```

**Benefits:**
- Comprehensive debugging workflows
- Simultaneous app and test debugging with coverage
- PowerShell workflow integration

## Test Configuration File

### ✅ **Created `testsettings.runsettings`**
Comprehensive test configuration including:

- **Parallel Execution**: Optimized for multi-core testing
- **Code Coverage**: XPlat Code Coverage with Cobertura format
- **Exclusion Patterns**: Excludes test projects, generated files, and third-party assemblies
- **Logging Configuration**: Detailed console and TRX logging
- **Performance Settings**: Optimized worker allocation and timeout settings

**Coverage Exclusions:**
- Test projects (`[BusBuddy.Tests]*`)
- Generated files (`*.g.cs`, `*.Designer.cs`)
- Third-party assemblies (`[Syncfusion.*]*`, `[Microsoft.*]*`)
- Migration files (`**/Migrations/**`)

**Included Coverage:**
- Core business logic (`[BusBuddy.Core]*`)
- WPF presentation layer (`[BusBuddy.WPF]*`)

## Enhanced Task Integration

### ✅ **New Coverage-Related Tasks**
1. **Run Tests with Coverage**: Executes tests with coverage collection using testsettings.runsettings
2. **Generate Coverage Report**: Creates HTML coverage reports using ReportGenerator
3. **Clean Test Results**: Cleans up test artifacts and coverage data

### ✅ **Integration Benefits**
- Seamless workflow from debug → test → coverage → cleanup
- PowerShell workflow debugging alongside .NET debugging
- Automated post-debug cleanup prevents artifact accumulation
- Comprehensive coverage analysis with HTML report generation

## Debug Workflow Recommendations

### **Quick Development Cycle**
1. Use "Run and Debug BusBuddy.WPF" for standard application debugging
2. Automatic cleanup occurs after each session

### **Test-Driven Development**
1. Use "Debug Tests with Coverage" for test debugging with coverage analysis
2. Follow up with "Generate Coverage Report" task for HTML analysis

### **Comprehensive Analysis**
1. Use "Full Development Debug Session" for complete workflow debugging
2. Includes PowerShell workflows, application, and tests with coverage

### **PowerShell Workflow Development**
1. Use "Debug PowerShell Workflows" for PowerShell script debugging
2. Direct integration with BusBuddy-Advanced-Workflows.ps1

## Implementation Impact

### **Strengths Enhanced**
- ✅ **Targeted Configurations**: Separate, focused configurations for different debugging scenarios
- ✅ **Environment Variables**: Maintained flexible debugging with ENABLE_DB_VALIDATION and VERBOSE_LOGGING
- ✅ **Symbol Resolution**: Continued use of Microsoft/NuGet symbol servers
- ✅ **Pre/Post Tasks**: Enhanced with automatic cleanup after debugging

### **Suggestions Implemented**
- ✅ **PowerShell Debugging**: Added dedicated PowerShell debug configuration
- ✅ **Post-Debug Cleanup**: Integrated "Clean Solution" as postDebugTask
- ✅ **Coverage Support**: Added testsettings.runsettings with comprehensive coverage configuration
- ✅ **Enhanced Workflows**: Created compound configurations for complex debugging scenarios

## Usage Examples

### **Standard App Debugging**
```bash
F5 → "Run and Debug BusBuddy.WPF"
# Builds → Runs → Debugs → Cleans up automatically
```

### **Test Coverage Analysis**
```bash
F5 → "Debug Tests with Coverage"
# Builds → Runs tests with coverage → Debugs → Cleans up
# Follow with "Generate Coverage Report" task
```

### **Full Development Session**
```bash
F5 → "Full Development Debug Session"
# PowerShell workflows → App debugging → Test coverage → All cleanup
```

## File Structure Impact

### **New Files Created**
- `testsettings.runsettings` - Comprehensive test configuration
- `VSCODE-DEBUG-ENHANCEMENTS.md` - This documentation

### **Enhanced Files**
- `.vscode/launch.json` - Added configurations and enhanced existing ones
- `.vscode/tasks.json` - Added coverage-related tasks (with custom problem matchers)

## Next Steps

1. **Test the new configurations** to ensure proper functionality
2. **Install ReportGenerator** if HTML coverage reports are desired:
   ```bash
   dotnet tool install -g dotnet-reportgenerator-globaltool
   ```
3. **Customize testsettings.runsettings** based on specific project needs
4. **Create team documentation** for the new debugging workflows

## Performance Considerations

- **Post-debug cleanup** prevents build artifact accumulation
- **Parallel test execution** optimizes test performance
- **Targeted coverage collection** focuses on relevant code areas
- **Efficient symbol resolution** through Microsoft/NuGet servers

This enhancement provides a comprehensive, integrated debugging experience that supports the full development lifecycle from PowerShell workflows through application debugging to test coverage analysis.
