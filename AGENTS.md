# BusBuddy agent instructions

AI agents (Cursor, Copilot, Claude, Grok, etc.) working in this repo should follow these pointers.

## Primary standards

- **Constitution (Spec-Kit)**: [.specify/memory/constitution.md](.specify/memory/constitution.md) — immutable architectural DNA. Use Cursor `/speckit-*` skills (Constitution → Specify → Plan → Tasks → Implement). Feature specs live under `specs/`. Never run `specify init --here --force` without backing up the constitution.
- **Due-outs tracker**: [docs/action-items.md](docs/action-items.md) — living checklist (Spec-Kit + Finish items). Historical narrative: [STEADY-STATE-AND-FINISH-ROADMAP.md](STEADY-STATE-AND-FINISH-ROADMAP.md).
- **Full technical rules**: [.github/copilot-instructions.md](.github/copilot-instructions.md) — architecture, Syncfusion, Serilog, RAG/MCP, anti-regression.
- **Syncfusion WPF skills**: [.cursor/skills/syncfusion-wpf-busbuddy/SKILL.md](.cursor/skills/syncfusion-wpf-busbuddy/SKILL.md) — BusBuddy overlay; vendor skills in `.agents/skills/` (gitignored, install via [.github/scripts/setup-syncfusion-skills.sh](.github/scripts/setup-syncfusion-skills.sh)). NuGet pin `SyncfusionVersion` in `Directory.Build.props` (**34.2.3**); WPF MCP `syncfusion-wpf-assistant` via `.github/scripts/run-syncfusion-mcp.sh` → NuGet `Syncfusion.WPF.MCP` / `search_docs` ([WPF MCP docs](https://help.syncfusion.com/wpf/mcp)). Passwords Name = `SYNCFUSION_API_KEY` / `Syncfusion_API_Key`. Feature: [specs/006-syncfusion-tool-integration/spec.md](specs/006-syncfusion-tool-integration/spec.md).
- **CI/CD workflow (solo developer)**: same file, section **Solo developer CI/CD workflow** — branch → PR → gates → auto-merge.
- **GCP / GEE / secrets**: [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](Documentation/GCP-GEE-SECRETS-AND-AUTH.md) — canonical auth reference.
- **Architecture map**: [STEADY-STATE-AND-FINISH-ROADMAP.md](STEADY-STATE-AND-FINISH-ROADMAP.md) (BusBuddy-3 Architecture Map section).

## Mandatory RAG usage

Before architectural, auth, CI, or cross-cutting changes:

1. Call `busbuddy-rag` → `search_repo_context` with a precise query.
2. Cite retrieved chunks (file:line) in reasoning.
3. Re-run `python -m rag.index` after updating docs listed in this file.

**High-value RAG queries:**

- `"Google Earth Engine GcpCredentialBootstrap Passwords production auth"`
- `"solo developer CI/CD auto-merge Build and Test CodeQL"`
- `"Postgres BUSBUDDY_CONNECTION docker-compose profiles"`
- `"BusBuddy-3 architecture diagram services CI Docker"`
- `"BusBuddy constitution Spec-Kit hybrid Mac Windows Ollama"`

## CI/CD quick reference

| Step                    | Action                                                     |
| ----------------------- | ---------------------------------------------------------- |
| Branch                  | `feature/<short-description>` from `master`                |
| Open PR                 | Target `master`; auto-merge enables automatically          |
| Merge gates             | `Build & Test`, `Security (CodeQL)` must pass              |
| Merge                   | Squash auto-merge when gates pass (no reviewer required)   |
| Direct push to `master` | Blocked by branch rules — use PRs                          |
| Optional                | Run **Docker CI simulation** workflow manually             |
| Release                 | Push to `master` publishes WPF artifact (non-blocking job) |
| Local pre-push          | `.github/scripts/validate-ci-local.sh`                     |

## Secrets & authentication

### macOS Passwords (entry Name = env var)

Loaded by `LoadApiKeysFromMacPasswords()` then `BootstrapGcpCredentialsForProduction()` in `BusBuddy.WPF/App.xaml.cs`.

| Env var                                        | Purpose                                                                                                    |
| ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `XAI_API_KEY` / `GROK_API_KEY`                 | Optional legacy xAI cloud key (`XAI:Provider=Xai` only). Default AI path is local Ollama — no key required |
| `SYNCFUSION_LICENSE_KEY`                       | Syncfusion WPF                                                                                             |
| `Syncfusion_API_Key`                           | Syncfusion MCP assistant                                                                                   |
| `GEE_PROJECT_ID`                               | `ee-bigessfour` (Earth Engine — not `busbuddy-465000`)                                                     |
| `GEE_SERVICE_ACCOUNT_EMAIL`                    | `bus-buddy-gee@ee-bigessfour.iam.gserviceaccount.com`                                                      |
| `GEE_SERVICE_ACCOUNT_JSON`                     | Full SA key JSON → materialized by `GcpCredentialBootstrap`                                                |
| `GOOGLE_APPLICATION_CREDENTIALS`               | Optional path to key file                                                                                  |
| `GCP_BILLING_PROJECT` / `GOOGLE_CLOUD_PROJECT` | `new-coursera-490518`                                                                                      |

**Setup scripts:**

```bash
.github/scripts/setup-gcp-gee.sh          # gcloud: SA + keys/bus-buddy-gee-key.json
.github/scripts/store-gcp-passwords.sh    # macOS Passwords
source .github/scripts/gcp-gee.env        # dev shell
```

### Production bootstrap (`GcpCredentialBootstrap`)

- Path: `BusBuddy.Core/Configuration/GcpCredentialBootstrap.cs`
- Materializes `GEE_SERVICE_ACCOUNT_JSON` to app data directory
- Sets `GoogleEarthEngine__*` env overrides for `IConfiguration`
- `IGeoDataService` gets live bearer token; `GoogleEarthEngineService` registered in DI

### Windows production

Set `GEE_SERVICE_ACCOUNT_JSON` or `GOOGLE_APPLICATION_CREDENTIALS` as machine/user env — no Keychain.

## GCP project map (agents must not hallucinate IDs)

| Project ID            | Role                                            |
| --------------------- | ----------------------------------------------- |
| `ee-bigessfour`       | Earth Engine API + service account              |
| `new-coursera-490518` | GCP console / billing / `gcloud config` default |
| ~~`busbuddy-465000`~~ | **Invalid** — removed from appsettings          |

## Local checks before PR

```bash
.github/scripts/validate-ci-local.sh
```

Or manually:

```bash
dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true
dotnet test BusBuddy.sln -c Release --no-build \
  --filter "Category!=Integration&Category!=InMemoryFlaky"
```

## Repo governance setup (one-time)

```bash
.github/scripts/setup-solo-ci-governance.sh
```

Requires `gh` CLI with admin access for auto-merge, branch ruleset, Dependabot alerts.

## Architecture diagram (mandatory for structural changes)

- Source: `STEADY-STATE-AND-FINISH-ROADMAP.md` → **BusBuddy-3 Architecture Map** (Mermaid)
- Optional editable: `Documentation/diagrams/busbuddy-3-architecture.mmd` if present
- Update diagram + run `python -m rag.index` when adding services, CI jobs, or auth flows
- Hybrid dev: Mac (Core/Docker/Passwords) + Windows VM (full WPF)

## Hybrid environment — agent checklist

Durable rules: [.specify/memory/constitution.md](.specify/memory/constitution.md) § V; detail: [DEVELOPMENT-GUIDE.md](DEVELOPMENT-GUIDE.md); feature: [specs/005-hybrid-dev-environment/spec.md](specs/005-hybrid-dev-environment/spec.md).

| Host       | Do                                                                                                                               | Do not                                            |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| Mac        | Core, Docker Postgres, RAG/MCP, Passwords secrets; `EnableWindowsTargeting` is in `Directory.Build.props` (CLI `-p:` still fine) | Claim WPF runs natively on macOS                  |
| Windows VM | Full Syncfusion WPF; shared folder; env / shared `keys/`                                                                         | Assume macOS Passwords/Keychain                   |
| Either     | Local Ollama for app AI; PR → Build & Test + CodeQL                                                                              | Invent AWS/cloud app hosting for BusBuddy runtime |

Launchers: `./run-wpf.sh` (Mac → UTM), `.\utm_run_in_vm.ps1` (inside VM). Postgres from VM uses Mac host IP (`run-wpf.sh` / `ipconfig getifaddr en0`).

## Key implementation files (quick index)

| Concern               | File                                                    |
| --------------------- | ------------------------------------------------------- |
| Passwords load        | `BusBuddy.WPF/App.xaml.cs`                              |
| GCP bootstrap         | `BusBuddy.Core/Configuration/GcpCredentialBootstrap.cs` |
| GEE service           | `BusBuddy.Core/Services/GoogleEarthEngineService.cs`    |
| Geo DI                | `BusBuddy.WPF/App.xaml.cs` → `ConfigureServices`        |
| AI chat (Ollama)      | `BusBuddy.WPF/Services/OllamaChatService.cs`            |
| CI workflow           | `.github/workflows/ci.yml`                              |
| Auto-merge            | `.github/workflows/auto-merge.yml`                      |
| RAG indexer           | `rag/index.py`                                          |
| Spec-Kit constitution | `.specify/memory/constitution.md`                       |

## Documentation to keep in sync

When changing auth, CI, or architecture, update:

1. `Documentation/GCP-GEE-SECRETS-AND-AUTH.md`
2. `README.md` (Quick Start + Environment Variables)
3. `AGENTS.md` (this file)
4. `STEADY-STATE-AND-FINISH-ROADMAP.md` (architecture map if structural)
5. Run `python -m rag.index`
