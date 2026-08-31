# Implementation Plan: Maps Platform Geo (retire Earth Engine)

**Branch**: `feature/007-maps-us1-implement` | **Date**: 2026-08-17 | **Spec**: [spec.md](./spec.md) | **Status**: US1+US3 implemented (US4 skipped); open PR for Maps clients

**Input**: Feature specification from `/specs/007-maps-platform-geo/spec.md`

## Summary

Retire Google Earth Engine from BusBuddy runtime and replace the student/map/trip geo path with Google Maps Platform REST: Address Validation (USPS CASS) behind `IAddressValidationService` / `IGeocodingService`, Routes API behind a new `IRoutingService`, keep Syncfusion `SfMap` + OSM + local shapefiles. Offline hash geocoding must not run in production DI.

## Technical Context

**Language/Version**: C# / .NET 9 (`net9.0` Core, `net9.0-windows` WPF)

**Primary Dependencies**: Existing HttpClient; Google Maps REST (no EE client libs). Remove `Google.Apis.Drive.v3` if unused after EE deletion. Keep `Google.Apis.Auth` only if still required elsewhere (expect **remove** with EE).

**Storage**: Existing EF student lat/lon + `Route.WaypointsJson`; no new tables in this increment (optional cache columns documented in data-model)

**Testing**: xUnit in `BusBuddy.Tests`; fake `HttpMessageHandler`; CI filter `Category!=Integration&Category!=InMemoryFlaky`

**Target Platform**: Windows WPF (VM); Mac host builds with `EnableWindowsTargeting`

**Project Type**: Desktop (Syncfusion WPF) + Core class library

**Performance Goals**: Address validate on save &lt; 3s perceived; cache hits in-process; no geocode on every keystroke (US1)

**Constraints**: Serilog only; Syncfusion-only UI; no committed secrets; constitution v1.1.0 Geo = Maps + shapefiles, not EE

**Scale/Scope**: Wiley-scale hundreds of students; 4 user stories (P3 autocomplete optional)

## Constitution Check

_GATE: Must pass before Phase 0 research. Re-check after Phase 1 design._

| Gate                    | Status                                                                        |
| ----------------------- | ----------------------------------------------------------------------------- |
| I Spec-driven + RAG     | Pass — spec 007; constitution amended; docs listed for RAG re-index in polish |
| II Syncfusion-only UI   | Pass — keep `SfMap`; no Maps JS                                               |
| III Serilog-only        | Pass — mapping services use Serilog                                           |
| IV Layered architecture | Pass — Core HTTP services; WPF DI in `App.xaml.cs`; tests in BusBuddy.Tests   |
| V Hybrid Mac/Windows    | Pass — key via Passwords (Mac) / env (Windows)                                |
| VI Solo CI/CD           | Pass — `feature/007-maps-platform-geo` PR; no master push                     |
| VII YAGNI / no secrets  | Pass — delete unused EE; don’t mass-regeocode; key in Passwords               |
| Geo (v1.1.0)            | Pass — Maps Platform + shapefiles; EE not an app dependency                   |
| Hosting                 | Pass — no AWS/cloud app host                                                  |

Post-design re-check: contracts are Core interfaces + HTTPS to Google; no new UI control families. **Pass.**

## Project Structure

### Documentation (this feature)

```text
specs/007-maps-platform-geo/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── address-validation.md
│   └── routes.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
BusBuddy.Core/
  Configuration/          # retire GcpCredentialBootstrap EE; add GoogleMapsOptions
  Services/
    Interfaces/           # IGeocodingService, IRoutingService (new)
    GoogleMaps/           # AddressValidation + Geocoding + Routing clients
    OfflineGeocodingService.cs   # tests/demo only — not production DI
    GeoDataService.cs     # DB routes only; drop GetGeoJsonAsync
  # DELETE: GoogleEarthEngineService.cs, GoogleEarthEngineOptions EE export workflow
BusBuddy.WPF/
  App.xaml.cs             # DI: Maps clients; drop EE bootstrap
  Views/GoogleEarth/      # keep view; OSM only; remove mt1.google.com layer
  ViewModels/GoogleEarth/
BusBuddy.Tests/Core/      # GoogleMaps*Tests, routing parser tests
.github/scripts/          # replace GeeConnectionProbe with MapsConnectionProbe
Documentation/            # rewrite GCP-GEE-SECRETS-AND-AUTH.md → Maps
AGENTS.md, README.md, STEADY-STATE-AND-FINISH-ROADMAP.md
```

**Structure Decision**: Existing Core + WPF + Tests. New folder `BusBuddy.Core/Services/GoogleMaps/` for HTTP clients. Do not add a new project.

## Complexity Tracking

No constitution violations requiring justification.

## Implementation approach

1. **Constitution + docs** — already amended Geo to v1.1.0; rewrite secrets docs and `AGENTS.md` in polish.
2. **Options + DI** — `GoogleMapsOptions` (`ApiKey` from `GOOGLE_MAPS_API_KEY`, `RegionCode=US`, `EnableUspsCass=true`, `QuotaProject=new-coursera-490518`). Register `HttpClient` named `GoogleMaps`.
3. **US1** — `GoogleAddressValidationClient` implements `IAddressValidationService` + `IGeocodingService` (or thin adapters). Production DI must not use `OfflineGeocodingService`.
4. **US2** — Delete EE types and Drive-only packages; strip `GetGeoJsonAsync`; stop `BootstrapGcpCredentialsForProduction`; remove `GoogleEarthEngine` from all appsettings; remove unofficial Google tiles.
5. **US3** — `IRoutingService.ComputeDrivePathAsync` → Routes `computeRoutes`; write polyline into `WaypointsJson`.
6. **US4** — Deferred unless time; Places Autocomplete on student form.
7. **Tests** — `HttpMessageHandler` fakes from `contracts/` sample JSON.

## Risks

- Maps APIs not enabled / key unrestricted → clerk sees config error (acceptable).
- Removing `Google.Apis.*` if something else still references them → grep before delete.
- `IGeoDataService` still used for DB routes — keep interface, shrink implementation.
- View still named GoogleEarth\* — out of scope rename (spec).
