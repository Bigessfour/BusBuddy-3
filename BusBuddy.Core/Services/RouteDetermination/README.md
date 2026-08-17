# Route Determination

Year-start fleet sizing and student assignment planner for BusBuddy (spec 008).

**Contracts**: `specs/008-route-determination/contracts/`

| Type | Role |
|------|------|
| `IRouteDeterminationService` | Generate/assign, assign fitness, clerk override, schedule regen |
| `DensityCellBuilder` | Bbox / density N-cell grid (Q3:B) |
| `RoutePacker` | Seating pack + outlier gap split |
| `PickupScheduleCalculator` | AM backward from StartTime; PM forward from DismissalTime |
| `AssignFitnessEvaluator` | Block seating / warn time-geo (Q2:B) |
| `RouteDeterminationService` | HomeToSchool + Transfer fleets (Q1:A) |

Apply Destination school-time migrations on **Windows SQL Server** (`dotnet ef database update`).
VM smoke: [specs/008-route-determination/quickstart.md](../../../specs/008-route-determination/quickstart.md).
