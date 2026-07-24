# Feature Specification: Hybrid Development Environment & Agent Guardrails

**Feature Branch**: `feature/spec-kit-bootstrap`

**Created**: 2026-07-24

**Status**: Implemented

**Input**: Codify Mac (Core/Docker/RAG) + Windows VM (full WPF + Syncfusion) workflow and rules agents must follow so they never invent cloud hosts or break hybrid builds.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Agents respect the Mac/Windows split (Priority: P1)

As an agent, I treat WPF UI as Windows-only and use `EnableWindowsTargeting` for Mac-side builds, without proposing AWS/cloud runtime hosting for BusBuddy.

**Why this priority**: Incorrect host assumptions waste cycles and break CI.

**Independent Test**: Constitution § V + this spec + `AGENTS.md` hybrid checklist are RAG-retrievable and consistent with `DEVELOPMENT-GUIDE.md`.

**Acceptance Scenarios**:

1. **Given** a WPF UI change request, **When** an agent plans work, **Then** it assumes Windows VM execution, not native macOS WPF.
2. **Given** a Mac build, **When** restore/build/test is suggested, **Then** `-p:EnableWindowsTargeting=true` is included.
3. **Given** any deployment suggestion, **When** evaluated against constitution, **Then** AWS/cloud app hosting is refused.

---

### User Story 2 - Secrets loading is platform-correct (Priority: P1)

As an operator, secrets load from macOS Passwords on Mac and from env / shared `keys/` on Windows.

**Why this priority**: Wrong secret path breaks Syncfusion/GEE/local AI setup.

**Independent Test**: Checklist points to `LoadApiKeysFromMacPasswords()` and Windows env guidance in `AGENTS.md` / GCP auth doc.

**Acceptance Scenarios**:

1. **Given** Mac host, **When** documenting secrets, **Then** Passwords (Name = env var) is the path.
2. **Given** Windows VM/production, **When** documenting secrets, **Then** machine/user env or shared keys is the path (no Keychain).

---

### User Story 3 - Postgres from VM is documented (Priority: P2)

As a Windows VM developer, I connect to Docker Postgres on the Mac host IP printed by `./run-wpf.sh`.

**Why this priority**: Common hybrid failure mode.

**Independent Test**: Checklist references `run-wpf.sh` / `ipconfig getifaddr en0` and docker compose db profile.

## Requirements *(mandatory)*

- **FR-001**: Agent-facing hybrid checklist exists in `AGENTS.md` (thin) and durable rules remain in constitution.
- **FR-002**: `DEVELOPMENT-GUIDE.md` hybrid section remains the detailed operator guide; this feature must not create a third competing standards doc.
- **FR-003**: Agents MUST NOT invent cloud/AWS/non-Windows WPF hosts for BusBuddy runtime.
- **FR-004**: Spec-Kit / RAG must surface these rules (constitution + this spec).

## Agent checklist (canonical short form)

| Host       | Do                                                                         | Do not                           |
| ---------- | -------------------------------------------------------------------------- | -------------------------------- |
| Mac        | Core, Docker Postgres, RAG, Passwords, build with `EnableWindowsTargeting` | Claim WPF runs natively on macOS |
| Windows VM | Full Syncfusion WPF run/debug; shared folder; env/keys secrets             | Assume Keychain/Passwords API    |
| Either     | Local Ollama for AI; solo CI via PR gates                                  | Propose AWS/cloud app hosting    |

## Success Criteria *(mandatory)*

- Checklist is visible from `AGENTS.md`.
- Constitution and this spec agree; no conflicting "deploy to AWS" language introduced.
