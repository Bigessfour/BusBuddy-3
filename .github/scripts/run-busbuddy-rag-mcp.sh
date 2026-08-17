#!/usr/bin/env bash
# Launch busbuddy-rag MCP from any cwd. Resolves the repo root from this script.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
exec python3 -m rag.mcp_server
