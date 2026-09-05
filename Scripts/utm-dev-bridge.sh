#!/bin/zsh
# utm-dev-bridge.sh
# Mac host <-> UTM Windows 11 ARM guest: persistent SSH, rsync, localhost port forwards.
#
# Save:  BusBuddy-3/Scripts/utm-dev-bridge.sh  (this repo)
# Optional:  ln -sf "$(pwd)/Scripts/utm-dev-bridge.sh" "$HOME/.local/bin/utm-dev-bridge"
#
# Run from the Mac (not inside the VM):
#   ./Scripts/utm-dev-bridge.sh
#   ./Scripts/utm-dev-bridge.sh --doctor
#   ./Scripts/utm-dev-bridge.sh --sync-once
#
# Config (env, or ~/.config/utm-dev-bridge.env):
#   VM_IP          Fixed IPv4. Empty = utmctl + last-known IP.
#   SSH_USER       Guest Windows account (default: Macbook)
#   SSH_KEY        Private key (default: ~/.ssh/busbuddy-utm)
#   UTM_VM_NAME    utmctl name (default: Windows)
#   LOCAL_DIR      Mac project root
#   REMOTE_DIR     Guest path, POSIX form (default: /c/dev/BusBuddy-3)
#   FORWARD_PORTS  Comma list forwarded Mac:port -> VM:127.0.0.1:port (default: 3000,5000,8080)
#   POLL_SECONDS   Sync interval if fswatch is missing (default: 1.5)

emulate -L zsh
set -u
setopt pipefail

typeset -r SCRIPT_NAME="${0:t}"
typeset -r SCRIPT_DIR="${0:A:h}"
typeset -r REPO_ROOT="${SCRIPT_DIR:h}"
typeset -r CONFIG_FILE="${UTM_DEV_BRIDGE_ENV:-$HOME/.config/utm-dev-bridge.env}"
typeset -r STATE_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/utm-dev-bridge"
typeset -r LAST_IP_FILE="${STATE_DIR}/last-ipv4"
typeset -r CTL_SOCK="${STATE_DIR}/ssh.sock"
typeset -ga FORWARD_ACTIVE FORWARD_SKIPPED FORWARD_SSH

# --- colors (TTY only) ----------------------------------------------------------
if [[ -t 1 ]]; then
  typeset -r C_RESET=$'\033[0m' C_DIM=$'\033[2m' C_BOLD=$'\033[1m'
  typeset -r C_RED=$'\033[31m' C_GRN=$'\033[32m' C_YLW=$'\033[33m'
  typeset -r C_CYN=$'\033[36m' C_MAG=$'\033[35m'
else
  typeset -r C_RESET= C_DIM= C_BOLD= C_RED= C_GRN= C_YLW= C_CYN= C_MAG=
fi

log()  { print -r -- "${C_DIM}[$(date +%H:%M:%S)]${C_RESET} $*"; }
ok()   { print -r -- "${C_GRN}●${C_RESET} $*"; }
warn() { print -r -- "${C_YLW}▲${C_RESET} $*"; }
err()  { print -r -- "${C_RED}✖${C_RESET} $*" >&2; }
info() { print -r -- "${C_CYN}→${C_RESET} $*"; }

usage() {
  cat <<'EOF'
utm-dev-bridge — persistent SSH + rsync + localhost tunnels (Mac → UTM Windows)

USAGE
  utm-dev-bridge.sh              Connect, watch-sync, keep tunnels up
  utm-dev-bridge.sh --sync-once  One rsync, then exit (no watch / no -N)
  utm-dev-bridge.sh --doctor      Probe utmctl, SSH, rsync, ports
  utm-dev-bridge.sh --help

ENVIRONMENT  (also loaded from ~/.config/utm-dev-bridge.env)
  VM_IP            Guest IPv4 (empty = auto via utmctl)
  SSH_USER         Windows account          default: Macbook
  SSH_KEY          Identity file            default: ~/.ssh/busbuddy-utm
  UTM_VM_NAME      utmctl VM name           default: Windows
  LOCAL_DIR        Mac project root         default: repo containing this script
  REMOTE_DIR       Guest dest (Git/MSYS)   default: /c/dev/BusBuddy-3
  FORWARD_PORTS    Mac localhost binds      default: 3000,5000,8080
  POLL_SECONDS      Fallback watch interval  default: 1.5

Ports use SSH LocalForward (-L): Mac browser http://127.0.0.1:3000
reaches the process listening on the guest's 127.0.0.1:3000.
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
  : "${LOCAL_DIR:=$REPO_ROOT}"
  : "${REMOTE_DIR:=/c/dev/BusBuddy-3}"
  : "${FORWARD_PORTS:=3000,5000,8080}"
  : "${POLL_SECONDS:=1.5}"
  : "${VM_IP:=}"
  : "${RSYNC_PATH:=}"
}

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || { err "Missing command: $1"; return 1; }
}

ipv4_only() {
  grep -E '^([0-9]{1,3}\.){3}[0-9]{1,3}$'
}

resolve_vm_ip() {
  local ip
  if [[ -n "${VM_IP}" ]]; then
    print -r -- "${VM_IP}"
    return 0
  fi

  if command -v utmctl >/dev/null 2>&1; then
    ip="$(utmctl ip-address "${UTM_VM_NAME}" 2>/dev/null | ipv4_only | head -1 || true)"
    if [[ -n "${ip}" ]]; then
      print -r -- "${ip}"
      return 0
    fi
  fi

  if [[ -f "${LAST_IP_FILE}" ]]; then
    ip="$(<"${LAST_IP_FILE}")"
    if [[ "${ip}" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]]; then
      warn "utmctl has no IPv4; using last-known ${ip}"
      print -r -- "${ip}"
      return 0
    fi
  fi

  return 1
}

ensure_vm_started() {
  command -v utmctl >/dev/null 2>&1 || return 0
  local vm_status
  vm_status="$(utmctl status "${UTM_VM_NAME}" 2>/dev/null || true)"
  case "${vm_status}" in
    started|running) return 0 ;;
  esac
  warn "UTM VM '${UTM_VM_NAME}' is '${vm_status:-unknown}' — starting"
  utmctl start "${UTM_VM_NAME}" >/dev/null 2>&1 || true
}

typeset -a SSH_OPTS

init_ssh_opts() {
  SSH_OPTS=(
    -i "${SSH_KEY}"
    -o IdentitiesOnly=yes
    -o StrictHostKeyChecking=accept-new
    -o UserKnownHostsFile="${STATE_DIR}/known_hosts"
    -o ServerAliveInterval=15
    -o ServerAliveCountMax=4
    -o TCPKeepAlive=yes
    -o ExitOnForwardFailure=yes
    -o ConnectTimeout=8
    -o BatchMode=yes
  )
}

remote_target() {
  print -r -- "${SSH_USER}@${1}"
}

ssh_direct() {
  ssh "${SSH_OPTS[@]}" "$@"
}

ssh_mux() {
  ssh "${SSH_OPTS[@]}" -o ControlMaster=no -o ControlPath="${CTL_SOCK}" "$@"
}

master_alive() {
  ssh -O check -o ControlPath="${CTL_SOCK}" "$(remote_target "$1")" >/dev/null 2>&1
}

ssh_run() {
  local host="$1"
  shift
  if master_alive "${host}"; then
    ssh_mux "$(remote_target "${host}")" "$@"
  else
    ssh_direct "$(remote_target "${host}")" "$@"
  fi
}

probe_remote_rsync() {
  local host="$1" candidate
  if [[ -n "${RSYNC_PATH}" ]]; then
    print -r -- "${RSYNC_PATH}"
    return 0
  fi
  for candidate in \
    '/usr/bin/rsync' \
    'C:/Program Files/Git/usr/bin/rsync.exe'
  do
    if ssh_run "${host}" "${candidate} --version" >/dev/null 2>&1; then
      print -r -- "${candidate}"
      return 0
    fi
  done
  if ssh_run "${host}" "rsync --version" >/dev/null 2>&1; then
    print -r -- rsync
    return 0
  fi
  return 1
}

remote_win_path() {
  local p="${REMOTE_DIR}"
  if [[ "${p}" == /c/* ]]; then
    print -r -- "C:${p#/c}"
  else
    print -r -- "${p}"
  fi
}

choose_sync_engine() {
  local host="$1"
  RSYNC_REMOTE=""
  if RSYNC_REMOTE="$(probe_remote_rsync "${host}")"; then
    SYNC_ENGINE=rsync
    return 0
  fi
  if ssh_run "${host}" "tar --version" >/dev/null 2>&1; then
    SYNC_ENGINE=tar
    return 0
  fi
  SYNC_ENGINE=""
  return 1
}

tar_once() {
  local host="$1"
  local dest dest_ps stamp list
  dest="$(remote_win_path)"
  stamp="${STATE_DIR}/last-tar-sync"
  list="$(mktemp -t utm-dev-bridge-files)"
  COPYFILE_DISABLE=1
  export COPYFILE_DISABLE

  (
    cd "${LOCAL_DIR}" || exit 1
    if [[ -f "${stamp}" ]]; then
      find . \
        \( -name .git -o -name node_modules -o -name bin -o -name obj \
           -o -name build -o -name TestResults -o -name .vs -o -name .idea \) -prune -o \
        -type f -newer "${stamp}" -print
    else
      find . \
        \( -name .git -o -name node_modules -o -name bin -o -name obj \
           -o -name build -o -name TestResults -o -name .vs -o -name .idea \) -prune -o \
        -type f -print
    fi
  ) > "${list}"

  if [[ ! -s "${list}" ]]; then
    rm -f "${list}"
    return 0
  fi

  local count
  count="$(wc -l < "${list}" | tr -d ' ')"
  info "tar  ${count} file(s) → ${dest}"

  # Windows OpenSSH uses cmd.exe: POSIX single quotes are literal, so -C 'C:/...'
  # becomes chdir to a quoted path. Double quotes are valid in cmd and PowerShell.
  (
    cd "${LOCAL_DIR}" || exit 1
    tar -cf - -T "${list}"
  ) | ssh_run "${host}" "tar -xf - -C \"${dest}\""

  local rc=$?
  rm -f "${list}"
  if (( rc == 0 )); then
    touch "${stamp}"
  fi
  return "${rc}"
}

sync_files() {
  local host="$1"
  case "${SYNC_ENGINE}" in
    rsync) rsync_once "${host}" "${RSYNC_REMOTE}" ;;
    tar)   tar_once "${host}" ;;
    *)     return 1 ;;
  esac
}

ensure_remote_dir() {
  local host="$1"
  local win_path
  win_path="$(remote_win_path)"
  win_path="${win_path//\//\\}"
  ssh_run "${host}" "powershell -NoProfile -Command New-Item -ItemType Directory -Force -Path '${win_path}' | Out-Null" \
    >/dev/null 2>&1 || true
}

rsync_once() {
  local host="$1" rsync_remote="$2"
  local -a excludes
  excludes=(
    --exclude '.git/'
    --exclude 'node_modules/'
    --exclude 'bin/'
    --exclude 'obj/'
    --exclude 'build/'
    --exclude 'TestResults/'
    --exclude '.vs/'
    --exclude '.idea/'
    --exclude 'rag/.index/'
    --exclude 'Documentation/Archive/'
    --exclude '.DS_Store'
    --exclude '*.user'
    --exclude '*.wpftmp.csproj'
  )

  rsync -a --delete --omit-dir-times --no-perms --no-group --no-owner \
    --itemize-changes --human-readable \
    -e "ssh ${SSH_OPTS[*]} -o ControlMaster=no -o ControlPath=${CTL_SOCK}" \
    --rsync-path="${rsync_remote}" \
    "${excludes[@]}" \
    "${LOCAL_DIR}/" \
    "$(remote_target "${host}"):${REMOTE_DIR}/"
}

port_in_use() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
}

select_port_forwards() {
  local p
  FORWARD_ACTIVE=()
  FORWARD_SKIPPED=()
  FORWARD_SSH=()
  for p in ${(s:,:)FORWARD_PORTS}; do
    p="${p// /}"
    [[ -z "${p}" ]] && continue
    if port_in_use "${p}"; then
      FORWARD_SKIPPED+=("${p}")
      continue
    fi
    FORWARD_ACTIVE+=("${p}")
    FORWARD_SSH+=(-L "127.0.0.1:${p}:127.0.0.1:${p}")
  done
}

start_master() {
  local host="$1"
  local -a ssh_cmd

  select_port_forwards

  mkdir -p "${STATE_DIR}"
  rm -f "${CTL_SOCK}"

  ssh_cmd=(
    ssh -N -M
    "${SSH_OPTS[@]}"
    -o ControlMaster=yes
    -o ControlPath="${CTL_SOCK}"
    -o ControlPersist=no
  )
  (( ${#FORWARD_SSH} )) && ssh_cmd+=("${FORWARD_SSH[@]}")
  ssh_cmd+=("$(remote_target "${host}")")

  "${ssh_cmd[@]}" &
  MASTER_PID=$!
  print -r -- "${MASTER_PID}" > "${STATE_DIR}/master.pid"

  local i
  for i in {1..25}; do
    if master_alive "${host}"; then
      return 0
    fi
    if ! kill -0 "${MASTER_PID}" 2>/dev/null; then
      return 1
    fi
    sleep 0.2
  done
  return 1
}

stop_master() {
  local host="${1:-}"
  if [[ -n "${host}" ]]; then
    ssh -O exit -o ControlPath="${CTL_SOCK}" "$(remote_target "${host}")" >/dev/null 2>&1 || true
  fi
  if [[ -n "${MASTER_PID:-}" ]]; then
    kill "${MASTER_PID}" 2>/dev/null || true
    wait "${MASTER_PID}" 2>/dev/null || true
  fi
  MASTER_PID=""
  rm -f "${CTL_SOCK}"
}

SYNC_PID=""
stop_sync() {
  if [[ -n "${SYNC_PID}" ]] && kill -0 "${SYNC_PID}" 2>/dev/null; then
    kill "${SYNC_PID}" 2>/dev/null || true
    wait "${SYNC_PID}" 2>/dev/null || true
  fi
  SYNC_PID=""
}

sync_loop() {
  local host="$1"

  run_sync() {
    local out
    if out="$(sync_files "${host}" 2>&1)"; then
      if [[ -n "${out}" ]]; then
        print -r -- "${C_DIM}${out}${C_RESET}"
      fi
    else
      warn "sync failed (will retry): ${out}"
    fi
  }

  run_sync

  if command -v fswatch >/dev/null 2>&1; then
    ok "watching with fswatch (latency 0.35s)"
    fswatch -o --latency 0.35 \
      -e '/\.git/' -e '/bin/' -e '/obj/' -e '/node_modules/' -e '/build/' \
      "${LOCAL_DIR}" | while read -r _; do
        info "change detected — syncing"
        sleep 0.2
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

print_banner() {
  print
  print -r -- "${C_BOLD}UTM dev bridge${C_RESET}  Mac ↔ ${UTM_VM_NAME}"
  print -r -- "${C_DIM}Ctrl-C stops tunnels and the sync loop.${C_RESET}"
  print
}

print_status() {
  local host="$1"
  ok "SSH  ${SSH_USER}@${host}  (keepalive 15s, ControlMaster)"
  ok "sync ${SYNC_ENGINE}  ${LOCAL_DIR}  →  ${REMOTE_DIR}"
  if (( ${#FORWARD_ACTIVE} )); then
    local p
    for p in "${FORWARD_ACTIVE[@]}"; do
      ok "open ${C_CYN}http://127.0.0.1:${p}/${C_RESET}  →  guest :${p}"
    done
  else
    warn "no port forwards bound"
  fi
  if (( ${#FORWARD_SKIPPED} )); then
    warn "already in use on Mac, skipped: ${(j:, :)FORWARD_SKIPPED}"
  fi
  print
}

doctor() {
  print -r -- "${C_BOLD}doctor${C_RESET}"
  need_cmd ssh && ok "ssh"
  need_cmd rsync && ok "rsync $(rsync --version | head -1)"
  if command -v utmctl >/dev/null 2>&1; then
    ok "utmctl  status=$(utmctl status "${UTM_VM_NAME}" 2>/dev/null || echo missing)"
  else
    warn "utmctl missing — set VM_IP by hand"
  fi
  command -v fswatch >/dev/null 2>&1 && ok "fswatch" || warn "fswatch missing (optional)"
  [[ -f "${SSH_KEY}" ]] && ok "key ${SSH_KEY}" || err "key missing: ${SSH_KEY}"
  [[ -d "${LOCAL_DIR}" ]] && ok "local ${LOCAL_DIR}" || err "LOCAL_DIR missing"

  ensure_vm_started
  local ip
  if ! ip="$(resolve_vm_ip)"; then
    err "Could not resolve VM IPv4. Set VM_IP=192.168.64.2"
    return 1
  fi
  ok "guest IPv4 ${ip}"

  if ssh_direct "$(remote_target "${ip}")" "echo ok" >/dev/null 2>&1; then
    ok "SSH login ${SSH_USER}@${ip}"
  else
    err "SSH failed. Check sshd in the guest and ${SSH_KEY}"
    return 1
  fi

  if choose_sync_engine "${ip}"; then
    if [[ "${SYNC_ENGINE}" == rsync ]]; then
      ok "guest rsync  ${RSYNC_REMOTE}"
    else
      warn "guest rsync missing — using tar incremental (Windows tar.exe). Deletes on the guest are not mirrored."
    fi
  else
    err "No rsync or tar on the guest"
    return 1
  fi

  local p
  for p in ${(s:,:)FORWARD_PORTS}; do
    p="${p// /}"
    if port_in_use "${p}"; then
      warn "Mac already listening on ${p}"
    else
      ok "Mac port ${p} free"
    fi
  done
  return 0
}

cleanup() {
  trap - INT TERM EXIT
  print
  warn "shutting down"
  stop_sync
  stop_master "${CURRENT_HOST:-}"
  exit 0
}

run_bridge() {
  mkdir -p "${STATE_DIR}" "${HOME}/.config"
  need_cmd ssh || exit 1
  [[ -f "${SSH_KEY}" ]] || { err "SSH_KEY not found: ${SSH_KEY}"; exit 1; }
  [[ -d "${LOCAL_DIR}" ]] || { err "LOCAL_DIR missing: ${LOCAL_DIR}"; exit 1; }

  print_banner
  trap cleanup INT TERM EXIT

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
    stop_sync
    stop_master "${ip}"

    if ! start_master "${ip}"; then
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

    print_status "${ip}"
    backoff=2

    sync_loop "${ip}" &
    SYNC_PID=$!

    wait "${MASTER_PID}" 2>/dev/null || true
    warn "SSH dropped — reconnecting"
    stop_sync
    sleep 1
  done
}

sync_once_main() {
  mkdir -p "${STATE_DIR}"
  need_cmd ssh || exit 1
  ensure_vm_started
  local ip
  ip="$(resolve_vm_ip)" || { err "No VM IPv4"; exit 1; }
  CURRENT_HOST="${ip}"
  trap 'stop_master "${CURRENT_HOST}"' EXIT INT TERM
  start_master "${ip}" || { err "SSH failed"; exit 1; }
  choose_sync_engine "${ip}" || { err "No guest rsync/tar"; exit 1; }
  ensure_remote_dir "${ip}"
  info "one-shot ${SYNC_ENGINE} ${LOCAL_DIR} → ${REMOTE_DIR}"
  rm -f "${STATE_DIR}/last-tar-sync"
  sync_files "${ip}"
  ok "sync complete"
}

# --- main --------------------------------------------------------------------
load_config
mkdir -p "${STATE_DIR}"
init_ssh_opts
MODE=bridge
case "${1:-}" in
  -h|--help) usage; exit 0 ;;
  --doctor) MODE=doctor ;;
  --sync-once) MODE=sync-once ;;
  "") MODE=bridge ;;
  *) err "Unknown argument: $1"; usage; exit 2 ;;
esac

case "${MODE}" in
  doctor) doctor ;;
  sync-once) sync_once_main ;;
  bridge) run_bridge ;;
esac
