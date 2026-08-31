# Feature Specification: Local LLM Provider (Ollama)

**Feature Branch**: `feature/spec-kit-bootstrap`

**Created**: 2026-07-24

**Status**: Implemented

**Input**: Replace cloud XAI/Grok as the default AI path with local Ollama behind existing interfaces (`IXAIChatService`, `GrokGlobalAPI` / `XaiOptions`).

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Chat works offline via Ollama (Priority: P1)

As a Windows desktop operator, BusBuddy chat uses local Ollama when it is running, without requiring `XAI_API_KEY`.

**Why this priority**: Primary offline AI path for the desktop app.

**Independent Test**: With Ollama running and model pulled, `IXAIChatService.GetResponseAsync` returns model text; DI resolves `OllamaChatService` when `XAI:Provider=Ollama`.

**Acceptance Scenarios**:

1. **Given** `XAI:Provider=Ollama` and Ollama reachable, **When** chat is invoked, **Then** a local model response is returned.
2. **Given** Ollama is not running, **When** chat is invoked, **Then** a clear offline message is returned and the app does not crash.

---

### User Story 2 - Route optimization uses local endpoint when configured (Priority: P1)

As an operator, route optimization prefers the local OpenAI-compatible Ollama base URL when Provider is Ollama.

**Why this priority**: Same provider mode for chat and optimization.

**Independent Test**: `GrokGlobalAPI` logs Ollama endpoint configuration without requiring an API key; on connection failure falls back to mock optimization.

**Acceptance Scenarios**:

1. **Given** Provider=Ollama, **When** `GrokGlobalAPI` is constructed, **Then** it is configured without `XAI_API_KEY`.
2. **Given** Ollama is down, **When** `OptimizeRoutesAsync` runs, **Then** mock optimization is returned (existing fallback).

---

### User Story 3 - Configuration via options pattern (Priority: P2)

As a developer, I configure provider mode and Ollama URLs/models under the existing `XAI` appsettings section.

**Why this priority**: Avoids a second options system.

**Independent Test**: `XaiOptions` exposes Provider, OllamaBaseUrl, OllamaNativeBaseUrl, OllamaModel; appsettings documents defaults.

## Requirements _(mandatory)_

- **FR-001**: `IXAIChatService` remains the chat contract; `OllamaChatService` implements it.
- **FR-002**: Default provider is Ollama; Disabled/Xai fall back to mock chat without cloud dependency.
- **FR-003**: DI registers chat + `GrokGlobalAPI` with shared `HttpClient`.
- **FR-004**: Missing Ollama yields Serilog warning + graceful user-facing message.
- **FR-005**: Cloud XAI key is optional when Provider=Ollama.

## Success Criteria _(mandatory)_

- App builds with new registrations.
- No hard dependency on cloud XAI for the default path.
