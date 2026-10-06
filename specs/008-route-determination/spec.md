# Feature Specification: Route Determination / Fleet Sizing

**Feature Branch**: `feature/008-route-determination`

**Created**: 2026-08-17

**Status**: Draft

**Input**: From a listing of students (home/pickup location, AM/PM ride mode, school assignment), determine the minimum number of routes and buses needed while respecting seating capacity of the assigned bus, school start/dismissal times (work backward to pickups), geographic clustering (simple quadrants + rural/outlier rules), and student comfort (ride time / mileage / headcount). AM and PM routes mirror each other, but a student may ride AM-only, PM-only, or both; stops remain for occasional riders. Recalculate on student assignment; year-start auto-assign with map override; toast when assign would break arrival goals or overload a bus; suggest additional routes past thresholds. School-to-school transfer routes follow the same logic independently. Must work for small districts and scale to >100 riders across a medium–large city.

## Baseline (as of draft)

| Area               | Current                                                                      | Target                                                                                 |
| ------------------ | ---------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Assign to route    | Capacity fill by route name (`AutoAssignStudentsAsync`); no geo/time planner | Suggest/create routes from student geography + school times + bus seats                |
| Capacity           | Route/bus capacity checks on assign                                          | Soft guidance; **hard** limit = assigned bus seating capacity                          |
| Pickup times       | Manual / transfer times only                                                 | Derived backward from school start (AM) / dismissal (PM)                               |
| Geography          | Waypoints rebuild (home → optional transfer stops → school); no clustering   | Quadrants + rural outlier split; optimize miles, time, count                           |
| Feedback on assign | Fail only when already at capacity / already assigned                        | Toast: arrival risk, overload, suggest another route; suggest new route past threshold |
| Transfers          | Separate entity + UI; not planned as a fleet                                 | Independent planner with same rules as home→school                                     |
| Year start         | Manual / bulk assign                                                         | Auto-assign with map override for outliers                                             |

Constitution: Syncfusion-only UI, Serilog-only logging, hybrid Mac/Windows, no cloud app hosting, no committed secrets. Builds on school Destinations, student geo, Maps drive paths (007), and transfer records (PR #36).

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Year-start auto route build for a school (Priority: P1)

As a transportation director, at the start of the year I select a school with start/dismissal times and its assigned riders; the system proposes the minimum set of AM routes (and mirrored PM routes), assigns students into those routes within bus seating limits and geographic clusters, and leaves occasional-rider stops on the path even if some students are AM-only or PM-only.

**Why this priority**: This is the core value for districts with many riders; everything else (toasts, transfers) builds on it.

**Independent Test**: With ≥12 geocoded students at one school and at least one bus seating capacity ≥12 in one geographic cluster, the system proposes one AM route (and mirrored PM) without exceeding seating. With students spread across distant quadrants such that one route would create large pickup gaps, the system proposes more than one route. Clerk can move a student between proposed routes on the map without losing the stop for occasional riders.

**Acceptance Scenarios**:

1. **Given** a school with start time and N students with home coordinates all near one quadrant, **When** year-start generation runs, **Then** the fewest routes that fit bus seating are proposed and students are auto-assigned AM (and mirrored PM where the student rides both or PM).
2. **Given** students whose homes would create an excessive gap on a single route (rural/outlier rule), **When** generation runs, **Then** outliers are placed on a separate suggested route (or flagged for override).
3. **Given** a student marked AM-only, **When** mirrored PM routes are built, **Then** the stop remains on the PM route for occasional riders but the student is not required as a daily PM assignment.
4. **Given** proposed routes on the map, **When** the clerk overrides an outlier assignment, **Then** the change persists and recalculation respects the override until cleared.

---

### User Story 2 - Assign-time toast and route suggestion (Priority: P1)

As a clerk assigning a student to transportation, when the assignment would overload the bus seating capacity or prevent meeting school arrival goals for that route, I see a clear toast and a suggestion to use another route (or create a new one past threshold)—without losing the ability to override when needed.

**Why this priority**: Day-to-day enrollment changes must stay flexible while protecting comfort and capacity.

**Independent Test**: Assigning one more student above hard seating capacity is blocked or requires explicit override after toast. Assigning a student who would break the backward-calculated arrival window shows a toast and suggests an alternate route when one exists.

**Acceptance Scenarios**:

1. **Given** a route at assigned-bus seating capacity, **When** the clerk assigns another student, **Then** a toast explains overload and the system suggests another route or a new route if past threshold.
2. **Given** a route whose current riders already fill the time budget to school start, **When** assigning a farther student, **Then** a toast warns that optimal arrival goals would be missed and suggests alternatives.
3. **Given** toast shown for time/geo risk, **When** the clerk continues, **Then** assignment proceeds (warn-and-allow). **Given** hard seating capacity would be exceeded, **When** the clerk assigns without an explicit override, **Then** assignment does not proceed.

---

### User Story 3 - School times drive pickup schedule (Priority: P2)

As a clerk placing a school on the map, I enter school start (and dismissal) times; assigned students’ pickup times are computed working backward along the route order so the bus arrives by start time.

**Why this priority**: Time windows are the constraint that forces additional routes beyond raw headcount.

**Independent Test**: Changing school start time regenerates pickup times for students on routes serving that school without changing route membership unless a toast/recalc indicates a constraint break.

**Acceptance Scenarios**:

1. **Given** a school with a confirmed start time and an ordered morning route, **When** times are computed, **Then** each pickup is early enough that the bus arrives at that school by its start, using Google leg drive times when routing is configured and the average-speed fallback when it is not.
2. **Given** a special-needs route that serves more than one school, **When** times are computed, **Then** each school stop is due at that school’s own start, and a pickup ordered after that rider’s school fails the plan without changing published clocks.
3. **Given** a confirmed dismissal time, **When** the afternoon route is scheduled, **Then** the bus is at each school at that dismissal and home drop-offs work forward from there. A long afternoon still publishes.
4. **Given** a school whose start and dismissal are still null, **When** a route that serves it is timed, **Then** that school is not used as a deadline and the clerk is warned. The form’s `08:00` / `15:30` example is not saved as the bell.

---

### User Story 4 - Independent transfer route planning (Priority: P2)

As a director, school-to-school transfer riders are planned with the same capacity, geography, and time logic as home→school, but as a separate set of routes so transfer demand does not silently consume home-route seats.

**Why this priority**: Transfers are uncommon but must not distort primary routing.

**Independent Test**: Generating home→school routes ignores transfer-only legs for seating on those routes; generating transfer routes uses transfer pickup/dropoff locations and times and produces its own minimum route set.

**Acceptance Scenarios**:

1. **Given** active transfer assignments with pickup/dropoff locations and times, **When** transfer planning runs, **Then** a separate route set is suggested under the same minimize-buses / comfort rules.
2. **Given** home→school generation, **When** it runs, **Then** it does not treat transfer stop pairs as substitutes for home pickups on home routes (home routes still use home coordinates).

---

### Edge Cases

- Student with no coordinates: exclude from auto-geo clustering; flag for manual assign.
- Student AM-only or PM-only: stop retained on mirror; daily roster reflects ride mode.
- Multiple schools on ordinary year-start packing: plan per school, not one district-wide mega-route. A published special-needs route may already serve several schools. Time that route against each rider’s bell. Do not merge those riders into the per-school packer to fix the clocks.
- School bell missing: generation for that school fails closed on the morning plan. An unconfirmed bell does not default to 08:00.
- Soft vs hard capacity: soft warnings for “crowding” preferences; hard stop at assigned bus seating capacity unless explicit override policy says otherwise.
- Recalc on assign: must be incremental enough for >100 riders (not full district rebuild every click if avoidable); full rebuild allowed at year-start.
- Rural long deadhead: prefer splitting route over forcing one bus across the whole district.
- Existing manual route names: generation may create drafts; clerk confirms before replacing production assignments.

## Requirements _(mandatory)_

### Functional Requirements

- **FR-001**: System MUST treat assigned bus seating capacity as the hard maximum riders on a route slot unless an explicit override is recorded.
- **FR-002**: System MUST support soft capacity guidance (warnings) below that hard limit when configured.
- **FR-003**: System MUST store school start time (morning) and dismissal time (afternoon) on the school destination. Both stay null until the clerk enters them. The editor MUST NOT persist a placeholder `08:00` / `15:30` because the form opened. A null bell is unconfirmed and MUST NOT be used as a deadline.
- **FR-004**: System MUST compute published clocks with the single plan in `specs/routes.md` (Published clocks). Morning walks backward from each confirmed school start along the published stop order, with a 5-minute pickup dwell and depot dwell of zero, and writes the depot departure as the morning begin time. Afternoon starts at each confirmed dismissal and walks forward with the same dwell on home drop-offs. Drive time is `routes.legs.duration` on the existing Routes client, or straight-line distance at `AverageSpeedMph` marked as an estimate. A morning plan that would miss a bell or that picks up a rider after that rider’s school MUST warn, MUST leave published clocks unchanged, and MUST NOT clamp times to midnight. An afternoon plan that runs long MUST still publish. Session `AM` / `PM` / morning-or-afternoon `SpecialNeeds` selects the plan. A `-PM` name suffix MUST NOT.
- **FR-004a**: `PickupScheduleCalculator` and `PublishedStopClockPlanner` MUST NOT remain competing publishers of `ScheduledArrival` / `ScheduledDeparture`. Time Route, school-bell save, home-pin save, stop add/remove/reorder, and drive-path refresh all run the one plan. A rider exception MUST NOT. Regen MUST include every active route with a rider or a stop for that school, not only routes whose `Route.School` text equals that school.
- **FR-005**: System MUST cluster students using a simple quadrant scheme scaled to district geographic size, with rural/outlier rules that split riders when pickup gaps would be unreasonably large.
- **FR-006**: System MUST minimize the number of buses/routes as the primary objective while respecting ride time, distance, and seating comfort constraints.
- **FR-007**: System MUST mirror AM and PM route stop structures while allowing per-student ride mode: AM-only, PM-only, or both; occasional-rider stops MUST remain on the mirror.
- **FR-008**: System MUST recalculate route fitness (capacity/time/geo) when a student is assigned to transportation.
- **FR-009**: System MUST auto-assign students into proposed routes at year-start generation, with clerk map override for outliers.
- **FR-010**: System MUST show a toast (or equivalent non-blocking/blocking message per policy) when an assignment would prevent meeting arrival goals or would overload seating, and MUST suggest another route or a new route when past threshold.
- **FR-011**: System MUST plan school-to-school transfer routes with the same logic independently from home→school routes.
- **FR-012**: System MUST remain usable for small districts and scale to more than 100 riders in a medium–large city service area.
- **FR-013**: System MUST log generation and assign decisions with Serilog (counts, route ids, constraint violations—no secrets).
- **FR-014**: Transfer routes MUST be planned and seated in a **separate fleet / route pool** from home→school routes (same capacity/time/geo rules, independent seat inventory).
- **FR-015**: On assign, the system MUST **block** when the assignment would exceed the assigned bus seating capacity (hard limit), unless an explicit override is recorded; for arrival-time / geographic fitness risks the system MUST **warn-and-allow** (toast + proceed) so clerks can continue with awareness.
- **FR-016**: System MUST build geographic cells as an **N-cell grid derived from the district bounding box and rider density** (not a fixed 4-from-centroid only), with rural/outlier split when pickup gaps exceed configured thresholds.

### Key Entities

- **School destination**: Campus with map location, start time, dismissal time.
- **Student rider**: Home location, school, ride mode (AM/PM/both), optional transfer.
- **Route proposal**: Ordered stops, assigned/suggested bus seating, estimated miles/time, AM or PM slot.
- **Constraint toast**: Human-readable reason (overload, arrival risk) + suggested alternate.
- **Transfer route set**: Separate proposals using transfer pickup/dropoff pairs.

## Success Criteria _(mandatory)_

### Measurable Outcomes

- **SC-001**: For a single-school scenario of 12 nearby students and a bus seating ≥12, year-start generation proposes **1** AM route (and mirrored PM structure) without exceeding seating.
- **SC-002**: For students split across distant quadrants such that one route would create large pickup gaps, generation proposes **more than one** route rather than a single district-wide path.
- **SC-003**: A clerk can complete year-start auto-assign for a 100-rider school set and override at least one outlier on the map in one session without re-entering all students.
- **SC-004**: On assign that exceeds hard seating capacity, the clerk always receives an explicit toast/message and the assign is **blocked** unless an explicit override is recorded; time/geo risks warn-and-allow.
- **SC-005**: Changing a confirmed school start time updates computed pickup times for every active route that serves that school, without requiring manual re-entry of each stop time. A plan that would miss the bell leaves the previous clocks in place and warns.
- **SC-005a**: A pure clock-plan test with several schools, one shared home, and two stops for the same school passes without a live Routes call. The impossible order fails without writing midnight clocks.
- **SC-006**: Transfer planning produces a route count independent of home→school route count for the same student population when transfers exist.

## Assumptions

- Published clocks use Maps leg durations when a drive path is refreshed, and average-speed Haversine only as a labeled fallback. Assign-time fitness may use the fallback so a click does not wait on the network. Details: `specs/routes.md` Published clocks.
- “Quadrant” and “outlier gap” thresholds will be configurable district settings with sensible defaults for a small district and a city.
- Occasional-rider stops mean the stop stays in the path/order even if the student is not on the daily AM or PM roster for that day.
- PR #36 school destinations, student geo, and transfer records are available before implementation.
- Clerk Generate Routes is `IRouteDeterminationService`. Visit order is `IRouteOptimizationService` (`optimizeTours`). `RouteService` persists the published route and fills seats on a selected route. Local Ollama does not generate or reorder routes.
