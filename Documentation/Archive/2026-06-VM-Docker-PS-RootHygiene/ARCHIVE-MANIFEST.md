# 2026-06 VM/Docker + PS Deprecation + Root Hygiene Round

**Date**: 2026-06-15 (continuation of feature/hygiene-vm-dedup-ps-deprecation-wsl-keys-mcp-ci-cleanup)
**Context**: After UTM Windows 11 ARM VM provisioning for WPF, Docker/Postgres modernization for Mac hybrid dev, previous PS purge, and ongoing root cleanup.

## Goals of this round
- Remove non-participating files (no longer referenced in sln/csproj, CI, current README, active dev flow).
- Archive historical one-off reports, old plans, and deprecation remnants instead of leaving them at root polluting the tree.
- Consolidate script locations (continue moving away from root and the old Powershell/ tree).
- File things into recommended locations (Archive for retired, Scripts/ or Documentation/ for active, root only for top-level essentials).
- Result: clean working tree suitable for PR + green CI on the hygiene branch.

## What was archived here (moved, not deleted)
See the moved files in this folder and subfolders. Original locations noted in git history (renames).

### High-level categories archived
1. Root-level historical .md / reports (one-time audits, old implementation notes, workflow cleanups).
2. Loose root *.ps1 "fix-*/legacy-*/test-*/setup-*" and CI analysis dups that are superseded by Scripts/ versions or no longer needed post-PS deprecation.
3. The entire old `Powershell/` directory tree (capital-P parallel structure; deprecation path was to Scripts/ + dotnet CLI + WSL/Docker).
4. Any other noise (garbage shell artifacts were deleted outright).

## Kept at root (participating / high visibility)
- README.md, STEADY-STATE-AND-FINISH-ROADMAP.md, DEVELOPMENT-GUIDE.md, LICENSE*, CONTRIBUTING.md, SETUP-GUIDE.md (core entry points).
- BusBuddy.sln, Directory.Build.*, NuGet.config, global.json, mcp.json, package.json (tooling config).
- docker-compose.yml, Dockerfile, .dockerignore, .devcontainer/, .github/ (now participating after Docker/UTM work).
- Scripts/ (the active/current script home).
- Documentation/ (active docs + this Archive/).

## Post-move recommendations
- Review the archive before final PR; delete subtrees only after team sign-off if desired.
- Update any remaining internal links in active docs that pointed to moved files (search for names in this manifest).
- Future hygiene: enforce "no new root *.ps1 or *.md reports" via PR template or pre-commit.

## Verification
- dotnet build/test (EnableWindowsTargeting) still green.
- No references in active code/CI/README to the archived items (or only historical mentions).
- Docker/Postgres and UTM VM dev flow unaffected (these files were not part of runtime).

Generated during automated + manual hygiene pass.

## Post-archive verification (2026-06-15)
- dotnet restore + build (Release, -p:EnableWindowsTargeting=true) : 0 errors.
- Removed the last reference to BusBuddy-Practical.ruleset (now in this archive) from Directory.Build.props; the MSB3884 "could not find ruleset" warnings are gone.
- docker compose (db profile) validates (minor obsolete `version:` warning noted for future tidy).
- No root .ps1 or historical one-off .md left outside keepers + Scripts/ + Documentation/.
- Powershell/ fully archived (deprecation complete for this round).
- Docker trio (.dockerignore, Dockerfile, docker-compose.yml) now tracked and participating.
- Tree ready for commit on the hygiene branch and PR (mostly renames + adds of modern infra).

Any future references to archived items should be updated or left as historical only.
