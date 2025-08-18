# 🤖 AI Assistant Efficiency Enforcement Protocol

# ==============================================

# This file defines mandatory efficiency standards for AI assistants working with BusBuddy

## 🚨 MANDATORY PRE-EXECUTION CHECKS

### Environment Validation (REQUIRED)

```powershell
# ALWAYS run these checks FIRST before any commands
$IsWindows = $env:OS -eq "Windows_NT"
$PSVersion = $PSVersionTable.PSVersion.Major
$WorkspaceRoot = Get-Location
```

### Platform Command Validation

```powershell
# FORBIDDEN commands on Windows:
# - head, tail, grep (use Select-Object, Where-Object)
# - uniq, sort (use Sort-Object, Get-Unique)
# - sed, awk (use PowerShell string operations)
# - ls, cat (use Get-ChildItem, Get-Content)

# ALLOWED commands:
# - dotnet, pwsh, Get-*, Set-*, Where-Object, Select-Object
```

## 📋 EFFICIENCY ENFORCEMENT RULES

### Rule 1: Batch Operations Only

```yaml
Constraint: "No more than 3 single-file edits per session"
Enforcement: "Group similar warnings/issues and fix in batches"
Example: "Fix all CA1860 warnings together, not one-by-one"
```

### Rule 2: Environment-First Approach

```yaml
Constraint: "Must validate environment before any commands"
Enforcement: "Check PowerShell version, available functions, workspace tools"
Validation: "Test-Path, Get-Command, $PSVersionTable checks required"
```

### Rule 3: Workspace Tools Priority

```yaml
Constraint: "Use existing BusBuddy functions before custom commands"
Enforcement: "Check for bb-*, use tasks.json, leverage existing scripts"
Example: "Use bb-health instead of parsing build output manually"
```

### Rule 4: Simple Commands Only

```yaml
Constraint: "No complex pipe chains that can fail"
Enforcement: "Break complex operations into simple, reliable steps"
AntiPattern: "dotnet build | Where-Object | ForEach-Object | Select-Object"
GoodPattern: "dotnet build; Count warnings separately"
```

### Rule 5: Strategic Analysis Required

```yaml
Constraint: "Identify top 3 highest-impact issues before fixing"
Enforcement: "Target most frequent warning types first"
Process: "Analyze → Prioritize → Batch Fix → Validate"
```

## 🛠️ ENFORCEMENT MECHANISMS

### VS Code Settings Enforcement

```json
{
  "agentic.platformValidationRequired": true,
  "agentic.batchFixesOnly": true,
  "agentic.noUnixCommandsOnWindows": true,
  "agentic.validateExistingToolsFirst": true,
  "agentic.maxSingleFileEdits": 3,
  "agentic.requireBulkOperations": true,
  "agentic.enforceSystematicApproach": true
}
```

### PowerShell Profile Enforcement

```powershell
# Add to BusBuddy PowerShell profile
function Assert-EfficiencyCompliance {
    Write-Host "🤖 AI Efficiency Check:" -ForegroundColor Cyan
    Write-Host "✅ Platform: Windows PowerShell $($PSVersionTable.PSVersion)" -ForegroundColor Green
    Write-Host "✅ Available Functions: $(Get-Command bb-* | Measure-Object | Select-Object -ExpandProperty Count)" -ForegroundColor Green
    Write-Host "✅ Tasks Available: $(Test-Path .vscode/tasks.json)" -ForegroundColor Green
}
```

### Task System Enforcement

```json
{
  "label": "🚨 AI Efficiency Validation",
  "type": "shell",
  "command": "pwsh",
  "args": ["-Command", "Assert-EfficiencyCompliance"],
  "group": "test",
  "runOptions": { "runOn": "folderOpen" }
}
```

## 📊 EFFICIENCY METRICS

### Time Allocation Targets

- **Environment Setup**: 30 seconds max
- **Problem Analysis**: 2 minutes max
- **Systematic Execution**: 5-10 minutes max
- **Validation**: 1 minute max

### Quality Metrics

- **Command Success Rate**: >95%
- **Single-Pass Fix Rate**: >80%
- **Platform Compatibility**: 100%
- **Batch Operation Ratio**: >70%

## 🚫 ANTI-PATTERNS TO AVOID

### Command Anti-Patterns

```bash
# FORBIDDEN on Windows
head -n 20 file.txt          # Use: Get-Content file.txt | Select-Object -First 20
grep "pattern" file.txt      # Use: Select-String "pattern" file.txt
ls -la                       # Use: Get-ChildItem -Force
```

### Workflow Anti-Patterns

- ❌ Parse build output with complex regex
- ❌ Fix warnings one-by-one across multiple files
- ❌ Use Unix commands without checking platform
- ❌ Create custom commands without checking existing tools

### Code Quality Anti-Patterns

- ❌ Edit files without understanding full context
- ❌ Apply fixes without testing in batches
- ❌ Make changes without considering side effects

## ✅ APPROVED PATTERNS

### Efficient Workflow Pattern

```powershell
# 1. Environment Check
Assert-EfficiencyCompliance

# 2. Use Existing Tools
bb-health

# 3. Batch Analysis
dotnet build --verbosity quiet | Select-String "warning"

# 4. Strategic Fixes
# Group by CA rule type, fix systematically

# 5. Validation
dotnet build --verbosity quiet
```

### Bulk Fix Pattern

```csharp
// Instead of: Fix one .Any() at a time
// Do this: grep_search for \.Any\(\), group by type, batch replace

// Example: All CA1860 fixes in one operation
find_all_instances("\.Any\(\)")
  .group_by_file_type()
  .apply_bulk_replacement(".Count > 0")
```

## 🎯 SUCCESS CRITERIA

An AI assistant session is considered efficient when:

- ✅ Zero platform command failures
- ✅ 70%+ issues fixed in batch operations
- ✅ Uses existing workspace tools effectively
- ✅ Completes in under 15 minutes total
- ✅ Achieves >95% of target fixes in single pass
- ✅ No overcomplicated command chains
- ✅ Clear validation of results

## 📝 VIOLATION REPORTING

When efficiency violations occur:

1. **Log the violation type** (Unix command, single-file fix, etc.)
2. **Note the time impact** (how much time was wasted)
3. **Document the correct approach** for future reference
4. **Update this protocol** if new patterns emerge

## 🔄 CONTINUOUS IMPROVEMENT

This protocol should be updated when:

- New efficiency anti-patterns are discovered
- Better workspace tools become available
- PowerShell/VS Code capabilities change
- User feedback identifies new pain points

---

**Last Updated**: July 26, 2025
**Next Review**: August 2025
**Owner**: BusBuddy Development Team
