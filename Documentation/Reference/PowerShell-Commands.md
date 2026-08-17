# PowerShell commands (legacy — removed)

**Status**: Deprecated and removed under [issue #15](https://github.com/Bigessfour/BusBuddy-3/issues/15).

The learning-era `bb-*` helpers (`bb-build`, `bb-run`, `bb-health`, `bb-anti-regression`, `bb-xaml-validate`, BusBuddy-Development, profile modules) are **gone**. Do not `Import-Module` archived paths.

## Use instead

```bash
dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true
dotnet test BusBuddy.sln -c Release --no-build \
  --filter "Category!=Integration&Category!=InMemoryFlaky"
.github/scripts/validate-ci-local.sh
./run-wpf.sh   # Mac → UTM Windows VM
```

Optional Windows-only helpers still under `Scripts/` (not `bb-*` modules):

- `Scripts/Validate-Dependencies.ps1` — license / package checks
- `Scripts/Manage-Dependabot.ps1`, `Scripts/Analyze-PullRequest.ps1`, etc.

Agent guidance: [AGENTS.md](../../AGENTS.md), [.github/copilot-instructions.md](../../.github/copilot-instructions.md).
