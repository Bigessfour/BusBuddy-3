# 🚌 BusBuddy — School Transportation Management System

Modern WPF application for school bus fleet management, built on .NET 9 and Syncfusion WPF controls.

[![Syncfusion](https://img.shields.io/badge/Syncfusion-30.2.5%20✅%20Licensed-orange)](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
[![MVP](https://img.shields.io/badge/MVP-Achieved-brightgreen)](#-project-status)
[![Status](https://img.shields.io/badge/Phase-Production%20Hardening-blue)](#-project-status)

## 🎯 Overview

BusBuddy streamlines school transportation operations with route management, student assignments, and fleet oversight — using professional-grade Syncfusion controls for a modern UI.

Highlights
- 🚌 Fleet management: assignments, maintenance, compliance
- 👥 Student enrollment and route assignment
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

# Load optimized PowerShell profile (fast loading ~400ms)
. .\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1

# Optional — set Syncfusion license to avoid trial dialogs
$env:SYNCFUSION_LICENSE_KEY = "<your-key>"  # https://help.syncfusion.com/wpf/wpf-license-registration

# Health → Build → Run (all commands available immediately)
bbHealth            # Environment diagnostics - Test-BusBuddyHealth
bbBuild             # Build solution - Invoke-BusBuddyBuild  
bbRun               # Launch WPF app - Start-BusBuddyApplication
bbTest              # Run tests - Start-BusBuddyTest

# Enhanced parallel processing commands (NEW)
bbTestParallel      # Run test projects in parallel for faster execution
bbHealth -Detailed  # Detailed health check with system info and parallel dependency checks
bbBuild -MaxCpuCount 12 -TimeoutSeconds 300  # Multi-core build with timeout protection
bbTest -Parallel -MaxCpuCount 12  # Parallel test execution with hyperthreading
bbAntiRegression -ThrottleLimit 12  # 12-thread anti-regression scanning

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
dotnet run --project .\BusBuddy.WPF\BusBuddy.WPF.csproj
```

### PowerShell-first workflow (optimized)

**Performance Improvements:**
- **Profile loading**: ~400ms (was 15+ seconds)
- **Lazy module loading**: Az/SqlServer modules load only when needed
- **Dynamic repo discovery**: Works for any clone location automatically
- **Up-to-date versions**: .NET 9.0.108, Syncfusion WPF 30.2.5
- **CLI integration**: GitHub CLI, Azure CLI, GitKraken CLI support with PowerShell Gallery modules

We now use an optimized PowerShell profile that loads in ~400ms (vs 15+ seconds previously) with lazy module loading and dynamic repo discovery.

**Profile structure:**
```
PowerShell/
├─ Profiles/
│  ├─ Microsoft.PowerShell_profile.ps1  # Optimized wrapper with dynamic repo discovery
│  ├─ BusBuddyProfile.ps1              # Main profile with lazy loading and bb* aliases
│  └─ Import-BusBuddyModule.ps1         # Legacy bootstrap (compatibility)
├─ Modules/
│  ├─ BusBuddy/                         # Core BusBuddy module
│  ├─ BusBuddy.Testing/                 # Testing module
│  └─ BusBuddy.CLI/                     # CLI integration module (GitHub, Azure, GitKraken)
```

**Key optimizations:**
- **Lazy loading**: Az and SqlServer modules load only when Azure functions are called
- **Dynamic discovery**: Automatically finds repo root for any clone location
- **Environment setup**: All variables configured (DOTNET_VERSION, BUILD_CONFIGURATION, etc.)
- **Safe stubs**: Missing modules don't break the environment

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

**Parallel Test Execution (NEW):**
The testing system now supports parallel execution for improved performance:

```powershell
# Standard test execution
bbTest                              # Run all tests sequentially
bbTest -Filter "TestCategory=Core"  # Run filtered tests
bbTest -Coverage                    # Include code coverage

# NEW: Parallel test execution
bbTestParallel                      # Auto-discover and run test projects in parallel
bbTestParallel -ThrottleLimit 4     # Limit to 4 concurrent test projects
bbTestParallel -Coverage            # Parallel execution with coverage
bbTest -Parallel -MaxCpuCount 12    # Enable parallel test runner within projects
```

**Performance Benefits:**
- **Project-level parallelism**: Multiple test projects run simultaneously
- **Test-level parallelism**: Within each project, tests can run in parallel
- **Hyperthreading utilization**: Uses all available CPU cores efficiently
- **Timeout protection**: Long-running tests are automatically terminated
- **Detailed reporting**: Shows duration and status for each test project

VS Code integration: use the Testing view (or the NUnit Test Runner extension) or stick with bb* commands.

```powershell
bbTest                                   # All tests
bbTest --filter "TestCategory=Scheduler"   # Subset example
```

Legacy harness scripts in `PowerShell/Testing` are archived — prefer `bbTest`.

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
# Or explicitly with dotnet
dotnet build .\BusBuddy.sln
dotnet run --project .\BusBuddy.WPF\BusBuddy.WPF.csproj
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

## 📈 Project status

Current phase: MVP achieved; production hardening in progress. Non‑MVP integrations (e.g., XAI, Google Earth Engine) remain deferred.

**Recent Performance Optimizations (August 2025):**
- PowerShell profile loading: ~400ms (97% improvement from 15+ seconds)
- Lazy Azure module loading: Modules load only when Azure functions are called
- Dynamic repo discovery: Works for any clone location automatically
- Updated dependencies: .NET 9.0.108, Syncfusion WPF 30.2.5

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

Last updated: August 15, 2025
