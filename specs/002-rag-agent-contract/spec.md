# Feature Specification: RAG Formalization & Agent Contract

**Feature Branch**: `feature/spec-kit-bootstrap`

**Created**: 2026-07-24

**Status**: Implemented

**Input**: Elevate existing `rag/` + `busbuddy-rag` MCP from optional tooling to a constitutional agent contract, and ensure Spec-Kit artifacts are always indexed.

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Agents must retrieve before changing (Priority: P1)

As a coding agent, before architectural, auth, CI, or cross-cutting work I must call `search_repo_context` and cite `file:line` results.

**Why this priority**: Prevents hallucinations against a long-iterated brownfield repo.

**Independent Test**: `AGENTS.md`, constitution, and `.github/copilot-instructions.md` all state the mandatory RAG rule; MCP tool `busbuddy-rag` / `search_repo_context` remains available.

**Acceptance Scenarios**:

1. **Given** an architectural change request, **When** an agent starts work, **Then** it invokes `search_repo_context` and cites chunks before editing.
2. **Given** Spec-Kit / constitution updates, **When** `python -m rag.index` is run, **Then** `.specify/memory/constitution.md` and `specs/**/*.md` appear in the indexable set.

---

### User Story 2 - Always-include pins work by path (Priority: P1)

As a maintainer, `ALWAYS_INCLUDE` entries with relative paths (e.g. `Documentation/GCP-GEE-SECRETS-AND-AUTH.md`, `.specify/memory/constitution.md`) correctly pin those files.

**Why this priority**: Basename-only matching made path-prefixed entries ineffective for non-extension edge cases and unclear for operators.

**Independent Test**: Unit-style check or dry-run collection includes constitution path; `is_always_include` matches both basename and relative path.

**Acceptance Scenarios**:

1. **Given** `ALWAYS_INCLUDE` contains `.specify/memory/constitution.md`, **When** collecting files, **Then** that file is included.
2. **Given** `ALWAYS_INCLUDE` contains `Documentation/GCP-GEE-SECRETS-AND-AUTH.md`, **When** matching, **Then** relative-path match succeeds (not only basename).

---

### User Story 3 - Operators know when to re-index (Priority: P2)

As a developer, I know to re-run `python -m rag.index` after Spec-Kit, auth, CI, or architecture doc changes.

**Why this priority**: Stale vector store breaks the closed loop.

**Independent Test**: `rag/README.md` lists Spec-Kit / constitution / `specs/` as re-index triggers.

## Requirements _(mandatory)_

### Functional Requirements

- **FR-001**: Agents MUST use `busbuddy-rag` → `search_repo_context` before architectural/auth/CI/cross-cutting changes.
- **FR-002**: Indexer MUST support relative-path entries in `ALWAYS_INCLUDE`.
- **FR-003**: Constitution path MUST be listed in `ALWAYS_INCLUDE`.
- **FR-004**: `.specify/` and `specs/` MUST NOT be in `IGNORE_DIRS` (markdown under them indexes via extensions).
- **FR-005**: `rag/README.md` and copilot-instructions MUST mention Spec-Kit artifacts as RAG sources / re-index triggers.

## Success Criteria _(mandatory)_

- Constitution and feature specs are RAG-retrievable after re-index.
- Path-aware `ALWAYS_INCLUDE` matching is implemented and documented.
