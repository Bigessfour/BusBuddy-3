# Feature Specification: Maps Platform Geo (retire Earth Engine)

**Feature Branch**: `feature/007-maps-platform-geo`

**Created**: 2026-08-17

**Status**: Paused (Earth Engine removed; Maps clients not wired)

**Input**: Earth Engine is the wrong product for student addresses, map plots, and trip planning. Replace it with Google Maps Platform Address Validation + Routes, keeping Syncfusion `SfMap`. Students entered in the system are eligible (no geofence).

## Baseline (as of draft)

| Area                  | Current                                                                         | Target                                                                          |
| --------------------- | ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| Address correctness   | Regex / format checks; optional skip                                            | Postal-grade US validation with standardized components and a clear fail reason |
| Map coordinates       | Hash scatter (`OfflineGeocodingService`, tests only)                            | Real coordinates from Address Validation, cached on the student                 |
| Trip / route geometry | Capacity fill + stored stop points; no road graph                               | Drive paths (distance, time, polyline) for school ↔ stops                       |
| Satellite / EE        | `GoogleEarthEngineService`, `GcpCredentialBootstrap`, invented `:exportGeoJson` | **Removed** from DI, config, secrets, and probes                                |
| Map UI                | Syncfusion `SfMap` + OSM only (unofficial Google tiles removed)                 | Keep `SfMap` + OSM; Maps Tiles API optional later                               |
| District eligibility  | Local shapefiles (wrong district)                                               | Students in the system are eligible — no geofence                               |
| GCP                   | `ee-bigessfour` EE project + broken SA JWT                                      | Billing project `new-coursera-490518` + Maps API key in Passwords               |

Constitution: this feature **amends** the Geo constraint (Earth Engine → Maps Platform Address Validation + SfMap; no shapefile geofence).

Nominated provider (working solution): **Google Maps Platform** on `new-coursera-490518` — Address Validation (USPS CASS), Routes (`computeRoutes` / `computeRouteMatrix`). Places Autocomplete is deferred (P3). Earth Engine stays unused; do not re-enable it in this feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Clerk saves a real student address and sees it on the map (Priority: P1)

As a transportation clerk, when I enter a Wiley-area student street address, the app tells me whether it is a real deliverable address, stores the standardized form, and plots the home on the district map — not a random point near the school.

**Why this priority**: Fake coordinates and regex-only checks are the current failure. This is the minimum replacement for Earth Engine on the student path.

**Independent Test**: With a mapping key present, saving a known Wiley street address yields a success status, persisted latitude/longitude, and a map marker at that location. With the key absent, the clerk sees a clear “mapping unavailable” message and the app does not invent coordinates. Unit tests cover valid, invalid, and no-key paths without calling the live network.

**Acceptance Scenarios**:

1. **Given** a mapping key and a complete US address in the Wiley service area, **When** the clerk validates or saves the student, **Then** the address is accepted or corrected to a standardized form and latitude/longitude are stored.
2. **Given** a mapping key and a nonsense or incomplete address, **When** the clerk validates, **Then** save is blocked (unless the existing skip-validation flag is on) and the clerk sees why it failed.
3. **Given** no mapping key, **When** the clerk plots or validates, **Then** the UI states mapping is not configured and does **not** hash the address into fake coordinates.
4. **Given** a previously validated student with coordinates, **When** the map bulk-plots eligible students, **Then** markers use stored coordinates and do not re-call the network for every row.

---

### User Story 2 - Earth Engine is gone from the running product (Priority: P1)

As a maintainer, I can start the app and run CI without Earth Engine credentials, service-account JSON, Drive export, or mock Kansas GeoJSON. The map still loads routes from the database and OSM tiles.

**Why this priority**: Leaving a broken EE client registered as “configured” is a false positive and a secret/ops burden.

**Independent Test**: Solution builds with Windows targeting; DI does not register an Earth Engine client; appsettings have no `GoogleEarthEngine` section; Passwords/env no longer require `GEE_*`; connection probe (if any) targets the street mapping provider. `GetGeoJsonAsync` / table-export-to-Drive code is absent.

**Acceptance Scenarios**:

1. **Given** a clean checkout with no `GEE_*` env, **When** the WPF app starts, **Then** it does not materialize a GCP service-account key and does not log Earth Engine token kinds.
2. **Given** the map view, **When** routes load, **Then** waypoints come from the database (or stop-derived points), not an Earth Engine asset export.
3. **Given** docs and constitution, **When** an agent follows `AGENTS.md`, **Then** geo instructions name the street mapping key and billing project, not Earth Engine signup.

---

### User Story 3 - Dispatcher sees a road-based trip, not a straight line of stops (Priority: P2)

As a dispatcher, when I review or optimize a route, I get driving distance, estimated time, and a road-following line on the map between Wiley School and the ordered stops.

**Why this priority**: Capacity assignment already exists; without a road graph, “trip planning” is still a list of points.

**Independent Test**: For a route with at least two geocoded stops, requesting a drive path returns distance, duration, and a polyline that the map can draw. Failure of the routing service leaves existing stop order intact and shows a recoverable error. Unit tests use recorded/fake HTTP, not live billing.

**Acceptance Scenarios**:

1. **Given** a mapping key and a route with geocoded stops, **When** the dispatcher generates or refreshes the trip path, **Then** `WaypointsJson` (or equivalent) holds a drive polyline and the map draws it.
2. **Given** routing unavailable, **When** optimize or refresh runs, **Then** student-to-route capacity assignment still works; the path step is skipped with a logged warning.
3. **Given** a student set and several active routes, **When** a route-matrix helper runs (optional within this story), **Then** it can rank drive time/distance without changing assignments until the clerk confirms.

---

### User Story 4 - Address type-ahead while typing (Priority: P3)

As a clerk, I can pick a suggested street address as I type so I spend less time correcting spelling.

**Why this priority**: Nice-to-have; validation + geocode (US1) already delivers correctness.

**Independent Test**: Student form suggestions appear only when a mapping key is present; choosing a suggestion fills components and still runs validation before save.

**Acceptance Scenarios**:

1. **Given** a mapping key, **When** the clerk types a street in the student form, **Then** address suggestions for the Wiley region appear.
2. **Given** no key, **When** the clerk types, **Then** the form behaves as today (no suggestions, no errors).

---

### Edge Cases

- Rural Prowers/Bent County addresses that USPS can certify but Google rooftop is approximate: store coordinates anyway; show precision (rooftop vs range vs approximate) to the clerk.
- Rate limits / quota: cache by normalized address; do not geocode on every keystroke (except P3 suggestions).
- Existing students with hash-scattered coordinates: treat as untrusted; re-validate on next edit, not a silent mass rewrite in this increment.
- Unofficial `mt1.google.com` map tiles: remove or disable; OSM remains default.
- Mapping key present but Address Validation API not enabled on the GCP project: surface a configuration error, not a crash.
- Offline tests and CI: never require a live Maps key; use fakes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST validate US student addresses with a postal-grade service (standardized components, deliverable/not-deliverable, human-readable failure) before treating them as mappable.
- **FR-002**: System MUST persist latitude and longitude from a successful validation onto the student record for map plotting.
- **FR-003**: System MUST NOT invent coordinates from a string hash when mapping is unconfigured or validation fails.
- **FR-004**: System MUST cache successful validations so repeated plots of the same address do not require a new paid call.
- **FR-005**: System MUST keep Syncfusion `SfMap` as the map surface. Students entered in the system are eligible (no geofence / no shapefile polygons).
- **FR-006**: System MUST remove Earth Engine from runtime: no EE DI services, no EE appsettings, no EE bootstrap, no EE connection probe, no EE secrets in agent docs.
- **FR-007**: System MUST load route geography from the database (stored waypoints or stop coordinates), not from an Earth Engine asset.
- **FR-008**: System MUST obtain drive distance, duration, and a road polyline for a route with geocoded stops when mapping is configured (US3).
- **FR-009**: System MUST fail open for optimize/capacity assignment if routing is down (log + message; do not block AM/PM seat fill).
- **FR-010**: System MUST load the mapping API key from macOS Passwords (Name = env var) on Mac and from environment variables on Windows; MUST never commit the key.
- **FR-011**: System MUST log mapping calls with Serilog (operation, success/failure, latency, never the API key or full raw PII payloads).
- **FR-012**: Automated tests MUST cover validation success/failure, no-key behavior, and routing parse without live network (CI filter unchanged: `Category!=Integration&Category!=InMemoryFlaky`).
- **FR-013**: Places-style type-ahead is optional (US4) and MUST NOT block US1–US3.

### Key Entities

- **ValidatedAddress**: Street, city, state, ZIP, standardized line, validity, precision, latitude, longitude, provider correlation id, retrieved-at.
- **StudentLocation**: Student id plus coordinates sourced from ValidatedAddress (existing student lat/lon fields).
- **RoutePath**: Route id, ordered stops, encoded/decoded polyline, distance, duration, computed-at.
- **MappingConfiguration**: Presence of API key, provider project (billing), enabled APIs; no service-account JSON.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A clerk can validate and plot a correct Wiley-area address in one save/validate action; the marker is within a city-block of the real location, not a random offset up to tens of kilometers.
- **SC-002**: With mapping unconfigured, 100% of plot/validate attempts show an explicit configuration message and 0% write hash-generated coordinates.
- **SC-003**: After this feature, starting the app with no Earth Engine secrets produces 0 Earth Engine log events and 0 service-account files written under the app data `keys` directory.
- **SC-004**: For a route of at least two geocoded stops, a dispatcher can display a road-following path and a distance/time summary without leaving the map/route screens.
- **SC-005**: CI unit tests for the new mapping services pass without a live mapping key.
- **SC-006**: Agent/geo documentation names one billing project and one mapping key; Earth Engine project `ee-bigessfour` is documented as unused by the app.

## Assumptions

- Nominated provider is Google Maps Platform (Address Validation with USPS CASS, Routes API) billed on `new-coursera-490518`.
- Wiley-scale volume is hundreds of students; validate on save; cache; route compute on demand.
- Renaming `MapView` / `MapViewModel` is out of scope (map UI stays; EE backend goes).
- `StudentRouteOptimizer` capacity fill remains; routing **adds** path geometry and optional matrix ranking, it does not replace seat-capacity rules in this increment.
- Local shapefiles remain the eligibility source; no Maps “dataset” upload in this feature.
- Constitution Geo line is amended in the same PR as implementation.
- Offline hasher remains only behind tests/demo flag if needed; production DI uses the mapping client or a no-op that returns null.

## Out of Scope

- Re-registering or repairing the `bus-buddy-gee` Earth Engine service account.
- Satellite imagery, NDVI, flood, or EE table export.
- Google Maps JavaScript, Navigation SDK, or replacing Syncfusion `SfMap`.
- Mass re-geocoding of the entire historical student table in this increment.
- AWS or any cloud host for the WPF app.
