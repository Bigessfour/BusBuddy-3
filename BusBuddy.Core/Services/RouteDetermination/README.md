# Route Determination

Year-start fleet sizing and student assignment planner for BusBuddy.

**Contracts**: `specs/008-route-determination/contracts/`

| Type | Role |
|------|------|
| `IRouteDeterminationService` | Generate/assign, assign fitness, clerk override |
| `DensityCellBuilder` | Bbox / density N-cell grid (Q3:B) |
| `RoutePacker` | Seating pack + outlier gap split |
| `RouteDeterminationService` | Orchestrates HomeToSchool (+ later Transfer) |

Apply Destination school-time migrations on **Windows SQL Server** (`dotnet ef database update`).
