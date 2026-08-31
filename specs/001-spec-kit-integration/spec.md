# Feature Specification: Spec-Kit Integration & Bootstrap

**Feature Branch**: `feature/spec-kit-bootstrap`

**Created**: 2026-07-24

**Status**: Implemented (platform bootstrap)

**Input**: Brownfield adoption of GitHub Spec-Kit into BusBuddy-3 without overwriting existing agent canon (`AGENTS.md`, copilot-instructions).

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Spec-Kit is present in the repo (Priority: P1)

As a solo developer or coding agent, I can use Spec-Kit skills (`/speckit-*`) in Cursor so every future feature follows Constitution → Specify → Plan → Tasks → Implement.

**Why this priority**: Without bootstrap, no later Spec-Kit feature can run.

**Independent Test**: Confirm `.specify/` exists with templates, scripts, workflows, and `.specify/memory/constitution.md`; confirm `.cursor/skills/speckit-*` skills exist alongside `syncfusion-wpf-busbuddy`.

**Acceptance Scenarios**:

1. **Given** a clean clone of the branch, **When** listing `.specify/` and `.cursor/skills/`, **Then** Spec-Kit infrastructure and `speckit-*` skills are present and Syncfusion skill is unchanged.
2. **Given** existing `AGENTS.md` / `README.md` / `.gitignore`, **When** Spec-Kit init completed, **Then** those files were not silently replaced by generic Spec-Kit content.

---

### User Story 2 - Agents know where Spec-Kit lives (Priority: P1)

As an agent, I can discover Spec-Kit from `AGENTS.md` without reading a duplicate constitution.

**Why this priority**: Agents already start at `AGENTS.md`.

**Independent Test**: `AGENTS.md` Primary standards includes a Spec-Kit bullet pointing at `.specify/memory/constitution.md` and `/speckit-*`.

**Acceptance Scenarios**:

1. **Given** `AGENTS.md`, **When** reading Primary standards, **Then** Spec-Kit pointer is present and does not paste the full constitution.

---

### User Story 3 - Upgrade safety is documented (Priority: P2)

As a maintainer, I know not to run `specify init --here --force` without backing up the constitution.

**Why this priority**: Force re-init overwrites constitution.

**Independent Test**: Spec and constitution both document the backup rule.

**Acceptance Scenarios**:

1. **Given** upgrade docs in this spec and constitution, **When** a maintainer plans a Spec-Kit upgrade, **Then** they see an explicit backup requirement for `.specify/memory/constitution.md`.

## Edge Cases

- Init in a non-empty repo may warn about merges; use `--force` only with backups.
- Community brownfield.bootstrap is deferred (rewrites `AGENTS.md`).

## Requirements _(mandatory)_

### Functional Requirements

- **FR-001**: Repository MUST contain `.specify/` shared infrastructure (templates, scripts, workflows, integration manifests).
- **FR-002**: Cursor integration MUST install `speckit-*` skills under `.cursor/skills/`.
- **FR-003**: Existing Syncfusion skill MUST remain intact.
- **FR-004**: `AGENTS.md` MUST point to Spec-Kit without duplicating the constitution.
- **FR-005**: Upgrade caution for constitution overwrite MUST be documented.

### Non-Functional / Constraints

- Do not install community brownfield.bootstrap in this feature.
- Do not change RAG indexer behavior here (owned by feature 002).
- Do not change app AI providers here (owned by feature 004).

## Success Criteria _(mandatory)_

- Spec-Kit skills usable in Cursor for subsequent features.
- Constitution file present and BusBuddy-specific (see feature 003).
- No regression to hybrid Mac/Windows or CI docs from init overwrites.

## Layout Reference

```
.specify/
  memory/constitution.md
  templates/
  scripts/bash/
  workflows/
  integrations/
.cursor/skills/speckit-*/
specs/001-spec-kit-integration/
```
