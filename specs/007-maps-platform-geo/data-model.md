# Data Model: 007 Maps Platform Geo

MVP persists on existing student and route columns. No required migration.

## ValidatedAddress (transient + mapped onto Student)

| Field             | Type     | Rules                                            |
| ----------------- | -------- | ------------------------------------------------ |
| Street            | string   | Required for validate                            |
| City              | string   | Optional input; filled from provider             |
| State             | string   | US 2-letter when present                         |
| PostalCode        | string   | 5 or 5+4                                         |
| FormattedAddress  | string   | Provider standardized line                       |
| IsDeliverable     | bool     | From verdict / CASS                              |
| Precision         | enum     | Rooftop, RangeInterpolated, Approximate, Unknown |
| Latitude          | double?  | Null if not geocoded                             |
| Longitude         | double?  | Null if not geocoded                             |
| ProviderRequestId | string?  | Correlation; log only                            |
| RetrievedAtUtc    | DateTime | Cache key with normalized input                  |

**Validation**: Reject save when mapping configured, skip-flag off, and `IsDeliverable` is false.

**Mapping to Student**: existing `HomeAddress`, city/state/zip fields, `Latitude`, `Longitude` (confirm exact property names at implement).

## RoutePath (mapped onto Route.WaypointsJson)

| Field           | Type     | Rules                       |
| --------------- | -------- | --------------------------- |
| RouteId         | int      | Existing PK                 |
| OrderedStopIds  | int[]    | RouteStops by StopOrder     |
| EncodedPolyline | string   | Routes API encoded polyline |
| DistanceMeters  | int      |                             |
| DurationSeconds | int      |                             |
| ComputedAtUtc   | DateTime |                             |

**State**: `None` → `Computed` → `Stale` (if stops change). Stale triggers recompute on dispatcher refresh, not automatically on every student assign (FR-009 fail-open).

## MappingConfiguration (env / IOptions)

| Field          | Source                |
| -------------- | --------------------- |
| ApiKey         | `GOOGLE_MAPS_API_KEY` |
| QuotaProject   | `new-coursera-490518` |
| EnableUspsCass | true (US)             |
| RegionCode     | US                    |

Never stored in git. Absence ⇒ Unconfigured (null geocode, UI message).

## Relationships

```text
Student 1──1 ValidatedAddress (logical)
Route 1──* RouteStop
Route 1──0..1 RoutePath (WaypointsJson)
```
