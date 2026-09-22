# BusBuddy-3 — Project brief

_Prepared 2026-09-15 for external technical review. Figures are taken from the repository at `master` on that date._

## What it is

BusBuddy-3 is a Windows desktop application for the transportation clerk of a small rural school district (Wiley / Lamar, Colorado). One clerk, one workstation, no hosted backend. The clerk catalogs schools, enters students, generates daily bus routes, assigns a bus and driver to each route, records the daily schedule, and logs fuel and maintenance against the bus.

It is a single-user line-of-business app. It is not a parent portal, it does not track buses live, and it is not hosted in a cloud.

## Who it is for

- **Primary user:** the district transportation clerk. Data entry is form-driven; addresses are validated and geocoded on entry.
- **Secondary:** transportation director (route review on the district map, printed rosters and assignments).

## Stack

| Layer   | Choice                                                                                                                                                                | Reason                                                                  |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| Runtime | .NET 9, C# 13, WPF                                                                                                                                                    | Windows-only clerk workstation; offline tolerant; no hosting cost       |
| UI      | Syncfusion WPF 34.2.3 (`SfDataGrid`, `SfMap`, `SfChart`, `SfScheduler`, PDF)                                                                                          | One vendor for grid, map, chart, scheduler, and PDF; community license  |
| Pattern | MVVM with CommunityToolkit.Mvvm; DI via `Microsoft.Extensions.DependencyInjection` (`AddDataServices` for Core, `App.Composition` for WPF)                            | Testable ViewModels; code-behind limited to window chrome               |
| Data    | EF Core 9, PostgreSQL (Npgsql) in Docker for dev/test; SQL Server provider retained for a hosted district DB                                                          | Migrations; `IBusBuddyDbContextFactory` opens one context per operation |
| Logging | Serilog only (file + console sinks, enrichers)                                                                                                                        | Structured; every clerk click writes a greppable `UI proof` line        |
| Geo     | Google Maps Platform: Address Validation, Places Autocomplete (New), Routes (`computeRoutes` / `computeRouteMatrix`), Route Optimization (`optimizeTours`), Map Tiles | Postal-grade addresses, real drive polylines, ToS-compliant basemap     |
| AI      | Local Ollama behind a service interface                                                                                                                               | No cloud xAI key; offline fallback when Ollama is down                  |
| Tests   | NUnit + Moq; 572 test cases across `BusBuddy.Tests` and `BusBuddy.XamlCompliance.Tests`                                                                               | Core services against Postgres; XAML contract tests for UI wiring       |
| CI      | GitHub Actions on `windows-latest`: restore → build → test → publish; squash auto-merge on green                                                                      | Solo-developer flow, protected `master`, no direct pushes               |

## Size

| Metric                         | Value                        |
| ------------------------------ | ---------------------------- |
| First commit                   | 2025-08-06                   |
| Commits on `master`            | 203                          |
| Merged pull requests           | 61                           |
| Source files (`.cs` + `.xaml`) | ~570                         |
| Core services                  | 65 (+16 Google Maps clients) |
| WPF views                      | 34                           |
| EF migrations                  | 24                           |
| Test cases                     | 572                          |

## How it works — the clerk path

Everything that matters flows through six hops. Each hop is one service method, one Serilog line, one Postgres row.

```text
1 Add school            DestinationService.AddSchoolAsync          → Destinations
2 Add / import students IStudentService.AddStudentAsync            → Students (DestinationId, validated lat/lng)
3 Generate routes       RouteDeterminationService.GenerateAndAssign → Routes, RouteStops
4 Bus + driver on route RouteService.Assign{Vehicle,Driver}ToRoute → Routes.AMVehicleId / AMDriverId (and PM)
5 Daily schedule        IScheduleService.AddScheduleAsync           → Schedules
6 Fuel / maintenance    FuelService / MaintenanceService            → FuelRecords / MaintenanceRecords → Vehicles
```

Supporting surfaces read those same tables: the District Map (Syncfusion `SfMap` on Google tiles), the Trip Board for one-off activity trips, and printed PDF reports.

### Route generation

Route determination is a two-stage heuristic, not a black box:

1. **Fleet sizing and packing (local).** Students for a school are clustered into density cells inside the district bounding box, rural outliers are split by gap, and cells are greedily packed into routes under the hard limit of the assigned bus's seating capacity. Pickup times are derived backward from school start (AM) or dismissal (PM).
2. **Visit order (Google, optional).** When a Maps key is present, each packed route's stop order is sent to Route Optimization `optimizeTours` as a clerk-initiated suggestion. Drive polylines and ETAs come from the Routes API. Without a key the heuristic order stands. The clerk still owns save and published times; the system never rewrites a published route overnight.

### Domain rules that shape the code

These are enforced by specs (`specs/*.md`) and by static tests:

- **Route ≠ Trip.** A route is a daily published run; a trip is a one-date activity with a manifest. `Route` has no `IsTrip` flag.
- **PickupMode** is `Home` or `CatalogStop`. Special-needs students are home pickup on a special-needs route with a trained driver and aide.
- **Same-day "not riding"** is a rider exception on the route, not a route rewrite.
- **Only validated coordinates are plotted.** No `0,0` pins, no US-centroid guesses.
- **Student PII never enters git.** Rosters load through clerk forms and Google Address Validation.
- **Live fleet GPS is deferred** by design.

## How it is built

- **Spec-driven.** A constitution (`.specify/memory/constitution.md`) fixes the non-negotiables (Syncfusion-only UI, Serilog-only logging, layered Core/WPF/Tests, hybrid Mac + Windows VM, solo-dev CI). Numbered feature specs under `specs/00x-*` carry plan and tasks; `specs/{students,routes,trips,…}.md` are the domain contract.
- **Hybrid development.** Core, tests, Docker Postgres, and secrets live on a Mac. The WPF app runs in a Windows 11 VM (UTM) from `C:\dev\BusBuddy-3` over SSH (`Scripts/utm-dev-bridge.sh`). `./run-wpf.sh` is Mac preflight plus `launch`; `utm-wpf-test.sh` is a shim for `test`. See `docs/utm-dev-bridge.md`.
- **AI-assisted, with guardrails.** The developer works with coding agents. `AGENTS.md`, the constitution, a RAG index of the repo, and a `check-project-done.py` judge exist to keep agents inside the domain rules rather than to replace review.
- **Definition of done is executable.** `python3 .github/scripts/check-project-done.py` returns exit 0 only when every checklist item passes or is explicitly deferred with a reason. See `01-status.md` for today's result.

## Reading order for the rest of this packet

1. `01-status.md` — where the project actually stands, including open gaps
2. `specs/README.md` → `students.md`, `routes.md`, `trips.md`, `maps.md` — domain contract
3. `docs/clerk-path.md` — the write path and the duplicate-write decisions
4. `Documentation/diagrams/busbuddy-3-architecture.md` — architecture map (Mermaid)
5. `.specify/memory/constitution.md` — the non-negotiables
