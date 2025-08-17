# BusBuddy Copilot Vision Prompt

**Purpose**: Consolidated guide for AI assistants working on BusBuddy - the comprehensive school transportation management system.

## 🎯 Project Vision

**BusBuddy** is a world-class school transportation management tool designed for 1,000+ students/routes with enterprise-grade reliability, modern UI, and comprehensive compliance tracking.

### Target Users
- **Transportation Coordinators**: Route planning, student assignments, driver scheduling
- **Mechanics & Fleet Managers**: Vehicle maintenance, fuel tracking, compliance reporting  
- **School Administrators**: Safety oversight, budget management, reporting dashboards
- **Drivers**: Route information, incident reporting, daily check-ins

## 🛡️ Non-Negotiable Standards (Zero Tolerance)

### **UI Framework: Syncfusion WPF Only**
- ✅ **REQUIRED**: Use only [Syncfusion WPF controls](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
- ❌ **FORBIDDEN**: Standard WPF controls (DataGrid, ListView, TreeView, etc.)
- **Themes**: FluentDark and FluentLight via SkinManager only
- **Global Resources**: All controls defined in resource directories, never locally

### **PowerShell: 7.5.2 Core Standards**
- ✅ **REQUIRED**: PowerShell 7.5.2 Core edition with bb* automation commands
- ✅ **REQUIRED**: Use `Write-Information` (not `Write-Host`) for output
- ✅ **REQUIRED**: Use `@()` for empty arrays (not `[array]::new()`)
- ✅ **REQUIRED**: SupportsShouldProcess for impactful operations
- **Commands**: Always use `bbBuild`, `bbTest`, `bbRun` instead of raw dotnet CLI

### **Code Quality Enforcement**
- **Logging**: Serilog throughout application (never Console.WriteLine)
- **Null Safety**: Nullable reference types enabled, proper null handling
- **Testing**: NUnit framework with comprehensive coverage via `bbTest`
- **Documentation**: Official Microsoft/Syncfusion docs required for all implementations

## 🏗️ Architecture Overview

### **Core Modules (MVP Requirement)**
1. **Student Management**: CRUD operations, Syncfusion SfDataGrid, geocoding, validation
2. **Vehicle & Driver Management**: Fleet tracking, maintenance calendars via SfScheduler  
3. **Route & Schedule Assignment**: Route builder with SfMap, schedule generation with SfCalendar
4. **Activity & Compliance Logging**: Timeline views, compliance reports, audit trails
5. **Dashboard & Navigation**: Central hub with DockingManager, global search, themes
6. **Data & Security Layer**: Azure SQL backend, EF Core repositories, secrets management

### **Technology Stack**
- **Frontend**: WPF with Syncfusion Essential WPF (licensed)
- **Backend**: .NET 9.0.304, Entity Framework Core, Azure SQL Database
- **Logging**: Serilog with structured logging throughout
- **Testing**: NUnit, comprehensive automation via PowerShell bb* commands
- **CI/CD**: GitHub Actions with bb* command integration

## 🔧 Development Workflow

### **Pre-Development Checklist**
```powershell
bbHealth -Detailed              # Verify environment
bbAntiRegression               # Check compliance  
bbXamlValidate                 # Validate Syncfusion usage
```

### **Standard Development Cycle**
```powershell
bbBuild                        # Clean build
bbTest                         # Run test suite
bbMvpCheck                     # Validate MVP features
bbRun                          # Launch application
```

### **Quality Gates**
- **bbHealth**: Must pass 100% before any development
- **bbAntiRegression**: Zero violations allowed
- **bbXamlValidate**: 100% Syncfusion compliance required
- **bbTest**: 90%+ test coverage required

## 📋 Common Tasks & Patterns

### **Adding New Syncfusion Controls**
1. **Research First**: Check [Syncfusion WPF Documentation](https://help.syncfusion.com/wpf/)
2. **Use Global Resources**: Define in `BusBuddy.WPF/Resources/`
3. **Theme Integration**: Ensure FluentDark/FluentLight compatibility
4. **Test Integration**: Add to `bbXamlValidate` scanning

### **Data Operations** 
1. **Repository Pattern**: Use existing `BusBuddy.Core/Services/` patterns
2. **Entity Framework**: Follow existing DbContext patterns
3. **Async/Await**: All database operations must be async
4. **Error Handling**: Structured logging with Serilog

### **PowerShell Automation**
1. **Function Naming**: Use camelCase for internal functions
2. **Error Handling**: No empty catch blocks, proper structured logging  
3. **Module Compliance**: SupportsShouldProcess for impactful operations
4. **Performance**: Use pipeline-based operations for large datasets

## 🚨 Common Pitfalls to Avoid

### **UI Development**
- ❌ **Never** use standard WPF controls (DataGrid, ListView, etc.)
- ❌ **Never** define styles/resources locally in UserControls
- ❌ **Never** hardcode colors or themes (use SkinManager)
- ❌ **Never** ignore DPI scaling considerations

### **PowerShell Development**  
- ❌ **Never** use `Write-Host` (use `Write-Information`)
- ❌ **Never** use raw `dotnet` commands (use bb* equivalents)
- ❌ **Never** use `[array]::new()` (use `@()`)
- ❌ **Never** skip `SupportsShouldProcess` for destructive operations

### **General Development**
- ❌ **Never** use `Console.WriteLine` (use Serilog)
- ❌ **Never** ignore nullable reference type warnings
- ❌ **Never** skip running `bbAntiRegression` before commits
- ❌ **Never** assume code works without running `bbTest`

## 🎯 Success Criteria (Finish Line Vision)

### **Functional Readiness**
- End-to-end workflow: Add 50 students, assign to 5 routes with drivers/vehicles, generate/export schedules – all in <5 minutes without errors
- Sample data seeded: 100+ entities for realistic testing  
- Cross-module integration: Changes (e.g., driver unavailability) cascade to schedules with alerts

### **Technical Excellence**
- `bbHealth`: Passes 100%
- `bbAntiRegression`/`bbXamlValidate`: Zero violations
- `bbBuild`/`bbTest`: Success with 90%+ coverage
- Performance: <2s for DB ops; memory stable after 1-hour run
- Security: No vulnerabilities in scans; all data encrypted in transit

### **User & Operational Validation**
- UX: Intuitive for non-tech users; themes consistent, DPI-scaled
- Deployment: Runnable MSI package; works offline with Azure sync on reconnect  
- Documentation: Comprehensive user guide in README, setup in <10 minutes
- Retrospective: All journey lessons applied – one-issue fixes, concise workflows

## 📚 Essential References

- **[Syncfusion WPF Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)** - Primary UI framework reference
- **[.NET 9.0 Documentation](https://learn.microsoft.com/dotnet/)** - Core platform reference
- **[PowerShell 7.5 Documentation](https://learn.microsoft.com/powershell/)** - Automation framework reference
- **[Entity Framework Core](https://learn.microsoft.com/ef/core/)** - Data access reference

## 🔄 Continuous Improvement

This prompt evolves with the project. Keep it updated as new patterns emerge and standards are refined. The goal is always: **world-class school transportation management ready for production deployment**.

---

**Last Updated**: August 17, 2025  
**Validation**: Run `bbCommands` to verify bb* automation availability
