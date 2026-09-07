#!/usr/bin/env bash
# Launch Google's gcloud MCP for BusBuddy (quota project busbuddy-507301).
# Deny list blocks key-string and other secret/destructive gcloud commands.
# Docs: https://github.com/googleapis/gcloud-mcp
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONFIG="${ROOT}/.github/scripts/gcloud-mcp-deny.json"

export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:${HOME}/.local/bin:${PATH:-}"
export CLOUDSDK_CORE_PROJECT="${CLOUDSDK_CORE_PROJECT:-busbuddy-507301}"
export CLOUDSDK_CORE_DISABLE_PROMPTS=1

if ! command -v gcloud >/dev/null 2>&1; then
  echo "gcloud MCP: gcloud CLI not found. Install: brew install --cask google-cloud-sdk" >&2
  exit 1
fi

if [[ ! -f "$CONFIG" ]]; then
  echo "gcloud MCP: missing deny list $CONFIG" >&2
  exit 1
fi

if ! command -v npx >/dev/null 2>&1; then
  echo "gcloud MCP: npx not found (need Node.js 20+)." >&2
  exit 1
fi

exec npx -y @google-cloud/gcloud-mcp -c "$CONFIG"
