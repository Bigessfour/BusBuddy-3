#!/usr/bin/env python3
"""
BusBuddy RAG MCP Server (stdio)

Exposes semantic search over the entire indexed BusBuddy codebase as an MCP tool.

This gives any MCP-capable agent (Grok, Copilot, Cursor, Claude Desktop, etc.)
instant, high-quality project context.

Add to your `.cursor/mcp.json` (Cursor) or project MCP config:
  "busbuddy-rag": {
    "command": "python",
    "args": ["-m", "rag.mcp_server"],
    "env": {}
  }

The agent MUST be instructed (see copilot-instructions.md) to call this tool
BEFORE proposing any code change.

Protocol: basic JSON-RPC 2.0 stdio (compatible with MCP).
"""

import sys
import json
from pathlib import Path
from typing import Any, Dict

import chromadb
from sentence_transformers import SentenceTransformer

DB_PATH = Path(__file__).parent / "chroma_db"
COLLECTION_NAME = "busbuddy_codebase"
EMBED_MODEL = "all-MiniLM-L6-v2"

# Lazy load on first use
_model = None
_collection = None

def get_collection():
    global _model, _collection
    if _collection is None:
        print("[busbuddy-rag] Loading embedding model and Chroma collection...", file=sys.stderr)
        _model = SentenceTransformer(EMBED_MODEL)
        client = chromadb.PersistentClient(path=str(DB_PATH))
        _collection = client.get_collection(COLLECTION_NAME)
        print(f"[busbuddy-rag] Ready with {_collection.count()} chunks.", file=sys.stderr)
    return _model, _collection

def search_repo_context(query: str, top_k: int = 8) -> str:
    """Core search. Returns nicely formatted context for the agent."""
    model, collection = get_collection()
    q_emb = model.encode([query]).tolist()

    res = collection.query(
        query_embeddings=q_emb,
        n_results=top_k,
        include=["documents", "metadatas", "distances"]
    )

    if not res["documents"] or not res["documents"][0]:
        return "No relevant context found in the BusBuddy codebase for that query."

    lines = [
        f"## BusBuddy RAG Context for query: {query}",
        f"Retrieved {len(res['documents'][0])} most relevant chunks (semantic search).",
        "Use this as authoritative project context before any code change.\n"
    ]

    for i, (doc, meta, dist) in enumerate(zip(
        res["documents"][0], res["metadatas"][0], res["distances"][0]
    ), 1):
        file = meta.get("file", "unknown")
        start = meta.get("start_line", "?")
        end = meta.get("end_line", "?")
        lang = meta.get("language", "text")
        score = round(1 - dist, 3)
        snippet = doc.strip()[:900]
        lines.append(
            f"### Result {i} | {file}:{start}-{end} | lang={lang} | similarity={score}\n"
            f"```\n{snippet}\n```\n"
        )

    lines.append(
        "\n**AGENT INSTRUCTION**: Reference the specific files/lines above in your reasoning. "
        "Quote relevant snippets when justifying changes. If context is insufficient, call this tool again with a more precise query."
    )
    return "\n".join(lines)

def handle_request(req: Dict[str, Any]) -> Dict[str, Any]:
    method = req.get("method")
    req_id = req.get("id")
    params = req.get("params") or {}

    if method == "initialize":
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "protocolVersion": "2024-11-05",
                "capabilities": {"tools": {}},
                "serverInfo": {"name": "busbuddy-rag", "version": "1.0.0"}
            }
        }

    if method == "tools/list":
        tools = [{
            "name": "search_repo_context",
            "description": (
                "Semantic RAG search over the FULL BusBuddy codebase (C# services, models, "
                "WPF views, Docker/Postgres config, docs, tests, Grok integration, etc.). "
                "Returns the most relevant code chunks + documentation with file:line references. "
                "**CRITICAL RULE**: You MUST call this tool with a good query describing the "
                "intended change BEFORE suggesting ANY edit, new feature, refactor, or architecture change. "
                "This gives you complete project context and prevents hallucinations or regressions from previous large refactors."
            ),
            "inputSchema": {
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "Precise description of the task or area you need context for (e.g. 'Postgres DbContext configuration and how SeedDataService uses it', 'GrokGlobalAPI route optimization flow', 'how the hybrid Docker + UTM VM setup works')."
                    },
                    "top_k": {
                        "type": "integer",
                        "default": 8,
                        "description": "How many chunks to retrieve (5-12 recommended)"
                    }
                },
                "required": ["query"]
            }
        }]
        return {"jsonrpc": "2.0", "id": req_id, "result": {"tools": tools}}

    if method == "tools/call":
        name = params.get("name")
        args = params.get("arguments") or {}
        if name == "search_repo_context":
            query = args.get("query", "")
            top_k = int(args.get("top_k", 8))
            text = search_repo_context(query, top_k)
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {
                    "content": [{"type": "text", "text": text}]
                }
            }
        return {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": f"Unknown tool: {name}"}}

    if method == "notifications/initialized":
        return None  # no response needed

    return {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": f"Method not found: {method}"}}

def main():
    # MCP stdio loop
    while True:
        try:
            line = sys.stdin.readline()
            if not line:
                break
            line = line.strip()
            if not line:
                continue
            req = json.loads(line)
            resp = handle_request(req)
            if resp is not None:
                print(json.dumps(resp), flush=True)
        except Exception as e:
            # Never crash the server on bad input
            err = {"jsonrpc": "2.0", "id": None, "error": {"code": -32603, "message": str(e)}}
            print(json.dumps(err), flush=True)

if __name__ == "__main__":
    import json  # local import so script is self-contained
    main()
