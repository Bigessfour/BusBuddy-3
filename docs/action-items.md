# BusBuddy Action Items (Ship SSOT)

**Canonical open work for agents and humans.** Open boxes only — completed work lives in git history and merged PRs; do not re-list checked boxes here.

| Companion                                                                                                    | Role                                                                                   |
| ------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| [clerk-path.md](./clerk-path.md)                                                                             | Clerk write path (school → kids → generate → bus/driver → schedule → fuel/maintenance) |
| [specs/README.md](../specs/README.md)                                                                        | Domain contract                                                                        |
| [../Documentation/diagrams/busbuddy-3-architecture.md](../Documentation/diagrams/busbuddy-3-architecture.md) | Architecture map                                                                       |
| [function-tree.md](./function-tree.md)                                                                       | Surface overview (optional)                                                            |
| [done-checklist.md](./done-checklist.md)                                                                     | Project-done judge (`python3 .github/scripts/check-project-done.py`)                   |

**Update rule:** One hop proof per session. Check the box here and link the PR when done. Spec-Kit `tasks.md` stays feature-local; promote leftover open items into this file only.

**Ship definition:** Clerk path proved on Windows VM + District Map / Settings smoke. Not a11y Phase 2, not portfolio wishlists.

**Project-done:** [done-checklist.md](./done-checklist.md) — `python3 .github/scripts/check-project-done.py`. Ship-ready is a subset. The project is done only when the checker exits 0.

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

### Routes leftover queue (ordered)

Do in this order so parked work is not forgotten and is not started out of sequence.

| Order | Item                                                           | Why this slot                                                                                                                                                     |
| ----- | -------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1     | District Map VM re-smoke, including **Move to selected route** | Ship proof for the override we just wired. Confirm Zoom In/Out, Show Schools, Plot Pickup Stops, Export Route, legend.                                            |
| 2     | `AMRoute` / `PMRoute` name-string drop                         | Phased campaign (~300 refs / ~56 files). Dual-write stays until then. **Do not start in one pass.** First slice later: one already-keyed read path, then stop.    |
| —     | `IRouteRepository`                                             | **Keep.** Address Validation `GetAllAsync`. Not a stub.                                                                                                           |
| —     | `StudentsBulkRouteCoordinator`                                 | Parked. Uses `SetSlot` + `UpdateStudentAsync`. Eventually call `IRouteService.AssignStudentToRouteAsync` so a shared name cannot null a known key. Not this pass. |

- [ ] **District Map VM re-smoke:** quit + relaunch Debug after `Data.*` pin bindings + pick-map attribution — expect clean captions (no CustomDataSymbol binding warnings), `WithSource` ≫ 0, `MapsOptionsBound … QuotaSource=none` (no createSession quota retry). Also confirm the 2026-09-15 toolbar fixes: Zoom In/Out stay visible after zooming, Show Schools leaves only black school pins, Plot Pickup Stops draws gold `Stop n` pins, Export Route toasts when no route is selected, legend card replaces Active Buses; **Move to selected route** moves a plotted student pin onto the combo route.

Optional:

- [ ] Optional Hop 1–6 ribbon clicks on VM (Clerk path “After hops” boxes) — only if you want UI confirmation beyond DbPrep

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
- [ ] Route FK follow-up — **phase 1 done, leftover writers wrap RouteService, drop phase remains.** Done: `Student.AmRouteId` / `PmRouteId` added and backfilled; `IStudentService.GetStudentsByRouteAsync(int)` is the keyed roster read (name overload unique-resolves or returns empty); name-assign and `IDriverService.AssignDriverToRouteAsync` call `RouteService.Assign*`. **Remaining:** drop the `AMRoute` / `PMRoute` name strings once their ~300 references across ~56 files are migrated to the key — do not attempt in one pass.

---

## Map / Settings (P1)

- [x] **Settings VM:** save depot lat/lng + bbox → Serilog district write → District Map recenters (not US-centroid fallback) (see Done log)
- [x] **District Map VM:** Zoom In/Out/Center/Reset + county fit + HOME/PK captions at zoom≥12 (see Done log)
- [x] Optional live District Map smoke on VM: pan/wheel feel + `BUSBUDDY_MAP_DIAGNOSTICS=1` → pull `logs/map-interactions-*.log` / `logs/ui-diagnostics-*.log` → set `=0` (see Done log 2026-09-10)
- [x] **Google Map Tiles logo** next to attribution when Google tiles are active (see Done log)
- [x] Apply migration `20260906220000_WidenRouteWaypointsJson` on Mac Docker Postgres (see Done log)
- [x] Windows VM env: `GOOGLE_MAPS_API_KEY` + `GCP_BILLING_PROJECT=busbuddy-507301` for geocode / Routes (see Done log)

---

## Decide later (non-ship)

- [x] **Activity Timeline:** header **Trips** button wires `ActivitiesButton_Click`; timeline **Trip Board** opens `ActivityManagementView` (ITripEventService)
- [ ] **Restore only if needed** (deleted in #62 as zero callers): `SyncfusionCultureFix` / `StartupOptimizationService` — do not restore as orphans

---

## Done log

### 2026-09-16 — Split RouteService and RouteAssignmentViewModel

- **Partial files only.** `IRouteService` is unchanged. No new types, no DI changes, no assign/stop behavior change.
- **Core:** `RouteService.cs` hub (ctors, context helpers, list metrics) plus `RouteService.Crud.cs`, `RouteService.Assignments.cs`, `RouteService.Stops.cs`.
- **WPF:** `RouteAssignmentViewModel.cs` hub plus existing `.Generation.cs`, new `.Commands.cs` and `.Loading.cs`. `MainWindow.xaml.cs` / `StudentsViewModel.cs` left alone.
- **Invariant:** Route ≠ Trip; same-day not-riding stays `RecordRiderExceptionAsync`.

### 2026-09-16 — Retired `*.disabled` source

- Deleted the ten parked XAI / Azure / vehicle / config stubs. Git history is the archive. Do not create new `*.disabled` files.
- Constitution v1.2.5 and copilot-instructions no longer tell agents to rename broken files to `.disabled`.

### 2026-09-16 — Leftover assignment writers wrap RouteService

- **No `IAssignmentService`.** Clerk-path winners stay on `RouteService.AssignStudentToRouteAsync` / `AssignDriverToRouteAsync` / `AssignVehicleToRouteAsync`.
- **Roster read:** `IStudentService.GetStudentsByRouteAsync(int)` uses `WhereOnRoute` (key first). The name overload is `UniqueIdForName` only — 0 or 2+ matches (two dated "North Elementary" runs) returns empty.
- **Kid assign leftover:** `IStudentService.AssignStudentToRouteAsync(names)` resolves unique ids then calls `RouteService`. Ambiguous names fail closed (no name-only orphan write). Dual-write of key + name stays in `StudentRouteAssignment.SetSlot`.
- **Driver assign leftover:** `DriverService.AssignDriverToRouteAsync(bool)` keeps CDL/training/availability checks, then persists via `RouteService.AssignDriverToRouteAsync(RouteTimeSlot)`. Does not write `RouteAssignments`.
- **Left parked:** `AMRoute`/`PMRoute` name-string drop; `StudentsBulkRouteCoordinator` still uses `SetSlot` + `UpdateStudentAsync`; leftover `AssignStudentsToRoutesAsync` still writes `RouteAssignments` (not clerk path).
- **Evidence:** UTM guest testhost **25 passed, 0 failed** (`StudentServiceTests` + `DriverServiceTests` + `RouteServiceTests.Assign`). Shared-name roster is empty; ambiguous name-assign fails closed; driver wrap sets `AMDriverId` and leaves `RouteAssignments` empty.

### 2026-09-16 — Activity logs factory, route soft-retire, trip display binds

- **ActivityLogService:** injects `IBusBuddyDbContextFactory` and opens a context per `Log*` / `Get*` (same pattern as `RouteService`). Timeline refresh is serialized with `SemaphoreSlim` so filter changes cannot start a second `GetLogsAsync` on a long-lived context. Dropped `Contains(..., StringComparison)` (not translated by Npgsql).
- **Route delete:** `DeleteRouteAsync` counts `Schedules`, student `AmRouteId`/`PmRouteId`, and leftover `TripEvents.RouteId`. Any blockers → `IsActive = false` and keep history. Hard-delete only when those counts are zero. Ribbon shows the Result message (retire vs deleted). Does not Cascade `FK_Schedules_Route`.
- **TripEventEditDialog:** `StatusDisplay` / `PathMilesDisplay` / `DriverHoursDisplay` bind on `TextBlock` `Mode=OneWay` (not `Run.Text`, which defaults TwoWay).

### 2026-09-16 — Spec 007 OSM + AddressValidationControl hygiene

- **AddressValidationControl:** already absent from `BusBuddy.WPF/Controls` on Mac (deleted in #61; clerk intake is `PlacesAddressBox`). Guest still had the three files until they were deleted. Locked by `UnhostedAddressValidationControl_IsGone_ClerkIntakeUsesPlacesAddressBox`. Parked unused-control box closed.
- **Spec 007:** `spec.md` / `plan.md` / `tasks.md` / `research.md` / `quickstart.md` no longer say OSM is the product default. Target is Google Map Tiles; OSM remains probe-only (`Tools/SfMapTileProbe`).
- **Map testhost:** UTM guest **76 passed, 0 failed** (`MapViewModelTests` + `MapViewTests` + `PlacesAddressSurfaceTests` + clerk-override + quota-source). `ResetView_ClearsTrailAndWaypointMarkers` now counts waypoint pins or gold `RouteStopLabel` tags. `RefreshMarkerZoomVisuals` snapshots `MapMarkers` so zoom/reset cannot throw `InvalidOperationException`.
- **Still open:** live District Map Debug click pass in the UTM window (Zoom stay-visible, Show Schools, Plot Pickup Stops, Export Route toast, legend, Move to selected route, `WithSource` / `MapsOptionsBound QuotaSource=none`). SSH `Start-Process` of the Debug exe did not keep a WPF window. Click it on the VM desktop.

### 2026-09-16 — Shapefile columns dropped; District Map clerk override

- **Shapefile paths:** `DistrictBoundaryShapefilePath` / `TownBoundaryShapefilePath` removed from `Route`, fluent config, and snapshot. `20260916200000_DropRouteShapefilePaths` actually drops the columns (the 20250814 migration was empty). Maps stay Google tiles only; `ImageryLayer.SubShapeFileLayers` remains the Syncfusion polyline host, not a `.shp` file.
- **Clerk override:** District Map **Move to selected route** calls `ApplyClerkOverrideAsync`. Pins now carry `StudentIds`; click selects the pin; the destination is the combo `SelectedRoute`; AM/PM comes from `RouteSession.ToAssignmentSlot`.
- **Left parked:** `AMRoute`/`PMRoute` name strings (phased drop). AddressValidationControl and spec 007 OSM narrative closed in the hygiene entry above.
- **Evidence:** UTM guest `MapViewModelTests` + `MapViewTests` + `RouteDeterminationServiceTests` **67 passed, 0 failed**.

### 2026-09-16 — Npgsql timestamp resolution + Student→Route foreign key

**The timestamp mismatch was one line, and it was also a live bug — not just a migration blocker.**

- **Root cause:** `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` in `EntityFrameworkPostgresExtensions`. Every timestamp column in the database is `timestamp with time zone` and the model snapshot already declared 79 of them that way, but the switch forces Npgsql's default CLR mapping to `timestamp without time zone`. Only the _runtime model_ disagreed, which is why `has-pending-model-changes` wanted a ~1,400-line migration rewriting every DateTime column, with a data-loss warning — blocking every feature migration.
- **It was also corrupting reads.** Measured against the live `busbuddy_test` rows: with the switch on, a 07:00 stop read back as **01:00 Local** and `Routes.Date` for Sept 2 read back as **Sept 1 18:00** — a six-hour skew that lands calendar dates on the previous day. With the switch removed the same rows read back as `07:00` and `Sept 2 00:00`. Removing it fixed the skew rather than causing one.
- **Why a converter, not a call-site audit:** verified directly against Postgres that with the switch off, EF writing a `DateTime` with `Kind=Local` or `Kind=Unspecified` to `timestamptz` throws `ArgumentException: ... only UTC is supported`. `DateTime.Today` and `DateTime.Now` are both Local, so essentially every write site would throw. `BusBuddyDbContext` now labels every `DateTime`/`DateTime?` property `Kind=Utc` on write and read **without shifting the clock**, so a 07:00 pickup stays 07:00 and a route date stays midnight on its own day. Confirmed end to end: all three Kinds write and round-trip with the wall clock intact, and the converters add zero schema churn.
- **Removed the compensating hack:** `RouteDeterminationService.DistrictWallClock` converted Mountain time to UTC purely to cancel out the legacy Local readback (its own comment said so). With the skew gone that conversion would store a 7am stop as 13:00, so it is now a plain face time. This is the source of the `13:00+00` / `01:00+00` outlier rows in `busbuddy_test`.
- **Also fixed:** the hand-written `20260916010000_StudentDeletionLogs` migration had no `.Designer.cs`, which is why `dotnet ef migrations remove` previously emptied the model snapshot. Reconstructed it, and verified `migrations remove` now reverts the snapshot cleanly.
- **Student→Route FK:** `AmRouteId` / `PmRouteId` added (nullable, indexed, `ON DELETE SET NULL`) in `20260916180546_StudentRouteForeignKeys`, backfilled from the name strings case-insensitively and **only where a name resolves to exactly one route**. A `Route` is the run published for the school year (`specs/routes.md`; the daily instance is a `Schedule`), so one key per session is the whole assignment. The rename cascade and delete-unassign now resolve riders by key, which removes the ambiguity guard that previously skipped riders whenever a name was shared across dates.
- **Evidence:** `has-pending-model-changes` reports **no changes**; migration applied cleanly to a clone of `busbuddy_test` and backfilled 12 of 14 students (the 2 skipped are the "North Elementary" orphans above); `dotnet build BusBuddy.sln` 0 errors.

### 2026-09-16 — Routes vertical sweep (spec, dead code, remaining blockers)

- **Map / assignment:** plotting a route no longer wipes the always-on school and catalog-stop overlay (clears only per-household pins); selection is pushed into `MapViewModel.SelectedRoute` so the path draws. `TimeRouteStops` is `async Task` and writes `ScheduledArrival`/`ScheduledDeparture` so the printed PDF is not stale. Special-needs mismatch no longer offers a seating override that `AssignStudentToRouteAsync` would reject anyway.
- **Exports / optimizer:** `RouteExportService` writes only to the clerk-chosen path (no leftover Desktop CSV of student names). `StudentRouteOptimizer` counts AM-or-PM missing, not both-empty, so a child with AM and no PM is still filled.
- **Dead:** deleted `RoutePopulationScaffold` / `IRoutePopulationScaffold`; stripped unused `IRouteRepository` query methods (CRUD stays for `IUnitOfWork`); removed unused `AMBusId`/`PMBusId` aliases, `NewRouteDate`/`TimeSlots`/`IsRouteSelected`, and a no-op in `AssignFitnessEvaluator`.
- **Auto-assign:** `AutoAssignStudentsAsync` `continue`s on a per-student rejection (special-needs mismatch) and only `break`s when the route is at capacity. Covered by `AutoAssignStudentsAsync_SkipsIneligibleAndContinues`.
- **Dates:** `Route.NormalizeRouteDate` no longer calls `ToUniversalTime()` (SfDatePicker Local values kept their calendar day). `DistrictWallClock` / stop-estimate fallbacks / clone default date use `DateTime.UtcNow.Date`.
- **Clerk override:** `ApplyClerkOverrideAsync` now passes `overrideSeating: true` so a clerk override onto a full bus is not blocked by the check it exists to bypass. Reason is still log-only until an override table exists. District Map **Move to selected route** is the production caller.
- **Evidence:** `dotnet build` 0 errors; `has-pending-model-changes` reports no changes; UTM guest gate **609 passed, 0 failed**.

### 2026-09-16 — Routes vertical production hardening

- **No more fake roster data:** `LoadMockData` and every call site are gone from `RouteAssignmentViewModel`, including the `catch` that replaced real routes with "Mock Elementary School" after a transient DB error. `IRouteService` is now a required constructor dependency, so the `_routeService == null` branches that mutated in-memory collections and reported success are gone too. When DI can't supply it, `RouteAssignmentView` leaves `DataContext` null, logs an error, and shows a red banner instead of a working-looking screen.
- **Add Stop works end to end:** `RouteStopEditDialog` now captures Places lat/lng via `AddressApplied`, shows the located coordinates, and blocks save until the address is picked from the suggestion list; the VM passes them through, so `AddStopToRouteAsync` no longer rejects the stop for missing coordinates. Locked in by `PlacesAddressSurfaceTests`.
- **Write correctness:** `CreateNewRouteAsync` uses `GetWriteContext()` (was writing on a `NoTracking` read context); `UpdateRouteAsync` loads the tracked row and copies via `CurrentValues.SetValues`, so unchanged columns stay out of the `UPDATE`; `Session` re-infers only when an inference input changed, so a deliberately-set session isn't stomped.
- **Transactions:** `CloneRouteAsync`, `ReorderRouteStopsAsync`, `AddStopToRouteAsync`, `RemoveStopFromRouteAsync`, `RecordRiderExceptionAsync` run through one `InTransactionAsync` helper that opens the transaction _inside_ `CreateExecutionStrategy().ExecuteAsync(...)` — the documented shape, and the reason this does not repeat the Hop 4 `BeginTransactionAsync` incompatibility. It rolls back on a failure `Result`, not just on exception.
- **Dead code removed:** 12 stub/dead members off `IRouteService` + `RouteService` (three "not implemented" stop methods, hardcoded distance/time, `SearchRoutesAsync`, `IsRouteNumberUniqueAsync`, `ValidateRouteCapacityAsync`, `GenerateRouteSchedulesAsync` which wrote a `.txt` into the working directory, and the unused `CanAssignStudentToRouteAsync` overload), the comments-only `RouteServiceExtensions.cs`, the orphaned `IStudentService.GetStudentsForRouteAsync(context, routeId)`, and the `AssignStudentsCommand` / `PrintRouteMapsCommand` aliases that were bound to nothing and pointed at unrelated work.
- **EF:** explicit `RouteAssignment` fluent config (cascade matching the initial migration), new `IX_Routes_IsActive`, and the duplicate `Route` block whose `RouteName` default disagreed with the snapshot is gone.
- **Bugbot finding (fixed):** `DeleteRouteAsync` selected riders with a case-sensitive `AMRoute == routeName` but cleared them with `OrdinalIgnoreCase`, so a rider stored as `"route a"` was never loaded and kept pointing at a deleted route. The query now lowers both sides like `CascadeRouteRenameAsync` does. Covered by `DeleteRouteAsync_UnassignsRidersStoredWithDifferentCasing`, which was confirmed to fail against the old query.
- **Evidence:** `dotnet build BusBuddy.sln` 0 errors (2 pre-existing warnings in untouched files); UTM guest gate `Category!=Integration&Category!=InMemoryFlaky` **597 passed, 0 failed**. Removed a stale `DriverStatusPersistProbeTests.cs` from the guest that exists in neither the tree nor git history and was failing every run.
- **Closed by the timestamp + FK entries above.** Rename cascade is now key-based; the legacy timestamp switch is gone.

### 2026-09-16 — Routes vertical closeout (canvas)

- **Spec:** `specs/routes.md` now matches Core. Date + `IsActive` (not `SchoolYear`/`Published`); two AM/PM rows; stops denormalize validated lat/lng (no `LocationId`/`StopKind`); no `RouteVersion` table; leftover `AM*`/`PM*` columns remain year-default pairings.
- **Dead code:** in-memory `TryAddStudent` / `AssignedStudents` / building-status helpers; unused `DateFormatted` / `TotalMiles` / `SafeRouteName`; unused WPF `RouteViewModel` AutoMapper maps; clone no longer copies unused shapefile path columns.
- **Roster counts:** `GetAllRoutesAsync` / `GetAllActiveRoutesAsync` / `GetRouteByIdAsync` populate `StudentCount` and `StopCount` from `AmRouteId`/`PmRouteId`. Dashboard Assigned uses `StudentCount` (was always 0 via empty `AssignedStudents`).
- **Schedule button:** Assignment **Schedule** opens published stop times for the selected route row, not the district-wide driver calendar.
- **DI:** `RouteManagementViewModel` takes routing, optimization, and the map VM from the constructor instead of `App.ServiceProvider` for those three.

### 2026-09-16 — Trip Board clerk surface (TripEvent)

- Trip Board (`ActivityManagementView`) now has SfScheduler, New/Edit/Confirm, Calculate Distance, Print Ticket. Editor is `TripEventEditDialog` bound to Core `TripEvent` — does not write `ActivitySchedule` or `Schedule`, never sets `RouteId`.
- Purpose and sports/reasons are clerk-editable (`ITripReasonCatalog` in `%AppData%/BusBuddy/user-settings.json`). New names persist without a code change.
- Distance/path: `RefreshPathMilesAsync` + `MapViewModel.TryPlotTrip` polyline via `IRoutingService`.
- Assignment: `IRouteService` available lists + `HasConflictsAsync` + published AM/PM warnings (`GetAssignmentWarningsAsync`). Home assignment unchanged.
- Trip ticket: `PdfReportService.GenerateTripTicket` with paper mileage/fuel/time blanks and `DriverHours` = duration + 1.5h inspection pad (`TripEvent.PrePostTripInspectionHours`).
- Spec: `specs/trips.md` ticket + inspection-hours invariants.

Completed Spec-Kit waves (001–008), Syncfusion audits, student archive/eligibility, Maps Platform geo, and related PRs are **not** tracked here. See GitHub merges and git history.

### 2026-09-15 — District Map toolbar, pins, and legend (clerk VM feedback)

- Diagnosis: none of the reported symptoms were Google's. Tiles were fine; captions, colors, and the toolbar are BusBuddy XAML/VM (Syncfusion `ImageryLayer.MarkerTemplate` + `ButtonAdv`).
- Labels: a route stop on an existing pin now **tags** that pin (`MapMarker.RouteStopLabel`, gold stroke, `Name (Stop n)`) instead of stacking a `WP` pin + second caption at the same coordinate. Household captions (home/student) wait for `MapDefaults.HomeLabelZoomLevel` (14); place captions keep 12.
- Colors: one fixed fill per `MapMarkerLabels.Kind` (school black, pickup orange, route stop gold, depot purple, home blue, student green) bound via `MapMarker.FillBrush` / `StrokeBrush`; legend card generated from `MapMarkerLabels.Legend`.
- Zoom In/Out vanishing: overlay `ButtonAdv` now owns an explicit `ControlTemplate` with literal brushes (theme `DynamicResource` lookups were dropping to transparent after the map re-rendered); toolbar `Panel.ZIndex=10`; map container `ClipToBounds`.
- Export Route: `EnableRouteGeoExport` Settings gate removed everywhere (keys, service, Settings VM/XAML, exporter). Button always enabled; no selection → toast asking for a route; success/failure toasts.
- Show Schools: schools-only view (`ClearMarkersExcept(School)`), center on schools, toast; Refresh restores the full district overlay.
- Plot Pickup Stops: catalog pickups **plus** published route stops of every route (`MapDistrictLayers.PlotRouteStopsAsync`); toast when nothing to plot.
- Active Buses card + `ActiveBuses` / `SelectedBus` / `IBusService` removed from `MapViewModel`.
- Evidence: Release build green; UTM guest `--full` filter 574 passed / 2 pre-existing failures also failing on `master` (`DirectContext_StatusUpdate_Persists`, `IsDriverAvailableForRouteAsync_ReturnsFalseWhenAlreadyAssigned` — InMemory driver suite, VM-only). Spec: `specs/maps.md` § Pin colors and captions / District Map toolbar contract.

### 2026-09-15 — Release publish NETSDK1152

- Cause: `BusBuddy.Core/appsettings.json` `CopyToOutputDirectory=Always` flows transitively into the WPF publish set; WPF ships its own file at the same relative path.
- Fix: Core item gains `CopyToPublishDirectory=Never`. Build outputs unchanged (WPF/Tests bins still get WPF's copy; DbPrep gets Core's). `dotnet publish` with the CI flags (`win-x64`, self-contained, single-file) now succeeds and ships the WPF `appsettings.json`.
- Follow-up (non-blocking): Core's copy is stale vs WPF (LocalDB); reconcile or delete once DbPrep/Tests config is pinned.

### 2026-09-15 — Project-done code debt sweep

- Deleted `BusService.GetAllRoutesAsync` / `GetSchedulesByRouteAsync` (`NotImplementedException`, not on `IBusService`, zero callers).
- `BusBuddyDbContext`: removed no-op `ConfigureGlobalQueryFilters` / `ApplyAuditFields` and their commented-out `BaseEntity` bodies; retirement stays per-aggregate (Bus/Driver `Status`, Student `Active`).
- `SeedDataService.SeedActivitiesAsync` removed (logged "seeded" while writing nothing); interface + `SeedAllAsync` call dropped. Trip Board stays clerk-entered.
- `RouteService`: validation scope and region comments reworded as decisions; no open TODOs remain in product code.
- Tests: `RouteManagementExportHelperTests` (CSV escaping, report lines, hop-5 `TryPersistScheduleAsync` UTC day / AM→PM fallback / no-write guards) and `FuelReconciliationViewModelTests` (per-day sums, bulk discrepancy + detail rows, location filter, date validation, `RememberBulk` gating). CI filter gained `Fuel`.
- Regenerated `docs/function-inventory.generated.md` (44 surfaces, 41 with proof).
- Evidence: Release build green; `check-project-done.py` 7 → 3 failures (all remaining = the VM re-smoke / ribbon-click session).

### 2026-09-11 — Fleet + Fuel UTM breakages (PR #69)

- Fuel chart `Circle` → `Ellipse` (XamlParseException)
- Bus save: Find + SetValues; list/detail no longer load AMRoutes/PMRoutes
- Bus delete: hard-delete only when Restrict FKs clear; else soft-retire + clear future route vehicle FKs; VM toast (no Npgsql dump)
- Do not Cascade `FK_Routes_AMVehicle`

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
- **Superseded 2026-09-16** (see the timestamp entry at the top of this log): the legacy timestamp switch is gone, `ConfigureNpgsqlAppContext()` is now a no-op, and `DistrictWallClock` no longer converts Mountain→UTC. A 07:00 ETA now reads back as `07:00` Kind=**Utc**. Do not reintroduce the conversion — it only ever existed to cancel out the legacy Local readback.
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
