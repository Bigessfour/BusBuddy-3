# 🚌 BusBuddy - School Transportation Management System

> **Modern WPF application for comprehensive school bus fleet management, built with .NET 9.0 and Syncfusion controls.**

[![Syncfusion](https://img.shields.io/badge/Syncfusion-30.2.4%20✅%20Licensed-orange)](https://www.syncfusion.com/wpf-controls)
[![MVP Status](https://img.shields.io/badge/MVP-⚠️%20Development-yellow)](https://github.com/Bigessfour/BusBuddy-3)
[![Production](https://img.shields.io/badge/Production-�%20In%20Progress-yellow)](https://github.com/Bigessfour/BusBuddy-3)

## 🎯 **Project Vision**

BusBuddy streamlines school transportation operations through intelligent route management, vehicle tracking, and student assignment systems. Built with modern .NET technologies and professional-grade UI components.

### **Core Features**
- 🚌 **Fleet Management**: Vehicle tracking, maintenance scheduling, driver assignments
- 📍 **Route Optimization**: Google Earth Engine integration for efficient route planning
- 👥 **Student Management**: Student enrollment, route assignments, pickup/dropoff tracking
- 📊 **Analytics Dashboard**: Real-time metrics, performance reports, operational insights
- 🔧 **Maintenance Tracking**: Scheduled maintenance, repair history, compliance monitoring

## 🚀 **Quick Start**

### **Prerequisites**
- **Windows 10/11** (for WPF)
- **.NET 9.0 SDK** (9.0.303 or later)
- **PowerShell 7.5.2+** (for automation)
- **Visual Studio Code** (recommended) or Visual Studio 2022

### **Installation & Setup**
```bash
# Clone the repository
git clone https://github.com/Bigessfour/BusBuddy-3.git
cd BusBuddy

# Load PowerShell automation
Import-Module .\PowerShell\Modules\BusBuddy\BusBuddy.psm1

# Build and run (MVP development version)
bbBuild && bbRun

# Check for known issues before deployment
bbHealth
```
### **📋 Current Build Status**
**MVP functionality operational with ongoing improvements:**
- ✅ **Build Status**: Clean build with 0 errors (warnings addressed)
- ✅ **Package Integrity**: Version conflicts resolved
- ✅ **Syncfusion License**: Configured and operational
- ✅ **MVP Validation**: Core functionality confirmed working
- ⚠️ **Environment**: .NET 9.0.304, PowerShell 7.5.2 operational
- 🔄 **Production Readiness**: In progress (see Known Risks section)

### **Development Setup**
```powershell
# Complete development environment setup
bbDevSession

# Verify system health
bbHealth

# Run tests
bbTest
```


## 🧪 New: VS Code NUnit Test Runner integration

Use the “NUnit Test Runner” extension for a fast local test UX alongside our bb* commands and CI pipeline.

### Install (once)
- In VS Code, open Extensions and install: “NUnit Test Runner” (Forms.nunit-test-runner)
- Open the Testing view (beaker icon). After a build, click “Refresh” to discover tests in `BusBuddy.Tests`.

### Local usage
- Run all tests: Testing view ▶ or `bbTest`
- Run a subset by category: use Testing view search (e.g., “Scheduler”, “Unit”), or run `bbTest -TestSuite Unit`
- Debug a single test: click “Debug Test” CodeLens above the test method
- Artifacts: TRX files are written under `BusBuddy.Tests/TestResults/`

Notes
- The legacy script `PowerShell/Testing/Run-Phase4-NUnitTests-Modular.ps1` is archived. Prefer `bbTest` (module command) for CLI and the Testing view for interactive runs.
- If tests don’t appear, make sure the solution builds and click “Refresh Tests”.

### Consistent filters (Local ↔ CI)
We use the same NUnit category filters locally and in CI. Quick examples:

```powershell
## 📊 **Current Status (August 8, 2025)**

### **🎉 MVP Core Features Ready**
- ✅ **Build Status**: Clean build (0 errors, warnings documented)
- ✅ **MVP Verification**: `bbMvpCheck` confirms core functionality working

CI snippet (mirrors local behavior and emits TRX):

```yaml
- ✅ **Documentation**: Command references updated and standardized
- ✅ **PowerShell Automation**: 20+ working commands with enterprise-grade tooling
- ⚠️ **Production Status**: See Known Risks section below

### **Recent Achievements (Commit: 29b7dc1)**
- ✅ **PowerShell Refactoring**: Fixed 49 Write-Host violations (5.4% compliance improvement)
- ✅ **Professional Tooling**: Created automated refactoring and analysis tools
- ✅ **Code Quality**: Fixed nullable reference warnings, maintained clean build
- ✅ **Comprehensive Documentation**: Updated all guides and reference materials

bbRun                 # Run application

Optional: to run a specific suite via CI inputs or environment, pass a filter string the same way you do locally (categories like Unit, Integration, Validation, Core, WPF):

```yaml
bbRestore             # Restore packages

bbInfo                # Show module information
bbCommands            # List all available commands

# Quality Assurance
bbXamlValidate        # Validate XAML files
bbAntiRegression      # Run compliance checks
bbMvpCheck            # Verify MVP readiness

# Route Optimization
bbRoutes              # XAI route optimization system
bbRouteDemo           # Demo with sample data
bbRouteStatus         # Check optimization status
```

## 🔁 CI Workflow (Simplified)
The repository uses a streamlined GitHub Actions workflow for .NET 9 WPF:
- Triggers on push/PR to main and via manual dispatch
- Steps: restore → build → test (TRX uploaded) → optional EF migration script/apply → publish WPF artifacts
- Timeout: 15 minutes; secrets masked via `::add-mask::`

Required secrets for optional steps (GitHub → Settings → Secrets and variables → Actions):
- `SYNCFUSION_LICENSE_KEY`
- `BUSBUDDY_CONNECTION`, `AZURE_SQL_SERVER`, `AZURE_SQL_USER`, `AZURE_SQL_PASSWORD`

See Actions tab for run results and artifacts.

#### 🆕 Recent workflow updates (Aug 15, 2025)
- `.github/workflows/db-migrations-seed.yml`
  - Added post‑migration schema validation using `sqlcmd` (checks for `Schedule%` tables). Runs only when `BUSBUDDY_CONNECTION` is set and `ensure-sqlcmd` succeeded.
  - Includes optional dynamic Azure SQL firewall allow/cleanup steps for runners.
- `.github/workflows/production-release.yml`
  - Exposes `SYNCFUSION_LICENSE_KEY` to the job when provided; logs a notice if missing to avoid silent license warnings.
  - Calls the reusable DB job (`db-migrations-seed.yml`) after production build to apply migrations and (optionally) seed.

### 🔐 Syncfusion & Azure SQL Integration

- Syncfusion licensing
  - CI: set `secrets.SYNCFUSION_LICENSE_KEY`. The pipeline exports it to `GITHUB_ENV` so builds avoid license warnings.
  - Runtime: `BusBuddy.WPF/App.xaml.cs` registers the license at startup using `Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(...)` if `SYNCFUSION_LICENSE_KEY` is present. Ensure this environment variable is set on production hosts to prevent trial dialogs. Reference: https://help.syncfusion.com/wpf/wpf-license-registration

- Azure SQL migrations and schema validation
  - Reusable workflow `.github/workflows/db-migrations-seed.yml` generates an idempotent EF script and applies it via `sqlcmd` when Azure SQL secrets are provided.
  - Post‑migration, a schema validation step now checks for expected tables and fails fast if missing. Example snippet:

```yaml
- name: 🧪 Validate database schema
  if: ${{ env.BUSBUDDY_CONNECTION != '' && steps.ensure-sqlcmd.outputs.found == 'True' }}
  shell: pwsh
  run: |
    echo "Validating database schema..."
    sqlcmd -S "${{ env.AZURE_SQL_SERVER }}" -d "BusBuddyDb" -U "${{ env.AZURE_SQL_USER }}" -P "${{ env.AZURE_SQL_PASSWORD }}" -Q "SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'Schedule%';" -h -1 |
      ForEach-Object { if ($_ -eq "0") { Write-Error "No Schedule tables found in database" } else { Write-Host "Schema OK — found $($_) Schedule tables." } }
```

  - Configure these secrets for CI validation: `BUSBUDDY_CONNECTION`, `AZURE_SQL_SERVER`, `AZURE_SQL_USER`, `AZURE_SQL_PASSWORD`. Adjust the table pattern (`Schedule%`) to fit future schema changes.

## ⚠️ **Known Risks & Issues**

### **Database & Migration Concerns**
- **Migration History**: Potential migration history out of sync between environments
- **Seeding Issues**: Database seeding shows "0 records" despite "Already seeded" messages
- **Schema Changes**: Possible regression in UI data binding due to recent schema modifications
- **LocalDB vs Production**: Differences between LocalDB development and production SQL Server behavior

#### ✅ Dynamic Azure SQL Firewall (Non‑static IPs)
If you see "Client IP is not allowed" when connecting to Azure SQL, quickly allow your current public IP and proceed—no static IP required.

Local usage (PowerShell 7.5+):
```powershell
# Prereqs (first time): Az modules + Azure login
Install-Module Az -Scope CurrentUser -Force
Connect-AzAccount

# Allow current IP on the Azure SQL server
pwsh -File .\PowerShell\Networking\Enable-AzureSqlAccess.ps1 `
  -SubscriptionId "57b297a5-44cf-4abc-9ac4-91a5ed147de1" `
  -ResourceGroupName "BusBuddy-RG" `
  -SqlServerName "busbuddy-server-sm2" `
  -RuleName "bb-local-$(Get-Date -Format 'yyyyMMdd-HHmm')"

# Run EF migrations after access is granted
dotnet ef migrations list --project .\BusBuddy.Core --startup-project .\BusBuddy.WPF
dotnet ef database update --project .\BusBuddy.Core --startup-project .\BusBuddy.WPF

# Optional cleanup when finished
pwsh -File .\PowerShell\Networking\Disable-AzureSqlAccess.ps1 `
  -SubscriptionId "57b297a5-44cf-4abc-9ac4-91a5ed147de1" `
  -ResourceGroupName "BusBuddy-RG" `
  -SqlServerName "busbuddy-server-sm2" `
  -RuleName "<the-rule-name-you-used>"
```

CI usage (GitHub Actions): grant runner IP before build/tests and clean up after:
```yaml
- name: Azure login
  uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

- name: Allow runner IP on Azure SQL
  shell: pwsh
  run: |
    $rule = "bb-${{ github.run_id }}-${{ github.job }}"
    pwsh -File .\PowerShell\Networking\Enable-AzureSqlAccess.ps1 `
      -SubscriptionId "${{ secrets.AZURE_SUBSCRIPTION_ID }}" `
      -ResourceGroupName "BusBuddy-RG" `
      -SqlServerName "busbuddy-server-sm2" `
      -RuleName $rule
    echo "RULE_NAME=$rule" >> $env:GITHUB_ENV

# ... build/test steps ...

- name: Remove runner IP rule
  if: always()
  shell: pwsh
  run: |
    if ($env:RULE_NAME) {
      pwsh -File .\PowerShell\Networking\Disable-AzureSqlAccess.ps1 `
        -SubscriptionId "${{ secrets.AZURE_SUBSCRIPTION_ID }}" `
        -ResourceGroupName "BusBuddy-RG" `
        -SqlServerName "busbuddy-server-sm2" `
        -RuleName $env:RULE_NAME
    }
```

References: Microsoft Azure SQL firewall configuration (https://learn.microsoft.com/azure/azure-sql/database/firewall-configure) and Az.Sql cmdlets (https://learn.microsoft.com/powershell/module/az.sql/).

Troubleshooting tip: If you see "Missing Az modules: Az.Resources", run:
`Install-Module Az -Scope CurrentUser -Force` then `Import-Module Az` and retry.

### **UI & Data Binding Risks**
- **Syncfusion Migration**: Ongoing migration from standard WPF to Syncfusion controls may introduce temporary inconsistencies
- **Data Binding**: Schema changes may affect existing MVVM data binding patterns
- **Performance**: Large datasets may impact UI responsiveness during initial load
- **Theme Consistency**: FluentDark/FluentLight theme application may be incomplete across all controls

### **PowerShell & Automation**
- **Module Compliance**: BusBuddy.psm1 has 45% compliance with Microsoft PowerShell standards
- **Write-Host Usage**: 50+ Write-Host violations need remediation for enterprise deployment
- **Command Standardization**: Recent `bb-*` to `bb*` format changes may require workflow adjustments
- **Environment Dependencies**: PowerShell 7.5.2+ requirement may limit deployment environments

### **External Dependencies**
- **Syncfusion Licensing**: Community license limitations in production environments
- **Google Earth Engine**: API rate limits and authentication dependencies
- **Azure SQL**: Network connectivity and firewall configuration requirements
- **Package Versions**: .NET 9.0.304 and associated package dependencies still stabilizing

### **Production Readiness Gaps**
- **Error Handling**: Exception handling patterns need standardization across modules
- **Logging**: Transition from multiple logging frameworks to Serilog-only not fully complete
- **Security**: Production security review and hardening pending
- **Performance Testing**: Load testing with realistic data volumes not yet conducted
- **Backup & Recovery**: Database backup and disaster recovery procedures not implemented

### **CI Workflow — Current Issues (Aug 13, 2025)**
- YAML indentation defects in `.github/workflows/ci.yml` can break parsing (e.g., "Implicit keys need to be on a single line" / "All mapping items must start at the same column").
- `enable-AzPSSession: true` is misplaced at the job level; it must be under the `with:` block of the `azure/login@v2` step.
- One or more steps (e.g., the NuGet cache step) are out‑dented; `- name:` entries must align with other steps under `jobs.<job>.steps`.
- Ensure the SQL firewall cleanup step runs with `if: ${{ always() && secrets.AZURE_SUBSCRIPTION_ID != '' }}` and only when `RULE_NAME` exists.

Quick reference (correct structure):

```yaml
      - name: 🔐 Azure login (for SQL firewall)
        if: ${{ secrets.AZURE_CLIENT_ID != '' && secrets.AZURE_TENANT_ID != '' && secrets.AZURE_SUBSCRIPTION_ID != '' }}
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
          enable-AzPSSession: true

      - name: 📦 Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: |
            ~/.nuget/packages
            ${{ github.workspace }}/**/obj/project.assets.json
            ${{ github.workspace }}/**/obj/*.csproj.nuget.*
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/packages.lock.json') }}
          restore-keys: ${{ runner.os }}-nuget-

      - name: 🔐 Remove runner IP rule (cleanup)
        if: ${{ always() && secrets.AZURE_SUBSCRIPTION_ID != '' }}
        shell: pwsh
        run: |
          if ($env:RULE_NAME) {
            pwsh -File .\PowerShell\Networking\Disable-AzureSqlAccess.ps1 `
              -SubscriptionId "${{ secrets.AZURE_SUBSCRIPTION_ID }}" `
              -ResourceGroupName "BusBuddy-RG" `
              -SqlServerName "busbuddy-server-sm2" `
              -RuleName $env:RULE_NAME
          }
```

Validation tips:
- Keep all `- name:` steps aligned exactly under `steps:`; `uses:`, `run:`, and `with:` are indented beneath each step.
- Place `enable-AzPSSession: true` inside the `with:` block of `azure/login@v2` only.
- Commit and push to a branch to let GitHub’s workflow linter surface YAML errors quickly.

### **Recommended Actions Before Production**
1. **Database Audit**: Verify migration state and seeding integrity across all environments
2. **End-to-End Testing**: Run comprehensive CRUD validation with `.\Test-EndToEndCRUD.ps1`
3. **UI Testing**: Comprehensive testing of all Syncfusion controls with real data
4. **PowerShell Remediation**: Address Write-Host violations and improve module compliance
5. **Security Review**: Conduct security assessment and implement recommended hardening
6. **Performance Testing**: Test with production-scale data volumes

### **🔧 Troubleshooting & Diagnostics**

#### **Quick Issue Resolution**
- **Migration Issues**: `dotnet ef database update --force` (see [Troubleshooting Log](TROUBLESHOOTING-LOG.md))
- **EF Version Sync**: `dotnet tool update --global dotnet-ef --version 9.0.8`
- **FK Constraint Errors**: Run `.\Test-EndToEndCRUD.ps1 -IncludeForeignKeyTests` for validation
- **Seeding Problems**: Check table mapping in DbContext (Bus entity → Vehicles table)

### ✅ Migration Fix (Design-time and Out-of-Sync)
When EF migrations appear out of sync across LocalDB/Azure SQL or EF tooling prompts for a startup project, use the exact commands below.

1) Ensure EF tools are current:

```powershell
dotnet tool update --global dotnet-ef --version 9.0.8
```

2) Target the Core project for migrations and the WPF app as the startup (design-time services):

```powershell
# List migrations
dotnet ef migrations list --project .\BusBuddy.Core --startup-project .\BusBuddy.WPF

# Apply migrations to the configured database
dotnet ef database update --project .\BusBuddy.Core --startup-project .\BusBuddy.WPF -- --no-build
```

3) If updating Azure SQL from CI or a changing IP, first allow the runner/local IP (see Dynamic Azure SQL Firewall above), then run the same commands.

4) Out-of-sync history diagnosis (optional):

```powershell
# Inspect applied migrations table
dotnet ef migrations list --project .\BusBuddy.Core --startup-project .\BusBuddy.WPF -v
```

Notes:
- Bus entity maps to `Vehicles` table; ensure generated migrations include the correct table mappings.
- Use `BUSBUDDY_CONNECTION` env var to override connection at runtime without editing appsettings.

### 🌱 Seed Data Fix (Idempotent Top‑Up + JSON Import)
Seeding now uses an idempotent top‑up strategy to avoid “Already seeded — 0 records” confusion and to prevent unique index violations.

What changed:
- `SeedDataService` tops up Drivers/Buses to target counts instead of bailing when any rows exist.
- Unique Bus fields (BusNumber, VIN, LicenseNumber) are generated deterministically to avoid duplicates.
- Logs show before/after counts and the actual number added.

How to run seeding (Development mode):

```powershell
# Option A: From the WPF app (Development mode)
# The Development menu calls SeedAllAsync() when enabled.

# Option B: Use the console seeder against your DB
dotnet build .\TestDataSeeding\TestDataSeeding.csproj -c Release

# Provide a connection string via environment variable; supports %VAR% and ${VAR} placeholders
$env:BUSBUDDY_CONNECTION = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True"

# Optional: clean Students/Families before seeding (use when dedupe prevents expected inserts)
$env:BUSBUDDY_CLEAN_BEFORE_SEED = "1"

dotnet run --project .\TestDataSeeding\TestDataSeeding.csproj --configuration Release
```

Tips:
- For Azure SQL, expand credentials with environment variables (e.g., ${AZURE_SQL_USER}) and allow your IP first.
- Global HasData seeding can be skipped via `SkipGlobalSeedData` if configured; the top‑up service handles development data.
- After seeding, verify FK integrity with `Test-EndToEndCRUD.ps1 -IncludeForeignKeyTests`.

#### **Comprehensive Testing**
```powershell
# Run end-to-end CRUD validation
.\Test-EndToEndCRUD.ps1 -IncludeForeignKeyTests -GenerateReport

# Check system health
bbHealth

# Validate migration status
dotnet ef migrations list --project BusBuddy.Core
```

#### **Common Fixes**
| Issue | Quick Fix | Documentation |
|-------|-----------|---------------|
| **Migration History Out of Sync** | `dotnet ef database update --force` | [Troubleshooting Log](TROUBLESHOOTING-LOG.md#migration-history-out-of-sync) |
| **EF Tools Version Mismatch** | `dotnet tool update --global dotnet-ef --version 9.0.8` | [Troubleshooting Log](TROUBLESHOOTING-LOG.md#ef-tools-version-mismatch) |
| **Table Mapping Errors** | Update DbContext entity configuration | [Troubleshooting Log](TROUBLESHOOTING-LOG.md#table-mapping--entity-configuration-issues) |
| **FK Constraint Violations** | Validate referential integrity with test script | [Troubleshooting Log](TROUBLESHOOTING-LOG.md#foreign-key-constraint-violations) |

📋 **Complete Issue Tracking**: See [TROUBLESHOOTING-LOG.md](TROUBLESHOOTING-LOG.md) for detailed solutions and verification steps.

## 🏗️ **Architecture**

> August 2025 Streamlining: Service interfaces are being migrated into `BusBuddy.Core/Services/Contracts`, experimental XAI placeholders collapsed into archive (`experiments/xai/XAI-ARCHIVE.md`), and centralized PowerShell alias registration introduced (`Register-BusBuddyAliases`).

### **Technology Stack**
| Component | Technology | Version |
|-----------|------------|---------|
| **Framework** | .NET | 9.0.303 |
| **UI Framework** | WPF | Built-in |
| **UI Controls** | Syncfusion Essential Studio | 30.2.4 |
| **Data Access** | Entity Framework Core | 9.0.7 |
| **Database** | SQL Server / LocalDB | Latest |
| **Logging** | Serilog | 4.3.0 |
| **Testing** | NUnit | 4.3.1 |
| **MVVM** | CommunityToolkit.MVVM | 8.3.2 |

### **Project Structure**
```
BusBuddy/

## 🖥️ High DPI and font sizing (Syncfusion WPF v30.4.42)

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

### **PowerShell Automation**
BusBuddy includes a comprehensive PowerShell module for development tasks:
bbTest                # Execute all tests
bbHealth              # System diagnostics
bb-anti-regression    # Prevent legacy patterns
bbDiagnostic          # Comprehensive system analysis
bb-commands           # List all available commands
```

---

## ⚙️ Build/Run (WPF quickref)

Prefer tasks or explicit project targeting:

```powershell
# Build solution
bbBuild
# Or
(dotnet build .\BusBuddy.sln)

# Run WPF app
bbRun
# Or
(dotnet run --project .\BusBuddy.WPF\BusBuddy.WPF.csproj)
```

If the app exits immediately, check `logs/bootstrap-YYYYMMDD.txt` for bootstrap diagnostics.

## 🔑 Syncfusion licensing (WPF 30.2.4)

Set before running:

```powershell
$env:SYNCFUSION_LICENSE_KEY = "<your-key>"
```

Registration occurs in `App.xaml.cs` before any controls initialize.

## 🧹 Log lifecycle & CI cleanup

What’s included:
- Streaming scans for legacy error logs (capped lines for I/O efficiency)
- Consolidated retention: 7 days (or delete if >10MB and older than 3 days)
- JSON export for dashboards/CI artifacts

Local usage:

```powershell
pwsh -File .\PowerShell\bbCleanup.ps1 -LogsDir .\logs -SummaryOut .\logs\log-summary.json
```

CI example:

```yaml
- name: 🧹 Log cleanup & summary
  shell: pwsh
  run: |
    pwsh -File .\PowerShell\bbCleanup.ps1 -LogsDir .\logs -SummaryOut .\logs\log-summary.json
  continue-on-error: true
- name: 📎 Upload log summary
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: log-summary
    path: logs/log-summary.json
```

## 🧪 Tests quickref

```powershell
# Build once
Invoke-BbBuild

# Run unit tests
(dotnet test .\BusBuddy.sln --no-build)
```

New tests: `BusBuddy.Tests/Logging/LogLifecycleManagerTests.cs` cover empty summaries and age-based cleanup.
dotnet build BusBuddy.sln
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj

# Or use PowerShell automation
bb-build && bb-run
```

### **Testing**
```bash
# Run all tests
dotnet test BusBuddy.sln

# Or use PowerShell
bb-test
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

## �️ Unified Scheduler Plan (Sports + Activities) — Update Aug 13, 2025

Decision:
- Deprecate the separate Route Scheduler concept. No dedicated Route Scheduler view will be built.
- Merge Sports and Activity scheduling into a single scheduler surface using Syncfusion SfScheduler.

Scope and implementation outline:
- Single view: `BusBuddy.WPF/Views/Schedule/UnifiedSchedulerView.xaml` hosting `<syncfusion:SfScheduler .../>`.
- Single ViewModel: `UnifiedSchedulerViewModel` (merges current Activity and Sports scheduler logic). `SportsSchedulerViewModel` will be marked obsolete and removed after migration.
- Data source: Compose a single ItemsSource from `IScheduleRepository` (Schedules) and `IActivityScheduleRepository` (ActivitySchedules), projecting to Scheduler appointments (e.g., types deriving from `Syncfusion.UI.Xaml.Scheduler.ScheduleAppointment`). Existing `ActivityTimelineEvent` can be reused/adapted.
- UX: Category filters (Sports, Activity), optional team/opponent filter, date range, and `SchedulerViewType` toggles (Day/Week/Timeline). Color-coding via theme brushes and an Appointment style selector. Keep theming consistent with existing resource dictionaries.
- Navigation: Redirect existing entry points (e.g., QuickActions ScheduleRoute_Click) to open UnifiedScheduler pre-filtered when needed. Remove/deprecate any references to a Route Scheduler view.

Documentation-first references (Syncfusion WPF):
- Getting started: https://help.syncfusion.com/wpf/scheduler/getting-started
- API reference: https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Scheduler.SfScheduler.html

Migration steps (safe, incremental):
1) Add UnifiedSchedulerView.xaml and UnifiedSchedulerViewModel with read-only listing of combined appointments.
2) Unify/extend `BusBuddyScheduleDataProvider` to produce a single ItemsSource with category metadata and theme-aware styles.
3) Wire navigation (replace route-scheduler hooks; optionally pass initial filters via navigation parameters).
4) Mark `SportsSchedulerViewModel` as `[Obsolete]` and remove legacy scheduler view artifacts once usage is fully migrated.
5) Validate: `bb-xaml-validate`, `bb-build`, and `bb-mvp-check` before commit. Add minimal tests for ViewModel filtering.

Current status check:
- No dedicated Route Scheduler view exists in the repo; a sports-focused ViewModel and activity timeline components are present. This plan consolidates both onto one Scheduler and removes the separate route scheduler path.

## �📊 **Features**

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
- `SYNCFUSION_LICENSE_KEY`: **Community License key** (required for production)
  - **Community License**: Free for individual developers and small teams
  - **Setup**: Set your actual license key from Syncfusion account
  - **Format**: Long alphanumeric string (200+ characters)
  - **Example**: `[System.Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "YOUR_ACTUAL_LICENSE_KEY", "User")`
- `ConnectionStrings__DefaultConnection`: Database connection string
- `GoogleEarthEngine__ApiKey`: Google Earth Engine API key

### **Syncfusion Community License Setup**
1. **Get License**: Visit [Syncfusion Community License](https://www.syncfusion.com/products/communitylicense) 
2. **Generate Key**: Login to your Syncfusion account and generate license key
3. **Set Environment**: Replace placeholder with your actual key (NOT "TRIAL_MODE")
4. **Verify**: Application should start without trial dialogs

### **Development Settings**
Configuration is managed through `appsettings.json` files in each project:
- `BusBuddy.Core/appsettings.json`: Core configuration
- `BusBuddy.WPF/appsettings.json`: UI-specific settings

## 🧪 **Testing** - Advanced NUnit Integration

### **PowerShell Testing Module** ✨ **NEW**
BusBuddy includes a comprehensive testing infrastructure with VS Code NUnit Test Runner integration:

```powershell
# Load advanced testing module
Import-Module ".\PowerShell\Modules\BusBuddy.Testing\BusBuddy.Testing.psd1"

# Quick testing commands
bb-test                    # Run all tests
bb-test -TestSuite Unit    # Run unit tests only
bb-test-watch              # Continuous testing with file monitoring
bb-test-report             # Generate comprehensive markdown report
bb-test-status             # Check current test status
```

### **Test Structure**
Legacy Phase 3/4 harness scripts have been archived (see `Documentation/Archive/LegacyScripts/INDEX.md`). Current active tests:
- **Unit Tests**: `BusBuddy.Tests/Core/` - Core business logic validation
- **Integration Tests**: `BusBuddy.Tests/Phase3Tests/` - Database and service interactions (rename planned post-MVP)
- **Validation Tests**: `BusBuddy.Tests/ValidationTests/` - Input validation and error handling

### **Test Categories**
| Category | Filter | Description |
|----------|--------|-------------|
| **All** | No filter | Complete test suite |
| **Unit** | `Category=Unit` | Core business logic |
| **Integration** | `Category=Integration` | Database/service interactions |
| **Validation** | `Category=Validation` | Input validation & error handling |
| **Core** | `TestName~Core` | BusBuddy.Core project tests |
| **WPF** | `TestName~WPF` | UI and presentation layer |

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

### **Unified Scheduler Tests** — NEW

The Unified Scheduler (merged Sports + Activities) now has dedicated tests to validate data composition and ViewModel behavior.

- Test locations:
  - `BusBuddy.Tests/SchedulerTests/UnifiedSchedulerViewModelTests.cs`
  - `BusBuddy.Tests/SchedulerTests/ScheduleDataProviderTests.cs`

- How to run just these tests:
  - PowerShell (recommended):
    - If your bb-test supports filters: bb-test -Filter "TestCategory=Scheduler"
    - Otherwise, use .NET CLI filter:
      dotnet test "BusBuddy.Tests/BusBuddy.Tests.csproj" -v m --filter TestCategory=Scheduler
  - From VS Code: use the Test Explorer and filter by Category=Scheduler

- What they cover (high level):
  - Merging ActivitySchedule and Schedule items into a single appointment source
  - Mapping of times/location to Syncfusion SfScheduler appointments
  - Data provider range queries and dirty-state tracking for adds/removes

Note: These tests are self-contained and use EF Core InMemory and/or mocked services. They won’t touch your real database.

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
- All tests must pass: `bb-test`
- Code analysis must pass: `bb-build`
- XAML validation: `bb-xaml-validate`
- PowerShell compliance: Follow Microsoft standards

## 📈 **Project Status**

### **Current Phase**: MVP Development (Historic Phase 4 artifacts archived)
- ✅ **Foundation**: Complete (.NET 9, Syncfusion, EF Core)
- ✅ **Testing Infrastructure**: Operational (NUnit, coverage reporting)
- ✅ **PowerShell Automation**: Core functionality stable (compliance improvements needed)
- 🟡 **UI Migration**: Completing Syncfusion control migration (some inconsistencies remain)
- 🟡 **Student Management**: Core features functional (production hardening in progress)
- 🔄 **Production Readiness**: Requires addressing known risks listed above
- 🎯 **Route Optimization**: Next major milestone

### **Quality Metrics**
- **Build Status**: ✅ Passing (0 errors, warnings documented)
- **Test Coverage**: 75%+ achieved (85% target)
- **Code Quality**: Meets development standards (production review pending)
- **Documentation**: Comprehensive for development (operational docs needed)
- **PowerShell Compliance**: 45% (Microsoft standards remediation in progress)

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
- 🔧 **PowerShell**: Use `bbHealth` for system diagnostics
- 🐛 **Issues**: Create GitHub issues for bugs or feature requests
- 💬 **Discussions**: Use GitHub discussions for questions

### **Troubleshooting**
```powershell
# System health check
bbHealth

# Build diagnostics
bb-build 2>&1 | tee build-output.log

# XAML validation
bb-xaml-validate
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

*Last Updated: August 13, 2025*
