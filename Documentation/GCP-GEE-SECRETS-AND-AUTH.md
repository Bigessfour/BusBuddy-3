# GCP / Google Maps Platform — Secrets & Authentication

Canonical geo secrets for BusBuddy-3 (spec [007-maps-platform-geo](../specs/007-maps-platform-geo/spec.md)).

Earth Engine is **not** an app dependency. Do not restore `GEE_*` keys, `GcpCredentialBootstrap`, or `GoogleEarthEngineService`.

## Status (active)

Runtime: Syncfusion SfMap with **Google Map Tiles API** roadmap tiles when `GOOGLE_MAPS_API_KEY` is set (OSM fail-open without a key/session). Google Maps Platform also provides address validation, Places autocomplete, and drive routing.

| API                                                                                                        | Use                                                           |
| ---------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| [Address Validation](https://developers.google.com/maps/documentation/address-validation)                  | Student/school validate + geocode (`IMapsGeoService`)         |
| [Places API (New)](https://developers.google.com/maps/documentation/places/web-service/place-autocomplete) | Address type-ahead on Student + School forms                  |
| [Routes API](https://developers.google.com/maps/documentation/routes)                                      | `computeRoutes` drive polyline + `computeRouteMatrix` ranking |
| [Map Tiles API](https://developers.google.com/maps/documentation/tile)                                     | District Map base imagery (ToS-compliant with Google content) |

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

| Env var                                        | Purpose                                                      |
| ---------------------------------------------- | ------------------------------------------------------------ |
| `GOOGLE_MAPS_API_KEY`                          | Maps Platform (AV + Places + Routes + Map Tiles + Geocoding) |
| `GCP_BILLING_PROJECT` / `GOOGLE_CLOUD_PROJECT` | `busbuddy-507301`                                            |
| `SYNCFUSION_LICENSE_KEY`                       | Syncfusion WPF                                               |
| `Syncfusion_API_Key`                           | Syncfusion MCP assistant                                     |

### API key restrictions (Cloud Console — not code)

Desktop WPF cannot use Android/iOS/HTTP-referrer restrictions. For a single district PC:

1. Restrict the key to these APIs only: **Address Validation**, **Places API (New)**, **Routes API**, **Geocoding API**, **Map Tiles API**.
2. Prefer **IP address** restriction for that clerk workstation (or a small outbound proxy), not an unrestricted key on a shared machine.
3. Never put the key in `appsettings` or commit it. Prefer Passwords (macOS) / machine env (Windows).

## Windows production / VM

Set `GOOGLE_MAPS_API_KEY` and `GCP_BILLING_PROJECT=busbuddy-507301` as machine/user env vars — no Keychain.

## Services in DI

| Type                              | Role                                                              |
| --------------------------------- | ----------------------------------------------------------------- |
| `GeoDataService`                  | `IGeoDataService` — routes/waypoints from Postgres                |
| `MapsGeoService`                  | `IMapsGeoService` + `IGeocodingService` (cached validate/geocode) |
| `GooglePlacesAutocompleteService` | `IPlacesAutocompleteService` (no-op without key)                  |
| `GoogleRoutingService`            | `IRoutingService` (drive path + route matrix; fail-open)          |
| `GoogleMapTileSessionService`     | `IGoogleMapTileSessionService` (Map Tiles createSession)           |

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
