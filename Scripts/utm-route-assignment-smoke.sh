#!/bin/zsh
# utm-route-assignment-smoke.sh
# Mac → UTM: sync, close WPF, Debug rebuild, testhost poke, interactive UIA mouse, log score.
#
# Usage:
#   ./Scripts/utm-route-assignment-smoke.sh
#   ./Scripts/utm-route-assignment-smoke.sh --no-sync
#   ./Scripts/utm-route-assignment-smoke.sh --testhost-only

emulate -L zsh
set -u
setopt pipefail

typeset -r SCRIPT_DIR="${0:A:h}"
typeset -r REPO_ROOT="${SCRIPT_DIR:h}"
typeset -r BRIDGE="${SCRIPT_DIR}/utm-dev-bridge.sh"
typeset -r FILTER='FullyQualifiedName~RouteAssignmentViewTests|FullyQualifiedName~RouteAssignmentToolbarSmokeTests'

DO_SYNC=1
TESTHOST_ONLY=0

while (( $# )); do
  case "$1" in
    -h|--help)
      print -r -- "Usage: $0 [--no-sync] [--testhost-only]"
      exit 0
      ;;
    --no-sync) DO_SYNC=0; shift ;;
    --testhost-only) TESTHOST_ONLY=1; shift ;;
    *) print -r -- "Unknown: $1" >&2; exit 2 ;;
  esac
done

[[ -f "${BRIDGE}" ]] || { print -r -- "missing ${BRIDGE}" >&2; exit 1; }

# shellcheck disable=SC1091
source "${SCRIPT_DIR}/utm-dev-bridge.inc"
load_config
init_ssh_opts
ensure_connected 300 || exit 1
win_root="$(posix_to_win_path)"
win_root_ps=${win_root:gs/\'/\'\'/}
ok "guest ${CURRENT_HOST}"

if (( DO_SYNC )); then
  info "sync Mac → guest"
  "${BRIDGE}" sync || { err "sync failed"; exit 1; }
fi

bridge_exec() {
  "${BRIDGE}" exec -- "$@"
}

info "close + rebuild Debug WPF on guest"
bridge_exec "
\$ErrorActionPreference = 'Continue'
Set-Location '${win_root_ps}'
Get-Process -Name 'BusBuddy.WPF' -ErrorAction SilentlyContinue | ForEach-Object {
  Write-Output ('STOP pid=' + \$_.Id)
  Stop-Process -Id \$_.Id -Force
}
Start-Sleep -Seconds 2
& dotnet build 'BusBuddy.WPF\\BusBuddy.WPF.csproj' -c Debug -p:EnableWindowsTargeting=true --nologo
Write-Output ('BUILD_EXIT=' + \$LASTEXITCODE)
if (\$LASTEXITCODE -ne 0) { exit \$LASTEXITCODE }
\$exe = '${win_root_ps}\\BusBuddy.WPF\\bin\\Debug\\net9.0-windows\\BusBuddy.WPF.exe'
Write-Output ('EXE_EXISTS=' + (Test-Path \$exe))
" || { err "guest rebuild failed"; exit 1; }

info "testhost: ${FILTER}"
"${BRIDGE}" test --no-sync --filter "${FILTER}" || { err "testhost failed"; exit 1; }

if (( TESTHOST_ONLY )); then
  ok "testhost-only complete"
  exit 0
fi

info "launch WPF in interactive session"
"${BRIDGE}" launch --no-sync || { err "interactive launch failed"; exit 1; }

info "UIA mouse smoke (schtasks /IT)"
bridge_exec "
\$ErrorActionPreference = 'Continue'
\$script = '${win_root_ps}\\Scripts\\utm-route-assignment-mouse.ps1'
\$wrapper = \"\$env:TEMP\\busbuddy-route-uia-run.ps1\"
@\"\`
\$ErrorActionPreference = 'Continue'
& '\$script'
\"@ | Set-Content -Path \$wrapper -Encoding UTF8
schtasks /Delete /TN BusBuddyRouteUia /F 2>\$null | Out-Null
schtasks /Create /TN BusBuddyRouteUia /SC ONCE /ST 23:59 /RL LIMITED /IT /F /TR \"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \`\"\$wrapper\`\"\" /RU ${SSH_USER} | Out-Null
schtasks /Run /TN BusBuddyRouteUia | Out-Null
Start-Sleep -Seconds 45
Get-ChildItem \"\$env:TEMP\\busbuddy-uia-route-*.log\" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1 | ForEach-Object { Get-Content \$_.FullName }
if (-not (Get-ChildItem \"\$env:TEMP\\busbuddy-uia-route-*.log\" -ErrorAction SilentlyContinue)) { Write-Output 'UIA_OUT_MISSING' }
" || { err "UIA mouse pass failed"; exit 1; }

info "score logs"
bridge_exec "
\$ErrorActionPreference = 'Continue'
\$since = (Get-Date).AddMinutes(-30).ToString('yyyy-MM-dd HH:mm:ss')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File '${win_root_ps}\\Scripts\\score-route-assignment-logs.ps1' -Root '${win_root_ps}' -Since \$since
Write-Output ('SCORE_EXIT=' + \$LASTEXITCODE)
" || { err "log score failed"; exit 1; }

ok "route assignment smoke complete"
