# BusBuddy-3 — Development status

_As of `master` 2026-09-15. Two judges apply: **ship-ready** (the clerk can do her job) and **project-done** (nothing unexplained is left in the tree). They give different answers on purpose._

## Ship-ready: yes

Definition (from `docs/action-items.md`): clerk hops 1–6 proved end to end on Postgres, and the WPF app runs on a Windows VM with the District Map and Settings smoke-tested.

| Criterion                                          | Evidence                                                                                                                                                                                              |
| -------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Hops 1–6 write the intended rows                   | `BusBuddy.DbPrep` harness against Docker Postgres, one hop per session, row IDs and Serilog lines recorded in the Done log (2026-09-09)                                                               |
| Canonical write chosen where two tables overlapped | Bus/driver on a route = `Routes.AMVehicleId / AMDriverId`; `RouteAssignments` kept in schema, not written (Hop 4b)                                                                                    |
| WPF runs on Windows                                | UTM guest test host: Students suites 27/27; Fuel/Maintenance/RouteManagement 15 passed; RouteManagement + CI-blocker filters 19 passed (2026-09-09 to 09-11)                                          |
| District Map live                                  | Google Map Tiles session, pan/zoom/center/reset, HOME/PK captions, depot and bounding box from Settings (2026-09-10)                                                                                  |
| Runtime error log worked down                      | VM `runtime-errors.log` cascades fixed: `SfMap.OnMouseMove` NRE, `TransformToVisual` during marker template inflation, `Circle`→`Ellipse` XAML, bus/driver soft-retire on FK conflicts (PRs #69, #74) |
| CI                                                 | `Build & Test` required on `master`; last four PRs (#71, #73, #74, #75) merged green                                                                                                                  |

## Project-done: no

`python3 .github/scripts/check-project-done.py` on 2026-09-15:

```text
Verdict: NOT DONE
Failed: 7 · Deferred: 8 · Passed: 21 · N/A: 2
```

(An eighth failure, B11 docs drift, was fixed in the PR that produced this packet; it is left in the table for honesty.)

### The failures, plainly

| Check              | What it found                                                                                 | Assessment                                                                           |
| ------------------ | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| B01 stubs          | Two `NotImplementedException` in `BusService.cs` (lines 1004, 1014)                           | Real leftover. Should be removed or implemented.                                     |
| B02 TODOs          | Four `TODO` comments in `BusBuddyDbContext`, `SeedDataService`, `RouteService`                | Comment debt, not runtime risk. Either do them or delete them.                       |
| K02 tests          | `RouteManagementExportHelper` and `FuelReconciliationViewModel` have no test file naming them | Coverage gap on two helpers.                                                         |
| B12 inventory      | Generated function inventory is 20 surfaces behind `.function-inventory.json`                 | Regenerate; a doc, not code.                                                         |
| B11 docs drift     | Constitution §VI still listed CodeQL as a merge gate after it was removed for a private repo  | Fixed 2026-09-15; now passes.                                                        |
| B14 UI proof       | Optional ribbon-click confirmations for hops 1–5 on the VM are unchecked                      | Same service methods are proved from the harness; the UI clicks are belt-and-braces. |
| K09 manual QA      | District Map re-smoke after the latest pin-binding change is unchecked                        | Needs one VM session.                                                                |
| B10 blocking boxes | The open boxes above, plus a parked `AmRouteId`/`PmRouteId` FK cleanup                        | Aggregate of the rows above.                                                         |

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

| Item           | State                                                                                                                                                                          |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Required check | `Build & Test` (restore, build, `dotnet test` with `Category!=Integration&Category!=InMemoryFlaky`)                                                                            |
| Merge policy   | Squash auto-merge when green; `master` protected, no force-push, no direct push                                                                                                |
| Release job    | Non-blocking self-contained `win-x64` publish on `master` push. Currently fails with `NETSDK1152` (duplicate `appsettings.json` from Core + WPF). Known; does not gate merges. |
| Dependabot     | 13 open alerts, all in `package-lock.json` for the Trunk lint launcher (`tar`, `yaml`). None are .NET app dependencies.                                                        |
| Code scanning  | None. GitHub CodeQL requires a paid Code Security license on private repos.                                                                                                    |

## What is next

1. One Windows VM session: District Map re-smoke and hops 1–5 ribbon clicks, pull `ui-diagnostics-*.log`, check the boxes.
2. Remove the two `NotImplementedException` stubs and four `TODO`s; add tests naming the two uncovered helpers; regenerate the function inventory.
3. Fix the release publish (`appsettings.json` collision) so `master` push CI is green end to end.
4. Then `check-project-done.py` should exit 0.

## Out of scope by decision

- Parent or driver mobile app; live GPS; cloud hosting; multi-district tenancy.
- Accessibility Phase 2 (WCAG audit beyond current `AutomationProperties` coverage).
- Replacing Syncfusion with open-source controls.
