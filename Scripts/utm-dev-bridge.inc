# utm-dev-bridge.inc
# Shared Mac → UTM Windows helpers. Sourced by utm-dev-bridge.sh and thin wrappers.
# Caller must set SCRIPT_DIR and REPO_ROOT before sourcing.
#
# Documented layers:
#   utmctl          https://docs.getutm.app/scripting/scripting/
#   OpenSSH Windows https://learn.microsoft.com/windows-server/administration/openssh/openssh_keymanagement
#   EncodedCommand  https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_powershell_exe
# BusBuddy-only: runtime root C:\dev\BusBuddy-3, SSH user Macbook, never launch via utmctl exec.

[[ -n "${UTM_BRIDGE_INC_LOADED:-}" ]] && return 0
typeset -g UTM_BRIDGE_INC_LOADED=1

: "${CONFIG_FILE:=${UTM_DEV_BRIDGE_ENV:-$HOME/.config/utm-dev-bridge.env}}"
: "${STATE_DIR:=${XDG_CACHE_HOME:-$HOME/.cache}/utm-dev-bridge}"
typeset -g LAST_IP_FILE="${STATE_DIR}/last-ipv4"
typeset -g CTL_SOCK="${STATE_DIR}/ssh.sock"
typeset -g STATUS_JSON="${STATE_DIR}/status.json"
typeset -g LOCK_FILE="${STATE_DIR}/bridge.lock"
typeset -g CURRENT_HOST=""
typeset -g MASTER_PID=""
typeset -g SYNC_ENGINE=""
typeset -g RSYNC_REMOTE=""
typeset -g SYNC_MIRROR=0
typeset -g UTM_LOCK_FD=""
typeset -ga FORWARD_ACTIVE FORWARD_SKIPPED FORWARD_SSH SSH_OPTS

if [[ -t 1 ]]; then
  typeset -g C_RESET=$'\033[0m' C_DIM=$'\033[2m' C_BOLD=$'\033[1m'
  typeset -g C_RED=$'\033[31m' C_GRN=$'\033[32m' C_YLW=$'\033[33m'
  typeset -g C_CYN=$'\033[36m'
else
  typeset -g C_RESET= C_DIM= C_BOLD= C_RED= C_GRN= C_YLW= C_CYN=
fi

log()  { print -r -- "${C_DIM}[$(date +%H:%M:%S)]${C_RESET} $*"; }
ok()   { print -r -- "${C_GRN}●${C_RESET} $*"; }
warn() { print -r -- "${C_YLW}▲${C_RESET} $*"; }
err()  { print -r -- "${C_RED}✖${C_RESET} $*" >&2; }
info() { print -r -- "${C_CYN}→${C_RESET} $*"; }

utm_expand_tilde() {
  local p="$1"
  if [[ "${p}" == '~' ]]; then
    print -r -- "${HOME}"
  elif [[ "${p}" == '~/'* ]]; then
    print -r -- "${HOME}/${p#"~/"}"
  else
    print -r -- "${p}"
  fi
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
  : "${FORWARD_PORTS:=}"
  : "${POLL_SECONDS:=1.5}"
  : "${VM_IP:=}"
  : "${RSYNC_PATH:=}"

  SSH_KEY="$(utm_expand_tilde "${SSH_KEY}")"
  LOCAL_DIR="$(utm_expand_tilde "${LOCAL_DIR}")"
}

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || { err "Missing command: $1"; return 1; }
}

ipv4_only() {
  grep -E '^([0-9]{1,3}\.){3}[0-9]{1,3}$'
}

posix_to_win_path() {
  local p="${1:-$REMOTE_DIR}"
  if [[ "${p}" =~ ^/([a-zA-Z])/(.*)$ ]]; then
    print -r -- "${(U)match[1]}:\\${match[2]//\//\\}"
  else
    print -r -- "${p}"
  fi
}

remote_win_path() {
  posix_to_win_path "${REMOTE_DIR}"
}

remote_target() {
  print -r -- "${SSH_USER}@${1}"
}

init_ssh_opts() {
  SSH_OPTS=(
    -i "${SSH_KEY}"
    -o IdentitiesOnly=yes
    -o StrictHostKeyChecking=accept-new
    -o UserKnownHostsFile="${STATE_DIR}/known_hosts"
    -o ServerAliveInterval=15
    -o ServerAliveCountMax=4
    -o TCPKeepAlive=yes
    -o ConnectTimeout=8
    -o BatchMode=yes
  )
}

ssh_direct() {
  ssh "${SSH_OPTS[@]}" "$@"
}

ssh_mux() {
  ssh "${SSH_OPTS[@]}" -o ControlMaster=no -o ControlPath="${CTL_SOCK}" "$@"
}

master_alive() {
  local host="${1:-$CURRENT_HOST}"
  [[ -n "${host}" ]] || return 1
  ssh -O check -o ControlPath="${CTL_SOCK}" "$(remote_target "${host}")" >/dev/null 2>&1
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

# UTF-16LE base64 — documented for powershell.exe -EncodedCommand.
# https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_powershell_exe
ssh_ps() {
  local host="$1"
  local script="$2"
  local enc wrapped
  wrapped=$'$ProgressPreference = \'SilentlyContinue\'\n'"${script}"
  enc="$(print -r -- "${wrapped}" | python3 -c 'import sys,base64; print(base64.b64encode(sys.stdin.read().encode("utf-16le")).decode())')"
  ssh_run "${host}" powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -EncodedCommand "${enc}" 2>&1 \
    | python3 -c 'import re,sys; t=sys.stdin.read(); sys.stdout.write(re.sub(r"#< CLIXML\r?\n?<Objs[\s\S]*?</Objs>","",t))'
}

probe_ssh_ok() {
  local ip="$1"
  local timeout="${2:-2}"
  ssh "${SSH_OPTS[@]}" -o ConnectTimeout="${timeout}" -o ExitOnForwardFailure=no \
    "$(remote_target "${ip}")" "echo ok" >/dev/null 2>&1
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

resolve_vm_ip() {
  local ip cached
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
    cached="$(<"${LAST_IP_FILE}")"
    if [[ "${cached}" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]]; then
      if probe_ssh_ok "${cached}" 2; then
        warn "utmctl has no IPv4; using last-known ${cached} (SSH probe ok)"
        print -r -- "${cached}"
        return 0
      fi
      warn "last-known ${cached} failed a 2s SSH probe — not using stale lease"
    fi
  fi

  return 1
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

utm_lock_begin() {
  mkdir -p "${STATE_DIR}"
  : >>"${LOCK_FILE}"
  if zmodload zsh/system 2>/dev/null; then
    if zsystem flock -t 15 -f UTM_LOCK_FD "${LOCK_FILE}"; then
      return 0
    fi
    err "Could not acquire ${LOCK_FILE} in 15s"
    return 1
  fi
  local i
  for i in {1..30}; do
    if mkdir "${LOCK_FILE}.dir" 2>/dev/null; then
      UTM_LOCK_FD=mkdir
      return 0
    fi
    sleep 0.5
  done
  err "Could not acquire mkdir lock ${LOCK_FILE}.dir"
  return 1
}

utm_lock_end() {
  if [[ -n "${UTM_LOCK_FD}" && "${UTM_LOCK_FD}" != mkdir ]]; then
    zsystem flock -u ${UTM_LOCK_FD} 2>/dev/null || true
    UTM_LOCK_FD=""
  elif [[ "${UTM_LOCK_FD}" == mkdir ]]; then
    rmdir "${LOCK_FILE}.dir" 2>/dev/null || true
    UTM_LOCK_FD=""
  fi
}

write_status_json() {
  mkdir -p "${STATE_DIR}"
  local ok_flag="${1:-true}"
  local alive="false"
  master_alive "${CURRENT_HOST}" && alive="true"
  python3 - "${STATUS_JSON}" "${ok_flag}" "${CURRENT_HOST}" "${SSH_USER}" "${REMOTE_DIR}" \
    "$(posix_to_win_path)" "${MASTER_PID:-}" "${alive}" "${SYNC_ENGINE}" "${RSYNC_REMOTE}" <<'PY'
import json, os, sys
from datetime import datetime, timezone
path, ok, ip, user, remote, win, pid, alive, engine, rsync = sys.argv[1:11]
data = {
    "ok": ok.lower() == "true",
    "ip": ip,
    "ssh_user": user,
    "remote_dir": remote,
    "win_root": win,
    "master_pid": int(pid) if str(pid).isdigit() else None,
    "master_alive": alive.lower() == "true",
    "last_sync": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "engine": engine,
    "rsync_remote": rsync,
    "written_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}
os.makedirs(os.path.dirname(path), exist_ok=True)
with open(path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2)
    f.write("\n")
PY
}

start_master() {
  local host="$1"
  local persist="${2:-300}"
  local -a ssh_cmd

  if master_alive "${host}"; then
    return 0
  fi

  mkdir -p "${STATE_DIR}"

  if [[ -e "${CTL_SOCK}" ]]; then
    ssh -O exit -o ControlPath="${CTL_SOCK}" "$(remote_target "${host}")" >/dev/null 2>&1 || true
    rm -f "${CTL_SOCK}"
  fi

  ssh_cmd=(
    ssh
    "${SSH_OPTS[@]}"
    -o ControlMaster=yes
    -o ControlPath="${CTL_SOCK}"
  )

  if [[ "${persist}" == "no" ]]; then
    select_port_forwards
    if (( ${#FORWARD_SSH} )); then
      ssh_cmd+=(-o ExitOnForwardFailure=yes)
      ssh_cmd+=("${FORWARD_SSH[@]}")
    else
      ssh_cmd+=(-o ExitOnForwardFailure=no)
    fi
    ssh_cmd+=(-N -M -o ControlPersist=no)
    "${ssh_cmd[@]}" "$(remote_target "${host}")" &
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
  fi

  # One-shot mux: no LocalForward (leftover web ports must not take the master down).
  ssh_cmd+=(-nNf -o ControlPersist="${persist}" -o ExitOnForwardFailure=no)
  "${ssh_cmd[@]}" "$(remote_target "${host}")" || return 1
  local j
  for j in {1..25}; do
    if master_alive "${host}"; then
      MASTER_PID="$(ssh -O check -o ControlPath="${CTL_SOCK}" "$(remote_target "${host}")" 2>&1 | grep -Eo '[0-9]+' | head -1 || true)"
      print -r -- "${MASTER_PID}" > "${STATE_DIR}/master.pid"
      return 0
    fi
    sleep 0.2
  done
  return 1
}

stop_master() {
  local host="${1:-$CURRENT_HOST}"
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

ensure_master() {
  local host="$1"
  local persist="${2:-300}"
  if master_alive "${host}"; then
    return 0
  fi
  utm_lock_begin || return 1
  {
    if master_alive "${host}"; then
      utm_lock_end
      return 0
    fi
    start_master "${host}" "${persist}"
    local rc=$?
    utm_lock_end
    return "${rc}"
  }
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
    if ssh_run "${host}" "\"${candidate}\" --version" >/dev/null 2>&1; then
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

ensure_remote_dir() {
  local host="$1"
  local win_path
  win_path="$(posix_to_win_path)"
  win_path="${win_path//\'/\'\'}"
  ssh_ps "${host}" "New-Item -ItemType Directory -Force -Path '${win_path}' | Out-Null" \
    >/dev/null 2>&1 || true
}

rsync_once() {
  local host="$1" rsync_remote="$2"
  local -a excludes rsync_args
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
    --exclude 'keys/'
    --exclude 'artifacts/rosters/'
    --exclude 'logs/'
    --exclude 'Logs/'
    --exclude '*.log'
    --exclude '.DS_Store'
    --exclude '*.user'
    --exclude '*.wpftmp.csproj'
  )

  rsync_args=(
    -a --omit-dir-times --no-perms --no-group --no-owner
    --itemize-changes --human-readable
    -e "ssh ${SSH_OPTS[*]} -o ControlMaster=no -o ControlPath=${CTL_SOCK}"
    --rsync-path="\"${rsync_remote}\""
    "${excludes[@]}"
  )

  if (( SYNC_MIRROR )); then
    rsync_args+=(
      --delete
      --filter 'protect keys/'
      --filter 'protect artifacts/rosters/'
      --filter 'protect logs/'
      --filter 'protect Logs/'
      --filter 'protect TestResults/'
      --filter 'protect *.log'
    )
  fi

  rsync "${rsync_args[@]}" \
    "${LOCAL_DIR}/" \
    "$(remote_target "${host}"):${REMOTE_DIR}/"
}

tar_once() {
  local host="$1"
  local dest stamp list
  dest="$(posix_to_win_path)"
  stamp="${STATE_DIR}/last-tar-sync"
  list="$(mktemp -t utm-dev-bridge-files)"
  COPYFILE_DISABLE=1
  export COPYFILE_DISABLE

  (
    cd "${LOCAL_DIR}" || exit 1
    if [[ -f "${stamp}" ]]; then
      find . \
        \( -name .git -o -name node_modules -o -name bin -o -name obj \
           -o -name build -o -name TestResults -o -name .vs -o -name .idea \
           -o -name keys -o -name logs -o -name Logs \
           -o -path './artifacts/rosters' \) -prune -o \
        -name '*.log' -prune -o \
        -type f -newer "${stamp}" -print
    else
      find . \
        \( -name .git -o -name node_modules -o -name bin -o -name obj \
           -o -name build -o -name TestResults -o -name .vs -o -name .idea \
           -o -name keys -o -name logs -o -name Logs \
           -o -path './artifacts/rosters' \) -prune -o \
        -name '*.log' -prune -o \
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

ensure_connected() {
  local persist="${1:-300}"
  mkdir -p "${STATE_DIR}" "${HOME}/.config"
  need_cmd ssh || return 1
  [[ -f "${SSH_KEY}" ]] || { err "SSH_KEY not found: ${SSH_KEY}"; return 1; }
  [[ -d "${LOCAL_DIR}" ]] || { err "LOCAL_DIR missing: ${LOCAL_DIR}"; return 1; }

  ensure_vm_started
  local ip
  if ! ip="$(resolve_vm_ip)"; then
    err "Could not resolve VM IPv4. Set VM_IP in ${CONFIG_FILE}"
    return 1
  fi
  CURRENT_HOST="${ip}"
  print -r -- "${ip}" > "${LAST_IP_FILE}"

  if ! ensure_master "${ip}" "${persist}"; then
    err "SSH master failed for ${SSH_USER}@${ip}"
    write_status_json false
    return 1
  fi

  if ! choose_sync_engine "${ip}"; then
    warn "Guest has neither rsync nor tar"
  fi
  write_status_json true
  return 0
}
