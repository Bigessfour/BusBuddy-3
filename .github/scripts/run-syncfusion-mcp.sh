#!/usr/bin/env bash
# Launches Syncfusion WPF MCP assistant with API key from macOS Passwords (no secrets in repo).
set -euo pipefail
if [[ -z "${Syncfusion_API_Key:-}" ]]; then
  export Syncfusion_API_Key="$(security find-generic-password -s SYNCFUSION_API_KEY -w 2>/dev/null || true)"
fi
if [[ -z "${Syncfusion_API_Key:-}" ]]; then
  export Syncfusion_API_Key="$(security find-generic-password -s Syncfusion_API_Key -w 2>/dev/null || true)"
fi
exec npx -y @syncfusion/wpf-assistant@latest
