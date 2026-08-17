# Research: 007 Maps Platform Geo

## Decision: Retire Earth Engine from the app

**Rationale**: Live probes (2026-08-17) showed `ee-bigessfour` registered with Earth Engine, but the app never used it for addresses or trips. `GetGeoJsonAsync` called a non-existent `:exportGeoJson` (HTTP 404). `table:export` body used invalid fields (HTTP 400). Address/plot paths used regex + hash geocoding. EE is catalog/raster compute, not a geocoder or router ([Earth Engine](https://developers.google.com/earth-engine)).

**Alternatives considered**:
- Repair EE REST + Drive export — still would not validate USPS addresses or plan bus trips.
- Keep EE “for later satellite” while adding Maps — extra secrets and a dead client; rejected (YAGNI).
- Nominatim + OSRM — weaker rural rooftops and bulk ToS; extra vendor.

## Decision: Google Maps Platform on `new-coursera-490518`

**Rationale**: Same GCP billing console already used. Address Validation (optional USPS CASS) returns standardized components **and** lat/lng ([overview](https://developers.google.com/maps/documentation/address-validation/overview), [validation vs geocoding](https://developers.google.com/maps/architecture/geocoding-address-validation)). Routes API `computeRoutes` / matrix for drive paths ([compute routes](https://developers.google.com/maps/documentation/routes/compute-route-over)). Restrict API key to those APIs.

**Alternatives considered**:
- Geocoding API only — less clerk feedback on bad components; use Address Validation as primary.
- Mapbox / HERE — extra vendor; GCP already in play.
- Maps JavaScript / Navigation SDK — not WPF; constitution requires Syncfusion UI.

## Decision: Keep SfMap + OSM; drop unofficial Google tiles

**Rationale**: `GoogleEarthView` already defaults to OSM with attribution. `mt1.google.com/vt` violates Google Maps tile ToS. Map Tiles API is optional later, not MVP.

## Decision: Keep shapefile eligibility

**Rationale**: `ShapefileEligibilityService` is local, offline, already wired. Uploading district polygons to Maps/EE is out of scope.

## Decision: Production DI must not use OfflineGeocodingService

**Rationale**: Spec SC-002 forbids hash coordinates when mapping is unconfigured. Use a null-returning `UnconfiguredGeocodingService` or the Maps client that returns null without a key. Keep hasher for tests if useful.

## Decision: Delete EE code rather than `.disabled`

**Rationale**: Constitution prefers `.disabled` for experimental breakage. EE client is **incorrect REST**, unused, and a false “configured” path. Deleting `GoogleEarthEngineService`, EE bootstrap, and Drive export is the smaller long-term surface. Git history retains the files.

## Decision: No new EF migration in MVP

**Rationale**: Student already has lat/lon; Route has `WaypointsJson`. Cache can be in-memory + those columns. Optional `AddressValidatedAt` later.

## Resolved clarifications

None remaining from Technical Context.
