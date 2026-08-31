# Feature Specification: Project Constitution

**Feature Branch**: `feature/spec-kit-bootstrap`

**Created**: 2026-07-24

**Status**: Implemented

**Input**: Hand-author BusBuddy architectural DNA into Spec-Kit memory so all later specs inherit Syncfusion, Serilog, RAG-first, hybrid Mac/Windows, solo CI, and GCP rules.

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Single source of architectural truth (Priority: P1)

As an agent or developer, I consult `.specify/memory/constitution.md` for non-negotiable project rules before specifying or implementing features.

**Why this priority**: Without a real constitution, Spec-Kit remains generic.

**Independent Test**: Open `.specify/memory/constitution.md` and verify principles cover Syncfusion-only UI, Serilog-only logging, RAG-first, hybrid Windows WPF, solo CI, no AWS/cloud hosting for runtime, and GCP project map.

**Acceptance Scenarios**:

1. **Given** the constitution file, **When** reviewing Core Principles, **Then** Syncfusion, Serilog, RAG-first, layered architecture, hybrid dev, and solo CI are present.
2. **Given** the constitution file, **When** reviewing GCP map, **Then** `ee-bigessfour` and `new-coursera-490518` are listed and `busbuddy-465000` is marked invalid.

---

### User Story 2 - Agents are pointed, not duplicated (Priority: P1)

As an agent starting from `AGENTS.md`, I am directed to the constitution without reading a second full copy of the same rules.

**Why this priority**: Drift between duplicated canons is the failure mode.

**Independent Test**: `AGENTS.md` links to `.specify/memory/constitution.md`; constitution remains the detailed DNA.

**Acceptance Scenarios**:

1. **Given** `AGENTS.md` Primary standards, **When** following the Spec-Kit link, **Then** the constitution file opens / is the linked path.

---

### User Story 3 - Amendments are governed (Priority: P2)

As a maintainer, I amend the constitution only via explicit PR with version/date update and RAG re-index.

**Why this priority**: Prevents silent constitution drift.

**Independent Test**: Governance section requires PR + Last Amended + re-index.

**Acceptance Scenarios**:

1. **Given** a proposed constitution change, **When** following Governance, **Then** PR + version metadata + `python -m rag.index` are required.

## Requirements _(mandatory)_

### Functional Requirements

- **FR-001**: `.specify/memory/constitution.md` MUST replace the stock Spec-Kit placeholder with BusBuddy rules.
- **FR-002**: Constitution MUST mandate RAG-first retrieval before architectural/cross-cutting work.
- **FR-003**: Constitution MUST forbid inventing AWS/cloud hosting for BusBuddy runtime.
- **FR-004**: Constitution MUST document Spec-Kit force-init backup caution.
- **FR-005**: Version metadata MUST be present (Version / Ratified / Last Amended).

### Source seeding (non-exhaustive)

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `STEADY-STATE-AND-FINISH-ROADMAP.md` (Architecture Map)
- `Documentation/GCP-GEE-SECRETS-AND-AUTH.md`

## Success Criteria _(mandatory)_

- Constitution is BusBuddy-specific and RAG-indexable.
- Later features 002–005 can inherit these rules without restating them in full.
