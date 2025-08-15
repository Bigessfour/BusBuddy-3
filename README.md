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

# Load PowerShell automation
. .\PowerShell\Profiles\BusBuddyProfile.ps1

# Optional — set Syncfusion license to avoid trial dialogs
$env:SYNCFUSION_LICENSE_KEY = "<your-key>"  # https://help.syncfusion.com/wpf/wpf-license-registration

# Health → Build → Run
bbHealth
bbBuild && bbRun

# Fallback (explicit target when using dotnet directly)
dotnet run --project .\BusBuddy.WPF\BusBuddy.WPF.csproj
```

### What to expect
- WPF desktop app; a dashboard window opens at launch.
- Syncfusion‑only policy for new/refactored UI.
- Prefer bb* commands for build/test/run.

### Dev quickref
```powershell
bbHealth            # Environment and project diagnostics
bbBuild             # Build the solution
bbRun               # Run the WPF app
bbTest              # Run tests (TRX + coverage)
bbAntiRegression    # Scan for disallowed APIs/patterns
bbXamlValidate      # Validate Syncfusion‑only XAML
bbMvpCheck          # Validate core MVP scenarios
```

## 🧪 Testing

VS Code integration: use the Testing view (or the NUnit Test Runner extension) or stick with bb* commands.

```powershell
bbTest                                   # All tests
bbTest --filter "TestCategory=Scheduler"   # Subset example
```

Legacy harness scripts in `PowerShell/Testing` are archived — prefer `bbTest`.

## 🔁 CI

GitHub Actions builds and tests on push/PR. Some jobs are conditional on secrets.

Secrets commonly used
- `SYNCFUSION_LICENSE_KEY`
- `BUSBUDDY_CONNECTION`, `AZURE_SQL_SERVER`, `AZURE_SQL_USER`, `AZURE_SQL_PASSWORD`

## 🛠️ Configuration

### Database setup
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

## 🏗️ Architecture

Tech stack (current)
- .NET SDK: 9.0.303
- WPF + Syncfusion WPF: 30.2.5
- EF Core: 9.0.8
- Serilog: 4.3.0
- NUnit: 4.3.1

Project layout
```
BusBuddy/
├─ BusBuddy.Core/   # Business logic, data access, services
├─ BusBuddy.WPF/    # WPF UI layer with Syncfusion controls
├─ BusBuddy.Tests/  # Unit and integration tests
├─ PowerShell/      # Build/test/run automation (bb* commands)
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
