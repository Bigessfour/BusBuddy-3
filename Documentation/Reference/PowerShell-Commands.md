# ⚡ PowerShell Commands Reference - BusBuddy Automation

**Purpose**: BusBuddy-specific PowerShell 7.5.2 commands for efficient development workflow automation.

**Philosophy**: `bb-*` command pattern following Microsoft PowerShell standards for consistent, discoverable automation.

## 🚀 Core Development Commands

### Build & Run Commands
```powershell
# Build entire solution
bb-build                    # Clean build with standard output
bb-build-full              # Build with detailed, non-truncated output

# Run application
bb-run                     # Launch BusBuddy WPF application
bb-run --project BusBuddy.WPF/BusBuddy.WPF.csproj  # Explicit project targeting

# Clean operations
bb-clean                   # Clean build artifacts
bb-restore                 # Force package restore
```

### Testing Commands
```powershell
# Test execution
bb-test                    # Run all tests with MVP focus
bb-test-full              # Tests with detailed, non-truncated output
bb-test-errors            # Show only test failures and errors
bb-test-log               # Test execution with enhanced logging
bb-test-watch             # Continuous testing with file monitoring

# Test validation
bb-mvp-check              # Verify MVP functionality (students/routes)
bb-validate-tests         # Comprehensive test suite validation
```

### Health & Diagnostics
```powershell
# System health checks
bb-health                 # Comprehensive system health analysis
bb-health --check-build   # Build configuration validation
bb-health --check-nuget   # NuGet configuration check
bb-health --check-extensions  # VS Code extensions validation

# Diagnostic reporting
bb-diagnostic             # Full environment and project health check
bb-report                 # Generate comprehensive project report
```

## 🛡️ Anti-Regression Commands

### Code Quality Validation
```powershell
# Prevent regressions during development
bb-anti-regression        # Scan for Microsoft.Extensions.Logging, Write-Host violations
bb-xaml-validate         # Ensure only Syncfusion controls in XAML
bb-code-analysis         # Run detailed code analysis against practical ruleset
```

**Critical Usage**: Run before any commit to prevent quality regressions.

## 🧪 Testing & Validation

### MVP Testing Commands
```powershell
# Student/Route functionality validation
bb-test-students          # Test student management features
bb-test-routes           # Test route assignment features
bb-test-mvp              # Comprehensive MVP feature testing

# Integration testing
bb-test-integration      # Database and service integration tests
bb-test-ui               # WPF UI automation tests
```

### Test Output Management
```powershell
# Enhanced test output (from Enhanced-Test-Output.ps1)
bb-test-full             # Full test output without truncation
bb-test-errors           # Filter and show only test errors
bb-test-log              # Enhanced logging during test execution
bb-test-watch            # Continuous testing with file system monitoring
```

## 🔧 Development Workflow

### Session Management
```powershell
# Complete development session startup
bb-dev-session           # Opens workspace, builds, starts debug monitoring

# Quick development cycles  
bb-quick-test            # Clean, build, test, validate cycle
bb-quick-build           # Fast iteration build process
```

### Debug & Monitoring
```powershell
# Debug helper integration (from App.xaml.cs DebugHelper)
bb-debug-start           # Start real-time debug filtering
bb-debug-stream          # Stream debug output to console
bb-debug-export          # Export debug data to JSON for analysis
bb-debug-test            # Test debug monitoring functionality
```

## 📚 Documentation & Reference

### Copilot Integration
```powershell
# Reference system access
bb-copilot-ref           # Open main Copilot Hub reference
bb-copilot-ref Syncfusion    # Open specific reference file
bb-docs                  # Access BusBuddy documentation
bb-mentor                # AI mentor assistance (if available)
```

### Knowledge Management
```powershell
# Documentation utilities
bb-docs-update           # Update reference documentation
bb-validate-docs         # Verify documentation integrity
bb-search-docs [query]   # Search documentation for specific topics
```

## 🚀 Special BusBuddy Features

### Google Earth Integration
```powershell
# Map visualization and route planning
bb-maps                  # Launch Google Earth view (if implemented)
bb-route-visual         # Visualize routes on map
bb-earth-view           # Open Google Earth interface

# Related Views: GoogleEarthView.xaml in BusBuddy.WPF/Views/GoogleEarth/
```

### XAI Chat & AI Features
```powershell
# AI-powered development and route optimization
bb-xai-chat             # Open XAI chat interface
bb-ai-help              # AI-powered development assistance

# Advanced Route Optimization with xAI Grok
bb-route-optimize -RouteId "Route-001" -CurrentPerformance "45 min, 12 stops" -TargetMetrics "Reduce time by 10%"
bb-route-optimize -RouteId "Elementary-North" -Constraints @("Max 8 stops", "Safety first", "No highway") -OutputPath "reports/optimization.json"
bb-route-optimize -RouteId "Route-003" -CurrentPerformance "52 minutes average" -TargetMetrics "Improve efficiency, reduce fuel" -Mock

# PDF Report Generation with Syncfusion
bb-generate-report -ReportType Roster -OutputPath "reports/student-roster.pdf" -OpenAfterGeneration
bb-generate-report -ReportType RouteManifest -RouteId "Route-001" -OutputPath "manifests/route-001.pdf"
bb-generate-report -ReportType DriverSchedule -Format Excel -OutputPath "schedules/driver-schedule.xlsx"

# Related Views: XAIChatView.xaml and route optimization services
# Related Services: GrokGlobalAPI.cs, PdfReportService.cs, SmartRouteOptimizationService.cs
```

### Route Management & Analysis
```powershell
# Core route operations
bb-routes               # Main route optimization system
bb-route-demo           # Demo route optimization with sample data
bb-route-status         # Check route optimization system status

# Route optimization examples
bb-route-optimize -RouteId "K-5-North" -CurrentPerformance "38 stops, 65 minutes" -TargetMetrics "Reduce stops to 30, target 50 minutes"
bb-route-optimize -RouteId "Middle-School-East" -Constraints @("No left turns on Main St", "Pickup before 7:30 AM") 
bb-route-optimize -RouteId "High-School-Express" -TargetMetrics "Express route, minimize stops" -OutputPath "optimization-reports/hs-express.json"

# Report generation examples  
bb-generate-report -ReportType StudentList -OutputPath "daily-reports/students-$(Get-Date -Format 'yyyy-MM-dd').pdf"
bb-generate-report -ReportType MaintenanceReport -Format CSV -OutputPath "maintenance/weekly-report.csv"
```

### Advanced Testing & Validation
```powershell
# Comprehensive testing beyond MVP
bb-test-ui              # WPF UI automation tests
bb-validate-all         # Comprehensive system validation
bb-benchmark            # Performance benchmarking
bb-load-test            # Load testing for large datasets
```

## 🔄 Configuration & Setup

### Environment Configuration
```powershell
# VS Code integration
bb-vscode               # Open workspace in VS Code
bb-tasks                # List available VS Code tasks
bb-extensions           # Validate required extensions

# PowerShell profile management
bb-profile-reload       # Reload PowerShell profile
bb-profile-update       # Update profile with latest commands
```

### Package Management
```powershell
# NuGet operations
bb-packages-update      # Update all packages to latest versions
bb-packages-clean       # Clear NuGet caches and restore
bb-validate-syncfusion  # Verify Syncfusion package integrity
```

## 🎯 MVP-Specific Commands

### Greenfield Reset Support
```powershell
# Clean slate operations for MVP focus
bb-disable-services     # Temporarily disable non-MVP services
bb-enable-services      # Re-enable disabled services post-MVP
bb-mvp-status          # Check MVP completion status
```

### Build Validation
```powershell
# Ensure clean builds for MVP
bb-validate-build      # Comprehensive build validation
bb-check-errors        # Identify and categorize build errors
bb-fix-cs0246          # Helper for missing type errors
```

## 💡 Copilot Usage Examples

### Build Automation
```powershell
# Copilot Prompt: "Create PowerShell function for clean BusBuddy build"
# Result: Uses bb-* pattern with Microsoft-compliant PowerShell standards
function bb-clean-build {
    [CmdletBinding()]
    param()
    
    Write-Information "Starting clean build process" -InformationAction Continue
    bb-clean
    bb-restore  
    bb-build
}
```

### Testing Workflow
```powershell
# Copilot Prompt: "Add comprehensive testing command with error handling"
# Result: Follows Microsoft PowerShell error handling patterns
function bb-test-comprehensive {
    [CmdletBinding()]
    param([switch]$IncludeIntegration)
    
    try {
        bb-test
        if ($IncludeIntegration) {
            bb-test-integration
        }
        bb-mvp-check
        Write-Output "All tests completed successfully"
    }
    catch {
        Write-Error "Test execution failed: $($_.Exception.Message)"
        throw
    }
}
```

## 🛠️ Command Implementation Standards

### Microsoft PowerShell Compliance
```powershell
# Standard function template for bb-* commands
function bb-[action] {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false)]
        [string]$Option
    )
    
    begin {
        Write-Verbose "Starting bb-[action] operation"
    }
    
    process {
        try {
            # Implementation using Write-Output, not Write-Host
            Write-Information "Processing action" -InformationAction Continue
            # Action implementation
            Write-Output $result
        }
        catch {
            Write-Error "Operation failed: $($_.Exception.Message)"
            throw
        }
    }
    
    end {
        Write-Verbose "bb-[action] operation completed"
    }
}
```

### Export Standards
```powershell
# Proper module member export
Export-ModuleMember -Function Get-BusBuddyFunction -Alias "bb-function"
```

## 🔍 Command Discovery

### Available Commands
```powershell
# List all bb-* commands
Get-Command bb-*

# Get command help
Get-Help bb-build -Detailed

# Show command examples
Get-Help bb-test -Examples
```

### Module Information
```powershell
# Module details
Get-Module BusBuddy -ListAvailable

# Function information
Get-Command -Module BusBuddy
```

---
*Efficient BusBuddy development through standardized PowerShell automation* ⚡
