#!/bin/zsh
# run-wpf.sh
# Hybrid Mac (host) + UTM Windows 11 VM launcher for BusBuddy WPF.
#
# What it does:
# - Ensures Mac Docker Postgres is running (Scripts/ensure-postgres-docker.sh).
# - Fast preflight: dotnet restore + build of the solution with -p:EnableWindowsTargeting=true
#   (catches compile errors on Mac before switching focus to the VM).
# - Ensures the UTM VM named "Windows" (or the one with the matching UUID) is running.
#   Starts it (visible window) if it is stopped. Polls until running.
# - Syncs to C:\dev\BusBuddy-3 over SSH and launches WPF in the logged-in
#   Macbook desktop session (schtasks /IT). utmctl exec is not used for launch.
# - If SSH launch isn't ready yet, prints manual commands for C:\dev\BusBuddy-3.
#
# Prerequisites on Mac:
#   - UTM installed (brew install --cask utm) + utmctl in PATH.
#   - The BusBuddy-3 folder (or a parent) shared into the VM as a directory share.
#     (Your share may be labeled "Shared with Windows" inside the guest.)
#   - .NET 9 SDK on Mac (for the preflight; the real WPF run happens in the VM).
#
# First-time in the Windows VM (one-time):
#   - Install .NET 9 SDK (ARM64).
#   - Set your Syncfusion license so you don't get trial watermarks:
#       [Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "paste-your-key-here", "User")
#     Or drop the key (single line) into keys/SYNCFUSION_LICENSE_KEY.txt on the Mac side
#     (it will sync via your shared folder and the inside-VM script will pick it up).
#   - Optional but nice: copy keys/bus-buddy-gee-key.json (produced by .github/scripts/...) into the shared tree.
#
# Usage:
#   ./run-wpf.sh
#   (or: bash run-wpf.sh)
#
# After this, the WPF main window should appear inside your already-open or newly-started VM.
# Use the VM's desktop to interact with Dashboard / Reports / Map / Students etc.

set -u

ROOT="$(cd "$(dirname "${0}")" && pwd)"
cd "${ROOT}"

VM_NAME="Windows"
VM_UUID="394EDB53-19DC-4E99-A325-9FCDFD0B6F62"   # fallback; name is usually sufficient

PFX="==>"

echo "${PFX} BusBuddy WPF hybrid launcher (Mac host + UTM VM)"
echo "${PFX} Project root: ${ROOT}"

echo "${PFX} Ensuring Mac Docker Postgres is up..."
if ! "${ROOT}/Scripts/ensure-postgres-docker.sh"; then
  echo "ERROR: Could not start Postgres. Start Docker Desktop on the Mac, then re-run ./run-wpf.sh" >&2
  exit 1
fi

# Cursor/PowerShell sessions often put /usr/local/share/dotnet first. That install
# currently has only the .NET 11 preview SDK, so global.json (9.0.303 + latestMinor)
# cannot resolve. Prefer the user-local 9.x SDK used by this repo.
if [[ -x "${HOME}/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="${HOME}/.dotnet"
  export PATH="${DOTNET_ROOT}:${PATH}"
fi
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet not found. Install .NET 9 SDK (https://aka.ms/dotnet/download)." >&2
  exit 1
fi
echo "${PFX} Using $(command -v dotnet)  (DOTNET_ROOT=${DOTNET_ROOT:-unset})"
dotnet --list-sdks | sed 's/^/    /'

# 1. Host preflight build (fast feedback, same flag the VM will use)
# We primarily build the runnable WPF app (plus Core via restore). This avoids transient test gaps
# (e.g. GapsCoverageTests calling not-yet-implemented methods) from blocking your "see the UI" workflow.
# Run `.github/scripts/validate-ci-local.sh` or `dotnet test ...` separately when you want the full gate.
echo "${PFX} Preflight: restore + build WPF on Mac (EnableWindowsTargeting) — catches most issues before VM focus"
dotnet restore "BusBuddy.sln" -p:EnableWindowsTargeting=true --verbosity minimal
dotnet build "BusBuddy.WPF/BusBuddy.WPF.csproj" -c Debug --no-restore -p:EnableWindowsTargeting=true /p:TreatWarningsAsErrors=false /p:WarningLevel=1

if [[ $? -ne 0 ]]; then
  echo "ERROR: Preflight build failed. Fix errors above, then re-run ./run-wpf.sh" >&2
  exit 1
fi
echo "${PFX} Preflight build OK (WPF project compiles under the Windows TFM)."

# 2. Ensure the VM is running (use already-open if possible; start only if stopped)
# Note: utmctl status typically reports "started" (booted/usable) or "stopped".
# We treat both "started" and "running" as ready states so we don't re-start an already-open VM.
echo "${PFX} Checking UTM VM status (${VM_NAME})..."
STATUS="$(utmctl status "${VM_NAME}" 2>/dev/null || utmctl status "${VM_UUID}" 2>/dev/null || echo 'stopped')"
echo "${PFX} Current status: ${STATUS}"

if [[ "${STATUS}" == "started" || "${STATUS}" == "running" ]]; then
  echo "${PFX} VM is already started — using the open session (no start command sent)."
else
  echo "${PFX} Starting VM '${VM_NAME}' (will open/show the Windows desktop)..."
  # Do NOT use --hide: user wants to see / interact with the WPF UI in the VM window.
  if ! utmctl start "${VM_NAME}" 2>/dev/null && ! utmctl start "${VM_UUID}" 2>/dev/null; then
    echo "Note: start command returned non-zero (this is often harmless if the VM is already in the process of starting)."
  fi

  # Poll until the VM reports a ready state ("started" or "running").
  # First boot + user login inside Windows can easily take 45-120s.
  echo -n "${PFX} Waiting for VM to become usable (started/running)"
  for i in {1..120}; do
    STATUS="$(utmctl status "${VM_NAME}" 2>/dev/null || utmctl status "${VM_UUID}" 2>/dev/null || echo 'stopped')"
    if [[ "${STATUS}" == "started" || "${STATUS}" == "running" ]]; then
      echo " OK (${STATUS})"
      break
    fi
    echo -n "."
    sleep 2
  done
  if [[ "${STATUS}" != "started" && "${STATUS}" != "running" ]]; then
    echo ""
    echo "VM did not report 'started' or 'running' after timeout."
    echo "Open UTM.app, make sure the Windows desktop is visible and you are logged in, then re-run this script."
    echo "You can also just switch to the VM and run the manual command shown at the end."
  fi
fi

# Helpful host IP for when the guest needs to reach Mac-hosted Docker Postgres.
# UTM shared network: Mac is 192.168.64.1 (stable). Fall back to en0 for bridged/other VMs.
if ifconfig 2>/dev/null | grep -q 'inet 192.168.64.1 '; then
  HOST_IP="192.168.64.1"
else
  HOST_IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo 'unknown')"
fi
echo "${PFX} Mac host IP for VM (Postgres etc.): ${HOST_IP}   (example BUSBUDDY_CONNECTION: Host=${HOST_IP};...)"
if [[ "${HOST_IP}" != "unknown" ]]; then
  mkdir -p "${ROOT}/keys"
  printf '%s\n' "${HOST_IP}" > "${ROOT}/keys/mac-host-ip.txt"
  echo "${PFX} Wrote keys/mac-host-ip.txt for the VM launcher."
fi

# 3. Sync NTFS copy + launch in the logged-in Macbook session (SSH, not utmctl exec).
BRIDGE="${ROOT}/Scripts/utm-dev-bridge.sh"
echo "${PFX} Syncing + launching WPF via ${BRIDGE} (C:\\dev\\BusBuddy-3, interactive session)..."
if [[ -x "${BRIDGE}" || -f "${BRIDGE}" ]]; then
  if "${BRIDGE}" launch; then
    echo ""
    echo "Launched from C:\\dev\\BusBuddy-3 in the Macbook desktop session."
    echo "The BusBuddy WPF window should appear on the Windows desktop in UTM."
    echo "If you need guest logs on the Mac: ./Scripts/utm-dev-bridge.sh pull-logs"
    exit 0
  fi
  echo "${PFX} Bridge launch failed — print manual steps (VM may still be logging in)."
else
  echo "ERROR: missing ${BRIDGE}" >&2
fi

# 4. Fallback: VM is up, SSH launch did not succeed.
echo ""
echo "Could not auto-launch via SSH. Log into the UTM Windows desktop, then either:"
echo ""
echo "  # From this Mac (preferred):"
echo "  ./Scripts/utm-dev-bridge.sh doctor"
echo "  ./Scripts/utm-dev-bridge.sh launch"
echo ""
echo "  # Inside the VM, from the NTFS copy only:"
echo "  cd C:\\dev\\BusBuddy-3"
echo "  powershell -NoProfile -ExecutionPolicy Bypass -File .\\utm_run_in_vm.ps1"
echo ""
echo "  # First-time OpenSSH bootstrap (guest agent / utmctl exec only):"
echo "  # Scripts/Enable-BusBuddyOpenSSH.ps1"
echo ""
echo "Host IP for Docker Postgres from inside VM: ${HOST_IP}"
echo "Example connection override:"
echo "   \$env:BUSBUDDY_CONNECTION = \"Host=${HOST_IP};Port=5432;Database=busbuddy;Username=busbuddy;Password=...\""
echo ""
echo "Done. Re-run ./run-wpf.sh after the desktop is fully up if you want another launch attempt."

exit 0
