# 🛡️ Anti-Regression Checklist - BusBuddy

> **Purpose**: Prevent legacy patterns from creeping back into the codebase. This checklist must be run before any major changes or merges.

## 🚨 **CRITICAL: Zero Tolerance Violations**

### **⚠️ Logging Violations**
**FORBIDDEN**: Microsoft.Extensions.Logging usage anywhere in the codebase.
**REQUIRED**: Pure Serilog implementation only.

#### **Detection Commands**
```powershell
# Find Microsoft.Extensions.Logging violations
grep -r "Microsoft.Extensions.Logging" --include="*.cs" --exclude-dir="bin" --exclude-dir="obj"

# Expected Result: Should return ZERO matches (except in XML documentation)
```

#### **Cleanup Command**
```powershell
# Automated cleanup script
bb-catch-errors "grep -r 'Microsoft.Extensions.Logging' --include='*.cs' --exclude-dir='bin' --exclude-dir='obj'"
```

#### **Current Known Violations** (August 2025)
```
🔍 Known Issues:
- BusBuddy.WPF\ViewModels\Vehicle\VehicleViewModel.cs:8
- BusBuddy.WPF\ViewModels\GoogleEarth\GoogleEarthViewModel.cs:8  
- BusBuddy.Tests\Phase3Tests\XAIChatServiceTests.cs:4
- BusBuddy.Core\Data\BusBuddyDbContext.cs:134 (LogLevel reference)
```

### **⚠️ UI Control Violations**
**FORBIDDEN**: Standard WPF controls in any new development.
**REQUIRED**: Syncfusion controls only (version 30.1.42).

#### **Detection Commands**
```powershell
# Find standard WPF controls in XAML
bb-xaml-validate

# Manual check for specific violations
grep -r "<DataGrid " --include="*.xaml"          # Should be <syncfusion:SfDataGrid
grep -r "<ComboBox " --include="*.xaml"          # Should be <syncfusion:SfComboBox
grep -r "<Button " --include="*.xaml"            # Should be <syncfusion:SfButton
```

#### **Required Syncfusion Namespace**
```xml
xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
```

### **⚠️ PowerShell Violations**
**FORBIDDEN**: Write-Host usage in PowerShell modules.
**REQUIRED**: Proper PowerShell output streams.

#### **Detection Commands**
```powershell
# Find Write-Host violations
grep -r "Write-Host" PowerShell/ --include="*.ps1" --include="*.psm1"

# Expected violations: Only in BusBuddy.psm1 for user feedback (approved usage)
```

#### **Approved vs. Forbidden Usage**
```powershell
# ✅ APPROVED (user feedback in modules)
Write-Host "🚌 BusBuddy PowerShell Module loaded!" -ForegroundColor Green

# ❌ FORBIDDEN (pipeline breaking)
Write-Host $result  # Should be: Write-Output $result
```

## 📋 **Comprehensive Regression Checklist**

### **🔧 Build System Compliance**
```powershell
# 1. Verify .NET 9.0 target
Select-String -Path "Directory.Build.props" -Pattern "net9.0-windows"

# 2. Verify Syncfusion version consistency
Select-String -Path "Directory.Build.props" -Pattern "30.1.42"

# 3. Verify CPM (Central Package Management) enabled
Select-String -Path "Directory.Build.props" -Pattern "ManagePackageVersionsCentrally>true"

# 4. Check for package version conflicts
dotnet list package --outdated
```

### **🎨 UI Consistency Verification**
```powershell
# 1. Run XAML validation
bb-xaml-validate

# 2. Check for mixed control usage
$xamlFiles = Get-ChildItem -Recurse -Filter "*.xaml" -Path "BusBuddy.WPF"
foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match "<DataGrid[^>]*>" -and $content -notmatch "syncfusion:SfDataGrid") {
        Write-Warning "Standard DataGrid found in: $($file.Name)"
    }
}

# 3. Verify Syncfusion namespace declarations
grep -r "xmlns:syncfusion" --include="*.xaml" BusBuddy.WPF/
```

### **📝 Code Quality Verification**
```powershell
# 1. Build validation
bb-build

# 2. Test execution
bb-test

# 3. PowerShell module health
bb-health

# 4. Check for nullable reference warnings
dotnet build 2>&1 | Select-String "CS8600|CS8601|CS8602|CS8603|CS8618"
```

## 🔄 **Automated Anti-Regression Script**

Create this as `PowerShell/Validation/Anti-Regression-Check.ps1`:

```powershell
#requires -Version 7.5
<#
.SYNOPSIS
    Automated anti-regression verification for BusBuddy
.DESCRIPTION
    Runs comprehensive checks to prevent legacy patterns from creeping back in
#>

function Invoke-AntiRegressionCheck {
    [CmdletBinding()]
    param()

    Write-Information "🛡️ Starting Anti-Regression Check..." -InformationAction Continue
    
    $issues = @()
    
    # Check 1: Microsoft.Extensions.Logging violations
    $loggingViolations = Select-String -Path "*.cs" -Pattern "Microsoft.Extensions.Logging" -Recurse
    if ($loggingViolations) {
        $issues += "❌ Found Microsoft.Extensions.Logging violations: $($loggingViolations.Count)"
    }
    
    # Check 2: Standard WPF controls in XAML
    $xamlViolations = Select-String -Path "*.xaml" -Pattern "<DataGrid |<ComboBox |<Button " -Recurse
    if ($xamlViolations) {
        $issues += "❌ Found standard WPF controls: $($xamlViolations.Count)"
    }
    
    # Check 3: PowerShell Write-Host violations
    $psViolations = Select-String -Path "PowerShell/" -Pattern "Write-Host" -Recurse | Where-Object { $_.Line -notmatch "Module loaded|ForegroundColor" }
    if ($psViolations) {
        $issues += "❌ Found PowerShell Write-Host violations: $($psViolations.Count)"
    }
    
    # Check 4: Build validation
    $buildResult = & dotnet build --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        $issues += "❌ Build failed with exit code: $LASTEXITCODE"
    }
    
    # Report results
    if ($issues.Count -eq 0) {
        Write-Information "✅ All anti-regression checks passed!" -InformationAction Continue
        return $true
    } else {
        Write-Warning "🚨 Anti-regression violations found:"
        $issues | ForEach-Object { Write-Warning "  $_" }
        return $false
    }
}

# Export for use in other scripts
Export-ModuleMember -Function Invoke-AntiRegressionCheck
```

## 📊 **Compliance Dashboard**

### **Current Status Metrics** (August 2025)
| Check | Status | Count | Target |
|-------|--------|-------|---------|
| Microsoft.Extensions.Logging | 🟡 Issues | 4 violations | 0 |
| Standard WPF Controls | ✅ Good | 0 new violations | 0 |
| PowerShell Write-Host | ✅ Good | 0 violations | 0 |
| Build Success | ✅ Good | 0 errors | 0 |
| Test Pass Rate | ✅ Good | 100% | 100% |
| Syncfusion Consistency | 🟡 In Progress | 90% migrated | 100% |

### **Priority Cleanup Tasks**
1. **🔥 HIGH**: Remove Microsoft.Extensions.Logging from ViewModels
2. **🔥 HIGH**: Complete remaining Syncfusion control migration
3. **⚡ MEDIUM**: Update XML documentation references
4. **💡 LOW**: Standardize error handling patterns

## 🎯 **Integration with Development Workflow**

### **Pre-Commit Hook** (`.git/hooks/pre-commit`)
```bash
#!/bin/sh
# Anti-regression check before commit
pwsh -Command "Import-Module .\PowerShell\Modules\BusBuddy\BusBuddy.psm1; Invoke-AntiRegressionCheck"
if [ $? -ne 0 ]; then
    echo "❌ Anti-regression check failed. Commit aborted."
    exit 1
fi
```

### **Pull Request Checklist Template**
```markdown
## 🛡️ Anti-Regression Checklist

- [ ] No Microsoft.Extensions.Logging usage added
- [ ] Only Syncfusion controls used in UI
- [ ] No Write-Host in PowerShell scripts
- [ ] Build passes without warnings
- [ ] All tests pass
- [ ] bb-xaml-validate runs clean
- [ ] Documentation updated if needed
```

### **VS Code Task Integration**
Add to `.vscode/tasks.json`:
```json
{
    "label": "🛡️ Anti-Regression Check",
    "type": "shell",
    "command": "pwsh",
    "args": ["-Command", "Import-Module .\\PowerShell\\Modules\\BusBuddy\\BusBuddy.psm1; Invoke-AntiRegressionCheck"],
    "group": "test",
    "detail": "Run anti-regression validation"
}
```

## 🔄 **Maintenance Schedule**

### **Daily** (Automated)
- Build validation
- Test execution
- PowerShell module health check

### **Weekly** (Manual)
- Full anti-regression check
- Dependency updates review
- Documentation consistency review

### **Monthly** (Comprehensive)
- Complete codebase scan
- Update anti-regression rules
- Review and update this checklist

## 📞 **Escalation Procedures**

### **When Anti-Regression Check Fails**
1. **STOP**: Do not proceed with merge/deployment
2. **IDENTIFY**: Run individual detection commands to isolate issues
3. **FIX**: Address each violation according to established patterns
4. **VERIFY**: Re-run full anti-regression check
5. **DOCUMENT**: Update this checklist if new patterns are discovered

### **Emergency Override** (Use with extreme caution)
```powershell
# Only for critical hotfixes - requires manager approval
bb-catch-errors "git commit -m 'EMERGENCY: Override anti-regression for critical fix'"
```

---

## 🎖️ **Success Metrics**

**GOAL**: Achieve and maintain 100% compliance across all categories.

**Current Target**: Zero violations by end of August 2025.

**Measurement**: Weekly automated reports with trend analysis.

---

**Last Updated**: August 3, 2025  
**Next Review**: August 10, 2025  
**Compliance Officer**: Development Team  
**Escalation Contact**: Project Lead

---

*This checklist is living documentation - update as new anti-patterns are discovered!*
