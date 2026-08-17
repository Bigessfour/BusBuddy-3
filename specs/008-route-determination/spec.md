# Feature Specification: Route Determination / Fleet Sizing

**Feature Branch**: `feature/008-route-determination`

**Created**: 2026-08-17

**Status**: Draft

**Input**: From a listing of students (home/pickup location, AM/PM ride mode, school assignment), determine the minimum number of routes and buses needed while respecting seating capacity of the assigned bus, school start/dismissal times (work backward to pickups), geographic clustering (simple quadrants + rural/outlier rules), and student comfort (ride time / mileage / headcount). AM and PM routes mirror each other, but a student may ride AM-only, PM-only, or both; stops remain for occasional riders. Recalculate on student assignment; year-start auto-assign with map override; toast when assign would break arrival goals or overload a bus; suggest additional routes past thresholds. School-to-school transfer routes follow the same logic independently. Must work for small districts and scale to >100 riders across a medium–large city.

## Baseline (as of draft)

| Area | Current | Target |
| ---- | ------- | ------ |
| Assign to route | Capacity fill by route name (`AutoAssignStudentsAsync`); no geo/time planner | Suggest/create routes from student geography + school times + bus seats |
| Capacity | Route/bus capacity checks on assign | Soft guidance; **hard** limit = assigned bus seating capacity |
| Pickup times | Manual / transfer times only | Derived backward from school start (AM) / dismissal (PM) |
| Geography | Waypoints rebuild (home → optional transfer stops → school); no clustering | Quadrants + rural outlier split; optimize miles, time, count |
| Feedback on assign | Fail only when already at capacity / already assigned | Toast: arrival risk, overload, suggest another route; suggest new route past threshold |
| Transfers | Separate entity + UI; not planned as a fleet | Independent planner with same rules as home→school |
| Year start | Manual / bulk assign | Auto-assign with map override for outliers |

Constitution: Syncfusion-only UI, Serilog-only logging, hybrid Mac/Windows, no cloud app hosting, no committed secrets. Builds on school Destinations, student geo, Maps drive paths (007), and transfer records (PR #36).

## User Scenarios & Testing *(mandatory)*

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
3. **Given** toast shown, **When** the clerk confirms an allowed override (where policy allows), **Then** assignment proceeds and is logged; when override is not allowed for hard seating, assignment does not proceed.

---

### User Story 3 - School times drive pickup schedule (Priority: P2)

As a clerk placing a school on the map, I enter school start (and dismissal) times; assigned students’ pickup times are computed working backward along the route order so the bus arrives by start time.

**Why this priority**: Time windows are the constraint that forces additional routes beyond raw headcount.

**Independent Test**: Changing school start time regenerates pickup times for students on routes serving that school without changing route membership unless a toast/recalc indicates a constraint break.

**Acceptance Scenarios**:

1. **Given** a school with start time and an ordered AM route, **When** times are computed, **Then** each stop has a pickup time such that estimated arrival at school meets start time under the configured average speed / drive-path estimate.
2. **Given** PM dismissal time, **When** mirrored PM is scheduled, **Then** dropoff/home times work forward from dismissal consistently with AM mirror stops.

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
- Multiple schools / campuses: plan per school (or per destination), not one district-wide mega-route.
- Soft vs hard capacity: soft warnings for “crowding” preferences; hard stop at assigned bus seating capacity unless explicit override policy says otherwise.
- Recalc on assign: must be incremental enough for >100 riders (not full district rebuild every click if avoidable); full rebuild allowed at year-start.
- Rural long deadhead: prefer splitting route over forcing one bus across the whole district.
- Existing manual route names: generation may create drafts; clerk confirms before replacing production assignments.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST treat assigned bus seating capacity as the hard maximum riders on a route slot unless an explicit override is recorded.
- **FR-002**: System MUST support soft capacity guidance (warnings) below that hard limit when configured.
- **FR-003**: System MUST store school start time (AM) and dismissal time (PM) on the school destination (or equivalent) when the school is placed/edited on the map.
- **FR-004**: System MUST compute student pickup times by working backward from school start along the ordered AM route (and forward from dismissal on PM).
- **FR-005**: System MUST cluster students using a simple quadrant scheme scaled to district geographic size, with rural/outlier rules that split riders when pickup gaps would be unreasonably large.
- **FR-006**: System MUST minimize the number of buses/routes as the primary objective while respecting ride time, distance, and seating comfort constraints.
- **FR-007**: System MUST mirror AM and PM route stop structures while allowing per-student ride mode: AM-only, PM-only, or both; occasional-rider stops MUST remain on the mirror.
- **FR-008**: System MUST recalculate route fitness (capacity/time/geo) when a student is assigned to transportation.
- **FR-009**: System MUST auto-assign students into proposed routes at year-start generation, with clerk map override for outliers.
- **FR-010**: System MUST show a toast (or equivalent non-blocking/blocking message per policy) when an assignment would prevent meeting arrival goals or would overload seating, and MUST suggest another route or a new route when past threshold.
- **FR-011**: System MUST plan school-to-school transfer routes with the same logic independently from home→school routes.
- **FR-012**: System MUST remain usable for small districts and scale to more than 100 riders in a medium–large city service area.
- **FR-013**: System MUST log generation and assign decisions with Serilog (counts, route ids, constraint violations—no secrets).
- **FR-014**: Transfer fleet coupling is [NEEDS CLARIFICATION: separate fleet only vs shared buses with time gaps].
- **FR-015**: Assign toast policy is [NEEDS CLARIFICATION: warn-and-allow vs block until alternate/override].
- **FR-016**: Quadrant construction is [NEEDS CLARIFICATION: fixed 4 from school centroid vs N from district bounding box/density].

### Key Entities

- **School destination**: Campus with map location, start time, dismissal time.
- **Student rider**: Home location, school, ride mode (AM/PM/both), optional transfer.
- **Route proposal**: Ordered stops, assigned/suggested bus seating, estimated miles/time, AM or PM slot.
- **Constraint toast**: Human-readable reason (overload, arrival risk) + suggested alternate.
- **Transfer route set**: Separate proposals using transfer pickup/dropoff pairs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a single-school scenario of 12 nearby students and a bus seating ≥12, year-start generation proposes **1** AM route (and mirrored PM structure) without exceeding seating.
- **SC-002**: For students split across distant quadrants such that one route would create large pickup gaps, generation proposes **more than one** route rather than a single district-wide path.
- **SC-003**: A clerk can complete year-start auto-assign for a 100-rider school set and override at least one outlier on the map in one session without re-entering all students.
- **SC-004**: On assign that exceeds hard seating capacity, the clerk always receives an explicit toast/message before the system either blocks or records an override (per clarified policy).
- **SC-005**: Changing school start time updates computed pickup times for affected routes without requiring manual re-entry of each stop time.
- **SC-006**: Transfer planning produces a route count independent of home→school route count for the same student population when transfers exist.

## Assumptions

- Drive-time estimates may use existing Maps routing when configured, or a documented average-speed fallback when not (fail-open like 007).
- “Quadrant” and “outlier gap” thresholds will be configurable district settings with sensible defaults for Wiley-scale and city-scale.
- Occasional-rider stops mean the stop stays in the path/order even if the student is not on the daily AM or PM roster for that day.
- PR #36 school destinations, student geo, and transfer records are available before implementation.
- Existing `IStudentRouteOptimizer` capacity fill remains a fallback until 008 generation replaces or wraps it.
