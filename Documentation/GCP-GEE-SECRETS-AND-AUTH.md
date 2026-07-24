# GCP / Google Earth Engine — Secrets & Authentication

Canonical reference for BusBuddy-3 Earth Engine and related secrets.  
Keep in sync with `AGENTS.md`, `README.md` (Environment Variables), and `.specify/memory/constitution.md`.

## Projects (do not invent IDs)

| Project ID | Role |
|------------|------|
| `ee-bigessfour` | Earth Engine API + service account |
| `new-coursera-490518` | GCP console / billing / `gcloud` default |
| ~~`busbuddy-465000`~~ | **Invalid** — removed; never invent |

Service account (typical): `bus-buddy-gee@ee-bigessfour.iam.gserviceaccount.com`

## macOS (dev) — Passwords app

Entry **Name** = env var name. Loaded at startup by `LoadApiKeysFromMacPasswords()` then `BootstrapGcpCredentialsForProduction()` in `BusBuddy.WPF/App.xaml.cs`.

| Env var | Purpose |
|---------|---------|
| `SYNCFUSION_LICENSE_KEY` | Syncfusion WPF |
| `Syncfusion_API_Key` | Syncfusion MCP assistant (`run-syncfusion-mcp.sh`) |
| `XAI_API_KEY` / `GROK_API_KEY` | Optional legacy cloud xAI (`XAI:Provider=Xai`); default AI is local Ollama |
| `GEE_PROJECT_ID` | `ee-bigessfour` |
| `GEE_SERVICE_ACCOUNT_EMAIL` | SA email |
| `GEE_SERVICE_ACCOUNT_JSON` | Full SA key JSON → materialized by `GcpCredentialBootstrap` |
| `GOOGLE_APPLICATION_CREDENTIALS` | Optional path to key file |
| `GCP_BILLING_PROJECT` / `GOOGLE_CLOUD_PROJECT` | `new-coursera-490518` |

Setup helpers:

```bash
.github/scripts/setup-gcp-gee.sh      # gcloud: SA + keys/bus-buddy-gee-key.json
.github/scripts/store-gcp-passwords.sh # macOS Passwords
source .github/scripts/gcp-gee.env    # dev shell
```

## Production bootstrap (`GcpCredentialBootstrap`)

- Path: `BusBuddy.Core/Configuration/GcpCredentialBootstrap.cs`
- Materializes `GEE_SERVICE_ACCOUNT_JSON` to app data directory
- Sets `GoogleEarthEngine__*` env overrides for `IConfiguration`
- `IGeoDataService` gets live bearer token; `GoogleEarthEngineService` registered in WPF DI

## Windows production / VM

Set `GEE_SERVICE_ACCOUNT_JSON` or `GOOGLE_APPLICATION_CREDENTIALS` as machine/user env — no Keychain.  
Shared `keys/` from Mac host is acceptable for VM smoke.

## Services wired in DI

| Type | Role |
|------|------|
| `GoogleEarthEngineService` | Full GEE export workflow (service account auth) |
| `GeoDataService` | `IGeoDataService` — REST calls with bearer token |
| `ShapefileEligibilityService` | Local Wiley district/town shapefiles (non-GEE) |

## Never commit

- Raw SA JSON, license keys, Passwords exports, or `.env` with secrets.

## Related

- Spec-Kit constitution: `.specify/memory/constitution.md`
- Agent quick ref: `AGENTS.md`
- Due-outs: `docs/action-items.md`
