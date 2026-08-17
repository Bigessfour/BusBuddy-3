# Data Model: 008 Route Determination

## Destination (School) — extend

| Field | Type | Rules |
|-------|------|-------|
| StartTime | TimeSpan? | Required for AM generation for that school |
| DismissalTime | TimeSpan? | Required for PM mirror schedule |
| (existing) Latitude/Longitude, Name, DistrictName | | Used as campus anchor |

**Validation**: Year-start generation fails with clear message if StartTime missing for AM (DismissalTime for PM).

## RoutingDistrictSettings (new or appsettings-backed)

| Field | Type | Rules |
|-------|------|-------|
| BoundingBox | lat/lon min/max or shapefile extent | District geographic frame |
| TargetRidersPerCell | int | Density hint for N-cell grid (default ~15–25) |
| MaxPickupGapMinutes | int | Outlier split threshold |
| AverageSpeedMph | double | Fallback ETA when Maps unavailable |
| MaxRideMinutes | int? | Soft comfort cap for warn-and-allow |
| AllowSeatingOverride | bool | If false, hard block has no UI override |

## Student ride mode

| Mode | Meaning |
|------|---------|
| AM | AMRoute set; PMRoute empty — stop kept on PM mirror for occasional riders |
| PM | PMRoute set; AMRoute empty — stop kept on AM mirror |
| Both | Both routes set |

Optional later: explicit `RideMode` column; MVP may derive from AM/PM route nullability.

## RouteProposal / draft Route

MVP may create real `Route` rows with a naming convention (`Draft-{School}-{Cell}-{n}`) or a `RouteProposals` table:

| Field | Type | Rules |
|-------|------|-------|
| SchoolDestinationId | int | FK |
| Slot | AM / PM | |
| FleetKind | HomeToSchool / Transfer | Separate pools (FR-014) |
| OrderedStudentIds | list | Stop order |
| SuggestedBusSeatingCapacity | int | From assigned/suggested bus |
| EstimatedMiles / EstimatedMinutes | double | |
| CellId | string | Density grid cell id |
| Status | Draft / Accepted / Rejected | |

Accepted proposals become or update operational `Route` + student AM/PM assignments.

## AssignFitnessResult (transient)

| Field | Type | Rules |
|-------|------|-------|
| Allowed | bool | False if seating hard-block without override |
| Severity | None / Warn / Block | |
| Reasons | string[] | Overload, arrival risk, geo outlier |
| SuggestedRouteIds | int[] | Alternates |
| SuggestNewRoute | bool | Past threshold |

## Transfer fleet

Uses `StudentSchoolTransfer` pickup/dropoff locations + times as stop inputs. `FleetKind=Transfer`. No seat sharing with HomeToSchool (FR-014).

## Relationships

```text
Destination (School) 1──* Route (HomeToSchool AM/PM)
Destination (School) 1──* Route (Transfer pool)     # separate
Student *──0..1 active StudentSchoolTransfer
Route 1──* ordered stops (students or transfer pairs)
Assign → AssignFitnessResult (transient)
```

## State transitions

```text
YearStart: Idle → Generating → DraftProposals → ClerkOverride → Accepted → Operational
Assign: CheckFitness → Warn (continue) | Block (stop) | OverrideRecorded → Assigned → WaypointsRefresh
```
