# Tasks: Route Determination / Fleet Sizing

**Input**: Design documents from `/specs/008-route-determination/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included (plan Testing + story Independent Tests). Prefer failing Core unit tests before production services.

**Organization**: Setup → Foundational → US1 (MVP) → US2 → US3 → US4 → Polish.

**Gate**: Prefer branch from `master` after PR #36 merge (schools, transfers, student geo).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: US1–US4 from [spec.md](./spec.md)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folder + DI registration surface for the planner

- [ ] T001 Create directory `BusBuddy.Core/Services/RouteDetermination/` and add placeholder `README.md` noting contracts in `specs/008-route-determination/contracts/`
- [ ] T002 [P] Add `RoutingDistrictSettings` options class in `BusBuddy.Core/Configuration/RoutingDistrictSettings.cs` (bbox or extent keys, TargetRidersPerCell, MaxPickupGapMinutes, AverageSpeedMph, MaxRideMinutes, AllowSeatingOverride) per [data-model.md](./data-model.md)
- [ ] T003 [P] Bind `RoutingDistrictSettings` section in `BusBuddy.WPF/appsettings.json` and `BusBuddy.Core/appsettings.json` with Wiley-scale defaults

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: School times + service contracts + DI — required before any user story

**⚠️ CRITICAL**: No user story work until this phase is complete

- [ ] T004 Add `StartTime` and `DismissalTime` (`TimeSpan?`) to `BusBuddy.Core/Models/Destination.cs` and configure in `BusBuddy.Core/Data/BusBuddyDbContext.cs`
- [ ] T005 Add EF migration under `BusBuddy.Core/Migrations/` for Destination school times; update snapshot; note Windows SQL Server apply path
- [ ] T006 [P] Add ride-mode helper in `BusBuddy.Core/Models/StudentRideMode.cs` (AM / PM / Both derived from `AMRoute`/`PMRoute`, with optional future enum)
- [ ] T007 Create `IRouteDeterminationService` in `BusBuddy.Core/Services/RouteDetermination/IRouteDeterminationService.cs` matching [contracts/route-determination.md](./contracts/route-determination.md) (`GenerateAndAssignAsync`, `RecalculateOnAssignAsync`, `ApplyClerkOverrideAsync`)
- [ ] T008 [P] Create DTOs `RouteGenerationResult`, `RouteProposalDto`, `AssignFitnessResult` in `BusBuddy.Core/Services/RouteDetermination/RouteDeterminationModels.cs` per [data-model.md](./data-model.md) and [contracts/assign-fitness.md](./contracts/assign-fitness.md)
- [ ] T009 Register `IRouteDeterminationService` (stub or real) in `BusBuddy.Core/Extensions/ServiceCollectionExtensions.cs` and `BusBuddy.WPF/App.xaml.cs`

**Checkpoint**: Build succeeds; Destination times migrate on SQL Server; DI resolves `IRouteDeterminationService`

---

## Phase 3: User Story 1 - Year-start auto route build (Priority: P1) 🎯 MVP

**Goal**: Minimum HomeToSchool AM routes (+ mirrored PM structure) from school times, homes, seating, density cells + outlier split; auto-assign with map override hook

**Independent Test**: 12 nearby students + bus seats ≥12 → 1 AM proposal; distant gap → >1 route; AM-only student keeps stop on PM mirror; clerk override moves student

### Tests for User Story 1

- [ ] T010 [P] [US1] Add `BusBuddy.Tests/Core/RouteDetermination/DensityCellBuilderTests.cs` — bbox/density yields N cells; empty coords excluded
- [ ] T011 [P] [US1] Add `BusBuddy.Tests/Core/RouteDetermination/RoutePackingTests.cs` — 12 nearby riders pack into one route under seating; outlier gap forces split (SC-001/SC-002)
- [ ] T012 [P] [US1] Add `BusBuddy.Tests/Core/RouteDetermination/RideModeMirrorTests.cs` — AM-only retains stop on PM mirror proposal

### Implementation for User Story 1

- [ ] T013 [US1] Implement density/bbox cell builder in `BusBuddy.Core/Services/RouteDetermination/DensityCellBuilder.cs` (Q3:B)
- [ ] T014 [US1] Implement outlier gap split + greedy seating packer in `BusBuddy.Core/Services/RouteDetermination/RoutePacker.cs` (hard capacity = suggested/assigned bus seating)
- [ ] T015 [US1] Implement `RouteDeterminationService.GenerateAndAssignAsync` for `FleetKind.HomeToSchool` in `BusBuddy.Core/Services/RouteDetermination/RouteDeterminationService.cs` (create draft routes + AM assignments; mirror PM stop structure)
- [ ] T016 [US1] Implement `ApplyClerkOverrideAsync` in `RouteDeterminationService.cs` (move student between proposals/routes; Serilog override)
- [ ] T017 [US1] Persist accepted drafts via `IRouteService` / route create helpers in `RouteDeterminationService.cs` (naming `Draft-{School}-{Cell}-{n}` or accept-into-existing)
- [ ] T018 [US1] Add year-start **Generate routes** command on `BusBuddy.WPF/ViewModels/Route/RouteManagementViewModel.cs` (or Students) calling `GenerateAndAssignAsync`
- [ ] T019 [US1] Show draft proposals on map / status in `BusBuddy.WPF/ViewModels/GoogleEarth/GoogleEarthViewModel.cs` (or Route map path) for override selection
- [ ] T020 [US1] Serilog `Route generation completed School={SchoolId} Fleet={Fleet} Routes={N} Students={S}` in `RouteDeterminationService.cs`

**Checkpoint**: US1 unit tests green; year-start generate produces drafts for one school

---

## Phase 4: User Story 2 - Assign-time toast and route suggestion (Priority: P1)

**Goal**: On assign — **block** hard seating (unless override); **warn-and-allow** time/geo; toast + suggested routes / new route past threshold

**Independent Test**: Overload → Blocked + no persist; time risk → Warned + assign proceeds; suggestions populated when alternate exists

### Tests for User Story 2

- [ ] T021 [P] [US2] Add `BusBuddy.Tests/Core/RouteDetermination/AssignFitnessTests.cs` — seating block without override; seating allow with override; warn on arrival/geo; SuggestNewRoute flag

### Implementation for User Story 2

- [ ] T022 [US2] Implement `RecalculateOnAssignAsync` / fitness evaluator in `BusBuddy.Core/Services/RouteDetermination/AssignFitnessEvaluator.cs` per [contracts/assign-fitness.md](./contracts/assign-fitness.md) (Q2:B)
- [ ] T023 [US2] Wrap `AssignStudentToRouteAsync` path in `BusBuddy.Core/Services/RouteService.cs` (or caller) to consult fitness before persist; record seating override when requested
- [ ] T024 [US2] Surface Syncfusion toast/status from `AssignFitnessResult` in `BusBuddy.WPF/ViewModels/Student/StudentsViewModel.cs` (bulk/single assign) and/or Route assign UI
- [ ] T025 [US2] When `SuggestNewRoute`, offer command to call generation for that school cell in the same ViewModel
- [ ] T026 [US2] Serilog `Assign fitness Blocked|Warned Student={Id} Route={Id} Reasons={Reasons}` in fitness evaluator

**Checkpoint**: US2 tests green; UI blocks overload and warns on time/geo

---

## Phase 5: User Story 3 - School times drive pickup schedule (Priority: P2)

**Goal**: Start/dismissal on school Destination; backward AM pickups; forward PM from dismissal

**Independent Test**: Changing StartTime regenerates monotonic pickup times; PM mirror consistent with dismissal

### Tests for User Story 3

- [ ] T027 [P] [US3] Add `BusBuddy.Tests/Core/RouteDetermination/PickupScheduleTests.cs` — backward from StartTime; forward from DismissalTime; fail generation when StartTime missing

### Implementation for User Story 3

- [ ] T028 [US3] Implement `PickupScheduleCalculator.cs` in `BusBuddy.Core/Services/RouteDetermination/` (Maps `IRoutingService` ETA when available; else AverageSpeedMph Haversine)
- [ ] T029 [US3] Integrate schedule calculator into `GenerateAndAssignAsync` and regenerate-on-StartTime-change helper
- [ ] T030 [US3] Add StartTime/DismissalTime editors on school Destination UI (`BusBuddy.WPF` school/map destination form — extend existing Destination or Student school panel)
- [ ] T031 [US3] Persist computed stop times onto `RouteStop` ScheduledArrival/Departure (or documented student fields) when writing proposals

**Checkpoint**: US3 tests green; school time fields editable; schedules regenerate

---

## Phase 6: User Story 4 - Independent transfer route planning (Priority: P2)

**Goal**: Separate Transfer fleet pool using transfer pickup/dropoff pairs; same packing rules; no seat theft from HomeToSchool (Q1:A)

**Independent Test**: Transfer generation route count independent of home routes; HomeToSchool generation ignores transfer pairs as home substitutes

### Tests for User Story 4

- [ ] T032 [P] [US4] Add `BusBuddy.Tests/Core/RouteDetermination/TransferFleetTests.cs` — Transfer pool separate; home generation does not seat from transfer demand

### Implementation for User Story 4

- [ ] T033 [US4] Extend `GenerateAndAssignAsync` for `FleetKind.Transfer` using active `StudentSchoolTransfer` stops in `RouteDeterminationService.cs`
- [ ] T034 [US4] Ensure HomeToSchool packing excludes transfer-only legs from home seat counts in `RoutePacker.cs`
- [ ] T035 [US4] Add **Generate transfer routes** command in `BusBuddy.WPF/ViewModels/Route/RouteManagementViewModel.cs` (or Students transfer context)
- [ ] T036 [US4] After transfer route accept, call `IRouteWaypointRebuildService` for affected routes in `RouteDeterminationService.cs`

**Checkpoint**: US4 tests green; transfer generate creates separate drafts

---

## Phase 7: Polish & Cross-Cutting

**Purpose**: Docs, inventory, architecture, RAG

- [ ] T037 [P] Update `docs/action-items.md` 008 checkboxes and link tasks proof
- [ ] T038 [P] Update `STEADY-STATE-AND-FINISH-ROADMAP.md` architecture map with RouteDetermination service if structural
- [ ] T039 [P] Re-scan function inventory / `docs/function-tree.md` for planner surfaces
- [ ] T040 Run `python -m rag.index` after doc/spec updates
- [ ] T041 VM smoke per [quickstart.md](./quickstart.md); record Serilog proof lines in action-items

---

## Dependencies & Story Order

```text
Phase 1 Setup
    ↓
Phase 2 Foundational (times + interfaces + DI)
    ↓
Phase 3 US1 MVP (generate/pack/override) ──┬──→ Phase 4 US2 (fitness on assign)
    ↓                                      │
Phase 5 US3 (schedules) ←── uses US1 packer/order
    ↓
Phase 6 US4 (transfer fleet) ←── same packer, separate FleetKind
    ↓
Phase 7 Polish
```

**Story completion order**: US1 → US2 (can partly parallel after T015) → US3 → US4 → Polish

**Parallel examples**:
- After T009: T010–T012 in parallel
- After T015: T021 tests while T018–T019 UI proceeds
- Polish T037–T039 in parallel

## Implementation strategy

1. **MVP**: Phase 1–3 only (year-start generate + override) — delivers SC-001/SC-002
2. **Increment**: US2 assign toasts (day-to-day safety)
3. **Increment**: US3 school-time scheduling quality
4. **Increment**: US4 transfer fleet
5. **Polish**: docs + VM proof + RAG

## Task count summary

| Phase | Tasks | Notes |
|-------|-------|-------|
| Setup | T001–T003 | 3 |
| Foundational | T004–T009 | 6 |
| US1 | T010–T020 | 11 (3 tests + 8 impl) |
| US2 | T021–T026 | 6 |
| US3 | T027–T031 | 5 |
| US4 | T032–T036 | 5 |
| Polish | T037–T041 | 5 |
| **Total** | **T001–T041** | **41** |

**MVP scope**: T001–T020 (Setup + Foundational + US1)

**Format validation**: All tasks use `- [ ]`, Task IDs, optional `[P]`, story labels on US phases only, and concrete file paths.
