# Implementation Plan: Route Determination / Fleet Sizing

**Branch**: `feature/008-route-determination` (spec artifacts; implement after PR #36 merge) | **Date**: 2026-08-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-route-determination/spec.md`

**Clarifications locked**: Q1:A separate transfer fleet; Q2:B block hard seating / warn-and-allow time-geo; Q3:B density/bbox grid cells

## Summary

Add a Core route-determination service that (1) year-start auto-builds the minimum home→school AM routes (mirrored PM) from school times, student homes, and bus seating, using density-based geographic cells plus outlier splits; (2) recalculates fitness on assign with hard block on seating overload and warn-and-allow toasts for time/geo risk; (3) plans transfer routes in a separate fleet pool with the same rules. WPF surfaces Syncfusion toasts and map override; drive times prefer Maps Routes (007) with average-speed fallback.

## Technical Context

**Language/Version**: C# / .NET 9 (`net9.0-windows` Core/WPF)

**Primary Dependencies**: Existing EF Core, `IRouteService`, `IRoutingService` (007), `IStudentSchoolTransferService`, Syncfusion WPF (toasts / SfMap), Serilog, CommunityToolkit.Mvvm

**Storage**: EF — extend `Destination` with school start/dismissal; optional district routing settings; route proposals may be draft `Route` rows or a `RouteProposal` table (see data-model); student ride mode (AM/PM/both) explicit if not already derivable from AMRoute/PMRoute nullability

**Testing**: NUnit in `BusBuddy.Tests`; InMemory for clustering/capacity/time backward scheduling; no live Maps in CI (`Category!=Integration&Category!=InMemoryFlaky`)

**Target Platform**: Windows WPF (VM); Mac host builds Core/tests with `EnableWindowsTargeting`

**Project Type**: Desktop (Syncfusion WPF) + Core class library

**Performance Goals**: Year-start generation for 100+ riders &lt; ~30s perceived on typical hardware; assign-time fitness check &lt; 1s without live routing (use cached/fallback ETA)

**Constraints**: Syncfusion-only UI; Serilog-only; no cloud hosting; YAGNI — no full commercial VRP solver in MVP (heuristic clustering + greedy fill); constitution Geo = Maps + shapefiles when available

**Scale/Scope**: Small districts through &gt;100 riders / medium–large city; 4 user stories in spec

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate                    | Status                                                                             |
| ----------------------- | ---------------------------------------------------------------------------------- |
| I Spec-driven + RAG     | Pass — spec 008; plan/research/data-model/contracts; RAG re-index after merge      |
| II Syncfusion-only UI   | Pass — toasts via Syncfusion notifications / existing patterns; SfMap for override |
| III Serilog-only        | Pass — planner logs counts/violations, no secrets                                  |
| IV Layered architecture | Pass — Core planner services; WPF VM/commands; tests in BusBuddy.Tests             |
| V Hybrid Mac/Windows    | Pass — Core algorithms runnable on Mac; UI smoke on VM                             |
| VI Solo CI/CD           | Pass — feature branch + PR to master                                               |
| VII YAGNI / no secrets  | Pass — heuristic planner; no new paid APIs beyond existing Maps                    |
| Geo (v1.1.0)            | Pass — optional Routes for ETA; shapefile/bbox for district extent                 |
| Hosting                 | Pass — no AWS/cloud app host                                                       |

Post-design re-check: contracts are Core service interfaces + UI event contracts; no new control families. **Pass.**

## Project Structure

### Documentation (this feature)

```text
specs/008-route-determination/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── route-determination.md
│   └── assign-fitness.md
├── checklists/requirements.md
└── tasks.md                 # /speckit-tasks (not this command)
```

### Source Code (repository root)

```text
BusBuddy.Core/
  Models/                    # Destination times; RoutingDistrictSettings; ride mode helpers
  Services/
    RouteDetermination/      # IRouteDeterminationService, clustering, time backward scheduler
    RouteWaypointRebuildService.cs  # evolve to honor ordered proposals (post-#36)
  Extensions/ServiceCollectionExtensions.cs
BusBuddy.WPF/
  ViewModels/Student|Route|GoogleEarth/  # year-start generate; assign toast; map override
  App.xaml.cs                            # DI
BusBuddy.Tests/Core/                     # clustering, capacity block, time schedule, transfer pool
```

**Structure Decision**: Existing Core + WPF + Tests only. New subfolder `BusBuddy.Core/Services/RouteDetermination/`. No new project.

## Complexity Tracking

No constitution violations requiring justification.

## Implementation approach (high level)

1. Persist school **StartTime** / **DismissalTime** on Destination (School).
2. Implement density/bbox cell builder + outlier split; greedy pack by seating.
3. Backward pickup schedule from school start (AM); forward from dismissal (PM mirror).
4. Year-start `GenerateAndAssignAsync(schoolId)` → draft routes + assignments; map override API.
5. Wrap `AssignStudentToRouteAsync` with fitness: hard block seating; warn toast time/geo.
6. Separate `GenerateTransferRoutesAsync` using transfer stop pairs only.
7. Wire Syncfusion toast/status on Students/Route assign paths; Serilog proof events.
