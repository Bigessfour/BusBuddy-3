# 🧪 BusBuddy Testing Guide 2025 - Microsoft Testing Platform Enhanced

## 📋 Table of Contents
- [Overview](#overview)
- [Quick Start](#quick-start)
- [Advanced Testing Features](#advanced-testing-features)
- [Testing Technology Stack](#testing-technology-stack)
- [BusBuddy-Specific Testing Scenarios](#busbuddy-specific-testing-scenarios)
- [Performance Optimization](#performance-optimization)
- [CI/CD Integration](#cicd-integration)
- [Troubleshooting](#troubleshooting)

## 🎯 Overview

BusBuddy's testing system has been enhanced with Microsoft Testing Platform 2025 (MTP), providing cutting-edge testing capabilities including hyperthreading optimization, real-time result streaming, intelligent test selection, and comprehensive automation for Syncfusion WPF controls and Azure SQL integration.

### Key Features
- **Microsoft Testing Platform 2025** - Next-generation test platform replacing VSTest
- **Hyperthreading Optimization** - Intelligent CPU utilization for maximum performance
- **Real-time Result Streaming** - Live test progress monitoring with JSON-RPC protocol
- **Syncfusion WPF UI Automation** - Automated testing of DataGrid, RibbonControl, DockingManager
- **Azure SQL Integration Testing** - Entity Framework Core 9 database testing patterns
- **Smart Test Selection** - Git-diff based impact analysis for efficient testing
- **FluentAssertions Advanced Patterns** - Modern assertion patterns with custom extensions

## 🚀 Quick Start

### Basic Testing Commands
```powershell
# Simple test execution with MTP 2025 optimization
bbTest

# Parallel execution with hyperthreading optimization
bbTest -Parallel -Coverage -Detailed

# Filtered testing with live results
bbTest -Filter "Category=Unit" -LiveResults

# Configuration-specific testing
bbTest -Configuration Release -Framework net9.0-windows
```

### Test Suite Execution
```powershell
# Predefined test suites
bbTest -TestSuite Unit -Parallel                    # Unit tests only
bbTest -TestSuite Integration -AzureSQLTests        # Integration + database tests
bbTest -TestSuite UI -SyncfusionUITests             # UI automation tests
bbTest -TestSuite Performance -PerformanceTests     # Performance & load tests
bbTest -TestSuite All -LiveResults                  # All tests with streaming
```

### Coverage Collection
```powershell
# Single format coverage
bbTest -IncludeCoverage -CoverageFormat cobertura

# Multiple format coverage
bbTest -IncludeCoverage -CoverageFormat "cobertura,opencover,json"

# Custom results directory
bbTest -IncludeCoverage -TestResultsPath "Reports" -CoverageFormat "cobertura,html"
```

## 🎮 Advanced Testing Features

### Test Discovery and Selection
```powershell
# Discover all available tests
Invoke-BusBuddyTestDiscovery -IncludeSource -OutputFormat json

# Smart test selection based on code changes
Select-BusBuddyImpactedTests -BaseBranch main -IncludeDependencies

# Run only impacted tests
$impactedCategories = Select-BusBuddyImpactedTests -BaseBranch main
bbTest -Categories $impactedCategories -NoBuild
```

### Live Test Execution
```powershell
# Real-time test execution with streaming results
Start-BusBuddyLiveTestExecution -TestSuite Performance -StreamResults

# Custom refresh interval for live monitoring
Start-BusBuddyLiveTestExecution -TestSuite UI -StreamResults -RefreshIntervalSeconds 1

# Background test execution
Start-BusBuddyLiveTestExecution -TestSuite All -StreamResults &
```

### Syncfusion WPF UI Automation
```powershell
# Test all Syncfusion controls
Invoke-BusBuddySyncfusionUITests

# Test specific control types
Invoke-BusBuddySyncfusionUITests -ControlType DataGrid -Interactive
Invoke-BusBuddySyncfusionUITests -ControlType RibbonControl -TimeoutSeconds 45
Invoke-BusBuddySyncfusionUITests -ControlType DockingManager

# Custom timeout for complex UI scenarios
Invoke-BusBuddySyncfusionUITests -ControlType All -TimeoutSeconds 60
```

### Azure SQL Integration Testing
```powershell
# Basic database integration testing
Invoke-BusBuddyAzureSQLTests

# Full dataset testing with cleanup
Invoke-BusBuddyAzureSQLTests -TestDataSet Full -CleanupTestData

# Custom connection string testing
Invoke-BusBuddyAzureSQLTests -ConnectionString "Server=custom;Database=BusBuddy;..."

# Minimal dataset for quick validation
Invoke-BusBuddyAzureSQLTests -TestDataSet Minimal
```

### Performance and Load Testing
```powershell
# Basic performance testing
Invoke-BusBuddyPerformanceTests

# High-load scenario testing
Invoke-BusBuddyPerformanceTests -ConcurrentUsers 100 -DurationMinutes 15

# Memory profiling during performance tests
Invoke-BusBuddyPerformanceTests -ConcurrentUsers 50 -DurationMinutes 10 -MemoryProfiling

# Specific scenario testing
Invoke-BusBuddyPerformanceTests -Scenario "StudentManagement" -ConcurrentUsers 25
```

### Test Result Analysis
```powershell
# Basic result analysis
Get-BusBuddyTestResults

# Comprehensive analysis with coverage
Get-BusBuddyTestResults -IncludeCoverage -GenerateReport -Format Detailed

# Custom results path analysis
Get-BusBuddyTestResults -ResultsPath "CustomResults" -Format Summary

# Historical trend analysis
Get-BusBuddyTestResults -ResultsPath "TestResults" -IncludeCoverage
```

## 🛠️ Testing Technology Stack

### Microsoft Testing Platform 2025 (MTP)
- **JSON-RPC Protocol** - Modern communication between test host and runners
- **Standalone Executables** - Independent test execution without Visual Studio dependencies
- **Native Integration** - Direct integration with .NET runtime and tooling
- **Enhanced Performance** - Optimized test discovery and execution

### NUnit 4.x Integration
```csharp
[Test]
[Category("Unit")]
public void StudentViewModel_Should_CalculateRouteDistance()
{
    // Modern NUnit testing with FluentAssertions
    var student = new Student { Id = 1, Name = "John Doe" };
    var route = new Route { Distance = 5.2 };
    
    var result = studentViewModel.CalculateDistance(student, route);
    
    result.Should().BeGreaterThan(0)
          .And.BeLessOrEqualTo(route.Distance);
}
```

### FluentAssertions Advanced Patterns
```csharp
[Test]
[Category("Integration")]
public void Students_Should_BeEquivalentToExpected()
{
    var students = await studentService.GetStudentsAsync();
    var expected = GetExpectedStudents();
    
    students.Should().BeEquivalentTo(expected, options => options
        .Excluding(s => s.Id)
        .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1)))
        .WhenTypeIs<DateTime>());
}

[Test]
[Category("Performance")]
public void StudentOperations_Should_MeetPerformanceRequirements()
{
    using (new AssertionScope())
    {
        var stopwatch = Stopwatch.StartNew();
        var result = studentService.ProcessLargeDataset(10000);
        stopwatch.Stop();
        
        result.Should().NotBeNull();
        result.Count.Should().Be(10000);
        stopwatch.ElapsedMilliseconds.Should().BeLessOrEqualTo(5000, "processing should complete within 5 seconds");
    }
}
```

### Syncfusion WPF Testing Patterns
```csharp
[Test]
[Category("Syncfusion")]
[Category("UI")]
public void DataGrid_Should_LoadStudentData()
{
    var dataGrid = new SfDataGrid();
    var students = GetTestStudents();
    
    dataGrid.ItemsSource = students;
    
    dataGrid.View.Records.Count.Should().Be(students.Count);
    dataGrid.Columns.Should().HaveCount(5);
}
```

### Azure SQL Testing Patterns
```csharp
[Test]
[Category("Database")]
[Category("Azure")]
public async Task StudentRepository_Should_SaveAndRetrieveStudent()
{
    using var context = CreateTestContext();
    var repository = new StudentRepository(context);
    
    var student = new Student 
    { 
        Name = "Test Student", 
        SchoolId = 1,
        RouteId = 1 
    };
    
    await repository.AddAsync(student);
    await context.SaveChangesAsync();
    
    var retrieved = await repository.GetByIdAsync(student.Id);
    
    retrieved.Should().BeEquivalentTo(student, options => options.Excluding(s => s.Id));
}
```

## 🏁 BusBuddy-Specific Testing Scenarios

### Student Management Workflow Testing
```powershell
# Test complete student management workflow
bbTest -Filter "FullyQualifiedName~BusBuddy.Tests.ViewModels.StudentsViewModel" -Detailed

# Test student CRUD operations
bbTest -Filter "Category=Unit&Name~Student&Name~CRUD" -Parallel

# Test student-route assignments
bbTest -Filter "Category=Integration&Name~StudentRoute" -AzureSQLTests
```

### Route Calculation Algorithm Testing
```powershell
# Test route calculation algorithms
bbTest -Filter "Category=Unit&Name~RouteCalculation" -Parallel -Coverage

# Test route optimization
bbTest -Filter "Category=Performance&Name~RouteOptimization" -PerformanceTests

# Test GPS integration (when available)
bbTest -Filter "Category=Integration&Name~GPS" --skip-if-unavailable
```

### Syncfusion Control Integration Testing
```powershell
# Test all Syncfusion DataGrid operations
Invoke-BusBuddySyncfusionUITests -ControlType DataGrid -Interactive

# Test ribbon control functionality
Invoke-BusBuddySyncfusionUITests -ControlType RibbonControl

# Test docking manager layout
Invoke-BusBuddySyncfusionUITests -ControlType DockingManager
```

### Database Integration Testing
```powershell
# Test Entity Framework Core integration
Invoke-BusBuddyAzureSQLTests -TestDataSet Full

# Test migration scripts
bbTest -Filter "Category=Database&Name~Migration" -AzureSQLTests

# Test data seeding
bbTest -Filter "Category=Database&Name~Seeding" -CleanupTestData
```

## ⚡ Performance Optimization

### Hyperthreading Optimization
```powershell
# Automatic hyperthreading detection and optimization
bbTest -Parallel -HyperthreadingMode Auto

# Use all logical cores (Intel HT / AMD SMT)
bbTest -Parallel -HyperthreadingMode LogicalCores -MaxCpuCount 16

# Use physical cores only
bbTest -Parallel -HyperthreadingMode PhysicalCores -MaxCpuCount 8

# Manual CPU count specification
bbTest -Parallel -HyperthreadingMode Manual -MaxCpuCount 12
```

### Memory Optimization
```powershell
# Memory-efficient coverage collection
bbTest -IncludeCoverage -CoverageFormat cobertura -Parallel

# Large dataset testing with memory monitoring
Invoke-BusBuddyPerformanceTests -ConcurrentUsers 100 -MemoryProfiling

# Timeout protection for memory-intensive tests
bbTest -TimeoutMinutes 30 -Parallel -MaxCpuCount 4
```

### Build Optimization
```powershell
# Skip build for rapid iteration
bbTest -NoBuild -Filter "Category=Unit"

# Build once, test multiple configurations
bbBuild -Configuration Release
bbTest -NoBuild -Configuration Release -TestSuite Unit
bbTest -NoBuild -Configuration Release -TestSuite Integration
```

## 🔄 CI/CD Integration

### GitHub Actions Integration
```yaml
name: Enhanced Testing Pipeline
on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET 9
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
          
      - name: Load BusBuddy PowerShell Module
        shell: pwsh
        run: |
          . .\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1
          
      - name: Health Check
        shell: pwsh
        run: bbHealth -Detailed
        
      - name: Build Solution
        shell: pwsh
        run: bbBuild -Configuration Release -MaxCpuCount 4
        
      - name: Run Unit Tests
        shell: pwsh
        run: bbTest -TestSuite Unit -Parallel -Coverage -LiveResults
        
      - name: Run Integration Tests
        shell: pwsh
        run: bbTest -TestSuite Integration -AzureSQLTests -Coverage
        env:
          BUSBUDDY_CONNECTION: ${{ secrets.BUSBUDDY_CONNECTION }}
          
      - name: Run UI Tests
        shell: pwsh
        run: Invoke-BusBuddySyncfusionUITests -ControlType All
        
      - name: Performance Testing
        shell: pwsh
        run: Invoke-BusBuddyPerformanceTests -ConcurrentUsers 25 -DurationMinutes 5
        
      - name: Analyze Results
        shell: pwsh
        run: Get-BusBuddyTestResults -IncludeCoverage -GenerateReport
```

### Local CI Simulation
```powershell
# Simulate complete CI pipeline locally
function Invoke-LocalCIPipeline {
    bbHealth -Detailed
    bbBuild -Configuration Release
    bbTest -TestSuite Unit -Parallel -Coverage
    bbTest -TestSuite Integration -AzureSQLTests
    Invoke-BusBuddySyncfusionUITests
    Invoke-BusBuddyPerformanceTests -ConcurrentUsers 10 -DurationMinutes 2
    Get-BusBuddyTestResults -IncludeCoverage -GenerateReport
}

Invoke-LocalCIPipeline
```

### Test Result Publishing
```powershell
# Generate comprehensive test reports
bbTest -TestSuite All -Parallel -Coverage -CoverageFormats @('cobertura', 'opencover', 'html')

# Analyze and publish results
Get-BusBuddyTestResults -IncludeCoverage -GenerateReport -Format Detailed

# Export results for external tools
bbTest -Logger "junit;LogFileName=junit-results.xml" -Logger "trx;LogFileName=vs-results.trx"
```

## 🔧 Troubleshooting

### Common Issues and Solutions

#### Test Discovery Issues
```powershell
# If tests are not being discovered
Invoke-BusBuddyTestDiscovery -IncludeSource -Filter ""

# Clear test discovery cache
Remove-Item -Path "TestResults" -Recurse -Force
bbTest -Filter "Category=Unit" -NoBuild
```

#### Performance Issues
```powershell
# If tests are running slowly
bbTest -Parallel -HyperthreadingMode Auto -MaxCpuCount ([Environment]::ProcessorCount)

# If memory issues occur
bbTest -MaxCpuCount 4 -TimeoutMinutes 15

# Monitor resource usage
Invoke-BusBuddyPerformanceTests -MemoryProfiling -ConcurrentUsers 5
```

#### Coverage Collection Issues
```powershell
# If coverage is not being collected
bbTest -IncludeCoverage -CoverageFormat cobertura -Verbosity detailed

# Alternative coverage collection
bbTest -IncludeCoverage -CoverageFormat opencover
```

#### UI Test Issues
```powershell
# If Syncfusion UI tests fail
Invoke-BusBuddySyncfusionUITests -Interactive -TimeoutSeconds 60

# Check UI test environment
$env:SYNCFUSION_LICENSE_KEY | Should -Not -BeNullOrEmpty
```

#### Database Test Issues
```powershell
# If Azure SQL tests fail
Invoke-BusBuddyAzureSQLTests -TestDataSet Minimal

# Check connection string
$env:BUSBUDDY_TEST_CONNECTION_STRING | Should -Not -BeNullOrEmpty
```

### Diagnostic Commands
```powershell
# Check test environment
bbHealth -Detailed

# Validate PowerShell module status
Get-Module BusBuddy* | Format-Table Name, Version, ModuleType

# Check available test frameworks
dotnet --list-sdks
dotnet test --help

# Validate test project structure
Get-ChildItem -Path . -Filter "*Tests*.csproj" -Recurse
```

### Performance Benchmarking
```powershell
# Benchmark test execution time
Measure-Command { bbTest -TestSuite Unit -Parallel }

# Compare parallel vs sequential execution
Measure-Command { bbTest -TestSuite Unit }
Measure-Command { bbTest -TestSuite Unit -Parallel }

# Memory usage during testing
Invoke-BusBuddyPerformanceTests -MemoryProfiling -ConcurrentUsers 1 -DurationMinutes 1
```

## 📚 Additional Resources

### Documentation Links
- [Microsoft Testing Platform 2025](https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-architecture-extensions)
- [NUnit Documentation](https://docs.nunit.org/)
- [FluentAssertions Documentation](https://fluentassertions.com/introduction)
- [Syncfusion WPF Controls](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
- [Entity Framework Core Testing](https://learn.microsoft.com/ef/core/testing/)

### BusBuddy Testing Standards
- All new features must include unit tests with 80%+ coverage
- UI components must include Syncfusion-specific automation tests
- Database operations must include integration tests
- Performance-critical features must include load tests
- All tests must pass in both Debug and Release configurations

### Command Reference
```powershell
# Quick reference for all testing commands
Get-Help bbTest -Examples
Get-Help Invoke-BusBuddySyncfusionUITests -Examples
Get-Help Invoke-BusBuddyPerformanceTests -Examples
Get-Help Start-BusBuddyLiveTestExecution -Examples

# List all available testing functions
Get-Command *BusBuddy*Test* | Format-Table Name, Source
```

---

**BusBuddy Testing Guide 2025**  
*Enhanced with Microsoft Testing Platform integration*  
*Version: 2.0.0*  
*Last Updated: 2025*  
*Compatibility: .NET 9.0, PowerShell 7.5.2, MTP 2025*
