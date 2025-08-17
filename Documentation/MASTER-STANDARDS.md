# BusBuddy Master Standards Document
*Last Updated: August 17, 2025*

## Purpose

This document consolidates all technical standards, compliance requirements, and development guidelines for the BusBuddy project as we approach the finish line. Every implementation must adhere to these standards.

## 🎯 Finish Line Vision

**Repository**: https://github.com/Bigessfour/BusBuddy-3  
**Azure SQL**: https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql  
**Syncfusion WPF**: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf

This document reflects the comprehensive finish line criteria for BusBuddy - a complete, production-ready school transportation management system. Not just MVP, but the full vision implemented with uncompromising technical standards.

## 🛡️ Compliance Standards (Zero Tolerance)

### UI Framework Standards
- **MANDATORY**: Use only Syncfusion WPF controls in UI
- **FORBIDDEN**: Standard WPF controls (DataGrid, ListView, Button, TextBox, etc.)
- **Global Resources**: All controls defined in resource directories, not locally
- **Themes**: FluentDark and FluentLight via SkinManager only
- **Documentation**: Reference [Syncfusion WPF Docs](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf) for all implementations

**Enforcement**:
- `bbXamlValidate` command scans for violations
- `bbAntiRegression` catches non-Syncfusion usage
- Zero tolerance policy - any standard WPF control is a build failure

### PowerShell Standards
- **Version**: PowerShell 7.5.2 Core edition required
- **Output Streams**: Use Write-Information (not Write-Host) for informational output
- **Error Handling**: No empty catch blocks, proper structured logging
- **Module Compliance**: SupportsShouldProcess for impactful operations
- **Performance**: Module load metrics with pipeline chaining (implemented)
- **Documentation**: Reference [PowerShell Docs](https://learn.microsoft.com/powershell/) for all implementations

**Enforcement**:
- `bbAntiRegression` scans for Write-Host usage
- PowerShell linting via PSScriptAnalyzer
- Performance monitoring tracks module load times

### Code Quality Standards
- **Logging**: Serilog only throughout application
- **Documentation**: Official docs required for all implementations:
  - [.NET Documentation](https://learn.microsoft.com/dotnet/)
  - [WPF Documentation](https://learn.microsoft.com/dotnet/desktop/wpf/)
  - [EF Core Documentation](https://learn.microsoft.com/ef/core/)
  - [Syncfusion WPF Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
- **Null Safety**: Nullable reference types enabled, proper null handling
- **Testing**: NUnit framework, 90%+ coverage requirement

## 📋 Module Implementation Standards

### 1. Student Management Module
**Technical Requirements**:
- **Data Grid**: Syncfusion SfDataGrid for student listing
- **Forms**: Syncfusion SfTextBox, SfComboBox for input validation
- **Export**: Syncfusion PDF/Excel export functionality
- **Geocoding**: Offline-capable address geocoding
- **Database**: EF Core with Azure SQL, real-time synchronization

**Syncfusion Controls Required**:
```xml
<!-- Student List -->
<syncfusion:SfDataGrid />

<!-- Student Form -->
<syncfusion:SfTextBox />
<syncfusion:SfComboBox />
<syncfusion:SfDatePicker />

<!-- Export Options -->
<syncfusion:SfButton />
```

**Documentation Links**:
- [SfDataGrid Documentation](https://help.syncfusion.com/wpf/datagrid/overview)
- [SfTextBox Documentation](https://help.syncfusion.com/wpf/textbox/overview)

### 2. Vehicle & Driver Management
**Technical Requirements**:
- **Scheduler**: Syncfusion SfScheduler for maintenance calendars
- **Charts**: Syncfusion SfChart for fleet utilization visualization
- **Data Management**: Syncfusion SfDataGrid for vehicle/driver listings
- **Notifications**: Maintenance alerts and expiration warnings

**Syncfusion Controls Required**:
```xml
<!-- Maintenance Calendar -->
<syncfusion:SfScheduler />

<!-- Fleet Analytics -->
<syncfusion:SfChart />

<!-- Vehicle/Driver Lists -->
<syncfusion:SfDataGrid />
```

### 3. Route & Schedule Assignment
**Technical Requirements**:
- **Mapping**: Syncfusion SfMap for route visualization
- **Calendar**: Syncfusion SfCalendar for schedule management
- **Tree Navigation**: Syncfusion SfTreeView for hierarchical routes
- **Drag & Drop**: Route assignment interface

**Syncfusion Controls Required**:
```xml
<!-- Route Mapping -->
<syncfusion:SfMap />

<!-- Schedule Management -->
<syncfusion:SfCalendar />

<!-- Route Hierarchy -->
<syncfusion:SfTreeView />
```

### 4. Dashboard & Navigation
**Technical Requirements**:
- **Docking**: Syncfusion DockingManager for panel management
- **Search**: Syncfusion SfAutoComplete for global search
- **Themes**: SkinManager for FluentDark/FluentLight switching
- **Navigation**: Tabbed interface with real-time metrics

**Syncfusion Controls Required**:
```xml
<!-- Main Layout -->
<syncfusion:DockingManager />

<!-- Global Search -->
<syncfusion:SfAutoComplete />

<!-- Themed Buttons -->
<syncfusion:SfButton />
```

## 🔧 Development Workflow Standards

### Build Commands (bb* Only)
- **bbHealth** - Environment validation (100% pass required)
- **bbBuild** - Solution build (zero warnings)
- **bbTest** - Test execution (90%+ coverage)
- **bbRun** - Application launch
- **bbAntiRegression** - Compliance scanning (zero violations)
- **bbXamlValidate** - Syncfusion-only XAML validation

### Performance Requirements
- **Database Operations**: <2 seconds response time
- **Module Loading**: Performance metrics tracked via PowerShell 7.5 pipeline
- **Memory Usage**: Stable after 1-hour continuous operation
- **UI Responsiveness**: No blocking operations on UI thread

### Testing Standards
- **Coverage**: 90% minimum via NUnit
- **Integration**: End-to-end workflow testing
- **UI Testing**: Syncfusion control interaction testing
- **Performance**: Load testing with 1,000+ entities

## 📊 Finish Line Validation Criteria

### Functional Readiness ✅
1. **End-to-end workflow**: Add 50 students, assign to 5 routes with drivers/vehicles, generate/export schedules – all in <5 minutes without errors
2. **Sample data seeded**: 100+ entities for realistic testing
3. **Cross-module integration**: Changes (e.g., driver unavailability) cascade to schedules with alerts

### Technical Excellence ✅
1. **bbHealth**: Passes 100%
2. **bbAntiRegression/bbXamlValidate**: Zero violations
3. **bbBuild/bbTest**: Success with 90%+ coverage
4. **Performance**: <2s for DB ops; memory stable after 1-hour run
5. **Security**: No vulnerabilities in scans; all data encrypted in transit

### User and Operational Validation ✅
1. **UX**: Intuitive for non-tech users; themes consistent, DPI-scaled
2. **Deployment**: Runnable MSI package; works offline with Azure sync on reconnect
3. **Docs**: Comprehensive user guide in README, with setup in <10 minutes
4. **Retrospective**: All journey lessons applied – e.g., one-issue fixes, concise workflows

## 🚫 Prohibited Practices

### Absolutely Forbidden
- **Standard WPF Controls**: Button, TextBox, DataGrid, ListView, etc.
- **Local Styles**: All styling via global resource dictionaries
- **Write-Host**: Use Write-Information with proper streams
- **Microsoft.Extensions.Logging**: Serilog only
- **Manual dotnet commands**: Use bb* automation
- **Hardcoded secrets**: Environment variables only
- **Empty catch blocks**: Proper error handling required
- **Phase-based terminology**: Focus on finish line completion

### Documentation Requirements
- **Every Syncfusion control**: Must link to official documentation
- **No invented APIs**: Only use documented patterns
- **Code examples**: Must match official Syncfusion samples
- **Error solutions**: Reference official troubleshooting guides

## 🎯 Success Metrics

### Development Metrics
- **Build Success**: 100% clean builds
- **Test Coverage**: 90%+ across all modules
- **Performance**: All operations <2 seconds
- **Compliance**: Zero violations in scans

### User Experience Metrics
- **Setup Time**: <10 minutes from clone to running
- **Workflow Speed**: Complete operations in <5 minutes
- **Responsiveness**: No UI blocking or delays
- **Intuitiveness**: Non-technical users can operate effectively

### Operational Metrics
- **Reliability**: Stable operation under load
- **Scalability**: 1,000+ students/routes supported
- **Maintenance**: Automated updates and synchronization
- **Security**: Full encryption and secure authentication

## 📚 Reference Documentation

### Primary References
- **Syncfusion WPF**: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- **Azure SQL**: https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql
- **PowerShell 7.5**: https://learn.microsoft.com/powershell/
- **EF Core 9**: https://learn.microsoft.com/ef/core/
- **.NET 9**: https://learn.microsoft.com/dotnet/

### Internal Documentation
- **Finish Line Vision**: [Documentation/FINISH-LINE-VISION.md](FINISH-LINE-VISION.md)
- **Copilot Instructions**: [.github/copilot-instructions.md](../.github/copilot-instructions.md)
- **Testing Standards**: [BusBuddy.Tests/TESTING-STANDARDS.md](../BusBuddy.Tests/TESTING-STANDARDS.md)
- **PowerShell Standards**: [Documentation/POWERSHELL-STANDARDS.md](POWERSHELL-STANDARDS.md)

---

**This document represents the complete technical standard for BusBuddy finish line completion. Every line of code, every control, every command must meet these standards. No exceptions, no partial implementations, no technical debt.**

*From the future perch of completion: These standards are the foundation of BusBuddy's success.*
