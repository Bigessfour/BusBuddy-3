#!/bin/zsh
# utm-wpf-test.sh
# Mac → UTM Windows: sync repo (optional) and run BusBuddy.Tests under Microsoft.WindowsDesktop.App.
#
# Usage (from Mac, VM up + logged in + OpenSSH):
#   ./Scripts/utm-wpf-test.sh
#   ./Scripts/utm-wpf-test.sh --deps-only
#   ./Scripts/utm-wpf-test.sh --no-sync --filter "FullyQualifiedName~DestinationServiceTests"
#   ./Scripts/utm-wpf-test.sh --full
#
# Config: same as utm-dev-bridge (~/.config/utm-dev-bridge.env).

emulate -L zsh
set -u
setopt pipefail

typeset -r SCRIPT_DIR="${0:A:h}"
typeset -r REPO_ROOT="${SCRIPT_DIR:h}"
typeset -r BRIDGE="${SCRIPT_DIR}/utm-dev-bridge.sh"
typeset -r CONFIG_FILE="${UTM_DEV_BRIDGE_ENV:-$HOME/.config/utm-dev-bridge.env}"
typeset -r STATE_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/utm-dev-bridge"

# Default filter: Students suites called out in docs/action-items.md + Hop 1 Core proofs.
typeset -r DEFAULT_FILTER='FullyQualifiedName~StudentsListCoordinatorTests|FullyQualifiedName~StudentsViewModelTests|FullyQualifiedName~StudentFormViewModelSaveTests|FullyQualifiedName~StudentsViewTests|FullyQualifiedName~PostgresConnectionResolverTests|FullyQualifiedName~DestinationServiceTests'
typeset -r FULL_FILTER='Category!=Integration&Category!=InMemoryFlaky'

if [[ -t 1 ]]; then
  typeset -r C_RESET=$'\033[0m' C_DIM=$'\033[2m' C_BOLD=$'\033[1m'
  typeset -r C_RED=$'\033[31m' C_GRN=$'\033[32m' C_YLW=$'\033[33m' C_CYN=$'\033[36m'
else
  typeset -r C_RESET= C_DIM= C_BOLD= C_RED= C_GRN= C_YLW= C_CYN=
fi

ok()   { print -r -- "${C_GRN}●${C_RESET} $*"; }
warn() { print -r -- "${C_YLW}▲${C_RESET} $*"; }
err()  { print -r -- "${C_RED}✖${C_RESET} $*" >&2; }
info() { print -r -- "${C_CYN}→${C_RESET} $*"; }

usage() {
  cat <<'EOF'
utm-wpf-test — run BusBuddy.Tests on the UTM Windows guest (WPF testhost)

USAGE
  ./Scripts/utm-wpf-test.sh                 Sync once, then Students + Hop 1 filter
  ./Scripts/utm-wpf-test.sh --deps-only     Probe SDK / WindowsDesktop / repo only
  ./Scripts/utm-wpf-test.sh --no-sync       Skip rsync/tar (guest already current)
  ./Scripts/utm-wpf-test.sh --full          Broader CI-like filter (excl. Integration/Flaky)
  ./Scripts/utm-wpf-test.sh --filter EXPR   Custom --filter expression for dotnet test

ENVIRONMENT (also ~/.config/utm-dev-bridge.env)
  Same as utm-dev-bridge.sh (SSH_USER, SSH_KEY, VM_IP, REMOTE_DIR, …)
EOF
}

load_config() {
  if [[ -f "${CONFIG_FILE}" ]]; then
    set -a
    # shellcheck disable=SC1090
    source "${CONFIG_FILE}"
    set +a
  fi
  : "${SSH_USER:=Macbook}"
  : "${SSH_KEY:=$HOME/.ssh/busbuddy-utm}"
  : "${UTM_VM_NAME:=Windows}"
  : "${REMOTE_DIR:=/c/dev/BusBuddy-3}"
  : "${VM_IP:=}"
  # Expand ~ in SSH_KEY
  SSH_KEY="${SSH_KEY/#\~/$HOME}"
}

posix_to_win_path() {
  # /c/dev/BusBuddy-3 → C:\dev\BusBuddy-3
  local p="$1"
  if [[ "${p}" =~ ^/([a-zA-Z])/(.*)$ ]]; then
    print -r -- "${(U)match[1]}:\\${match[2]//\//\\}"
  else
    print -r -- "${p}"
  fi
}

resolve_ip() {
  if [[ -n "${VM_IP}" ]]; then
    print -r -- "${VM_IP}"
    return 0
  fi
  if command -v utmctl >/dev/null 2>&1; then
    local ip
    ip="$(utmctl ip-address "${UTM_VM_NAME}" 2>/dev/null | grep -E '^([0-9]{1,3}\.){3}[0-9]{1,3}$' | head -1 || true)"
    [[ -n "${ip}" ]] && { print -r -- "${ip}"; return 0; }
  fi
  if [[ -f "${STATE_DIR}/last-ipv4" ]]; then
    print -r -- "$(<"${STATE_DIR}/last-ipv4")"
    return 0
  fi
  return 1
}

typeset -a SSH_OPTS
init_ssh() {
  mkdir -p "${STATE_DIR}"
  SSH_OPTS=(
    -i "${SSH_KEY}"
    -o IdentitiesOnly=yes
    -o StrictHostKeyChecking=accept-new
    -o UserKnownHostsFile="${STATE_DIR}/known_hosts"
    -o ConnectTimeout=12
    -o BatchMode=yes
    -o ServerAliveInterval=15
  )
}

ssh_guest() {
  ssh "${SSH_OPTS[@]}" "${SSH_USER}@${1}" "${@:2}"
}

# Run a PowerShell script on the guest via -EncodedCommand (UTF-16LE base64).
# Avoids Windows OpenSSH eating multiline -Command strings.
ssh_ps() {
  local host="$1"
  local script="$2"
  local enc
  enc="$(print -r -- "${script}" | python3 -c 'import sys,base64; print(base64.b64encode(sys.stdin.read().encode("utf-16le")).decode())')"
  ssh_guest "${host}" powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "${enc}"
}

check_deps() {
  local host="$1"
  local win_root sln_path
  win_root="$(posix_to_win_path "${REMOTE_DIR}")"
  # PowerShell single-quoted path: double any embedded quotes.
  sln_path="${win_root}\\BusBuddy.sln"
  local sln_ps=${sln_path:gs/\'/\'\'/}

  info "Probing guest toolchain on ${SSH_USER}@${host} …"
  local out
  out="$(ssh_ps "${host}" "
\$ProgressPreference = 'SilentlyContinue'
\$ErrorActionPreference = 'Continue'
& dotnet --list-runtimes
if (Test-Path -LiteralPath '${sln_ps}') { 'SLN_OK' } else { 'SLN_MISSING' }
\$conn = [Environment]::GetEnvironmentVariable('BUSBUDDY_CONNECTION', 'Machine')
if ([string]::IsNullOrWhiteSpace(\$conn)) { \$conn = [Environment]::GetEnvironmentVariable('BUSBUDDY_CONNECTION', 'User') }
if ([string]::IsNullOrWhiteSpace(\$conn)) { \$conn = \$env:BUSBUDDY_CONNECTION }
if ([string]::IsNullOrWhiteSpace(\$conn)) { 'CONN_MISSING' } else {
  \$hostPart = (\$conn -split ';') | Where-Object { \$_ -like 'Host=*' } | Select-Object -First 1
  'CONN_OK ' + \$hostPart
}
")" || {
    err "SSH probe failed"
    return 1
  }
  print -r -- "${C_DIM}${out}${C_RESET}"

  print -r -- "${out}" | grep -q 'Microsoft.WindowsDesktop.App' || {
    err "Microsoft.WindowsDesktop.App runtime missing on guest — install .NET 9 Desktop Runtime / SDK"
    return 1
  }
  ok "WindowsDesktop.App present"

  print -r -- "${out}" | grep -q 'SLN_OK' || {
    err "${win_root}\\BusBuddy.sln missing — run ./Scripts/utm-dev-bridge.sh --sync-once (REMOTE_DIR=${REMOTE_DIR})"
    return 1
  }
  ok "Guest repo present (${win_root})"

  if print -r -- "${out}" | grep -q 'CONN_OK'; then
    ok "BUSBUDDY_CONNECTION available (User/Machine)"
  else
    warn "BUSBUDDY_CONNECTION missing — Integration tests needing Postgres will fail; run .\\utm_run_in_vm.ps1 once in the guest"
  fi
  return 0
}

run_remote_test() {
  local host="$1"
  local filter="$2"
  local win_root filter_ps script
  win_root="$(posix_to_win_path "${REMOTE_DIR}")"
  filter_ps=${filter:gs/\'/\'\'/}

  info "dotnet test on guest (${win_root})"
  info "filter: ${filter}"

  script=$(cat <<EOF
\$ProgressPreference = 'SilentlyContinue'
\$ErrorActionPreference = 'Stop'
Set-Location '${win_root}'
if (-not (Test-Path -LiteralPath 'BusBuddy.Tests\\BusBuddy.Tests.csproj')) {
  Write-Error 'BusBuddy.Tests project missing under ${win_root}'
  exit 2
}
\$userConn = [Environment]::GetEnvironmentVariable('BUSBUDDY_CONNECTION', 'User')
if ([string]::IsNullOrWhiteSpace(\$userConn)) {
  \$userConn = [Environment]::GetEnvironmentVariable('BUSBUDDY_CONNECTION', 'Machine')
}
if (-not [string]::IsNullOrWhiteSpace(\$userConn)) { \$env:BUSBUDDY_CONNECTION = \$userConn }
\$hint = \$env:BUSBUDDY_CONNECTION -replace 'Password=[^;]+', 'Password=***'
Write-Host ("BUSBUDDY_CONNECTION host hint: " + \$hint)
& dotnet test 'BusBuddy.Tests\\BusBuddy.Tests.csproj' -c Release --filter '${filter_ps}'
exit \$LASTEXITCODE
EOF
)

  ssh_ps "${host}" "${script}"
}

# --- main --------------------------------------------------------------------
DO_SYNC=1
DEPS_ONLY=0
FILTER="${DEFAULT_FILTER}"

while (( $# )); do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --deps-only) DEPS_ONLY=1; shift ;;
    --no-sync) DO_SYNC=0; shift ;;
    --full) FILTER="${FULL_FILTER}"; shift ;;
    --filter)
      shift
      [[ $# -ge 1 ]] || { err "--filter needs an expression"; exit 2; }
      FILTER="$1"
      shift
      ;;
    *) err "Unknown argument: $1"; usage; exit 2 ;;
  esac
done

load_config
init_ssh

[[ -f "${SSH_KEY}" ]] || { err "SSH key missing: ${SSH_KEY}"; exit 1; }
[[ -x "${BRIDGE}" || -f "${BRIDGE}" ]] || { err "utm-dev-bridge.sh missing at ${BRIDGE}"; exit 1; }

local_ip=
local_ip="$(resolve_ip)" || { err "Could not resolve VM IPv4 — set VM_IP in ~/.config/utm-dev-bridge.env"; exit 1; }
ok "guest ${local_ip}"

if (( DO_SYNC )) && (( ! DEPS_ONLY )); then
  info "Syncing Mac → guest via utm-dev-bridge --sync-once"
  "${BRIDGE}" --sync-once || { err "sync failed"; exit 1; }
fi

check_deps "${local_ip}" || exit 1
(( DEPS_ONLY )) && { ok "deps-only complete"; exit 0; }

run_remote_test "${local_ip}" "${FILTER}"
exit $?
