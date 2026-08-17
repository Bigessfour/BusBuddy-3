# BusBuddy Action Items (Due-Outs Tracker)

**Canonical due-outs file for agents and humans.**
**Historical finish narrative:** [STEADY-STATE-AND-FINISH-ROADMAP.md](../STEADY-STATE-AND-FINISH-ROADMAP.md)
**Spec-Kit features:** [specs/](../specs/) (each feature may also have `tasks.md`)
**Open GitHub issues:** https://github.com/Bigessfour/BusBuddy-3/issues
**Generated inventory (optional):** run function-inventory scanner → `docs/function-inventory.generated.md`
**Visual tree (optional):** [function-tree.md](./function-tree.md)

**Update rule:** When starting or finishing work, check boxes here and link the PR/spec. Prefer this file for “what’s left”; use Spec-Kit `tasks.md` for implementation steps inside a feature.

---

## Priorities (now)

### P0 — Platform / tooling

- [x] Spec-Kit brownfield bootstrap (001–005) — merged [PR #20](https://github.com/Bigessfour/BusBuddy-3/pull/20)
- [x] **007 Maps Platform Geo (retire Earth Engine)** — US2+docs merged [PR #31](https://github.com/Bigessfour/BusBuddy-3/pull/31) · [spec](../specs/007-maps-platform-geo/spec.md) · [tasks](../specs/007-maps-platform-geo/tasks.md)
  - [x] US2: Remove GEE DI, client, probe, unofficial Google tiles
  - [ ] US1: Address Validation + geocode onto SfMap (**paused**)
  - [ ] US3: Routes API drive polyline (P2)
  - [ ] US4: Places type-ahead (P3, optional)
  - [x] Docs for US2 + pause (constitution v1.1.0; Maps clients not wired)
- [x] **006 Syncfusion Tool Integration** — [spec](../specs/006-syncfusion-tool-integration/spec.md) — merged [PR #21](https://github.com/Bigessfour/BusBuddy-3/pull/21)
  - [x] MCP paths, skills overlay, Syncfusion **34.2.3**, deps audit
  - [x] `python -m rag.index` after merge (2026-07-24; ~3399 chunks)
  - [x] Windows VM Syncfusion license + UI smoke — checklist: [windows-vm-smoke.md](../specs/006-syncfusion-tool-integration/windows-vm-smoke.md) — **2026-08-16:** license registered early in `Program.cs`; MainWindow DockingManager loaded (no trial dialog / no star-width XAML crash)
  - [x] P2: AutoMapper 12 → **15.1.1** (GHSA-rvv3-g6hj-g44x)

### P1 — Finish / domain (Spec-Kit wave 2) — aligns with [issue #11](https://github.com/Bigessfour/BusBuddy-3/issues/11)

- [x] Student import / optimize end-to-end (UI + SeedDataService + tests)
  - [x] CSV import wired: `ISeedDataService.ImportStudentsFromCsvAsync` + Students/StudentForm Import CSV buttons (Wiley-format file picker). Proof: parent address columns, next `WSD` number, Wiley header rejection, form import refreshes list via `StudentsImportedMessage`
  - [x] Optimize routes: `IStudentRouteOptimizer` fills active routes via `IRouteService.AutoAssignStudentsAsync`, then Ollama/`GrokGlobalAPI` commentary (mock fallback). Wired on Students + Dashboard. Proof: `StudentRouteOptimizerTests`
- [x] Reports: `IOperationalReportService` writes live PDFs/CSVs via `PdfReportService.GenerateTabularReport` + Ollama/`GrokGlobalAPI.GetShortCommentaryAsync` (mock fallback). All Reports buttons + Dashboard Generate Report. Proof: `OperationalReportServiceTests`, `PdfReportServiceTests.GenerateTabularReport_ReturnsValidPdf`. Merged [PR #24](https://github.com/Bigessfour/BusBuddy-3/pull/24). CLI `--generate-report` uses the same service (aliases: Roster, RouteManifest, StudentList, DriverSchedule)
- [x] Driver availability + SfScheduler — `DriverAvailabilityCalculator` uses Schedule rows; `DriverScheduleView` binds SfScheduler; `Schedule_Click` opens it
  - Serilog proof: `Driver availability calculated` / `Driver schedules loaded Appointments=`
- [x] Maintenance UI polish — `MaintenanceView` + `IMaintenanceService` CRUD; `Maintenance_Click` opens it
  - Serilog proof: `Maintenance UI loaded Records=` / `Created maintenance record`
- [x] Google Earth Engine enhancements (beyond current DI/auth) — **superseded by 007**: EE is the wrong product for addresses/trips; see [007 Maps Platform Geo](../specs/007-maps-platform-geo/spec.md)
  - Historical: shared map VM + `IGeocodingService` + SfMap plot (hash geocoder / EE client remain until 007 lands)
- [x] SfMap mapping: official OSM + Wiley center/zoom, Syncfusion string markers, shared map VM, live routes/buses (not sample-only)
- [x] End-to-end student → assign → report proof test — `BusBuddy.Tests/Core/RouteAssignmentFlowTests.cs` (SeedDataService → StudentService → RouteService → PdfReportService). **UTM Windows VM 2026-08-16:** `Total tests: 1`, `Passed: 1` (built from `C:\dev\BusBuddy-3` after Z:\ sync). Mac host cannot execute WPF testhost; use `./run-wpf.sh` + `utm_run_in_vm.ps1` for GUI.

### P2 — Hygiene / quality

- [x] Bootstrap function-inventory generated scan — `.function-inventory.json` (16 surfaces) → [function-inventory.generated.md](./function-inventory.generated.md) (2026-08-16: 11/16 with proof)
- [x] Resolve LFS/chroma noise — stop tracking `rag/chroma_db/` (~54MB sqlite blobs that triggered GH001); drop `*.pdf`/`*.sqlite` LFS attrs (PDF stays git binary). History purge of old blobs still needs force-push approval.
- [x] AutoMapper 12.x advisory — upgraded to 15.1.1
- [x] Restore [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](../Documentation/GCP-GEE-SECRETS-AND-AUTH.md) (was missing; AGENTS link)

### GitHub issues triage (open as of 2026-07-24)

| Issue                                                     | Topic                                           | Suggested disposition                                                                                      |
| --------------------------------------------------------- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| [#13](https://github.com/Bigessfour/BusBuddy-3/issues/13) | ViewModel dedup                                 | **Closed** (hygiene / PR #16)                                                                                  |
| [#14](https://github.com/Bigessfour/BusBuddy-3/issues/14) | CI + secrets/MCP                                | Solo CI + auto-merge done; Passwords for Syncfusion/Maps. Optional: GH Actions secrets / `ci-with-ai` cleanup → **close or narrow** |
| [#15](https://github.com/Bigessfour/BusBuddy-3/issues/15) | Deprecate bb-* PS                               | Modules removed — open [PR #32](https://github.com/Bigessfour/BusBuddy-3/pull/32) (`Closes #15`)              |
| [#11](https://github.com/Bigessfour/BusBuddy-3/issues/11) | Close stubs (Reports/Grok/Settings/Maintenance) | P1 done ([PR #30](https://github.com/Bigessfour/BusBuddy-3/pull/30)); Drivers MVP placeholders wired to live reports/services — close via follow-up PR |

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

| Surface                                                        | Tier | Proof / next check                                                                                                                                                                                                         |
| -------------------------------------------------------------- | ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Student / Seed / Route / Optimizer / Reports services          | P1   | Unit tests present (`StudentServiceTests`, `SeedDataServiceTests`, `RouteServiceTests`, `StudentRouteOptimizerTests`, `OperationalReportServiceTests`, `PdfReportServiceTests`)                                            |
| `StudentsView` / `ReportsView`                                 | P1   | No WPF testhost on Mac. Proof is VM smoke (`./run-wpf.sh`) + the Core tests above. Do not treat as a missing feature.                                                                                                      |
| `ScheduleService`                                              | P1   | **Runtime proof via Serilog:** `GetSchedulesAsync` logs count + elapsed. SfScheduler UI: `DriverScheduleViewModel` / `DriverAvailabilityService` log appointment and 14-day availability summaries. Unit tests still open. |
| `DriverService`                                                | P1   | `DriverServiceTests` exist; `DriverScheduleView` + `DriverAvailabilityCalculator` (Schedule + ActivitySchedule). Availability calc logs `Drivers=` / `WithOpenDays=`                                                       |
| `MaintenanceService` / Dashboard metrics / theme manager | P2   | `MaintenanceService` logs CRUD. Dashboard: `DashboardViewModel` logs refresh/optimize/report. Earth Engine retired (spec 007). |
| `DashboardView` / `GeoDataService`                       | P2   | VM smoke + Serilog: `Dashboard refresh completed` / `Loaded routes with geo data` |

---

## Meta

- Re-check this file at the start of each session.
- After structural changes: update architecture map in `STEADY-STATE-AND-FINISH-ROADMAP.md` and re-index RAG.
- Do not put secrets here.

---

*Updated 2026-08-17: P1 stubs closed (availability, maintenance UI, activity persist/conflicts, map scope/dispose). Close issue #11 after merge.*
