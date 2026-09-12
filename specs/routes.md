# Routes

## Purpose

A route is the published, daily way the district picks up and drops off students. It is an ordered list of stops with target times, a session (AM, PM, Transfer, SpecialNeeds), and a default bus and driver. Parents and clerks plan around it. It is not a trip, and it is not a live GPS track.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not add hosted routing-as-a-service or automatic re-optimization that rewrites published times without a clerk. Clerk-initiated Google Route Optimization (`optimizeTours`) may propose a new stop order; the clerk still owns save and schedule times.

## Invariants

- MUST treat Route and Trip as different aggregates. See `specs/trips.md`.
- MUST belong to exactly one session: `AM`, `PM`, `Transfer`, or `SpecialNeeds`. Do not hide session behind a boolean on a mixed record unless Core already did that — then name the session in UI and specs anyway.
- MUST be an ordered list of stops. Each stop is a location from `specs/locations.md` plus a target time.
- MUST use those stops as Google Routes waypoints. The map polyline is derived from the published stop list, not freehand drawing as source of truth.
- MUST have a default bus and default driver for the school year. Substitutes and spares are session exceptions; they do not rewrite the published pairing unless the clerk changes the default.
- MUST keep published stop times stable and parent-visible. Same-day “student not riding” is a **rider exception**, not a new route version.
- MUST version mid-year structural edits (add/remove/reorder stop, change target time) with an effective date. Do not silently rewrite history.
- MUST NOT invent a parallel route model. Extend `BusBuddy.Core.Models.Route`, `IGeoDataService`, `IRoutingService`, `RouteDrivePathRefresher`.
- MUST NOT use live vehicle position to define the path.
- Default: same stops, same times, every school day of the year.
- Exception: rider absence that day, spare bus, substitute driver, weather/road notice in notes, or a dated route version.

## Relationships

- Route `1` → `1` session (`AM` | `PM` | `Transfer` | `SpecialNeeds`).
- Route `1` → `0..1` default Bus.
- Route `1` → `0..1` default Driver.
- Route `1` → `1..*` ordered stops (location + target time + sequence).
- Route stop → Location (`School`, `PickupStop`, `StudentHome` for home/special-needs pickup, optional `Depot` pull-out).
- Route `1` → `0..*` student assignments for that session.
- Route `1` → `0..*` rider exceptions (date + student + not riding).
- Route `1` → `0..*` versions (effective date + stop list snapshot or equivalent).
- Geographic area is a planning label (Wiley in-town, rural east, special needs). It is not a required GIS polygon in this version.

## Data the app must store

| Field                       | Type            | Required      | Notes                                              |
| --------------------------- | --------------- | ------------- | -------------------------------------------------- |
| RouteId                     | existing key    | yes           | Core `Route`.                                      |
| RouteNumberOrName           | string          | yes           | What clerks and students quote (“Route 5 AM”).     |
| SchoolYear                  | string/year     | yes           | e.g. 2026-2027.                                    |
| Session                     | enum            | yes           | AM, PM, Transfer, SpecialNeeds.                    |
| AreaLabel                   | string          | no            | Planning hint only.                                |
| DefaultBusId                | bus id          | recommended   | Student-facing number comes from the bus.          |
| DefaultDriverId             | driver id       | recommended   | Home driver.                                       |
| Published                   | bool            | yes           | Visible as the official run.                       |
| Active                      | bool            | yes           | Soft-retire.                                       |
| PathDistance / PathDuration | from Routes API | after refresh | Efficiency metrics; store on the current version.  |
| Notes                       | string          | no            | Dirt roads, no turnaround, winter variant pointer. |

### Ordered stops (child collection)

| Field       | Type                                            | Required    | Notes                           |
| ----------- | ----------------------------------------------- | ----------- | ------------------------------- |
| RouteStopId | key                                             | yes         |                                 |
| Sequence    | int                                             | yes         | 1..n. Source of waypoint order. |
| LocationId  | location id                                     | yes         | Must be validated + geocoded.   |
| TargetTime  | time                                            | yes         | Published time at this stop.    |
| StopKind    | enum: PullOut, Pickup, Transfer, School, Return | recommended | Helps AM vs PM direction.       |

### Rider exceptions (child collection)

| Field         | Type   | Required | Notes                            |
| ------------- | ------ | -------- | -------------------------------- |
| ExceptionDate | date   | yes      | That school day only.            |
| StudentId     | fk     | yes      |                                  |
| Reason        | string | no       | Absent, parent drop-off, sports. |

Rider exceptions do not delete the student from the year assignment.

## Behaviors / UI

- Clerk builds or edits the ordered stop list from existing locations. Save refreshes the drive path (`RouteDrivePathRefresher` / Google Routes).
- Map draws the polyline from `RouteLinePoints` / waypoint path and pins each stop. Unvalidated locations cannot be stops.
- Published times on the stop list are what parents and substitute drivers see.
- Marking “not riding today” only affects that date’s roster and any load count. Stop times stay published.
- Adding a mid-year catalog stop: new location first, then a new route version with effective date, then attach students.
- Transfer session routes move students school-to-school during the day. They are routes, not trips, because they repeat on the bell schedule.
- Special-needs session routes are home-pickup heavy. Do not force those students onto in-town catalog stops.
- Capacity warning uses the default (or session) bus vs that session’s riders.
- Print / PDF of the route may embed a map snapshot. That is a picture of the published path, not a live track.

## AM vs PM vs transfer

- AM: pickups then school destination.
- PM: school origin then drop-offs (often reverse of AM, but do not assume a perfect reverse — model PM as its own stop list).
- Transfer: school or program site to another school, repeating on school days.
- Special needs: may be AM or PM as separate route records; home stops dominate.

Pick one implementation: either two route rows (5 AM and 5 PM) or one route with two session stop lists. Prefer **two route rows** if Core already keys routes that way. Do not invent both.

## How this differs from a trip

|                  | Route                               | Trip                           |
| ---------------- | ----------------------------------- | ------------------------------ |
| When             | Every school day                    | One date                       |
| Times            | Published, stable                   | Expected to move               |
| Riders           | Year assignments + daily exceptions | Event manifest                 |
| Cancel one rider | Exception row                       | Manifest change or cancel trip |
| Path             | Year waypoints                      | Path for that outing           |

## Out of scope

- Live ETA from the moving bus.
- Solver that auto-rebuilds stop order every night. Clerk **Optimize Order** (`IRouteOptimizationService`) is in scope.
- Parent app that edits stops.
- Treating a Friday-only activity bus as a route unless the clerk publishes it as one.

## Code anchors

| Spec term              | Existing code                                                   |
| ---------------------- | --------------------------------------------------------------- |
| Route record           | `BusBuddy.Core.Models.Route` (`RouteModel` in WPF)              |
| Stop / waypoint access | `IGeoDataService`, `IMapsGeoService`                            |
| Drive path             | `IRoutingService`, `RouteDrivePathRefresher`, Google Routes API |
| Visit order            | `IRouteOptimizationService` (`optimizeTours`); pins start/end   |
| Polyline on map        | `MapViewModel.RouteLinePoints`, `RouteLineUpdated`              |
| Students on the run    | `IStudentService` assignments                                   |
| Default bus            | `IBusService`                                                   |
| AutoMapper (not maps)  | `MappingService`                                                |

## Worked examples

- Published in-town AM: depot optional, catalog stops in sequence, Wiley School last, target times on each stop, Bus #2, same every weekday.
- Special-needs AM Bus #5: ordered **homes** + school, not corner stops, aide expected, capacity includes wheelchair stations.
- Student sick Thursday: rider exception for that date. Route version and parent-published times unchanged.
- New house in October: validate home, clerk inserts stop at sequence 4, new version effective next Monday.

## Agent instructions

When changing route code, read this file plus `specs/locations.md`, `specs/students.md`, `specs/buses.md`, `specs/drivers.md`, `specs/trips.md`, and `specs/maps.md`. Quote the invariant you implemented. Never implement “student not riding” by deleting a stop or cloning Route into Trip.
