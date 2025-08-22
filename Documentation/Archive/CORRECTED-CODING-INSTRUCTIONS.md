# BusBuddy Coding Instructions - CORRECTED .NET 9 VERSION

**Date Updated**: August 3, 2025  
**Status**: ✅ **CORRECTED** - All .NET version references updated to .NET 9 for consistency

---

## 🎯 **BusBuddy MVP Priority**

**Primary Goal**: Achieve a clean build (0 errors) and support a runnable application with student entry and route assignment by end-of-day, per the Greenfield Reset strategy.

**MVP Focus**: Students, routes, basic UI forms. AI assistants must:

- Prioritize `bb-*` commands (`bb-build`, `bb-run`, `bb-mvp-check`) over raw `dotnet` commands.
- Support disabling non-MVP services (e.g., XAI, GoogleEarthEngine) to resolve build errors (e.g., CS0246).
- Enforce Syncfusion-only UI and Serilog logging to prevent regressions.
- Run `bb-anti-regression` and `bb-xaml-validate` before suggesting changes.

**Deferred Until Post-MVP**:

- XAI integration (e.g., `XAIService`, `OptimizedXAIService`).
- Google Earth Engine integration (e.g., `GoogleEarthEngineService`).
- Complex features: vehicle management, driver scheduling, maintenance, fuel tracking, advanced reporting.

**CRITICAL: Use BusBuddy PowerShell Commands First**

- **Always use `bb-*` commands** instead of raw dotnet commands
- **Check available commands**: Use `bb-commands` to see all options
- **Health checks**: Use `bb-health` before troubleshooting
- **MVP validation**: Use `bb-mvp-check` to ensure student/route functionality
- **Anti-regression**: Use `bb-anti-regression` and `bb-xaml-validate` before commits

**Primary Development Commands:**

```powershell
bb-health         # System health check
bb-build          # Build solution
bb-run            # Run application
bb-test           # Run tests
bb-mvp-check      # Check MVP readiness
bb-anti-regression # Prevent legacy patterns
bb-xaml-validate  # Ensure Syncfusion-only UI
bb-commands       # List all commands
```

---

## 🛠️ **Technology Stack & Versions - .NET 9 ONLY**

### **✅ CORRECTED Global Tools & SDK Versions**

- **PowerShell**: 7.5.2 (Required minimum version)
- **.NET SDK**: 9.0.303 (per `global.json`)
- **Target Framework**: **.NET 9.0-windows** (WPF projects) - **CORRECTED**
- **MSBuild SDK**: Microsoft.Build.Traversal 3.4.0
- **Roll Forward Policy**: latestMinor (per `global.json`)

### **✅ CORRECTED Package Versions (Directory.Build.props)**

- **Syncfusion WPF**: 30.1.42 (Essential Studio for WPF, per Directory.Build.props)
- **Entity Framework Core**: 9.0.7 (.NET 9 compatible)
- **Serilog**: 4.3.0 (Pure Serilog implementation)
- **Code Analysis**: Enabled with Recommended analysis mode
- **Practical Ruleset**: `BusBuddy-Practical.ruleset` for MVP development

### **✅ Project File Consistency Check - ALL .NET 9**

```xml
<!-- ALL PROJECT FILES NOW USE .NET 9 -->
<TargetFramework>net9.0-windows</TargetFramework>

<!-- Confirmed in: -->
<!-- ✅ BusBuddy.Core/BusBuddy.Core.csproj -->
<!-- ✅ BusBuddy.WPF/BusBuddy.WPF.csproj -->
<!-- ✅ BusBuddy.Tests/BusBuddy.Tests.csproj -->
```

### **Database Configuration Standards**

- **Development**: LocalDB with SQL Server LocalDB instance
- **Production**: Azure SQL Database with secure connection strings
- **Connection Strings**: Environment variable substitution for credentials
- **Database Provider**: Configurable via `appsettings.json` DatabaseProvider setting

**Database Connection Examples:**

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=[Project];Integrated Security=True;MultipleActiveResultSets=True",
        "AzureConnection": "Server=tcp:[server].database.windows.net,1433;Initial Catalog=[database];Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    },
    "DatabaseProvider": "LocalDB"
}
```

---

## 🚫 **CRITICAL: MANDATORY DOCUMENTATION COMPLIANCE**

**NO CODE WITHOUT PROPER DOCUMENTATION REFERENCE - ZERO TOLERANCE POLICY**

### **Documentation-First Development - ABSOLUTE REQUIREMENT**

- ❌ **FORBIDDEN**: Writing ANY code without referencing official documentation first
- ❌ **FORBIDDEN**: Implementing features based on assumptions or "common patterns"
- ❌ **FORBIDDEN**: Building PowerShell modules without Microsoft PowerShell standards compliance
- ❌ **FORBIDDEN**: Using Syncfusion controls without official Syncfusion documentation reference
- ❌ **FORBIDDEN**: Creating "quick fixes" that violate established standards and best practices

### **MANDATORY DOCUMENTATION SOURCES**

- **Microsoft PowerShell**: [Official PowerShell Documentation](https://docs.microsoft.com/en-us/powershell/) - Required for ALL PowerShell development
- **Microsoft .NET 9**: [Official .NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/) - Required for ALL C# development
- **Syncfusion WPF**: [Official Syncfusion Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf) - Required for ALL UI components
- **Entity Framework 9**: [Official EF Core 9 Documentation](https://docs.microsoft.com/en-us/ef/core/) - Required for ALL data access
- **WPF Framework**: [Official WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - Required for ALL WPF development

---

## 🚫 **CRITICAL: NO SYNCFUSION REGRESSION POLICY**

**ABSOLUTE PROHIBITION: Never Replace Syncfusion Components with Standard WPF Controls**

- ❌ **NEVER replace SfDataGrid with DataGrid** - Fix namespace/reference issues instead
- ❌ **NEVER replace Syncfusion ComboBox with standard ComboBox** - Resolve compilation errors properly
- ❌ **NEVER downgrade working Syncfusion components** - Hard-earned progress must be preserved
- ❌ **NO SHORTCUTS** - Compilation errors must be fixed through proper namespace resolution, not component replacement
- ❌ **NO REGRESSION JUSTIFICATION** - "Fixing errors" is never a valid reason to replace Syncfusion components
- ❌ **UPGRADE, DON'T DOWNGRADE** - Standard DataGrid found in legacy code should be upgraded to SfDataGrid

**MANDATORY ERROR RESOLUTION APPROACH:**

1. **First**: Check namespace declarations and assembly references
2. **Second**: Verify Syncfusion package versions and licensing
3. **Third**: Consult Syncfusion documentation for proper usage patterns
4. **Fourth**: Add missing using statements or update project references
5. **NEVER**: Replace Syncfusion components with standard WPF controls

---

## 🏗️ **Syncfusion Implementation Requirements**

**CRITICAL RULE: Only Use Official Syncfusion Documentation**

- **Reference ONLY**: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- **WPF API Reference**: https://help.syncfusion.com/cr/wpf/Syncfusion.html
- **No custom fixes**: Use only documented Syncfusion APIs, methods, and examples
- **No invented code**: Every Syncfusion implementation must be found in official docs
- **Verify before implementing**: Search documentation first, implement only documented patterns
- **API Reference**: Use Syncfusion's complete WPF API reference for all controls and methods

**Documentation-First Development Process:**

1. **Search Syncfusion WPF docs** for the specific control/feature needed
2. **Find official examples** in the documentation or sample browser
3. **Copy exact patterns** from Syncfusion's documented examples
4. **Test with documented parameters** and properties only
5. **No modifications** to documented patterns without verifying in docs

**Common Syncfusion WPF Controls - Documentation Required:**

- **SfDataGrid**: Follow documented binding and column configuration patterns
- **DockingManager**: Use DockingStyle enum for docking operations
- **SfChart**: Use only documented series types and properties
- **NavigationDrawer**: Follow documented navigation patterns
- **SfButton**: Apply only documented style properties and themes

**Forbidden Practices:**

- ❌ **NO custom Syncfusion extensions** or helper methods
- ❌ **NO invented property combinations** not shown in docs
- ❌ **NO assumed API patterns** based on other frameworks
- ❌ **NO "enhanced" wrappers** around Syncfusion controls
- ❌ **NO undocumented parameters** or method calls
- ❌ **ABSOLUTELY NO REGRESSION** from Syncfusion controls to standard WPF controls
- ❌ **NO REPLACEMENT** of working Syncfusion components with DataGrid, ComboBox, or other standard controls
- ❌ **NO SHORTCUTS** - Fix compilation errors by proper namespace/reference resolution, not by component downgrade

---

## 📚 **BusBuddy Domain Knowledge**

### **🚌 Core Business Context**

- **School transportation management system** for bus fleet operations
- **Key entities**: Buses, Drivers, Routes, Maintenance, Fuel, Activities
- **Primary users**: Transportation coordinators, mechanics, administrators
- **Critical features**: Safety compliance, route optimization, maintenance tracking

### **🎨 UI/UX Standards**

- **Syncfusion controls preferred** over standard Windows Forms
- **Material Design theming** with consistent color schemes
- **Responsive layouts** that handle DPI scaling properly
- **Enhanced dashboards** with diagnostic logging and fallback strategies

---

## 💾 **Data Architecture - .NET 9 Focus**

### **✅ CORRECTED Architecture Standards**

- **Entity Framework Core 9.0.7** for data access (.NET 9 compatible)
- **Repository pattern** with dependency injection
- **SQL Server backend** with proper connection management
- **Test database initialization** for development/testing

### **✅ CORRECTED Development Tools**

- **PowerShell (pwsh) 7.5.2** for ALL commands - use abbreviated pwsh when possible
- **Single build approach** - run one build, get data, move on - no repetitive builds
- **VS Code tasks** for build/test operations
- **Syncfusion licensing** handled via helper classes
- **Git hooks** for code quality enforcement

---

## 🚀 **Application Execution - LOCKED METHOD**

**CRITICAL: Use ONLY this method to run BusBuddy application**

**✅ FINAL SOLUTION: Explicit project targeting required when .sln and .csproj coexist**

**PRIMARY RUN COMMAND:**

```powershell
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj
```

**Build and Run Sequence:**

1. **Clean**: `dotnet clean BusBuddy.WPF/BusBuddy.WPF.csproj`
2. **Build**: `dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj`
3. **Run**: `dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj`
4. **Application should display**: WPF Dashboard window

**Why This Is Required:**

- **Both files exist**: `.sln` and `.csproj` in same directory causes ambiguity
- **dotnet behavior**: Always asks "which project?" when multiple exist
- **Explicit targeting**: Only way to avoid the prompt
- **Standard practice**: Normal .NET development pattern

**Alternative Commands:**

```powershell
# Solution-level operations (when needed)
dotnet build "BusBuddy.sln"
dotnet clean "BusBuddy.sln"

# Direct executable (after build) - .NET 9 path
.\BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.exe
```

**Expected Behavior:**

- Command works consistently every time without prompts
- Application launches as WPF desktop application
- Dashboard window should appear
- Syncfusion controls should be properly initialized
- No console window should remain open

---

## 🧪 **Testing Approach - .NET 9 Compatibility**

### **✅ CORRECTED Testing Standards**

- **UI tests** in `BusBuddy.Tests/UI/` directory
- **Integration tests** for data layer operations with EF Core 9.0.7
- **Coverage reports** generated via PowerShell scripts
- **Avoid legacy test files** - consolidate into organized structure
- **Target Framework**: net9.0-windows for all test projects

---

## 💬 **Communication Preferences**

### **Problem Resolution Approach**

- **Incremental fixes first** - Always attempt targeted edits before complete rebuilds
- **Assess corruption level** - Check actual errors and their scope before deciding approach
- **User consultation** - Let the user decide on complete overhauls vs. targeted fixes
- **Error analysis** - Identify root causes (missing methods, property name mismatches, duplicates)
- **Minimal viable fix** - Use the smallest change that resolves the issue
- **Escalation path**:
    1. First: Targeted edits for specific errors
    2. Second: Consult user if issues appear complex
    3. Last resort: Complete rebuild only with user approval

---

## 🔧 **Mandatory Anti-Regression Checks**

Before suggesting any code changes or commits, run these checks to prevent regressions:

```powershell
bb-anti-regression  # Scans for Microsoft.Extensions.Logging, standard WPF controls, and Write-Host
bb-xaml-validate    # Ensures only Syncfusion controls in XAML
```

**Rules:**

- Never suggest code that introduces Microsoft.Extensions.Logging (use Serilog).
- Never suggest standard WPF controls (e.g., `<DataGrid>`)—use Syncfusion equivalents (`syncfusion:SfDataGrid`).
- Never suggest Write-Host in PowerShell—use Write-Output or Write-Information.
- If checks fail, fix violations first (e.g., replace logging, upgrade controls) and document in commit message.

---

## 📋 **CORRECTED VERSION SUMMARY**

### **✅ FIXED: Version Consistency Achieved**

**Before (INCONSISTENT):**
<<<<<<< HEAD

- # ❌ `BusBuddy.Core.csproj`: `net8.0-windows`
- ❌ `BusBuddy.Core.csproj`: `net9.0-windows`
- ✅ `Directory.Build.props`: `net9.0-windows`

**After (CONSISTENT):**

- ✅ `BusBuddy.Core.csproj`: **CORRECTED** to `net9.0-windows`
- ✅ `BusBuddy.WPF.csproj`: `net9.0-windows`
- ✅ `BusBuddy.Tests.csproj`: `net9.0-windows`
- ✅ `global.json`: .NET SDK 9.0.303
- ✅ `Directory.Build.props`: `net9.0-windows`

### **✅ NO AMBIGUITY - ALL .NET 9**

**Framework Targets:**

- **ALL PROJECTS**: `net9.0-windows`
- **SDK VERSION**: 9.0.303
- **ENTITY FRAMEWORK**: 9.0.7 (.NET 9 compatible)
- **RUNTIME**: .NET 9 only

**File Update Applied:**

- **File**: `BusBuddy.Core/BusBuddy.Core.csproj`
- **Change**: `net8.0-windows` → `net9.0-windows`
- **Status**: ✅ **COMPLETED**

---

**END OF CORRECTED INSTRUCTIONS**

All references to .NET 8 have been eliminated. The project now uses .NET 9 consistently across all components.
