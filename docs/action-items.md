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
- [x] **006 Syncfusion Tool Integration** — [spec](../specs/006-syncfusion-tool-integration/spec.md) — merged [PR #21](https://github.com/Bigessfour/BusBuddy-3/pull/21)
  - [x] MCP paths, skills overlay, Syncfusion **34.1.32**, deps audit
  - [x] `python -m rag.index` after merge (2026-07-24; ~3399 chunks)
  - [x] Windows VM Syncfusion license + UI smoke — checklist: [windows-vm-smoke.md](../specs/006-syncfusion-tool-integration/windows-vm-smoke.md) — **2026-08-16:** license registered early in `Program.cs`; MainWindow DockingManager loaded (no trial dialog / no star-width XAML crash)
  - [ ] P2: AutoMapper 12 → ≥15.1.1 (GHSA-rvv3-g6hj-g44x)

### P1 — Finish / domain (Spec-Kit wave 2) — aligns with [issue #11](https://github.com/Bigessfour/BusBuddy-3/issues/11)

- [x] Student import / optimize end-to-end (UI + SeedDataService + tests)
  - [x] CSV import wired: `ISeedDataService.ImportStudentsFromCsvAsync` + Students/StudentForm Import CSV buttons (Wiley-format file picker). Proof: parent address columns, next `WSD` number, Wiley header rejection, form import refreshes list via `StudentsImportedMessage`
  - [x] Optimize routes: `IStudentRouteOptimizer` fills active routes via `IRouteService.AutoAssignStudentsAsync`, then Ollama/`GrokGlobalAPI` commentary (mock fallback). Wired on Students + Dashboard. Proof: `StudentRouteOptimizerTests`
- [x] Reports: `IOperationalReportService` writes live PDFs/CSVs via `PdfReportService.GenerateTabularReport` + Ollama/`GrokGlobalAPI.GetShortCommentaryAsync` (mock fallback). All Reports buttons + Dashboard Generate Report. Proof: `OperationalReportServiceTests`, `PdfReportServiceTests.GenerateTabularReport_ReturnsValidPdf`. Merged [PR #24](https://github.com/Bigessfour/BusBuddy-3/pull/24). CLI `--generate-report` uses the same service (aliases: Roster, RouteManifest, StudentList, DriverSchedule)
- [ ] Driver availability + SfScheduler
- [ ] Maintenance UI polish
- [ ] Google Earth Engine enhancements (beyond current DI/auth)
- [x] End-to-end student → assign → report proof test — `BusBuddy.Tests/Core/RouteAssignmentFlowTests.cs` (SeedDataService → StudentService → RouteService → PdfReportService). **UTM Windows VM 2026-08-16:** `Total tests: 1`, `Passed: 1` (built from `C:\dev\BusBuddy-3` after Z:\ sync). Mac host cannot execute WPF testhost; use `./run-wpf.sh` + `utm_run_in_vm.ps1` for GUI.

### P2 — Hygiene / quality

- [x] Bootstrap function-inventory generated scan — `.function-inventory.json` (16 surfaces) → [function-inventory.generated.md](./function-inventory.generated.md) (2026-08-16: 11/16 with proof)
- [ ] Resolve/exclude corrupted LFS noise on `TWN_CICD_Checklist_…pdf` if still dirty locally
- [ ] AutoMapper 12.x advisory — upgrade or replace (see 006 deps-audit)
- [x] Restore [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](../Documentation/GCP-GEE-SECRETS-AND-AUTH.md) (was missing; AGENTS link)

### GitHub issues triage (open as of 2026-07-24)

| Issue                                                     | Topic                                           | Suggested disposition                                                                                      |
| --------------------------------------------------------- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| [#13](https://github.com/Bigessfour/BusBuddy-3/issues/13) | ViewModel dedup                                 | Largely done in hygiene/PR #16 (`BaseViewModelMvp` removed). Confirm no remaining flat VM refs → **close** |
| [#14](https://github.com/Bigessfour/BusBuddy-3/issues/14) | CI + secrets/MCP                                | Mostly done (solo CI, Passwords, Syncfusion MCP). Re-check `ci-with-ai` / GH secrets → update or **close** |
| [#15](https://github.com/Bigessfour/BusBuddy-3/issues/15) | Deprecate bb-* PS                               | Docs/deprecation done; residual archive refs OK → **close** or narrow remaining tasks                      |
| [#11](https://github.com/Bigessfour/BusBuddy-3/issues/11) | Close stubs (Reports/Grok/Settings/Maintenance) | Still active — maps to **P1** above; keep open                                                             |

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

| Surface                                                        | Tier | Proof / next check                                                                                                                                                              |
| -------------------------------------------------------------- | ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Student / Seed / Route / Optimizer / Reports services          | P1   | Unit tests present (`StudentServiceTests`, `SeedDataServiceTests`, `RouteServiceTests`, `StudentRouteOptimizerTests`, `OperationalReportServiceTests`, `PdfReportServiceTests`) |
| `StudentsView` / `ReportsView`                                 | P1   | No WPF testhost on Mac. Proof is VM smoke (`./run-wpf.sh`) + the Core tests above. Do not treat as a missing feature.                                                           |
| `ScheduleService`                                              | P1   | **No proof** — next domain slice (driver availability + SfScheduler)                                                                                                            |
| `DriverService`                                                | P1   | `DriverServiceTests` exist; SfScheduler UI still open                                                                                                                           |
| `MaintenanceService` / GEE / Dashboard metrics / theme manager | P2   | Tests or GapsCoverage present; Maintenance UI + GEE enhancements still open                                                                                                     |
| `DashboardView` / `GcpCredentialBootstrap`                     | P2   | No dedicated test; VM smoke / auth doc is the current check                                                                                                                     |

---

## Meta

- Re-check this file at the start of each session.
- After structural changes: update architecture map in `STEADY-STATE-AND-FINISH-ROADMAP.md` and re-index RAG.
- Do not put secrets here.

---

*Updated 2026-08-16: function-inventory bootstrapped (16 surfaces, 11 with proof).*
