# GCP / Google Maps Platform — Secrets & Authentication

Canonical geo secrets for BusBuddy-3 after Earth Engine was retired (spec [007-maps-platform-geo](../specs/007-maps-platform-geo/spec.md)).

Earth Engine is **not** an app dependency. Do not restore `GEE_*` keys, `GcpCredentialBootstrap`, or `GoogleEarthEngineService`.

## Status (paused)

Runtime today: local DB waypoints + Syncfusion SfMap (OpenStreetMap) + shapefile eligibility + offline hash geocoder (placeholder).

**Next (paused):** Google Maps Platform on billing project `new-coursera-490518`:

| API | Use |
|-----|-----|
| Address Validation (`enableUspsCass`) | Student addresses + lat/lng |
| Routes (`computeRoutes`) | Drive polyline / distance / time |

Implementation is **not** wired yet. Resume from `specs/007-maps-platform-geo/tasks.md` US1 / US3.

## Projects (do not invent IDs)

| Project ID | Role |
|------------|------|
| `new-coursera-490518` | GCP console / billing / Maps APIs / `gcloud` default |
| `ee-bigessfour` | **Unused by the app** (historical Earth Engine — do not wire) |
| ~~`busbuddy-465000`~~ | **Invalid** — never invent |

## macOS (dev) — Passwords app

Entry **Name** = env var. Loaded by `LoadApiKeysFromMacPasswords()` in `BusBuddy.WPF/App.xaml.cs`.

| Env var | Purpose |
|---------|---------|
| `SYNCFUSION_LICENSE_KEY` | Syncfusion WPF |
| `Syncfusion_API_Key` | Syncfusion MCP assistant |
| `XAI_API_KEY` / `GROK_API_KEY` | Optional legacy cloud xAI; default AI is local Ollama |
| `GOOGLE_MAPS_API_KEY` | Maps Platform (when US1/US3 resume). Restrict to Address Validation + Routes |
| `GCP_BILLING_PROJECT` / `GOOGLE_CLOUD_PROJECT` | `new-coursera-490518` |

## Windows production / VM

Set `GOOGLE_MAPS_API_KEY` as a machine/user env var — no Keychain.

## Services in DI (current)

| Type | Role |
|------|------|
| `GeoDataService` | `IGeoDataService` — routes/waypoints from Postgres |
| `OfflineGeocodingService` | Temporary `IGeocodingService` until Maps client lands |
| `ShapefileEligibilityService` | Local Wiley district/town shapefiles |

## Never commit

- API keys, SA JSON, Passwords exports, or `.env` with secrets.

## Related

- Spec: `specs/007-maps-platform-geo/`
- Constitution: `.specify/memory/constitution.md` (Geo = Maps + shapefiles)
- Agent quick ref: `AGENTS.md`
