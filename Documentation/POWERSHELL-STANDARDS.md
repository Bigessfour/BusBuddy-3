# PowerShell Development Standards for Bus Buddy

## 📋 **Overview**
This document establishes PowerShell coding standards and best practices for the Bus Buddy project based on PSScriptAnalyzer findings and project requirements.

## ✅ **Code Quality Standards**

### **1. Write-Host Usage Policy**
- **Policy**: Write-Host is **ALLOWED** for Bus Buddy interactive scripts
- **Rationale**: User interaction and colored output are essential for development workflows
- **Alternative**: Use `Write-Information` for logging, `Write-Host` for user interaction
- **Configuration**: `PSAvoidUsingWriteHost` disabled in `PSScriptAnalyzerSettings.psd1`

### **2. Cmdlet Alias Standards**
- **Allowed Aliases**: `cd`, `ls`, `cat`, `cp`, `mv`, `rm`, `echo`, `where`
- **Forbidden**: Non-standard or unclear aliases
- **Rationale**: Common aliases improve readability in interactive scripts
- **Rule**: `PSAvoidUsingCmdletAliases` with whitelist

### **3. File Encoding Standards**
- **Required**: UTF-8 with BOM for all PowerShell files
- **Tool**: Use `fix-ps-encoding.ps1` script for batch conversion
- **Verification**: PSScriptAnalyzer rule `PSUseBOMForUnicodeEncodedFile`
- **Task**: "Fix PowerShell Encoding" in VS Code Task Explorer

### **4. Whitespace and Formatting**
- **Indentation**: 4 spaces (no tabs)
- **Operators**: Space before and after binary and assignment operators
- **Braces**: Consistent brace placement (OTBS style)
- **Auto-Fix**: Use "Fix PowerShell Code Issues" task

## 🔧 **Development Workflow**

### **Before Committing Code**
1. Run **"Fix PowerShell Code Issues"** task
2. Run **"Fix PowerShell Encoding"** task  
3. Run **"Analyze PowerShell Scripts"** task
4. Address any remaining Error-level issues

### **VS Code Integration**
- **Tasks**: All PowerShell tools available in Task Explorer
- **Settings**: PowerShell extension configured for Bus Buddy standards
- **Analysis**: Real-time analysis with `PSScriptAnalyzerSettings.psd1`

### **Continuous Integration**
- **Pre-commit**: PowerShell analysis and formatting
- **Build Pipeline**: Include PowerShell quality gates
- **Standards**: Enforce through automated checks

## 📊 **Current Status**

### **Analysis Results Summary**
```
Total Issues Found: ~1000+
- Write-Host Usage: 500+ (ALLOWED for Bus Buddy)
- Whitespace Issues: 200+ (AUTO-FIXABLE)
- BOM Encoding: 20+ files (AUTO-FIXABLE)
- Cmdlet Aliases: 30+ (REVIEWED - Some allowed)
- Empty Catch Blocks: 3 (REQUIRES MANUAL FIX)
```

### **Priority Fixes Needed**
1. **Empty Catch Blocks** - Manual review required
2. **Should Process Functions** - Add SupportsShouldProcess where needed
3. **Unused Parameters** - Review and remove if not needed
4. **WMI Cmdlets** - Replace with CIM cmdlets (Get-CimInstance)

## 🎯 **Bus Buddy Specific Standards**

### **Interactive Script Pattern**
```powershell
# ✅ CORRECT - User interaction pattern
Write-Host "🚌 Starting Bus Buddy operation..." -ForegroundColor Cyan
Write-Host "✅ Operation completed successfully" -ForegroundColor Green
Write-Host "❌ Error occurred: $errorMessage" -ForegroundColor Red
```

### **Logging Pattern**  
```powershell
# ✅ CORRECT - Structured logging
Write-Information "Operation started" -InformationAction Continue
Write-Warning "Warning condition detected"
Write-Error "Error condition occurred"
```

### **Error Handling Pattern**
```powershell
# ✅ CORRECT - Comprehensive error handling
try {
    Invoke-BusBuddyOperation
    Write-Host "✅ Operation successful" -ForegroundColor Green
}
catch {
    Write-Host "❌ Operation failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Error "Detailed error: $($_.Exception)" -ErrorAction Continue
    throw  # Re-throw for upstream handling
}
```

### **Function Structure**
```powershell
# ✅ CORRECT - Function with proper attributes
function Start-BusBuddyProcess {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$ProcessName,
        
        [Parameter()]
        [switch]$Force
    )
    
    begin {
        Write-Information "Starting $ProcessName process" -InformationAction Continue
    }
    
    process {
        if ($PSCmdlet.ShouldProcess($ProcessName, "Start Process")) {
            # Process implementation
        }
    }
    
    end {
        Write-Information "Process $ProcessName completed" -InformationAction Continue
    }
}
```

## 🛠️ **Tools and Configuration**

### **PSScriptAnalyzer Configuration**
- **File**: `PSScriptAnalyzerSettings.psd1`
- **Custom Rules**: Bus Buddy specific allowances
- **Integration**: VS Code PowerShell extension
- **Analysis**: Real-time and on-demand

### **VS Code Tasks**
- **Fix PowerShell Code Issues**: Auto-fix formatting and minor issues
- **Fix PowerShell Encoding**: UTF-8 BOM conversion
- **Analyze PowerShell Scripts**: Comprehensive analysis
- **PowerShell Formatting**: Format code to standards

### **Automation Scripts**
- **fix-ps-encoding.ps1**: Batch encoding conversion
- **PowerShell profile integration**: Available functions
- **Git hooks**: Pre-commit validation (future)

## 📈 **Quality Metrics**

### **Target Goals**
- **Zero Error-level issues**: Critical for production
- **Minimal Warning-level issues**: Focus on code quality
- **Consistent formatting**: 100% compliance
- **Proper encoding**: All files UTF-8 BOM

### **Measurement**
- **Daily**: Run analysis during development
- **Pre-commit**: Automated checks
- **CI/CD**: Quality gates in build pipeline
- **Reviews**: Code review checklist

## 🔄 **Continuous Improvement**

### **Regular Reviews**
- **Monthly**: Review and update standards
- **Feedback**: Incorporate team feedback
- **Tools**: Update PSScriptAnalyzer and rules
- **Training**: Team training on standards

### **Documentation Updates**
- **Standards**: Keep this document current
- **Examples**: Add real-world examples
- **Patterns**: Document approved patterns
- **Exceptions**: Document approved exceptions

---

**Last Updated**: July 20, 2025  
**Next Review**: August 20, 2025  
**Owner**: Bus Buddy Development Team
