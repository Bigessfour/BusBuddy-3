# BusBuddy project-done checklist

**When this checklist is green, the project is done.** The judge is not a feeling and not a prior “ship-ready” paragraph. The judge is:

```bash
python3 .github/scripts/check-project-done.py
```

Exit code **0** = done. Exit code **1** = incomplete items remain (printed with file:line evidence). Catalog of allowed deferrals: [done-catalog.json](./done-catalog.json).

Open work still lives in [action-items.md](./action-items.md). That file is the queue. This file is the definition of done. Clerk spine: [clerk-path.md](./clerk-path.md). Domain contract: [specs/README.md](../specs/README.md).

Adapted from [An Engineer’s Guide to Knowing if You Are Done With a Project](https://www.joekarlsson.com/blog/an-engineers-guide-to-knowing-if-you-are-done-with-a-project/) (Joe Karlsson, 2018), then tightened for a Syncfusion WPF clerk app. Karlsson’s Best Buy list includes A/B tests and marketing pixels; those do not apply here and are explicit N/A, not silent skips.

---

## How “done” is decided

| Result       | Meaning                                                                                   |
| ------------ | ----------------------------------------------------------------------------------------- |
| **PASS**     | Evidence exists in the current tree (file, checkbox, or scan with zero hits).             |
| **FAIL**     | Incomplete: stub, orphan, missing wiring, missing UI proof, invariant break, or open box. |
| **DEFERRED** | Known leftover listed in `done-catalog.json` with a reason. Counts as resolved for done.  |
| **N/A**      | Karlsson item that does not exist in this product (A/B, a11y Phase 2).                    |

Unexplained leftovers fail. If you want to keep unused schema or a parked path, add a deferral with a reason. Do not edit the checker to hide a finding.

Mac cannot click WPF. Static UI wiring and XAML tests run here. Live ribbon proof is recorded as checked boxes in `action-items.md` after a Windows VM session.

---

## Karlsson items (mapped)

| ID  | Karlsson question    | BusBuddy evidence                                                                    | Pass when                                             |
| --- | -------------------- | ------------------------------------------------------------------------------------ | ----------------------------------------------------- |
| K01 | Does the code work?  | Clerk hops 1–6 Core boxes checked; `BusBuddy.sln` + `run-wpf.sh` exist               | Hops 1–6 are `[x]` and required launch files exist    |
| K02 | Tests / coverage     | Not a line-% gate. Every `.function-inventory.json` surface has a matching test file | No inventory surface lacks proof                      |
| K03 | End-to-end harness   | `Scripts/utm-wpf-test.sh` + WPF test project                                         | Harness file exists (guest run is `--with-utm`)       |
| K04 | README               | `README.md` Quick Start, env vars, hybrid Mac/VM                                     | File exists and mentions `run-wpf.sh`                 |
| K05 | Analytics            | No marketing tags. Observability = Serilog `RuntimeCapabilityLogger`                 | Logger file exists                                    |
| K06 | A/B tests            | N/A — single-district clerk desktop                                                  | N/A                                                   |
| K07 | UX / design audit    | Syncfusion-only + XAML compliance tests                                              | No `<DataGrid>`; XamlCompliance project/tests present |
| K08 | Automated regression | `.github/workflows/ci.yml` Build & Test                                              | Workflow exists with `dotnet test`                    |
| K09 | Manual QA            | Windows VM smoke recorded in action-items                                            | Map/settings smoke boxes are `[x]`                    |
| K10 | Accessibility audit  | Phase 2 out of scope                                                                 | N/A                                                   |
| K11 | Performance          | Map tile session tests; no OSM fail-open / quota retry loop                          | Forbidden OSM patterns absent in product              |
| K12 | In STAGE/PROD        | Not a web host. “Prod” = CI Release artifact + VM clerk path                         | CI workflow publishes or builds Release; hops proved  |

Karlsson’s later note ([Ship the Ugly Pot](https://www.joekarlsson.com/blog/ship-the-ugly-pot/)) still applies: this list is a finish line, not a reason to gold-plate. N/A and deferrals keep Best Buy-scale items from blocking a clerk app. Remaining **FAIL** rows are unfinished product, not polish.

---

## Extra gates (user-requested)

These are why the checker exists. Karlsson does not mention them; without them you can call a stub-ridden UI “done.”

| ID  | What it finds                   | Pass when                                                                                             |
| --- | ------------------------------- | ----------------------------------------------------------------------------------------------------- |
| B01 | Stubs                           | No `NotImplementedException` / `NotImplemented` in `BusBuddy.Core` or `BusBuddy.WPF` (tests excluded) |
| B02 | TODO/FIXME/HACK in product code | Zero hits in Core/WPF (allowlisted comments do not exist; defer or delete)                            |
| B03 | Orphan views                    | Every `*View.xaml` / `*Form.xaml` is constructed or referenced outside its own files                  |
| B04 | Orphan handlers                 | Every `*_Click` in WPF code-behind is referenced from XAML `Click="..."` (or removed)                 |
| B13 | UI wiring                       | Each clerk hop view contains the required command/click tokens                                        |
| B14 | UI operational proof            | Hop 1–6 UI evidence boxes and District Map VM re-smoke are `[x]` in action-items                      |

**UI proof of operational capability** means a clerk can open the real window and complete the hop, not only that DbPrep wrote a row. Core proof (hops 1–6 `[x]`) is necessary. It is not sufficient.

---

## Extra gates (additional — not in Karlsson, not in the original ask)

| ID  | Why it is on the finish line | Pass when                                                                                                    |
| --- | ---------------------------- | ------------------------------------------------------------------------------------------------------------ |
| B05 | Non-participating services   | Every `I*Service` under `BusBuddy.Core/Services` is registered in App or Core DI, or deferred                |
| B06 | Dead DbSets                  | Every `DbSet<>` is used by a participating service/UI **or** listed as a dead-table deferral                 |
| B07 | Domain invariants            | No `IsTrip` on `Route`; no OSM in product views; `IsLiveTrackingEnabled` is not enabled as a feature         |
| B08 | Secrets in repo              | No `AIza`, `sk-`, or `-----BEGIN` private key material in tracked product files                              |
| B09 | Syncfusion-only              | No standard WPF `<DataGrid>` in product XAML                                                                 |
| B10 | Action-items blocking boxes  | Every `- [ ]` in action-items is either done, matching `action_item_nonblocking`, or this FAIL list          |
| B11 | Docs drift                   | Constitution vs AGENTS must not contradict merge gates (CodeQL)                                              |
| B12 | Inventory freshness          | `.function-inventory.json` surfaces appear in `docs/function-inventory.generated.md`                         |
| B15 | Demo/sample data             | No `LoadSampleData` / Monday demo strings in WPF                                                             |
| B16 | Duplicate writes             | Hop 4 still documents `Route.AM*`/`PM*` as canonical; checker notes `RouteAssignments` as deferred duplicate |

Suggested later (not in the runner yet; add if they start lying): UTC persist on fuel/maintenance dates, migration pending vs applied, PII filename scan, Syncfusion license fail-open log.

---

## Clerk hops (operational spine)

From [clerk-path.md](./clerk-path.md). Each hop needs **Core proof** (already `[x]`) **and** **UI proof** (B14).

1. Add school (`SchoolDestinationForm`)
2. Add student (`StudentForm`)
3. Generate routes (`RouteManagementView`)
4. Bus + driver on `Route.AM*` / `PM*`
5. Daily schedule (`DriverScheduleView`)
6. Fuel + maintenance on that bus
7. Map (Settings depot/bbox + District Map smoke)

Dock Students/Routes/Buses/Drivers grids are summaries. They do not invent sample rows.

---

## What does _not_ have to be true

These stay **N/A** or **DEFERRED** so the finish line matches this product:

- Marketing analytics, A/B tests, Power Week load tests
- Accessibility Phase 2
- Live fleet GPS (do not hook a timer on `FleetMonitoringService`)
- Families / Guardians family graph (`FamilyService` / `GuardianService` — not in DI; guardian fields live on the student)
- `ActivityService` / `ActivityScheduleService` as a second trip product (board = `TripEventService`)
- `BusBuddyAIReportingService` clerk surface
- Implementing empty `DataIntegrityService` / `BusBuddyScheduleDataProvider` / `*.disabled` (hygiene delete later)
- Splitting `RouteService` for file size (parked in action-items)
- `RouteAssignments` as a second write path for hop 4
- OSM anywhere except `Tools/SfMapTileProbe`

Parked unused **views** and **Click handlers** are not deferred. Wire them or delete them.

---

## Running

```bash
# Default: static scans + action-items + catalog (Mac-safe)
python3 .github/scripts/check-project-done.py

# JSON for agents
python3 .github/scripts/check-project-done.py --json

# Optional heavier gates (Windows / time)
python3 .github/scripts/check-project-done.py --with-build
python3 .github/scripts/check-project-done.py --with-utm
```

After a cluster of orphan/stub fixes, run `/code-review` on the touched views before the next checker pass. Do not wait until the whole list is green; review when a hop’s UI surface changes.

When the checker exits 0, update `docs/action-items.md` so it does not still claim leftover blockers, then the project is done.
