# 🚌 Workflow Cleanup Quick Reference

## TL;DR - How to mass delete workflow runs

### 🎯 Recommended Approach (Safest)

1. **Preview what will be deleted**:
   ```bash
   .\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly -WhatIf
   ```

2. **Delete all failed runs**:
   ```bash
   .\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly
   ```

### 🚀 Quick Start Options

| Method | Difficulty | Safety | Speed |
|--------|------------|--------|-------|
| `cleanup-workflows.bat` | Easy | High | Medium |
| PowerShell script | Medium | High | Fast |
| Manual GitHub CLI | Hard | Medium | Slow |

### 📋 Common Commands

```powershell
# 1. Preview failed runs (SAFE - shows what would be deleted)
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly -WhatIf

# 2. Delete failed runs (removes 90%+ of clutter)
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly

# 3. Delete old runs (clean up remaining runs older than 30 days)
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -OlderThanDays 30

# 4. Nuclear option (delete EVERYTHING - use with caution)
.\Scripts\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteAll -WhatIf
```

### 📊 Current Status

- **Total workflow runs**: 116+
- **Failed runs**: ~90%+ (estimated)
- **Successful runs**: ~10% (estimated)
- **Main workflow**: CI Pipeline (ID: 179562737)

### 🎛️ Easy Mode

Just double-click `cleanup-workflows.bat` and follow the menu!

## Prerequisites Checklist

- [ ] GitHub CLI installed (`gh --version`)
- [ ] GitHub authenticated (`gh auth status`)
- [ ] PowerShell available (Windows default)
- [ ] Repository access (admin permissions)

## Safety First

✅ **Always use `-WhatIf` first** to preview
✅ **Start with failed runs** (safest option)
✅ **Keep recent successful runs** for reference

## Support

See `WORKFLOW-CLEANUP-GUIDE.md` for detailed instructions and troubleshooting.