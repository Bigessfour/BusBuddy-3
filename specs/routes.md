# Routes

## Purpose

A route is the published, daily way the district picks up and drops off students. It is an ordered list of stops with target times, a session (AM, PM, Transfer, SpecialNeeds), and a default bus and driver. Parents and clerks plan around it. It is not a trip, and it is not a live GPS track.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not add hosted routing-as-a-service or automatic re-optimization that rewrites published times without a clerk. Clerk-initiated Google Route Optimization (`optimizeTours`) may propose a new stop order; the clerk still owns save and schedule times.

## Invariants

- MUST treat Route and Trip as different aggregates. See `specs/trips.md`.
- MUST belong to exactly one session: `AM`, `PM`, `Transfer`, or `SpecialNeeds`. Core keys AM and PM as **two route rows** (for example `Draft-School-cell-1` and `Draft-School-cell-1-PM`). `Route.Session` plus `RouteSession.Infer` name the session. Do not add a second session structure on the same row.
- MUST be an ordered list of stops. Each stop carries a validated, geocoded name/address/lat/lng plus a target time. The clerk picks the place from the location catalog (school `Destination`, `PickupStop`, or a validated student home); the stop then **denormalizes** those fields. There is no `RouteStop.LocationId` column — do not invent a parallel location graph.
- MUST use those stops as Google Routes waypoints. The map polyline is derived from the published stop list, not freehand drawing as source of truth.
- MUST keep year-default bus and driver on `Route.AMVehicleId` / `AMDriverId` (and the PM pair). Substitutes and spares are session exceptions on `Schedule`; they do not rewrite the published pairing unless the clerk changes the default. Leftover AM*/PM* columns on a single row are those year-default pairings (Hop 4b), not a second session.
- MUST keep published stop times stable and parent-visible. Same-day “student not riding” is a **rider exception**, not a new route version.
- MUST NOT invent a dated `RouteVersion` table. Mid-year structural edits (add/remove/reorder stop, change target time) update the current published stop list in place. `CloneRouteAsync` copies a run onto another calendar date when the clerk needs a dated variant. Do not silently drop stops.
- MUST NOT invent a parallel route model. Extend `BusBuddy.Core.Models.Route`, `IGeoDataService`, `IRoutingService`, `RouteDrivePathRefresher`. WPF binds that Core type — do not add a `RouteViewModel` DTO.
- MUST NOT use live vehicle position to define the path.
- Default: the published stop list is the official run. Core uniqueness is `(Date, RouteName)` because generate/clone persist calendar-dated rows. School year lives on `Student.SchoolYear`, not on `Route`.
- Official vs retired: `Route.IsActive` is the published/active flag. There is no separate `Published` column.
- Exception: rider absence that day, spare bus, substitute driver, weather/road notice in notes, or a cloned row on another date.

## Relationships

- Route `1` → `1` session (`AM` | `PM` | `Transfer` | `SpecialNeeds`).
- Route `1` → `0..1` default Bus (slot columns `AMVehicleId` / `PMVehicleId`).
- Route `1` → `0..1` default Driver (slot columns `AMDriverId` / `PMDriverId`).
- Route `1` → `1..*` ordered stops (denormalized place + target time + sequence).
- Route stop place comes from `specs/locations.md` (`School`, `PickupStop`, `StudentHome` for home/special-needs pickup, optional `Depot` pull-out) at edit time.
- Route `1` → `0..*` student assignments for that session (`Student.AmRouteId` / `PmRouteId`, names mirrored until the name-string drop).
- Route `1` → `0..*` rider exceptions (date + student + not riding).
- Geographic area is a planning label (Wiley in-town, rural east, special needs). It is not a required GIS polygon in this version.

## Data the app must store

| Field                       | Type            | Required      | Notes                                                              |
| --------------------------- | --------------- | ------------- | ------------------------------------------------------------------ |
| RouteId                     | existing key    | yes           | Core `Route`.                                                      |
| RouteNumberOrName           | string          | yes           | `Route.RouteName` — what clerks and students quote (“Route 5 AM”). |
| Date                        | date (UTC date) | yes           | Calendar day of this published row. Unique with `RouteName`.       |
| Session                     | string          | yes           | AM, PM, Transfer, SpecialNeeds (`Route.Session`).                  |
| AreaLabel                   | string          | no            | `Boundaries` / planning hint only.                                 |
| DefaultBusId                | bus id          | recommended   | `AMVehicleId` or `PMVehicleId` for this row’s session.             |
| DefaultDriverId             | driver id       | recommended   | `AMDriverId` or `PMDriverId`.                                      |
| IsActive                    | bool            | yes           | Official run when true; soft-retire when false.                    |
| PathDistance / PathDuration | from Routes API | after refresh | `Distance` / `EstimatedDuration` after drive-path refresh.         |
| Notes                       | string          | no            | Dirt roads, no turnaround, winter variant pointer.                 |

`SchoolYear` and a separate `Published` flag are **not** stored on `Route`. Year is a student/enrollment concern. `IsActive` is the official-run flag.

### Ordered stops (child collection)

| Field              | Type    | Required | Notes                                                         |
| ------------------ | ------- | -------- | ------------------------------------------------------------- |
| RouteStopId        | key     | yes      |                                                               |
| StopOrder          | int     | yes      | 1..n. Source of waypoint order.                               |
| StopName           | string  | yes      | Copied from the catalog place the clerk picked.               |
| StopAddress        | string  | yes      | Copied after Google Address Validation.                       |
| Latitude/Longitude | decimal | yes      | Validated coordinates. Unvalidated locations cannot be stops. |
| ScheduledArrival   | time    | yes      | Published time at this stop.                                  |
| ScheduledDeparture | time    | yes      | Published leave time.                                         |

`LocationId` and `StopKind` are **not** columns. Kind is implied by session and order (AM pickups then school; PM school then drop-offs). Do not add those columns as a hybrid beside the denormalized fields.

Estimated arrival/departure `DateTime` columns still exist so Postgres non-nullable timestamptz rows can persist; the clerk-facing times are the `TimeSpan` scheduled fields.

### Rider exceptions (child collection)

| Field         | Type   | Required | Notes                            |
| ------------- | ------ | -------- | -------------------------------- |
| ExceptionDate | date   | yes      | That school day only.            |
| StudentId     | fk     | yes      |                                  |
| Reason        | string | no       | Absent, parent drop-off, sports. |

Rider exceptions do not delete the student from the year assignment.

## Behaviors / UI

- Clerk builds or edits the ordered stop list from existing locations (Places + Address Validation in the stop dialog). Save refreshes the drive path (`RouteDrivePathRefresher` / Google Routes).
- Map draws the polyline from `RouteLinePoints` / waypoint path and pins each stop. Unvalidated locations cannot be stops.
- Published times on the stop list are what parents and substitute drivers see. Assignment **Schedule** opens those times for the selected route row — not the district-wide driver calendar.
- Marking “not riding today” only affects that date’s roster and any load count. Stop times stay published.
- Adding a mid-year catalog stop: new location first, then insert it on the current published list. Clone the route to another date if the clerk needs a dated variant.
- Transfer session routes move students school-to-school during the day. They are routes, not trips, because they repeat on the bell schedule.
- Special-needs session routes are home-pickup heavy. Do not force those students onto in-town catalog stops.
- Capacity warning uses the default (or session) bus vs that session’s riders.
- Print / PDF of the route may embed a map snapshot. That is a picture of the published path, not a live track.

## AM vs PM vs transfer

- AM: pickups then school destination.
- PM: school origin then drop-offs (often reverse of AM, but do not assume a perfect reverse — model PM as its own stop list).
- Transfer: school or program site to another school, repeating on school days.
- Special needs: may be AM or PM as separate route records; home stops dominate.

Implementation: **two route rows** (5 AM and 5 PM). Core already keys routes that way. Do not also store two session stop lists on one row.

## How this differs from a trip

|                  | Route                               | Trip                           |
| ---------------- | ----------------------------------- | ------------------------------ |
| When             | Every school day                    | One date                       |
| Times            | Published, stable                   | Expected to move               |
| Riders           | Year assignments + daily exceptions | Event manifest                 |
| Cancel one rider | Exception row                       | Manifest change or cancel trip |
| Path             | Published waypoints                 | Path for that outing           |

## Out of scope

- Live ETA from the moving bus.
- Solver that auto-rebuilds stop order every night. Clerk **Optimize Order** (`IRouteOptimizationService`) is in scope.
- Parent app that edits stops.
- Treating a Friday-only activity bus as a route unless the clerk publishes it as one.
- A `RouteVersion` / effective-date history table.
- Shapefile overlays on `Route` (`DistrictBoundaryShapefilePath` / `TownBoundaryShapefilePath` are leftover columns; maps use Google tiles only).

## Code anchors

| Spec term              | Existing code                                                   |
| ---------------------- | --------------------------------------------------------------- |
| Route record           | `BusBuddy.Core.Models.Route`                                    |
| Session                | `Route.Session`, `RouteSession`                                 |
| Stop / waypoint access | `IGeoDataService`, `IMapsGeoService`                            |
| Drive path             | `IRoutingService`, `RouteDrivePathRefresher`, Google Routes API |
| Visit order            | `IRouteOptimizationService` (`optimizeTours`); pins start/end   |
| Polyline on map        | `MapViewModel.RouteLinePoints`, `RouteLineUpdated`              |
| Students on the run    | `Student.AmRouteId` / `PmRouteId` via `StudentRouteAssignment`  |
| Default bus            | `IBusService` + `Route.AMVehicleId` / `PMVehicleId`             |
| AutoMapper (not maps)  | `MappingService`                                                |

## Worked examples

- Published in-town AM: depot optional, catalog stops in sequence, Wiley School last, target times on each stop, Bus #2, same weekday pattern via generate/clone.
- Special-needs AM Bus #5: ordered **homes** + school, not corner stops, aide expected, capacity includes wheelchair stations.
- Student sick Thursday: rider exception for that date. Published times unchanged.
- New house in October: validate home, clerk inserts stop at sequence 4 on the current list.

## Agent instructions

When changing route code, read this file plus `specs/locations.md`, `specs/students.md`, `specs/buses.md`, `specs/drivers.md`, `specs/trips.md`, and `specs/maps.md`. Quote the invariant you implemented. Never implement “student not riding” by deleting a stop or cloning Route into Trip.
