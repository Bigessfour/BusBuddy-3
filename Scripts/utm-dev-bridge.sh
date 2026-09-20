#!/bin/zsh
# utm-dev-bridge.sh
# Agent-facing CLI: Mac host ↔ UTM Windows 11 ARM (SSH + NTFS copy at C:\dev\BusBuddy-3).
#
#   ./Scripts/utm-dev-bridge.sh doctor|status|sync|exec|test|launch|pull-logs|watch
#
# Operators: docs/utm-dev-bridge.md
# Config: ~/.config/utm-dev-bridge.env  (see Scripts/utm-dev-bridge.env.example)

emulate -L zsh
set -u
setopt pipefail

typeset -r SCRIPT_NAME="${0:t}"
typeset -r SCRIPT_DIR="${0:A:h}"
typeset -r REPO_ROOT="${SCRIPT_DIR:h}"

# shellcheck disable=SC1091
source "${SCRIPT_DIR}/utm-dev-bridge.inc"

typeset -r DEFAULT_FILTER='FullyQualifiedName~StudentsListCoordinatorTests|FullyQualifiedName~StudentsViewModelTests|FullyQualifiedName~StudentFormViewModelSaveTests|FullyQualifiedName~StudentsViewTests|FullyQualifiedName~PostgresConnectionResolverTests|FullyQualifiedName~DestinationServiceTests'
typeset -r FULL_FILTER='Category!=Integration&Category!=InMemoryFlaky'

usage() {
  cat <<'EOF'
utm-dev-bridge — Mac → UTM Windows guest RPC (SSH, not utmctl exec)

USAGE
  utm-dev-bridge.sh doctor
  utm-dev-bridge.sh status
  utm-dev-bridge.sh sync [--mirror] [--full]
  utm-dev-bridge.sh exec -- <powershell>
  utm-dev-bridge.sh test [--no-sync] [--deps-only] [--full] [--filter EXPR]
  utm-dev-bridge.sh launch [--no-sync] [--no-build]
  utm-dev-bridge.sh pull-logs
  utm-dev-bridge.sh watch          # human-only; agents must not start this

ALIASES
  --doctor      doctor
  --sync-once   sync

ENVIRONMENT  (also ~/.config/utm-dev-bridge.env)
  VM_IP          Guest IPv4 (empty = utmctl, then probed last-known)
  SSH_USER       Windows account          default: Macbook
  SSH_KEY        Identity file            default: ~/.ssh/busbuddy-utm
  UTM_VM_NAME    utmctl VM name           default: Windows
  LOCAL_DIR      Mac project root         default: this repo
  REMOTE_DIR     Guest dest (Git/MSYS)    default: /c/dev/BusBuddy-3
  FORWARD_PORTS  Opt-in Mac localhost -L  default: empty
  POLL_SECONDS   watch poll if no fswatch default: 1.5

Runtime root is C:\dev\BusBuddy-3. Z:\ is bootstrap only.
See docs/utm-dev-bridge.md.
EOF
}

print_status_human() {
  local host="$1"
  ok "SSH  ${SSH_USER}@${host}  (keepalive 15s, ControlMaster)"
  ok "sync ${SYNC_ENGINE:-none}  ${LOCAL_DIR}  →  ${REMOTE_DIR}  ($(posix_to_win_path))"
  if (( ${#FORWARD_ACTIVE} )); then
    local p
    for p in "${FORWARD_ACTIVE[@]}"; do
      ok "open ${C_CYN}http://127.0.0.1:${p}/${C_RESET}  →  guest :${p}"
    done
  else
    info "no port forwards (FORWARD_PORTS empty)"
  fi
  if (( ${#FORWARD_SKIPPED} )); then
    warn "already in use on Mac, skipped: ${(j:, :)FORWARD_SKIPPED}"
  fi
}

cmd_doctor() {
  print -r -- "${C_BOLD}doctor${C_RESET}"
  need_cmd ssh && ok "ssh"
  need_cmd rsync && ok "rsync $(rsync --version | head -1)"
  if command -v utmctl >/dev/null 2>&1; then
    ok "utmctl  status=$(utmctl status "${UTM_VM_NAME}" 2>/dev/null || echo missing)"
  else
    warn "utmctl missing — set VM_IP by hand"
  fi
  command -v fswatch >/dev/null 2>&1 && ok "fswatch" || warn "fswatch missing (optional)"
  if [[ "${SSH_KEY}" == '~'* ]]; then
    err "SSH_KEY still has a literal tilde: ${SSH_KEY} — quote-expand failed"
  fi
  [[ -f "${SSH_KEY}" ]] && ok "key ${SSH_KEY}" || err "key missing: ${SSH_KEY}"
  [[ -d "${LOCAL_DIR}" ]] && ok "local ${LOCAL_DIR}" || err "LOCAL_DIR missing"

  ensure_vm_started
  local ip
  if ! ip="$(resolve_vm_ip)"; then
    err "Could not resolve VM IPv4. Set VM_IP=192.168.64.2"
    return 1
  fi
  CURRENT_HOST="${ip}"
  ok "guest IPv4 ${ip}"

  if probe_ssh_ok "${ip}" 8; then
    ok "SSH login ${SSH_USER}@${ip}"
  else
    err "SSH failed. Check sshd in the guest and ${SSH_KEY}"
    err "Microsoft checklist: https://learn.microsoft.com/windows-server/administration/openssh/openssh_keymanagement"
    return 1
  fi

  if choose_sync_engine "${ip}"; then
    if [[ "${SYNC_ENGINE}" == rsync ]]; then
      ok "guest rsync  ${RSYNC_REMOTE}"
    else
      warn "guest rsync missing — using tar incremental. Deletes on the guest are not mirrored."
    fi
  else
    err "No rsync or tar on the guest"
    return 1
  fi

  if [[ -n "${FORWARD_PORTS}" ]]; then
    local p
    for p in ${(s:,:)FORWARD_PORTS}; do
      p="${p// /}"
      [[ -z "${p}" ]] && continue
      if port_in_use "${p}"; then
        warn "Mac already listening on ${p}"
      else
        ok "Mac port ${p} free"
      fi
    done
  else
    ok "FORWARD_PORTS empty (no LocalForward)"
  fi

  write_status_json true
  return 0
}

cmd_status() {
  mkdir -p "${STATE_DIR}"
  if [[ -f "${STATUS_JSON}" ]]; then
    if [[ -n "${CURRENT_HOST}" ]] || CURRENT_HOST="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get("ip") or "")' "${STATUS_JSON}" 2>/dev/null)"; then
      :
    fi
  fi
  if [[ -z "${CURRENT_HOST}" ]]; then
    CURRENT_HOST="$(resolve_vm_ip 2>/dev/null || true)"
  fi
  local alive="false"
  if [[ -n "${CURRENT_HOST}" ]] && master_alive "${CURRENT_HOST}"; then
    alive="true"
  fi
  if [[ -f "${STATUS_JSON}" ]]; then
    python3 - "${STATUS_JSON}" "${alive}" <<'PY'
import json, sys
path, alive = sys.argv[1], sys.argv[2]
with open(path, encoding="utf-8") as f:
    data = json.load(f)
data["master_alive"] = alive.lower() == "true"
print(json.dumps(data, indent=2))
PY
  else
    print -r -- '{"ok":false,"error":"no status.json yet — run doctor or sync"}'
    return 1
  fi
}

cmd_sync() {
  local reset_tar=0
  while (( $# )); do
    case "$1" in
      --mirror) SYNC_MIRROR=1; shift ;;
      --full) reset_tar=1; shift ;;
      *) err "Unknown sync flag: $1"; return 2 ;;
    esac
  done
  ensure_connected 300 || return 1
  choose_sync_engine "${CURRENT_HOST}" || { err "No guest rsync/tar"; return 1; }
  ensure_remote_dir "${CURRENT_HOST}"
  local extra=""
  (( SYNC_MIRROR )) && extra=" (--mirror)"
  (( reset_tar )) && extra+=" (--full)"
  info "one-shot ${SYNC_ENGINE} ${LOCAL_DIR} → ${REMOTE_DIR}${extra}"
  if [[ "${SYNC_ENGINE}" == tar ]] && (( reset_tar )); then
    rm -f "${STATE_DIR}/last-tar-sync"
  fi
  sync_files "${CURRENT_HOST}" || { err "sync failed"; write_status_json false; return 1; }
  write_status_json true
  ok "sync complete"
}

cmd_exec() {
  if [[ "${1:-}" == -- ]]; then
    shift
  fi
  (( $# )) || { err "exec needs a PowerShell command after --"; return 2; }
  ensure_connected 300 || return 1
  local script="${(j: :)@}"
  ssh_ps "${CURRENT_HOST}" "${script}"
}

cmd_test() {
  local do_sync=1 deps_only=0 filter="${DEFAULT_FILTER}"
  while (( $# )); do
    case "$1" in
      --deps-only) deps_only=1; shift ;;
      --no-sync) do_sync=0; shift ;;
      --full) filter="${FULL_FILTER}"; shift ;;
      --filter)
        shift
        [[ $# -ge 1 ]] || { err "--filter needs an expression"; return 2; }
        filter="$1"
        shift
        ;;
      --mirror) SYNC_MIRROR=1; shift ;;
      *) err "Unknown test flag: $1"; return 2 ;;
    esac
  done

  ensure_connected 300 || return 1
  local win_root sln_ps filter_ps
  win_root="$(posix_to_win_path)"
  sln_ps="${win_root}\\BusBuddy.sln"
  sln_ps=${sln_ps:gs/\'/\'\'/}

  if (( do_sync )) && (( ! deps_only )); then
    choose_sync_engine "${CURRENT_HOST}" || { err "No guest rsync/tar"; return 1; }
    ensure_remote_dir "${CURRENT_HOST}"
    info "Syncing Mac → guest"
    sync_files "${CURRENT_HOST}" || { err "sync failed"; return 1; }
    write_status_json true
  fi

  info "Probing guest toolchain on ${SSH_USER}@${CURRENT_HOST} …"
  local out
  out="$(ssh_ps "${CURRENT_HOST}" "
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
    err "${win_root}\\BusBuddy.sln missing — run ./Scripts/utm-dev-bridge.sh sync (REMOTE_DIR=${REMOTE_DIR})"
    return 1
  }
  ok "Guest repo present (${win_root})"

  if print -r -- "${out}" | grep -q 'CONN_OK'; then
    ok "BUSBUDDY_CONNECTION available (User/Machine)"
  else
    warn "BUSBUDDY_CONNECTION missing — Integration tests needing Postgres will fail; run launch or .\\utm_run_in_vm.ps1 once in the guest"
  fi

  (( deps_only )) && { ok "deps-only complete"; return 0; }

  filter_ps=${filter:gs/\'/\'\'/}
  info "dotnet test on guest (${win_root})"
  info "filter: ${filter}"
  ssh_ps "${CURRENT_HOST}" "
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
Write-Host ('BUSBUDDY_CONNECTION host hint: ' + \$hint)
& dotnet test 'BusBuddy.Tests\\BusBuddy.Tests.csproj' -c Release --filter '${filter_ps}'
exit \$LASTEXITCODE
"
}

cmd_launch() {
  local do_sync=1 do_build=1
  while (( $# )); do
    case "$1" in
      --no-sync) do_sync=0; shift ;;
      --no-build) do_build=0; shift ;;
      --mirror) SYNC_MIRROR=1; shift ;;
      *) err "Unknown launch flag: $1"; return 2 ;;
    esac
  done

  ensure_connected 300 || return 1
  local win_root exe_ps user_ps launcher_ps
  win_root="$(posix_to_win_path)"
  win_root=${win_root:gs/\'/\'\'/}
  exe_ps="${win_root}\\BusBuddy.WPF\\bin\\Debug\\net9.0-windows\\BusBuddy.WPF.exe"
  launcher_ps="${win_root}\\Scripts\\UtmLaunchWpf.ps1"
  user_ps=${SSH_USER:gs/\'/\'\'/}

  if (( do_sync )); then
    choose_sync_engine "${CURRENT_HOST}" || { err "No guest rsync/tar"; return 1; }
    ensure_remote_dir "${CURRENT_HOST}"
    info "Syncing Mac → guest before launch"
    sync_files "${CURRENT_HOST}" || { err "sync failed"; return 1; }
    write_status_json true
  fi

  info "Stopping leftover BusBuddy.WPF so the guest build can overwrite bin/"
  ssh_ps "${CURRENT_HOST}" "
Get-Process -Name 'BusBuddy.WPF' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Get-Process -Name 'BusBuddy.WPF' -ErrorAction SilentlyContinue | ForEach-Object { \$_.Kill(); Start-Sleep -Seconds 1 }
" || true

  if (( do_build )); then
    info "Building WPF on guest (bin/ is not synced from Mac)"
    ssh_ps "${CURRENT_HOST}" "
\$ErrorActionPreference = 'Stop'
Set-Location '${win_root}'
& dotnet build 'BusBuddy.WPF\\BusBuddy.WPF.csproj' -c Debug -p:EnableWindowsTargeting=true --nologo
exit \$LASTEXITCODE
" || { err "guest WPF build failed"; return 1; }
  fi

  info "Launching WPF on interactive session ${user_ps} from ${win_root}"
  ssh_ps "${CURRENT_HOST}" "
\$ErrorActionPreference = 'Continue'
\$winRoot = '${win_root}'
\$exe = '${exe_ps}'
\$launcher = '${launcher_ps}'
\$statusFile = Join-Path \$winRoot 'artifacts\\utm-launch-last.txt'
if (-not (Test-Path -LiteralPath \$launcher)) {
  Write-Output 'LAUNCH_FAIL missing Scripts\\UtmLaunchWpf.ps1'
  exit 3
}
if (-not (Test-Path -LiteralPath \$exe)) {
  Write-Output 'LAUNCH_FAIL no exe'
  exit 3
}
Get-Process -Name 'BusBuddy.WPF' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
if (Test-Path -LiteralPath \$statusFile) { Remove-Item -LiteralPath \$statusFile -Force }
\$tn = 'BusBuddyUtmLaunch'
schtasks /Delete /TN \$tn /F 2>\$null | Out-Null
\$tr = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"' + \$launcher + '\"'
\$createOut = schtasks /Create /TN \$tn /SC ONCE /ST 23:59 /RL LIMITED /IT /F /TR \$tr /RU '${user_ps}'
Write-Output \$createOut
\$runOut = schtasks /Run /TN \$tn
Write-Output \$runOut
for (\$i = 0; \$i -lt 40; \$i++) {
  Start-Sleep -Seconds 1
  \$p = Get-Process -Name 'BusBuddy.WPF' -ErrorAction SilentlyContinue | Select-Object -First 1
  if (\$null -eq \$p) { continue }
  if (Test-Path -LiteralPath \$statusFile) {
    Get-Content -LiteralPath \$statusFile
  }
  Write-Output ('LAUNCH pid=' + \$p.Id + ' session=' + \$p.SessionId)
  if ((Get-Content -LiteralPath \$statusFile -ErrorAction SilentlyContinue) -match 'LAUNCH_FAIL') { exit 3 }
  exit 0
}
Write-Output 'LAUNCH_FAIL no process after wait'
if (Test-Path -LiteralPath \$statusFile) { Get-Content -LiteralPath \$statusFile }
exit 3
"
}

cmd_pull_logs() {
  ensure_connected 300 || return 1
  local stamp dest win_root out copied
  stamp="$(date -u +%Y%m%dT%H%M%SZ)"
  dest="${REPO_ROOT}/artifacts/utm-runtime-logs-${stamp}"
  mkdir -p "${dest}"
  win_root="$(posix_to_win_path)"
  win_root=${win_root:gs/\'/\'\'/}

  info "Collecting guest Serilog files → ${dest}"
  typeset -a posix_dirs
  posix_dirs=(
    "${REMOTE_DIR}/BusBuddy.WPF/bin/Debug/net9.0-windows/logs"
    "${REMOTE_DIR}/BusBuddy.WPF/bin/Release/net9.0-windows/logs"
    "${REMOTE_DIR}/BusBuddy.WPF/bin/Debug/logs"
    "${REMOTE_DIR}/BusBuddy.WPF/bin/Release/logs"
    "${REMOTE_DIR}/BusBuddy.WPF/logs"
    "${REMOTE_DIR}/BusBuddy.WPF/Logs"
    "${REMOTE_DIR}/logs"
    "${REMOTE_DIR}/Logs"
  )

  copied=0
  local d win leaf parent win_scp
  for d in "${posix_dirs[@]}"; do
    win="$(posix_to_win_path "${d}")"
    win=${win:gs/\'/\'\'/}
    out="$(ssh_ps "${CURRENT_HOST}" "if (Test-Path -LiteralPath '${win}') { 'YES' } else { 'NO' }" | tr -d '\r')"
    if [[ "${out}" != *YES* ]]; then
      continue
    fi
    leaf="${d##*/}"
    parent="${d%/*}"
    parent="${parent##*/}"
    win_scp="${win//\\//}"
    mkdir -p "${dest}/${parent}-${leaf}"
    if scp -q -r "${SSH_OPTS[@]}" "$(remote_target "${CURRENT_HOST}"):${win_scp}" "${dest}/${parent}-${leaf}/"; then
      (( copied++ )) || true
    fi
  done

  if (( copied == 0 )); then
    warn "no matching Serilog directories on guest"
  else
    ok "copied ${copied} log dir(s) → ${dest}"
  fi
  print -r -- "${dest}"
}

SYNC_PID=""
stop_sync_loop() {
  if [[ -n "${SYNC_PID}" ]] && kill -0 "${SYNC_PID}" 2>/dev/null; then
    kill "${SYNC_PID}" 2>/dev/null || true
    wait "${SYNC_PID}" 2>/dev/null || true
  fi
  SYNC_PID=""
}

sync_loop() {
  local host="$1"
  local in_flight=0

  run_sync() {
    local out
    if (( in_flight )); then
      return 0
    fi
    in_flight=1
    if out="$(sync_files "${host}" 2>&1)"; then
      if [[ -n "${out}" ]]; then
        print -r -- "${C_DIM}${out}${C_RESET}"
      fi
      write_status_json true
    else
      warn "sync failed (will retry): ${out}"
    fi
    in_flight=0
  }

  run_sync

  if command -v fswatch >/dev/null 2>&1; then
    ok "watching with fswatch (debounce 1s)"
    fswatch -o --latency 0.35 \
      -e '/\.git/' -e '/bin/' -e '/obj/' -e '/node_modules/' -e '/build/' \
      -e '/keys/' -e '/logs/' -e '/Logs/' -e '/TestResults/' \
      "${LOCAL_DIR}" | while read -r _; do
        info "change detected — debounce"
        sleep 1
        run_sync
      done
  else
    warn "fswatch not installed — polling every ${POLL_SECONDS}s (brew install fswatch)"
    while true; do
      sleep "${POLL_SECONDS}"
      run_sync
    done
  fi
}

cleanup_watch() {
  trap - INT TERM EXIT
  print
  warn "shutting down"
  stop_sync_loop
  stop_master "${CURRENT_HOST:-}"
  utm_lock_end
  exit 0
}

cmd_watch() {
  print
  print -r -- "${C_BOLD}UTM dev bridge${C_RESET}  Mac ↔ ${UTM_VM_NAME}"
  print -r -- "${C_DIM}Ctrl-C stops tunnels and the sync loop. Agents should not start watch.${C_RESET}"
  print
  trap cleanup_watch INT TERM EXIT

  local backoff=2
  while true; do
    ensure_vm_started
    local ip
    if ! ip="$(resolve_vm_ip)"; then
      err "No guest IPv4 yet (UTM shared net is usually 192.168.64.2)"
      sleep "${backoff}"
      (( backoff = backoff < 30 ? backoff + 2 : 30 ))
      continue
    fi
    CURRENT_HOST="${ip}"
    print -r -- "${ip}" > "${LAST_IP_FILE}"

    info "connecting ${SSH_USER}@${ip} ..."
    stop_sync_loop
    if ! ensure_master "${ip}" no; then
      err "SSH master failed — retry in ${backoff}s (guest asleep or sshd down?)"
      sleep "${backoff}"
      (( backoff = backoff < 30 ? backoff + 2 : 30 ))
      continue
    fi

    if ! choose_sync_engine "${ip}"; then
      err "Guest has neither rsync nor tar — retry in ${backoff}s"
      sleep "${backoff}"
      continue
    fi
    ensure_remote_dir "${ip}"
    print_status_human "${ip}"
    write_status_json true
    backoff=2

    sync_loop "${ip}" &
    SYNC_PID=$!

    if [[ -n "${MASTER_PID}" ]]; then
      wait "${MASTER_PID}" 2>/dev/null || true
    else
      while master_alive "${ip}"; do
        sleep 5
      done
    fi
    warn "SSH dropped — reconnecting"
    stop_sync_loop
    sleep 1
  done
}

# --- main --------------------------------------------------------------------
load_config
mkdir -p "${STATE_DIR}"
init_ssh_opts

MODE=""
case "${1:-}" in
  ""|-h|--help|help)
    usage
    if [[ -f "${STATUS_JSON}" ]]; then
      print
      info "existing status:"
      cmd_status || true
    fi
    [[ -z "${1:-}" ]] && exit 2
    exit 0
    ;;
  doctor|--doctor) MODE=doctor; shift ;;
  status) MODE=status; shift ;;
  sync|--sync-once) MODE=sync; shift ;;
  exec) MODE=exec; shift ;;
  test) MODE=test; shift ;;
  launch) MODE=launch; shift ;;
  pull-logs) MODE=pull-logs; shift ;;
  watch) MODE=watch; shift ;;
  *) err "Unknown argument: $1"; usage; exit 2 ;;
esac

case "${MODE}" in
  doctor) cmd_doctor ;;
  status) cmd_status ;;
  sync) cmd_sync "$@" ;;
  exec) cmd_exec "$@" ;;
  test) cmd_test "$@" ;;
  launch) cmd_launch "$@" ;;
  pull-logs) cmd_pull_logs ;;
  watch) cmd_watch ;;
esac
