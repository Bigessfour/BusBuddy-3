# Research: 008 Route Determination / Fleet Sizing

## Decision: Heuristic clustering + greedy fill (not full VRP solver)

**Rationale**: Spec requires minimize buses with comfort for &gt;100 riders, but constitution YAGNI forbids a heavy commercial OR-Tools/VRP dependency in MVP. Density/bbox cells + outlier gap split + greedy seat packing delivers SC-001/SC-002 and can later plug Maps matrix costs.

**Alternatives considered**:
- Google OR-Tools VRP — stronger optima; heavier native deps and ops surface; deferred.
- Single k-means only — weak rural outliers; rejected as sole method.
- Manual routes only — fails year-start auto-assign requirement.

## Decision: Q1:A — Separate transfer fleet / route pool

**Rationale**: User choice. Transfer demand must not consume home→school seats. Same algorithms, separate inventory and generation entry point.

**Alternatives considered**: Shared buses with time gaps (Q1:B); separate default with share override (Q1:C).

## Decision: Q2:B — Block hard seating; warn-and-allow time/geo

**Rationale**: User choice. Aligns with “soft max with override” only for non-capacity constraints; assigned bus `SeatingCapacity` remains the hard ceiling unless an explicit override is recorded.

**Alternatives considered**: Warn-and-allow everything (A); block both with role override (C).

## Decision: Q3:B — N-cell grid from district bbox + density

**Rationale**: User choice. Fixed 4-from-centroid is too coarse for medium/large cities; density-aware cells better match &gt;100 rider scenarios while remaining simple.

**Alternatives considered**: Fixed 4 (A); 4 + auto-split only (C) — outlier split still used *inside* cells as a secondary rule.

## Decision: School times on Destination; pickups computed, not clerk-entered per stop (MVP)

**Rationale**: Spec US3 — start/dismissal on map school; work backward along ordered stops. Persist computed times on route stops or student schedule fields when those exist; regenerate when start time or order changes.

**Alternatives considered**: Per-student manual pickup windows first — more data entry; deferred.

## Decision: AM/PM mirror with ride mode AM / PM / both

**Rationale**: Spec FR-007. Derive mode from AMRoute/PMRoute presence today; optionally add explicit `RideMode` enum later if ambiguity appears. Occasional-rider stops remain in path order even when mode is one-sided.

## Decision: Drive ETA — Maps Routes when configured, else average-speed Haversine

**Rationale**: Matches 007 fail-open pattern; assign-time checks must not require network. Year-start can optionally refresh with Routes for better schedules.

**Alternatives considered**: Always require Maps — breaks offline/Mac Core tests.

## Decision: Depend on PR #36 entities before implement

**Rationale**: Schools as Destinations, transfers, waypoint rebuild, student geo are prerequisites. Implement 008 on a branch from post-merge master.

## Resolved clarifications

All FR-014 / FR-015 / FR-016 markers resolved (Q1:A, Q2:B, Q3:B). No Technical Context NEEDS CLARIFICATION remaining.
