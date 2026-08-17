# 🚌[![Build Status](https://img.shields.io/badge/build-✅%20passing-brightgreen)](https://github.com/Bigessfour/BusBuddy-3)

[![.NET](https://img.shields.io/badge/.NET-9.0.304-blue)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![PowerShell](https://img.shields.io/badge/PowerShell-7.5.2%20✨%20Advanced-blue)](https://github.com/PowerShell/PowerShell)
[![Syncfusion](https://img.shields.io/badge/Syncfusion-33.2.10%20✅%20Licensed-orange)](https://www.syncfusion.com/wpf-controls)
[![Hyperthreading](https://img.shields.io/badge/Hyperthreading-⚡%20Optimized-green)](https://docs.microsoft.com/en-us/powershell/scripting/learn/experimental-features)
[![MVP Status](https://img.shields.io/badge/MVP-⚠️%20Development-yellow)](https://github.com/Bigessfour/BusBuddy-3)
[![Production](https://img.shields.io/badge/Production-🚧%20In%20Progress-yellow)](https://github.com/Bigessfour/BusBuddy-3)ddy - School Transportation Management System

> **Modern WPF application for comprehensive school bus fleet management, built with .NET 9.0 and Syncfusion controls. Features state-of-the-art PowerShell 7.5.2 automation with hyperthreading optimization.**

[![Build Status](https://img.shields.io/badge/build-✅%20passing-brightgreen)](https://github.com/Bigessfour/BusBuddy-3)
[![.NET](https://img.shields.io/badge/.NET-9.0.304-blue)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![PowerShell](https://img.shields.io/badge/PowerShell-7.5.2-blue)](https://github.com/PowerShell/PowerShell)
[![Syncfusion](https://img.shields.io/badge/Syncfusion-33.2.10%20✅%20Licensed-orange)](https://www.syncfusion.com/wpf-controls)
[![MVP Status](https://img.shields.io/badge/MVP-⚠️%20Development-yellow)](https://github.com/Bigessfour/BusBuddy-3)
[![Production](https://img.shields.io/badge/Production-�%20In%20Progress-yellow)](https://github.com/Bigessfour/BusBuddy-3)

## 🎯 **Project Vision**

BusBuddy streamlines school transportation operations through intelligent route management, vehicle tracking, and student assignment systems. Built with modern .NET technologies, professional-grade UI components, and state-of-the-art PowerShell 7.5.2 automation with machine-specific hyperthreading optimization.

### **Core Features**

- 🚌 **Fleet Management**: Vehicle tracking, maintenance scheduling, driver assignments
- 📍 **Route Optimization**: Google Earth Engine integration for efficient route planning
- 👥 **Student Management**: Student enrollment, route assignments, pickup/dropoff tracking
- 📊 **Analytics Dashboard**: Real-time metrics, performance reports, operational insights
- 🔧 **Maintenance Tracking**: Scheduled maintenance, repair history, compliance monitoring
- ⚡ **Performance Optimization**: Hyperthreading-aware parallel processing and machine-specific tuning

## 🚀 **Quick Start**

### **Prerequisites**

- **Windows 10/11** (for WPF)
- **.NET 9.0 SDK** (9.0.303 or later)
- **PowerShell 7.5.2+** (with state-of-the-art features)
- **Visual Studio Code** (recommended) or Visual Studio 2022

### **Development Environment (PowerShell Deprecated; Mac + Windows VM Support)**

**NOTE (2026)**: The original PowerShell development automation ("dd method" / bb-* commands, hyperthreading profiles, BusBuddy-Development) is **DEPRECATED**. It was created while learning PowerShell. The author now prefers **WSL / Docker / standard dotnet**.

### On MacBook Pro (with Windows 11 VM via Parallels/UTM)
- **Mac side (recommended for .NET Core, tests, services, editing)**: Use VS Code + Dev Containers extension + the `.devcontainer` (Linux .NET 9 container via Docker Desktop).
  - Open folder → "Reopen in Container".
  - `Directory.Build.props` sets `EnableWindowsTargeting` so Mac CLI **and** OmniSharp/C# language service can load `net*-windows` TFMs (NETSDK1100). Passing `-p:EnableWindowsTargeting=true` remains valid and is still used in CI.
  - Run Core tests, use Docker Compose for Postgres (real DB for seeding/EF tests instead of InMemory).
  - WPF UI **cannot** run natively on macOS.
- **Windows 11 VM side (for full WPF app)**:
  - Share the project folder from Mac (UTM directory sharing works great; name it "Shared with Windows" or similar). The source tree is live/bidirectional.
  - In VM: Install .NET 9 SDK (ARM64 if Apple Silicon). The shared folder appears under a drive letter or "Shared with Windows".
  - Build/run the full `BusBuddy.WPF` project normally for UI/debug (or use the helper below from your Mac).
  - Changes sync via shared folder/git.
- **Docker on Mac**: Use `docker compose` (see below) for Postgres and test isolation. Accessible from VM via Mac host IP (run `ipconfig getifaddr en0` on Mac; the script below prints it for you).
- **Keys & secrets**: Loaded from **macOS Passwords** at startup (`LoadApiKeysFromMacPasswords()` + `GcpCredentialBootstrap`). See [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](Documentation/GCP-GEE-SECRETS-AND-AUTH.md) and [AGENTS.md](AGENTS.md).

**To launch the WPF UI from your Mac terminal (the "dotnet run" experience for this hybrid setup):**

```bash
./run-wpf.sh
```

What it does:
- Preflight `dotnet build ... -p:EnableWindowsTargeting=true` on the Mac (fast compile gate; focuses on the WPF app).
- Ensures your UTM "Windows" VM is running (starts it if stopped; re-uses if already open).
- Tries to auto-discover the shared project root *inside the guest* and launch `dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj` detached so the main window appears on the VM desktop.
- If guest automation isn't ready yet (boot/login), prints the exact manual steps (including the robust `utm_run_in_vm.ps1` that lives in your shared tree, handles drive-letter / "Shared with Windows" discovery, shared GEE key, and optional Syncfusion license drop-in).

When you are already inside the VM PowerShell: just run `.\utm_run_in_vm.ps1` from the project root (or any dir — it searches for the sln).

Use standard tools:
- `dotnet build BusBuddy.sln -p:EnableWindowsTargeting=true` (Mac/container preflight)
- `./run-wpf.sh` (Mac) or `dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj` (inside VM)
- Docker for services/tests.

Legacy `bb-*` PowerShell modules were removed ([issue #15](https://github.com/Bigessfour/BusBuddy-3/issues/15)). Keep only minimal scripts under `Scripts/` (e.g. `Validate-Dependencies.ps1`). See [STEADY-STATE-AND-FINISH-ROADMAP.md](STEADY-STATE-AND-FINISH-ROADMAP.md).

### **Google Cloud & Earth Engine (GEE)**

| Item                  | Value                                                                                             |
| --------------------- | ------------------------------------------------------------------------------------------------- |
| Earth Engine project  | `ee-bigessfour`                                                                                   |
| GCP console project   | [new-coursera-490518](https://console.cloud.google.com/iam-admin/iam?project=new-coursera-490518) |
| Service account       | `bus-buddy-gee@ee-bigessfour.iam.gserviceaccount.com`                                             |
| Key file (gitignored) | `keys/bus-buddy-gee-key.json`                                                                     |

**First-time setup:**

```bash
brew install --cask google-cloud-sdk
gcloud auth login
.github/scripts/setup-gcp-gee.sh           # create SA + key + appsettings
.github/scripts/store-gcp-passwords.sh   # macOS Passwords (production auth)
```

On app startup (Mac), Passwords entries load into env; `GcpCredentialBootstrap` materializes the service account JSON and wires `GoogleEarthEngineService` + `IGeoDataService` with a live token.

Full reference: [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](Documentation/GCP-GEE-SECRETS-AND-AUTH.md)

### **CI/CD (solo developer)**

- Branch `feature/<topic>` → PR to `master` → gates **Build & Test** + **Security (CodeQL)** → squash auto-merge
- Local pre-push: `.github/scripts/validate-ci-local.sh`
- Details: [AGENTS.md](AGENTS.md), `.github/copilot-instructions.md`


### **Installation & Setup**

```bash
# Clone the repository
git clone https://github.com/Bigessfour/BusBuddy-3.git
cd BusBuddy-3

dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true
dotnet test BusBuddy.sln -c Release --no-build \
  --filter "Category!=Integration&Category!=InMemoryFlaky"
# Or: .github/scripts/validate-ci-local.sh
```

**✅ Current Status**: Application builds and runs successfully with modern UI.

**Development Environment**: Prefer Mac + Windows VM (or WSL) with plain `dotnet` / Docker. Legacy `bb-*` PowerShell modules are removed ([issue #15](https://github.com/Bigessfour/BusBuddy-3/issues/15)).

**Syncfusion AI Assist**: MCP server @syncfusion/wpf-assistant configured in [`.cursor/mcp.json`](.cursor/mcp.json). Prefix AI prompts with `SyncfusionWPFAssistant ` for accurate WPF + Syncfusion code gen (requires your Syncfusion API key). See .github/copilot-instructions.md and https://help.syncfusion.com/wpf/ai-coding-assistant/overview .

**Syncfusion WPF Agent Skills** (Cursor): Official component skills install locally into `.agents/skills/` (not committed). BusBuddy-specific rules live in [`.cursor/skills/syncfusion-wpf-busbuddy/`](.cursor/skills/syncfusion-wpf-busbuddy/). One-time setup:

```bash
.github/scripts/setup-syncfusion-skills.sh          # all 96 components
.github/scripts/setup-syncfusion-skills.sh minimal  # interactive subset
```

See https://help.syncfusion.com/wpf/skills/component-skills .

### **📋 Current Build Status**

**Application is functional and ready for feature development:**

- ✅ **Build Status**: Clean build with 0 errors
- ✅ **Dependencies**: All packages resolved (.NET 9.0, Syncfusion 30.1.42)
- ✅ **Database**: Entity Framework migrations working with LocalDB
- ✅ **UI Framework**: WPF with Syncfusion controls operational
- ✅ **Development Tools**: `dotnet` CLI + Docker + optional `Scripts/Validate-Dependencies.ps1`
- 🔄 **Active Development**: Ready for feature work

### **Development Setup**

```bash
dotnet build BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj   # Windows
./run-wpf.sh                                           # Mac → UTM
```

## 📊 **Current Status (August 21, 2025)**

### **🎉 Application Status**

- ✅ **Build Status**: Clean build with .NET 9.0 and Syncfusion 30.1.42
- ✅ **Core Application**: WPF application runs successfully
- ✅ **Student Management**: Working student entry and management features
- ✅ **Route Management**: Basic route assignment functionality
- ✅ **Database**: Entity Framework with LocalDB for development
- ✅ **PowerShell Tools**: Development automation commands working
- ✅ **Syncfusion Licensing**: Proper license registration implemented
- ✅ **Log Management**: Centralized logging with consolidated workspace logs
- ✅ **Dependency Management**: Comprehensive dependency health checking and vulnerability scanning

### **🧹 Recent Legacy Cleanup (August 21, 2025)**

- ✅ **Legacy Code Removal**: Purged 13 legacy test files and documentation artifacts
- ✅ **Dead Reference Cleanup**: Removed orphaned references from FETCHABILITY-INDEX.json
- ✅ **Archive Consolidation**: Cleaned up Documentation/Archive/LegacyScripts directory
- ✅ **Root Directory Cleanup**: Removed temporary test files (TestApp.cs, TestConnection.cs, etc.)
- ✅ **Build Artifact Cleanup**: Removed legacy build logs and temporary assembly fixes
- ✅ **Documentation Hygiene**: Removed obsolete tracking files and raw link indexes
- ✅ **Dependency Management Module**: Added comprehensive PowerShell module for package management

### **Recent Progress (August 19-21, 2025)**

- ✅ **Enhanced Syncfusion License Handling**: Improved registration with validation and diagnostics
- ✅ **Centralized Logging**: All workspace logs consolidated into `logs/collected/` directory
- ✅ **PowerShell Profile Formatting**: Applied trunk formatting standards and PSScriptAnalyzer compliance
- ✅ **License Management Helper**: Syncfusion via `SYNCFUSION_LICENSE_KEY` (Passwords / env)
- ✅ **Documentation Updates**: Verified Syncfusion licensing requirements and NuGet package setup
- ✅ **Dependency Health Monitoring**: Prefer `dotnet list package --vulnerable` / Dependabot; optional `Scripts/Validate-Dependencies.ps1`
- ✅ **Codebase Hygiene**: Comprehensive legacy cleanup removing 13 obsolete files and dead references

### **Available Commands**

```bash
# Core development
dotnet build BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj   # Windows VM
./run-wpf.sh                                            # Mac → UTM helper

# Local CI mirror
.github/scripts/validate-ci-local.sh

# Optional Windows dependency / license checks
# pwsh Scripts/Validate-Dependencies.ps1 -ValidateLicense

# Format / lint
trunk check --force
```

## ⚠️ **Development Notes**

### **Current Focus Areas**

- **Feature Development**: Building student and route management functionality
- **UI Polish**: Continuing Syncfusion control implementation for professional look
- **Code Quality**: Maintaining clean architecture and following .NET best practices
- **Database**: Using Entity Framework with LocalDB for development

### **Known Development Items**

- **UI Consistency**: Syncfusion-only controls in Views (MCP config: Syncfusion WPF assistant)
- **Testing**: Unit test coverage being expanded as features are added
- **Documentation**: Keeping docs current with active development

### **For Production Deployment**

- Database migration to production SQL Server
- Environment-specific configuration
- Security review and hardening

### **🔧 Troubleshooting & Diagnostics**

#### **Quick Issue Resolution**

- **Syncfusion License**: Set `SYNCFUSION_LICENSE_KEY` (macOS Passwords or Windows env); optional `Scripts/Validate-Dependencies.ps1 -ValidateLicense`
- **Log Analysis**: All workspace logs consolidated in `logs/collected/` for easy review
- **Code Formatting**: Use `trunk fmt --force [file]` to apply project formatting standards
- **Migration Issues**: `dotnet ef database update --force` (see [Troubleshooting Log](TROUBLESHOOTING-LOG.md))
- **Build Errors**: `dotnet build BusBuddy.sln -p:EnableWindowsTargeting=true` and fix reported diagnostics

#### **Comprehensive Testing**

```bash
dotnet test BusBuddy.sln -c Release --filter "Category!=Integration&Category!=InMemoryFlaky"
dotnet ef migrations list --project BusBuddy.Core
# Optional (Windows): pwsh Scripts/Validate-Dependencies.ps1 -ValidateLicense
```

#### **Common Fixes**

| Issue                             | Quick Fix                                               | Documentation                                                                            |
| --------------------------------- | ------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| **Migration History Out of Sync** | `dotnet ef database update --force`                     | [Troubleshooting Log](TROUBLESHOOTING-LOG.md#migration-history-out-of-sync)              |
| **EF Tools Version Mismatch**     | `dotnet tool update --global dotnet-ef --version 9.0.8` | [Troubleshooting Log](TROUBLESHOOTING-LOG.md#ef-tools-version-mismatch)                  |
| **Table Mapping Errors**          | Update DbContext entity configuration                   | [Troubleshooting Log](TROUBLESHOOTING-LOG.md#table-mapping--entity-configuration-issues) |
| **FK Constraint Violations**      | Validate referential integrity with test script         | [Troubleshooting Log](TROUBLESHOOTING-LOG.md#foreign-key-constraint-violations)          |

📋 **Complete Issue Tracking**: See [TROUBLESHOOTING-LOG.md](TROUBLESHOOTING-LOG.md) for detailed solutions and verification steps.

## 🏗️ **Architecture**

> August 2025 Streamlining: Service interfaces are being migrated into `BusBuddy.Core/Services/Contracts`, experimental XAI placeholders collapsed into archive (`experiments/xai/XAI-ARCHIVE.md`), and centralized PowerShell alias registration introduced (`Register-BusBuddyAliases`).

### **Technology Stack**

| Component        | Technology                  | Version                             |
| ---------------- | --------------------------- | ----------------------------------- |
| **Framework**    | .NET                        | 9.0.303                             |
| **UI Framework** | WPF                         | Built-in                            |
| **UI Controls**  | Syncfusion Essential Studio | 33.2.10 (see Directory.Build.props) |
| **Data Access**  | Entity Framework Core       | 9.0.7                               |
| **Database**     | SQL Server / LocalDB        | Latest                              |
| **Logging**      | Serilog                     | 4.3.0                               |
| **Testing**      | NUnit                       | 4.3.1                               |
| **MVVM**         | CommunityToolkit.MVVM       | 8.3.2                               |

### **Project Structure**

```
BusBuddy/

## 🖥️ High DPI and font sizing (Syncfusion WPF v30.1.42)

- App is PerMonitorV2 DPI-aware via application manifest (`BusBuddy.WPF/app.manifest`).
- Windows-level: we use layout rounding and device pixel snapping on key windows.
- Per-monitor handling: StudentForm and StudentsView override OnDpiChanged to adjust font size and bitmap scaling quality when monitors change.
- Fonts: avoid hardcoded pixel sizes. Prefer inherited FontSize and theme/system resources. Where fixed sizes exist, they scale via window FontSize so they remain legible.
- Bitmaps/icons: prefer vector (glyphs/Segoe MDL2). When bitmaps are used, BitmapScalingMode switches to HighQuality for scale > 1.0.
- Syncfusion notes: ChromelessWindow TitleFontSize inherits window FontSize; no custom scaling needed. Use built-in theming (FluentDark/Light).

Now implemented:
- Manifest: PerMonitorV2 enabled and wired in `BusBuddy.WPF.csproj`.
- Windows: StudentForm and StudentsView set TextOptions and BitmapScalingMode and handle OnDpiChanged.

Rollout plan:
1) Apply the same pattern to the remaining views as you touch them (override OnDpiChanged, set text/bitmap options). 2) Prefer vector assets and inherited FontSize. 3) Avoid hardcoded pixel fonts; bind to window FontSize or theme resources.

Testing guidance:
- Move the window between monitors with different scaling (100% ↔ 150%/200%) and watch text/controls stay crisp. Zoom-level changes should not blur bitmaps or clip text. Use ClearType rendering for text.
├── 🏢 BusBuddy.Core/           # Business logic, data access, services
├── 🎨 BusBuddy.WPF/            # WPF UI layer with Syncfusion controls
├── 🧪 BusBuddy.Tests/          # Unit and integration tests
├── 🔧 PowerShell/              # Build automation and utilities
├── 📚 Documentation/           # Project documentation
└── 📋 Standards/               # Development standards and guidelines
```

### **Design Patterns**

- **MVVM**: Model-View-ViewModel for clean separation of concerns
- **Dependency Injection**: Built-in .NET DI container
- **Repository Pattern**: Data access abstraction
- **Command Pattern**: User actions through ICommand interface

## 🔧 **Development**

### **Build & quality (no bb-\* modules)**

```bash
dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true
dotnet test BusBuddy.sln -c Release --no-build \
  --filter "Category!=Integration&Category!=InMemoryFlaky"
trunk check --force
.github/scripts/validate-ci-local.sh
```

Legacy `bb-*` / profile helpers were removed ([issue #15](https://github.com/Bigessfour/BusBuddy-3/issues/15)).

### **Log Management**

All workspace logs are automatically consolidated into `logs/collected/` directory for easy analysis:

- Application logs (bootstrap, runtime, errors)
- Build artifacts and file lists
- Trunk tool logs
- Test results and coverage reports

### **Building**
```bash
dotnet build BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj
```

### **Testing**

```bash
# Run all tests
dotnet test BusBuddy.sln -c Release \
  --filter "Category!=Integration&Category!=InMemoryFlaky"
```

## 🎨 **UI Design**

BusBuddy uses Syncfusion Essential Studio for WPF to provide a modern, professional user interface:

### **Key UI Components**

- **SfDataGrid**: Advanced data grid with sorting, filtering, and editing
- **DockingManager**: Professional layout management
- **SfChart**: Rich charting and visualization
- **NavigationDrawer**: Modern navigation patterns
- **SfScheduler**: Calendar and scheduling interface

### **Themes**

- **FluentDark**: Modern dark theme
- **FluentLight**: Clean light theme
- **Custom**: BusBuddy-specific styling

## 📊 **Features**

### **Core Modules**

#### **🚌 Vehicle Management**

- Fleet inventory and specifications
- Maintenance scheduling and tracking
- Driver assignments and qualifications
- Fuel consumption monitoring
- Inspection compliance

#### **📍 Route Management**

- Interactive route planning with Google Earth Engine
- Stop optimization and timing
- Student assignment to routes
- Real-time tracking and updates
- Performance analytics

#### **👥 Student Management**

- Student enrollment and profiles
- Route assignments and pickup locations
- Attendance tracking
- Parent communication
- Special needs accommodation

#### **📈 Analytics & Reporting**

- Operational dashboards
- Performance metrics
- Cost analysis
- Compliance reporting
- Predictive maintenance

## 🛠️ **Configuration**

### **Database Setup**

```bash
# Create/update database
dotnet ef database update

# Add new migration
dotnet ef migrations add NewMigrationName
```

### **Environment Variables**

**macOS (recommended):** Store in Passwords app; Name = env var. App loads automatically — see [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](Documentation/GCP-GEE-SECRETS-AND-AUTH.md).

| Variable                               | Purpose                                  |
| -------------------------------------- | ---------------------------------------- |
| `SYNCFUSION_LICENSE_KEY`               | Syncfusion WPF license (required for UI) |
| `XAI_API_KEY` / `GROK_API_KEY`         | Grok / xAI route optimization            |
| `GEE_PROJECT_ID`                       | Earth Engine project (`ee-bigessfour`)   |
| `GEE_SERVICE_ACCOUNT_JSON`             | Service account key JSON (production)    |
| `GOOGLE_APPLICATION_CREDENTIALS`       | Path to SA key file                      |
| `GoogleEarthEngine__ProjectId`         | Config override (set by bootstrap)       |
| `ConnectionStrings__DefaultConnection` | Database connection                      |
| `BUSBUDDY_CONNECTION`                  | Postgres override for Docker profiles    |

**Windows production:** Set `GEE_SERVICE_ACCOUNT_JSON` or `GOOGLE_APPLICATION_CREDENTIALS` as machine env vars.

**Deprecated / invalid:** `GoogleEarthEngine__ApiKey`, project `busbuddy-465000`, PowerShell `bbLicense` / SecretManagement flows — use Passwords + `store-gcp-passwords.sh` instead.

### **🔐 Secrets (macOS Passwords / Windows env)**

Do **not** use SecretManagement / `PowerShell\Modules\*.psm1` (removed with [issue #15](https://github.com/Bigessfour/BusBuddy-3/issues/15)).

| Host | How |
| ---- | --- |
| macOS | Passwords entries (Name = env var); loaded by `LoadApiKeysFromMacPasswords()` |
| Windows | User/machine env vars |

Common keys: `SYNCFUSION_LICENSE_KEY`, `XAI_API_KEY` / `GROK_API_KEY` (optional), `GOOGLE_MAPS_API_KEY` when Maps clients resume. See [AGENTS.md](AGENTS.md) and [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](Documentation/GCP-GEE-SECRETS-AND-AUTH.md).

#### **xAI / Ollama note**

Default app AI path is **local Ollama** (no cloud key). Optional legacy xAI uses `XAI_API_KEY` / `GROK_API_KEY` only when `XAI:Provider=Xai` is configured — never via deleted SecretManagement modules.

### **Syncfusion License Setup**

1. **Get License**: Visit [Syncfusion Community License](https://www.syncfusion.com/products/communitylicense)
2. **Generate Key**: Login to your Syncfusion account and generate license key
3. **Set Environment**: Store as `SYNCFUSION_LICENSE_KEY` (macOS Passwords or Windows env)
4. **Verify**: App starts without Syncfusion trial dialogs on the Windows VM

**Note**: No separate licensing NuGet package required - license registration is built into Syncfusion WPF packages.

### **Development Settings**

Configuration is managed through `appsettings.json` files in each project:

- `BusBuddy.Core/appsettings.json`: Core configuration
- `BusBuddy.WPF/appsettings.json`: UI-specific settings

## 🧪 **Testing** - NUnit

### **Run tests**

```bash
dotnet test BusBuddy.sln -c Release \
  --filter "Category!=Integration&Category!=InMemoryFlaky"
```

Legacy PowerShell testing modules (`bb-test`, `BusBuddy.Testing`) were removed ([issue #15](https://github.com/Bigessfour/BusBuddy-3/issues/15)).

### **Test Structure**

Legacy Phase 3/4 harness scripts have been archived (see `Documentation/Archive/LegacyScripts/INDEX.md`). Current active tests:

- **Unit Tests**: `BusBuddy.Tests/Core/` - Core business logic validation
- **Integration Tests**: `BusBuddy.Tests/Phase3Tests/` - Database and service interactions (rename planned post-MVP)
- **Validation Tests**: `BusBuddy.Tests/ValidationTests/` - Input validation and error handling

### **Test Categories**

| Category        | Filter                 | Description                       |
| --------------- | ---------------------- | --------------------------------- |
| **All**         | No filter              | Complete test suite               |
| **Unit**        | `Category=Unit`        | Core business logic               |
| **Integration** | `Category=Integration` | Database/service interactions     |
| **Validation**  | `Category=Validation`  | Input validation & error handling |
| **Core**        | `TestName~Core`        | BusBuddy.Core project tests       |
| **WPF**         | `TestName~WPF`         | UI and presentation layer         |

### **VS Code Integration**

Legacy Phase 4 tasks removed; use standard NUnit Test Explorer or `bbTest` commands.

- **NUnit Test Runner Extension**: Automatic test discovery and execution
- **PowerShell**: `bbTest` (all) / future `bbTest -Watch` (planned)

### **Advanced Features**

- **Watch Mode**: Monitors `*.cs` and `*.xaml` files for changes, auto-runs tests
- **Detailed Reporting**: Markdown reports with metrics, environment details, and recommendations
- **Category-Based Testing**: Focus on specific test types during development
- **Microsoft Compliance**: PowerShell 7.5.2 standards-compliant automation

### **Coverage**

Test coverage reports are generated in `TestResults/` directory with detailed TRX files and comprehensive markdown reports in `Documentation/Reports/`.

## 📚 **Documentation**

### **For Developers**

- [**System Architecture**](Documentation/PHASE-2-IMPLEMENTATION-PLAN.md)
- [**Development Standards**](Standards/MASTER-STANDARDS.md)
- [**PowerShell Reference**](Documentation/PowerShell-7.5.2-Reference.md)
- [**Phase 4 Status**](Documentation/Phase4-Milestone-Report.md)

### **For AI Assistants**

- [**AI Assistant Guide**](Grok%20Resources/GROK-README.md)
- [**Repository Navigation**](Grok%20Resources/AI-ASSISTANT-GUIDE.md)

## 🤝 **Contributing**

### **Development Workflow**

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Follow the [development standards](Standards/MASTER-STANDARDS.md)
4. Commit changes: `git commit -m 'Add amazing feature'`
5. Push to branch: `git push origin feature/amazing-feature`
6. Open a Pull Request

### **Standards**

- ✅ **Syncfusion controls only** (no standard WPF controls)
- ✅ **Serilog logging** (no Microsoft.Extensions.Logging)
- ✅ **PowerShell 7.5.2 compliance** (no Write-Host)
- ✅ **.NET 9.0 target framework**
- ✅ **MVVM pattern** with proper separation of concerns

### **Code Quality**

- All tests must pass: `dotnet test` (or CI **Build & Test**)
- Build must pass: `dotnet build BusBuddy.sln -p:EnableWindowsTargeting=true`
- Local pre-push: `.github/scripts/validate-ci-local.sh`
- Syncfusion-only XAML (review Views; no `bb-xaml-validate` module)

## 📈 **Project Status**

### **Current Phase**: MVP Development (Historic Phase 4 artifacts archived)

- ✅ **Foundation**: Complete (.NET 9, Syncfusion, EF Core)
- ✅ **Testing Infrastructure**: Operational (NUnit, coverage reporting)
- ✅ **Dev tooling**: `dotnet` / Docker / trunk (legacy `bb-*` modules removed — [issue #15](https://github.com/Bigessfour/BusBuddy-3/issues/15))
- 🟢 **UI Migration**: Syncfusion-only controls across Views
- 🟡 **Student Management**: Core features functional (production hardening in progress)
- 🔄 **Production Readiness**: Requires addressing known risks listed above
- 🎯 **Route Optimization**: Next major milestone

### **Quality Metrics**

- **Build Status**: ✅ Passing (0 errors, warnings documented)
- **Test Coverage**: 75%+ achieved (85% target)
- **Code Quality**: Meets development standards (production review pending)
- **Documentation**: Comprehensive for development (operational docs needed)

## 🌟 **Roadmap**

### **Upcoming Features**

- 🎯 **Student Entry System**: Complete student-to-route assignment
- 📱 **Mobile Companion**: Driver mobile app
- 🔔 **Real-time Notifications**: Parent and administrator alerts
- 🤖 **AI Route Optimization**: Machine learning for route efficiency
- 📊 **Advanced Analytics**: Predictive maintenance and cost optimization

### **Long-term Vision**

- Integration with state transportation reporting systems
- Multi-district support
- IoT device integration for real-time tracking
- Environmental impact tracking and reporting

## 📞 **Support**

### **Getting Help**

- 📚 **Documentation**: Start with this README and linked guides
- 🔧 **Build / CI**: `.github/scripts/validate-ci-local.sh` or `dotnet build` / `dotnet test`
- 🐛 **Issues**: Create GitHub issues for bugs or feature requests
- 💬 **Discussions**: Use GitHub discussions for questions

### **Troubleshooting**

```bash
dotnet build BusBuddy.sln -p:EnableWindowsTargeting=true 2>&1 | tee build-output.log
.github/scripts/validate-ci-local.sh
```

## 📄 **License**

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 **Acknowledgments**

- **Syncfusion**: Professional WPF controls
- **Microsoft**: .NET platform and development tools
- **PowerShell Team**: Advanced automation capabilities
- **NUnit Team**: Comprehensive testing framework

---

**Built with ❤️ for school transportation professionals**

_Last Updated: August 19, 2025 - Enhanced licensing, logging, and PowerShell automation_
