# BusBuddy Action Items (Due-Outs Tracker)

**Canonical due-outs file for agents and humans.**
**Clerk write path (school → kids → generate → bus/driver → fuel/maintenance):** [clerk-path.md](./clerk-path.md)
**Historical finish narrative:** [STEADY-STATE-AND-FINISH-ROADMAP.md](../STEADY-STATE-AND-FINISH-ROADMAP.md)
**Spec-Kit features:** [specs/](../specs/) (each feature may also have `tasks.md`)
**Open GitHub issues:** https://github.com/Bigessfour/BusBuddy-3/issues
**Generated inventory (optional):** run function-inventory scanner → `docs/function-inventory.generated.md`
**Visual tree (optional):** [function-tree.md](./function-tree.md)

**Update rule:** When starting or finishing work, check boxes here and link the PR/spec. Prefer this file for “what’s left”; use Spec-Kit `tasks.md` for implementation steps inside a feature.

---

## Priorities (now)

### P0 — Clerk path (living tracker)

Canonical hops: [clerk-path.md](./clerk-path.md). **Do not** split `MainWindow.xaml.cs` / `StudentsViewModel.cs` in the same session as a hop proof.

**Now (hop 1 — Add School)**

- [x] `IDestinationService.AddSchoolAsync` (name, address, bell times, optional GPS)
- [x] Students → **Add School** form; geocode via `IGeocodingService` or typed lat/lng
- [x] Unit: `DestinationServiceTests.AddSchool_PersistsCatalogRowWithBellTimes` + `AddSchool_PersistsOptionalGps`
- [ ] **VM smoke:** Students → Add School → Serilog `Added school DestinationId=` → row in `Destinations` with `StartTime` / `DismissalTime`. Prefer GPS so hop 3 can persist stop times (`./run-wpf.sh`)

**Next (one hop at a time; prove then check the box)**

- [ ] Hop 2 — Student save / CSV: `DestinationId` + coordinates. VM: Add Student, pick the school, validate address
    - Code complete 2026-09-03: `StudentSchoolLinker`, grid/bulk/delete via `IStudentService`, school/pickup catalog messages, geocode on validate/save, CSV sole-school link, Hop2 grid columns (`DestinationId`, lat/lon). **VM smoke still open on Windows.**
- [ ] Hop 3 — Generate Routes: Serilog `Route generation completed`. Stop if a second table is written
- [ ] Hop 4 — Bus + driver on `Routes.AMVehicleId` / `AMDriverId` (Assign Bus opens Route Assignments)
    - Vehicle fleet code 2026-09-03: `VehicleManagementViewModel` save/delete via `IBusService`; `BusForm` removed; `VehicleFleetLauncher` is the single entry (MainWindow, QuickActions, Dashboard). **VM smoke still open:** add bus → Serilog `Added vehicle BusId=` → row in `Buses`.
- [ ] Hop 4b — Pick **one** write: `Route.AM*` vs `RouteAssignments` vs `Schedules` (do not add a fourth)
- [ ] Hop 5 — `ScheduleService` row for that route
- [ ] Hop 6 — Fuel / Maintenance records point at that bus (`VehicleFueledId` / `VehicleId`)

**Later (after hops, not during proof)**

- [ ] Split `MainWindow.xaml.cs` (1477) — navigation vs lifecycle partials
- [ ] Stop growing `StudentsViewModel.cs` (1385); school catalog stays on `SchoolDestinationFormViewModel`
- [ ] Dead tables stay unwired until hops are proved: Families/Guardians, TripEvents, AIInsights, SchoolCalendar, ActivityLogs

**Already wired (not the current hop)**

- [x] Dock grids load from services only (no John Doe sample rows)
- [x] Edit Student uses grid `SelectedItem`
- [x] Fuel and Maintenance on the main header
- [x] Drivers **Assign Bus** opens Route Assignments

### P1 — Syncfusion page-by-page control audit (UI)

Static scan 2026-08-28: **41 XAML surfaces** (1 shell, 18 pages, 18 dialogs, 4 controls), **611** `syncfusion:*` instances across **31** types, **474** `{Binding}` properties. Plus `maps:SfMap` on MapView (different xmlns, not in the 611).

Go **one surface at a time**. For every Syncfusion control on that surface, check:

1. **Theme** — `DynamicResource` BusBuddy/Fluent brushes; no hardcoded `#F5F5F5` / `Foreground="White"`; headers use `Text.OnPrimary`.
2. **ButtonAdv** — `Label` (not `Content`), `SizeMode=Normal`, `IconWidth=0` + `SmallIcon={x:Null}` unless a real icon is set; `Command` or `Click` wired.
3. **Grids** — `SfDataGrid` `ItemsSource` + `SelectedItem` match the ViewModel; column `MappingName` exists on the row type.
4. **Inputs** — `ComboBoxAdv` / `SfTextBoxExt` / `SfMaskedEdit` / `SfDatePicker` two-way bind; watermark vs `Label`.
5. **Charts / scheduler / map** — series `ItemsSource`, `SfScheduler` appointment mapping, `SfMap` layers.

Waves (do not skip Wave 1):

| Wave | Surfaces                                                                                                                                 |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | `MainWindow`, `RouteAssignmentView`, `RouteManagementView`, `StudentsView`, `StudentForm`, `MapView`                                     |
| 2    | `DriversView`, `DriverForm`, `DriverTrainingChecklistView`, `VehicleManagementView`, `VehiclesView` (stub: 0 SF controls), `ReportsView` |
| 3    | Remaining pages (`Dashboard`, `Fuel`, `Maintenance`, `Activity`, `Analytics`, `Settings`, `DriverSchedule`, `DriverManagement`)          |
| 4    | Dialogs / forms (`*Form`, `*Dialog`, preview/welcome)                                                                                    |
| 5    | `Controls/*` (`QuickActionsPanel`, `AddressValidationControl`, `StudentStatisticsPanel`, `TestSyncfusionControl`)                        |

Known scan flags (not yet fixed): **17** `ButtonAdv` with neither `Command` nor `Click` (code-behind or dead); `MainWindow` is Click-heavy (22 clicks, 0 commands); `VehiclesView` has no Syncfusion controls.

Wave 1 (2026-08-28): implicit ButtonAdv glyph suppression on shell/map/form; Route Management Edit/Copy `Text.OnPrimary`; Students Map/Suggest min-width; map Live/Show AutomationNames + tracking interval binding.

Wave 2–3 (2026-08-28): DriverForm ComboBox `SelectedValue`+`Content`; Vehicles stub hosts `VehicleManagementView`; Vehicle status bar off Accent blue; Reports/Fuel/Dashboard/Maintenance/Analytics/Timeline/Settings/Schedule glyph collapse + AutomationNames.

- [x] Wave 1 XAML audit (`MainWindow`, `RouteAssignmentView`, `RouteManagementView`, `StudentsView`, `StudentForm`, `MapView`) — remaining: VM smoke
- [x] **StudentForm vertical audit** — service dry-run, route combos, numpad, student number removed from UI
- [x] Removed `QuickActionsDialog`; aligned grades via `StudentGradeCatalog`; MainWindow Add/Edit → `StudentsView`
- [x] **Student views code-complete (2026-09-03)** — all P1 fixes; VM smoke Hops 1–2 remain clerk proof on Windows
- [ ] **VM smoke (Hop 1):** Add School → Serilog `Added school DestinationId=` → `Destinations` row
- [ ] **VM smoke (Hop 2):** Add Student → Serilog `Successfully saved student` → `Students` row with `DestinationId` + coords
- [x] Wave 2 complete (`DriversView`, `DriverForm`, `DriverTrainingChecklistView`, `VehicleManagementView`, `VehiclesView` host, `ReportsView`)
- [x] Wave 3 complete (remaining pages)
- [x] Waves 4–5 complete (dialogs + shared controls) — 2026-08-31: unwired ButtonAdv wired or Click-in-XAML; NotificationWindow FindName; VehicleForm ancestor; missing styles; activity editor Syncfusion-only
- [x] Code-review follow-up (2026-08-31): shared `ButtonAdvTextOnly.xaml`; generate coordinator takes `IDestinationService`; assignment generate is a partial; docking headers without emoji; live-tracking timer; welcome/VehiclesView stripped; VehicleForm hosts fleet view

### P0 — Platform / tooling

- [x] Spec-Kit brownfield bootstrap (001–005) — merged [PR #20](https://github.com/Bigessfour/BusBuddy-3/pull/20)
- [x] **007 Maps Platform Geo (retire Earth Engine)** — US2+docs merged [PR #31](https://github.com/Bigessfour/BusBuddy-3/pull/31); US1+US3 [PR #35](https://github.com/Bigessfour/BusBuddy-3/pull/35) · [spec](../specs/007-maps-platform-geo/spec.md) · [tasks](../specs/007-maps-platform-geo/tasks.md)
    - [x] US2: Remove GEE DI, client, probe, unofficial Google tiles
    - [x] US1: Address Validation + geocode onto SfMap (Maps client + DI)
    - [x] US3: Routes API drive polyline (fail-open optimizer)
    - [x] US4: Places type-ahead on Student + School forms (`GooglePlacesAutocompleteService`, session tokens)
    - [x] Docs for US2; Maps clients wired
- [x] **Student contact + school destinations** — parent/emergency fields, Destination School catalog, intake school dropdown, map schools, inter-district `StudentSchoolTransfer` (timed pickup/dropoff) — merged [PR #36](https://github.com/Bigessfour/BusBuddy-3/pull/36)
    - [x] Apply migration `20260817140000_StudentContactFieldsAlignment` — Mac Postgres `dotnet ef database update` works on a **fresh** database (`BUSBUDDY_CONNECTION` → Docker). WPF startup uses `Migrate()`; existing EnsureCreated DBs must be dropped or pointed at a new catalog.
    - [ ] VM: assign school on intake; Show Schools on map; create transfer home→campus
    - [x] Mac smoke 2026-08-28: `dotnet ef database update` on Docker Postgres applied through `20260817160000_DestinationSchoolTimes` (11 migrations). VM UI smoke still due.
    - [x] Transfer UI (Students → School Transfer) — pickup/dropoff location + times required; waypoints rebuild on assign/transfer
- [x] **Driver employment + CDE training sub-module** — contact/address/hire; [CDE 2024-25 License/Training Matrix](https://resources.finalsite.net/images/v1764086158/cdestatecous/mpcomjjt3zryb1vussig/2024-25-License-Training-Matrix.pdf) checklist via `DriverTrainingRecord` / `IDriverTrainingService` — merged [PR #36](https://github.com/Bigessfour/BusBuddy-3/pull/36) (NoTracking write fix for Upsert)
    - [x] Apply migration `20260817150000_DriverTrainingSubmodule` — same Mac Postgres `database update` path as student-contact (2026-08-28)
    - [ ] VM: edit driver employment fields; open Training grid; mark complete + certificate
    - [x] Dedicated training grid UI (Drivers → Training) — mark complete / certificate per row
- [x] **008 Route determination / fleet sizing** — [tasks](../specs/008-route-determination/tasks.md) T001–T041 implemented on [PR #38](https://github.com/Bigessfour/BusBuddy-3/pull/38) (follow-on to merged [#37](https://github.com/Bigessfour/BusBuddy-3/pull/37))
    - Design locked 2026-08-17: Q1:A / Q2:B / Q3:B
    - [x] US1 generate/pack/override · US2 assign fitness · US3 school-time schedules · US4 transfer fleet · polish
    - [x] Review hardenings (schedule regen persists Scheduled+Estimated; transfer stops use pickup/dropoff; Both creates AM+PM) — PR #39 follow-up
    - [x] Apply migrations on Mac Docker Postgres (`…DestinationSchoolTimes` + #36 migrations) — 2026-08-28
    - [ ] VM smoke per [quickstart](../specs/008-route-determination/quickstart.md) (**T041 still open**)
        - Generate Routes / Transfer Routes are on the docking **Route Assignment** toolbar and the right-hand **Routes** pane (not only the Route Management dialog).
    - Serilog expected: `Route generation completed`, `Assign fitness Blocked|Warned`, `Schedule regen School=`
    - [x] Unit proof (2026-08-31): `RouteDeterminationServiceTests` (missing school / AM StartTime / dry-run drafts / Both override reject); `AssignFitnessEvaluatorTests` (seating block/override, geo warn); `RouteGenerationCoordinatorTests` (no planner/schools, named-school dispatch). Mac cannot run WPF testhost — CI windows-latest.
- [x] **006 Syncfusion Tool Integration** — [spec](../specs/006-syncfusion-tool-integration/spec.md) — merged [PR #21](https://github.com/Bigessfour/BusBuddy-3/pull/21)
    - [x] MCP paths, skills overlay, Syncfusion **34.2.3**, deps audit
    - [x] `python -m rag.index` after merge (2026-07-24; ~3399 chunks)
    - [x] Windows VM Syncfusion license + UI smoke — checklist: [windows-vm-smoke.md](../specs/006-syncfusion-tool-integration/windows-vm-smoke.md) — **2026-08-16:** license registered early in `Program.cs`; MainWindow DockingManager loaded (no trial dialog / no star-width XAML crash)
    - [x] P2: AutoMapper 12 → **15.1.1** (GHSA-rvv3-g6hj-g44x)

### P1 — Finish / domain (Spec-Kit wave 2) — aligns with [issue #11](https://github.com/Bigessfour/BusBuddy-3/issues/11)

- [x] Student import / optimize end-to-end (UI + SeedDataService + tests)
    - [x] CSV import wired: `ISeedDataService.ImportStudentsFromCsvAsync` + Students/StudentForm Import CSV buttons. Proof: parent address columns, next `STU` number, unexpected-header rejection, form import refreshes list via `StudentsImportedMessage`
    - [x] Optimize routes: `IStudentRouteOptimizer` fills active routes via `IRouteService.AutoAssignStudentsAsync`, then Ollama/`GrokGlobalAPI` commentary (mock fallback). Wired on Students + Dashboard. Proof: `StudentRouteOptimizerTests`
- [x] Reports: `IOperationalReportService` writes live PDFs/CSVs via `PdfReportService.GenerateTabularReport` + Ollama/`GrokGlobalAPI.GetShortCommentaryAsync` (mock fallback). All Reports buttons + Dashboard Generate Report. Proof: `OperationalReportServiceTests`, `PdfReportServiceTests.GenerateTabularReport_ReturnsValidPdf`. Merged [PR #24](https://github.com/Bigessfour/BusBuddy-3/pull/24). CLI `--generate-report` uses the same service (aliases: Roster, RouteManifest, StudentList, DriverSchedule)
- [x] Driver availability + SfScheduler — `DriverAvailabilityCalculator` uses Schedule rows; `DriverScheduleView` binds SfScheduler; `Schedule_Click` opens it
    - Serilog proof: `Driver availability calculated` / `Driver schedules loaded Appointments=`
- [x] Maintenance UI polish — `MaintenanceView` + `IMaintenanceService` CRUD; `Maintenance_Click` opens it
    - Serilog proof: `Maintenance UI loaded Records=` / `Created maintenance record`
- [x] Google Earth Engine enhancements (beyond current DI/auth) — **superseded by 007**: EE is the wrong product for addresses/trips; see [007 Maps Platform Geo](../specs/007-maps-platform-geo/spec.md)
    - Historical: shared map VM + `IGeocodingService` + SfMap plot (hash geocoder retired with 007 US1)
- [x] SfMap mapping: official OSM + school-catalog / fallback center, Syncfusion string markers, shared `MapViewModel`, live routes/buses (not sample-only). Earth Engine is not used.
- [x] End-to-end student → assign → report proof test — `BusBuddy.Tests/Core/RouteAssignmentFlowTests.cs` (SeedDataService → StudentService → RouteService → PdfReportService). **UTM Windows VM 2026-08-16:** `Total tests: 1`, `Passed: 1` (built from `C:\dev\BusBuddy-3` after Z:\ sync). Mac host cannot execute WPF testhost; use `./run-wpf.sh` + `utm_run_in_vm.ps1` for GUI.
- [x] P1 surface proof files (2026-08-31): `StudentsViewTests` (Import/Optimize/Transfer/Add commands in XAML); `ReportsViewTests` (roster/unassigned/route summary/CSV); inventory links `AssignFitnessEvaluatorTests` + `RouteDeterminationServiceTests`

### P2 — Hygiene / quality

- [x] Bootstrap function-inventory generated scan — `.function-inventory.json` (26 surfaces) → [function-inventory.generated.md](./function-inventory.generated.md) (2026-08-17: 16/26 with proof; added Destination/Transfer/Training)
- [x] Resolve LFS/chroma noise — stop tracking `rag/chroma_db/` (~54MB sqlite blobs that triggered GH001); drop `*.pdf`/`*.sqlite` LFS attrs (PDF stays git binary). History purge of old blobs still needs force-push approval.
- [x] AutoMapper 12.x advisory — upgraded to 15.1.1
- [x] Restore [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](../Documentation/GCP-GEE-SECRETS-AND-AUTH.md) (was missing; AGENTS link)

### GitHub issues triage (open as of 2026-07-24)

| Issue                                                     | Topic                                           | Suggested disposition                                                                                                                              |
| --------------------------------------------------------- | ----------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| [#13](https://github.com/Bigessfour/BusBuddy-3/issues/13) | ViewModel dedup                                 | **Closed** (hygiene / PR #16)                                                                                                                      |
| [#14](https://github.com/Bigessfour/BusBuddy-3/issues/14) | CI + secrets/MCP                                | Solo CI + auto-merge done; Passwords for Syncfusion/Maps. Optional: GH Actions secrets / `ci-with-ai` cleanup → **close or narrow**                |
| [#15](https://github.com/Bigessfour/BusBuddy-3/issues/15) | Deprecate bb-\* PS                              | **Closed** — remaining install/archive modules removed in [PR #32](https://github.com/Bigessfour/BusBuddy-3/pull/32)                               |
| [#11](https://github.com/Bigessfour/BusBuddy-3/issues/11) | Close stubs (Reports/Grok/Settings/Maintenance) | P1 done ([PR #30](https://github.com/Bigessfour/BusBuddy-3/pull/30)); Drivers placeholders wired to live reports/services — close via follow-up PR |

---

## Spec-Kit features — status

| Spec    | Title                       | Status                                             |
| ------- | --------------------------- | -------------------------------------------------- |
| 001–005 | Platform wave               | Done (PR #20)                                      |
| 006     | Syncfusion Tool Integration | Done code (PR #21); VM smoke **passed 2026-08-16** |

---

## AI / agent tooling

- [x] `busbuddy-rag` MCP + mandatory RAG rule (+ re-index after #21)
- [x] Syncfusion WPF MCP path-correct (Box workspace) + key via Passwords / env
- [x] Syncfusion WPF skills overlay updated for 34.x (vendor: `setup-syncfusion-skills.sh`)
- [x] Spec-Kit `/speckit-*` skills committed

---

## Function inventory (surfaces)

Config: [`.function-inventory.json`](../.function-inventory.json) · generated: [function-inventory.generated.md](./function-inventory.generated.md) · tree: [function-tree.md](./function-tree.md)

Re-scan after adding a P1 view or core service:

```bash
python3 ~/.cursor/skills/function-inventory/scripts/update-function-inventory.py \
  --root . --output docs/function-inventory.generated.md
```

| Surface                                                  | Tier | Proof / next check                                                                                                                                                              |
| -------------------------------------------------------- | ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Student / Seed / Route / Optimizer / Reports services    | P1   | Unit tests present (`StudentServiceTests`, `SeedDataServiceTests`, `RouteServiceTests`, `StudentRouteOptimizerTests`, `OperationalReportServiceTests`, `PdfReportServiceTests`) |
| `DestinationService` / `StudentSchoolTransferService`    | P1   | Transfer + waypoints: `StudentSchoolTransferAndWaypointTests`. Destination: `DestinationServiceTests` (no auto-seeded school). VM intake school assign still due (P0).          |
| `ScheduleService`                                        | P1   | Unit: `ScheduleServiceTests`. Runtime Serilog: `GetSchedulesAsync` count + elapsed. SfScheduler: `DriverScheduleView`.                                                          |
| `DriverTrainingService`                                  | P1   | `DriverTrainingServiceTests` present. VM Training grid smoke still due (P0).                                                                                                    |
| `StudentsView` / `ReportsView` / transfer & training UI  | P1   | No WPF testhost on Mac. Proof is VM smoke (`./run-wpf.sh`) + Core tests above. Do not treat as a missing feature.                                                               |
| `DriverService`                                          | P1   | `DriverServiceTests` exist; `DriverScheduleView` + `DriverAvailabilityCalculator` (Schedule + ActivitySchedule). Availability calc logs `Drivers=` / `WithOpenDays=`            |
| `MaintenanceService` / Dashboard metrics / theme manager | P2   | `MaintenanceService` logs CRUD. Dashboard: `DashboardViewModel` logs refresh/optimize/report. Earth Engine retired (spec 007).                                                  |
| `DashboardView` / `GeoDataService`                       | P2   | VM smoke + Serilog: `Dashboard refresh completed` / `Loaded routes with geo data`                                                                                               |

---

## Meta

- Re-check this file at the start of each session.
- After structural changes: update architecture map in `STEADY-STATE-AND-FINISH-ROADMAP.md` and re-index RAG.
- Do not put secrets here.

---

_Updated 2026-08-31: clerk-path hop tracker. Now = hop 1 VM smoke. Later = split giant files after hops._
