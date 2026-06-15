# BusBuddy Local RAG + MCP Tool

This directory provides **full-project semantic context** for AI agents via RAG.

## Why this exists
The BusBuddy repo went through many iterations (first programming project). Even after aggressive archiving of legacy/MVP/Phase/debug code, an agent can lose context across sessions or large refactors.

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

# 2. The MCP server is already registered in mcp.json as "busbuddy-rag"
```

## For Agents (Copilot, Grok, Claude, etc.)

See the top of `.github/copilot-instructions.md` — the **CRITICAL RAG RULE** is now the very first thing.

When the MCP host loads `mcp.json`, it will discover the `busbuddy-rag` tool automatically.

Example tool call an agent should make:

search_repo_context with query is "how the current Postgres + BUSBUDDY_CONNECTION setup works in docker-compose and the DbContext" top_k is 8

Always quote the best results when explaining your change.

## Re-indexing

Run `python -m rag.index` whenever:
- Major features are added
- Large refactors or hygiene passes complete
- You want the absolute latest baseline for the agent

The index is fast enough for a repo of this size (~480 files → ~3k chunks).

## Benefits for Portfolio / Cloud Resume Challenge

- Agents now have reliable, up-to-date, full-project context instead of stale or partial memory.
- Encourages clean, context-aware changes.
- The RAG index itself becomes a nice artifact showing "I built tooling to keep AI grounded in a real codebase."

This + the existing Docker/Postgres testing story + clean baseline after legacy removal makes the repo much stronger for the challenge.
