#!/bin/zsh
# utm-route-schedule-smoke.sh
# Mac → UTM: quit WPF, Debug rebuild, testhost, launch, Bus 5 AM Schedule + Print.

emulate -L zsh
set -u
setopt pipefail

typeset -r SCRIPT_DIR="${0:A:h}"
typeset -r REPO_ROOT="${SCRIPT_DIR:h}"
typeset -r BRIDGE="${SCRIPT_DIR}/utm-dev-bridge.sh"
typeset -r FILTER='FullyQualifiedName~RouteScheduleViewModelTests|FullyQualifiedName~RouteAssignmentViewTests|FullyQualifiedName~RouteManagementViewModelTests|FullyQualifiedName~RouteManagementViewTests'

DO_SYNC=1
TESTHOST_ONLY=0

while (($#)); do
	case "$1" in
	-h | --help)
		print -r -- "Usage: $0 [--no-sync] [--testhost-only]"
		exit 0
		;;
	--no-sync)
		DO_SYNC=0
		shift
		;;
	--testhost-only)
		TESTHOST_ONLY=1
		shift
		;;
	*)
		print -r -- "Unknown: $1" >&2
		exit 2
		;;
	esac
done

[[ -f ${BRIDGE} ]] || {
	print -r -- "missing ${BRIDGE}" >&2
	exit 1
}

# shellcheck disable=SC1091
source "${SCRIPT_DIR}/utm-dev-bridge.inc"
load_config
init_ssh_opts
ensure_connected 300 || exit 1
win_root="$(posix_to_win_path)"
win_root_ps=${win_root:gs/\'/\'\'/}

if ((DO_SYNC)); then
	"${BRIDGE}" sync || exit 1
fi

info "close leftover WPF + rebuild Debug"
"${BRIDGE}" exec -- "
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
" || exit 1

info "testhost: ${FILTER}"
"${BRIDGE}" test --no-sync --filter "${FILTER}" || exit 1

if ((TESTHOST_ONLY)); then
	ok "testhost-only complete"
	exit 0
fi

info "launch WPF"
"${BRIDGE}" launch --no-sync || exit 1

info "UIA Schedule + Print (schtasks /IT)"
"${BRIDGE}" exec -- "
\$ErrorActionPreference = 'Continue'
\$script = '${win_root_ps}\\Scripts\\utm-route-schedule-smoke.ps1'
\$wrapper = \"\$env:TEMP\\busbuddy-schedule-uia-run.ps1\"
@\"\`
\$ErrorActionPreference = 'Continue'
& '\$script'
\"@ | Set-Content -Path \$wrapper -Encoding UTF8
schtasks /Delete /TN BusBuddyScheduleUia /F 2>\$null | Out-Null
schtasks /Create /TN BusBuddyScheduleUia /SC ONCE /ST 23:59 /RL LIMITED /IT /F /TR \"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \`\"\$wrapper\`\"\" /RU ${SSH_USER} | Out-Null
schtasks /Run /TN BusBuddyScheduleUia | Out-Null
Start-Sleep -Seconds 50
Get-ChildItem \"\$env:TEMP\\busbuddy-uia-schedule-*.log\" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1 | ForEach-Object { Get-Content \$_.FullName }
if (-not (Get-ChildItem \"\$env:TEMP\\busbuddy-uia-schedule-*.log\" -ErrorAction SilentlyContinue)) { Write-Output 'UIA_OUT_MISSING' }
" || exit 1

info "score logs"
"${BRIDGE}" exec -- "
\$ErrorActionPreference = 'Continue'
\$since = (Get-Date).AddMinutes(-30).ToString('yyyy-MM-dd HH:mm:ss')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File '${win_root_ps}\\Scripts\\score-route-schedule-logs.ps1' -Root '${win_root_ps}' -Since \$since
Write-Output ('SCORE_EXIT=' + \$LASTEXITCODE)
" || exit 1

ok "route schedule smoke complete"
