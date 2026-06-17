#!/bin/zsh
# run-wpf.sh
# Hybrid Mac (host) + UTM Windows 11 VM launcher for BusBuddy WPF.
#
# What it does:
# - Fast preflight: dotnet restore + build of the solution with -p:EnableWindowsTargeting=true
#   (catches compile errors on Mac before switching focus to the VM).
# - Ensures the UTM VM named "Windows" (or the one with the matching UUID) is running.
#   Starts it (visible window) if it is stopped. Polls until running.
# - Tries to auto-discover the shared project root *inside the guest* and launch the WPF
#   app detached (so the window appears on the VM desktop while you stay on Mac terminal).
# - If guest exec/launch isn't ready yet (early boot, no guest agent, or first login),
#   prints crystal-clear manual instructions you can copy-paste into a PowerShell window inside the VM.
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

# Helpful host IP for when the guest needs to reach Mac-hosted Docker Postgres
HOST_IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo 'unknown')"
echo "${PFX} Mac host IP for VM (Postgres etc.): ${HOST_IP}   (example BUSBUDDY_CONNECTION: Host=${HOST_IP};...)"

# 3. Attempt automatic launch inside guest via utmctl exec + discovery (best UX when guest agent ready)
echo "${PFX} Attempting auto-launch of WPF inside the VM (via guest exec)..."

# The discovery + launch command. We run a PowerShell one-liner that:
# - Searches likely drives + PSDrives for BusBuddy.sln (mirrors the logic in utm_run_in_vm.ps1)
# - If found, Start-Process dotnet run detached so the GUI appears on the desktop and this returns quickly.
# - Prints the used path so we can show it here.
LAUNCH_PS=$'
$ErrorActionPreference = "SilentlyContinue"
$roots = @()
Get-PSDrive -PSProvider FileSystem | % { if ($_.Root) { $roots += $_.Root } }
# UTM SPICE shares commonly appear as Z: localhost@9843 (with "spice clipboard" folder).
# Prioritize Z: and anything with localhost@ or spice in the name/root.
$roots += @("Z:\\", "Y:\\", "X:\\", "E:\\", "D:\\", "C:\\")
$roots += @("Z:\\Shared with Windows", "D:\\Shared with Windows", "Z:\\BusBuddy-3")
$found = $null
foreach ($r in ($roots | Select -Unique)) {
  if (-not (Test-Path $r)) { continue }
  $m = Get-ChildItem -Path $r -Filter "BusBuddy.sln" -Recurse -Depth 5 -ErrorAction SilentlyContinue |
       ? { $_.FullName -notlike "*\\bin\\*" -and $_.FullName -notlike "*\\obj\\*" -and $_.FullName -notlike "*\\Archive\\*" } |
       Select -First 1
  if ($m) { $found = $m.DirectoryName; break }
}
if (-not $found) { Write-Output "NOTFOUND"; exit 1 }
Set-Location $found
Write-Output "FOUND:$found"
Start-Process -FilePath "dotnet" -ArgumentList "run","--project","BusBuddy.WPF/BusBuddy.WPF.csproj" -WorkingDirectory $found -WindowStyle Normal
exit 0
'

# Run the discovery+launch. Capture stdout (the FOUND line or NOTFOUND).
# We use --cmd powershell.exe with the -Command payload.
GUEST_OUT=$(utmctl exec "${VM_NAME}" \
  --cmd 'powershell.exe' \
  -- '-NoProfile' '-NonInteractive' '-Command' "${LAUNCH_PS}" 2>&1 || true)

echo "${PFX} Guest exec output (may be empty if agent not ready yet):"
echo "${GUEST_OUT}" | sed 's/^/    /'

if echo "${GUEST_OUT}" | grep -q "FOUND:" ; then
  GUEST_ROOT=$(echo "${GUEST_OUT}" | grep "FOUND:" | head -1 | sed 's/.*FOUND://')
  echo ""
  echo "✅ Launched (or launch requested) from inside guest at: ${GUEST_ROOT}"
  echo "   The BusBuddy WPF window should now appear on the Windows desktop in your UTM VM."
  echo "   If you do not see it, check the VM window (it may have been minimized or on another virtual desktop)."
  exit 0
fi

# 4. Fallback: VM is up (or starting), but we couldn't auto-launch. Give perfect manual instructions.
echo ""
echo "⚠️  Could not auto-launch via guest exec (this is common on first boot, before full login, or if UTM guest agent integration is not fully enabled)."
echo ""
echo "✅ The VM should now be running and visible."
echo "   Switch to the UTM 'Windows' window, log in if prompted, open a PowerShell (or Windows Terminal) *inside the VM*, and run ONE of:"
echo ""
echo "   # Preferred (robust discovery of your 'Shared with Windows' or other mount + GEE + optional license file):"
echo "   .\\utm_run_in_vm.ps1"
echo ""
echo "   # Or the direct equivalent (once you are cd'ed to the shared project root inside the guest):"
echo "   dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj"
echo ""
echo "   (The .ps1 also handles GEE key from the shared keys/ folder and will remind about Syncfusion license.)"
echo ""
echo "Host IP for Docker Postgres from inside VM: ${HOST_IP}"
echo "Example connection override (if not using the default in App):"
echo "   \$env:BUSBUDDY_CONNECTION = \"Host=${HOST_IP};Port=5432;Database=busbuddy;Username=busbuddy;Password=...\""
echo ""
echo "Tip: Put the project on a share that appears early (or map a drive letter in your VM login script) for the smoothest experience."
echo "     You can also open the shared folder in Explorer inside the VM and double-click or right-click 'Run with PowerShell' on utm_run_in_vm.ps1 ."
echo ""
echo "Done. Re-run ./run-wpf.sh after the desktop is fully up if you want another launch attempt."

exit 0
