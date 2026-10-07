# Research: 008 Route Determination / Fleet Sizing

## Decision: Heuristic clustering + greedy fill, with optional Google Route Optimization

**Rationale**: Spec requires minimize buses with comfort for &gt;100 riders. Density/bbox cells + outlier gap split + greedy seat packing delivers SC-001/SC-002 without a native OR-Tools dependency. When `IRouteOptimizationService` is configured, generation reorders pickups inside each packed route via Google Route Optimization `optimizeTours` (fail-open). Heuristic order remains if the API is missing or unauthorized.

**Alternatives considered**:

- Google OR-Tools VRP — stronger optima; heavier native deps; still deferred as a local solver.
- Route Optimization API only (no heuristic) — fails closed without a key / OAuth; rejected as the sole method.

## Decision: Q1:A — Separate transfer fleet / route pool

**Rationale**: User choice. Transfer demand must not consume home→school seats. Same algorithms, separate inventory and generation entry point.

**Alternatives considered**: Shared buses with time gaps (Q1:B); separate default with share override (Q1:C).

## Decision: Q2:B — Block hard seating; warn-and-allow time/geo

**Rationale**: User choice. Aligns with “soft max with override” only for non-capacity constraints; assigned bus `SeatingCapacity` remains the hard ceiling unless an explicit override is recorded.

**Alternatives considered**: Warn-and-allow everything (A); block both with role override (C).

## Decision: Q3:B — N-cell grid from district bbox + density

**Rationale**: User choice. Fixed 4-from-centroid is too coarse for medium/large cities; density-aware cells better match &gt;100 rider scenarios while remaining simple.

**Alternatives considered**: Fixed 4 (A); 4 + auto-split only (C) — outlier split still used _inside_ cells as a secondary rule.

## Decision: School times on Destination; pickups computed, not clerk-entered per stop in this increment

**Rationale**: Spec US3 — start/dismissal on map school; work backward along ordered stops. Persist computed times on route stops or student schedule fields when those exist; regenerate when start time or order changes.

**Alternatives considered**: Per-student manual pickup windows first — more data entry; deferred.

## Decision: AM/PM mirror with ride mode AM / PM / both

**Rationale**: Spec FR-007. Derive mode from AMRoute/PMRoute presence today; optionally add explicit `RideMode` enum later if ambiguity appears. Occasional-rider stops remain in path order even when mode is one-sided.

## Decision: Published clocks — one bell plan, leg durations, 5-minute dwell

**Rationale**: `specs/routes.md` Published clocks, and FR-003 / FR-004 / FR-004a. Morning is a backward walk from each confirmed school start. Afternoon walks forward from dismissal. Special-needs routes that serve several schools use each rider’s bell and stay out of the per-school packer. A missed morning bell warns and does not publish. `08:00` / `15:30` is not a default bell.

**Supersedes**: treating average-speed Haversine and a 45-second dwell as the published-clock method, regenerating only routes whose `Route.School` equals one name, and inferring afternoon from a `-PM` suffix.

**Unchanged**: assign-time fitness may still use the average-speed fallback so it does not require the network. Year-start packing of ordinary routes stays per school.

## Decision: Drive ETA — Maps Routes when configured, else average-speed Haversine

**Rationale**: Matches 007 fail-open pattern; assign-time checks must not require network. Year-start can optionally refresh with Routes for better schedules.

**Alternatives considered**: Always require Maps — breaks offline/Mac Core tests.

## Decision: Depend on PR #36 entities before implement

**Rationale**: Schools as Destinations, transfers, waypoint rebuild, student geo are prerequisites. Implement 008 on a branch from post-merge master.

## Resolved clarifications

All FR-014 / FR-015 / FR-016 markers resolved (Q1:A, Q2:B, Q3:B). No Technical Context NEEDS CLARIFICATION remaining.
