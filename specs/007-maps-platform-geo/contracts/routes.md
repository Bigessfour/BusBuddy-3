# Contract: Routes (drive path)

**Provider**: Google Routes API
**Docs**: https://developers.google.com/maps/documentation/routes/compute-route-over
**Auth**: `X-Goog-Api-Key`, field mask required

## BusBuddy interface (new)

```text
IRoutingService
  ComputeDrivePathAsync(origin, destination, waypoints[]) →
    { Polyline, DistanceMeters, Duration, Error? }
```

- `origin` / `destination`: Wiley School default (existing WileyMapDefaults) unless route specifies otherwise.
- `waypoints`: RouteStops with lat/lon, StopOrder.
- `travelMode`: DRIVE
- `routingPreference`: TRAFFIC_UNAWARE (rural AM/PM planning; cheaper/stabler)

## HTTP (implementer)

`POST https://routes.googleapis.com/directions/v2:computeRoutes`

Headers:

- `X-Goog-FieldMask`: `routes.duration,routes.distanceMeters,routes.polyline.encodedPolyline`

Optional later: `computeRouteMatrix` for ranking students vs routes — same interface file, not MVP-blocking.

## Persistence

Write encoded polyline + decoded point list into `Route.WaypointsJson` using existing `RouteWaypointSerializer` if compatible; otherwise extend serializer in Core/Mapping (same assembly).

## Error contract

| Condition           | App behavior                                                                   |
| ------------------- | ------------------------------------------------------------------------------ |
| No key / 403        | Skip path; keep stop order; Warning log (FR-009)                               |
| Fewer than 2 points | No call; Information log                                                       |
| Empty polyline      | Treat as failure; do not wipe existing waypoints unless clerk confirms refresh |

## Logging

`Drive path computed RouteId={RouteId} Stops={StopCount} DistanceMeters={M} ElapsedMs={ElapsedMs}`
