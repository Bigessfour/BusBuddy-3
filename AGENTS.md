# BusBuddy agent instructions

AI agents (Cursor, Copilot, Claude, etc.) working in this repo should follow these pointers.

## Primary standards

- **Full technical rules**: [.github/copilot-instructions.md](.github/copilot-instructions.md) — architecture, Syncfusion, Serilog, RAG/MCP, anti-regression.
- **CI/CD workflow (solo developer)**: same file, section **Solo developer CI/CD workflow** — branch → PR → gates → auto-merge.

## CI/CD quick reference

| Step | Action |
|------|--------|
| Branch | `feature/<short-description>` from `master` |
| Open PR | Target `master`; auto-merge enables automatically |
| Merge gates | `Build & Test`, `Security (CodeQL)` must pass |
| Merge | Squash auto-merge when gates pass (no reviewer required) |
| Direct push to `master` | Blocked by branch rules — use PRs |
| Optional | Run **Docker CI simulation** workflow manually |
| Release | Push to `master` publishes WPF artifact (non-blocking job) |

## Local checks before PR

```bash
dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true
dotnet test BusBuddy.sln -c Release --no-build \
  --filter "Category!=Integration&Category!=InMemoryFlaky"
```

## Repo governance setup (one-time)

```bash
.github/scripts/setup-solo-ci-governance.sh
```

Requires `gh` CLI with admin access to configure auto-merge, branch ruleset, and Dependabot security alerts.
