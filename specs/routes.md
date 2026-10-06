# Routes

## Purpose

A route is the published, daily way the district picks up and drops off students. It is an ordered list of stops with target times, a session (AM, PM, Transfer, SpecialNeeds), and a default bus and driver. Parents and clerks plan around it. It is not a trip, and it is not a live GPS track.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not add hosted routing-as-a-service or a night solver that reorders stops and rewrites times. Clerk-initiated Google Route Optimization (`optimizeTours`) may propose a new stop order; the clerk still owns save. Recomputing published clocks after that save, or after a clerk changes a school bell, a stop, a home pin, or the drive path, is part of the save. It is not a second optimizer.

## Invariants

- MUST treat Route and Trip as different aggregates. See `specs/trips.md`.
- MUST belong to exactly one session: `AM`, `PM`, `Transfer`, or `SpecialNeeds`. Core keys AM and PM as **two route rows** (for example `Draft-School-cell-1` and `Draft-School-cell-1-PM`). `Route.Session` plus `RouteSession.Infer` name the session. Do not add a second session structure on the same row.
- MUST be an ordered list of stops. Each stop carries a validated, geocoded name/address/lat/lng plus a target time. The clerk picks the place from the location catalog (school `Destination`, `PickupStop`, or a validated student home); the stop then **denormalizes** those fields. There is no `RouteStop.LocationId` column — do not invent a parallel location graph. When the place is a student home (home pickup or special needs), a later change to that student's stored home rewrites that stop and clears the stored drive path so the map is drawn from the new place. A catalog stop shared with other riders stays put.
- MUST use those stops as Google Routes waypoints. The map polyline is derived from the published stop list, not freehand drawing as source of truth.
- MUST NOT send the drive path, the map, or the printed sheet to a student-home stop unless that student is assigned to the route (`AssignedRouteStops`). School, depot, and catalog stops stay when that place is still an active catalog location. A stop named for a school that is not an active destination is not on the path. Generated stops that still carry `StudentId` notes stay when the route has no roster yet.
- MUST keep year-default bus and driver on `Route.AMVehicleId` / `AMDriverId` (and the PM pair). Substitutes and spares are session exceptions on `Schedule`; they do not rewrite the published pairing unless the clerk changes the default. Leftover AM*/PM* columns on a single row are those year-default pairings (Hop 4b), not a second session.
- MUST keep published stop times parent-visible. Same-day “student not riding” is a **rider exception** and does **not** recompute clocks. A clerk change to a school bell, stop list, assigned home coordinate, or drive path does recompute clocks under [Published clocks](#published-clocks). A plan that would miss a bell does not overwrite the clocks that are already published.
- MUST publish `ScheduledArrival` / `ScheduledDeparture` from one clock plan. `PickupScheduleCalculator` and `PublishedStopClockPlanner` must not remain competing publishers. Time Route runs that plan. It does not walk forward from a typed start.
- MUST NOT invent a dated `RouteVersion` table. Mid-year structural edits (add/remove/reorder stop, change target time) update the current published stop list in place. `CloneRouteAsync` copies a run onto another calendar date when the clerk needs a dated variant. Do not silently drop stops.
- MUST NOT invent a parallel route model. Extend `BusBuddy.Core.Models.Route`, `IGeoDataService`, `IRoutingService`, `RouteDrivePathRefresher`. WPF binds that Core type — do not add a `RouteViewModel` DTO.
- MUST NOT use live vehicle position to define the path.
- Default: the published stop list is the official run. Core uniqueness is `(Date, RouteName)` because generate/clone persist calendar-dated rows. School year lives on `Student.SchoolYear`, not on `Route`.
- Official vs retired: `Route.IsActive` is the published/active flag. Clerk **Retire** sets `IsActive = false` and keeps students, stops, schedules, and trip rows. Hard-delete only an empty unpublished draft: `IsActive` is already false, and there are no stops, student AM/PM keys or name mirrors, `Schedules`, or `TripEvents.RouteId`. Do not Cascade `FK_Schedules_Route`.
- Exception: rider absence that day, spare bus, substitute driver, weather/road notice in notes, or a cloned row on another date.

## Relationships

- Route `1` → `1` session (`AM` | `PM` | `Transfer` | `SpecialNeeds`).
- Route `1` → `0..1` default Bus (slot columns `AMVehicleId` / `PMVehicleId`).
- Route `1` → `0..1` default Driver (slot columns `AMDriverId` / `PMDriverId`).
- Route `1` → `1..*` ordered stops (denormalized place + target time + sequence).
- Route stop place comes from `specs/locations.md` (`School`, `PickupStop`, `StudentHome` for home/special-needs pickup, optional `Depot` pull-out) at edit time.
- Route `1` → `0..*` student assignments for that session (`Student.AmRouteId` / `PmRouteId`, names mirrored until the name-string drop).
- Route `1` → `0..*` rider exceptions (date + student + not riding).
- Geographic area is a planning label (in-town, rural, special needs). It is not a required GIS polygon in this version.

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

`LocationId` and `StopKind` are **not** columns. Classify a stop by its denormalized name and address: the district depot, an active school `Destination`, or otherwise an assigned home or catalog stop. A morning route is not required to be pickups-then-one-school. A special-needs route may visit several schools. Do not add kind or location-id columns beside those fields.

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
- Special-needs session routes are home-pickup heavy. Do not force those students onto in-town catalog stops. Their published times come from [Published clocks](#published-clocks).
- Capacity check uses the default bus on that session versus the session roster (assigned riders minus same-day not-riding) and wheelchair riders versus that bus's wheelchair stations. A missing bus warns and does not invent seats. Assign with seating override off blocks overflow. The check lives on route assign (`RouteBusCapacity`), not on `IBusService`.
- Print / PDF of the route may embed a map snapshot. That is a picture of the published path, not a live track.

## AM vs PM vs transfer

- AM: homes or catalog stops, then the school or schools those riders attend. One school at the end is the usual in-town shape, not a rule that forbids a second school on a special-needs run.
- PM: schools, then drop-offs. Model PM as its own stop list. Do not assume it is a perfect reverse of AM.
- Transfer: school or program site to another school, repeating on school days. Time it with the same clock plan, using the destination school’s bell.
- Special needs: morning and afternoon are separate route records when both exist. Home stops dominate. A morning special-needs row is not an afternoon row because a PM bus column is filled in.

Session chooses the plan. `AM` is morning. `PM` is afternoon. `SpecialNeeds` is morning when its assigned riders ride the morning, and afternoon when they ride the afternoon. Do not infer the plan from a route name that ends in `-PM`, and do not infer afternoon from leftover PM vehicle or driver columns on a morning row.

Implementation: **two route rows** when both a morning and an afternoon run exist. Core already keys routes that way. Do not also store two session stop lists on one row.

## Published clocks

School bells live on the school `Destination`: `StartTime` and `DismissalTime`. A null bell is unconfirmed. The school form may show `HH:mm` as a format example. It must not save `08:00` or `15:30` just because the form opened. An unconfirmed bell is not a deadline. One pair is the school-day bell. Do not invent a second Friday bell. A day the district does not run is a day without a published route, not a hole inside the clock plan.

Drive time for each leg is `routes.legs.duration` from the existing Google Routes client (`IRoutingService`). Do not add a second routing client. When the key is missing or the call fails, use straight-line distance at `RoutingDistrictSettings.AverageSpeedMph` and mark the clocks as estimates. Assign-time fitness may use that fallback so a click does not wait on the network.

Dwell is one district setting, `RoutingDistrictSettings.StopDwellMinutes`, default **5**. It applies to a pickup or a home drop-off. The depot does not dwell. A school arrival is the bell. Do not subtract dwell from the bell to invent an earlier school arrival.

### Morning

Walk the published stop order backward.

- Each school stop is due at that school’s confirmed `StartTime`. Arriving earlier is allowed. A later school’s deadline may force an earlier arrival.
- A pickup’s departure is the next stop’s arrival minus that leg’s drive time. Its arrival is that departure minus dwell.
- The depot’s departure is the morning begin time already stored for the row (`AMBeginTime` on a morning row). Do not add a new begin-time column.

The plan fails, leaves the published clocks unchanged, and warns the clerk when any school would arrive after its start, or when a rider’s pickup is ordered after that rider’s school. Do not clamp those clocks to midnight and save them. A second visit to a school before that school’s riders are aboard is the same failure. Do not delete the extra stop to make the plan pass.

### Afternoon

Be at each school at that school’s confirmed `DismissalTime`. That is the pickup. Then walk forward: the next arrival is the previous departure plus the leg’s drive time, and each home drop-off dwells. A long afternoon still publishes. The depot departure is the drive from the depot to the first school so the bus is there at the first dismissal. Write it on the afternoon begin time. Do not fail the route for running long.

### When the plan runs

Run it, and save it only when it succeeds, after the clerk:

- saves a school bell
- changes an assigned student’s stored home, including a clerk driveway pin
- adds, removes, or reorders a stop
- refreshes the drive path
- chooses Time Route

Include every active route that has an assigned rider of that school or a stop matched to that school. `Route.School` is one label. It is not the list of schools the route serves.

A rider exception does not run the plan. Year-start packing still builds ordinary routes per school and still leaves special-needs riders off those routes for a hand-built special-needs route. Timing that published special-needs route against each rider’s own bell is required. Do not pull those riders back into the per-school packer to satisfy the clock.

## How this differs from a trip

|                  | Route                               | Trip                           |
| ---------------- | ----------------------------------- | ------------------------------ |
| When             | Every school day                    | One date                       |
| Times            | Published clocks from the bell plan | Expected to move               |
| Riders           | Year assignments + daily exceptions | Event manifest                 |
| Cancel one rider | Exception row                       | Manifest change or cancel trip |
| Path             | Published waypoints                 | Path for that outing           |

## Out of scope

- Live ETA from the moving bus.
- Solver that auto-rebuilds stop order every night. Clerk **Optimize Order** (`IRouteOptimizationService`) is in scope. Recomputing clocks after a clerk save is in scope and is not that solver.
- Parent app that edits stops.
- Treating a Friday-only activity bus as a route unless the clerk publishes it as one.
- A `RouteVersion` / effective-date history table.

## Code anchors

| Spec term               | Existing code                                                         |
| ----------------------- | --------------------------------------------------------------------- |
| Route record            | `BusBuddy.Core.Models.Route`                                          |
| Session                 | `Route.Session`, `RouteSession`                                       |
| Stop / waypoint access  | `IGeoDataService`, `IMapsGeoService`                                  |
| Drive path              | `IRoutingService`, `RouteDrivePathRefresher`, Google Routes API       |
| Published clocks        | One plan in route determination. Not a second Routes client.          |
| Visit order             | `IRouteOptimizationService` (`optimizeTours`); pins start/end         |
| Polyline on map         | `MapViewModel.RouteLinePoints`, `RouteLineUpdated`                    |
| Students on the run     | `Student.AmRouteId` / `PmRouteId` via `StudentRouteAssignment`        |
| Stops the bus may visit | `AssignedRouteStops` — student homes only when that rider is assigned |
| Default bus             | `IBusService` + `Route.AMVehicleId` / `PMVehicleId`                   |
| AutoMapper (not maps)   | `MappingService`                                                      |

## Worked examples

- Published in-town AM: depot optional, catalog stops in sequence, the school last, target times on each stop, a bus number, same weekday pattern via generate/clone.
- Special-needs morning bus: ordered homes and every school those riders attend, not corner stops. Each school stop is due at that school’s confirmed start. The barn departure is the result of the backward walk, not a typed start. Aide expected. Capacity includes wheelchair stations.
- Student sick Thursday: rider exception for that date. Published times unchanged.
- New house in October: validate home, clerk inserts stop at sequence 4 on the current list.

## Agent instructions

When changing route code, read this file plus `specs/locations.md`, `specs/students.md`, `specs/buses.md`, `specs/drivers.md`, `specs/trips.md`, and `specs/maps.md`. Quote the invariant you implemented. Never implement “student not riding” by deleting a stop or cloning Route into Trip. Published clocks have one writer, described above. Do not leave `PickupScheduleCalculator` and `PublishedStopClockPlanner` as competing publishers.

Clerk **Generate Routes** calls `IRouteDeterminationService`. Clerk **Optimize Order** calls `IRouteOptimizationService`. `RouteService` persists the published route and can fill seats on a route the clerk already selected. It does not generate a plan, and local Ollama does not either.
