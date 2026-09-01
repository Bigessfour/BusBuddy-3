<!--
Sync Impact Report
Version change: 1.1.0 → 1.2.0 (MINOR — Geo: drop shapefile geofence and Earth Engine naming; Maps Platform Address Validation + SfMap)
Modified: Technology table Geo (shapefiles/eligibility polygons removed; students in the system are eligible; map UI is Syncfusion SfMap)
Modified: Last Amended 2026-08-31
Added: none
Removed: shapefile eligibility; hardcoded district school seed
Templates: plan/spec/tasks — no mandatory section changes
Follow-up: AGENTS.md, Documentation/GCP-GEE-SECRETS-AND-AUTH.md, architecture map
-->

# BusBuddy Constitution

This document is the immutable architectural DNA for BusBuddy-3.
All Spec-Kit features (`specs/**`), plans, tasks, and agent work MUST obey it.
Quick agent pointers live in `AGENTS.md`; full tactical rules live in `.github/copilot-instructions.md`.
When those conflict with this constitution, **this file wins** until amended under Governance.

## Core Principles

### I. Spec-Driven + RAG-First (NON-NEGOTIABLE)

- New work follows Spec-Kit: Constitution → Specify → Plan → Tasks → Implement (`/speckit-*` skills).
- Before architectural, auth, CI/CD, or cross-cutting changes, agents MUST call `busbuddy-rag` → `search_repo_context` with a precise query and cite retrieved `file:line` chunks.
- After significant doc, architecture, auth, CI, or Spec-Kit artifact changes, re-run `python -m rag.index`.
- Spec-Kit artifacts (this constitution, `specs/**`, plans, tasks) are first-class RAG sources.

### II. Syncfusion-Only UI

- WPF UI MUST use Syncfusion controls only (`SfDataGrid`, `SfMap`, Syncfusion Ribbon, etc.).
- Standard WPF controls that replace Syncfusion equivalents (e.g. `<DataGrid>`) are forbidden.
- Prefer official Syncfusion WPF docs and the project Syncfusion MCP / `.cursor/skills/syncfusion-wpf-busbuddy` skill.

### III. Serilog-Only Observability

- Use Serilog for all application logging. Do not introduce `Microsoft.Extensions.Logging` as the app logging API.
- Prefer structured message templates and enrichers already used in the solution.

### IV. Layered Architecture

- **Core**: domain models, EF Core data access, UnitOfWork + repository pattern, business services.
- **WPF**: Views + ViewModels (MVVM + CommunityToolkit.Mvvm), DI wiring in `App.xaml.cs`.
- **Tests**: prefer Postgres/Docker for realistic DB tests; filter out Integration/InMemoryFlaky in CI unit gates.
- Keep experimental or broken services disabled via `.disabled` rather than deleting history casually.

### V. Hybrid Development Reality

- BusBuddy WPF is **Windows-only**. Full UI runs on a Windows VM (UTM/Parallels), not natively on macOS.
- Mac host: Core, Docker Postgres, RAG/MCP, Passwords-backed secrets, builds with `-p:EnableWindowsTargeting=true`.
- Windows guest: Syncfusion WPF run/debug; shared folder to Mac repo.
- Agents MUST NOT invent cloud hosting, AWS deployment, or non-Windows WPF host assumptions for the app runtime.

### VI. Solo-Developer CI/CD

- Branch from `master` as `feature/<short-description>`; open PR to `master`.
- Merge gates: **Build & Test** and **Security (CodeQL)** must pass; squash auto-merge when green.
- Direct push to `master` is blocked. Local pre-push: `.github/scripts/validate-ci-local.sh`.

### VII. Simplicity & Anti-Regression

- Prefer the smallest change that satisfies the stated requirement (YAGNI).
- Do not reintroduce archived PowerShell `bb-*` workflows as the primary path; prefer `dotnet` / documented scripts.
- Do not commit secrets, license keys, or raw service-account JSON.

## Technology & Environment Constraints

| Area     | Rule                                                                                                                                                                                                                      |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UI       | Syncfusion WPF only; Windows target                                                                                                                                                                                       |
| Logging  | Serilog only                                                                                                                                                                                                              |
| Data     | EF Core; Postgres preferred for real tests (`docker-compose` profiles)                                                                                                                                                    |
| AI (app) | Prefer local Ollama (or compatible) behind existing service interfaces; cloud XAI is not required for core path                                                                                                           |
| Geo      | Google Maps Platform (Address Validation, Routes) on documented billing project; Syncfusion SfMap + OSM for display. Students entered in the system are eligible — no geofence. Earth Engine is **not** an app dependency |
| Hosting  | No cloud app hosting / no AWS for BusBuddy runtime                                                                                                                                                                        |

### GCP project map (do not hallucinate)

| Project ID            | Role                                                                  |
| --------------------- | --------------------------------------------------------------------- |
| `new-coursera-490518` | GCP console / billing / Maps APIs / `gcloud` default                  |
| `ee-bigessfour`       | **Unused by the app** (historical Earth Engine project — do not wire) |
| ~~`busbuddy-465000`~~ | **Invalid** — removed; never invent                                   |

### Secrets

- macOS: Passwords app entries (Name = env var), loaded by `LoadApiKeysFromMacPasswords()` in `BusBuddy.WPF/App.xaml.cs`.
- Windows production: machine/user env vars (including `GOOGLE_MAPS_API_KEY`); no Keychain.
- Canonical geo/auth doc: `Documentation/GCP-GEE-SECRETS-AND-AUTH.md` (retitled/rewritten under spec 007 to Maps; do not document Earth Engine as required).

## Spec-Kit Upgrade Caution

- Never run `specify init --here --force` without backing up `.specify/memory/constitution.md` first.
- Force re-init can overwrite this constitution with the stock template.

## Development Workflow

1. Retrieve RAG context when the change is architectural or cross-cutting.
2. Author or update a feature under `specs/NNN-short-name/` via Spec-Kit skills.
3. Implement against the plan/tasks; keep constitution compliance.
4. Update architecture map in `STEADY-STATE-AND-FINISH-ROADMAP.md` when structure changes; re-index RAG.
5. Open PR; wait for Build & Test + CodeQL; auto-merge.

## Governance

- This constitution supersedes informal agent habits and conflicting draft docs.
- Amendments require an explicit PR that updates this file, notes **Last Amended**, and re-indexes RAG.
- Runtime tactical detail remains in `.github/copilot-instructions.md` and `AGENTS.md` as long as they stay consistent with this document.
- Complexity beyond stated requirements must be justified in the PR or rejected.

**Version**: 1.2.0 | **Ratified**: 2026-07-24 | **Last Amended**: 2026-08-31
