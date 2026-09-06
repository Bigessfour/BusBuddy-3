# GCP / Google Maps Platform — Secrets & Authentication

Canonical geo secrets for BusBuddy-3 (spec [007-maps-platform-geo](../specs/007-maps-platform-geo/spec.md)).

Earth Engine is **not** an app dependency. Do not restore `GEE_*` keys, `GcpCredentialBootstrap`, or `GoogleEarthEngineService`.

## Status (active)

Runtime: local DB waypoints + Syncfusion SfMap (OpenStreetMap tiles). Google Maps Platform provides address validation, Places autocomplete, and drive routing when `GOOGLE_MAPS_API_KEY` is set.

| API                                                                                                        | Use                                                           |
| ---------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| [Address Validation](https://developers.google.com/maps/documentation/address-validation)                  | Student/school validate + geocode (`IMapsGeoService`)         |
| [Places API (New)](https://developers.google.com/maps/documentation/places/web-service/place-autocomplete) | Address type-ahead on Student + School forms                  |
| [Routes API](https://developers.google.com/maps/documentation/routes)                                      | `computeRoutes` drive polyline + `computeRouteMatrix` ranking |

Students entered in the system are eligible — there is no geofence.

## Projects (do not invent IDs)

| Project ID            | Role                                                          |
| --------------------- | ------------------------------------------------------------- |
| `busbuddy-507301`     | **Primary** GCP / billing / Maps APIs / `gcloud` default      |
| `new-coursera-490518` | Legacy Coursera project (billed; prefer `busbuddy-507301`)    |
| `ee-bigessfour`       | **Unused by the app** (historical Earth Engine — do not wire) |
| ~~`busbuddy-465000`~~ | **Invalid** — never invent                                    |

## macOS (dev) — Passwords app

Entry **Name** = env var. Loaded by `LoadApiKeysFromMacPasswords()` in `BusBuddy.WPF/App.xaml.cs`.

| Env var                                        | Purpose                                              |
| ---------------------------------------------- | ---------------------------------------------------- |
| `GOOGLE_MAPS_API_KEY`                          | Maps Platform (Address Validation + Places + Routes) |
| `GCP_BILLING_PROJECT` / `GOOGLE_CLOUD_PROJECT` | `busbuddy-507301`                                    |
| `SYNCFUSION_LICENSE_KEY`                       | Syncfusion WPF                                       |
| `Syncfusion_API_Key`                           | Syncfusion MCP assistant                             |

Restrict the Maps key to: Address Validation API, Places API (New), Routes API.

## Windows production / VM

Set `GOOGLE_MAPS_API_KEY` and `GCP_BILLING_PROJECT=busbuddy-507301` as machine/user env vars — no Keychain.

## Services in DI

| Type                              | Role                                                              |
| --------------------------------- | ----------------------------------------------------------------- |
| `GeoDataService`                  | `IGeoDataService` — routes/waypoints from Postgres                |
| `MapsGeoService`                  | `IMapsGeoService` + `IGeocodingService` (cached validate/geocode) |
| `GooglePlacesAutocompleteService` | `IPlacesAutocompleteService` (no-op without key)                  |
| `GoogleRoutingService`            | `IRoutingService` (drive path + route matrix; fail-open)          |
| `OfflineGeocodingService`         | Tests/demo only — **not** registered in production DI             |

## Smoke probe

```bash
.github/scripts/run-maps-connection-probe.sh
```

Tests Address Validation, Routes, and Places Autocomplete with `GOOGLE_MAPS_API_KEY`.

## Never commit

- API keys, SA JSON, Passwords exports, or `.env` with secrets.

## Related

- Spec: `specs/007-maps-platform-geo/`
- Quickstart: `specs/007-maps-platform-geo/quickstart.md`
- Constitution: `.specify/memory/constitution.md`
- Agent quick ref: `AGENTS.md`
