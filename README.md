# 🚌 BusBuddy — School Transportation Management System

Modern WPF application for school bus fleet management, built on .NET 9 and Syncfusion WPF controls.

[![Syncfusion](https://img.shields.io/badge/Syncfusion-30.2.5%20✅%20Licensed-orange)](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
[![MVP](https://img.shields.io/badge/MVP-Achieved-brightgreen)](#-project-status)
[![Status](https://img.shields.io/badge/Phase-Production%20Hardening-blue)](#-project-status)

## 🎯 Overview

BusBuddy streamlines school transportation operations with route management, student assignments, and fleet oversight — using professional-grade Syncfusion controls for a modern UI.

**Production Status (August 2025)**
- ✅ **Phase 1 Complete (Aug 17)**: Foundation stabilized, technical debt resolved
- ✅ PowerShell Performance Monitoring: Advanced metrics system operational
- ✅ Build Environment: .NET 9.0.304, PowerShell 7.5.2, Syncfusion 30.2.5 licensed
- ✅ UI Compliance: Syncfusion-only policy enforced, no standard WPF controls
- ✅ Development Tools: bb* command surface fully operational
- 🎯 **Current Focus**: [Phase 2 - Core Features](Documentation/FINISH-LINE-VISION.md) - MVP module implementation

Highlights
- 🚌 Fleet management: assignments, maintenance, compliance
- 👥 Student enrollment and route assignmen
- 📍 Route planning (non‑MVP integrations like XAI are deferred)
- 📊 Dashboards and basic analytics

## 🚀 Quick start

### Prerequisites
- Windows 10/11 (WPF)
- .NET SDK 9.0.303+
- PowerShell 7.5.2+
- VS Code (recommended) or Visual Studio 2022

### Install & run
```powershell
# Clone
git clone https://github.com/Bigessfour/BusBuddy-3.git
cd BusBuddy

# Load hardened PowerShell module system (persistent ~300ms)
. .\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1

# Optional — set Syncfusion license to avoid trial dialogs
$env:SYNCFUSION_LICENSE_KEY = "<your-key>"  # https://help.syncfusion.com/wpf/wpf-license-registration

# Health → Build → Run (all commands available immediately)
bbHealth            # Environment diagnostics - Test-BusBuddyHealth
bbBuild             # Build solution - Invoke-BusBuddyBuild  
bbRun               # Launch WPF app - Start-BusBuddyApplication
bbTest              # Run tests - Start-BusBuddyTest

# Production commands
bbMvpCheck          # Validate MVP features
bbAntiRegression    # Scan for compliance violations  
bbXamlValidate      # Validate Syncfusion-only XAML
bbCommands          # List all available commands

# Module system persistence (NEW)
bbRefresh           # Refresh/reload all bb* commands if missing
bbStatus            # Check module load status and environment
bbValidate          # Validate environment setup and dependencies
bbRepair            # Repair module system if commands are unavailable

# Quality assurance commands
bbMvpCheck          # Validate MVP readiness
bbAntiRegression    # Scan for anti-patterns (Microsoft.Extensions.Logging, standard WPF controls, Write-Host)
bbXamlValidate      # Ensure Syncfusion-only XAML controls
bbCommands          # List all available bb* commands with descriptions

# CLI Integration Commands (if GitHub CLI, Azure CLI, GitKraken CLI installed)
bbFullScan          # Comprehensive scan using all CLI tools
bbWorkflows         # Scan GitHub workflows  
bbAzResources       # Scan Azure resources  
bbRepos             # Scan repositories (GitHub) or workspaces (GitKraken)
bbGh <args>         # GitHub CLI wrapper
bbAz <args>         # Azure CLI wrapper  
bbGk <args>         # GitKraken CLI wrapper

# Fallback (explicit target when using dotnet directly)
# Note: Prefer bb* commands for integrated workflow
# dotnet run --project .\BusBuddy.WPF\BusBuddy.WPF.csproj
```

### PowerShell-first workflow (optimized)

**Performance Improvements:**
- **Profile loading**: ~300ms (hardened module system)
- **Hardened persistence**: bb* commands survive terminal refreshes and crashes
- **Retry logic**: Robust module loading with validation and recovery
- **Dynamic repo discovery**: Works for any clone location automatically
- **Up-to-date versions**: .NET 9.0.108, Syncfusion WPF 30.2.5
- **CLI integration**: GitHub CLI, Azure CLI, GitKraken CLI support with PowerShell Gallery modules

We now use a hardened PowerShell module system that loads in ~300ms with comprehensive persistence, retry logic, and crash recovery to ensure bb* commands are always available.

**Hardened profile structure:**
```
PowerShell/
├─ Profiles/
│  ├─ Microsoft.PowerShell_profile.ps1  # Hardened wrapper with module manager integration
│  ├─ BusBuddyProfile.ps1              # Main profile with lazy loading and bb* aliases
│  ├─ Import-BusBuddyModule.ps1         # Legacy bootstrap (compatibility)
│  └─ BusBuddy.ModuleManager.ps1        # NEW: Hardened module manager with retry logic
├─ Modules/
│  ├─ BusBuddy/                         # Core BusBuddy module
│  ├─ BusBuddy.Testing/                 # Testing module
│  └─ BusBuddy.CLI/                     # CLI integration module (GitHub, Azure, GitKraken)
```

**Hardening features:**
- **Persistent commands**: bb* aliases survive terminal refreshes, crashes, and environment changes
- **Retry logic**: Comprehensive validation with 3-attempt retry system for robust loading
- **Environment monitoring**: Continuous validation of PowerShell version, repository state
- **Recovery system**: Automatic repair of broken module states and missing commands
- **Validation gates**: Pre-load checks for PowerShell Core 7.5+, repository structure
- **Fallback mechanisms**: Graceful degradation when components are unavailable

Standard modules from PowerShell Gallery (optional, auto-loaded when needed):
- **Az** — Azure services (loads lazily ~10-15s when first used)
- **SqlServer** — SQL Server management (loads lazily when needed)  
- **PowerShellForGitHub** — GitHub API integration (optional, falls back to gh CLI)
- **dbatools** — Advanced SQL Server administration tools (500+ commands)
- **Logging** — Enhanced PowerShell logging framework with multiple targets
- **WPFBot3000** — WPF DSL framework for UI automation and testing
- **PoshWPF** — WPF XAML UI integration for PowerShell scripts
- InvokeBuild — build automation (if available)
- Pester — testing (if available)

**Azure functions with lazy loading:**
```powershell
# These will trigger module loading on first use
Connect-BusBuddySql -Query "SELECT TOP 5 * FROM Students"
Enable-BusBuddyFirewall -ResourceGroup "BusBuddy-RG"
```

Install manually if needed (CurrentUser scope):
```powershell
# Core modules (recommended)
Install-Module -Name Az, SqlServer -Repository PSGallery -Scope CurrentUser -Force

# Database administration and logging (NEW - auto-installed with profile)
Install-Module -Name dbatools, Logging -Repository PSGallery -Scope CurrentUser -Force

# WPF UI automation and testing (NEW - auto-installed with profile)  
Install-Module -Name WPFBot3000, PoshWPF -Repository PSGallery -Scope CurrentUser -Force

# Optional enhancements (CLI tools preferred for these operations)
Install-Module -Name PowerShellForGitHub -Repository PSGallery -Scope CurrentUser -Force
```

Note: If a module is temporarily unavailable, the profile will continue in a degraded mode with warnings and stubbed commands. The optimized profile ensures fast loading regardless of module availability. Azure modules load automatically when their functions are called.

### What to expect
- **Fast startup**: Profile loads in ~400ms with lazy module loading
- WPF desktop app; a dashboard window opens at launch
- Syncfusion‑only policy for new/refactored UI
- Prefer bb* commands for build/test/run operations
- Azure modules load automatically when Azure functions are used

### Dev quickref
```powershell
bbHealth            # Environment and project diagnostics (Test-BusBuddyHealth)
bbBuild             # Build the solution (Invoke-BusBuddyBuild)
bbRun               # Run the WPF app (Start-BusBuddyApplication)
bbTest              # Run tests (Start-BusBuddyTest)
bbAntiRegression    # Scan for disallowed APIs/patterns
bbXamlValidate      # Validate Syncfusion‑only XAML
bbMvpCheck          # Validate core MVP scenarios

# NEW: Enhanced parallel processing commands
bbTestParallel      # Run test projects in parallel (auto-discovers *Tests*.csproj)
bbHealth -Detailed  # Detailed health check with system specs
bbBuild -MaxCpuCount 12 -TimeoutSeconds 300  # Multi-core build with timeout
bbTest -Parallel -MaxCpuCount 12  # Parallel test execution
bbAntiRegression -ThrottleLimit 12  # 12-thread scanning for anti-patterns

# NEW: Module and environment management
bbModuleStatus      # Check PowerShell module installation and loading status
bbCliStatus         # Check CLI tools availability (git, az, gh, winget, etc.)

# Database operations (auto-loads SqlServer module)
Connect-BusBuddySql -Query "SELECT TOP 5 * FROM Students"  # Azure SQL queries
Invoke-Sqlcmd -ServerInstance "(localdb)\MSSQLLocalDB" -Query "SELECT @@VERSION"  # LocalDB
```

# Performance test (should complete in ~400ms)
Measure-Command { . .\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1 }
```

## 🧪 Testing

**Microsoft Testing Platform 2025 Enhanced (NEW):**
The testing system has been completely upgraded with cutting-edge 2025 technologies and intelligent optimization:

```powershell
# 🚀 Basic Testing - Now with MTP 2025 integration
bbTest                                    # All tests with hyperthreading optimization
bbTest -Parallel -Coverage -Detailed     # Parallel execution with comprehensive coverage
bbTest -Filter "Category=Unit" -Configuration Release  # Filtered unit tests in Release mode

# 🎯 Advanced Test Execution Strategies
bbTest -TestSuite Unit -LiveResults       # Live streaming test results
bbTest -SyncfusionUITests -AzureSQLTests  # Specialized test categories
bbTest -PerformanceTests -MaxCpuCount 8   # Performance testing with custom CPU allocation

# 🔍 Test Discovery & Selection
Invoke-BusBuddyTestDiscovery -IncludeSource -OutputFormat json  # MTP 2025 test discovery
Select-BusBuddyImpactedTests -BaseBranch main -IncludeDependencies  # Smart test selection

# 🎮 Syncfusion WPF UI Automation
Invoke-BusBuddySyncfusionUITests -ControlType DataGrid -Interactive  # DataGrid automation
Invoke-BusBuddySyncfusionUITests -ControlType RibbonControl          # Ribbon testing

# 💾 Azure SQL Integration Testing
Invoke-BusBuddyAzureSQLTests -TestDataSet Full -CleanupTestData      # Database testing
Invoke-BusBuddyAzureSQLTests -ConnectionString "custom-connection"   # Custom connection

# 🏎️ Performance & Load Testing
Invoke-BusBuddyPerformanceTests -ConcurrentUsers 50 -DurationMinutes 10 -MemoryProfiling
Invoke-BusBuddyPerformanceTests -Scenario StudentManagement -ConcurrentUsers 25

# 📊 Live Test Execution with Streaming
Start-BusBuddyLiveTestExecution -TestSuite Performance -StreamResults -RefreshIntervalSeconds 1
Start-BusBuddyLiveTestExecution -TestSuite UI -StreamResults  # Real-time UI test monitoring

# 📈 Advanced Result Analysis
Get-BusBuddyTestResults -IncludeCoverage -GenerateReport -Format Detailed
Get-BusBuddyTestResults -ResultsPath "CustomResults" -Format Summary
```

**2025 Testing Technology Stack:**
- **Microsoft Testing Platform 2025 (MTP)** - Next-generation test platform replacing VSTest
- **Hyperthreading Optimization** - Intelligent CPU core utilization (75% of logical processors)
- **FluentAssertions Advanced Patterns** - BeEquivalentTo, AssertionScope, custom extensions
- **Syncfusion WPF UI Automation** - DataGrid, RibbonControl, DockingManager testing
- **Azure SQL Integration Testing** - Entity Framework Core 9 with Azure SQL patterns
- **Real-time Result Streaming** - Live test progress monitoring with JSON-RPC protocol
- **Smart Test Selection** - Git-diff based test impact analysis

**Performance Optimizations:**
- **Parallel execution** with automatic hyperthreading detection
- **Memory-efficient** coverage collection with multiple formats (Cobertura, OpenCover, JSON)
- **Intelligent test grouping** and distribution across CPU cores
- **Timeout protection** to prevent CI/CD pipeline hanging
- **Async test pattern** support for modern C# testing

**BusBuddy-Specific Testing Scenarios:**
```powershell
# Student Management Workflow Tests
bbTest -Filter "FullyQualifiedName~BusBuddy.Tests.ViewModels.StudentsViewModel" -Detailed

# Route Calculation Algorithm Verification
bbTest -Filter "Category=Unit&Name~RouteCalculation" -Parallel -Coverage

# Syncfusion DataGrid Integration Tests
Invoke-BusBuddySyncfusionUITests -ControlType DataGrid -TimeoutSeconds 45

# Azure SQL Database Integration
Invoke-BusBuddyAzureSQLTests -TestDataSet Minimal -CleanupTestData

# Performance Testing for Large Student Datasets
Invoke-BusBuddyPerformanceTests -Scenario "LargeDataset" -ConcurrentUsers 100 -DurationMinutes 15
```

**Legacy Parallel Testing (Superseded):**
```powershell
# Legacy commands (still functional but superseded by bbTest -Parallel)
bbTestParallel                          # Auto-discover and run test projects in parallel
bbTestParallel -ThrottleLimit 4         # Limit to 4 concurrent test projects
bbTestParallel -Coverage                # Parallel execution with coverage
```

VS Code integration: use the Testing view (or the NUnit Test Runner extension) or stick with bb* commands.

**Example Advanced Testing Workflows:**
```powershell
# 1. Full CI/CD Testing Pipeline
bbTest -TestSuite All -Parallel -Coverage -CoverageFormats @('cobertura', 'opencover') -LiveResults

# 2. Development Iteration Testing
Select-BusBuddyImpactedTests -BaseBranch main | ForEach-Object { bbTest -Categories $_ -NoBuild }

# 3. UI Automation Testing
Invoke-BusBuddySyncfusionUITests -ControlType All -Interactive
Get-BusBuddyTestResults -IncludeCoverage -GenerateReport

# 4. Database Integration Pipeline
Invoke-BusBuddyAzureSQLTests -TestDataSet Full -CleanupTestData
Get-BusBuddyTestResults -Format Detailed

# 5. Performance Benchmarking
Invoke-BusBuddyPerformanceTests -ConcurrentUsers 100 -DurationMinutes 20 -MemoryProfiling
Get-BusBuddyTestResults -ResultsPath "PerformanceResults"
```

Legacy harness scripts in `PowerShell/Testing` are archived — prefer the new `bbTest` with MTP 2025 integration.

## ⚡ PowerShell Module Enhancements

**Performance & Robustness Improvements (August 2025):**

### Parallel Processing & Hyperthreading
All bb* commands now support multi-core execution and timeout protection:

```powershell
# Multi-core builds (uses all available CPU cores)
bbBuild -MaxCpuCount 12 -TimeoutSeconds 300

# Parallel testing with hyperthreading
bbTest -Parallel -MaxCpuCount 12 -TimeoutMinutes 10
bbTestParallel -ThrottleLimit 4  # Run 4 test projects concurrently

# Multi-threaded anti-regression scanning
bbAntiRegression -ThrottleLimit 12  # 12 threads for faster scanning

# Parallel health checks
bbHealth -Detailed -TimeoutSeconds 30  # .NET SDK, Git, Node.js checked concurrently
```

### Enhanced Error Handling & Validation
- **Timeout protection**: All long-running operations have configurable timeouts
- **Process isolation**: Uses `Start-Process` with proper cleanup on failure
- **Enhanced validation**: Detailed error messages with suggested fixes
- **Resource management**: Automatic cleanup of failed processes and jobs

### Environment Testing & Validation (NEW)
Comprehensive testing scripts for development environment setup:

```powershell
# Test all PowerShell modules and capabilities
.\PowerShell\Scripts\Test-AllModules.ps1

# Database connectivity tests
.\Scripts\Database\Test-DatabaseScripts.ps1 -TestLocal -TestAzure

# Module and CLI tool status checks
bbModuleStatus  # PowerShell modules (SqlServer, Logging, WPF tools)
bbCliStatus     # CLI tools (git, az, gh, winget, grok, LocalDB)
```

### Anti-Regression Scanning
Enhanced scanning with parallel execution and detailed reporting:

```powershell
bbAntiRegression  # Scans for:
# ✗ Microsoft.Extensions.Logging (use Serilog)
# ✗ Standard WPF controls (use Syncfusion equivalents)
# ✗ Write-Host in PowerShell (use Write-Information/Write-Output)
# ✗ Nullable reference type violations
```

### Performance Metrics
- **Profile loading**: ~400ms (vs 15+ seconds previously)
- **Parallel scanning**: 12-thread anti-regression checks
- **Concurrent testing**: Multiple test projects run simultaneously
- **Multi-core builds**: Utilizes all available CPU cores
- **Health checks**: Parallel dependency validation with timeouts

**Reference Documentation:**
- [PowerShell ForEach-Object -Parallel](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/foreach-object#example-14--using-parallel-processing)
- [dotnet build options](https://learn.microsoft.com/dotnet/core/tools/dotnet-build#options)
- [dotnet test selective execution](https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests)

## 🔁 CI

GitHub Actions builds and tests on push/PR. Some jobs are conditional on secrets.

Secrets commonly used
- `SYNCFUSION_LICENSE_KEY`
- `BUSBUDDY_CONNECTION`, `AZURE_SQL_SERVER`, `AZURE_SQL_USER`, `AZURE_SQL_PASSWORD`

## 🛠️ Configuration

### Database setup

**NEW: Automated Database Connectivity Scripts**

BusBuddy now includes PowerShell scripts to resolve common database connectivity issues:

```powershell
# Quick database environment test
.\Scripts\Database\Test-DatabaseScripts.ps1 -TestLocal -TestAzure

# Switch to LocalDB for development (auto-backup original config)
.\Scripts\Database\Update-AppSettingsForLocalDB.ps1 -WpfProjectPath "BusBuddy.WPF" -BackupOriginal

# Install LocalDB if missing
.\Scripts\Database\Install-LocalDB.ps1 -TestConnection

# Add your IP to Azure SQL firewall (replace with actual values)
.\Scripts\Database\Add-AzureSqlFirewallRule.ps1 -ResourceGroup "YourRG" -ServerName "YourServer"
```

**Standard EF Core Operations:**
```powershell
# Update database using explicit project targets
dotnet ef database update --project .\BusBuddy.Core --startup-project .\BusBuddy.WPF

# Add a migration
dotnet ef migrations add NewMigrationName --project .\BusBuddy.Core --startup-project .\BusBuddy.WPF
```

### EF Core migrations (quickref)
Docs: https://learn.microsoft.com/ef/core/

```powershell
# Keep tools in sync with the repo’s EF Core version
dotnet tool update --global dotnet-ef --version 9.0.8
# List migrations
dotnet ef migrations list --project .\BusBuddy.Core --startup-project .\BusBuddy.WPF
```

### Environment variables
- `SYNCFUSION_LICENSE_KEY` — required before any Syncfusion UI initialization (recommended for CI/production)
- `BUSBUDDY_CONNECTION` — default connection string name/key for the application
- Azure (optional): `AZURE_SQL_USER` and `AZURE_SQL_PASSWORD`

### Enhanced PowerShell Tools (NEW)

**Database Administration:**
```powershell
# SqlServer module (97 commands available)
Invoke-Sqlcmd -ServerInstance "server" -Database "db" -Query "SELECT * FROM Users"
Get-SqlDatabase -ServerInstance "(localdb)\MSSQLLocalDB"
Backup-SqlDatabase / Restore-SqlDatabase

# dbatools module (500+ advanced commands - install separately)
# Note: May conflict with SqlServer module when loaded simultaneously
```

**Enhanced Logging:**
```powershell
# Logging module for PowerShell scripts
Write-Log -Level INFO -Message "BusBuddy operation started"
Add-LoggingTarget -File -Path "logs/powershell-debug.log"
Set-LoggingDefaultLevel -Level DEBUG
```

**WPF UI Automation (for testing):**
```powershell
# WPFBot3000 - DSL framework
Window {
    TextBox -Name 'TestInput'
    Button 'Execute Test' -OnClick { Show-MessageBox $WPFVariable.TestInput.Text }
} | Show-WPFWindow

# PoshWPF - XAML integration
New-WPFXaml -Path "TestUI.xaml" | Show-WPFDialog
```

**Module Management:**
```powershell
bbModuleStatus      # Check all PowerShell module status
bbCliStatus         # Check CLI tools (git, az, gh, winget, grok, etc.)
```

## 🏗️ Architecture

Tech stack (current)
- .NET SDK: 9.0.304 (updated January 2025)
- WPF + Syncfusion WPF: 30.2.5 (updated January 2025)
- EF Core: 9.0.8
- Serilog: 4.3.0
- NUnit: 4.3.1
- PowerShell: 7.5.2+ (enhanced with parallel processing and timeout protection)

Project layout
```
BusBuddy/
├─ BusBuddy.Core/   # Business logic, data access, services
├─ BusBuddy.WPF/    # WPF UI layer with Syncfusion controls
├─ BusBuddy.Tests/  # Unit and integration tests
├─ PowerShell/      # Build/test/run automation (bb* commands)
├─ BusBuddy.Cli/    # .NET CLI tools for complex tasks (migrations, code analysis)
└─ Documentation/   # Project documentation
```

Design patterns
- MVVM with INotifyPropertyChanged
- Simple DI
- Repository where appropriate

## ⚙️ Build/run quickref

```powershell
bbBuild
bbRun
# Or explicitly with dotnet (not recommended - use bb* commands instead)
# dotnet build .\BusBuddy.sln
# dotnet run --project .\BusBuddy.WPF\BusBuddy.WPF.csproj
```

## 🔑 Syncfusion licensing

Set before running to avoid trial dialogs:
```powershell
$env:SYNCFUSION_LICENSE_KEY = "<your-key>"
```
Registration occurs in `App.xaml.cs` before control initialization. Docs: https://help.syncfusion.com/wpf/wpf-license-registration

## 🧹 Logs & cleanup

Local usage
```powershell
pwsh -File .\PowerShell\bbCleanup.ps1 -LogsDir .\logs -SummaryOut .\logs\log-summary.json
```

## 📊 Features

### Core modules
- 🚌 Vehicles: inventory, maintenance, driver assignments
- 📍 Routes: planning, stop management, student assignment
- 👥 Students: enrollment, profiles, attendance basics
- 📈 Analytics: dashboards and basic reporting

## 📅 Unified Scheduler plan (Sports + Activities)

A consolidated Scheduler surface (SfScheduler) will merge sports and activity scheduling.

References (Syncfusion WPF)
- Getting started: https://help.syncfusion.com/wpf/scheduler/getting-started
- API: https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Scheduler.SfScheduler.html

## 📚 Documentation
- Setup guide: `SETUP-GUIDE.md`
- Architecture overview: `docs/README.md`
- Standards and policies: `Documentation/` and `Standards/`

## 🤝 Contributing

### Development workflow
1. Fork the repo
2. Create a feature branch: `git checkout -b feature/my-change`
3. Follow standards in `Standards/`
4. Commit: `git commit -m "Describe change"`
5. Push and open a PR

### Standards
- Syncfusion‑only UI (no standard WPF controls in new/refactored code)
- Serilog‑only logging (no Microsoft.Extensions.Logging)
- PowerShell 7.5.2 compliance (no Write‑Host)
- .NET 9 target framework

### Code quality
- All tests pass: `bbTest`
- Build and analyzers pass: `bbBuild`
- XAML validation: `bbXamlValidate`

## 📈 Project Status

**Phase 1: COMPLETED ✅** (August 17, 2025) - Foundation Stabilized & Assessed

Phase 1 achieved 100% success in stabilizing the technical foundation. All immediate technical debt has been resolved, and the PowerShell 7.5.2 automation system is fully operational with reliable build processes.

### ✅ Phase 1 Achievements (Completed August 17, 2025)
- **bbHealth -Detailed**: 100% pass rate, comprehensive environment validation
- **bbXamlValidate**: 35.6% compliance (29 violations identified for Phase 2 fix)
- **bbBuild**: Clean builds in 11.6s with only minor version warnings
- **bbTest**: Test suite operational and reliable
- **Write-Host Fixes**: All violations corrected to Write-Information standards
- **PowerShell Array Syntax**: Proper `@()` usage with analyzer suppressions
- **Documentation**: COPILOT-VISION-PROMPT.md created for AI assistant guidance

### 🎯 Phase 2: Core Feature Implementation (In Progress)
**Target**: Complete the 6 MVP modules for full BusBuddy functionality

- [ ] **Student Management Module**: CRUD operations with SfDataGrid, geocoding, validation
- [ ] **Vehicle & Driver Management**: Fleet tracking, maintenance calendars via SfScheduler
- [ ] **Route & Schedule Assignment**: Route builder with SfMap, schedule generation
- [ ] **Activity & Compliance Logging**: Timeline views, compliance reports, audit trails
- [ ] **Dashboard & Navigation**: Central hub with DockingManager, global search
- [ ] **Data & Security Layer**: Complete Azure SQL integration, EF Core repositories

### 🏁 Finish Line Success Criteria
**Functional**: Add 50 students, assign to 5 routes with drivers/vehicles, generate/export schedules – all in <5 minutes without errors
**Technical**: bbHealth 100%, bbBuild/bbTest 90%+ coverage, <2s DB ops, zero compliance violations
**Operational**: Runnable MSI package, <10 minute setup time, comprehensive user documentation

**Recent Performance Optimizations (August 2025):**
- PowerShell profile loading: ~300ms (99% improvement from 15+ seconds)
- Performance monitoring system: Real-time module load metrics with trending
- Lazy Azure module loading: Modules load only when Azure functions are called
- Dynamic repo discovery: Works for any clone location automatically

## 📞 Support

### Getting help
- Documentation in `Documentation/` and `docs/`
- Diagnostics: `bbHealth`
- Issues and discussions via GitHub

### Troubleshooting
```powershell
bbHealth
bbBuild 2>&1 | Tee-Object -FilePath build-output.log
bbXamlValidate
```

## 📄 License

MIT — see `LICENSE`.

---

Built with ❤️ for school transportation professionals

Last updated: August 17, 2025 - Phase 1 Foundation Complete
