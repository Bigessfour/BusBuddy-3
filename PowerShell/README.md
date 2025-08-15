PowerShell folder for BusBuddy

Structure:
- Modules/   -> Custom PowerShell modules used by BusBuddy (keep limited)
- Scripts/   -> Standalone scripts and helpers
- Profiles/  -> PowerShell profile scripts (Import-BusBuddyModule.ps1 etc.)

Guidelines:
- Use PowerShell 7.5.2 in CI and local dev.
- Avoid Write-Host; prefer Write-Information/Write-Output.
- Run ScriptAnalyzer before committing: Invoke-ScriptAnalyzer -Path . -Recurse
