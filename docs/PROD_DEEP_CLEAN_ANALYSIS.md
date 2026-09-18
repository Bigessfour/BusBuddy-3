# BusBuddy-3 Production Deep-Clean Analysis

**Date:** 2026-09-18  
**Scope:** Full-repository analysis (analysis only — no code changes in this pass)  
**Method:** Traced from `BusBuddy.WPF/Program.cs` → `App.xaml.cs` DI → Core `AddDataServices` → views/navigation; cross-checked solution graph, call sites, `check-project-done.py`, and `.function-inventory.json`.

**Companion docs:** [action-items.md](./action-items.md) · [done-checklist.md](./done-checklist.md) · [clerk-path.md](./clerk-path.md) · [Architecture map](../Documentation/diagrams/busbuddy-3-architecture.md)

---

## Executive summary

BusBuddy-3 is a **Syncfusion WPF .NET 9 desktop clerk app** with a **Postgres-first** data layer (`BusBuddy.Core`) and a thick presentation layer (`BusBuddy.WPF`). The solution builds and CI publishes a Windows x64 self-contained artifact. Core clerk hops 1–6 are **proved at the service/DB layer**; optional UI ribbon proof and District Map VM re-smoke remain open.

`python3 .github/scripts/check-project-done.py` currently reports **NOT DONE** (2 failures, 6 deferrals, 26 passes). Primary blockers are **optional UI operational evidence** (B14) and **unchecked action-items boxes** (B10) — not compile-time stubs.

**Top production risks (prioritized):**

| Priority | Risk | Evidence |
|----------|------|----------|
| P0 | God-class shell (`MainWindow.xaml.cs` 1,462 lines) blocks maintainability | Navigation, dock chrome, theme, 15+ window launchers in one file |
| P0 | Sync-over-async on UI thread (`GetAwaiter().GetResult()`) | `App.xaml.cs`, `MainWindow.xaml.cs`, `UserSettingsService`, `DistrictSettingsAccessor` |
| P1 | ~2,200 lines of dead/unwired WPF infrastructure compile but never run | See §1 orphan nominations |
| P1 | Duplicate `BaseEntity` types + `Repository<T>` soft-delete logic that no entity uses | `Models/BaseEntity.cs` vs `Models/Base/BaseEntity.cs`; `BusBuddyDbContext` comment L317 |
| P1 | `AMRoute`/`PMRoute` string columns (~300 refs) vs new FK migration incomplete | `action-items.md` Route FK follow-up |
| P2 | XML docs globally suppressed (`CS1591` in `Directory.Build.props`) | Public API contract documentation not enforced |

---

## 0. Program map

### 0.1 Solution structure

| Project | In `BusBuddy.sln` | Role | Shipped? |
|---------|-------------------|------|----------|
| `BusBuddy.WPF` | Yes (startup) | WinExe UI — Syncfusion WPF, MVVM, maps | **Yes** |
| `BusBuddy.Core` | Yes | Class library — EF Core, services, models, migrations | **Yes** (DLL) |
| `BusBuddy.Tests` | Yes | NUnit unit/integration tests | No |
| `BusBuddy.XamlCompliance.Tests` | Yes | XAML/theme compliance scanner | No |
| `BusBuddy.DbPrep` | **No** | CLI clerk-hop proof harness (`Program.cs` 733 lines) | Dev/CI tool |
| `Tools/SfMapTileProbe` | **No** | Syncfusion map tile isolation probe (OSM allowed) | Dev tool only |
| `.github/scripts/MapsConnectionProbe` | **No** | Maps API connectivity probe | CI/dev tool |

**Solution Items folder:** `Directory.Build.props`, `global.json`, `README.md`

### 0.2 Tech stack

| Layer | Technology | Version / notes |
|-------|------------|-----------------|
| Runtime | .NET | 9.0 (WPF targets `net9.0-windows` + `net8.0-windows`) |
| UI | Syncfusion WPF | 34.2.3 (`Directory.Build.props`) |
| MVVM | CommunityToolkit.Mvvm | 8.4.2 |
| ORM | EF Core | 9.0.20 |
| Primary DB | PostgreSQL (Npgsql) | 9.0.4; Docker `docker-compose.yml` |
| Fallback DB | SQL Server LocalDB, SQLite, InMemory | Config-driven |
| Logging | Serilog | 4.4.0 |
| Mapping (DTO) | AutoMapper | 15.1.3 (`BusBuddy.WPF/Services/MappingService.cs` — **not** geospatial) |
| Maps | Google Maps Platform + Syncfusion SfMap | Address Validation, Places, Routes, Map Tiles |
| Local AI | Ollama (OpenAI-compatible HTTP) | `OllamaAiService`, `OllamaChatService` |
| CI | GitHub Actions `windows-latest` | Build, test, publish win-x64 single-file |

### 0.3 Entry points and startup flow

```
Program.Main [STAThread]
  └─ EntityFrameworkPostgresExtensions.ConfigureNpgsqlAppContext()
  └─ new App()
       ├─ LoadKeysDotEnv()          → keys/.env, macOS Passwords, keys file
       ├─ RegisterSyncfusionLicenseOnce()
       ├─ BuildConfiguration()      → appsettings.json + env
       ├─ Serilog from config
       └─ OnStartup
            ├─ ConfigureServices()  → AddDataServices + WPF services/VMs
            ├─ Background: migrate + SeedDataService.SeedFromJsonAsync()
            └─ CreateMainWindow()   → MainWindow(MainWindowViewModel)
```

**Key files:**

| Concern | Path |
|---------|------|
| Entry | `BusBuddy.WPF/Program.cs` |
| DI / startup | `BusBuddy.WPF/App.xaml.cs` (~1,210 lines) |
| Core DI | `BusBuddy.Core/Extensions/ServiceCollectionExtensions.cs` → `AddDataServices()` |
| Shell | `BusBuddy.WPF/Views/Main/MainWindow.xaml` + `.xaml.cs` |
| DbContext | `BusBuddy.Core/Data/BusBuddyDbContext.cs` (1,473 lines) |
| Connection | `BUSBUDDY_CONNECTION` env → `PostgresConnectionResolver` |

### 0.4 Runtime composition (shipped path)

**Data layer:** `BusBuddyDbContext` → 11 repositories + `UnitOfWork` → domain services.

**Registered domain services (Core `AddDataServices`):**  
`BusService`, `DriverService`, `ActivityService`, `RouteService`, `StudentService`, `DestinationService`, `PickupStopService`, `StudentSchoolTransferService`, `RouteWaypointRebuildService`, `DriverTrainingService`, `RouteDeterminationService`, `FuelService`, `MaintenanceService`, `ScheduleService`, `StudentScheduleService`, `FleetMonitoringService`, `AddressValidationService`, `ActivityLogService`, `DashboardMetricsService`, `TripEventService`, `OperationalReportService`, `PdfReportService` (singleton), Google Maps clients (singleton HTTP), `SeedDataService` (WPF re-registers).

**WPF-only registrations (`App.xaml.cs`):**  
`RouteExportService`, `SkinManagerService`, `OllamaChatService`/`IAiChatService`, `DistrictMapSync`, `DriverAvailabilityService`, `UserSettingsService`, `FuelLocationCatalog`, `TripReasonCatalog`, 16 ViewModels (see below).

**DI-registered ViewModels:**  
`MainWindowViewModel`, `DashboardViewModel`, `ActivityTimelineViewModel`, `ActivityManagementViewModel`, `SettingsViewModel`, `AnalyticsDashboardViewModel`, `FuelManagementViewModel`, `MaintenanceViewModel`, `DriverScheduleViewModel`, `ReportsViewModel`, `StudentsViewModel`, `StudentFormViewModel`, `RouteManagementViewModel`, `DriverFormViewModel`, `DriversViewModel`, `MapViewModel` (singleton).

**Manually constructed (live, not DI):**  
`RouteAssignmentViewModel` (+ 5 partials), `VehicleManagementViewModel`, student dialog VMs (`PickupStopFormViewModel`, `SchoolDestinationFormViewModel`, etc.), fuel dialogs, `TripEventEditDialogViewModel`, `DriverTrainingChecklistViewModel`.

**Main navigation surfaces (ribbon/header in `MainWindow`):**  
Dashboard, District Map, Students, Routes, Route Assignments (dock), Drivers, Buses (via `VehicleFleetLauncher`), Activities, Fuel, Settings, Reports, Maintenance, Analytics, Driver Schedule.

### 0.5 Non-runtime paths (do not ship)

| Area | Purpose |
|------|---------|
| `BusBuddy.Tests/` (115 `.cs` files) | Automated proof |
| `BusBuddy.XamlCompliance.Tests/` | Theme/XAML policy |
| `BusBuddy.DbPrep/` | Headless clerk-hop CLI |
| `Tools/SfMapTileProbe/` | Map tile probe (OSM quarantined) |
| `rag/` | Repo RAG indexer + MCP |
| `Scripts/`, `.github/scripts/` | Dev/CI automation |
| `Documentation/Archive/` | 34 archived `.cs` files + historical docs |
| `specs/`, `.cursor/`, `.specify/` | Spec-Kit, agent skills, constitution |
| `package.json` | Trunk launcher only (`@trunkio/launcher`) |

### 0.6 File counts (product code)

| Project | `.cs` files (approx.) | Largest hotspots |
|---------|----------------------|------------------|
| `BusBuddy.Core` | ~170 | `SeedDataService` 1,782 · `StudentService` 1,712 · `DriverService` 1,591 · `BusBuddyDbContext` 1,473 · `ActivityService` 1,371 · `RouteService` partials ~2,200 |
| `BusBuddy.WPF` | ~200 | `MapViewModel` 1,970 · `MainWindow.xaml.cs` 1,462 · `App.xaml.cs` 1,210 · `StudentsViewModel` 1,099 |

---

## 1. Orphan / non-participating files — nominations for removal

Evidence standard: no reference from solution build, DI, navigation, XAML, or active tests. **Conservative:** items marked **verify** need a `rg`/VM smoke before delete.

### 1.1 Projects and folders outside solution

| Path | Evidence | Risk | Action |
|------|----------|------|--------|
| `BusBuddy.DbPrep/` | Not in `BusBuddy.sln`; used manually for clerk hops (`docs/clerk-path.md`) | **verify** | **Keep** as dev tool; optionally add to solution as non-shipping project |
| `Tools/SfMapTileProbe/` | Explicitly quarantined in `done-catalog.json` (`osm-probe-only`) | **safe** (for product) | **Keep** in repo; never reference from WPF |
| `.github/scripts/MapsConnectionProbe/` | CI/dev probe only | **safe** | **Keep** |
| `Documentation/Archive/**/*.cs` (34 files) | Not compiled; historical reference | **safe** | **Archive** — already isolated; consider git-tag-only |
| `Documentation/Archive/WileySeeder.csproj` | Orphan csproj | **safe** | **Delete** or move fully under Archive |

### 1.2 WPF — compile but never wired

| Path | Evidence | Risk | Action |
|------|----------|------|--------|
| `BusBuddy.WPF/ViewModels/LoadingViewModel.cs` | Zero external references | **safe** | **Delete** |
| `BusBuddy.WPF/ViewModels/QuickActionsViewModel.cs` | Zero hosts; spec 009 removed `QuickActionsDialog` | **safe** | **Delete** |
| `BusBuddy.WPF/ViewModels/Dashboard/DashboardAnalyticsViewModel.cs` | Superseded by `DashboardViewModel` / `AnalyticsDashboardViewModel` | **verify** | **Delete** after `rg` confirms |
| `BusBuddy.WPF/ViewModels/Panels/PanelViewModel.cs` | Abstract base; no subclasses | **safe** | **Delete** |
| `BusBuddy.WPF/ViewModels/ActivityScheduleViewModel.cs` | Only self-reference; live path uses inline VM in `ActivityScheduleEditDialog.xaml.cs` | **verify** | **Delete** or wire to dialog |
| `BusBuddy.WPF/ViewModels/Bus/BusManagementViewModel.cs` | Never constructed; fleet uses `VehicleManagementViewModel`; kept per `specs/009-dialog-mvvm-inputs/spec.md` | **verify** | **Archive/delete** after spec amendment |
| `BusBuddy.WPF/Views/Bus/BusEditDialog.*` | Only reachable via unused `BusManagementViewModel` | **verify** | **Delete** with BusManagement VM |
| `BusBuddy.WPF/Controls/QuickActionsPanel.*` | Not in any XAML | **safe** | **Delete** |
| `BusBuddy.WPF/Controls/StudentStatisticsPanel.*` | Not in any XAML | **safe** | **Delete** |
| `BusBuddy.WPF/Services/DevelopmentModeService.cs` | Not in DI | **safe** | **Delete** |
| `BusBuddy.WPF/Services/StartupPreloadService.cs` + `IStartupPreloadService` | Not in DI | **safe** | **Delete** |
| `BusBuddy.WPF/Services/LazyViewModelService.cs` + `ILazyViewModelService` | Not in DI | **safe** | **Delete** |
| `BusBuddy.WPF/Services/Schedule/BusBuddyScheduleDataProvider.cs` | Not in DI; Syncfusion scheduler uses inline data | **verify** | **Delete** or wire to `SfScheduler` |
| `BusBuddy.WPF/Extensions/ServiceCollectionExtensions.cs` | `AddUILogging()` never called | **safe** | **Delete** or wire |
| `BusBuddy.WPF/Utilities/BackgroundTaskManager.cs` | Explicit stub; zero callers | **safe** | **Delete** |
| `BusBuddy.WPF/Utilities/PerformanceOptimizer.cs` | Zero callers | **safe** | **Delete** |
| `BusBuddy.WPF/Utilities/StartupPerformanceMonitor.cs` | Zero callers | **safe** | **Delete** |
| `BusBuddy.WPF/Utilities/XamlErrorHandler.cs` | Zero callers | **safe** | **Delete** |
| `BusBuddy.WPF/Utilities/FileAccessHelper.cs` | Zero callers | **verify** | **Delete** |
| `BusBuddy.WPF/Utilities/WpfGridManager.cs`, `WpfLayoutManager.cs` | Zero callers | **safe** | **Delete** |
| `BusBuddy.WPF/Utilities/ResourceDictionaryDuplicateKeyResolver.cs` | Zero callers | **safe** | **Delete** |
| `BusBuddy.WPF/Logging/CondensedLogFormatter.cs`, `UIPerformanceLogger.cs` | Not in Serilog pipeline | **verify** | **Delete** or wire |
| `BusBuddy.WPF/Models/ChatMessage.cs` | Not used by `OllamaChatService` | **safe** | **Delete** |
| `BusBuddy.WPF/Models/Activity/ActivityItem.cs` | Zero references | **safe** | **Delete** |
| `BusBuddy.WPF/Models/DataIntegrityReport.cs`, `DataIntegrityIssue.cs` | Legacy; live integrity service archived | **safe** | **Delete** |
| `BusBuddy.WPF/Models/BusViewModel.cs` | Duplicate of `ViewModels/Bus/BusViewModel.cs`; AutoMapper uses `Models.BusViewModel` | **verify** | **Consolidate** to one type |
| `BusBuddy.WPF/Resources/Styles/SyncfusionOverrides.xaml` | Marked DEPRECATED; not merged in `App.xaml` | **safe** | **Delete** |
| `BusBuddy.WPF/Resources/Themes/CustomBrushes.xaml` | Not merged in `App.xaml` | **verify** | **Delete** if themes cover brushes |
| `BusBuddy.WPF/Converters/BooleanConverters.cs`, `NullToBoolConverter.cs`, `UserMsgBgConverter.cs`, `InverseBooleanConverter.cs` | Not registered in `App.xaml` resources | **verify** | **Delete** unused converters |
| `BusBuddy.WPF/ViewModels/Activity/DesignTime/ActivityScheduleDesignViewModel.cs` | Blend design-time only | **safe** | **Keep** (design-time) |

### 1.3 Core — compile but not in runtime path

| Path | Evidence | Risk | Action |
|------|----------|------|--------|
| `BusBuddy.Core/Services/BusBuddyAIReportingService.cs` (484 lines) | Zero references outside file | **safe** | **Delete** |
| `BusBuddy.Core/Utilities/DatabaseResilienceService.cs` (264 lines) | Never instantiated; superseded by `ResilientDbExecution` | **safe** | **Delete** |
| `BusBuddy.Core/Services/BusBuddyScheduleDataProvider.cs` (3 lines) | Empty stub; real provider in WPF | **safe** | **Delete** |
| `BusBuddy.Core/Services/IBusRepository.cs` | Empty duplicate of `Data/Interfaces/IBusRepository.cs` | **safe** | **Delete** |
| `BusBuddy.Core/Services/ServiceContainer.cs` | Legacy stub; superseded by `ServiceCollectionExtensions` | **verify** | **Delete** |
| `BusBuddy.Core/Services/FamilyService.cs`, `GuardianService.cs` | Implemented + tested; **not in DI**; deferred in `done-catalog.json` | **risky** | **Defer** until Spec-Kit feature or drop migration |
| `BusBuddy.Core/Models/BaseEntity.cs` | `Repository<T>` references `Models.BaseEntity`; no entity inherits it | **verify** | **Consolidate** with `Models/Base/BaseEntity.cs` or delete |
| `BusBuddy.Core/Models/Base/BaseEntity.cs` | Only `UnitOfWork` audit hook; entities use own audit fields | **verify** | **Consolidate** or delete unused inheritance path |
| `BusBuddy.Core/Models/Vehicle.cs` | Deprecated — merged into `Bus` | **verify** | **Delete** if no references |
| `BusBuddy.Core/Data/enhanced-realworld-data.json`, `sample-realworld-data.json` | JSON seeding disabled in `App.xaml.cs` L564–565 | **verify** | **Archive** or delete |
| `BusBuddy.Core/Services/Contracts/IActivityLogService.cs` | Duplicate of `Services/IActivityLogService.cs` | **verify** | **Merge** namespaces |

### 1.4 Tests — disabled / legacy

| Path | Evidence | Risk | Action |
|------|----------|------|--------|
| `BusBuddy.Tests/WileyTests.cs` | Wrapped in `#if false` | **safe** | **Delete** |
| `BusBuddy.Tests/StudentsViewModelTests.cs` | `#if EXCLUDE_UI_DUPLICATE` block | **verify** | **Delete** duplicate block |

### 1.5 Root / docs / scripts (non-product)

| Path | Evidence | Risk | Action |
|------|----------|------|--------|
| `STEADY-STATE-AND-FINISH-ROADMAP.md` | Self-described stub; content in Archive | **safe** | **Keep** as pointer or delete |
| `appsettings.json` (repo root) | Duplicate of project appsettings; not copied by WPF publish | **verify** | **Delete** or document as template only |
| `BusBuddy.WPF/Documentation/*.pdf` | TWN CICD checklist PDF in product tree | **verify** | **Move** to `Documentation/` |
| `Scripts/map-sfmaps-osm-probe.ps1` | OSM probe script | **safe** (quarantined) | **Keep** with warning comment |
| `.function-inventory.json` + `docs/function-inventory.generated.md` | Agent inventory; not runtime | **safe** | **Keep** |

### 1.6 Reachable but redundant (not orphans — document for consolidation)

| Path | Notes | Action |
|------|-------|--------|
| `Views/Driver/DriverManagementView` | Used from `DashboardView`; `MainWindow` uses `DriversView` | **Consolidate** driver entry points post-ship |
| `Views/Vehicle/VehiclesView.xaml` | Minimal wrapper; fleet CRUD in `VehicleManagementView` | **verify** usage |
| `AddressService` (Core) | `new AddressService()` in `StudentsViewModel` — not DI | **Register in DI** or fold into `AddressValidationService` |

---

## 2. Multi-responsibility files — nominations for dissection

Design goal stated: **one cohesive task per file**. Below: files that clearly violate that today.

### 2.1 WPF presentation layer

| Path | Lines | Current responsibilities | Suggested split |
|------|------:|-------------------------|-----------------|
| `Views/Main/MainWindow.xaml.cs` | 1,462 | Ribbon navigation (15+ windows), dock pane hosting, theme application, dashboard-on-startup, student/bus/driver dock actions, route export triggers, global exception hooks, eligibility PDF delegation, `UiProofLog` instrumentation | `MainWindowNavigation.cs`, `MainWindowDockHost.cs`, `MainWindowTheme.cs`, `MainWindowClerkActions.cs` (partial class or dedicated launcher services per domain) |
| `App.xaml.cs` | 1,210 | Key loading (Passwords, keys/.env), Serilog bootstrap, Syncfusion license, full DI graph, DB migrate/seed background task, CLI report/optimize handlers, fallback DI, `CreateMainWindow` | `AppCompositionRoot.cs`, `AppSecretsLoader.cs`, `AppDatabaseBootstrap.cs`, `AppCliHandlers.cs` |
| `ViewModels/Map/MapViewModel.cs` | 1,970 | Tile session bootstrap, marker layers (schools/students/stops/routes), selection sync, route path rendering, export/print, district bbox, generation result overlay, eligibility PDF | `MapTileCoordinator`, `MapMarkerCoordinator`, `MapRouteLayerCoordinator`, `MapExportCoordinator` (mirror Student coordinator pattern) |
| `ViewModels/Student/StudentsViewModel.cs` | 1,099 | Grid binding, 10+ command surfaces, filter/sort, weak messenger refresh, CSV, AI optimize, map launch — **already partially delegated to coordinators** | Finish extraction: move remaining commands to coordinators; target &lt;400 lines orchestrator |
| `ViewModels/Route/RouteAssignmentViewModel.*` | ~2,300 (6 partials) | Loading, commands, schedule, reports, generation — **good partial pattern**; `RouteAssignmentViewModel.Commands.cs` still 926 lines | Split commands by concern: `AssignmentCommands`, `ScheduleCommands`, `ReportCommands` |
| `Views/Map/MapView.xaml.cs` | 656 | SfMap event wiring, tile health logging, marker assignment, bootstrap lifecycle | `MapViewBootstrap.cs`, `MapViewMarkers.cs` |
| `Views/Student/StudentForm.xaml` + `.xaml.cs` | 817 + 641 | Large form layout + validation UI + map pin launch + field error styling | XAML: user controls per section (Identity, Address, School, Route, Special Needs) |
| `Utilities/DebugOutputFilter.cs` | 1,162 | Debug trace filtering + sync-over-async test hooks | Split filter rules from async test utilities |

### 2.2 Core business layer

| Path | Lines | Current responsibilities | Suggested split |
|------|------:|-------------------------|-----------------|
| `Services/StudentService.cs` | 1,712 | CRUD, geocoding, route assignment, validation, deletion audit, CSV paths, eligibility | `StudentPersistenceService`, `StudentRouteAssignmentService`, `StudentValidationService`, `StudentDeletionService` |
| `Services/DriverService.cs` | 1,591 | CRUD, route assignment, training, caching, schedule links | `DriverPersistenceService`, `DriverRouteAssignmentService` |
| `Services/RouteService.*` | ~2,200 (4 partials) | CRUD, stops, assignments, waypoints — partials exist but `Assignments.cs` is 843 lines | Extract `RouteStudentAssignmentService`, `RouteVehicleAssignmentService` |
| `Services/SeedDataService.cs` | 1,782 | JSON seed, CSV import, demo geo, map demo pins | `JsonSeedImporter`, `CsvRosterImporter`, `MapDemoGeoSeeder` |
| `Services/ActivityService.cs` | 1,371 | Activity CRUD, scheduling, PDF reports, vehicle/driver links | Split PDF/report builders (already partially extracted to `RouteSummary*`) |
| `Data/BusBuddyDbContext.cs` | 1,473 | Provider config, fluent mappings, seed data, save hooks, UTC converters, audit | `BusBuddyDbContext.Configure.cs` (mappings), `BusBuddyDbContext.SaveChanges.cs` (hooks) |
| `Data/Repositories/Repository.cs` | 710 | Generic CRUD + soft-delete branches for unused `BaseEntity` | Simplify after BaseEntity consolidation |
| `Services/RouteDetermination/RouteDeterminationService.cs` | 1,109 | Density packing, fitness, assign, clerk override | Already has helper classes; extract `RouteGenerationOrchestrator` vs `RouteAssignmentOrchestrator` |

### 2.3 Dependency notes for splits

- **Do not split `MainWindow` / `StudentsViewModel` casually** — explicit warning in `docs/action-items.md`. Any split needs incremental PRs with VM smoke (`Scripts/utm-wpf-test.sh`).
- **MapViewModel is singleton** — extracted coordinators should remain stateless or receive state via constructor; avoid new scoped service capture.
- **RouteService partials** are the established pattern — prefer new partial files over new service types unless crossing bounded contexts.

---

## 3. Dead code

### 3.1 Unused types / methods (high confidence)

| Symbol | Location | Evidence |
|--------|----------|----------|
| `BusBuddyAIReportingService` | `Core/Services/BusBuddyAIReportingService.cs` | `rg` — zero call sites |
| `DatabaseResilienceService` | `Core/Utilities/DatabaseResilienceService.cs` | Never constructed; `ResilientDbExecution` used instead |
| `BackgroundTaskManager` | `WPF/Utilities/BackgroundTaskManager.cs` | Stub with log-only methods; zero callers |
| `LoadingViewModel`, `QuickActionsViewModel` | WPF ViewModels | Zero references |
| `BusBuddyScheduleDataProvider` (Core) | 3-line empty file | Superseded by WPF copy (also unwired) |
| `ServiceContainer` | `Core/Services/ServiceContainer.cs` | Only self-reference; DI uses `ServiceCollectionExtensions` |
| `IBusRepository.cs` (empty) | `Core/Services/IBusRepository.cs` | Duplicate empty file |
| `#if false` block | `BusBuddy.Tests/WileyTests.cs` | Entire file disabled |

### 3.2 Registered but unused at runtime

| Symbol | Evidence |
|--------|----------|
| `IStudentScheduleService` / `StudentScheduleService` | In DI (`AddDataServices` L99); no WPF ViewModel references found |
| `IFamilyService` / `IGuardianService` | Not in DI; deferred dead tables (`done-catalog.json`) |
| `IEnhancedCachingService` / `IBusCachingService` | Registered; verify actual cache hits in hot paths |
| `SeedDataService.SeedFromJsonAsync` on startup | Runs in background `Task.Run` in `App.xaml.cs` L543–575; may no-op on populated DB |

### 3.3 Unreachable / legacy branches

| Location | Evidence |
|----------|----------|
| `BusBuddyDbContext` commented `JsonDataImporter` calls | L141, L231, L269 — JSON seed path disabled |
| `App.xaml.cs` fallback DI | L581–598 — minimal provider if full DI fails; most VMs null |
| `Repository<T>` soft-delete filters | Entities don't inherit `BaseEntity`; filters never apply (`BusBuddyDbContext` L317 comment) |
| `WileyTests.cs`, `StudentsViewModelTests.cs` `#if` blocks | Compile-out legacy UI duplicate tests |

### 3.4 Commented-out / obsolete feature flags

| Pattern | Location | Notes |
|---------|----------|-------|
| `#if DEBUG` test hooks | `StudentService`, `DriverService`, `ActivityService`, `ActivityScheduleService`, interfaces | Debug-only API surface on interfaces — verify not called in Release |
| `IsLiveTrackingEnabled` | Removed from product per tests (`MapViewTests`, `MapViewModelTests`) | Correct per `specs/maps.md` |
| `Obsolete` | `Documentation/Archive/.../JsonDataImporter.cs` only | Archive only |

### 3.5 Duplicate / conflicting types

| Issue | Files | Impact |
|-------|-------|--------|
| Two `BaseEntity` classes | `Models/BaseEntity.cs`, `Models/Base/BaseEntity.cs` | `Repository<T>` vs `UnitOfWork` use different types; soft-delete dead |
| Two `IActivityLogService` | `Services/IActivityLogService.cs`, `Services/Contracts/IActivityLogService.cs` | Namespace confusion |
| Two `BusViewModel` | `WPF/Models/BusViewModel.cs`, `WPF/ViewModels/Bus/BusViewModel.cs` | AutoMapper uses Models version |
| `Vehicle` model vs `Bus` entity | `Models/Vehicle.cs` deprecated | Schema uses `Vehicles` table for `Bus` |

### 3.6 Dead XAML resources

| Resource | Evidence |
|----------|----------|
| `SyncfusionOverrides.xaml` | Not merged in `App.xaml` |
| `CustomBrushes.xaml` | Not merged in `App.xaml` |
| Unused converters (see §1.2) | Not in resource dictionaries |

### 3.7 Event handlers — NOT dead

`check-project-done.py` B04 passes: every `*_Click` in WPF code-behind is referenced from XAML. Orphan **views** also pass B03 (including `DriverManagementView` via `DashboardView`).

---

## 4. Documentation gaps, stubs, and underdeveloped methods

Grouped by production priority. **Note:** `CS1591` (missing XML docs) is globally suppressed in `Directory.Build.props` L66 — public APIs are **not** enforced despite `GenerateDocumentationFile=true`.

### 4.1 Blocks production (P0)

| Item | Location | Notes |
|------|----------|-------|
| UI operational proof open | `docs/action-items.md` | B14 failure: optional Hop 1–3 UI clicks + District Map VM re-smoke |
| Unchecked action-items | `docs/action-items.md` | B10: route schedule smoke, map toolbar live proof, Route FK string migration |
| `check-project-done.py` exit 1 | `.github/scripts/check-project-done.py` | 2 failures — project formally "not done" |

### 4.2 Should fix before release (P1)

| Item | Location | Notes |
|------|----------|-------|
| `AddressService` not in DI | `StudentsViewModel.cs` L66, L97; `StudentsView.xaml.cs` L66 | Bypasses validation pipeline; `new AddressService()` |
| `AMRoute`/`PMRoute` string columns | ~300 refs / ~56 files | FK migration phase 1 done; string drop remains (`action-items.md`) |
| `RouteAssignments` table unused | `done-catalog.json` `route-assignments-table` | Duplicate write path deferred |
| Dead DbSets: `SchoolCalendar`, `AIInsight`, `Families`, `Guardians` | `done-catalog.json` deferrals | Schema present; no clerk UI |
| XML documentation absent on public APIs | All Core/WPF public members | Suppressed CS1591 — re-enable for `BusBuddy.Core/Services/I*.cs` first |
| Sync-over-async on startup | `App.xaml.cs` L613, `MainWindow.xaml.cs` L179, `UserSettingsService` L237–239, `DistrictSettingsAccessor` L143–154 | UI-thread block risk at startup/settings load |
| `App.xaml.cs` CLI handlers use `Task.Run(...).GetAwaiter().GetResult()` | L936, L946, L1045 | Report/optimize from command line |
| Duplicate driver entry (`DriversView` vs `DriverManagementView`) | MainWindow vs Dashboard | Clerk confusion risk |
| `BusManagementViewModel` / `BusEditDialog` | Intentionally kept per spec 009 | Dead path alongside live `VehicleManagementViewModel` |

### 4.3 Nice-to-have (P2)

| Item | Location | Notes |
|------|----------|-------|
| Serilog enrichers not centralized | `BusBuddy.WPF.csproj` L128 comment | `Serilog.Enrichers.*` versions not in `Directory.Build.props` |
| `STEADY-STATE-AND-FINISH-ROADMAP.md` stub | Root | Points to archive |
| `BusBuddy.WPF/Documentation/` PDF | In product tree | Move to `Documentation/` |
| `ActivityScheduleViewModel` vs inline dialog VM | Split brain | Consolidate |
| DEBUG-only interface methods | `IStudentService`, `IDriverService`, `IActivityService`, `IActivityScheduleService` | Remove from public contracts or guard with `[Conditional("DEBUG")]` |
| Places Autocomplete (US4) | `specs/007-maps-platform-geo/plan.md` | Deferred spec item |
| Live fleet GPS | `specs/maps.md` | Explicitly deferred — do not enable |
| a11y Phase 2 | `done-checklist.md` K10 N/A | Out of scope |

### 4.4 Stubs / NotImplemented status

**Current state (verified):** B01 passes — **zero** `NotImplementedException` in `BusBuddy.Core` or `BusBuddy.WPF` product code. Historical stubs in `BusService` were removed 2026-09-15 per `docs/instructor-packet/01-status.md`.

**Remaining stub-class (explicit):**

- `BackgroundTaskManager` — documents itself as "stub implementation for compilation compatibility"

### 4.5 TODO/FIXME/HACK status

**B02 passes** — zero `TODO`/`FIXME`/`HACK` in Core/WPF product `.cs` files. Remaining hits are in Archive, agent skills, or csproj comments (`BusBuddy.WPF.csproj` L128 package centralization note).

---

## 5. Performance and prod-readiness task backlog

### P0 — Must address for production confidence

| ID | Task | Rationale | Files / verification |
|----|------|-----------|----------------------|
| P0-1 | Complete District Map VM re-smoke | B14 blocker; map is clerk hop 7 | `docs/action-items.md`; `Scripts/run-map-diag-smoke.cmd` + `score-map-logs.ps1` on Windows VM |
| P0-2 | Eliminate sync-over-async on UI thread at startup | Deadlock risk; blocks UI during settings load | `App.xaml.cs:613`, `MainWindow.xaml.cs:179`, `UserSettingsService.cs:237-239` |
| P0-3 | Add structured error surface for DI bootstrap failure | Fallback DI creates shell with null VMs | `App.xaml.cs:577-598` — fail fast with user dialog vs silent degrade |
| P0-4 | Verify Release publish artifact on clean Windows VM | CI publishes single-file win-x64 | `.github/workflows/ci.yml` `release-artifacts` job; smoke `deployment-package` artifact |
| P0-5 | Secrets audit on operator machines | Keys via env/Passwords — not in repo (B08 passes) | Confirm `GOOGLE_MAPS_API_KEY`, `SYNCFUSION_LICENSE_KEY` documented in `README.md`; rotate if ever committed |

### P1 — Should fix before release

| ID | Task | Rationale | Files / verification |
|----|------|-----------|----------------------|
| P1-1 | Delete confirmed dead code (~1,500+ lines) | Compile time, confusion, review noise | §1.2, §1.3 nominations marked **safe** |
| P1-2 | Register `AddressService` in DI or merge into `AddressValidationService` | Consistent validation path for clerk intake | `StudentsViewModel`, `StudentsView.xaml.cs` |
| P1-3 | Consolidate duplicate `BaseEntity` + simplify `Repository<T>` | Dead soft-delete branches; audit hook confusion | `Models/BaseEntity.cs`, `Models/Base/BaseEntity.cs`, `Repository.cs` |
| P1-4 | Route FK phase 2: drop `AMRoute`/`PMRoute` strings | ~300 refs; dual-write drift risk | `action-items.md` Route FK follow-up |
| P1-5 | Split `MainWindow.xaml.cs` navigation into launcher services | 1,462-line god file; post-ship incremental | One domain per PR with `utm-wpf-test.sh` |
| P1-6 | Split `MapViewModel` using coordinator pattern | 1,970 lines; map regressions expensive | Mirror `Students*Coordinator` pattern |
| P1-7 | Audit N+1 in hot services | `ActivityService` has 56 `.Include()` calls; others vary | Profile Students grid load, Route assignment load, Dashboard metrics |
| P1-8 | HttpClient factory for Google Maps clients | Multiple `new HttpClient()` with `ownsHttpClient: true` | `ServiceCollectionExtensions.cs:111-161` — use `IHttpClientFactory` |
| P1-9 | Re-enable nullable warnings as errors (staged) | `Directory.Build.props` L54-55 commented out | Start with `BusBuddy.Core/Services/Interfaces` |
| P1-10 | Re-enable CS1591 for public service interfaces | Prod requires documented contracts per user mandate | Remove CS1591 from NoWarn; document `IStudentService`, `IRouteService`, etc. |
| P1-11 | Decision: Family/Guardian tables | Dead schema or future feature | `done-catalog.json` — wire DI or migration to drop |
| P1-12 | Decision: `IStudentScheduleService` | In DI, no consumer | Wire to UI or remove registration |
| P1-13 | Consolidate `DriverManagementView` / `DriversView` | Two driver UIs | Pick one clerk path |
| P1-14 | Index review for Postgres | Clerk grids filter by school, route, active year | `BusBuddyDbContext` fluent config; add indexes on `Students.DestinationId`, `Students.AmRouteId`, `Routes.AMVehicleId` if missing |

### P2 — Post-release / hygiene

| ID | Task | Rationale |
|----|------|-----------|
| P2-1 | Add `BusBuddy.DbPrep` to solution (non-shipping) | Discoverability for clerk proof |
| P2-2 | Trim `Documentation/Archive` CS files from working tree | 34 compiled-nowhere files |
| P2-3 | Centralize Serilog enricher versions in `Directory.Build.props` | `BusBuddy.WPF.csproj` L128 |
| P2-4 | Remove `package.json`/Trunk if unused in CI | Only `@trunkio/launcher` devDep |
| P2-5 | Add integration test category for Postgres Docker | `Category!=Integration` filter in CI |
| P2-6 | Version stamping in Release artifact | `BUILD-STAMP.txt` exists — wire into publish |
| P2-7 | Dependabot + `dotnet list package --vulnerable` gate | CI step is informational only (`ci.yml` L52-55) |
| P2-8 | Split `SeedDataService` | 1,782 lines; dev-only paths mixed with prod seed |
| P2-9 | Extract `App.xaml.cs` CLI handlers | Report/optimize/Ollama CLI mixed with WPF startup |
| P2-10 | RAG index freshness | Run `python -m rag.index` after doc changes per `AGENTS.md` |

### 5.1 Test coverage gaps (critical paths)

**Inventory rule (K02):** Every `.function-inventory.json` surface has a matching test — **passes**.

**Gaps by risk (not in inventory / thin coverage):**

| Critical path | Current tests | Gap |
|---------------|---------------|-----|
| `MainWindow.xaml.cs` navigation | `MainWindowClerkPathTests.cs` (token asserts) | No integration test opening each window |
| `App.xaml.cs` DI bootstrap | None dedicated | Fallback path untested |
| `RouteAssignmentViewModel` manual construction | `RouteAssignmentViewTests.cs` | Limited command coverage vs 2,300 lines |
| `VehicleManagementViewModel` | Partial via fleet tests | No dedicated VM test file |
| `DbPrep` clerk hops | Manual CLI | Not in CI |
| Postgres integration | Filtered out (`Category!=Integration`) | No CI Postgres profile |
| `FamilyService`/`GuardianService` | Unit tests exist | Services not in prod DI |
| End-to-end clerk UI | Optional VM clicks | B14 — not automated |

### 5.2 Security checklist

| Check | Status | Notes |
|-------|--------|-------|
| API keys in repo | **Pass** (B08) | Placeholders `${GOOGLE_MAPS_API_KEY}` only |
| SQL injection | **Low risk** | EF Core parameterized; no raw SQL in hot paths found |
| AuthN/AuthZ | **N/A** | Single-user desktop clerk; `UserContextService` is audit-only |
| PII in git | **Policy** | `Documentation/STUDENT-ROSTER-INTAKE.md` — never commit rosters |
| Maps key restrictions | **Operator** | Document API key HTTP referrer/IP restrictions in GCP |
| `docker-compose.yml` dev password | `busbuddy_dev` | Acceptable for local Docker only — document prod separation |

### 5.3 Build / CI / release hygiene

| Item | Current state | Recommendation |
|------|---------------|----------------|
| CI OS | `windows-latest` | Correct for WPF |
| Test filter | Excludes Integration + InMemoryFlaky | Add optional Postgres job |
| Docs PRs | Run full CI (no paths-ignore on PR) | ~3 min cost — acceptable |
| Release trigger | `workflow_dispatch` or push to master | Document in README for operators |
| CodeQL | Removed (private repo) | Documented in `AGENTS.md` — do not re-add |
| Auto-merge | Squash on green `Build & Test` | `setup-solo-ci-governance.sh` |
| Local pre-push | `validate-ci-local.sh` | Run before prod tag |

---

## 6. Execution order (recommended)

For a senior engineer executing this backlog without re-investigation:

1. **Ship blockers:** P0-1 (map smoke), optional UI proof boxes, Route FK plan sign-off.
2. **Safe deletions:** §1 items marked **safe** (~15 WPF files, 4 Core files) — one PR, `dotnet test` green.
3. **Startup reliability:** P0-2, P0-3 (async settings load, DI fail-fast).
4. **Dead code + DI hygiene:** P1-1, P1-2, P1-11, P1-12.
5. **Schema cleanup:** P1-4, P1-11 (coordinate migrations).
6. **God-class splits:** P1-5, P1-6 (incremental, post-ship).
7. **Docs/contracts:** P1-10 (XML docs on service interfaces).
8. **Performance:** P1-7, P1-8, P1-14.

After each cluster touching views: run `/code-review` or `Scripts/utm-wpf-test.sh` on Windows VM per `docs/done-checklist.md`.

---

## 7. Appendix — project-done checker snapshot (2026-09-18)

```
Verdict: NOT DONE
Failed: 2 · Deferred: 6 · Passed: 26 · N/A: 2

Failures:
- B14_ui_operational_evidence (optional Hop UI + District Map re-smoke)
- B10_action_items_blocking (unchecked action-items boxes)

Deferrals:
- IFamilyService / IGuardianService (dead-families-guardians)
- SchoolCalendar, AIInsight (dead tables)
- RouteAssignments duplicate write
- live-gps, osm-probe-only
```

Re-run: `python3 .github/scripts/check-project-done.py`

---

*End of analysis. No functional code was changed in this pass.*
