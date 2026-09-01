# Tasks: Syncfusion Tool Integration (006)

## Phase 1 — Tracking & MCP

- [x] Bootstrap `docs/action-items.md` due-outs tracker
- [x] Fix `.cursor/mcp.json` paths (Syncfusion MCP + filesystem + RAG cwd → Box workspace)
- [x] Smoke: Cursor reload MCP / `run-syncfusion-mcp.sh` with key (manual)

## Phase 2 — Skills

- [x] Update `.cursor/skills/syncfusion-wpf-busbuddy/SKILL.md` for 34.x pin
- [x] Note vendor refresh: `.github/scripts/setup-syncfusion-skills.sh` / `npx skills update`

## Phase 3 — Syncfusion NuGet

- [x] Set `SyncfusionVersion` → `34.1.32` in `Directory.Build.props`
- [x] `dotnet restore` + `dotnet build -c Release -p:EnableWindowsTargeting=true` (0 errors)
- [x] No 34.x compile breaks on Mac cross-build

## Phase 4 — Core infra audit

- [x] Write `deps-audit.md` from outdated list
- [x] Apply safe bumps; defer AutoMapper major / EF10 / Extensions10
- [x] Re-build after bumps (0 errors; AutoMapper NU1903 remains until P2)

## Phase 5 — Docs

- [x] Update `AGENTS.md` (due-outs pointer + Syncfusion pin)
- [x] Update `Documentation/PACKAGE-MANAGEMENT.md` Syncfusion version section
- [x] Update `docs/action-items.md` checkboxes
- [x] `python -m rag.index` after merge (operator) — done 2026-07-24
- [x] Windows VM license + UI smoke — see `windows-vm-smoke.md`

## Done when

Spec FR-001–FR-009 acceptance scenarios met; build green; action-items 006 rows updated; VM smoke + RAG re-index after merge.

## Phase 6: Convergence

- [x] T001 Replace leftover `30.1.40` examples in `Documentation/PACKAGE-MANAGEMENT.md` with `$(SyncfusionVersion)` / `34.1.32` and re-index RAG per FR-008 / US5/AC1 (partial)
- [x] T002 Make `.cursor/mcp.json` Syncfusion MCP + RAG launchers script-relative (no hard-coded home path) per FR-002 / US1/AC2 (partial)
