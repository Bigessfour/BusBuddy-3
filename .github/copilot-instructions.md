# BusBuddy AI Assistant Instructions — August 17, 2025

**Purpose**: Completing the BusBuddy finish line vision - a comprehensive, production-ready school transportation management system. Focus on achieving the complete MVP as defined in the finish line criteria, not just stability improvements.

## 🎯 Project Status - Phase 1 Foundation Complete ✅

**Phase 1 Status**: COMPLETED (August 17, 2025) - Foundation Stabilized & Assessed ✅
**Phase 2 Target**: Complete BusBuddy MVP - 6 core modules implementation
**Vision**: World-class school transportation management tool ready for 1,000+ students/routes

### Phase 1 Achievements (Completed August 17, 2025) ✅
- **bbHealth -Detailed**: 100% pass rate, comprehensive environment validation
- **bbXamlValidate**: 35.6% compliance baseline established (29 violations identified for Phase 2)
- **bbBuild**: Clean builds in 11.6s with only minor version warnings
- **bbTest**: Test suite operational and reliable
- **Technical Debt**: All Write-Host violations fixed, proper PowerShell array syntax
- **Documentation**: COPILOT-VISION-PROMPT.md created for AI assistant guidance
- **Foundation**: PowerShell 7.5.2 automation system fully stabilized

### Phase 2 Requirements - Core MVP Modules
- **Student Management Module**: CRUD operations with Syncfusion SfDataGrid, geocoding, validation
- **Vehicle & Driver Management**: Fleet tracking, maintenance calendars, driver profiles
- **Route & Schedule Assignment**: Route builder with SfMap, schedule generation with SfCalendar
- **Activity & Compliance Logging**: Timeline views, compliance reports, audit trails
- **Dashboard & Navigation**: Central hub with DockingManager, global search, theme management
- **Data & Security Layer**: Complete Azure SQL backend, EF Core repositories, secrets management

### Current Environment Status (Phase 1 Complete)
- **Environment**: .NET 9.0.304, PowerShell 7.5.2, Syncfusion WPF 30.2.5 licensed
- **Build Status**: ✅ Clean builds (11.6s), bb* commands operational
- **UI Compliance**: ✅ Syncfusion-only enforcement complete
- **Performance Monitoring**: ✅ Complete (PowerShell 7.5 pipeline-based metrics)
- **Next Phase**: Phase 2 - Core feature implementation (6 MVP modules)

## � PowerShell 7.5.2 Modernization Framework (MANDATORY)

### Primary Commands (Enhanced with Auto-Repair)
```powershell
# MANDATORY: Load environment and validate (one-time per session)
. .\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1

# REQUIRED: Comprehensive health check before any development
bbHealth                     # Basic environment validation
bbHealth -Detailed          # Comprehensive system analysis  
bbHealth -ModernizationScan  # Legacy code and syntax detection
bbHealth -AutoRepair         # Automatic issue resolution
bbHealth -Detailed -ModernizationScan -AutoRepair  # Full audit + repair

# Core workflow (only after bbHealth passes)
bbBuild          # Build solution  
bbRun            # Launch WPF application
bbTest           # Run test suite
bbMvpCheck       # Validate MVP features
bbAntiRegression # Scan for compliance violations
bbXamlValidate   # Validate Syncfusion-only XAML
bbCommands       # List all available commands
```

### Decision Points for Tool Usage

#### 🔍 **WHEN TO USE bbHealth Variants**
- **bbHealth**: Start of every development session, basic validation
- **bbHealth -Detailed**: Performance issues, complex troubleshooting
- **bbHealth -ModernizationScan**: Before code changes, legacy detection
- **bbHealth -AutoRepair**: PSModulePath issues, missing modules
- **Full scan**: Before commits, comprehensive environment audit

#### 🚫 **Deprecated .NET CLI Commands (AUTO-DETECTED)**
| Forbidden Command | Required Replacement | Detection Rule |
|-------------------|---------------------|----------------|
| `dotnet build` | `bbBuild` / `Invoke-BusBuddyBuild` | Flagged in modernization scan |
| `dotnet test` | `bbTest` / `Invoke-BusBuddyTest` | Flagged in modernization scan |
| `dotnet run` | `bbRun` / `Invoke-BusBuddyRun` | Flagged in modernization scan |
| `dotnet clean` | `bbClean` / `Invoke-BusBuddyClean` | Flagged in modernization scan |
| `dotnet restore` | `bbRestore` / `Invoke-BusBuddyRestore` | Flagged in modernization scan |

#### 📦 **PowerShell Gallery Module Decision Matrix**
```powershell
# RULE: If missing PowerShell tool detected → Auto-install from Gallery
# Decision Points:
Database operations    → Install-Module dbatools
Git integration       → Install-Module posh-git  
Testing framework     → Install-Module Pester
Azure management      → Install-Module Az
Package management    → Use built-in PackageManagement
Build automation      → Install-Module InvokeBuild
```

### Missing Definition Resolution Protocol

#### **Automatic Escalation Rules**
1. **bb* alias missing target** → bbHealth -AutoRepair attempts module reload
2. **PowerShell Gallery module missing** → Show install command, offer auto-install
3. **Legacy .NET command detected** → Show PowerShell equivalent + deprecation
4. **Unapproved PowerShell verb** → Suggest approved alternatives
5. **camelCase violation** → Provide correction guidance

#### **Function Naming Standards (AUTO-ENFORCED)**
```powershell
# ✅ REQUIRED: camelCase for internal functions
function getBusBuddyStatus { }
function initializeBusBuddyDatabase { }
function validateBusBuddyConfiguration { }

# ❌ FORBIDDEN: Will be auto-detected and flagged
function Get-BusBuddyStatus { }     # PascalCase reserved for public cmdlets
function GetBusBuddyStatus { }      # Mixed case not allowed
function Ensure-BusBuddySetup { }   # "Ensure" is unapproved verb
```

### Legacy Syntax Modernization (AUTO-DETECTED)

#### **PowerShell 7.5.2 Compliance Rules**
```powershell
# ❌ LEGACY (Auto-flagged by bbHealth -ModernizationScan)
@()                          # Empty array construction
.Replace('old', 'new')       # String replacement method
$var -eq $null              # Null comparison order
ForEach-Object { $_.Prop }   # Verbose object iteration

# ✅ MODERN (PowerShell 7.5.2 compliant)
[array]::new()               # Proper array construction
$var -replace 'old', 'new'   # Regex replacement operator  
$null -eq $var              # Proper null comparison
$collection.ForEach{$_.Prop} # Method syntax iteration
```

### Environment Validation Requirements

#### **PRE-DEVELOPMENT CHECKLIST (AUTOMATED)**
```powershell
# Run before any PowerShell operations:
bbHealth                     # ✅ PowerShell 7.5.2 detected
                            # ✅ All BusBuddy modules loaded
                            # ✅ bb* commands functional
                            # ✅ No legacy syntax detected
                            # ✅ PowerShell Gallery modules available
```

#### **POST-CHANGE VALIDATION (AUTOMATED)**  
```powershell
# Run after any code changes:
bbHealth -ModernizationScan  # ✅ No new legacy patterns
                            # ✅ camelCase compliance maintained
                            # ✅ No unapproved verbs introduced
                            # ✅ No .NET CLI usage added
```

### Integration Standards

#### **Module Loading Hardening**
- **PSModulePath integrity checking** with auto-repair
- **Duplicate module detection** and cleanup
- **Missing manifest validation** 
- **Inconsistent loading path detection**
- **Auto-import of available modules**

#### **Build System Modernization**
- **bb* commands mandatory** for all build operations
- **PowerShell modules replace external tools** where possible
- **Comprehensive error handling** built into all functions
- **Parallel processing capabilities** for performance
- **Structured logging throughout** build pipeline

---

**ENFORCEMENT: bbHealth runs comprehensive validation and provides specific remediation steps for any detected issues. All development must pass bbHealth validation before proceeding.**

## 🛡️ Compliance Standards (Zero Tolerance)

### Syncfusion UI Policy
- **MANDATORY**: Use only Syncfusion WPF controls in UI
- **FORBIDDEN**: Standard WPF controls (DataGrid, ListView, etc.)
- **Global Resources**: All controls defined in resource directories, not locally
- **Themes**: FluentDark and FluentLight via SkinManager only
- **Documentation**: Reference [Syncfusion WPF Docs](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf) for all implementations

### PowerShell Standards
- **Version**: PowerShell 7.5.2 Core edition required
- **Output Streams**: Use Write-Information (not Write-Host) for informational output
- **Error Handling**: No empty catch blocks, proper structured logging
- **Module Compliance**: SupportsShouldProcess for impactful operations
- **Documentation**: Reference [PowerShell Docs](https://learn.microsoft.com/powershell/) for all implementations

### Code Quality
- **Logging**: Serilog only throughout application
- **Documentation**: Official docs required for all implementations:
  - [.NET Documentation](https://learn.microsoft.com/dotnet/)
  - [WPF Documentation](https://learn.microsoft.com/dotnet/desktop/wpf/)
  - [EF Core Documentation](https://learn.microsoft.com/ef/core/)
- **Null Safety**: Nullable reference types enabled, proper null handling
- **Testing**: NUnit framework, comprehensive coverage

## 📁 Repository Structure

### Key Documentation Locations
- `README.md` - Primary setup and workflow guide with finish line status
- `Documentation/FINISH-LINE-VISION.md` - Complete finish line criteria and module requirements
- `Documentation/MASTER-STANDARDS.md` - Consolidated technical standards and compliance rules
- `Documentation/POWERSHELL-STANDARDS.md` - PowerShell compliance rules
- `Documentation/Development/CODING-STANDARDS-HIERARCHY.md` - Development standards
- `BusBuddy.Tests/TESTING-STANDARDS.md` - Testing guidelines

### Project Structure
- `BusBuddy.Core/` - Business logic and data access
- `BusBuddy.WPF/` - Syncfusion WPF presentation layer
- `BusBuddy.Tests/` - Comprehensive test suite
- `PowerShell/` - bb* command automation system

## 🔧 Common Operations

### Before Making Changes
```powershell
bbHealth               # Verify environment
bbAntiRegression       # Check current compliance
bbXamlValidate         # Validate XAML compliance
```

### After Making Changes
```powershell
bbBuild                # Clean build required
bbTest                 # All tests must pass
bbMvpCheck             # MVP features operational
bbAntiRegression       # No new violations
```

### Production Hardening Focus Areas
1. **PowerShell Module Compliance** - Fix empty catch blocks, add ShouldProcess support
2. **Error Handling Enhancement** - Structured logging, user-friendly messages  
3. **Performance Optimization** - Parallel processing, efficient queries
4. **Security Hardening** - Proper authentication, secure configurations
5. **Documentation Updates** - Keep standards current, remove outdated content

## 🏁 Finish Line Criteria - What Will Right Look Like

### Phase 1: Foundation ✅ COMPLETED (August 17, 2025)
- **bbHealth -Detailed**: ✅ 100% pass rate achieved
- **bbBuild**: ✅ Clean builds in 11.6s with only minor warnings
- **bbTest**: ✅ Test suite operational and reliable
- **Technical Debt**: ✅ All Write-Host violations fixed, PowerShell syntax modernized
- **Environment**: ✅ PowerShell 7.5.2 automation system fully stabilized

### Phase 2: Core Feature Implementation (In Progress)
**Target**: Complete 6 MVP modules for full functionality

### Functional Readiness (REQUIRED)
- **End-to-end workflow**: Add 50 students, assign to 5 routes with drivers/vehicles, generate/export schedules – all in <5 minutes without errors
- **Sample data seeded**: 100+ entities for realistic testing
- **Cross-module integration**: Changes (e.g., driver unavailability) cascade to schedules with alerts

### Technical Excellence (REQUIRED)
- **bbHealth**: Passes 100% ✅ (Phase 1 Complete)
- **bbAntiRegression/bbXamlValidate**: Zero violations (currently 29 XAML violations to fix)
- **bbBuild/bbTest**: Success with 90%+ coverage
- **Performance**: <2s for DB ops; memory stable after 1-hour run
- **Security**: No vulnerabilities in scans; all data encrypted in transit

### User and Operational Validation (REQUIRED)
- **UX**: Intuitive for non-tech users; themes consistent, DPI-scaled
- **Deployment**: Runnable MSI package; works offline with Azure sync on reconnect
- **Docs**: Comprehensive user guide in README, with setup in <10 minutes
- **Retrospective**: All journey lessons applied – e.g., one-issue fixes, concise workflows

### Module Completion Status
- [x] **Data & Security Foundation**: PowerShell 7.5.2 system, EF Core setup, build automation ✅
- [ ] **Student Management Module**: CRUD operations, Syncfusion SfDataGrid, geocoding, validation
- [ ] **Vehicle & Driver Management**: Fleet tracking, maintenance calendars via SfScheduler
- [ ] **Route & Schedule Assignment**: Route builder with SfMap, schedule generation with SfCalendar
- [ ] **Activity & Compliance Logging**: Timeline views, compliance reports, audit trails
- [ ] **Dashboard & Navigation**: Central hub with DockingManager, global search, themes

## 🚫 Deprecated/Deferred

- **Post-MVP enhancements** - AI route optimization, mobile integrations (after finish line)
- **Manual dotnet commands** - Use bb* automation instead
- **Standard WPF controls** - Use Syncfusion equivalents only
- **Local styles/attributes** - All UI elements via global resource dictionaries
- **Phase terminology** - Focus on finish line completion, not incremental phases
- **Partial implementations** - Complete modules fully before moving to next

---

**Last Updated**: August 17, 2025
**Validation**: Run `bbCommands` to verify bb* automation is available
