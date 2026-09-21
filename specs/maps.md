# Maps

## Purpose

A map is the clerk’s picture of the district: schools, catalog stops, geocoded student homes, and the published path of a selected route or trip. It exists so the office can plan pickup and drop-off times, show substitute drivers how a run starts and ends, and measure distance and time. It is not live dispatch.

BusBuddy-3 is a Syncfusion WPF .NET 9 desktop app on Windows. It is not hosted on AWS. Do not add Mapbox, ArcGIS Online, Amplify, or a browser map control to satisfy this spec.

## Invariants

- MUST use Syncfusion `SfMap` as the only map surface in the WPF client.
- MUST use official Google Map Tiles (`tile.googleapis.com` via `GoogleMapTilesImageryLayer` / `MapTileBootstrap`) as the **only** basemap. No OpenStreetMap, Mapbox, or unofficial `mt1.google.com` tiles.
- MUST fail closed on the basemap when `GOOGLE_MAPS_API_KEY` / Map Tiles session is unavailable (markers/polylines may still plot on an empty imagery layer; show Google attribution only when Google tiles are active). OSM is allowed only in `Tools/SfMapTileProbe`, never as a District Map fail-open.
- MUST treat maps as **four clerk operations** with **one writer per seam**. Do not add a second tile strategy, a second marker assigner, or a second polyline builder on the same `SfMap`. Do not merge Address Validation, Places, Routes, Map Tiles, and Route Optimization into one Google client.
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
- `MapView` / `MapView.xaml.cs` apply camera (`MapCameraHost`), assign markers (`MapMarkerHost`), and replay the polyline (`MapRouteTrailLayer`). They do not invent tiles, pins, or paths.
- `MapViewModel` holds center/zoom, selected route id, `MapMarkers`, `RouteLinePoints`, snapshot bytes, and status.
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

- First open: center ~38.0872, -102.6208, district-appropriate zoom, Google Map Tiles when the key/session works; otherwise empty basemap with a clear status (not OSM). Do **not** auto-select a route or draw a polyline until the clerk picks one.
- Load markers for schools and catalog stops that have coordinates. Skip incomplete addresses and list them as “needs validation.” Homes wait for a selected route (or an explicit student search).
- Selecting a route: fit (or center) on that path, draw polyline, show that run’s stops and eligible student homes.
- Refresh path after stop order changes. Stale polylines are a bug.
- Substitute driver sees the same published path and times as the home driver.
- Print/PDF may embed the current snapshot. Caption it as the published route, not “live.”
- Live-tracking controls, if still in XAML, stay disabled or clearly deferred. Do not wire a timer that fakes GPS.
- No “Active Buses” list on the District Map. Without live GPS it carries no clerk-usable information; the side card is the pin legend.

### District Map toolbar contract

| Button                 | Behavior                                                                                                                                                                                                                                                      |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Zoom In / Out          | Step zoom around the current center. Buttons use an explicit `ButtonAdv` template with literal brushes (no theme `DynamicResource`) and sit above the map (`Panel.ZIndex`, clipped container) so they never vanish after a zoom.                              |
| Home / Refresh         | **Home** recenters to the configured depot and keeps the selected route trail. **Refresh** reloads district base layers (schools + catalog stops + depots), re-draws the selected route (roster homes + drive path), and undoes filters such as Show Schools. |
| Show Schools           | **Schools-only** view: clears every non-school pin and the route polyline, plots schools, centers on them. Refresh restores everything.                                                                                                                       |
| Show Routes            | Refreshes the **selected** route (drive path when `GOOGLE_MAPS_API_KEY` is set). If nothing is selected, selects the first route with stored waypoints and refreshes it. Combo shows name, session, and route id.                                             |
| Plot Students          | Plots **assigned** riders on the selected route only (stored pickup/home GPS; no geocode). Disabled until a route is selected.                                                                                                                                |
| Center on Stops        | Span-fit plotted markers; if none, center on district depot (fleet GPS deferred).                                                                                                                                                                             |
| Plot Pickup Stops      | Plots catalog pickup stops **and** the published stops of every route. Toast when there is nothing to plot; never a silent no-op.                                                                                                                             |
| Move to selected route | Clerk override (spec 008): select a student/home pin, pick a route in the list, then move the rider onto that AM or PM row. Seating may be exceeded; reason is logged as `District Map`.                                                                      |
| Export Route           | Always enabled. No route selected → toast “select a route”; otherwise GeoJSON of the selected route via `IGeoDataService`. No hidden Settings gate.                                                                                                           |
| Print                  | Snapshot of the current view for the route PDF.                                                                                                                                                                                                               |

## Efficiency metrics

Distance and duration from Google Routes on the current published waypoint list are the measurable metrics for fuel and time talk. Store them on the route version. Do not compute “efficiency” from live pings that do not exist.

## Out of scope

- Live fleet GPS, driver phone breadcrumbs, parent “where is the bus.”
- Turn-by-turn in-cab navigation app (waypoints may later feed one; not this WPF surface).
- Editing geometry by dragging the polyline. Change the stop list, then refresh.
- A second basemap vendor as source of truth.
- OSM fail-open on any product `SfMap` host (District Map, school pick, stop pick, home pin).
- Merging Map Tiles, Address Validation, Places, Routes, and Route Optimization into one service.
- Treating Syncfusion’s old `GetUri` + `mt1.google.com/vt` sample as the product tile path. Official hook is `ImageryLayer.UrlTemplate` with Google `2dtiles`.
- Making leftover probes, satellite session UI, Print Map polish, route-optimization visit order, or UIA harness pieces “all green” as a maps fix.
- Committing student home coordinates to GitHub sample data.

## Clerk operations (the product)

Maps is four operations, not eighty files. Three hosts (District Map, school pick, stop/home pick) are allowed if they all call the same APIs. Do not merge the windows.

| #   | Clerk operation     | Allowed writers                                                                                                                                             | Official contract                                                                                                                                                                   |
| --- | ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Open District Map   | `MapTileBootstrap` + `GoogleMapTilesImageryLayer` + `GoogleMapTileSessionService`; camera via `MapCameraHost` + `DistrictMapAnchor` / `MapDefaults`         | Google `POST …/v1/createSession` then `GET …/v1/2dtiles/{z}/{x}/{y}?session=&key=`. SfMap `ImageryLayer.UrlTemplate`. Empty basemap if session/tile GET fails.                      |
| 2   | Plot places         | `MapMarkerHost` assigns `ImageryLayer.Markers`. Overlay built into `MapViewModel.MapMarkers` via `MapDistrictLayers` + `MapMarker` / `MapMarkerLabels.Kind` | Validated lat/lng only (`LocationCoordinate`). Schools = `IDestinationService`, stops = `IPickupStopService`, homes only for selected route / search. One `MarkerTemplateSelector`. |
| 3   | Draw published path | `RouteDrivePathRefresher` → `MapRouteTrail` → `RouteLinePoints` → `MapRouteTrailLayer`                                                                      | Google Routes on published stop order. No freehand, no Haversine-as-truth. Schedule clocks consume Routes duration (`PublishedStopClockPlanner`).                                   |
| 4   | Validate an address | `PlacesAddressBox` → `PlacesAddressAutocompleteCoordinator` / `PlaceAddressApplier` → `MapsGeoService`                                                      | Places session token; Address Validation + Geocoding write coords on the entity. Map only reads them. No pin until validated. Never 0,0 or US centroid.                             |

`MapViewModel` holds center/zoom, selected route id, marker collection, `RouteLinePoints`, snapshot bytes, and status. Load commands belong in those collaborators, not another 60 KB of VM.

Keep Google clients split (they match Google’s product split): `GoogleMapTileSessionService`, `MapsGeoService` / Address Validation, `GooglePlacesAutocompleteService`, `GoogleRoutingService` + `RouteDrivePathRefresher`, and data services (`IDestinationService`, `IPickupStopService`, `IGeoDataService`).

### Starve / do not extend

| Keep as the one writer                   | Starve / fold in                                                                                                                                                                                                                                                           |
| ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MapTileBootstrap`                       | Extra tile probes in the WPF process, `GetUri` scraper URLs, duplicate OSM switch. Archived (do not restore): `MapInteractionDiagnostics`, `EligibilityRoutePdfBuilder`, OSM probe, `map-tile-probe`, diag smoke → `Documentation/Archive/2026-09-Maps-Competing-Writers/` |
| `MapMarkerHost` + `MapMarkerLabels.Kind` | Coordinators assigning `ImageryLayer.Markers` themselves; geocode-on-plot (`BulkPlotStudentsAsync` network path, `StudentsMapCoordinator` geocode fallback)                                                                                                                |
| `MapRouteTrail` / `MapRouteTrailLayer`   | A second polyline builder inside `MapViewModel`                                                                                                                                                                                                                            |
| `PlacesAddressBox` + one coordinator     | Parallel “apply address” helpers that drop pins                                                                                                                                                                                                                            |
| `MapView.xaml.cs` camera + snapshot only | Business logic in code-behind                                                                                                                                                                                                                                              |

`MappingService.cs` is AutoMapper — not this spec. `IRouteOptimizationService` is not a District Map writer.

## Vendor contract

Living comparison against official docs only. Guest pass/fail is filled from `MapsConnectionProbe` and `Tools/SfMapTileProbe` (`google-urltemplate`) plus one District Map log line `MapTileBootstrap Host=DistrictMap Outcome=ok|no-key|session-null|error`.

Sources:

- Google Map Tiles: [session tokens](https://developers.google.com/maps/documentation/tile/session_tokens), [2D tiles](https://developers.google.com/maps/documentation/tile/2d-tiles-overview)
- Syncfusion WPF: [map providers](https://help.syncfusion.com/wpf/maps/map-providers) — custom XYZ is `ImageryLayer.UrlTemplate` with `{z}/{x}/{y}` (Azure Maps is the documented example of that hook). Quote: _When UrlTemplate is set, the ImageryLayer gives first preference to loading map tiles from the specified URL and ignores LayerType and BingMapKey._ Markers in [markers](https://help.syncfusion.com/wpf/maps/markers) are hemisphere **strings** (`38.8833N`). OSM is `LayerType="OSM"` — product must not use it. Do not follow the KB that overrides `GetUri` with `mt1.google.com/vt`.

| #   | Failure                     | Our call site                                                            | Syncfusion WPF                                                     | Google                                                                                | Guest                                                                        |
| --- | --------------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------ | ------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| 1   | Create tile session         | `GoogleMapTileSessionService`                                            | n/a                                                                | POST `createSession?key=` body `mapType`, `language`, `region`. Token expiry ~2 weeks | Prove with `MapsConnectionProbe`                                             |
| 2   | Tile URL                    | `MapBasemap.TileUrlTemplate`                                             | `UrlTemplate` `{z}/{x}/{y}`                                        | `tile.googleapis.com/v1/2dtiles/{z}/{x}/{y}?session=&key=`. No `mt1.google.com`       | Probe one Wiley tile GET                                                     |
| 3   | SfMap bind                  | `GoogleMapTilesImageryLayer.UseGoogleTiles`                              | `UrlTemplate` wins over `LayerType`. Cache clear on session rotate | Session URL on every tile GET                                                         | `SfMapTileProbe google-urltemplate` paints; `DeleteTilesFromCache` on rotate |
| 4   | OSM fail-open               | `ClearBasemap`                                                           | `LayerType=OSM` is a full provider                                 | Google content must not sit on a non-Google map                                       | Product empty, not OSM                                                       |
| 5   | Markers                     | `MapMarkerHost` + `MapMarker`                                            | `ImageryLayer.Markers` + `MarkerTemplate`; lat/lng strings         | Google does not draw pins                                                             | Count pins vs DB rows with coords                                            |
| 6   | Polyline                    | `MapRouteTrailLayer` + XAML `MapPolyline`                                | `SubShapeFileLayers` / `MapElements`                               | Encoded polyline from Routes, stop order                                              | Bus 5 AM: one gold line                                                      |
| 7   | Places + Address Validation | `PlacesAddressBox` → `MapsGeoService`                                    | n/a                                                                | Places session token; validated coords on the entity                                  | Bad address = no pin                                                         |
| 8   | Routes duration             | `RouteDrivePathRefresher.ApplyPathMetrics` → `PublishedStopClockPlanner` | n/a                                                                | Duration/distance from `computeRoutes`                                                | Schedule span = path duration, not `+1 min`/stop                             |
| 9   | Snapshot                    | `MapSnapshotEncoder`                                                     | n/a                                                                | n/a                                                                                   | `RenderTargetBitmap` of live control; no `Measure(Infinity)`                 |
| 10  | Policy                      | `CanCacheTiles`; no disk tile dump                                       | In-control cache                                                   | Map Tiles ToS: attribution; no scrape                                                 | No `mt1`; clear cache on session rotate                                      |

If a row cannot be checked against those two official sources, it is out of scope.

## Code anchors

| Spec term                | Existing code                                                                                 |
| ------------------------ | --------------------------------------------------------------------------------------------- |
| View (camera + snapshot) | `MapView.xaml`, `MapView.xaml.cs`                                                             |
| View state               | `MapViewModel` (center/zoom, route id, `MapMarkers`, `RouteLinePoints`, snapshot, status)     |
| Tiles                    | `MapTileBootstrap`, `GoogleMapTilesImageryLayer`, `GoogleMapTileSessionService`, `MapBasemap` |
| Camera                   | `MapCameraHost`, `DistrictMapAnchor`, `MapDefaults`, `DistrictDepot`                          |
| Host subclass            | `DistrictSfMap` (mouse NRE swallow — not a second tile strategy)                              |
| Pin kinds / colors       | `MapMarkerLabels` (`Kind`, `FillHex`, `Legend`)                                               |
| Pin state                | `MapMarker` (`Latitude`/`Longitude` strings, `DisplayCaption`)                                |
| Pin assign               | `MapMarkerHost` (only writer of `ImageryLayer.Markers`)                                       |
| Overlay builder          | `MapDistrictLayers` (writes `MapMarkers` collection, not SfMap)                               |
| Path                     | `IRoutingService`, `RouteDrivePathRefresher`, `MapRouteTrail`, `MapRouteTrailLayer`           |
| Clocks                   | `PublishedStopClockPlanner` (consumes Routes duration)                                        |
| Address                  | `PlacesAddressBox`, `IMapsGeoService`, `GooglePlacesAutocompleteService`                      |
| Geo data                 | `IDestinationService`, `IPickupStopService`, `IGeoDataService`                                |
| Snapshot                 | `MapSnapshotEncoder`                                                                          |
| Harness (not product)    | `MapsConnectionProbe`, `Tools/SfMapTileProbe`                                                 |
| Object mapping           | `MappingService` (AutoMapper — **not** this spec)                                             |

## Worked examples

- Clerk opens District Map before any selection: Wiley/Lamar area, schools and catalog stops visible, no bus moving.
- Select special-needs AM Route 5: pins on validated homes + school, polyline from Routes API in stop order, label “#5”.
- New student address fails validation: form stays incomplete, no pin, route cannot add that stop yet.
- Google tiles fail: basemap stays empty; markers and polyline still plot when coordinates exist. Geocoding/Routes still use Google APIs when the key is present.

## Agent instructions

When changing map code, read this file plus `specs/locations.md`, `specs/routes.md`, `specs/students.md`, and `specs/trips.md`. Quote the clerk operation (1–4) and the invariant you implemented.

- If a file is not an allowed writer for one of the four operations, do not edit it to “fix the map.”
- Never implement maps by editing `MappingService.cs`.
- Never enable live tracking to “finish” the district map.
- Never add OSM, `mt1.google.com`, Bing as a real basemap, or a second `ImageryLayer` on the same `SfMap`.
- Never merge Google Maps clients. Never geocode inside a plot path.
- Guest proof for tiles is `MapsConnectionProbe` + `SfMapTileProbe google-urltemplate` + `MapTileBootstrap Host=DistrictMap Outcome=…`. Stop reading blogs when those pass.
- Do not restore files from `Documentation/Archive/2026-09-Maps-Competing-Writers/` into `BusBuddy.WPF` or `Scripts/`. Those are competing tile/OSM/diagnostics writers, not the contract.
