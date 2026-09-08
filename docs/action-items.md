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

### P0 — Students domain ship-readiness (2026-09-07)

End-to-end audit fixes against [specs/students.md](../specs/students.md). Migration `20260907150000_StudentRideEligibilityAndSchoolYear` is **scaffolded and verified but not yet applied to the clerk database** — see the open box below.

- [x] **Archive replaces hard delete.** _"MUST NOT delete a student to end service."_ `StudentService.ArchiveStudentAsync` / `RestoreStudentAsync`; `PurgeStudentRecordAsync` refuses an active student or one with schedule/transfer history; toolbar reads **Archive Student**. `Family → Student`, `StudentSchoolTransfer → Student`, and `StudentSchedules → Student` cascades changed to `Restrict`.
    - Evidence: `StudentArchiveAndEligibilityTests` — archive/restore round-trip, archived rows stay loadable but leave the active read, purge gating, FK restrict behaviour.
- [x] **Explicit AM/PM eligibility + SchoolYear.** `Student.RidesAm` / `RidesPm` / `SchoolYear` are stored columns; `StudentRideModeHelper` reads the flags, not route-name strings. **Both flags default to `false`** in C# and in the database — eligibility is stated, never assumed. The new-student form pre-ticks both where a clerk can see them (`StudentFormViewModel`).
    - Evidence: `NewStudent_IsEligibleForNeitherRun_UntilStated`, `ApplyRouteDerivedEligibility_*`, `ImportStudentsFromCsvAsync_RosterFormat_AmOnlyRowIsNotPmEligible`.
- [x] **EF snapshot re-baselined on Npgsql** and 14 orphan `Auto_*` migrations deleted. `dotnet ef migrations has-pending-model-changes` reports no drift.
- [x] **Address validation is one policy.** Unvalidated saves stay possible but are flagged: `Student.IsIntakeIncomplete` / `IntakeStatus`, edit mode validates, inline address edits clear stale coordinates, and **Show Incomplete Records** surfaces the backlog. Map plots only validated lat/lng. **2026-09-07:** `Validate Address` was a no-op persist: form wrote lat/lng in memory only, and the grid called `UpdateStudentAsync` (full intake validation). Both now use `IStudentService.UpdateHomeGeocodeAsync`. Pickup ComboBoxAdv + ButtonAdv icons follow Syncfusion docs (`DisplayMemberPath` only; `LargeIcon`/`SmallIcon` `{x:Null}`).
    - Evidence: `UpdateHomeGeocodeAsync_WritesCoordinatesWithoutFullIntakeValidation`. VM still needed: Validate Address on an existing student → Serilog `Home geocode persisted` → `Latitude`/`Longitude` set.
- [x] **Route-name linkage hardened.** Indexes on `AMRoute` / `PMRoute` / `SchoolYear`. `ISeedDataService.EnsureRoutesForStudentAssignmentsAsync` creates any missing `Routes` row and rewrites word-order variants to the existing spelling, so `ValidateStudentAsync` cannot lock a clerk out of an assigned student.
    - Evidence: `EnsureRoutesForStudentAssignmentsAsync_CreatesRouteForAlreadyAssignedStudents`, `_RewritesVariantSpellingToExistingRoute`. Canonical Bus 5 name is **`AM Special Needs Bus 5`** (the clerk database spelling).
- [x] **`DestinationId` is the school of record.** List/detail reads `Include` `Destination` + `PickupStop`; the legacy `School` string is a derived display mirror.
- [x] **Correctness fixes:** `SearchStudentsAsync` uses `EF.Functions.Like` (the `StringComparison` overload does not translate on Npgsql), three leaked DbContexts disposed, pointless `SemaphoreSlim` removed, inline grid saves write only dirty rows, `async void` commands converted to `AsyncRelayCommand`, UTC timestamps, no `ContinueWith`.
- [x] **Dead surface removed:** `StudentViewModel` + its AutoMapper profile; the unused `StudentRepository` / `IStudentRepository` / `IUnitOfWork.Students` layer; `FilteredBusStops`; placeholder route names; `Student.RouteId` / `SpecialInstructions` / `EmergencyContactPhone` / `AgeYears` aliases; `FromRouteNames`; and four zero-caller service methods.
- [x] **Apply the migration to the clerk database.** Applied `20260907120000_RouteSessionAndRiderExceptions`, `20260907140000_TripEventBoardColumns`, and `20260907150000_StudentRideEligibilityAndSchoolYear` to `busbuddy_test` (14 student rows preserved). `RidesAm`/`RidesPm`/`SchoolYear` exist; schedule/transfer/family FKs are `RESTRICT`.
- [x] **Correct the 9 Bus 5 riders already imported under the old both-true default.** Migration backfill sets `RidesAm`/`RidesPm` from non-blank `AMRoute`/`PMRoute`, so AM-only Bus 5 riders read `RidesAm=true, RidesPm=false` without a re-import.
- [x] **Create the `AM Special Needs Bus 5` route row** so those riders pass `ValidateStudentAsync`: `dotnet run --project BusBuddy.DbPrep -- ensure-routes` (`ISeedDataService.EnsureRoutesForStudentAssignmentsAsync`).
- [ ] **Windows test pass.** `BusBuddy.Tests` targets `net9.0-windows` with `UseWPF`, so its testhost cannot start on macOS. The WPF-side Students tests (`StudentsListCoordinatorTests`, `StudentsViewModelTests`, `StudentFormViewModelSaveTests`, `StudentsViewTests`) compile but are unrun. Core-layer proof on macOS: 99/99 via an out-of-repo harness referencing `BusBuddy.Core`.
- [ ] **Route FK follow-up.** `AMRoute` / `PMRoute` remain `varchar(50)` names. Plan: add nullable `AmRouteId` / `PmRouteId`, backfill by name match, report unmatched names, move reads/writes to the FK while the name columns mirror for one release, then drop them. Needs the seed + DbPrep paths in the same pass.

### P1 — Unreachable Settings (district map config)

`SettingsView` / `SettingsViewModel` still write bus-barn + bbox via `IDistrictSettingsAccessor`. `MapView` reads that for depot camera (`DistrictDepot` / `DistrictCameraUi`). Spec: [maps.md](../specs/maps.md) clerk default ~38.0872, -102.6208 when nothing is selected; Settings is how a clerk replaces that.

- [x] **Wire Settings from the shell.** Header `ButtonAdv` `Click="SettingsButton_Click"` opens existing `SettingsView` (bus barn + bbox). Do not invent a second district-config form. (`feature/settings-shell-button`)
- [ ] **Proof:** VM: Settings → save depot lat/lng + bbox → Serilog district write → District Map recenters off the US-centroid fallback. Unit: `MainWindowClerkPathTests` asserts `Click="SettingsButton_Click"` (Mac cannot run WPF testhost).

### P2 — Unreachable Activity Timeline

- [ ] **Decide keep vs drop.** `ActivitiesButton_Click` (~888) hosts `ActivityTimelineView`, but no XAML button calls it. VMs are still registered in `App.xaml.cs`. This is an activity-log viewer, not the trip board (`TripEvent`). Either add an Activities header button, or delete the unused Click + consider whether the view stays for later.

### P2 — Startup helpers deleted in [#62](https://github.com/Bigessfour/BusBuddy-3/pull/62) (missing wiring, not proven abandoned)

Both types had **zero callers**; #62 deleted them as dead. They still read like startup that was never hooked. Restore from `938e052^` only if we intend to call them from `App` / `Program` — do not restore unused files.

- [ ] **`SyncfusionCultureFix.ApplyCultureFixes`** — set thread + `FrameworkElement.Language` to invariant culture to avoid Syncfusion XAML star-width parse issues. Restore path: `BusBuddy.WPF/Utilities/SyncfusionCultureFix.cs`. Call once at WPF startup **before** MainWindow XAML loads, or confirm `Program.cs` already covers that (VM smoke 2026-08-16 said no star-width crash). Do not force invariant culture if it would break clerk date/time display.
- [ ] **`StartupOptimizationService.PreloadCriticalServicesAsync`** — parallel `GetService` of Bus/Route/Driver/Schedule. Restore path: `BusBuddy.WPF/Services/StartupOptimizationService.cs`. Optional perf only; not a clerk-path blocker. If restored, register and await from startup; do not put it back as another orphan.

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
    - [x] Map route line hardening (2026-09-06): XAML-hosted `RouteTrail`, stop vs road split, select draws stored geometry, Routes on Refresh/Drive Path, `WaypointsJson` unbounded (`20260906220000_WidenRouteWaypointsJson`)
    - [x] Dead map-stack files removed (2026-09-06): Leaflet `map.html`, MapWinGIS stub, WebView2 package, hash `OfflineGeocodingService`, unused `TerrainAnalysisResult`; live-tracking chrome dropped (status line only); `GeoDataService` sample routes removed; MapView ctor DB ping removed
    - [x] District Map plot layers (2026-09-06): `PlotPickupStopsCommand`; student plot uses catalog stop GPS when present (PK + optional smaller HOME), else geocode/plot home only; auto-seed schools/pickups/students-with-coords on load; SCH/PK/HOME/DEPOT prefixes; depot + dual home/pickup pins; span zoom; persist stop-derived `WaypointsJson`
    - [x] District Map interaction fix (2026-09-07): pan/wheel/Zoom In/Out were dead because (a) `ImageryLayer.Radius` (bound to `MapFitRadiusKm`) re-solved zoom on every camera change with a 2.5x bounds inflation, and (b) Google tiles went through `ImageryLayer.UrlTemplate`, whose `async void` `HttpClient` download throws on 403/429 and leaves `isTileGenerationInProgress` stuck. `GoogleMapTilesImageryLayer` now overrides `GetUri` (guarded `BitmapImage` path, `CanCacheTiles=false`), `Radius` binding removed, `MapDefaults.ZoomForBounds` is a viewport-aware Web Mercator fit (`MapViewportSize` reported by `SizeChanged`), zoom clamped 1–19 to match the layer. Jumbled pins: `MapMarkerTemplateSelector` received `CustomDataSymbol` (not the marker) so everything used the stop template; now unwraps `.Data`. HOME/PK/waypoint captions hide below zoom 12 (`ShowDetailLabels`); schools/depots always captioned; tooltip keeps the full label. Verified on Mac via Core math script + static source-assertion scan (WPF testhost cannot run on macOS).
    - [x] Ship-readiness pass vs Syncfusion + Google docs (2026-09-07): (1) Geocoding fallback moved off the legacy `maps/api/geocode/json` (only documents `?key=`) to **Geocoding API v4** `GET geocode.googleapis.com/v4/geocode/address/{address}?regionCode=` with `X-Goog-Api-Key` + `X-Goog-FieldMask` (`GoogleAddressValidationClient.BuildGeocodeV4Uri` / `ParseGeocodeJson` read `results[].placeId|location.latitude|longitude|formattedAddress|granularity`; `DescribeGeocodeFailure` maps 403/404/429). (2) Map Tiles API **Policies** attribution: `IGoogleMapTileSessionService.GetViewportCopyrightAsync` calls `GET tile.googleapis.com/tile/v1/viewport?session&key&zoom&north&south&east&west` and `MapView` shows the returned `copyright` in `MapAttributionText` (750 ms debounce after pan/zoom, Google tiles only; static "Google Maps" until it lands). `MapDefaults.BoundsForViewport` is the inverse Mercator solve. (3) `ZoomForBounds` latitude axis was one level too far out (Mercator Y span is 2π, not π) — fixed; lon axis unchanged. Bugbot findings fixed: form save drops stale lat/lng when the address text changes (`_pinnedAddressKey`), incomplete-records SQL mirrors `IsIntakeIncomplete` (0,0 / centroid / SchoolYear / PickupStopId). Verified: `dotnet build` clean; `/tmp` harness named `BusBuddy.Tests` exercised the Core parsers/URIs/Mercator math (all pass); WPF strings static-asserted (`MapViewTests`, `MapViewModelTests`).
    - [x] **GCP `busbuddy-507301`** (2026-09-07): enabled `tile.googleapis.com` (Map Tiles), `places.googleapis.com` (New), `places-backend`, `geocoding-backend`, `apikeys` (note: Google's suggested `maptiles.googleapis.com` does not exist — the service is `tile.googleapis.com`). Key `BusBuddy Maps Platform` allowlist extended with `tile` + `places` (kept geocoding/addressvalidation/routes/maps-backend). Live smoke with the Passwords key: `createSession` 200, 2D tile 200 `image/png`, viewport `copyright` "Map data ©2026", Places autocomplete 200 (2 suggestions), Geocoding v4 200 `ROOFTOP` for a Wiley address. Remaining: confirm Google Road layer + attribution on the VM.
    - [ ] Map Tiles policy follow-up: the attribution overlay is text-only. Google's policy also asks for the **Google logo** next to the tiles — add the official logo asset from https://developers.google.com/maps/documentation/tile/policies (brand asset download; not vendored here) to `MapAttribution` bottom-left when Google tiles are active.
    - [x] District Map interaction trace (2026-09-07): `MapInteractionDiagnostics` (on via `Map:InteractionDiagnostics=true`, override `BUSBUDDY_MAP_DIAGNOSTICS=0|1`) records mouse down/up/wheel, key names + modifiers (no characters), SfMap `ZoomedIn/ZoomedOut/Panned`, ImageryLayer `ZoomLevelChanging/CenterChanged/MarkerSelected`, tile-request bursts (indices only — never the session URL) to `logs/map-interactions-.log` and a 30-entry ring buffer; dispatcher exceptions and WPF binding errors touching MapView/MapMarker/ImageryLayer/SfMap are logged with the preceding breadcrumbs. Attached on Loaded, re-attached after tab switch, disposed on Unloaded.
    - [ ] VM: District Map — drag pan, wheel zoom, Zoom In/Out, Center on Fleet, Reset all respond; county-wide plot fits the viewport; zoom past 12 reveals HOME/PK captions. After the session, pull `logs/map-interactions-*.log` (+ `logs/errors-actionable-*.log`) from the VM; any `ERROR` line carries the interaction breadcrumbs to reproduce. Set `BUSBUDDY_MAP_DIAGNOSTICS=0` (or config false) once the map is signed off.
    - [ ] Follow-up: drop unused `Route` shapefile path columns (empty `RemoveShapefileColumns` migration never dropped them); unused `AddressValidationControl`; OSM-only layer combo
    - [ ] VM: District Map opens on school GPS (or Settings depot/bbox — header **Settings** now opens that form); schools + PK pins + student homes/stops without three clicks; pick route with ≥2 geocoded stops → gold trail + Start/End pins only; **Refresh** optional for road path
    - [ ] Apply migration `20260906220000_WidenRouteWaypointsJson` on Mac Docker Postgres
    - [ ] Windows VM: `GOOGLE_MAPS_API_KEY` + `GCP_BILLING_PROJECT=busbuddy-507301` (Address Validation and/or Geocoding + Routes enabled) so Plot Students can geocode missing homes
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
- [x] SfMap mapping: OSM + school GPS / Settings bus barn / bbox centroid (US overview only when unconfigured). Earth Engine is not used.
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
| `SettingsView` / `SettingsViewModel`                     | P1   | Bindings + district write covered by `SettingsViewModelTests` / `MainWindowClerkPathTests` (shell Click now wired). **VM:** save depot/bbox then reopen District Map.           |

---

## Meta

- Re-check this file at the start of each session.
- After structural changes: update architecture map in `STEADY-STATE-AND-FINISH-ROADMAP.md` and re-index RAG.
- Do not put secrets here.

---

_Updated 2026-09-07: tracked unreachable Settings/Timeline shell buttons and #62 startup helpers that were deleted as unused callers._
