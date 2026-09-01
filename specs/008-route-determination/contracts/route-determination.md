# Contract: Route Determination Service

**Interface (Core)**: `IRouteDeterminationService`

## GenerateAndAssignAsync

**Input**:

- `schoolDestinationId` (int)
- `slot` — `AM` | `PM` | `Both` (Both ⇒ generate AM then mirror PM)
- `fleetKind` — `HomeToSchool` | `Transfer`
- `options` — dry-run (proposals only) vs persist assignments

**Output**:

- `RouteGenerationResult` — list of proposals, assigned student counts, Serilog-correlated `OperationId`, warnings

**Rules**:

- HomeToSchool uses student home coordinates + school StartTime/DismissalTime.
- Transfer uses active `StudentSchoolTransfer` pickup/dropoff pairs only; separate seating pool.
- Minimize route count subject to seating capacity and gap/time thresholds.
- Students without coordinates are listed in `UnclusteredStudentIds` (manual).

## RecalculateOnAssignAsync

**Input**: `studentId`, `routeId`, `slot`, `overrideSeating` (bool)

**Output**: `AssignFitnessResult` (see [assign-fitness.md](./assign-fitness.md))

**Side effects** (when Allowed):

- Persist AM/PM assignment via existing `IRouteService`
- Trigger waypoint rebuild for affected route(s)

## ApplyClerkOverrideAsync

**Input**: `studentId`, `fromRouteId`, `toRouteId`, `slot`, reason

**Output**: success/failure; keeps occasional-rider stop on mirror when ride mode is one-sided

## Logging (Serilog)

Must emit structured events such as:

- `Route generation completed School={SchoolId} Fleet={Fleet} Routes={N} Students={S}`
- `Assign fitness Blocked|Warned Student={Id} Route={Id} Reasons={Reasons}`
