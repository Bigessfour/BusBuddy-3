# Feature Specification: Syncfusion Tool Integration

**Feature Branch**: `feature/006-syncfusion-tool-integration`

**Created**: 2026-07-24

**Status**: Implementing (NuGet 34.1.32 + MCP paths + deps audit applied; Windows VM smoke pending)

**Input**: Integrate and refresh Syncfusion agent tooling (WPF MCP + WPF Skills), upgrade Syncfusion WPF NuGet packages to the latest stable release, and audit/bump core infrastructure dependency versions in `Directory.Build.props` for current enhancements—without breaking Syncfusion-only UI, hybrid Mac/Windows builds, or license bootstrap.

## Baseline (as of draft)

| Area                 | Current                                                                                                                                                                                                                                                        | Target intent                                                                                                             |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Syncfusion NuGet pin | `33.2.10` in [`Directory.Build.props`](../../Directory.Build.props)                                                                                                                                                                                            | Latest stable on nuget.org (observed **34.1.31** for `Syncfusion.SfGrid.WPF` as of 2026-07-13; confirm at implement time) |
| WPF MCP              | `syncfusion-wpf-assistant` via [`.github/scripts/run-syncfusion-mcp.sh`](../../.github/scripts/run-syncfusion-mcp.sh) (`npx @syncfusion/wpf-assistant@latest`)                                                                                                 | Working from this repo path; key from Passwords / env; documented for agents                                              |
| MCP paths            | [`.cursor/mcp.json`](../../.cursor/mcp.json) still references `/Users/stephenmckitrick/BusBuddy/BusBuddy-3` for filesystem + script                                                                                                                            | Paths resolve to the active BusBuddy-3 workspace (Box path or relative/script-based)                                      |
| WPF Skills           | Overlay [`.cursor/skills/syncfusion-wpf-busbuddy`](../../.cursor/skills/syncfusion-wpf-busbuddy/SKILL.md); vendor skills via [`.github/scripts/setup-syncfusion-skills.sh`](../../.github/scripts/setup-syncfusion-skills.sh) → `.agents/skills/` (gitignored) | Overlay updated for new major; vendor skills refreshable (`npx skills update`)                                            |
| Core infra pins      | EF `9.0.8`, Serilog `4.3.0`, Microsoft.Extensions `9.0.8`, CommunityToolkit.Mvvm `8.4.2`, etc.                                                                                                                                                                 | Audited against nuget.org; bump to latest **compatible** stables (same major TFM / no breaking host assumptions)          |

Constitution constraints (MUST obey): Syncfusion-only UI; Serilog-only logging; Windows WPF + `EnableWindowsTargeting` on Mac; RAG-first for architectural changes; no AWS/cloud app hosting.

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Syncfusion WPF MCP works in Cursor (Priority: P1)

As a developer using Cursor, I can invoke the Syncfusion WPF MCP assistant against this repo so UI guidance comes from Syncfusion’s assistant, not invented APIs.

**Why this priority**: Without a working MCP, agents invent Syncfusion patterns and violate constitution Syncfusion-only rules.

**Independent Test**: From the active workspace, MCP server `syncfusion-wpf-assistant` starts; a probe prompt (e.g. SfDataGrid binding) returns a usable answer when `Syncfusion_API_Key` / Passwords entry is present. Paths in `.cursor/mcp.json` match the workspace (no stale `/Users/stephenmckitrick/BusBuddy/BusBuddy-3` unless that is still the real clone).

**Acceptance Scenarios**:

1. **Given** `Syncfusion_API_Key` (or Passwords name `SYNCFUSION_API_KEY` / `Syncfusion_API_Key`), **When** Cursor loads project MCP, **Then** `syncfusion-wpf-assistant` is listed and runnable via `run-syncfusion-mcp.sh`.
2. **Given** the Box (or current) clone path, **When** inspecting `.cursor/mcp.json`, **Then** filesystem / script paths point at this repo (or use a path strategy that does not break on machine moves).
3. **Given** no API key, **When** the launcher runs, **Then** it fails clearly (non-zero / stderr) without hanging or leaking secrets into the repo.

---

### User Story 2 - Syncfusion WPF Skills are current and BusBuddy-aware (Priority: P1)

As an agent editing WPF XAML, I load official Syncfusion component skills plus the BusBuddy overlay so markup matches installed packages and project themes.

**Why this priority**: Skills must track the NuGet major (33 → 34) and existing BusBuddy theming rules.

**Independent Test**: `.github/scripts/setup-syncfusion-skills.sh` succeeds; `.agents/skills/syncfusion-wpf-*` exist locally; overlay skill documents the pinned `SyncfusionVersion` and package→skill map; `npx skills update` documented as the refresh path.

**Acceptance Scenarios**:

1. **Given** a fresh clone, **When** running `setup-syncfusion-skills.sh`, **Then** vendor skills install under `.agents/skills/` (still gitignored).
2. **Given** Syncfusion NuGet major bump, **When** reviewing `.cursor/skills/syncfusion-wpf-busbuddy/SKILL.md`, **Then** version notes and package map match `Directory.Build.props` / `BusBuddy.WPF.csproj`.
3. **Given** agent UI work, **When** following overlay rules, **Then** FluentDark/FluentLight via `SfSkinManager` remains mandatory and view-level theme dictionary merges stay forbidden.

---

### User Story 3 - Syncfusion WPF packages upgraded to latest stable (Priority: P1)

As a maintainer, all Syncfusion WPF PackageReferences resolve to one latest stable version via `$(SyncfusionVersion)`.

**Why this priority**: Primary product ask; unlocks control enhancements and fixes.

**Independent Test**: `Directory.Build.props` `SyncfusionVersion` equals the chosen nuget.org latest; `dotnet restore` + `dotnet build … -p:EnableWindowsTargeting=true` succeed; no mixed Syncfusion versions across projects.

**Acceptance Scenarios**:

1. **Given** implement-time nuget.org latest for `Syncfusion.SfGrid.WPF` (and peer WPF packages), **When** updating `SyncfusionVersion`, **Then** every Syncfusion.\* PackageReference uses `$(SyncfusionVersion)` only.
2. **Given** the bump, **When** building Release with `EnableWindowsTargeting`, **Then** 0 errors (warnings triage documented if new analyzer noise appears).
3. **Given** license registration in `App.xaml.cs`, **When** app starts on Windows VM, **Then** Syncfusion license still registers from env / Passwords (no trial watermarks when key present).
4. **Given** upgrade notes / breaking changes in Syncfusion 34.x release notes, **When** compiling XAML, **Then** any API renames required by the bump are fixed in-repo (no deferred broken views).

---

### User Story 4 - Core infrastructure dependency version check (Priority: P2)

As a maintainer, I audit and selectively bump non-Syncfusion pins in `Directory.Build.props` (EF Core, Npgsql, Serilog, Microsoft.Extensions.\*, CommunityToolkit.Mvvm, WebView2, test packages, Google client libs used by GEE, etc.) to latest compatible stables.

**Why this priority**: High leverage but secondary to Syncfusion tooling; must not destabilize CI.

**Independent Test**: Produce an audit table (current → candidate → decision); apply safe bumps; restore/build/test Core filter still green locally or in CI.

**Acceptance Scenarios**:

1. **Given** `Directory.Build.props` version properties, **When** checking nuget.org (or `dotnet list package --outdated`), **Then** an audit artifact is recorded under this feature folder (e.g. `deps-audit.md`).
2. **Given** a candidate bump, **When** it is a major that breaks net9-windows / Syncfusion / EF provider alignment, **Then** it is deferred with rationale (not force-upgraded).
3. **Given** applied bumps, **When** `dotnet test` with CI filter (`Category!=Integration&Category!=InMemoryFlaky`), **Then** Core regression set still passes (Windows CI or documented Mac limitation).
4. **Given** AutoMapper 12.x known advisory, **When** auditing, **Then** either upgrade to a fixed line or document explicit risk acceptance / replacement plan (do not ignore silently).

---

### User Story 5 - Docs and agent pointers stay aligned (Priority: P2)

As an agent, I discover MCP + skills + version pins from `AGENTS.md` / constitution-adjacent docs without stale “30.1.40” or wrong paths.

**Why this priority**: Stale docs caused prior version drift (PACKAGE-MANAGEMENT still mentions old versions in places).

**Independent Test**: Touch `AGENTS.md` Syncfusion bullet(s), `Documentation/PACKAGE-MANAGEMENT.md` Syncfusion section, overlay skill, and re-run `python -m rag.index` after merge.

**Acceptance Scenarios**:

1. **Given** new SyncfusionVersion, **When** searching docs for old pins (e.g. `30.1.40`, `33.2.10` after bump), **Then** canonical docs are updated or clearly marked archived.
2. **Given** MCP/skills setup, **When** reading `AGENTS.md`, **Then** one short pointer covers MCP key, skills install script, and NuGet pin location.

## Edge Cases

- License key valid for 33.x but not 34.x → document Syncfusion account license regeneration / Essential Studio version alignment.
- Mac cannot run WPF UI tests → build with `EnableWindowsTargeting` is the Mac gate; full UI smoke on Windows VM.
- Vendor skills install fails offline → script documents network requirement; overlay still committed.
- Mixed Syncfusion versions from transitive packages → force alignment via central property / explicit PackageReference versions.
- `mcp.json` is machine-specific → prefer script-relative invocation; avoid hard-coding another developer’s home path.

## Requirements _(mandatory)_

### Functional Requirements

- **FR-001**: Syncfusion WPF MCP (`syncfusion-wpf-assistant`) MUST launch via repo script with key from env/Passwords; no keys in git.
- **FR-002**: MCP configuration paths MUST resolve for the active BusBuddy-3 workspace.
- **FR-003**: BusBuddy Syncfusion overlay skill MUST remain committed; vendor skills remain installable and gitignored.
- **FR-004**: `SyncfusionVersion` MUST be updated to the latest stable Syncfusion WPF line confirmed at implement time; all Syncfusion PackageReferences MUST use that property.
- **FR-005**: Solution MUST restore and build Release with `-p:EnableWindowsTargeting=true` after the bump.
- **FR-006**: Produce `specs/006-syncfusion-tool-integration/deps-audit.md` listing core infra package current vs candidate vs action.
- **FR-007**: Apply agreed non-Syncfusion bumps from the audit; defer incompatible majors with written rationale.
- **FR-008**: Update agent/docs pointers (`AGENTS.md`, `PACKAGE-MANAGEMENT.md` Syncfusion section, overlay skill) and re-index RAG after merge.
- **FR-009**: Remain Syncfusion-only UI; do not introduce standard WPF DataGrid or non-Serilog logging while upgrading.

### Non-Goals

- Rewriting views for new Syncfusion controls not already in use.
- Cloud hosting / AWS.
- Committing `.agents/skills/` vendor tree or chroma DB.
- Expanding Spec-Kit constitution beyond a one-line pointer if versions are already covered by docs.

## Success Criteria _(mandatory)_

- MCP + skills refresh path documented and path-correct for this clone.
- Single Syncfusion NuGet version = latest stable at implement time; build green.
- `deps-audit.md` exists; safe bumps applied or explicitly deferred.
- Docs/RAG reflect new pins; no secret material added.

## Implementation notes (for `/speckit.plan`)

Suggested order:

1. Fix MCP paths + verify assistant launch.
2. Refresh skills (`setup-syncfusion-skills.sh` / `npx skills update`) + overlay version notes.
3. Bump `SyncfusionVersion` → restore/build → fix compile breaks from 34.x.
4. Run outdated audit → selective infra bumps → test filter.
5. Doc + RAG re-index.

Primary files: `Directory.Build.props`, `BusBuddy.WPF/BusBuddy.WPF.csproj`, `.cursor/mcp.json`, `.github/scripts/run-syncfusion-mcp.sh`, `.github/scripts/setup-syncfusion-skills.sh`, `.cursor/skills/syncfusion-wpf-busbuddy/*`, `AGENTS.md`, `Documentation/PACKAGE-MANAGEMENT.md`.

## References

- Syncfusion WPF NuGet: https://www.nuget.org/packages/Syncfusion.SfGrid.WPF
- Syncfusion WPF skills: https://help.syncfusion.com/wpf/skills/component-skills
- Constitution: `.specify/memory/constitution.md` (Syncfusion-only, hybrid, RAG-first)
