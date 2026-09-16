# Feature Specification: Local LLM Provider (Ollama)

**Feature Branch**: `feature/spec-kit-bootstrap`

**Created**: 2026-07-24

**Updated**: 2026-09-15

**Status**: Implemented

**Input**: App AI is local Ollama only. Cloud xAI / Grok is not a supported provider.

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Chat works offline via Ollama (Priority: P1)

As a Windows desktop operator, BusBuddy chat uses local Ollama when it is running. No cloud API key is required.

**Why this priority**: Primary offline AI path for the desktop app.

**Independent Test**: With Ollama running and model pulled, `IAiChatService.GetResponseAsync` returns model text; DI resolves `OllamaChatService`.

**Acceptance Scenarios**:

1. **Given** Ollama is reachable at `Ollama:BaseUrl`, **When** chat is invoked, **Then** a local model response is returned.
2. **Given** Ollama is not running, **When** chat is invoked, **Then** a clear offline message is returned, the app does not crash, and Serilog records a **Warning** (not an Error / actionable runtime failure).

---

### User Story 2 - Route optimization uses local Ollama (Priority: P1)

As an operator, route optimization commentary uses the local OpenAI-compatible Ollama base URL.

**Why this priority**: Same provider for chat and optimization.

**Independent Test**: `OllamaAiService` logs the Ollama endpoint without an API key; on connection failure falls back to mock optimization and logs Warning.

**Acceptance Scenarios**:

1. **Given** `Ollama:Enabled` is true, **When** `OllamaAiService` is constructed, **Then** it is configured without any cloud key.
2. **Given** Ollama is down, **When** `OptimizeRoutesAsync` runs, **Then** mock optimization is returned and no Error-level log is written for the refused connection.

---

### User Story 3 - Configuration via options pattern (Priority: P2)

As a developer, I configure Ollama URLs and model under the `Ollama` appsettings section.

**Why this priority**: One options system, no leftover xAI section.

**Independent Test**: `OllamaOptions` exposes BaseUrl, NativeBaseUrl, Model, TimeoutSeconds, MaxTokens, Temperature, Enabled.

## Requirements _(mandatory)_

- **FR-001**: `IAiChatService` is the chat contract; `OllamaChatService` implements it.
- **FR-002**: There is no cloud xAI provider path in live code.
- **FR-003**: DI registers chat + `OllamaAiService` with separate `HttpClient` instances.
- **FR-004**: Missing Ollama yields Serilog **Warning** + graceful user-facing / mock fallback. Do not log this as an actionable Error.
- **FR-005**: No `XAI_API_KEY` / `GROK_API_KEY` is loaded or required.

## Success Criteria _(mandatory)_

- App builds with Ollama registrations.
- No hard dependency on cloud xAI.
- Connection refused to `localhost:11434` does not appear as ACTIONABLE ERROR.
