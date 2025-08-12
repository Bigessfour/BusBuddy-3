# Legacy Scripts & Artifacts Archive

Archived on: 2025-08-12

Purpose: Consolidate redundant, phase-specific, or superseded scripts/log/index artifacts to reduce active surface area while preserving historical context. Original full contents remain retrievable via Git history unless explicitly copied below.

| Original Path | Archived Filename | Category | Rationale | Replacement / Successor |
|---------------|------------------|----------|-----------|--------------------------|
| PowerShell/Scripts/Query-Students-Azure.ps1 | (Deleted) | Duplicate (Removed) | Duplicate of root script removed | Use root script only |
| PowerShell/Scripts/Enhanced-Test-Output.ps1 | (Deleted) | Duplicate (Removed) | Fully removed (canonical functions version retained) | Keep functions version |
| PowerShell/Scripts/MinimalOutputCapture.ps1 | (Deleted) | Duplicate (Removed) | Superseded by module utility | Use functions version |
| PowerShell/Scripts/WileySeed.ps1 | (Deleted) | Feature (Deferred, Removed) | Deferred feature script removed | Future district seeding service |
| PowerShell/Scripts/Test-WileyDataSeeding.ps1 | (Deleted) | Feature (Deferred, Removed) | Fully removed (deferred test harness) | Future integration tests |
| PowerShell/Testing/Test-RouteService.ps1 | (Deleted) | Phase 4 Test Harness (Removed) | Replaced by bbTest | `bbTest` / NUnit tasks |
| PowerShell/Testing/Run-Phase4-NUnitTests-Modular.ps1 | (Deleted) | Phase 4 Bridge (Removed) | Historic bridge no longer needed | `bbTest` |
| PowerShell/Scripts/Quick-Azure-Test.ps1 | (Deleted) | Ad-hoc (Removed) | Superseded by bbHealth / bbValidateDatabase | `bbHealth` |
| PowerShell/Scripts/test-module-load.ps1 | (Deleted) | Ad-hoc (Removed) | Module load validation internal to bbHealth | `bbHealth` |
| PowerShell/Scripts/Debug-DIContainer.ps1 | (Deleted) | Diagnostic (Removed) | DI validation moved to bbHealth future | `bbHealth` (future DI section) |
| PowerShell/Scripts/Runtime-Capture-Monitor.ps1 | (Deleted) | Diagnostic (Removed) | Superseded by structured logging | Serilog + future telemetry |
| raw-index.csv | (Deleted) | Generated Index (Removed) | Large raw link list removed | Use curated docs / fetch guide |
| raw-index.json | (Deleted) | Generated Index (Removed) | Large raw JSON list removed | Same as above |
| RAW-LINKS.txt | (Deleted) | Generated Links (Removed) | Superseded by FILE-FETCHABILITY-GUIDE | FILE-FETCHABILITY-GUIDE.md |
| RAW-LINKS-PINNED.txt | (Deleted) | Generated Links (Removed) | Superseded by FILE-FETCHABILITY-GUIDE | FILE-FETCHABILITY-GUIDE.md |

## Notes
1. Scripts retained here may be selectively refactored post-MVP; keeping them isolated prevents accidental reintroduction of deprecated patterns (e.g., Write-Host usage).
2. Where content was large (indexes / logs), only the filename was moved; retrieve full contents via: `git show <commit>:<original path>` if required.
3. Active canonical student query script: `./Query-Students-Azure.ps1` at repository root.
4. Testing now routed exclusively through the BusBuddy PowerShell module commands (`bbTest`, `bbHealth`, etc.).

### Hard Archived Definition
Hard Archived = Original active script replaced in-place with a minimal stub (comment + terminating throw) to prevent execution while leaving path history intact.

## Future Cleanup Candidates
Evaluate after MVP stabilization: additional Azure setup script consolidation, further diagnostic script retirement.
