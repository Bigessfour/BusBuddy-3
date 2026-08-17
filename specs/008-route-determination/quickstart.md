# Quickstart: 008 Route Determination

Validation guide after implementation (not runnable until `/speckit-tasks` + implement).

## Prerequisites

- PR #36 merged (schools, transfers, student geo) and migrations applied on Windows SQL Server or Postgres with fixed seed types
- At least one School `Destination` with GPS + **StartTime** / **DismissalTime**
- ≥12 students with home lat/lon assigned to that school; one bus with `SeatingCapacity` ≥ 12
- Optional: `GOOGLE_MAPS_API_KEY` for tighter ETAs (fail-open without it)

## Core unit checks (Mac OK)

```bash
dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj -c Release -p:EnableWindowsTargeting=true \
  --filter "FullyQualifiedName~RouteDetermination&Category!=Integration&Category!=InMemoryFlaky"
```

Expect: clustering packs nearby 12 into one route; distant gap splits; seating block; time backward schedule monotonic.

## VM UI smoke

1. Set school start/dismissal on map school editor.
2. Run **Generate routes** (year-start) for that school — expect draft AM + mirrored PM; map shows clusters.
3. Override one outlier on the map — assignment moves; stop remains on mirror for AM-only student.
4. Assign a student that exceeds seating — **blocked** toast; assign with time risk only — **warn** and proceeds.
5. Create two transfers — **Generate transfer routes** produces a separate pool (no seat theft from home routes).

## Serilog proof

Look for:

- `Route generation completed`
- `Assign fitness Blocked` / `Assign fitness Warned`

## Related

- [spec.md](./spec.md) · [data-model.md](./data-model.md) · [contracts/route-determination.md](./contracts/route-determination.md)
