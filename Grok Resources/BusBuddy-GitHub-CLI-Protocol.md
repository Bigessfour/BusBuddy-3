# 🚀 BusBuddy GitHub CLI Protocol

## GitHub CLI Setup & Authentication

### Prerequisites
```powershell
# Install GitHub CLI (if not already installed)
winget install GitHub.cli

# Verify installation
gh --version

# Authenticate with GitHub
gh auth login
```

### Authentication Status Check
```powershell
# Check current authentication status
gh auth status

# Expected output:
# github.com
#   ✓ Logged in to github.com account [username]
#   - Active account: true
#   - Git operations protocol: https
#   - Token scopes: 'gist', 'read:org', 'repo', 'workflow'
```

## Standard Git Operations with GitHub CLI

### Daily Development Workflow
```powershell
# 1. Check repository status
git status

# 2. Stage changes
git add .

# 3. Commit with descriptive message
git commit -m "feat: implement dashboard view with syncfusion controls

✅ COMPLETED:
- Dashboard metrics display
- Driver count widget
- Vehicle status overview
- Recent activities list

🔧 TECHNICAL:
- Syncfusion SfDataGrid integration
- Entity Framework data binding
- MVVM pattern implementation
- FluentDark theme consistency"

# 4. Push to GitHub
git push origin main

# 5. Verify push success
git status
```

### Branch Management
```powershell
# Create new feature branch
git checkout -b feature/driver-management
gh repo set-default Bigessfour/BusBuddy-2

# Push new branch to GitHub
git push -u origin feature/driver-management

# Create pull request via GitHub CLI
gh pr create --title "feat: implement driver management view" --body "Implements core driver CRUD operations with Syncfusion controls"

# List pull requests
gh pr list

# Merge pull request
gh pr merge --squash
```

### Repository Information
```powershell
# View repository details
gh repo view

# Check repository status on GitHub
gh repo view --web

# List recent commits
gh api repos/Bigessfour/BusBuddy-2/commits

# View commit details
gh api repos/Bigessfour/BusBuddy-2/commits/[commit-hash]
```

### Issue Management
```powershell
# Create new issue
gh issue create --title "XAML parsing errors blocking build" --body "3 XAML files have parsing errors preventing compilation"

# List issues
gh issue list

# View specific issue
gh issue view [issue-number]

# Close issue
gh issue close [issue-number]
```

### Release Management
```powershell
# Create new release
gh release create v1.0.0 --title "BusBuddy MVP Release" --notes "Initial MVP release with core transportation management features"

# List releases
gh release list

# Download release assets
gh release download v1.0.0
```

## Advanced GitHub CLI Commands

### Repository Management
```powershell
# Clone repository
gh repo clone Bigessfour/BusBuddy-2

# Fork repository
gh repo fork Bigessfour/BusBuddy-2

# Archive repository
gh repo archive Bigessfour/BusBuddy-2

# Set repository visibility
gh repo edit --visibility public
```

### Workflow Management
```powershell
# List GitHub Actions workflows
gh workflow list

# View workflow runs
gh run list

# View specific workflow run
gh run view [run-id]

# Rerun failed workflow
gh run rerun [run-id]
```

### Security & Secrets
```powershell
# List repository secrets
gh secret list

# Set repository secret
gh secret set SYNCFUSION_LICENSE_KEY --body "your-license-key"

# Delete secret
gh secret delete SYNCFUSION_LICENSE_KEY
```

## Emergency Protocols

### When Build Fails
```powershell
# 1. Check build status locally
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj

# 2. If successful locally, check GitHub Actions
gh run list --limit 5

# 3. View failed run details
gh run view [failed-run-id]

# 4. If GitHub Actions failing, rerun workflow
gh run rerun [run-id]
```

### When Push Fails
```powershell
# 1. Check authentication
gh auth status

# 2. Re-authenticate if needed
gh auth refresh

# 3. Check remote repository status
gh repo view

# 4. Force push if necessary (use with caution)
git push --force-with-lease origin main
```

### When Merge Conflicts Occur
```powershell
# 1. Pull latest changes
git pull origin main

# 2. Resolve conflicts manually in VS Code
# 3. Stage resolved files
git add .

# 4. Complete merge
git commit -m "resolve merge conflicts"

# 5. Push merged changes
git push origin main
```

## Best Practices

### Commit Message Standards
```
Format: type(scope): description

Types:
- feat: new feature
- fix: bug fix
- docs: documentation updates
- style: formatting changes
- refactor: code refactoring
- test: adding tests
- chore: maintenance tasks

Examples:
- feat(dashboard): implement metrics display with syncfusion charts
- fix(xaml): resolve parsing errors in vehicle forms
- docs(readme): update build instructions
- chore(deps): update entity framework to v9.0.7
```

### Branch Naming Conventions
```
feature/[feature-name]     - New features
bugfix/[bug-description]   - Bug fixes
hotfix/[critical-fix]      - Critical production fixes
release/[version]          - Release preparation
docs/[documentation-type]  - Documentation updates

Examples:
- feature/driver-management
- bugfix/xaml-parsing-errors
- hotfix/critical-database-fix
- release/v1.0.0
- docs/api-documentation
```

### GitHub CLI Aliases (Optional)
```powershell
# Add to PowerShell profile for convenience
gh alias set prs 'pr list'
gh alias set issues 'issue list'
gh alias set repos 'repo list'
gh alias set clone 'repo clone'
```

## Current Session Protocol (August 2, 2025)

### Successfully Executed
```powershell
# 1. Checked authentication
gh auth status  # ✅ Authenticated as Bigessfour

# 2. Staged all changes
git add .  # ✅ All modified files staged

# 3. Committed with comprehensive message
git commit -m "🚧 XAML Corruption Resolution Session..."  # ✅ Commit successful

# 4. Pushed to GitHub
git push origin main  # ✅ Push successful

# 5. Verified clean state
git status  # ✅ Working tree clean, up to date with origin/main
```

### Next Session Protocol
```powershell
# Standard workflow for future sessions:

# 1. Start development session
gh repo view --web  # Open repository in browser

# 2. Pull latest changes
git pull origin main

# 3. Create feature branch (if needed)
git checkout -b feature/fix-remaining-xaml-errors

# 4. Make changes and test locally
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj

# 5. Stage, commit, and push
git add .
git commit -m "fix(xaml): resolve remaining 3 XAML parsing errors"
git push origin feature/fix-remaining-xaml-errors

# 6. Create pull request if using feature branch
gh pr create --title "Fix remaining XAML parsing errors" --body "Resolves the last 3 XAML files blocking build compilation"
```

## Repository Information
- **Repository**: Bigessfour/BusBuddy-2
- **Visibility**: Public
- **Main Branch**: main
- **Authentication**: GitHub CLI with token-based auth
- **Protocol**: HTTPS (recommended for GitHub CLI)
- **Scopes**: repo, workflow, gist, read:org

---
**Status**: ✅ GitHub CLI protocol established and tested
**Last Updated**: August 2, 2025 - 4:15 AM
**Next Action**: Resume XAML corruption fixes to restore build capability
