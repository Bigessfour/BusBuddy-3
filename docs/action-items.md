# BusBuddy Action Items (Due-Outs Tracker)

**Canonical due-outs file for agents and humans.**
**Historical finish narrative:** [STEADY-STATE-AND-FINISH-ROADMAP.md](../STEADY-STATE-AND-FINISH-ROADMAP.md)
**Spec-Kit features:** [specs/](../specs/) (each feature may also have `tasks.md`)
**Generated inventory (optional):** run function-inventory scanner → `docs/function-inventory.generated.md`
**Visual tree (optional):** [function-tree.md](./function-tree.md)

**Update rule:** When starting or finishing work, check boxes here and link the PR/spec. Prefer this file for “what’s left”; use Spec-Kit `tasks.md` for implementation steps inside a feature.

---

## Priorities (now)

### P0 — Platform / tooling in flight

- [x] Spec-Kit brownfield bootstrap (001–005) — merged [PR #20](https://github.com/Bigessfour/BusBuddy-3/pull/20)
- [ ] **006 Syncfusion Tool Integration** — [spec](../specs/006-syncfusion-tool-integration/spec.md) (branch `feature/006-syncfusion-tool-integration`)
  - [x] Fix Syncfusion WPF MCP paths (`.cursor/mcp.json` → Box workspace path)
  - [x] Refresh WPF skills overlay notes for 34.x (run `setup-syncfusion-skills.sh` locally after clone)
  - [x] Bump `SyncfusionVersion` **33.2.10 → 34.1.32** (build green on Mac with EnableWindowsTargeting)
  - [x] Core infra deps audit → [deps-audit.md](../specs/006-syncfusion-tool-integration/deps-audit.md) + safe bumps
  - [ ] Docs/`AGENTS.md` + `python -m rag.index` after merge
  - [ ] Windows VM: Syncfusion license + UI smoke on 34.x
  - [ ] P2 follow-up: AutoMapper 12 → ≥15.1.1 (GHSA-rvv3-g6hj-g44x) — deferred in audit
  - **Verification:** `dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true`; MCP assistant starts; Windows VM smoke for license/UI

### P1 — Finish / domain (from roadmap; Spec-Kit wave 2)

- [ ] Student import / optimize end-to-end (UI + SeedDataService + tests)
- [ ] Reports: PdfReportService + AI path fully wired in UI
- [ ] Driver availability + SfScheduler
- [ ] Maintenance UI polish
- [ ] Google Earth Engine enhancements (beyond current DI/auth)
- [ ] End-to-end student → assign → report proof test

### P2 — Hygiene / quality

- [ ] Bootstrap function-inventory generated scan (`docs/function-inventory.generated.md`)
- [ ] Resolve/exclude corrupted LFS noise on `TWN_CICD_Checklist_…pdf` if still dirty locally
- [ ] AutoMapper 12.x advisory — upgrade or document risk acceptance (in 006 audit)
- [ ] Ensure `Documentation/GCP-GEE-SECRETS-AND-AUTH.md` present or AGENTS links fixed (file was missing at Spec-Kit adopt)

---

## Spec-Kit features — status

| Spec | Title                              | Status                                                                       |
| ---- | ---------------------------------- | ---------------------------------------------------------------------------- |
| 001  | Spec-Kit Integration & Bootstrap   | Done (PR #20)                                                                |
| 002  | RAG Formalization & Agent Contract | Done (PR #20)                                                                |
| 003  | Project Constitution               | Done (PR #20)                                                                |
| 004  | Local LLM (Ollama)                 | Done (PR #20)                                                                |
| 005  | Hybrid Dev Environment Guardrails  | Done (PR #20)                                                                |
| 006  | Syncfusion Tool Integration        | **Implementing** (NuGet + MCP + audit done; VM smoke + RAG re-index pending) |

---

## AI / agent tooling

- [x] `busbuddy-rag` MCP + mandatory RAG rule
- [ ] Syncfusion WPF MCP path-correct + key documented (006)
- [ ] Syncfusion WPF skills refresh for NuGet major (006)
- [x] Spec-Kit `/speckit-*` skills committed

---

## Meta

- Re-check this file at the start of each session.
- After structural changes: update architecture map in `STEADY-STATE-AND-FINISH-ROADMAP.md` and re-index RAG.
- Do not put secrets here.

---

*Bootstrapped 2026-07-24 to give BusBuddy a single due-outs checklist (function-inventory convention).*
