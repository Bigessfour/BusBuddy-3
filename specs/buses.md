# Buses

## Purpose

A bus is a district-owned student-transport vehicle. Students recognize it by its number. BusBuddy-3 tracks the asset, its capacity and equipment, its default route, and proof that inspections, fuel, and maintenance happened. The bus is not the route, and it is not live GPS.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not add telematics, ELD, or a hosted fleet dashboard to satisfy this spec.

## Invariants

- MUST treat the visible bus number as the student-facing key (“that is my bus”).
- MUST record ownership as the school district. Do not model contractor fleets unless a later spec says so.
- MUST distinguish vehicle type: at least `Regular` and `SpecialNeeds` (lift, wheelchair stations, aide position as applicable).
- MUST store seating/wheelchair capacity and compare it to the assigned session roster. Warn on overflow; do not silently assign.
- MUST keep a default (home) route for the year without forbidding a spare or substitute bus for a day or a trip.
- MUST store inspections, fuel, and maintenance as dated child records — not as flags on the bus row.
- MUST require daily pre-trip and post-trip as operational duties (logged by the on-duty driver; see `specs/drivers.md`).
- MUST NOT overwrite the home-route assignment when the bus is loaned to a trip. See `specs/trips.md`.
- MUST NOT plot live vehicle position. District Map shows schools, students, stops, and route paths. Live fleet GPS is deferred.
- MUST NOT invent a parallel vehicle model. Extend `IBusService` and `BusBuddy.Core.Models.Bus`.
- Default: one bus number ↔ one home route for the school year; students expect that pairing.
- Exception: spare bus, shop time, trip after the AM run, mid-year route rematch.

## Relationships

- Bus `1` → `0..1` home Route (default published run).
- Bus `1` → `0..*` trip loans (temporary).
- Bus `1` → `0..*` inspection / fuel / maintenance events.
- Bus `1` → `0..1` driver on duty for a session (via assignment or duty log).
- Special-needs bus → special-needs route + aide-capable staffing.
- Students are assigned to a **route**, and they recognize the **bus number** that normally runs that route.

## Data the app must store

| Field              | Type                        | Required           | Notes                                           |
| ------------------ | --------------------------- | ------------------ | ----------------------------------------------- |
| BusId              | existing key                | yes                | Core `Bus` identity.                            |
| BusNumber          | string                      | yes                | What students see. Unique in the district.      |
| AssetName          | string                      | no                 | Optional garage name.                           |
| VehicleType        | enum: Regular, SpecialNeeds | yes                | Equipped vs standard.                           |
| HasLift            | bool                        | when special needs |                                                 |
| WheelchairStations | int                         | when special needs |                                                 |
| SeatedCapacity     | int                         | yes                | Body capacity.                                  |
| Active             | bool                        | yes                | False if sold, totaled, or parked for the year. |
| HomeRouteId        | route id                    | no                 | Default year pairing.                           |
| HomeDepotId        | location id                 | recommended        | `DistrictDepot` / depot location.               |
| Notes              | string                      | no                 | “No highway if winds high”, radio #.            |

### Inspection, fuel, maintenance (child collections)

| Field          | Type                                                                | Required            | Notes                                                                                 |
| -------------- | ------------------------------------------------------------------- | ------------------- | ------------------------------------------------------------------------------------- |
| EventId        | key                                                                 | yes                 | Shared idea with driver duty logs; do not duplicate if one table already covers both. |
| BusId          | fk                                                                  | yes                 |                                                                                       |
| DriverId       | fk                                                                  | when pre/post/fuel  | Who did it.                                                                           |
| EventType      | enum: PreTrip, PostTrip, Defect, Fuel, ScheduledMaintenance, Repair | yes                 |                                                                                       |
| EventedAt      | datetime                                                            | yes                 |                                                                                       |
| Mileage        | int                                                                 | recommended         | Especially fuel and shop.                                                             |
| FuelQuantity   | decimal                                                             | when fuel           | Proof of fueling.                                                                     |
| Notes / defect | string                                                              | when defect or shop | Supervisor sees defects.                                                              |
| Completed      | bool                                                                | yes                 | Proof of completion for inspection/shop.                                              |

Minimum inspections are daily pre-trip and post-trip plus whatever the district already schedules in the shop. This spec does not invent a full CMMS.

## Behaviors / UI

- Clerk/supervisor adds a bus with number, type, capacity, depot, and optional home route.
- Route assignment screen shows the default bus number next to the route so rosters and parent notices stay consistent.
- Same-day spare: supervisor points that session at a different bus. HomeRouteId on both vehicles stays put.
- Trip assignment warns if the bus is still due on a published PM route.
- Capacity check runs against the AM or PM roster (or trip headcount), including wheelchair stations for special-needs loads.
- Defect from a pre/post-trip appears on the supervisor’s maintenance list. Parking the bus is a human decision plus `Active` or an out-of-service note — not automatic.
- Map may show the **route path** and the **bus number as a label**. It must not imply a live coordinate for the vehicle.

## How students use the number

When a student sees their assigned number, they expect that body to run their published route. Software should preserve that pairing as the default. A spare-bus day is an exception the office knows about; do not casually rotate numbers in the UI.

## Out of scope

- Live AVL / GPS pings, breadcrumbs, or “where is bus 5.”
- Fuel-card vendor imports (manual or later integration).
- Parts inventory and work-order accounting.
- Contractor or activity-van fleets.
- AWS IoT or any hosted telematics.

## Code anchors

| Spec term             | Existing code                                                                                                         |
| --------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Bus record            | `BusBuddy.Core.Models.Bus`, `IBusService`, `IBusRepository`                                                           |
| SQL table             | `Vehicles` / `VehicleId` — legacy names; do not add a `Vehicle` CLR type. Rename table only via a later EF migration. |
| Fleet clerk chrome    | `VehicleForm` / `VehiclesView` wrap `Bus`. Filenames still say Vehicle.                                               |
| Map list of buses     | `MapViewModel` active-bus collection — labels only, no live track                                                     |
| Home depot            | `DistrictDepot`, location type `Depot`                                                                                |
| Route pairing         | `BusBuddy.Core.Models.Route`                                                                                          |
| Driver inspections    | duty events in `specs/drivers.md`                                                                                     |
| AutoMapper (not maps) | `MappingService`                                                                                                      |

## Worked examples

- Home pairing: Bus #5, `VehicleType=SpecialNeeds`, lift + wheelchair stations, home route = AM special-needs run for 2026-2027. Students look for #5.
- Spare Thursday: #5 in the shop; #12 runs that AM session. Duty logs attach to #12 and the substitute or regular driver. #5 HomeRouteId is unchanged.
- Trip: #5 finishes AM, then is assigned to a 2:30 volleyball trip. PM home route needs another bus or the trip waits.
- Capacity: special-needs AM roster plus two wheelchairs. If stations < 2, warn before publish.

## Agent instructions

When changing bus code, read this file plus `specs/drivers.md`, `specs/routes.md`, `specs/trips.md`, and `specs/maps.md`. Quote the invariant you implemented. Do not attach live coordinates to `Bus`. Do not store inspections only as booleans on the vehicle. Do not invent `Vehicle.cs` or `IVehicleRepository`. `AssignVehicleCommand` / `AMVehicleId` are session FKs to `Bus`.
