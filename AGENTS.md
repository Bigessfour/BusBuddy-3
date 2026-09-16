# BusBuddy agent instructions

AI agents (Cursor, Copilot, Claude, Grok, etc.) working in this repo should follow these pointers.

## Primary standards

- **Constitution (Spec-Kit)**: [.specify/memory/constitution.md](.specify/memory/constitution.md) — immutable architectural DNA. Use Cursor `/speckit-*` skills (Constitution → Specify → Plan → Tasks → Implement). Feature specs live under `specs/`. **Domain contract:** [specs/README.md](specs/README.md) plus `specs/{students,locations,routes,trips,drivers,buses,maps}.md`. Never run `specify init --here --force` without backing up the constitution.
- **Due-outs tracker (ship SSOT)**: [docs/action-items.md](docs/action-items.md) — open work only. Clerk spine: [docs/clerk-path.md](docs/clerk-path.md). **Project-done judge:** [docs/done-checklist.md](docs/done-checklist.md) — run `python3 .github/scripts/check-project-done.py`; exit 0 means the project is done. Historical finish narrative archived under [Documentation/Archive/2026-06-Steady-State-Finish/](Documentation/Archive/2026-06-Steady-State-Finish/) (root [STEADY-STATE-AND-FINISH-ROADMAP.md](STEADY-STATE-AND-FINISH-ROADMAP.md) is a stub).
- **Full technical rules**: [.github/copilot-instructions.md](.github/copilot-instructions.md) — architecture, Syncfusion, Serilog, RAG/MCP, anti-regression.
- **Syncfusion WPF skills**: [.cursor/skills/syncfusion-wpf-busbuddy/SKILL.md](.cursor/skills/syncfusion-wpf-busbuddy/SKILL.md) — BusBuddy overlay; vendor skills in `.agents/skills/` (gitignored, install via [.github/scripts/setup-syncfusion-skills.sh](.github/scripts/setup-syncfusion-skills.sh)). NuGet pin `SyncfusionVersion` in `Directory.Build.props` (**34.2.3**); WPF MCP `syncfusion-wpf-assistant` via `.github/scripts/run-syncfusion-mcp.sh` → NuGet `Syncfusion.WPF.MCP` / `search_docs` ([WPF MCP docs](https://help.syncfusion.com/wpf/mcp)). Passwords Name = `SYNCFUSION_API_KEY` / `Syncfusion_API_Key`. Feature: [specs/006-syncfusion-tool-integration/spec.md](specs/006-syncfusion-tool-integration/spec.md).
- **CI/CD workflow (solo developer)**: same file, section **Solo developer CI/CD workflow** — branch → PR → gates → auto-merge.
- **GCP / Maps / secrets**: [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](Documentation/GCP-GEE-SECRETS-AND-AUTH.md) — Maps Platform (Address Validation + Places + Routes) + Passwords. Earth Engine is not an app dependency. Inspect live GCP with `.github/scripts/gcloud-maps-status.sh` or project MCP `gcloud` (`run_gcloud_command`). Do not print API key strings.
- **Architecture map**: [Documentation/diagrams/busbuddy-3-architecture.md](Documentation/diagrams/busbuddy-3-architecture.md).

## Mandatory RAG usage

Before architectural, auth, CI, or cross-cutting changes:

1. Call `busbuddy-rag` → `search_repo_context` with a precise query.
2. Cite retrieved chunks (file:line) in reasoning.
3. Re-run `python -m rag.index` after updating docs listed in this file.

**High-value RAG queries:**

- `"Google Maps Platform GOOGLE_MAPS_API_KEY Address Validation Routes"`
- `"solo developer CI/CD auto-merge Build and Test"`
- `"Postgres BUSBUDDY_CONNECTION docker-compose profiles"`
- `"BusBuddy-3 architecture diagram services CI Docker"`
- `"BusBuddy constitution Spec-Kit hybrid Mac Windows Ollama"`

## CI/CD quick reference

| Step                    | Action                                                      |
| ----------------------- | ----------------------------------------------------------- |
| Branch                  | `feature/<short-description>` from `master`                 |
| Open PR                 | Target `master`; auto-merge enables automatically           |
| Merge gates             | `Build & Test` must pass (repo is private — see note below) |
| Merge                   | Squash auto-merge when gates pass (no reviewer required)    |
| Direct push to `master` | Blocked by branch rules — use PRs                           |
| Optional                | Run **Docker CI simulation** workflow manually              |
| Release                 | `gh workflow run ci.yml --ref master` publishes the WPF artifact (non-blocking job). Auto-merged squashes are bot-attributed and do not fire `push` |
| Local pre-push          | `.github/scripts/validate-ci-local.sh`                      |

**No CodeQL gate.** This repo is private, and GitHub code scanning on private repositories requires a paid GitHub Code Security license (available on Team/Enterprise only — not Free or Pro). `codeql-action/analyze` cannot upload its SARIF without one, so a `Security (CodeQL)` check would fail permanently and block every PR. The job was removed from `ci.yml` and from the required checks in the `Master solo-dev gates` ruleset. Do not re-add it while the repo is private.

## Secrets & authentication

### macOS Passwords (entry Name = env var)

Loaded by `LoadApiKeysFromMacPasswords()` in `BusBuddy.WPF/App.xaml.cs`.

| Env var                                        | Purpose                                                                                                    |
| ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `XAI_API_KEY` / `GROK_API_KEY`                 | Optional legacy xAI cloud key (`XAI:Provider=Xai` only). Default AI path is local Ollama — no key required |
| `SYNCFUSION_LICENSE_KEY`                       | Syncfusion WPF                                                                                             |
| `Syncfusion_API_Key`                           | Syncfusion MCP assistant                                                                                   |
| `GOOGLE_MAPS_API_KEY`                          | Google Maps Platform (Address Validation + Places + Routes + Map Tiles) — optional; fail-open without key  |
| `GCP_BILLING_PROJECT` / `GOOGLE_CLOUD_PROJECT` | Leave unset for API keys (see [GCP-GEE-SECRETS-AND-AUTH.md](Documentation/GCP-GEE-SECRETS-AND-AUTH.md))    |

**Setup:** Store Passwords entries (Name = env var). Maps API key is optional (app degrades gracefully). There is no Earth Engine setup script.

### Windows production

Set `GOOGLE_MAPS_API_KEY` as a machine/user env var when Maps clients are wired — no Keychain. Do not set `GCP_BILLING_PROJECT` for API-key auth.

## GCP project map (agents must not hallucinate IDs)

| Project ID            | Role                                                                                         |
| --------------------- | -------------------------------------------------------------------------------------------- |
| `busbuddy-507301`     | **Primary** GCP / billing / Maps Platform / `gcloud` default                                 |
| `new-coursera-490518` | Legacy Coursera project (billed; prefer `busbuddy-507301`). Do not header Maps traffic there |
| `ee-bigessfour`       | **Unused by the app** (historical Earth Engine — do not wire)                                |
| ~~`busbuddy-465000`~~ | **Invalid** — removed from appsettings                                                       |

## Local checks before PR

```bash
.github/scripts/validate-ci-local.sh
python3 .github/scripts/check-project-done.py
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

- Source: [Documentation/diagrams/busbuddy-3-architecture.md](Documentation/diagrams/busbuddy-3-architecture.md)
- Optional editable: `Documentation/diagrams/busbuddy-3-architecture.mmd` if present
- Update diagram + run `python -m rag.index` when adding services, CI jobs, or auth flows
- Hybrid dev: Mac (Core/Docker/Passwords) + Windows VM (full WPF)

## Hybrid environment — agent checklist

Durable rules: [.specify/memory/constitution.md](.specify/memory/constitution.md) § V; detail: [DEVELOPMENT-GUIDE.md](DEVELOPMENT-GUIDE.md); feature: [specs/005-hybrid-dev-environment/spec.md](specs/005-hybrid-dev-environment/spec.md).

| Host       | Do                                                                                                                               | Do not                                            |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| Mac        | Core, Docker Postgres, RAG/MCP, Passwords secrets; `EnableWindowsTargeting` is in `Directory.Build.props` (CLI `-p:` still fine) | Claim WPF runs natively on macOS                  |
| Windows VM | Full Syncfusion WPF; shared folder; env / shared `keys/`                                                                         | Assume macOS Passwords/Keychain                   |
| Either     | Local Ollama for app AI; PR → Build & Test                                                                                       | Invent AWS/cloud app hosting for BusBuddy runtime |

Launchers: `./run-wpf.sh` (Mac → UTM), `.\utm_run_in_vm.ps1` (inside VM), `./Scripts/utm-dev-bridge.sh` (SSH sync), `./Scripts/utm-wpf-test.sh` (sync + `dotnet test` on guest WPF testhost). Postgres from VM uses Mac host IP (`run-wpf.sh` / `ipconfig getifaddr en0`).

## Key implementation files (quick index)

| Concern               | File                                                                                                     |
| --------------------- | -------------------------------------------------------------------------------------------------------- |
| Passwords load        | `BusBuddy.WPF/App.xaml.cs`                                                                               |
| Geo (DB + map)        | `MapView` / `MapViewModel`; Google Map Tiles only (no OSM); camera via `DistrictMapAnchor`               |
| Geo (district config) | Settings → bus barn + bbox (`IDistrictSettingsAccessor`); not a baked-in town                            |
| Geo (Maps Platform)   | [spec 007](specs/007-maps-platform-geo/spec.md) — Address Validation + Places + Routes; not Earth Engine |
| Geo (GCP inspect)     | `gcloud` + `.github/scripts/gcloud-maps-status.sh`; Cursor MCP `gcloud` in `.cursor/mcp.json`            |
| Geo DI                | `BusBuddy.WPF/App.xaml.cs` → `ConfigureServices`                                                         |
| AI chat (Ollama)      | `BusBuddy.WPF/Services/OllamaChatService.cs`                                                             |
| CI workflow           | `.github/workflows/ci.yml`                                                                               |
| WPF tests on UTM      | `./Scripts/utm-wpf-test.sh` (SSH → guest `dotnet test`; needs WindowsDesktop)                            |
| Auto-merge            | `.github/workflows/auto-merge.yml`                                                                       |
| RAG indexer           | `rag/index.py`                                                                                           |
| Spec-Kit constitution | `.specify/memory/constitution.md`                                                                        |
| Project-done checker  | [docs/done-checklist.md](docs/done-checklist.md) + `.github/scripts/check-project-done.py`               |

## Documentation to keep in sync

When changing auth, CI, or architecture, update:

1. `Documentation/GCP-GEE-SECRETS-AND-AUTH.md`
2. `README.md` (Quick Start + Environment Variables)
3. `AGENTS.md` (this file)
4. `Documentation/diagrams/busbuddy-3-architecture.md` (architecture map if structural)
5. `docs/action-items.md` (if open ship work changed)
6. `docs/done-checklist.md` / `docs/done-catalog.json` (if the definition of done changed)
7. Run `python -m rag.index`
