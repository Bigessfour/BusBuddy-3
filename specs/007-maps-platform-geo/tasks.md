# Tasks: Maps Platform Geo (retire Earth Engine)

**Input**: Design documents from `/specs/007-maps-platform-geo/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included (FR-012). Write failing tests before production clients.

**Organization**: Setup → Foundational (unblock) → US1 → US2 → US3 → US4 → Polish.

**Current product (after later map work):** District Map is Google Map Tiles only (`specs/maps.md`). T020/US2 originally left OSM as the default after removing unofficial Google tiles; that default is retired. Address intake is `PlacesAddressBox`; there is no `AddressValidationControl` in `BusBuddy.WPF/Controls`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1–US4 from spec.md

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Config surface for Maps; stop documenting EE as required in appsettings shape

- [x] T001 Add `GoogleMaps` section (ApiKey env placeholder, QuotaProject `busbuddy-507301`, EnableUspsCass true, RegionCode US) to `appsettings.json`, `BusBuddy.WPF/appsettings.json`, and `BusBuddy.Core/appsettings.json`; remove `GoogleEarthEngine` sections from those files
- [x] T002 Create `BusBuddy.Core/Configuration/GoogleMapsOptions.cs` (SectionName `GoogleMaps`) matching T001 keys
- [x] T003 [P] Add `GOOGLE_MAPS_API_KEY` to the Passwords load list in `BusBuddy.WPF/App.xaml.cs` (`LoadApiKeysFromMacPasswords`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: DI and HTTP host for Maps; unregister Earth Engine so no story depends on EE tokens

**⚠️ CRITICAL**: No user story work until this phase is complete

- [x] T004 Bind `GoogleMapsOptions` and register Maps `HttpClient` factories in `AddDataServices` / WPF `ConfigureServices` (key from env `GOOGLE_MAPS_API_KEY`; `QuotaProject` from `GCP_BILLING_PROJECT` then `GOOGLE_CLOUD_PROJECT` then JSON/default `busbuddy-507301`)
- [x] T005 Remove `BootstrapGcpCredentialsForProduction` invocation and `GoogleEarthEngineService` / EE `IGeoDataService` token factory from `BusBuddy.WPF/App.xaml.cs`; keep `IGeoDataService` as DB-backed `GeoDataService` without a bearer token
- [x] T006 Unconfigured geocode: `GoogleAddressValidationClient` returns null / MappingUnconfigured when key missing (supersedes separate `UnconfiguredGeocodingService`); production DI is not `OfflineGeocodingService`
- [x] T007 Strip `GetGeoJsonAsync` from `BusBuddy.Core/Services/IGeoDataService.cs` and `BusBuddy.Core/Services/GeoDataService.cs` (DB route methods stay)

**Checkpoint**: App starts without GEE env; map can still load DB routes; geocode returns null

---

## Phase 3: User Story 1 - Clerk saves a real address and sees it on the map (Priority: P1)

**Goal**: Postal-grade validate + persist lat/lon; no hash coordinates in production

**Independent Test**: Fake HTTP tests for deliverable/undeliverable/no-key; student save writes `Student.Latitude` / `Student.Longitude`; map plot uses stored coords

### Tests for User Story 1

- [x] T008 [P] [US1] Add tests in `BusBuddy.Tests/Core/GoogleAddressValidationClientTests.cs` for deliverable, undeliverable, and missing key using `HttpMessageHandler` fakes per `specs/007-maps-platform-geo/contracts/address-validation.md`
- [x] T009 [P] [US1] Add test in `BusBuddy.Tests/Core/GeocodingServiceRegistrationTests.cs` asserting production registration is not `OfflineGeocodingService`

### Implementation for User Story 1

- [x] T010 [US1] Implement `BusBuddy.Core/Services/GoogleMaps/GoogleAddressValidationClient.cs` (validate + geocode) per `specs/007-maps-platform-geo/contracts/address-validation.md`
- [x] T011 [US1] Implement or adapt `IAddressValidationService` in `BusBuddy.Core/Services/AddressValidationService.cs` to delegate to the Maps client when a key is present (keep regex only as last-resort format hint, not as success)
- [x] T012 [US1] Wire `IGeocodingService` and `IAddressValidationService` to the Maps client in `BusBuddy.WPF/App.xaml.cs` and `BusBuddy.Core/Extensions/ServiceCollectionExtensions.cs`; do not register `OfflineGeocodingService` in production
- [x] T013 [US1] Persist `Student.Latitude` / `Student.Longitude` from geocode in `BusBuddy.Core/Services/StudentService.cs` and student form plot path in `BusBuddy.WPF/ViewModels/Student/StudentFormViewModel.cs`
- [x] T014 [US1] Show mapping-unconfigured vs validation-failed messages in `BusBuddy.WPF/ViewModels/Student/StudentFormViewModel.cs` (no fake success)
- [x] T015 [US1] Serilog Information/Warning for validate/geocode in the Maps client (never log API key)

**Checkpoint**: US1 tests green; clerk path uses real or null coords only

---

## Phase 4: User Story 2 - Earth Engine is gone from the running product (Priority: P1)

**Goal**: Delete EE client, probe, Drive export, unofficial tiles; docs still updated in polish if needed for compile

**Independent Test**: Grep finds no `earthengine.googleapis.com` in Core/WPF; build succeeds; product map layer is Google Map Tiles (no `LayerType="OSM"`)

### Implementation for User Story 2

- [x] T016 [P] [US2] Delete `BusBuddy.Core/Services/GoogleEarthEngineService.cs` and update `BusBuddy.Tests/Core/GapsCoverageTests.cs` (remove EE constructor test)
- [x] T017 [P] [US2] Delete or gut `BusBuddy.Core/Configuration/GcpCredentialBootstrap.cs` and `BusBuddy.Core/Configuration/GoogleEarthEngineOptions.cs` if unused
- [x] T018 [P] [US2] Remove `Google.Apis.Drive.v3` (and unused `Google.Apis.*`) from `BusBuddy.Core/BusBuddy.Core.csproj` after grep confirms no remaining references
- [x] T019 [US2] **Skipped (pause):** `GeeConnectionProbe` deleted; `MapsConnectionProbe` not added until US1
- [x] T020 [US2] Remove unofficial `mt1.google.com` imagery. Later superseded: product `MapView` uses Google Map Tiles only (`GoogleMapTilesImageryLayer`); OSM is probe-only.
- [x] T021 [US2] Removed `.github/scripts/gcp-gee.env` and `.github/scripts/setup-gcp-gee.sh`; living docs no longer reference GEE setup scripts

**Checkpoint**: No EE runtime types; product map tiles Google-only (no OSM fail-open)

---

## Phase 5: User Story 3 - Road-based trip path (Priority: P2)

**Goal**: `computeRoutes` polyline + distance/time on routes with geocoded stops; fail-open for optimizer

**Independent Test**: Fake Routes HTTP; `WaypointsJson` updated; optimizer still assigns if routing throws

### Tests for User Story 3

- [x] T022 [P] [US3] Add tests in `BusBuddy.Tests/Core/GoogleRoutingServiceTests.cs` per `specs/007-maps-platform-geo/contracts/routes.md`

### Implementation for User Story 3

- [x] T023 [US3] Add `BusBuddy.Core/Services/IRoutingService.cs` and `BusBuddy.Core/Services/GoogleMaps/GoogleRoutingService.cs`
- [x] T024 [US3] Extend `BusBuddy.Core/Mapping/RouteWaypointSerializer.cs` if needed to store encoded polyline + points
- [x] T025 [US3] Call `IRoutingService` from route refresh in `BusBuddy.WPF/ViewModels/Map/MapViewModel.cs` and/or `BusBuddy.WPF/ViewModels/Route/RouteManagementViewModel.cs`
- [x] T026 [US3] Seat fill stays on `RouteService.AutoAssignStudentsAsync` if routing fails. `StudentRouteOptimizer` was removed; generation is `IRouteDeterminationService` and stop order is `IRouteOptimizationService`.
- [x] T027 [US3] Register `IRoutingService` in `BusBuddy.WPF/App.xaml.cs` (via `AddDataServices`)

**Checkpoint**: Path draws when key present; optimize works without key

---

## Phase 6: User Story 4 - Address type-ahead (Priority: P3)

**Goal**: Optional Places Autocomplete on student + school forms

**Independent Test**: No key → no suggestions, no errors

- [x] T028 [P] [US4] `GooglePlacesAutocompleteService` + `IPlacesAutocompleteService` (Places API New)
- [x] T029 [US4] StudentForm + SchoolDestinationForm address autocomplete popups; validate still required on save

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Agent docs, architecture, inventory, RAG

- [x] T030 [P] Rewrite `Documentation/GCP-GEE-SECRETS-AND-AUTH.md` for Maps (`GOOGLE_MAPS_API_KEY`, billing project); mark `ee-bigessfour` unused
- [x] T031 [P] Update `AGENTS.md` secrets table, RAG query hints, and key file index (remove GEE bootstrap as required)
- [x] T032 [P] Update `README.md` Environment Variables for Maps; remove EE production bootstrap as required
- [x] T033 Update architecture map in `STEADY-STATE-AND-FINISH-ROADMAP.md` (Geo node: GeoDataService / Maps / ShapefileEligibility — no GoogleEarthEngine)
- [x] T034 Update `.function-inventory.json` surfaces (drop `GoogleEarthEngineService`; add Maps client if P1) and regenerate `docs/function-inventory.generated.md` / `docs/function-tree.md`
- [x] T035 Update `docs/action-items.md` 007 checkboxes from this feature
- [x] T036 Run `python -m rag.index` after doc/constitution/spec edits
- [x] T037 Run `.github/scripts/validate-ci-local.sh` (or Mac equivalent restore/build/test filter) before PR — passed 2026-08-17 (Docker Core + host compile; host tests skipped on Darwin)
- [x] T038 Open PR `feature/007-maps-platform-geo` → `master` (Build & Test + CodeQL) — https://github.com/Bigessfour/BusBuddy-3/pull/31. After merge, `/code-review` on the Maps HTTP clients and key handling

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Immediate
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all stories
- **US1 (Phase 3)**: After Foundational — User Story 1
- **US2 (Phase 4)**: After Foundational; can overlap US1 if files don’t conflict (`App.xaml.cs` is shared — serialize with US1)
- **US3 (Phase 5)**: After US1 (needs real coordinates)
- **US4 (Phase 6)**: After US1; skippable
- **Polish (Phase 7)**: After US1+US2 (US3 docs if shipped)

### User Story Dependencies

- **US1**: No dependency on US3/US4
- **US2**: Independent of US1 except shared `App.xaml.cs` / csproj
- **US3**: Needs US1 coordinates for useful polylines
- **US4**: Needs US1 validation after suggestion pick

### Parallel Opportunities

- T001–T003 after options shape agreed
- T008/T009 tests in parallel
- T016–T018 deletes in parallel after grep
- T030–T032 docs in parallel

---

## Implementation Strategy

**Resumed 2026-08-17**: US1 + US3 implemented on `feature/007-maps-us1-implement`. **US4 completed 2026-09-03**: Places Autocomplete on Student + School forms. Open follow-up PR for Maps clients + DI; run `/code-review` on HTTP clients and key handling after merge.

### User Story 1 first (User Story 1 + Foundational + US2 compile-clean)

1. Phase 1–2
2. Phase 3 US1
3. Phase 4 US2 (must not leave deleted types referenced)
4. **STOP**: VM smoke — validate Wiley address, plot, no EE logs
5. Then US3 path; skip US4 unless requested

### Incremental Delivery

1. Setup + Foundational → app runs without EE
2. US1 → real addresses
3. US2 → dead code gone
4. US3 → road polyline
5. Polish + PR

---

## Phase 8: District Map plot layers (follow-up 2026-09-06)

**Goal**: One MapView/VM tree; plot schools, catalog pickups, and students (stop if assigned, else home) without three separate clicks. Live GPS stays off. Camera stays school → Settings depot/bbox → US overview (no baked town).

**Independent Test**: Unit tests on `MapViewModel` / `MapView.xaml`; VM smoke still required on Windows.

- [x] T042 [P] Add `MapMarkerLabels` prefixes (`SCH `, `PK `, `HOME `, `WP `, `DEPOT `) + `MapMarker.Kind`; merge same-kind only; Syncfusion school vs stop `MarkerTemplateSelector`
- [x] T043 [US1] Add `PlotPickupStopsCommand` → `IPickupStopService.GetActiveStopsAsync()` in `BusBuddy.WPF/ViewModels/Map/MapViewModel.cs` and **Plot Pickup Stops** in `BusBuddy.WPF/Views/Map/MapView.xaml`
- [x] T044 [US1] **Superseded by `specs/maps.md`:** plot stored coords only (`StudentPlotLocation.PinsFromStored`). Do **not** geocode inside `MapDistrictLayers` / `StudentsMapCoordinator`. Address Validation stays on the student form (`PlacesAddressBox`).
- [x] T045 [US1] **Superseded by `specs/maps.md`:** `InitializeMapDataAsync` loads depot → schools → pickups → stored student coords. Do **not** auto-select a route or refresh a drive path on first open.
- [x] T046 [P] Remove live-tracking chrome (**Show All Buses**, **Track Selected**, interval timer); keep one Fleet GPS status line until AVL exists
- [x] T047 Tests in `BusBuddy.Tests/WPF/MapViewTests.cs` and `MapViewModelTests.cs` for prefixes, pickup-vs-home, auto-seed, no live-tracking chrome
- [x] T048 Update `docs/action-items.md` (plot layers + remaining VM smoke / migration)
- [x] T049 [US1] Seed depot pin (`DEPOT `) and home pins (`HOME `) alongside PK/school; pickup-assigned students keep a home pin when coords differ
- [x] T050 [P] `MapDefaults.ZoomForBounds` for `CenterOnPoints` (SfMap has no fit-bounds API — Center + ZoomLevel only)
- [x] T051 Persist `RouteStops`-derived `WaypointsJson` from `GeoDataService` when the column is empty; never overwrite stored JSON (drive-path fail-open)
- [x] T052 [US1] HOME pins use a smaller `HomeMarkerTemplate` (8px vs 12px PK); skip catalog pickups at 0,0 (`PickupStop.HasGpsCoordinates`)

Polyline stays XAML `MapPolyline` on `SubShapeFileLayer` (Syncfusion documented API) plus `MapRouteTrailLayer.Refresh`. Quota project remains `busbuddy-507301`. Do not bake Wiley/Lamar into `MapDefaults`.

---

## Notes

- Constitution already amended to v1.1.0 in `.specify/memory/constitution.md` (this feature)
- Do not commit `GOOGLE_MAPS_API_KEY` or SA JSON
- `MapView*` rename is out of scope
- Suggested commit after implement: `feat: replace Earth Engine with Maps Platform geo (007)`
