# BusBuddy Local RAG + MCP Tool

This directory provides **full-project semantic context** for AI agents via RAG.

## Why this exists
The BusBuddy repo went through many iterations (first programming project). Even after aggressive archiving of legacy/Phase/debug code, an agent can lose context across sessions or large refactors.

**Rule**: Before any code change, the agent **must** retrieve fresh, relevant chunks from the entire current baseline using this tool.

## Components

- `index.py` — One-time (or after big changes) indexer. Walks the repo (excludes Archive/, bin/, obj/, etc.), creates structure-preserving chunks, embeds with `all-MiniLM-L6-v2` (local), stores in persistent ChromaDB.
- `query.py` — Simple CLI for manual validation: `python -m rag.query "your question here"`
- `mcp_server.py` — Stdio MCP server exposing `search_repo_context`. This is the tool agents call.
- `chroma_db/` — The actual vector store (generated, gitignored).

## Setup (done once)

```bash
# 1. (Re)build the index after significant changes
python -m rag.index

# 2. The MCP server is registered in .cursor/mcp.json as "busbuddy-rag" (Cursor project MCP config)
```

## For Agents (Copilot, Grok, Claude, etc.)

See the top of `.github/copilot-instructions.md` — the **CRITICAL RAG RULE** is now the very first thing.

When the MCP host loads `.cursor/mcp.json`, it will discover the `busbuddy-rag` tool automatically.

Example tool call an agent should make:

search_repo_context with query is "how the current Postgres + BUSBUDDY_CONNECTION setup works in docker-compose and the DbContext" top_k is 8

Always quote the best results when explaining your change.

## Re-indexing

Run `python -m rag.index` whenever:
- Major features are added
- Large refactors or hygiene passes complete
- **Auth, CI/CD, GCP/Maps, or agent docs change** (`AGENTS.md`, `Documentation/GCP-GEE-SECRETS-AND-AUTH.md`, `README.md`)
- **Spec-Kit artifacts change** (`.specify/memory/constitution.md`, anything under `specs/`, Spec-Kit templates that agents must follow)
- You want the absolute latest baseline for the agent

Always-included files for RAG (see `ALWAYS_INCLUDE` in `index.py` — basenames **or** repo-relative paths):
- `README.md`, `AGENTS.md`, `STEADY-STATE-AND-FINISH-ROADMAP.md`, `DEVELOPMENT-GUIDE.md`
- `Documentation/GCP-GEE-SECRETS-AND-AUTH.md`
- `.github/copilot-instructions.md`, `.cursor/mcp.json`
- `.specify/memory/constitution.md`

Also indexed via extensions (not ignored): `specs/**/*.md`, other `.specify/**/*.md` templates/docs.

Example queries for Maps / Spec-Kit context:

```
search_repo_context query="Google Maps Platform GOOGLE_MAPS_API_KEY Address Validation" top_k=8
search_repo_context query="solo developer CI auto-merge workflow gates" top_k=6
search_repo_context query="BusBuddy constitution Syncfusion Serilog RAG Spec-Kit" top_k=8
```

The index is fast enough for a repo of this size (~480 files → ~3k chunks).

## Benefits for Portfolio / Cloud Resume Challenge

- Agents now have reliable, up-to-date, full-project context instead of stale or partial memory.
- Encourages clean, context-aware changes.
- The RAG index itself becomes a nice artifact showing "I built tooling to keep AI grounded in a real codebase."

This + the existing Docker/Postgres testing story + clean baseline after legacy removal makes the repo much stronger for the challenge.
