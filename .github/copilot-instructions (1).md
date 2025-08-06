# GitHub Copilot Custom Instructions - Phase 1 Focused

This file contains custom instructions for GitHub Copilot when working with this repository.

## 🎯 **PHASE 1 PRIORITY OVERRIDE**

**CRITICAL: All instructions below are SECONDARY to Phase 1 Core Goals**

### **Phase 1 Mission (30 Minutes)**
- **PRIMARY GOAL**: MainWindow → Dashboard → 3 Core Views (Drivers, Vehicles, Activity Schedule) with real-world data
- **FUNCTIONAL OVER PERFECT**: Get it working first, optimize later
- **NO COMPREHENSIVE REWRITES**: Fix specific issues, build incrementally
- **PRESERVE EXISTING WORK**: Enhance and repair rather than replace

### **Phase 1 Development Standards**
- ✅ **Build without errors** - Priority #1
- ✅ **Basic data display** - ListView/DataGrid sufficient for now
- ✅ **Real transportation data** - 15-20 drivers, 10-15 vehicles, 25-30 activities
- ⚠️ **Defer to Phase 2**: Advanced MVVM, comprehensive testing, performance optimization
- ⚠️ **Defer to Phase 2**: Advanced styling, complex validation, external integrations

### **Enhanced Task Monitoring Standards**
- **Enhanced Task Monitor**: Use `enhanced-task-monitor-fixed.ps1` for ALL builds/runs
- **Output Capture**: All task output saved to timestamped log files in `logs/` directory
- **Real-time Feedback**: Show build/run progress with comprehensive error capture
- **Process Completion Detection**: Wait for process completion and capture full output
- **Incremental Fixes**: Use captured output to make targeted repairs, not rewrites

### **Phase 1 Task Monitoring Commands**
```powershell
# Test the enhanced task monitor
pwsh -ExecutionPolicy Bypass -File "enhanced-task-monitor-fixed.ps1" -TaskName "Build Test" -Command "dotnet" -Arguments "build","BusBuddy.sln" -WaitForCompletion -CaptureOutput -ShowRealTime

# Use the helper functions
& "enhanced-task-monitor-fixed.ps1"; Start-BusBuddyTask -TaskType Build
& "enhanced-task-monitor-fixed.ps1"; Start-BusBuddyTask -TaskType Run
& "enhanced-task-monitor-fixed.ps1"; Start-BusBuddyTask -TaskType Health
```

### **Phase 1 Problem Resolution Approach**
- **Incremental fixes first** - Always attempt targeted edits before complete rebuilds
- **Assess corruption level** - Check actual errors and their scope before deciding approach
- **User consultation** - Let the user decide on complete overhauls vs. targeted fixes
- **Error analysis** - Identify root causes (missing methods, property name mismatches, duplicates)
- **Minimal viable fix** - Use the smallest change that resolves the issue
- **Escalation path**:
  1. First: Targeted edits for specific errors
  2. Second: Consult user if issues appear complex
  3. Last resort: Complete rebuild only with user approval

---

## Error Handling and Resilience Standards - Phase 1 Simplified

### **Phase 1 Error Handling (Keep It Simple)**
- ✅ **Basic Try/Catch**: Simple exception handling around data operations
- ✅ **User Messages**: Basic MessageBox.Show() for user feedback
- ✅ **Log to Console**: Simple Console.WriteLine for debugging (upgrade to Serilog later)
- ⚠️ **Defer**: Complex resilience patterns, retry logic, circuit breakers

### **Phase 1 Error Pattern**
```csharp
try
{
    // Data operation
    var data = await context.Drivers.ToListAsync();
    return data;
}
catch (Exception ex)
{
    // Simple error handling for Phase 1
    Console.WriteLine($"Error loading drivers: {ex.Message}");
    MessageBox.Show($"Error loading drivers: {ex.Message}");
    return new List<Driver>();
}
```

- In XML/XAML comments, always replace double-dash (`--`) with em dash (`—`)
- Ensure no XML comment ends with a dash character (`-`) by adding a space or period if needed
- Always validate XML comment syntax to ensure it's well-formed
- When adding new comments, use em dashes (`—`) instead of double dashes

## File Organization and Structure Standards

### Solution Structure
- **BusBuddy.Core**: Core business logic, models, services, and data access
- **BusBuddy.WPF**: WPF presentation layer with Views, ViewModels, and UI-specific services
- **BusBuddy.Tests**: Comprehensive test suite for all layers

### WPF Project Organization (BusBuddy.WPF)
- **Assets/**: Static resources (images, fonts, icons)
- **Controls/**: Custom user controls and control templates
- **Converters/**: Value converters for data binding
- **Extensions/**: Extension methods and helpers
- **Logging/**: Logging configuration and enrichers
- **Models/**: UI-specific model classes and DTOs
- **Resources/**: Resource dictionaries, styles, and themes
- **Services/**: UI services (Navigation, Dialog, etc.)
- **Utilities/**: Helper classes and utility functions
- **ViewModels/**: MVVM ViewModels organized by feature
- **Views/**: XAML views organized by feature

### Feature-Based Organization
- **Domain Folders**: Group related files by business domain (Activity, Bus, Dashboard, Driver, etc.)
- **Paired Files**: Keep View and ViewModel files in corresponding folders
- **Naming Convention**: Use consistent naming patterns (e.g., `BusManagementView.xaml` / `BusManagementViewModel.cs`)

### Core Project Organization (BusBuddy.Core)
- **Configuration/**: App configuration and settings
- **Data/**: Entity Framework contexts and configurations
- **Extensions/**: Core extension methods
- **Interceptors/**: EF interceptors and data access enhancements
- **Migrations/**: Entity Framework migrations
- **Models/**: Domain models and entities
- **Services/**: Business logic services with interfaces
- **Utilities/**: Core utility classes and helpers

### File Naming Conventions
- **ViewModels**: Use descriptive names ending with `ViewModel` (e.g., `BusManagementViewModel.cs`)
- **Views**: Use descriptive names ending with `View` (e.g., `BusManagementView.xaml`)
- **Services**: Use descriptive names ending with `Service` (e.g., `NavigationService.cs`)
- **Interfaces**: Prefix with `I` (e.g., `INavigationService.cs`)
- **Base Classes**: Use `Base` prefix (e.g., `BaseViewModel.cs`)
- **Extensions**: Use descriptive names ending with `Extensions` (e.g., `DatabaseExtensions.cs`)

### Folder Structure Rules
- **Mirror Structure**: ViewModels and Views folders should mirror each other
- **Logical Grouping**: Group related functionality in domain-specific folders
- **Separation of Concerns**: Keep UI logic in WPF project, business logic in Core project
- **Resource Organization**: Organize resources by type and usage (themes, styles, templates)

### File Placement Guidelines
- **New ViewModels**: Place in appropriate domain folder under `ViewModels/`
- **New Views**: Place in corresponding domain folder under `Views/`
- **New Services**: Place in `Services/` folder with appropriate interface
- **New Models**: UI models in WPF/Models/, domain models in Core/Models/
- **New Extensions**: Group by functionality in appropriate Extensions/ folder
- **New Utilities**: Place in project-appropriate Utilities/ folder

## Debug Helper Integration Patterns

- **App.xaml.cs Integration**: The `DebugHelper` class in `BusBuddy.WPF.Utilities` provides debug functionality accessible via PowerShell
- **Command Line Arguments**: Application supports debug arguments (`--start-debug-filter`, `--export-debug-json`, etc.)
- **Real-time Filtering**: Use `DebugOutputFilter` for live debug output analysis and filtering
- **Actionable Error Detection**: Implement `HasCriticalIssues()` and priority-based error categorization
- **PowerShell Bridge**: All debug methods accessible via `bb-debug-*` PowerShell commands

### Debug Helper Method Patterns
- **Static Methods**: All debug helper methods are static and accessible without instantiation
- **Conditional Compilation**: Use `[Conditional("DEBUG")]` for debug-only functionality
- **Structured Output**: Debug output uses structured formatting with priority indicators
- **Event Integration**: Subscribe to `HighPriorityIssueDetected` and `NewEntriesFiltered` events
- **JSON Export**: Support exporting debug data to JSON for external tool integration

### PowerShell Debug Command Patterns
```powershell
# Start debug filter
bb-debug-start          # Calls DebugHelper.StartAutoFilter()

# Export debug data
bb-debug-export         # Calls DebugHelper.ExportToJson()

# Health monitoring
bb-health              # Calls DebugHelper.HealthCheck()

# Test functionality
bb-debug-test          # Calls DebugHelper.TestAutoFilter()
```

### Debug Output Standards
- **Priority Levels**: Use 1 (Critical), 2 (High), 3 (Medium), 4 (Low) for issue classification
- **Actionable Recommendations**: Include specific remediation steps for each detected issue
- **Real-time Notifications**: Trigger UI notifications for Priority 1 (Critical) issues only
- **Structured Data**: Use consistent JSON schema for debug data export
- **Performance Impact**: Minimize performance overhead of debug monitoring in production builds

## Logging Standards (Serilog ONLY with Enrichments)

- **ONLY use Serilog** for ALL logging throughout the application - no other logging methods
- **Static Logger Pattern**: Use `private static readonly ILogger Logger = Log.ForContext<ClassName>();` in each class
- **Structured Logging**: Always use structured logging with message templates and properties
  ```csharp
  Logger.Information("User {UserId} performed {Action} on {Entity}", userId, action, entity);
  ```
- **Log Context Enrichment**: Use `LogContext.PushProperty()` for operation-specific context enrichment
- **Exception Logging**: Always log exceptions with context: `Logger.Error(ex, "Operation failed for {Context}", context)`
- **Performance Logging**: Use `using (LogContext.PushProperty("Operation", "OperationName"))` for tracking operations
- **Startup Logging**: Include enhanced startup logging with operation markers and timing
- **Error Enrichment**: Use structured error data with actionable information
- **ViewModel Logging**: Use BaseViewModel logging patterns with correlation IDs and timing
- **Service Layer Logging**: Log all service operations with structured context and performance metrics
- **Enrichment Patterns**: Use Serilog enrichers for automatic property injection (environment, thread, correlation IDs)
- **No Console.WriteLine**: Replace any Console.WriteLine with appropriate Serilog levels
- **No Debug.WriteLine**: Replace any Debug.WriteLine with Logger.Debug() calls
- **No Trace.WriteLine**: Replace any Trace.WriteLine with Logger.Verbose() calls

## Architecture Standards - Phase 1 Simplified

### **Phase 1 Architecture (Minimum Viable)**
- ✅ **Basic MVVM**: Simple ViewModels with INotifyPropertyChanged, defer advanced patterns
- ✅ **Direct Data Access**: Simple Entity Framework queries, defer complex repositories
- ✅ **Basic Navigation**: Simple Frame.Navigate() calls, defer advanced navigation service
- ✅ **Essential Error Handling**: Try/catch on data operations, defer comprehensive patterns
- ⚠️ **Defer**: Complex dependency injection, advanced async patterns, comprehensive validation

### **Phase 1 Quick Patterns**
```csharp
// Quick ViewModel pattern for Phase 1
public class DriversViewModel : INotifyPropertyChanged
{
    public ObservableCollection<Driver> Drivers { get; set; } = new();

    public async Task LoadDriversAsync()
    {
        try
        {
            using var context = new BusBuddyContext();
            var drivers = await context.Drivers.ToListAsync();
            Drivers.Clear();
            foreach(var driver in drivers) Drivers.Add(driver);
        }
        catch (Exception ex)
        {
            // Basic error handling for Phase 1
            MessageBox.Show($"Error loading drivers: {ex.Message}");
        }
    }
}

// Quick navigation pattern for Phase 1
private void NavigateToDrivers() => ContentFrame.Navigate(new DriversView());
```

## MVVM Implementation Standards - Phase 1 Focused

### **Phase 1 MVVM (Keep It Simple)**
- ✅ **Basic ViewModels**: Implement INotifyPropertyChanged manually for now
- ✅ **Simple Commands**: Use basic RelayCommand, defer advanced command patterns
- ✅ **Direct Binding**: Basic two-way binding, defer complex converters
- ✅ **Observable Collections**: Use ObservableCollection<T> for lists
- ⚠️ **Defer**: Advanced MVVM frameworks, complex validation, sophisticated patterns

### **Phase 1 Data Binding**
```xml
<!-- Simple data binding for Phase 1 -->
<DataGrid ItemsSource="{Binding Drivers}" AutoGenerateColumns="True" />
<TextBox Text="{Binding SelectedDriver.Name, Mode=TwoWay}" />
```

## Database and Entity Framework Standards - Phase 1 Simplified

### **Phase 1 Database (Direct and Simple)**
- ✅ **Basic DbContext**: Simple context with DbSet properties
- ✅ **Direct Queries**: Basic LINQ queries, defer complex repositories
- ✅ **Simple Migrations**: Basic EF migrations, defer complex schema management
- ✅ **Basic Connection**: Simple connection string, defer advanced resilience
- ⚠️ **Defer**: Advanced patterns, connection pooling, complex error handling

### **Phase 1 Database Pattern**
```csharp
// Simple context for Phase 1
public class BusBuddyContext : DbContext
{
    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Activity> Activities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Data Source=BusBuddy.db");
    }
}

// Simple query pattern for Phase 1
var drivers = await context.Drivers.ToListAsync();
```

## Error Handling and Resilience Standards

- **Comprehensive Exception Handling**: Use `ExceptionHelper` for consistent exception analysis
- **Retry Patterns**: Implement exponential backoff for transient failures
- **Circuit Breaker**: Use circuit breaker pattern for external dependencies
- **Validation Layers**: Implement multiple validation layers (client, service, database)
- **Null Safety**: Use nullable reference types and proper null checks throughout
- **Resource Management**: Always implement `IDisposable` for resources, use `using` statements
- **Async Exception Handling**: Properly handle exceptions in async operations
- **User-Friendly Messages**: Always provide actionable error messages to users
- **Debugging Support**: Include debugging aids like `Debugger.Break()` for development

## Null Safety and Validation Standards

- **Nullable Reference Types**: Enable and use nullable reference types throughout
- **Null Coalescing**: Use `??` operator for safe null handling: `value ?? defaultValue`
- **Null Conditional**: Use `?.` operator for safe member access: `object?.Property`
- **Guard Clauses**: Use `ArgumentNullException.ThrowIfNull()` for parameter validation
- **Entity Defaults**: Provide sensible defaults for required entity properties
- **Collection Initialization**: Initialize collections in constructors to avoid null references
- **Service Layer Validation**: Validate inputs in service methods before processing
- **ViewModel Validation**: Implement validation in ViewModels for user input
- **Database Null Handling**: Use proper nullable column types and handle null values in queries

## Testing Standards

- **Unit Tests**: Create comprehensive unit tests for all business logic and ViewModels
- **Integration Tests**: Test service interactions and data layer operations
- **Null Handling Tests**: Specifically test null scenarios and edge cases
- **Async Testing**: Use proper async testing patterns with `Task.Run` and cancellation tokens
- **Performance Tests**: Include performance benchmarks for critical operations
- **Validation Tests**: Test all validation scenarios and error conditions
- **Mock Services**: Use `Mock<T>` for service dependencies in tests
- **Database Tests**: Test database operations with proper transaction management

## Code Style Guidelines

- Follow the existing code style in the repository
- Use the established Syncfusion FluentDark theme standards for UI components
- Maintain consistent naming conventions for styles, resources, and other identifiers
- Ensure all Syncfusion controls have the proper namespace declarations and theme settings
- **Performance**: Consider performance implications of UI updates and data operations
- **Memory Management**: Be mindful of memory usage, especially with large collections and long-running operations
- **User Experience**: Ensure all UI operations provide appropriate feedback and loading states

## Startup and Configuration Standards

- **WPF Startup Pattern**: Use `App.xaml.cs` for application initialization and service configuration
- **Host Builder Pattern**: Use `IHost` with `CreateDefaultBuilder()` for dependency injection in WPF
- **Startup Validation**: Use `StartupValidationService` for comprehensive application health checks
- **Configuration Management**: Use `IConfigurationService` for centralized configuration access
- **Environment Handling**: Use `EnvironmentHelper` for environment-specific logic
- **Service Dependencies**: Validate all critical service dependencies in `App.xaml.cs`
- **License Management**: Register Syncfusion license in App constructor before any UI initialization
- **Security Validation**: Implement security checks for production deployments in `OnStartup`
- **Performance Monitoring**: Monitor startup performance and log timing metrics
- **Service Registration**: Use `ConfigureServices` methods in `App.xaml.cs` for clean DI setup
- **Startup Orchestration**: Use `StartupOrchestrationService` for complex initialization sequences

## Syncfusion Integration Standards

- **Version 30.1.40**: This project uses Syncfusion Essential Studio for WPF version 30.1.40
- **Official Documentation**: [Syncfusion WPF Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
- **Theme Documentation**: [FluentDark Theme Guide](https://help.syncfusion.com/wpf/themes/fluent-dark-theme)
- **Control References**: [WPF Control Gallery](https://help.syncfusion.com/wpf/control-gallery)
- **Migration Guides**: [Version Migration Documentation](https://help.syncfusion.com/wpf/upgrade-guide)

### Control-Specific Documentation Links
- **DockingManager**: [DockingManager Documentation](https://help.syncfusion.com/wpf/docking/getting-started)
- **NavigationDrawer**: [NavigationDrawer Documentation](https://help.syncfusion.com/wpf/navigation-drawer/getting-started)
- **SfDataGrid**: [DataGrid Documentation](https://help.syncfusion.com/wpf/datagrid/getting-started)
- **SfChart**: [Chart Documentation](https://help.syncfusion.com/wpf/charts/getting-started)
- **RibbonControl**: [Ribbon Documentation](https://help.syncfusion.com/wpf/ribbon/getting-started)

### Implementation Standards
- **Theme Consistency**: Use FluentDark/FluentLight themes consistently across all Syncfusion controls
- **Assembly Management**: Reference precompiled .NET 8.0 assemblies with proper HintPath configurations
- **Control Standards**: Follow established patterns for DockingManager, NavigationDrawer, and other controls
- **Resource Organization**: Maintain organized resource dictionaries for themes and styles
- **Validation Utilities**: Use `SyncfusionValidationUtility` for runtime validation of control properties
- **DateTime Patterns**: Use validated DateTimePattern values to prevent runtime errors
- **Performance Optimization**: Use appropriate control settings for optimal performance

### License Management
- **License Registration**: Always register Syncfusion license in App constructor before UI initialization
  ```csharp
  Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR_LICENSE_KEY");
  ```
- **Environment Variables**: Store license keys in environment variables for security
- **Development Builds**: Use community license for development, commercial license for production

## Documentation Standards

- Document all public APIs with clear and concise comments
- For complex logic, add explanatory comments that describe the purpose, not just the mechanics
- Use standardized section headers in resource dictionaries and other configuration files
- Maintain clear separation of concerns in documentation sections
- **XML Documentation**: Use proper XML documentation for all public members
- **README Updates**: Keep README.md current with setup instructions and architecture overview
- **Code Comments**: Include purpose-driven comments for complex business logic
- **Architecture Documentation**: Document architectural decisions and patterns used

### VS Code Settings Integration Patterns
```json
// PowerShell terminal configuration in .vscode/settings.json
"terminal.integrated.profiles.windows": {
  "PowerShell 7.5.2": {
    "path": "pwsh.exe",
    "args": ["-NoProfile", "-NoExit", "-Command",
      "& 'C:\\path\\to\\BusBuddy-PowerShell-Profile.ps1';
       & 'C:\\path\\to\\BusBuddy-Advanced-Workflows.ps1'"]
  }
}
```

### Task Explorer Configuration Standards
- **Exclusive Interface**: Task Explorer is the ONLY approved method for running tasks
- **No Direct Commands**: Avoid using direct terminal commands for builds/runs
- **Profile Integration**: Tasks automatically have access to PowerShell profile functions
- **Keyboard Shortcuts**: Configure `Ctrl+Shift+P` → "Task Explorer: Run Task" workflows
- **Task Dependencies**: Configure tasks as independent, non-chaining operations

### Command Integration Examples
```powershell
# Complete development session startup
bb-dev-session          # Opens workspace, builds, starts debug monitoring

# Quick test cycle
bb-quick-test           # Clean, build, test, validate

# Comprehensive system analysis
bb-diagnostic           # Full environment and project health check

# Export debug data for analysis
bb-report               # Generate comprehensive project report
```

## PowerShell Development Environment Integration

- **PowerShell Core 7.5.2**: Use PowerShell Core for all development scripting and task automation
- **VS Code Integration**: Use robust `code` command integration with automatic VS Code/VS Code Insiders detection
- **Task Explorer Exclusive**: Task Explorer is the ONLY method for task management - no direct terminal commands for builds
- **Debug Helper Integration**: All `DebugHelper` methods from `App.xaml.cs` accessible via PowerShell commands

### PowerShell Profile Standards
- **Profile Location**: `BusBuddy-PowerShell-Profile.ps1` in project root for core functionality
- **Advanced Workflows**: `BusBuddy-Advanced-Workflows.ps1` for comprehensive development automation
- **Auto-Loading**: VS Code terminal profiles automatically load both PowerShell files
- **Function Naming**: Use `Verb-BusBuddyNoun` pattern for all Bus Buddy specific functions
- **Alias Standards**: Use `bb-` prefix for all Bus Buddy command aliases

### Core PowerShell Commands
- **VS Code Integration**: `code`, `vs`, `vscode`, `edit`, `edit-file` with robust path detection
- **Basic Bus Buddy**: `bb-open`, `bb-build`, `bb-run` for fundamental operations
- **Debug Integration**: `bb-debug-start`, `bb-debug-stream`, `bb-health`, `bb-debug-export`
- **Advanced Workflows**: `bb-dev-session`, `bb-quick-test`, `bb-diagnostic`, `bb-report`

### Debug System Integration
- **DebugHelper Methods**: All static methods from `BusBuddy.WPF.Utilities.DebugHelper` accessible via PowerShell
- **Real-time Streaming**: Use `DebugOutputFilter.StartRealTimeStreaming()` for live debug monitoring
- **JSON Export**: Export actionable debug items for VS Code integration and analysis
- **Command Line Arguments**: Support `--start-debug-filter`, `--export-debug-json`, `--start-streaming`
- **Health Monitoring**: Automatic system health checks with `HasCriticalIssues()` detection

### Advanced Workflow Standards
- **Development Sessions**: Use `Start-BusBuddyDevSession` for complete environment setup
- **Quick Testing**: Use `Start-BusBuddyQuickTest` for rapid build-test-validate cycles
- **Comprehensive Diagnostics**: Use `Invoke-BusBuddyFullDiagnostic` for system health analysis
- **Project Reporting**: Use `Export-BusBuddyProjectReport` for debug data and system status export
- **Log Monitoring**: Use `Watch-BusBuddyLogs` for real-time log file monitoring

### VS Code Configuration Integration
- **Terminal Profiles**: Configure PowerShell 7.5.2 as default with profile auto-loading
- **Task Explorer**: Use Task Explorer extension as exclusive task management interface
- **Settings Integration**: PowerShell configuration in `.vscode/settings.json` with profile paths
- **Command Integration**: Seamless `code` command functionality across all PowerShell sessions
- **Extension Requirements**: XAML Styler and Task Explorer extensions for optimal workflow

### Error Handling in PowerShell
- **Structured Error Handling**: Use try-catch with meaningful error messages and logging
- **Path Validation**: Always validate workspace and project paths before operations
- **Exit Code Checking**: Check `$LASTEXITCODE` after all dotnet commands
- **Fallback Mechanisms**: Provide fallback options when primary commands fail
- **User Feedback**: Use color-coded console output for status, errors, and success messages

### PowerShell 7.5.2 Specific Features and Patterns
- **Parallel Processing**: Use `ForEach-Object -Parallel` for concurrent operations (max 5 threads default)
  ```powershell
  $files | ForEach-Object -Parallel { dotnet build $_ } -ThrottleLimit 3
  ```
- **Ternary Operators**: Leverage `condition ? true_value : false_value` syntax for concise conditionals
- **Pipeline Chain Operators**: Use `&&` and `||` for conditional pipeline execution
  ```powershell
  dotnet build && dotnet test || Write-Error "Build or test failed"
  ```
- **Null Conditional Operators**: Use `?.` and `?[]` for safe property/array access
- **String Interpolation**: Use `$()` within double quotes for complex expressions
- **Error Handling**: Leverage `$?` automatic variable for last command success status
- **JSON Cmdlets**: Use native `ConvertTo-Json` and `ConvertFrom-Json` with `-Depth` parameter
- **Cross-Platform Paths**: Use `Join-Path` and `Resolve-Path` for platform-agnostic path handling
- **Module Management**: Use `Import-Module -Force` for development module reloading
- **Background Jobs**: Use `Start-ThreadJob` for lightweight background tasks over `Start-Job`

### Performance and Optimization
- **Background Jobs**: Use PowerShell jobs for long-running debug operations
- **Lazy Loading**: Load advanced workflows only when needed
- **Caching**: Cache frequently accessed paths and configuration data
- **Minimal Dependencies**: Keep PowerShell profiles lightweight with fast loading times
- **Concurrent Safety**: Ensure PowerShell functions work safely with multiple VS Code instances
- **Parallel Execution**: Use PowerShell 7.5.2 parallel features for concurrent builds and tests
- **Memory Management**: Use `[System.GC]::Collect()` sparingly and only when necessary
- **Stream Processing**: Use pipeline streaming for large data sets to reduce memory footprint

### Development Workflow Integration
- **IDE Agnostic**: PowerShell functions work from any terminal, not just VS Code
- **Cross-Session**: Functions available across all PowerShell sessions in the project
- **State Management**: Maintain development session state across PowerShell restarts
- **Documentation**: Use comprehensive help documentation with examples for all functions
- **Version Control**: Include PowerShell profiles in version control for team consistency

## Development Workflow Standards

- **Independent Tasks**: VS Code tasks are configured to run independently without chaining
- **Build Process**: Clean → Restore → Build → Run as separate, non-dependent tasks
- **Git Workflow**: Create backup commits before major changes, use descriptive commit messages
- **Incremental Development**: Test after each significant change, validate builds frequently
- **Code Reviews**: Follow established code review practices and standards
- **Branch Management**: Use appropriate branching strategies for feature development

## Security Standards

- **Connection String Security**: Never hardcode sensitive connection strings
- **Environment Variables**: Use environment variables for sensitive configuration
- **Input Validation**: Validate all user inputs at multiple layers
- **SQL Injection Prevention**: Use parameterized queries and Entity Framework properly
- **Authentication**: Implement proper authentication and authorization patterns
- **Data Protection**: Encrypt sensitive data at rest and in transit
- **Audit Logging**: Log security-relevant events for compliance and monitoring

## Performance Standards

- **Async Operations**: Use async/await for all I/O operations
- **Lazy Loading**: Implement lazy loading for expensive operations
- **Caching Strategies**: Use appropriate caching for frequently accessed data
- **Database Optimization**: Use proper indexing and query optimization
- **Memory Management**: Monitor memory usage and implement proper disposal patterns
- **UI Performance**: Optimize UI updates and data binding performance
- **Background Processing**: Use background tasks for long-running operations

## Best Practices

- When suggesting changes, ensure they align with the established patterns in the codebase
- Prioritize maintainability and readability over clever or complex solutions
- When working with XAML UI elements, consider accessibility implications
- Validate that changes don't break existing functionality
- **Service Layer Design**: Keep services focused and follow single responsibility principle
- **Error Recovery**: Implement graceful error recovery where possible
- **Monitoring**: Include appropriate monitoring and telemetry for production systems

## Troubleshooting Guide

### Common PowerShell Errors and Solutions

#### Execution Policy Errors
```powershell
# Error: Execution policy does not allow this script to run
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

#### Module Import Failures
```powershell
# Error: Module not found or cannot be imported
Import-Module -Name ModuleName -Force -Verbose
Get-Module -ListAvailable | Where-Object { $_.Name -like "*ModuleName*" }
```

#### Path Resolution Issues
```powershell
# Error: Path not found or cannot be resolved
Test-Path -Path $variablePath -PathType Container
Resolve-Path -Path $relativePath -ErrorAction SilentlyContinue
```

#### Parallel Execution Errors
```powershell
# Error: Parallel execution throttling or timeout
$files | ForEach-Object -Parallel {
    $using:function
} -ThrottleLimit 2 -TimeoutSeconds 30
```

#### JSON Conversion Issues
```powershell
# Error: JSON depth exceeded or invalid JSON
ConvertTo-Json -InputObject $data -Depth 10 -Compress
ConvertFrom-Json -InputObject $jsonString -ErrorAction Stop
```

### Common Build and Development Issues

#### NuGet Package Restore Failures
- **Solution**: Clear NuGet cache and restore packages
  ```powershell
  dotnet nuget locals all --clear
  dotnet restore --force --no-cache
  ```

#### Entity Framework Migration Issues
- **Solution**: Validate database connection and reset migrations if needed
  ```powershell
  dotnet ef database drop --force
  dotnet ef database update
  ```

#### Syncfusion License Errors
- **Solution**: Verify license registration and environment variables
  ```csharp
  // Ensure license is registered before any Syncfusion control initialization
  Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
  ```

#### XAML Compilation Errors
- **Solution**: Check namespace declarations and control references
  ```xml
  xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
  ```

#### Debug Output Filter Issues
- **Solution**: Restart debug filter and validate configuration
  ```powershell
  bb-debug-start -Force
  bb-health -Verbose
  ```

### Performance Troubleshooting

#### High Memory Usage
- **Monitor**: Use `bb-diagnostic` to check memory consumption patterns
- **Solution**: Implement proper disposal patterns and weak references
- **Tools**: Use dotMemory or PerfView for detailed analysis

#### Slow UI Response
- **Monitor**: Check UI thread blocking and async operation completion
- **Solution**: Move long-running operations to background threads
- **Validation**: Use `BaseViewModel.ExecuteCommandAsync()` patterns

#### Database Connection Issues
- **Monitor**: Enable EF Core logging and connection resilience metrics
- **Solution**: Use `DatabaseOperationExtensions.SafeQueryAsync()` patterns
- **Recovery**: Implement retry policies and circuit breaker patterns

### Development Environment Issues

#### VS Code Task Failures
- **Solution**: Use Task Explorer exclusively, avoid direct terminal commands
- **Validation**: Check task configuration in `.vscode/tasks.json`
- **Debugging**: Use `bb-diagnostic` to validate environment setup

#### PowerShell Profile Loading Issues
- **Solution**: Validate profile paths and execution policies
  ```powershell
  Test-Path -Path $PROFILE.CurrentUserAllHosts
  Get-ExecutionPolicy -List
  ```

#### Extension Compatibility Problems
- **Required Extensions**: XAML Styler, Task Explorer, PowerShell
- **Solution**: Update extensions and validate compatibility with VS Code version
- **Alternative**: Use VS Code Insiders for latest extension support

### Logging and Monitoring Issues

#### Serilog Configuration Problems
- **Solution**: Validate logger configuration and enricher setup
- **Check**: Ensure `Log.ForContext<ClassName>()` pattern usage
- **Debug**: Enable self-logging in Serilog configuration

#### Missing Log Entries
- **Solution**: Verify log level configuration and output sinks
- **Check**: Ensure structured logging patterns with message templates
- **Validation**: Use `Logger.Information()` instead of `Console.WriteLine()`

These instructions help maintain consistent code quality, architecture, and development practices throughout the project.
