# Implementation Plan: Syncfusion Tool Integration (006)

**Branch**: `feature/006-syncfusion-tool-integration`
**Spec**: [spec.md](./spec.md)
**Due-outs**: [docs/action-items.md](../../docs/action-items.md)

## Technical approach

1. **MCP** — Point `.cursor/mcp.json` at the active clone (Box path) and prefer repo-relative launchers where Cursor resolves cwd to the project root. Keep `run-syncfusion-mcp.sh` loading `Syncfusion_API_Key` from Passwords.
2. **Skills** — Document refresh via `setup-syncfusion-skills.sh` / `npx skills update`; update BusBuddy overlay for Syncfusion **34.x**.
3. **NuGet** — Set `SyncfusionVersion` to **34.1.32** (nuget.org latest for SfGrid.WPF at implement time); all PackageReferences already use `$(SyncfusionVersion)`.
4. **Deps audit** — `dotnet list package --outdated` → `deps-audit.md`; bump safe patch/minor aligned to net9; defer majors with rationale (e.g. AutoMapper).
5. **Docs** — `AGENTS.md`, `PACKAGE-MANAGEMENT.md` Syncfusion pin, action-items checkboxes; RAG re-index after merge.

## Constraints

- Constitution: Syncfusion-only UI, Serilog-only, hybrid Mac/Windows, RAG-first.
- Mac gate: build with `-p:EnableWindowsTargeting=true`.
- Do not commit `.agents/skills/` or secrets.

## Risks

- Syncfusion 34.x XAML/API breaks → fix compile errors in-repo.
- License key must cover 34.x Essential Studio line.
- AutoMapper NU1903 — document; prefer upgrade path if available without large rewrite.
