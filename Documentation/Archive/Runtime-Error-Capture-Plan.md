# BusBuddy Runtime Error Capture Plan

## 🎯 **Comprehensive Runtime Error Capture Strategy**

### **Available Tools & Infrastructure:**

✅ **PowerShell Modules:**

- `BusBuddy.ExceptionCapture.psm1` - Professional exception capture
- `BusBuddy.psm1` - Main module with `bb-capture-runtime-errors` alias
- Existing error logging in `logs/runtime-errors/`

✅ **Application Infrastructure:**

- Serilog logging in `App.xaml.cs`
- Command-line argument handling
- Debug helper utilities in `DebugHelper.cs`
- Existing `runtime-errors.log` file

✅ **New Capture Script:**

- `PowerShell/Scripts/Capture-RuntimeErrors.ps1` - Comprehensive monitoring

---

## 🚀 **Execution Plan:**

### **Phase 1: Quick Runtime Test (Immediate)**

```powershell
# 1. Build and basic health check
bb-build
bb-health

# 2. Quick 30-second runtime capture
bb-capture-runtime-errors -Duration 30

# 3. Review results
Get-ChildItem logs/runtime-capture -Recurse
```

### **Phase 2: Extended Monitoring (Comprehensive)**

```powershell
# 1. Extended capture with detailed logging
bb-capture-runtime-errors -Duration 300 -DetailedLogging -OpenLogsAfter

# 2. Monitor specific scenarios:
#    - Application startup
#    - Student data loading
#    - Route management
#    - Syncfusion control interactions
```

### **Phase 3: Targeted Error Scenarios**

```powershell
# Test specific error conditions:
# 1. Database connection issues
# 2. Missing configuration
# 3. Syncfusion license problems
# 4. XAI service unavailability
```

---

## 📊 **Capture Mechanisms:**

### **1. Multi-Stream Capture**

- **STDOUT**: Application output and normal logs
- **STDERR**: Error messages and exceptions
- **Debug**: Detailed debug information
- **Events**: Windows event log entries

### **2. Real-Time Monitoring**

- Live error detection during execution
- Progress indicators and status updates
- Automatic timeout handling
- Background process management

### **3. Comprehensive Reporting**

- Markdown summary report
- Timestamped log files
- Error categorization and counting
- Actionable recommendations

---

## 🔧 **Usage Examples:**

### **Basic Runtime Check:**

```powershell
bb-capture-runtime-errors
# Monitors for 60 seconds, generates basic report
```

### **Extended Analysis:**

```powershell
bb-capture-runtime-errors -Duration 300 -DetailedLogging -OpenLogsAfter
# 5-minute detailed capture with auto-open results
```

### **Integration with Build Process:**

```powershell
bb-build && bb-capture-runtime-errors -Duration 120
# Build then monitor for 2 minutes
```

---

## 📁 **Output Structure:**

```
logs/runtime-capture/
├── runtime-capture-YYYYMMDD-HHMMSS-main.log      # Application output
├── runtime-capture-YYYYMMDD-HHMMSS-errors.log    # Error stream
├── runtime-capture-YYYYMMDD-HHMMSS-debug.log     # Debug information
└── runtime-capture-YYYYMMDD-HHMMSS-summary.md    # Analysis report
```

---

## ⚡ **Quick Start:**

1. **Immediate Test:**

    ```powershell
    bb-capture-runtime-errors
    ```

2. **Review Results:**

    ```powershell
    Get-ChildItem logs/runtime-capture | Sort-Object LastWriteTime -Descending | Select-Object -First 4
    ```

3. **Open Latest Summary:**
    ```powershell
    $latest = Get-ChildItem logs/runtime-capture -Filter "*summary.md" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    code $latest.FullName
    ```

---

## 🎯 **Success Criteria:**

✅ **Clean Run:** No errors detected in logs  
⚠️ **Minor Issues:** < 5 non-critical errors  
❌ **Problems:** > 5 errors or critical failures

---

## 🔄 **Integration with MVP Workflow:**

```powershell
# MVP Development Cycle:
bb-health           # Environment check
bb-build            # Clean build
bb-capture-runtime-errors -Duration 60  # Runtime validation
bb-mvp-check        # MVP readiness
```

This comprehensive approach ensures we capture all runtime issues during BusBuddy execution!
