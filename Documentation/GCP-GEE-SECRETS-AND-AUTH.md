# GCP / Google Maps Platform — Secrets & Authentication

Canonical geo secrets for BusBuddy-3 (spec [007-maps-platform-geo](../specs/007-maps-platform-geo/spec.md)).

Earth Engine is **not** an app dependency. Do not restore `GEE_*` keys, `GcpCredentialBootstrap`, or `GoogleEarthEngineService`.

## Status (active)

Runtime: Syncfusion SfMap with **Google Map Tiles API** roadmap tiles when `GOOGLE_MAPS_API_KEY` is set (empty basemap without a key/session — no OSM). Google Maps Platform also provides address validation, Places autocomplete, and drive routing.

| API                                                                                                        | Use                                                                                                                                      |
| ---------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| [Address Validation](https://developers.google.com/maps/documentation/address-validation)                  | Student/school validate + geocode (`IMapsGeoService`)                                                                                    |
| [Places API (New)](https://developers.google.com/maps/documentation/places/web-service/place-autocomplete) | Address type-ahead on student, school, driver, depot, pickup stop, route stop, transfer, and trip destination forms (`PlacesAddressBox`) |
| [Routes API](https://developers.google.com/maps/documentation/routes)                                      | `computeRoutes` drive polyline + `computeRouteMatrix` ranking                                                                            |
| [Route Optimization API](https://developers.google.com/maps/documentation/route-optimization)              | Clerk-initiated stop order + same-day trip fleet (`optimizeTours`)                                                                   |
| [Map Tiles API](https://developers.google.com/maps/documentation/tile)                                     | District Map base imagery (ToS-compliant with Google content)                                                                            |

Students entered in the system are eligible — there is no geofence.

## Projects (do not invent IDs)

| Project ID            | Role                                                                                         |
| --------------------- | -------------------------------------------------------------------------------------------- |
| `busbuddy-507301`     | **Primary** GCP / billing / Maps APIs / `gcloud` default                                     |
| `new-coursera-490518` | Legacy Coursera project (billed; prefer `busbuddy-507301`). Do not header Maps traffic there |
| `ee-bigessfour`       | **Unused by the app** (historical Earth Engine — do not wire)                                |
| ~~`busbuddy-465000`~~ | **Invalid** — never invent                                                                   |

## macOS (dev) — Passwords app

Entry **Name** = env var. Loaded by `LoadApiKeysFromMacPasswords()` in `BusBuddy.WPF/App.xaml.cs`.

| Env var                                        | Purpose                                                                                                                                                                                                                                                    |
| ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GOOGLE_MAPS_API_KEY`                          | Maps Platform (AV + Places + Routes + Route Optimization + Map Tiles + Geocoding)                                                                                                                                                                          |
| `GCP_BILLING_PROJECT` / `GOOGLE_CLOUD_PROJECT` | **Leave unset for API keys.** Billing follows the project that owns `GOOGLE_MAPS_API_KEY` (create the key under `busbuddy-507301`). Setting these forces `X-Goog-User-Project` and often returns HTTP 403 (`serviceUsageConsumer`) even for a correct key. |
| `SYNCFUSION_LICENSE_KEY`                       | Syncfusion WPF                                                                                                                                                                                                                                             |
| `Syncfusion_API_Key`                           | Syncfusion MCP assistant                                                                                                                                                                                                                                   |

### API key restrictions (Cloud Console — not code)

Desktop WPF cannot use Android/iOS/HTTP-referrer restrictions. For a single district PC:

1. Restrict the key to these APIs only: **Address Validation**, **Places API (New)**, **Routes API**, **Route Optimization API**, **Geocoding API**, **Map Tiles API**.
2. Prefer **IP address** restriction for that clerk workstation (or a small outbound proxy), not an unrestricted key on a shared machine.
3. Never put the key in `appsettings` or commit it. Prefer Passwords (macOS) / machine env (Windows).

Every Google call sends the key in the `X-Goog-Api-Key` header (never in the URL) — the endpoints below are the ones that document header auth:

| Client                            | Endpoint                                                                                                                                                                                |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GoogleAddressValidationClient`   | `POST addressvalidation.googleapis.com/v1:validateAddress`                                                                                                                              |
| Geocoding fallback (same client)  | `GET geocode.googleapis.com/v4/geocode/address/{address}?regionCode=` + `X-Goog-FieldMask`                                                                                              |
| `GooglePlacesAutocompleteService` | `POST places.googleapis.com/v1/places:autocomplete`                                                                                                                                     |
| `GoogleRoutingService`            | `POST routes.googleapis.com/directions/v2:computeRoutes` + `X-Goog-FieldMask`                                                                                                           |
| `GoogleRouteOptimizationService`  | `POST routeoptimization.googleapis.com/v1/projects/{project}:optimizeTours` (API key header; vendor REST also documents OAuth `cloud-platform` + IAM `routeoptimization.locations.use`) |
| `GoogleMapTileSessionService`     | `POST tile.googleapis.com/v1/createSession`; `GET …/v1/2dtiles/{z}/{x}/{y}`; `GET …/tile/v1/viewport` (copyright)                                                                       |

The Map Tiles session token is scoped to the session and tiles are not written to the local tile cache; the `viewport` copyright string is displayed in the District Map attribution as the Map Tiles API Policies require.

## Windows production / VM

Set `GOOGLE_MAPS_API_KEY` as a machine/user env var (value from Credentials → **BusBuddy Maps Platform** under `busbuddy-507301`) — no Keychain. Do **not** set `GCP_BILLING_PROJECT` for API-key auth.

**Quota header:** Official Maps samples authenticate with the API key only. The app omits `X-Goog-User-Project` unless `GCP_BILLING_PROJECT` / `GOOGLE_CLOUD_PROJECT` / `GoogleMaps:QuotaProject` is explicitly set. Map Tiles/Routes still retry once without the header if a mis-set quota project returns 403.

## Services in DI

| Type                              | Role                                                                      |
| --------------------------------- | ------------------------------------------------------------------------- |
| `GeoDataService`                  | `IGeoDataService` — routes/waypoints from Postgres                        |
| `MapsGeoService`                  | `IMapsGeoService` + `IGeocodingService` (cached validate/geocode)         |
| `GooglePlacesAutocompleteService` | `IPlacesAutocompleteService` (no-op without key)                          |
| `GoogleRoutingService`            | `IRoutingService` (drive path + route matrix; fail-open)                  |
| `GoogleRouteOptimizationService`  | `IRouteOptimizationService` (`optimizeTours`; fail-open; clerk-initiated) |
| `GoogleMapTileSessionService`     | `IGoogleMapTileSessionService` (Map Tiles createSession)                  |

## gcloud CLI + MCP (project metadata)

`gcloud` is already the way to inspect GCP project, enabled APIs, billing, and API-key **metadata** for `busbuddy-507301`. It does **not** call Address Validation / Places / Routes (those need `GOOGLE_MAPS_API_KEY` via the app or the smoke probe below). Never run `gcloud services api-keys get-key-string`.

**Read-only status (no key material):**

```bash
.github/scripts/gcloud-maps-status.sh
```

**Cursor / Grok MCP:** project [`.cursor/mcp.json`](../.cursor/mcp.json) registers `gcloud` → `.github/scripts/run-gcloud-mcp.sh` (`npx @google-cloud/gcloud-mcp`, deny list in `.github/scripts/gcloud-mcp-deny.json`). Reload MCP after pulling. Tool: `run_gcloud_command`. Pin is `CLOUDSDK_CORE_PROJECT=busbuddy-507301`. Not added to global `~/.cursor/mcp.json` (keeps this quota project scoped to BusBuddy).

```bash
brew install --cask google-cloud-sdk   # if needed
gcloud auth login
gcloud config set project busbuddy-507301
```

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
