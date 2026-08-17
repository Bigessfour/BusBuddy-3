#!/usr/bin/env bash
# Launches Syncfusion WPF MCP (NuGet Syncfusion.WPF.MCP) with API key from macOS Passwords / Keychain.
# Docs: https://help.syncfusion.com/wpf/mcp
# Never echo the key. No secrets in the repo.
set -euo pipefail

export DOTNET_ROOT="${DOTNET_ROOT:-${HOME}/.dotnet}"
export PATH="${DOTNET_ROOT}:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:${PATH:-}"

if [[ -z "${Syncfusion_API_Key:-}" ]]; then
  for svc in SYNCFUSION_API_KEY Syncfusion_API_Key com.bigessfour.cloudresume.syncfusion-mcp com.wileyco.syncfusion.blazor-mcp; do
    raw="$(security find-generic-password -s "$svc" -w 2>/dev/null || true)"
    if [[ -n "$raw" ]]; then
      Syncfusion_API_Key="$(printf '%s' "$raw" | tr -d '\r\n')"
      export Syncfusion_API_Key
      export SYNCFUSION_API_KEY="$Syncfusion_API_Key"
      break
    fi
  done
fi

if [[ -z "${Syncfusion_API_Key:-}" && -n "${SYNCFUSION_API_KEY:-}" ]]; then
  export Syncfusion_API_Key="${SYNCFUSION_API_KEY}"
fi

if [[ -z "${Syncfusion_API_Key:-}" ]]; then
  echo "Syncfusion WPF MCP: set Passwords entry Name=SYNCFUSION_API_KEY (or Syncfusion_API_Key)." >&2
  exit 1
fi

# dnx needs SDK 10; leave the repo so global.json (net9) does not apply.
if command -v dnx >/dev/null 2>&1; then
  cd "$HOME"
  exec dnx Syncfusion.WPF.MCP --yes
fi

echo "Syncfusion WPF MCP: dnx not found (need .NET 10 SDK). Falling back to deprecated npm package." >&2
exec npx -y @syncfusion/wpf-assistant@latest "$@"
