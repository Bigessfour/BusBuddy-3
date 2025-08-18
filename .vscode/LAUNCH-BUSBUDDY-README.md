# BusBuddy focused VS Code launch configurations

This repo includes a focused, minimal set of VS Code debug configurations for BusBuddy PowerShell development.

Files created/modified

- `.vscode/launch.busbuddy.json` — focused PowerShell debug configurations (saved but not required).
- `.vscode/launch.json` — now replaced with the BusBuddy-only configurations (backup saved as `.vscode/launch.json.backup`).

Why this exists

- Keeps the Run/Debug picker clean so you see only BusBuddy PowerShell configurations.
- Makes it easy to debug `Microsoft.PowerShell_profile.ps1`, `BusBuddy.ModuleManager.ps1`, and `BusBuddy-GitKraken.ps1`.

How to use

1. Open the Debug panel in VS Code (Ctrl+Shift+D).
2. Choose a configuration (examples):
   - `PowerShell: Debug BusBuddy Module` — step through module manager.
   - `PowerShell: Debug GitKraken Module` — debug GitKraken CLI integration.
   - `PowerShell: Debug Profile` — run the repo profile directly.
   - `PowerShell: Debug BusBuddy Environment Test` — runs profile with `-SkipGitKraken` for quick environment checks.
3. Set breakpoints in the target PowerShell file and press F5 to start debugging.

Commands (PowerShell) to run locally

```powershell
# Run profile in current shell (verbose)
. ./PowerShell/Profiles/Microsoft.PowerShell_profile.ps1 -Verbose

# Run profile but skip GitKraken initialization
. ./PowerShell/Profiles/Microsoft.PowerShell_profile.ps1 -Verbose -SkipGitKraken

# Debug a single module script (example)
pwsh -NoProfile -File ./PowerShell/BusBuddy-GitKraken.ps1
```

Reverting to the previous launch.json

- A backup of the previous `launch.json` was saved at `.vscode/launch.json.backup`.
- To restore:

```powershell
cp .vscode/launch.json.backup .vscode/launch.json
# or in PowerShell
Copy-Item .vscode/launch.json.backup -Destination .vscode/launch.json -Force
```

Notes

- If you prefer to keep the Python / Chrome configs visible, open `.vscode/launch.json.backup` to manually copy entries back.
- If you'd rather keep the focused file separate, you can open `.vscode/launch.busbuddy.json` and use its configurations directly in the Debug panel.

If you'd like, I can also add a small VS Code task to switch between 'busbuddy' and 'full' launch.json automatically.
