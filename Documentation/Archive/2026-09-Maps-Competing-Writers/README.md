# Archived competing Maps writers (2026-09)

These files are **not** part of the District Map contract. They competed with `specs/maps.md` (four clerk operations, one writer per seam) and made Google Map Tiles ambiguous.

Constitution § IV: git history is the official archive. This folder is a human-readable shelf so agents do not restore them into `BusBuddy.WPF` or `Scripts/` as product code. Do **not** rename to `*.disabled`.

## Contract writers (stay in the product)

Tiles: `MapTileBootstrap`, `GoogleMapTilesImageryLayer`, `GoogleMapTileSessionService`, `MapBasemap`
Pins: `MapMarkerHost`, `MapDistrictLayers`, `MapMarker`, `MapMarkerLabels`
Path: `RouteDrivePathRefresher`, `MapRouteTrail`, `MapRouteTrailLayer`
Address: `PlacesAddressBox`, `MapsGeoService` / Places
Harness (not product): `MapsConnectionProbe`, `Tools/SfMapTileProbe`

## Moved here

| File                               | Why it competed                                                                                                                                                 |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MapInteractionDiagnostics.cs`     | Second observer inside the WPF process (tile breadcrumbs, binding traces, dispatcher hooks). Product tile proof is `MapTileBootstrap` logs + the two harnesses. |
| `EligibilityRoutePdfBuilder.cs`    | Second PDF writer on District Map / MainWindow. Print is the live snapshot (`PrintRouteMapsCommand`), not a student-map PDF.                                    |
| `scripts/map-sfmaps-osm-probe.ps1` | OSM window. Product is OSM fail-closed. Probe OSM stays only in `Tools/SfMapTileProbe`.                                                                         |
| `scripts/map-tile-probe.ps1`       | Duplicate tile GET vs `MapsConnectionProbe`.                                                                                                                    |
| `scripts/score-map-logs.ps1`       | Scored the diagnostics log, not official Google/Syncfusion tile proof.                                                                                          |
| `scripts/run-map-diag-smoke.cmd`   | Forced `BUSBUDDY_MAP_DIAGNOSTICS=1` and launched a competing smoke path.                                                                                        |

## Tests evaluated and removed from `BusBuddy.Tests`

Copied to `tests/MapInteractionDiagnosticsTests.archived.cs` and `tests/EligibilityRoutePdfTests.archived.cs` so they are not compiled:

- `MapInteractionDiagnostics_IsGatedByConfigWithEnvOverride`
- `MapInteractionDiagnostics_BreadcrumbsNameKeysNotCharacters`
- `GenerateEligibilityRoutePdfAsync_*`

`MapViewTests.RetiredLeafletWebViewAndMapWinGis_AreGone` asserts those classes are absent from WPF.

Keep (not District Map writers): `RouteStopOrderPlanner` / `IRouteOptimizationService` (clerk Optimize Order), `MappingService` (AutoMapper). `CenterOnFleetCommand` is the “Center on Stops” Home-style button, not live GPS.

## Do not restore

If you need tile proof: `./Scripts/utm-dev-bridge.sh` → `MapsConnectionProbe` + `Tools/SfMapTileProbe google-urltemplate` + `MapTileBootstrap Host=DistrictMap Outcome=…`.
