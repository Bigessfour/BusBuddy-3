# 🚌 BusBuddy Workflow Cleanup Guide

This guide explains how to mass delete old GitHub workflow runs after the BusBuddy refactoring.

## 📋 Overview

After the major BusBuddy refactoring, the repository has accumulated 116+ workflow runs (mostly failures from the development phase) that are no longer relevant. This guide provides tools and instructions for cleaning up these obsolete workflow runs.

## 🎯 Current Situation

- **Total workflow runs**: 116+ (mainly from CI pipeline)
- **Status**: Most are failed runs from development/refactoring phase
- **Relevance**: No longer valuable after project refactoring
- **Goal**: Clean slate for future development

## 🛠️ Cleanup Tools

### 1. PowerShell Script (Recommended)

**Location**: `Scripts/Delete-WorkflowRuns.ps1`

**Features**:
- ✅ Delete failed runs only
- ✅ Delete runs older than specified days
- ✅ Target specific workflows
- ✅ Preview mode (WhatIf)
- ✅ Bulk deletion with progress tracking
- ✅ Safety confirmations

### 2. Batch File (Quick Access)

**Location**: `cleanup-workflows.bat`

**Features**:
- ✅ Interactive menu
- ✅ Common cleanup scenarios
- ✅ Safety checks
- ✅ No PowerShell knowledge required

## 🚀 Quick Start

### Option 1: Use the Batch File (Easiest)

1. **Double-click** `cleanup-workflows.bat`
2. **Choose** from the menu:
   - Option 1: Preview failed runs
   - Option 2: Delete failed runs
   - Option 3: Delete runs older than 30 days

### Option 2: Use PowerShell Directly

```powershell
# Preview what would be deleted (recommended first step)
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly -WhatIf

# Delete all failed runs
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly

# Delete runs older than 30 days
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -OlderThanDays 30
```

## 📋 Prerequisites

### Required Tools

1. **GitHub CLI**: Install from [cli.github.com](https://cli.github.com/)
2. **PowerShell**: Available on Windows by default
3. **Git Authentication**: Run `gh auth login` if not already authenticated

### Verification Commands

```bash
# Check GitHub CLI
gh --version

# Check authentication
gh auth status

# Test API access
gh api repos/Bigessfour/BusBuddy-3/actions/runs --limit 5
```

## 🎛️ Advanced Usage

### Delete Specific Workflow

```powershell
# Target the main CI pipeline only
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -WorkflowId 179562737 -DeleteFailedOnly
```

### Custom Date Range

```powershell
# Delete runs older than 1 week
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -OlderThanDays 7

# Delete runs older than 60 days
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -OlderThanDays 60
```

### Nuclear Option (Delete Everything)

```powershell
# ⚠️ WARNING: This deletes ALL workflow runs
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteAll -WhatIf  # Preview first
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteAll           # Actually delete
```

## 📊 Current Workflow Analysis

### Active Workflows
- **🚌 BusBuddy CI Pipeline** (179562737) - 116 runs, mostly failed
- **Dependabot Updates** (179562742) - Automated dependency updates
- **CI PR Gate** (180851686) - Pull request validation
- **Duplicate File Guard** (181321482) - File duplication checks
- **CI Legacy** (181897121) - Legacy CI workflow
- **Copilot** (183150312) - Copilot integration

### Recommended Cleanup Strategy

1. **Phase 1**: Delete all failed runs
   ```powershell
   .\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly
   ```

2. **Phase 2**: Delete old successful runs (older than 30 days)
   ```powershell
   .\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -OlderThanDays 30
   ```

3. **Phase 3**: Keep only the most recent successful runs for reference

## 🔒 Safety Features

### Built-in Protections

- ✅ **WhatIf Mode**: Preview deletions without actually deleting
- ✅ **Confirmation Prompts**: Require explicit confirmation for destructive actions
- ✅ **Progress Tracking**: Real-time feedback during bulk operations
- ✅ **Error Handling**: Graceful handling of API failures
- ✅ **Rate Limiting**: Prevents API abuse with built-in delays

### Best Practices

1. **Always use -WhatIf first** to preview what will be deleted
2. **Start with failed runs** - they're definitely not useful
3. **Keep recent successful runs** for reference
4. **Backup important artifacts** before mass deletion
5. **Test on a single workflow** before targeting all workflows

## 🚨 Troubleshooting

### Common Issues

**Problem**: "GitHub CLI not found"
```bash
# Solution: Install GitHub CLI
winget install GitHub.cli
# OR download from https://cli.github.com/
```

**Problem**: "Not authenticated"
```bash
# Solution: Login to GitHub
gh auth login
```

**Problem**: "API rate limit exceeded"
```bash
# Solution: Wait a few minutes, the script has built-in rate limiting
# Or run with smaller batches using -OlderThanDays with smaller values
```

**Problem**: "Permission denied"
```bash
# Solution: Ensure you have admin access to the repository
# Or ask the repository owner to run the cleanup
```

### Debug Mode

```powershell
# Enable verbose output for troubleshooting
$VerbosePreference = "Continue"
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly -WhatIf
```

## 📈 Expected Results

After running the cleanup:

- ✅ **Cleaner repository**: No obsolete workflow runs cluttering the Actions tab
- ✅ **Better performance**: Faster loading of the Actions page
- ✅ **Clear history**: Only relevant, recent workflow runs remain
- ✅ **Fresh start**: Clean slate for future development workflows

### Before/After Comparison

**Before Cleanup**:
- 116+ workflow runs
- Most are failed runs from refactoring phase
- Cluttered Actions tab
- Confusing history

**After Cleanup**:
- ~5-10 recent successful runs
- Clear, relevant history
- Clean Actions tab
- Focus on current development

## 🔄 Automation Options

### Schedule Regular Cleanup

You can automate this process by:

1. **Adding to CI/CD**: Include cleanup step in workflows
2. **GitHub Actions**: Create a scheduled workflow for cleanup
3. **Local Automation**: Set up a scheduled task on your machine

### Example GitHub Action for Auto-Cleanup

```yaml
name: Cleanup Old Workflow Runs
on:
  schedule:
    - cron: '0 0 * * 0'  # Every Sunday at midnight
  workflow_dispatch:

jobs:
  cleanup:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Cleanup old runs
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          # Delete failed runs older than 7 days
          gh api repos/${{ github.repository }}/actions/runs \
            --paginate \
            --jq '.workflow_runs[] | select(.conclusion == "failure" and (.created_at | fromdateiso8601) < (now - 604800)) | .id' \
            | xargs -I {} gh api repos/${{ github.repository }}/actions/runs/{} -X DELETE
```

## 📞 Support

If you encounter issues with the cleanup process:

1. **Check Prerequisites**: Ensure GitHub CLI is installed and authenticated
2. **Review Error Messages**: Most errors include helpful guidance
3. **Use WhatIf Mode**: Always preview before destructive operations
4. **Start Small**: Test with a single workflow before bulk operations
5. **Check Repository Permissions**: Ensure you have admin access

## 📚 Additional Resources

- [GitHub CLI Documentation](https://cli.github.com/manual/)
- [GitHub Actions API Reference](https://docs.github.com/en/rest/actions)
- [PowerShell Documentation](https://docs.microsoft.com/en-us/powershell/)

---

**Note**: This cleanup is specifically designed for the BusBuddy repository after its major refactoring. The scripts can be adapted for other repositories by changing the repository parameter.