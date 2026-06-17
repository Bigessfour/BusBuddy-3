#!/usr/bin/env bash
# Install Syncfusion WPF Agent Skills for Cursor (local only — .agents/skills/ is gitignored).
# Docs: https://help.syncfusion.com/wpf/skills/component-skills
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$ROOT"

if ! command -v node >/dev/null 2>&1; then
  echo "Node.js >= 16 required. Install from https://nodejs.org/" >&2
  exit 1
fi

MODE="${1:-all}"

if [[ "$MODE" == "minimal" ]]; then
  echo "Interactive install — select BusBuddy-relevant skills (datagrid, charts, button, busy-indicator, navigation-drawer, treeview, scheduler, accordion, textboxext, skin-manager)."
  npx skills add syncfusion/wpf-ui-components-skills
else
  echo "Installing all Syncfusion WPF component skills into .agents/skills/ ..."
  npx skills add syncfusion/wpf-ui-components-skills -y
fi

echo ""
echo "Done. Vendor skills: .agents/skills/ (gitignored)"
echo "BusBuddy overlay (committed): .cursor/skills/syncfusion-wpf-busbuddy/"
echo "List: npx skills list | Update: npx skills update"
