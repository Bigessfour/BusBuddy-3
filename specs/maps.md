# Maps

## Purpose

A map is the clerk’s picture of the district: schools, catalog stops, geocoded student homes, and the published path of a selected route or trip. It exists so the office can plan pickup and drop-off times, show substitute drivers how a run starts and ends, and measure distance and time. It is not live dispatch.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not add Mapbox, ArcGIS Online, Amplify, or a browser map control to satisfy this spec.

## Invariants

- MUST use Syncfusion `SfMap` as the only map surface in the WPF client.
- MUST use official Google Map Tiles (`tile.googleapis.com` via `GoogleMapTilesImageryLayer` / `MapTileBootstrap`) as the **only** basemap. No OpenStreetMap, Mapbox, or unofficial `mt1.google.com` tiles.
- MUST fail closed on the basemap when `GOOGLE_MAPS_API_KEY` / Map Tiles session is unavailable (markers/polylines may still plot on an empty imagery layer; show Google attribution only when Google tiles are active).
- MUST geocode and validate with Google Address Validation + Geocoding. MUST build drive paths with Google Routes. MUST use Google Route Optimization (`optimizeTours`) only as a clerk-initiated visit-order / fleet suggestion — never a nightly rewrite of published times.
- MUST plot only validated coordinates. No pin at 0,0, no US centroid, no guessed “close enough” point.
- MUST default the clerk map center to Lamar/Wiley CO (~38.0872, -102.6208) when nothing is selected. Do not use `MapDefaults` US-centroid fallback as the district home view.
- MUST derive route polylines from published route stops (waypoints), refreshed by `RouteDrivePathRefresher` / `IRoutingService`. Freehand lines are not source of truth.
- MUST keep live fleet GPS off. `IsLiveTrackingEnabled` stays deferred. Bus numbers may appear as labels on a route, not as moving pings.
- MUST NOT treat `MappingService.cs` as a map service. That type is AutoMapper (object mapping).
- MUST NOT invent a second geo stack. Extend `IMapsGeoService`, `IGeoDataService`, `IGeocodingService`, `IRoutingService`, `MapViewModel`.
- Default: district overlay of schools + catalog stops; selecting a route adds homes on that run and the path.
- Exception: trip focus plots origin/destination/path for that outing without destroying the idea of the published daily map.

## What the map shows

| Layer                        | When                                       | Source                                               |
| ---------------------------- | ------------------------------------------ | ---------------------------------------------------- |
| Basemap tiles                | Always                                     | Google Map Tiles only (empty if session unavailable) |
| Schools                      | Always (active)                            | `IDestinationService`                                |
| Catalog pickup stops         | Always (active)                            | `IPickupStopService`                                 |
| Student homes                | Selected route / search / eligibility work | Student + geocoder                                   |
| Depot / fuel / shop          | Operational toggle                         | `DistrictDepot` / location types                     |
| Route polyline               | Selected route                             | Waypoints → Routes API → `RouteLinePoints`           |
| Trip origin/destination/path | Selected trip                              | Trip locations → Routes API                          |
| Bus number label             | Selected route                             | Default or session bus — **not** live XY             |

Parent notification of “where the student is picked up” uses the published stop and time from `specs/routes.md`, optionally illustrated by this map. It does not use a moving vehicle.

### Pin colors and captions

Every pin kind has one fixed color so the clerk can read a mixed overlay at a glance. The legend card on the District Map is generated from `MapMarkerLabels.Legend`; do not hand-paint a second legend.

| Kind         | Fill             | Caption visible from                  |
| ------------ | ---------------- | ------------------------------------- |
| School       | Black `#000000`  | zoom ≥ `DetailLabelZoomLevel` (12)    |
| Pickup stop  | Orange `#F28C28` | zoom ≥ 12                             |
| Route stop   | Gold `#D4A017`   | zoom ≥ 12 (`Stop n` sequence caption) |
| Depot / barn | Purple `#7B3FE4` | zoom ≥ 12                             |
| Student home | Blue `#5B8DEF`   | zoom ≥ `HomeLabelZoomLevel` (14)      |
| Student      | Green `#2E9E5B`  | zoom ≥ 14                             |

Source of truth is the `*FillHex` constants in `MapMarkerLabels`; this table mirrors them.

- Captions are rendered by the Syncfusion `ImageryLayer.MarkerTemplate` (`MapMarker.DisplayCaption`), not by Google. Google only draws tiles; caption visibility, color, and overlap are BusBuddy's responsibility.
- When a published route stop lands on an existing pin (school, home, catalog stop), the existing pin is **tagged** (`RouteStopLabel`, gold stroke, caption `Name (Stop n)`) instead of stacking a second pin and a second caption at the same coordinate.
- Household captions wait for zoom 14 so a town-level view does not become a wall of names.

## Relationships

- Map view **displays** locations, routes, and trips. It does not own them.
- `MapView` / `MapView.xaml.cs` apply camera, markers, and polyline.
- `MapViewModel` loads routes, requests refresh, holds `RouteLinePoints`, zoom/center, snapshot bytes for PDF.
- Geo services answer “where is this address” and “what path connects these waypoints.”

## Data the app must store (map-related)

The map does not need its own table of pins. Persist facts on Location and Route:

| Fact                              | Lives on                            | Notes                                        |
| --------------------------------- | ----------------------------------- | -------------------------------------------- |
| Address + lat/lng                 | Location                            | After validation                             |
| Waypoint order + target times     | Route stops                         |                                              |
| Encoded path or distance/duration | Route version                       | From Routes API refresh                      |
| Last clerk center/zoom            | session UI only                     | Do not fight the Wiley default on first load |
| Map snapshot PNG                  | `MapViewModel.LatestMapSnapshotPng` | For route PDF; not a source of coordinates   |

## Behaviors / UI

- First open: center ~38.0872, -102.6208, district-appropriate zoom, Google Map Tiles when the key/session works; otherwise empty basemap with a clear status (not OSM).
- Load markers for schools and catalog stops that have coordinates. Skip incomplete addresses and list them as “needs validation.”
- Selecting a route: fit (or center) on that path, draw polyline, show that run’s stops and eligible student homes.
- Refresh path after stop order changes. Stale polylines are a bug.
- Substitute driver sees the same published path and times as the home driver.
- Print/PDF may embed the current snapshot. Caption it as the published route, not “live.”
- Live-tracking controls, if still in XAML, stay disabled or clearly deferred. Do not wire a timer that fakes GPS.
- No “Active Buses” list on the District Map. Without live GPS it carries no clerk-usable information; the side card is the pin legend.

### District Map toolbar contract

| Button                 | Behavior                                                                                                                                                                                                                         |
| ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Zoom In / Out          | Step zoom around the current center. Buttons use an explicit `ButtonAdv` template with literal brushes (no theme `DynamicResource`) and sit above the map (`Panel.ZIndex`, clipped container) so they never vanish after a zoom. |
| Home / Refresh         | Recenter to the configured depot; Refresh reloads the **full** district overlay (schools + catalog stops + depots + route homes) and undoes any filter.                                                                          |
| Show Schools           | **Schools-only** view: clears every non-school pin, plots schools, centers on them. Refresh restores everything.                                                                                                                 |
| Plot Pickup Stops      | Plots catalog pickup stops **and** the published stops of every route. Toast when there is nothing to plot; never a silent no-op.                                                                                                |
| Move to selected route | Clerk override (spec 008): select a student/home pin, pick a route in the list, then move the rider onto that AM or PM row. Seating may be exceeded; reason is logged as `District Map`.                                         |
| Export Route           | Always enabled. No route selected → toast “select a route”; otherwise GeoJSON of the selected route via `IGeoDataService`. No hidden Settings gate.                                                                              |
| Print                  | Snapshot of the current view for the route PDF.                                                                                                                                                                                  |

## Efficiency metrics

Distance and duration from Google Routes on the current published waypoint list are the measurable metrics for fuel and time talk. Store them on the route version. Do not compute “efficiency” from live pings that do not exist.

## Out of scope

- Live fleet GPS, driver phone breadcrumbs, parent “where is the bus.” Do not hook a timer on `FleetMonitoringService` to simulate pings.
- Turn-by-turn in-cab navigation app (waypoints may later feed one; not this WPF surface).
- Editing geometry by dragging the polyline. Change the stop list, then refresh.
- A second basemap vendor as source of truth.
- Committing student home coordinates to GitHub sample data.

## Code anchors

| Spec term              | Existing code                                       |
| ---------------------- | --------------------------------------------------- |
| View                   | `MapView.xaml`, `MapView.xaml.cs`                   |
| View model             | `MapViewModel`                                      |
| Pin kinds / colors     | `MapMarkerLabels` (`Kind`, `FillHex`, `Legend`)     |
| Pin state              | `MapMarker` (`DisplayCaption`, `FillBrush`)         |
| District layers        | `MapDistrictLayers` (schools, pickups, route stops) |
| GeoJSON export         | `MapRouteExporter`                                  |
| Tiles                  | `GoogleMapTilesImageryLayer`, `MapTileBootstrap`    |
| Geo facade             | `IMapsGeoService`, `IGeoDataService`                |
| Address                | `IGeocodingService`, Google Address Validation      |
| Path                   | `IRoutingService`, `RouteDrivePathRefresher`        |
| Visit order            | `IRouteOptimizationService` (`optimizeTours`)       |
| Depot                  | `DistrictDepot`                                     |
| Object mapping         | `MappingService` (AutoMapper — **not** this spec)   |
| Feature branch context | `feature/map-route-display` / PR #59                |

## Worked examples

- Clerk opens District Map before any selection: Wiley/Lamar area, schools and catalog stops visible, no bus moving.
- Select special-needs AM Route 5: pins on validated homes + school, polyline from Routes API in stop order, label “#5”.
- New student address fails validation: form stays incomplete, no pin, route cannot add that stop yet.
- Google tiles fail: basemap stays empty; markers and polyline still plot when coordinates exist. Geocoding/Routes still use Google APIs when the key is present.

## Agent instructions

When changing map code, read this file plus `specs/locations.md`, `specs/routes.md`, `specs/students.md`, and `specs/trips.md`. Quote the invariant you implemented. Never implement maps by editing `MappingService.cs`. Never enable live tracking to “finish” the district map.
