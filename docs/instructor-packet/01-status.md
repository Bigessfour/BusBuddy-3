# BusBuddy-3 — Development status

_As of `master` 2026-09-15. Two judges apply: **ship-ready** (the clerk can do her job) and **project-done** (nothing unexplained is left in the tree). They give different answers on purpose._

## Ship-ready: yes

Definition (from `docs/action-items.md`): clerk hops 1–6 proved end to end on Postgres, and the WPF app runs on a Windows VM with the District Map and Settings smoke-tested.

| Criterion                                          | Evidence                                                                                                                                                                                                                                                                               |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Hops 1–6 write the intended rows                   | `BusBuddy.DbPrep` harness against Docker Postgres, one hop per session, row IDs and Serilog lines recorded in the Done log (2026-09-09)                                                                                                                                                |
| Canonical write chosen where two tables overlapped | Bus/driver on a route = `Routes.AMVehicleId / AMDriverId`; `RouteAssignments` kept in schema, not written (Hop 4b)                                                                                                                                                                     |
| WPF runs on Windows                                | UTM guest test host: Students suites 27/27; Fuel/Maintenance/RouteManagement 15 passed; RouteManagement + CI-blocker filters 19 passed (2026-09-09 to 09-11)                                                                                                                           |
| District Map live                                  | Google Map Tiles session, pan/zoom/center/reset, depot and bounding box from Settings (2026-09-10); per-kind pin colors + legend, route-stop tagging on shared pins, schools-only filter, pickup/route-stop plotting, always-on GeoJSON export, Active Buses card removed (2026-09-15) |
| Runtime error log worked down                      | VM `runtime-errors.log` cascades fixed: `SfMap.OnMouseMove` NRE, `TransformToVisual` during marker template inflation, `Circle`→`Ellipse` XAML, bus/driver soft-retire on FK conflicts (PRs #69, #74)                                                                                  |
| CI                                                 | `Build & Test` required on `master`; last four PRs (#71, #73, #74, #75) merged green                                                                                                                                                                                                   |

## Project-done: no

`python3 .github/scripts/check-project-done.py` on 2026-09-15:

```text
Verdict: NOT DONE
Failed: 3 · Deferred: 8 · Passed: 25 · N/A: 2
```

The morning of 2026-09-15 the count was 8. Five were closed the same day and are kept in the table so the trail is visible.

### The failures, plainly

| Check              | What it found                                                                                 | Status                                                                                                        |
| ------------------ | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| B01 stubs          | Two `NotImplementedException` in `BusService.cs`                                              | Closed 09-15: deleted (not on `IBusService`, zero callers).                                                   |
| B02 TODOs          | Six `TODO` comments in `BusBuddyDbContext`, `SeedDataService`, `RouteService`                 | Closed 09-15: no-op audit/soft-delete hooks and the empty activity seeder removed; two reworded as decisions. |
| K02 tests          | `RouteManagementExportHelper` and `FuelReconciliationViewModel` have no test file naming them | Closed 09-15: behavioral tests added for both; CI filter widened to run the Fuel suite.                       |
| B12 inventory      | Generated function inventory is 20 surfaces behind `.function-inventory.json`                 | Closed 09-15: regenerated (44 surfaces, 41 with proof).                                                       |
| B11 docs drift     | Constitution §VI still listed CodeQL as a merge gate after it was removed for a private repo  | Closed 09-15.                                                                                                 |
| B14 UI proof       | Optional ribbon-click confirmations for hops 1–5 on the VM are unchecked                      | **Open.** Same service methods are proved from the harness; the UI clicks are belt-and-braces.                |
| K09 manual QA      | District Map re-smoke after the latest pin-binding change is unchecked                        | **Open.** Needs one VM session.                                                                               |
| B10 blocking boxes | The open boxes above, plus a parked `AmRouteId`/`PmRouteId` FK cleanup                        | **Open.** Aggregate of the two rows above.                                                                    |

### The 8 deferrals (explained, allowed)

Listed in `docs/done-catalog.json` with a reason each:

- `IFamilyService` / `IGuardianService` not in DI — Families/Guardians tables exist but have no clerk surface yet.
- `SchoolCalendar` and `AIInsight` DbSets unused — schema kept, no UI.
- `RouteAssignments` table retained but not written by Assign Vehicle/Driver — Hop 4b decision.
- Unused shapefile path columns on `Route`; unused `AddressValidationControl`; historical OSM narrative in spec 007.
- `SyncfusionCultureFix` / `StartupOptimizationService` deleted as zero-caller; restore only if needed.

## Known design debts (self-reported)

These do not fail the checker but a reviewer will notice them.

| Debt                                                    | Where                                                                               | Position                                                                                                                                                                          |
| ------------------------------------------------------- | ----------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MainWindow.xaml.cs` is 1,471 lines                     | `BusBuddy.WPF/Views/Main/`                                                          | Ribbon and dock chrome plus one `*_Click` per header button. Splitting is deliberately deferred until after ship; each handler is now a two-line open-window + `UiProofLog` call. |
| `StudentsViewModel.cs` is 1,029 lines                   | `BusBuddy.WPF/ViewModels/Student/`                                                  | Already decomposed into ten coordinator classes (`StudentsCsvCoordinator`, `StudentsMapCoordinator`, …); the remaining file is orchestration.                                     |
| Three storage paths for "kid on a route"                | `Student.AMRoute`/`PMRoute` strings, `Student.RouteId`, `Student.RouteAssignmentId` | Known. Canonical is the string pair today; nullable FK backfill is the next schema hop.                                                                                           |
| Both `Bus` and legacy `Vehicle` naming                  | `Vehicles` table, `Bus` model                                                       | Migration `RenameVehicleIdToBusId` moved the key; the table name stayed to avoid a destructive rename.                                                                            |
| Static XAML contract tests                              | `BusBuddy.Tests/WPF/*Tests.cs`                                                      | CI runs on a Windows box without a desktop session, so UI wiring is asserted by reading XAML. Behavioral coverage lives in Core service tests against Postgres.                   |
| Several Syncfusion packages referenced but lightly used | `BusBuddy.WPF.csproj`                                                               | Chat, Spreadsheet, RichTextBox, NavigationDrawer are pulled by the theme bundle; trimming is a post-ship task.                                                                    |

## CI health

| Item           | State                                                                                                                                                                                                                                                                                                                                                                    |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Required check | `Build & Test` (restore, build, `dotnet test` with `Category!=Integration&Category!=InMemoryFlaky`)                                                                                                                                                                                                                                                                      |
| Merge policy   | Squash auto-merge when green; `master` protected, no force-push, no direct push                                                                                                                                                                                                                                                                                          |
| Release job    | Non-blocking self-contained `win-x64` publish, run by `workflow_dispatch` on `master`. Failed with `NETSDK1152` (Core's `appsettings.json` flowed into the WPF publish set) until 2026-09-15; Core's copy is now `CopyToPublishDirectory=Never`. The job also used to be `push`-only, which bot-attributed auto-merges never fire, so it had not run at all since 09-12. |
| Dependabot     | 13 open alerts, all in `package-lock.json` for the Trunk lint launcher (`tar`, `yaml`). None are .NET app dependencies.                                                                                                                                                                                                                                                  |
| Code scanning  | None. GitHub CodeQL requires a paid Code Security license on private repos.                                                                                                                                                                                                                                                                                              |

## What is next

1. One Windows VM session: District Map re-smoke and hops 1–5 ribbon clicks, pull `ui-diagnostics-*.log`, check the boxes. This is the only thing standing between the checker and exit 0.
2. Nullable `AmRouteId`/`PmRouteId` FK backfill (parked; the string pair is canonical today).
3. Reconcile `BusBuddy.Core/appsettings.json` with the WPF copy (it still carries LocalDB and the pre-Ollama xAI section; only DbPrep and the test host read it, and both take connection strings from the environment first).

## Out of scope by decision

- Parent or driver mobile app; live GPS; cloud hosting; multi-district tenancy.
- Accessibility Phase 2 (WCAG audit beyond current `AutomationProperties` coverage).
- Replacing Syncfusion with open-source controls.
