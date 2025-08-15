# BusBuddy PowerShell Guidelines (Doc‑First)

Authoritative docs:
- Module guidelines: https://learn.microsoft.com/powershell/scripting/developer/module/writing-a-windows-powershell-module
- Output streams: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
- Exceptions: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-exceptions
- ShouldProcess: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/cmdlet-overview#confirming-impactful-operations
- Get-Command: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/get-command

Comment to guide Copilot:
- Explain intent before code.
- Example: “# Get log files and count them — show a compact table.”

Cmdlet validation pattern:
```powershell
# Validate tool/cmdlet availability
if (-not (Get-Command Invoke-Pester -ErrorAction SilentlyContinue)) {
  Write-Warning 'Pester not available. Install with: Install-Module Pester -Scope CurrentUser'
  return
}
```

Error handling + streams:
```powershell
try {
  # Operation
} catch {
  Write-Error ("Operation failed: {0}" -f $_.Exception.Message)
}
```

ShouldProcess pattern:
```powershell
[CmdletBinding(SupportsShouldProcess)]
param()
if ($PSCmdlet.ShouldProcess($Target, 'Do-Thing')) {
  # perform action
}
```

Pipeline safety (7.5+):
```powershell
# Build then test; report failures via Write-Error
dotnet build && dotnet test || Write-Error 'Build or test failed'
```

FileSystemWatcher essentials:
```powershell
$w = [IO.FileSystemWatcher]::new($Path,'*.cs')
$w.IncludeSubdirectories = $true
$w.EnableRaisingEvents = $true
# Register events (Changed/Created/Renamed) and debounce as needed
```

Encoding and output:
- Use UTF8 for files: $PSDefaultParameterValues['Out-File:Encoding'] = 'UTF8'
- Prefer Write-Output / Write-Information over Write-Host.

Anti-pattern (avoid):
```powershell
# BAD: piping preferences/variables into Select-Object
$ErrorActionPreference | Select-Object Name
```

Disable Copilot for complex scripts (per-file):
- VS Code: “Copilot: Disable for This File” from Command Palette while bbCleanup.ps1 is active.
- Alternatively, toggle inline suggestions in the status bar for the current file.
- Re-enable only after stabilizing patterns in this guideline.

Notes:
- Keep changes minimal and documented.
- Reference these links in code comments to maintain doc-first compliance.
