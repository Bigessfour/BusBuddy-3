# BusBuddy-3 domain specs

Canonical meaning and invariants for the WPF clerk app. These are **not** UI mockups. They win over guesswork and leftover UI behavior.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS.

## When to read which spec

| If you are changing…                                         | Read                         |
| ------------------------------------------------------------ | ---------------------------- |
| Student forms, pickup mode, AM/PM, special needs, rosters    | [students.md](students.md)   |
| Schools, stops, homes, depots, geocoding, place types        | [locations.md](locations.md) |
| Daily published runs, stop order, rider exceptions, versions | [routes.md](routes.md)       |
| One-off team/club/field trips, manifests, trip status        | [trips.md](trips.md)         |
| CDL, training, home route vs substitute vs trip loan         | [drivers.md](drivers.md)     |
| Bus number, capacity, inspections, fuel, maintenance         | [buses.md](buses.md)         |
| District Map, tiles, polylines, pins, snapshots              | [maps.md](maps.md)           |

Read **related** specs too (each file’s “Agent instructions” section lists them).

## Domain split (do not collapse)

```text
Students (who rides)
    → Locations (validated places)
        → Routes (daily published)     Trips (one date, manifest)
            → Buses + Drivers (default pairing, session exceptions)
                → Maps (display only — not live GPS)
```

## Hard rules (all agents)

- **Route ≠ Trip.** Never add `IsTrip` to `Route`.
- **PickupMode** is `Home` or `CatalogStop`. Special needs ⇒ home pickup on a special-needs route.
- Same-day **“not riding”** is a **rider exception**, not a route rewrite.
- **`MappingService.cs`** is AutoMapper (object mapping), not geospatial maps.
- **Maps:** Syncfusion `SfMap` + official Google Map Tiles (OSM fail-open). Geocode/validate with Google Address Validation; paths with Google Routes.
- **Default clerk map center:** Lamar/Wiley CO (~38.0872, -102.6208). Not `MapDefaults` US centroid as the home view.
- **Live fleet GPS is deferred.** Do not enable `IsLiveTrackingEnabled` to “finish” the map.
- Plot only **validated** lat/lng. No 0,0 pins, no US centroid guesses.
- **Never commit student PII** (names, addresses, rosters). Load through clerk forms; keep paper/Excel rosters local.

## Code-first anchors

Extend existing Core types and services — do not invent parallel models:

| Domain                    | Start here                                                                                    |
| ------------------------- | --------------------------------------------------------------------------------------------- |
| Students                  | `IStudentService`, Core `Student`                                                             |
| Schools / destinations    | `IDestinationService`                                                                         |
| Catalog stops             | `IPickupStopService`                                                                          |
| Routes / waypoints        | `BusBuddy.Core.Models.Route`, `IGeoDataService`, `IRoutingService`, `RouteDrivePathRefresher` |
| Trips (not routes)        | `BusBuddy.Core.Models.Trips.TripEvent`, `ITripEventService`                                   |
| Geo / map VM              | `IMapsGeoService`, `MapViewModel`, `MapView`                                                  |
| Buses                     | `IBusService`, `BusBuddy.Core.Models.Bus`                                                     |
| Depot                     | `DistrictDepot`                                                                               |
| Object mapping (not maps) | `MappingService`                                                                              |

## If code conflicts with a spec

Stop. Quote the invariant from the relevant `specs/<concept>.md` and the conflicting code. Propose a reconciliation. Do not implement a hybrid model.

## Numbered feature specs

Spec-Kit feature folders (`specs/00x-*`) cover cross-cutting implementation plans (maps platform, route determination, hybrid dev, etc.). The domain files above (`students.md`, `locations.md`, …) are the **domain contract** for day-to-day clerk features.
