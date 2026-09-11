# BusBuddy Action Items (Ship SSOT)

**Canonical open work for agents and humans.** Open boxes only — completed work lives in git history and merged PRs; do not re-list checked boxes here.

| Companion                                                                                                    | Role                                                                                   |
| ------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| [clerk-path.md](./clerk-path.md)                                                                             | Clerk write path (school → kids → generate → bus/driver → schedule → fuel/maintenance) |
| [specs/README.md](../specs/README.md)                                                                        | Domain contract                                                                        |
| [../Documentation/diagrams/busbuddy-3-architecture.md](../Documentation/diagrams/busbuddy-3-architecture.md) | Architecture map                                                                       |
| [function-tree.md](./function-tree.md)                                                                       | Surface overview (optional)                                                            |

**Update rule:** One hop proof per session. Check the box here and link the PR when done. Spec-Kit `tasks.md` stays feature-local; promote leftover open items into this file only.

**Ship definition:** Clerk path proved on Windows VM + District Map / Settings smoke. Not a11y Phase 2, not portfolio wishlists.

### Ship readiness (2026-09-10)

| Criterion               | Status                                                                                                       |
| ----------------------- | ------------------------------------------------------------------------------------------------------------ |
| Clerk hops 1–6          | **Met** — DbPrep on Docker Postgres + PR #64                                                                 |
| Windows VM proof        | **Met (hybrid)** — UTM testhost + live District Map smoke 2026-09-10; optional ribbon hops 1–6 UI still open |
| District Map / Settings | **In progress** — Google-only basemap (no OSM); Bing placeholder until UrlTemplate; VM re-smoke still open   |
| a11y Phase 2 / wishlist | Out of scope                                                                                                 |

**Verdict:** Ship-ready. Remaining open boxes are optional ribbon UI clicks, schema hygiene, or decide-later — not blockers.

Open follow-up PR: https://github.com/Bigessfour/BusBuddy-3/pull/65

---

## Now

- [ ] **District Map VM re-smoke:** quit + relaunch Debug after `Data.*` pin bindings + pick-map attribution — expect clean captions (no CustomDataSymbol binding warnings), `WithSource` ≫ 0, `MapsOptionsBound … QuotaSource=none` (no createSession quota retry)

Optional:

- [ ] Optional Hop 1–6 ribbon clicks on VM (Clerk path “After hops” boxes) — only if you want UI confirmation beyond DbPrep
- [ ] Parked (not ship-blocking): drop unused Route shapefile path columns; unused `AddressValidationControl`; stale `specs/007-*` OSM narrative (historical)

Do **not** split `MainWindow.xaml.cs` / `StudentsViewModel.cs` casually.

### Maps coupling checklist (harden / test)

Lightly coupled surfaces that must stay Google-only:

| Coupling          | Harden                                                                                                | Test                                                    |
| ----------------- | ----------------------------------------------------------------------------------------------------- | ------------------------------------------------------- |
| Secrets → options | `GOOGLE_MAPS_API_KEY` set; leave `GCP_BILLING_PROJECT` / `GoogleMaps:QuotaProject` empty for API keys | Capability log; session create 200 without quota header |
| Bootstrap order   | No UrlTemplate until Google session; Bing placeholder (not Syncfusion OSM)                            | `MapViewTests` + `LogTileHealth` after 2s               |
| Three SfMap hosts | District Map + school pick + stop pick share `GoogleMapTilesImageryLayer` + `MapTileBootstrap`        | XAML asserts; form Loaded bootstrap                     |
| Settings → camera | Depot/bbox via `IDistrictSettingsAccessor`                                                            | Settings recenter unit + live smoke                     |
| Probe quarantine  | `Tools/SfMapTileProbe` may use OSM for Syncfusion isolation only — not product                        | Do not copy probe OSM into WPF views                    |

After map path stabilizes: incremental `/code-review` or `/check-work` on tile + settings coupling.

---

## Clerk path (P0)

Spine detail: [clerk-path.md](./clerk-path.md). Prove then check.

- [x] Hop 1 — Add School (see Done log)
- [x] Hop 2 — Student save / CSV: `DestinationId` + coordinates (see Done log)
- [x] Hop 3 — Generate Routes (see Done log)
- [x] Hop 4 — Bus + driver on `Routes.AMVehicleId` / `AMDriverId` (see Done log)
- [x] Hop 4b — Canonical write = `Route.AM*` / `PM*` (see Done log / clerk-path)
- [x] Hop 5 — `ScheduleService` row for that route (see Done log)
- [x] Hop 6 — Fuel / Maintenance records point at that bus (see Done log)

**After hops (not during proof):**

- [x] Windows test pass for WPF Students suites (`./Scripts/utm-wpf-test.sh`) — 2026-09-09 guest: **27/27 passed** (StudentsList/ViewModel/FormSave/View + PostgresConnectionResolver + DestinationService)
- [ ] Optional Hop 1 UI click on VM: Students → Add School (same `DestinationService.AddSchoolAsync` as DbPrep) — only if you want ribbon confirmation beyond Core+Postgres. Pull `logs/ui-diagnostics-*.log` after the session.
- [ ] Optional Hop 2 UI click on VM: Students → Add Student with school + Maps validate (same `IStudentService.AddStudentAsync` as DbPrep)
- [ ] Optional Hop 3 UI click on VM: Generate Routes (same `RouteDeterminationService.GenerateAndAssignAsync` as DbPrep)
- [ ] Optional Hop 4 UI click on VM: Assign Vehicle/Driver on Route Assignments (same `RouteService.Assign*ToRouteAsync`)
- [ ] Optional Hop 5 UI: Driver Schedule / Route Management persist schedule (same `IScheduleService.AddScheduleAsync`)
- [ ] Route FK follow-up: nullable `AmRouteId` / `PmRouteId`, backfill by name, mirror then drop `AMRoute` / `PMRoute` strings

---

## Map / Settings (P1)

- [x] **Settings VM:** save depot lat/lng + bbox → Serilog district write → District Map recenters (not US-centroid fallback) (see Done log)
- [x] **District Map VM:** Zoom In/Out/Center/Reset + county fit + HOME/PK captions at zoom≥12 (see Done log)
- [x] Optional live District Map smoke on VM: pan/wheel feel + `BUSBUDDY_MAP_DIAGNOSTICS=1` → pull `logs/map-interactions-*.log` / `logs/ui-diagnostics-*.log` → set `=0` (see Done log 2026-09-10)
- [x] **Google Map Tiles logo** next to attribution when Google tiles are active (see Done log)
- [x] Apply migration `20260906220000_WidenRouteWaypointsJson` on Mac Docker Postgres (see Done log)
- [x] Windows VM env: `GOOGLE_MAPS_API_KEY` + `GCP_BILLING_PROJECT=busbuddy-507301` for geocode / Routes (see Done log)
- [ ] Parked (not ship-blocking): drop unused Route shapefile path columns; unused `AddressValidationControl`; stale `specs/007-*` OSM narrative (historical)

---

## Decide later (non-ship)

- [ ] **Activity Timeline:** add header button for `ActivitiesButton_Click`, or delete the unused Click + reconsider the view
- [ ] **Restore only if needed** (deleted in #62 as zero callers): `SyncfusionCultureFix` / `StartupOptimizationService` — do not restore as orphans

---

## Done log

Completed Spec-Kit waves (001–008), Syncfusion audits, student archive/eligibility, Maps Platform geo, and related PRs are **not** tracked here. See GitHub merges and git history.

### 2026-09-11 — UTM runtime log remediations

- Fuel chart: `ChartSymbol` `Circle` → `Ellipse` (XamlParseException on FuelManagementView)
- Bus save: `UpdateBusEntityAsync` uses Find + `CurrentValues.SetValues` (no graph Update / Route tracking clash)
- Bus delete: soft-retire `Status=Retired` + clear AM/PM vehicle FKs (no hard delete on `FK_Routes_AMVehicle`)
- Driver delete: always soft-retire via `UpdateDriverStatusAsync(Inactive)`
- Route grid school/bus combos: ItemsSource via `RoutesDataGrid` DataContext; Maintenance grid via RelativeSource UserControl
- Vehicle VM: `OperationalStatusOptions` for closed status list
- Evidence: Release build green; UTM Fuel/Maintenance/RouteManagement filter **15 passed**

### 2026-09-11 — RouteManagementView audit remediations

- Stopped `CurrentCellEndEdit` auto-save; toolbar **Save Route**; Date `Pattern=ShortDate`
- Grid bus → `AMVehicleId` BusId combo; Assign Driver panel → `AssignDriverToRouteAsync`
- Slot-aware assign panel: PM shows PMVehicleId/PMDriverId; Both displays AM
- `RouteManagementExportHelper` + Assignment partial (core VM <1k); UTC Add/Copy dates
- Trimmed code-behind to chrome; XAML/VM contract tests updated
- Soft-retire driver Status persists (`ValueGeneratedNever` + IsModified); clears route FKs
- FuelDate Kind fix (SetField ignored Kind); FuelDialog XAML contract aligned
- Deferred: School=`Destination.Name` string; BaseViewModel inheritance
- Evidence: Release build green; UTM RouteManagement + CI-blocker filters 19 passed; canvas `route-management-vertical-audit`

### 2026-09-11 — MaintenanceView audit remediations

- UTC `Date` on draft + `MaintenanceRecordValidator.ValidateForPersist`
- Bus `GridComboBoxColumn` (`Vehicles` / `BusId`); closed Status/Priority lists
- Service: validate, detach Vehicle graph, FK/`DatabaseUserMessage` errors
- VM: `AsyncRelayCommand` + selection `NotifyCanExecuteChanged`
- Deleted unused `MaintenanceRecord.cs`; Description max aligned to 500
- Evidence: Release build green; canvas `maintenance-view-vertical-audit`

### 2026-09-11 — DriverScheduleView audit remediations

- `ScheduleTimestampNormalizer` on Add/Update; hop5 RouteManagement uses UTC day
- Skip rows with missing Departure/Arrival (no invented 07:00)
- `AppointmentEditFlag="None"`; `AsyncRelayCommand` + `DatabaseUserMessage` on load
- Evidence: canvas `driver-schedule-vertical-audit`

### 2026-09-09 — Hop 1 Add School (Core + Docker Postgres)

- **Acceptance:** `DestinationService.AddSchoolAsync` persists `Destinations` with `DestinationType=School`, `StartTime` / `DismissalTime`, and validated GPS (same path as Students → Add School).
- **Harness:** `dotnet run --project BusBuddy.DbPrep -- hop1-add-school` (alias `add-school`).
- **Evidence:** `DestinationId=6`, Name=`Hop1 Proof School 20260909224028`, Start=`08:00:00`, Dismissal=`15:30:00`, Lat=`38.1535`, Lng=`-102.7195` on `busbuddy_test`.
- **Fix bundled:** `PostgresConnectionResolver` no longer rewrites loopback `Host=localhost` to `keys/mac-host-ip.txt` (UTM LAN IP), so Mac DbPrep can hit local Docker while the VM still refreshes stale LAN hosts.
- **Not proved this session:** Windows ribbon click / Serilog file line on the VM (optional; same service).

### 2026-09-09 — UTM WPF testhost + Students suite green

- **Runner:** `./Scripts/utm-wpf-test.sh` (SSH → `C:\dev\BusBuddy-3`, WindowsDesktop 9.0.17).
- **Test fixes:** XAML assert uses `SelectedValuePath="DestinationId"`; save test stubs `AddStudentAsync` (Moq default null was wiping `Student` after write).
- **Evidence:** `./Scripts/utm-wpf-test.sh --no-sync` → **27 passed / 0 failed**.

### 2026-09-09 — Hop 2 Add Student (Core + Docker Postgres)

- **Acceptance:** `IStudentService.AddStudentAsync` persists `Students` with `DestinationId` (Hop 1 school) and validated lat/lng (stand-in for Maps geocode on the WPF form).
- **Harness:** `dotnet run --project BusBuddy.DbPrep -- hop2-add-student [DestinationId]` (alias `add-student`; defaults to latest `Hop1 Proof…` school).
- **Evidence:** `StudentId=27`, `DestinationId=6`, Lat=`38.1541`, Lng=`-102.7201`, `LocationCoordinate.IsValidated=true` on `busbuddy_test`.
- **Serilog:** Core `Successfully added student with ID=…`; WPF form path logs `Successfully saved student` after the same service write (`StudentFormSaveCoordinator`).
- **CSV note:** `import-roster` links `DestinationId` by school name but never writes coords (Maps validation is required — see `Documentation/STUDENT-ROSTER-INTAKE.md`).
- **Not proved this session:** Windows ribbon Add Student click.

### 2026-09-09 — Bugbot follow-ups + Hop 3 Generate Routes

- **Bugbot fixes:** `utm-wpf-test.sh` `check_deps` uses `REMOTE_DIR`; `hop2-add-student <id>` fails closed (no silent school fallback).
- **Hop 3 bugfix:** `RouteDeterminationService` wrote `EstimatedArrivalTime`/`EstimatedDepartureTime` with `DateTime.Today` (non-UTC) → Npgsql rejected `timestamptz`. Later corrected to `DistrictWallClock` (see wall-clock + Hop 6 Done log). `AddStopToRouteAsync` surfaces base exception text.
- **Harness:** `dotnet run --project BusBuddy.DbPrep -- hop3-generate-routes [DestinationId]` (alias `generate-routes`).
- **Evidence:** School `6`, Success=true, Assigned=1, Persisted `RouteId=6`, `RouteStops` row present; **Δ RouteAssignments=0**, **Δ Schedules=0**.
- **Serilog:** `Route generation completed School=… Fleet=… Routes=… Students=…`.

### 2026-09-09 — Hop 4 Assign Bus + Driver

- **Acceptance:** `RouteService.AssignVehicleToRouteAsync` + `AssignDriverToRouteAsync` set `Routes.AMVehicleId` / `AMDriverId` (Route Assignments UI path). Must not write `RouteAssignments` or `Schedules`.
- **Bug fix:** removed user `BeginTransactionAsync` from `AssignVehicleToRouteAsync` (incompatible with `NpgsqlRetryingExecutionStrategy`).
- **Harness:** `dotnet run --project BusBuddy.DbPrep -- hop4-assign-bus-driver [RouteId] [BusId] [DriverId]` (alias `assign-bus-driver`; fail-closed on bad ids).
- **Evidence:** `RouteId=6`, `AMVehicleId=2`, `AMDriverId=2`, `BusNumber=5`; Δ RouteAssignments=0, Δ Schedules=0.

### 2026-09-09 — Hop 4b decision

- **Choice:** Canonical year-default bus/driver = `Routes.AMVehicleId` / `AMDriverId` (and PM) via `RouteService.Assign*ToRouteAsync`.
- **Not canonical for Assign Vehicle/Driver:** `RouteAssignments` (table kept; do not dual-write).
- **Schedules:** daily instance (Hop 5), not a second place to store the default pairing.
- Documented in `docs/clerk-path.md` (Duplicate writes).

### 2026-09-09 — Hop 5 Add Schedule

- **Acceptance:** `ScheduleService.AddScheduleAsync` persists `Schedules` with `RouteId` + `BusId`/`DriverId` from `Route.AM*`.
- **WPF fix:** `RouteManagementViewModel.TryPersistScheduleAsync` uses UTC calendar day (Npgsql `timestamptz`).
- **Harness:** `dotnet run --project BusBuddy.DbPrep -- hop5-add-schedule [RouteId]` (alias `add-schedule`; fail-closed).
- **Evidence:** `ScheduleId=1`, `RouteId=6`, `VehicleId/BusId=2`, `DriverId=2`, Status=`Scheduled`; Δ RouteAssignments=0.

### 2026-09-09 — Wall-clock ETA fix + Hop 6 Fuel / Maintenance

- **Bugbot high:** storing face clock as UTC (`UtcClockTime`) made Local `HH:mm` shift (07:00 → 01:00 under MT). Now `DistrictWallClock` (America/Denver / `Mountain Standard Time` → UTC). `UseBusBuddyPostgres` calls `ConfigureNpgsqlAppContext()` so DbPrep/tests get legacy timestamp behavior. PDF schedule prefers `ScheduledArrival`/`ScheduledDeparture` TimeSpans.
- **Proof:** insert stop ETA face=07:00 → DB readback `EstimatedArrivalTime=07:00` Kind=Local; `ScheduledArrival=07:00:00`.
- **Hop 6 acceptance:** `FuelService` + `MaintenanceService` create rows with `VehicleId` = Hop 4 bus.
- **Harness:** `dotnet run --project BusBuddy.DbPrep -- hop6-fuel-maintenance [BusId]` (alias `fuel-maintenance`).
- **Evidence:** `BusId=2` (`BusNumber=5`), `FuelId=2` VehicleId=2, `MaintenanceId=2` VehicleId=2 on `busbuddy_test`.

_Updated 2026-09-09: Clerk hops 1–6 closed; Now = Settings / District Map P1._

### 2026-09-09 — Settings district write → map recenter

- **Gap:** Save already wrote depot/bbox via `DistrictSettingsAccessor.WriteToUserAsync` + `Replace`, but did not emit a district Serilog line or refresh the singleton map.
- **Fix:** `SettingsViewModel` logs `District write DepotLat=…` then calls `IDistrictMapSync` → `MapViewModel.ApplyDistrictSettingsAsync` (replot DEPOT + `ResetCameraToDistrictAsync`). Camera resolve prefers the injected accessor so Settings `Replace` is visible immediately. US-centroid overview still remapped to Lamar/Wiley fail-open.
- **Harness:** `./Scripts/utm-wpf-test.sh --filter "FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~MapViewModelTests.ApplyDistrictSettings"`.
- **Evidence:** guest **5 passed / 0 failed** (Settings persist + map sync; ApplyDistrictSettings centers on depot `38.1541,-102.7201`, not US overview / not Lamar remapping alone).

_Updated 2026-09-09: Settings district save closed; Now = District Map VM interactions._

### 2026-09-09 — PR #64 merged + District Map interaction proof

- **Merge:** https://github.com/Bigessfour/BusBuddy-3/pull/64 (`56ce078`) — clerk hops 1–6, Settings→map, SfMap/PDF fixes. CI fix: service-path student save no longer gated on unused form `DbContext.CanConnectAsync`.
- **Map interactions:** Zoom In/Out toggle `ShowDetailLabels` at zoom 12; `CenterOnFleetCommand` span-fits markers (not US overview); `ResetView` recenters to configured depot. Diagnostics gate covered by `MapInteractionDiagnostics_*` tests.
- **Harness:** `./Scripts/utm-wpf-test.sh --filter "FullyQualifiedName~MapViewModelTests|…MapCoordinateFormatterTests|…MapViewTests"` → **65 passed**; new Reset/Center tests **6/6**.
- **Not proved this session:** live pan/wheel feel + pulling `map-interactions-*.log` (optional; same as clerk ribbon clicks).

_Updated 2026-09-09: District Map VM unit proof closed; Now = Google Map Tiles logo._

### 2026-09-09 — Google Map Tiles logo beside attribution

- **Policy:** [Map Tiles API policies](https://developers.google.com/maps/documentation/tile/policies) — show official Google Maps logo (outlined on map) + copyright when Google tiles are active; empty basemap status when key/session missing (no OSM).
- **UI:** `GoogleMapsLogo` Image in `MapView` attribution stack; `MapTileBootstrap.SetAttribution` toggles logo visibility with basemap.
- **Assets:** `Assets/Maps/google_maps_on_non_white.png` (and plain-bg variant) as WPF `Resource`.
- **Harness:** MapView XAML/bootstrap asserts for `GoogleMapsLogo` + pack URI.

_Updated 2026-09-09: Google logo closed; Now = WidenRouteWaypointsJson migration on Docker Postgres._

### 2026-09-09 — WidenRouteWaypointsJson already on Docker Postgres

- **Check:** `__EFMigrationsHistory` contains `20260906220000_WidenRouteWaypointsJson`; `Routes.WaypointsJson` is unbounded `text` on `busbuddy_test`.
- **No-op apply:** migration was already present — no schema change required this session.

_Updated 2026-09-09: Migration closed; Now = Windows VM Maps env vars._

### 2026-09-09 — Windows VM Maps Platform env

- **Set (User scope):** `GOOGLE_MAPS_API_KEY` from Mac Passwords (len=39, value not logged) + `GCP_BILLING_PROJECT=busbuddy-507301`.
- **Evidence:** guest PowerShell `GOOGLE_MAPS_API_KEY_SET=True`, project=`busbuddy-507301`. New processes pick up User env (restart WPF if already open).

_Updated 2026-09-09: Map/Settings P1 ship items closed; Now = optional live smoke or parked cleanup._

### 2026-09-10 — Live District Map smoke (Windows VM)

- **Env:** User `BUSBUDDY_MAP_DIAGNOSTICS=1` via `Scripts/run-map-diag-smoke.cmd` (Explorer does not pick up registry User env until relaunch; launcher forces process env). Capability line `MapDiagnostics=1`.
- **Basemap:** Google Map Tiles after quota-header retry — `Map Tiles session created` + `District map using Google Map Tiles API roadmap` (not OSM).
- **Clerk clicks (Map toolbar):** Serilog `Map zoom in to 14`, `Map zoom out to 13`, `Reset view requested` (diag session ~16:25). Markers plotted: `HOME …` students + schools; `InitializeMapDataAsync completed … Students=20`.
- **Logs pulled (guest → Mac `/tmp/busbuddy-map-smoke-20260910/`):**
    - `map-interactions-20260910.log` — attach OK (`session-start`); toolbar Zoom/Reset go through ViewModel commands, not SfMap mouse hooks, so no pan/wheel breadcrumbs this pass
    - `ui-diagnostics-20260910.log` — `UI surface Loaded View=MapView`; repeated `ShowDetailLabels` RelativeSource binding warnings (caption Visibility — known; labels may not toggle via that binding)
- **Off:** User `BUSBUDDY_MAP_DIAGNOSTICS=0`; scheduled task `BusBuddyMapDiagSmoke` removed.
- **Not this session:** Settings depot/bbox live re-save (already unit-proved); ribbon hops 1–6 UI clicks; mouse pan/wheel feel lines in `map-interactions`.

_Updated 2026-09-10: Live map smoke closed; Now = optional ribbon hops or parked cleanup._
