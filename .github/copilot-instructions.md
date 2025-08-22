# GitHub Copilot Custom Instructions - BusBuddy Excellence Development

**Purpose**: Guides AI assistants (GitHub Copilot, Grok, etc.) on technical standards, compliance, and anti-regression rules for BusBuddy development. For project-specific quality goals and developer workflows, see `vscode-userdata/BusBuddy.instructions.md`.

**Scope**: Ensures AI suggestions align with Microsoft, Syncfusion, and .NET standards, prioritize clean builds, and prevent regressions during excellence-driven development.

## 🎯 **BusBuddy MVP Priority**

## 🎯 **BusBuddy Excellence Standards**

**Primary Goal**: Achieve production-quality software with clean builds (0 errors), excellent architecture, and comprehensive functionality including student entry and route assignment, following best practices and documentation standards.

**Excellence Focus**: See `vscode-userdata/BusBuddy.instructions.md` for detailed quality standards (students, routes, UI excellence). AI assistants must:

- Prioritize `bb-*` commands (`bb-build`, `bb-run`, `bb-quality-check`) over raw `dotnet` commands.
- Support disabling experimental services (e.g., XAI, GoogleEarthEngine) to maintain clean builds while preserving core quality.
- Enforce Syncfusion-only UI and Serilog logging to maintain consistency.
- Run `bb-anti-regression` and `bb-xaml-validate` before suggesting changes.

**Advanced Features** (implemented with proper architecture):

- XAI integration (e.g., `XAIService`, `OptimizedXAIService`) - when properly architected.
- Google Earth Engine integration (e.g., `GoogleEarthEngineService`) - with clean interfaces.
- Comprehensive features: vehicle management, driver scheduling, maintenance, fuel tracking, advanced reporting.

**CRITICAL: Use BusBuddy PowerShell Commands First**

- **Always use `bb-*` commands** instead of raw dotnet commands
- **Check available commands**: Use `bb-commands` to see all options
- **Health checks**: Use `bb-health` before troubleshooting
- **Quality validation**: Use `bb-quality-check` to ensure excellent student/route functionality
- **Anti-regression**: Use `bb-anti-regression` and `bb-xaml-validate` before commits

**Primary Development Commands:**

```powershell
bb-health         # System health check
bb-build          # Build solution
bb-run            # Run application
bb-test           # Run comprehensive tests
bb-quality-check  # Verify production excellence
bb-anti-regression # Prevent legacy patterns
bb-xaml-validate  # Ensure Syncfusion-only UI
bb-commands       # List all commands
```

**Clean Architecture Strategy:**
To maintain excellent code quality, properly isolate experimental services by renaming files to `.disabled` when they cause build issues. Example:

```powershell
cd BusBuddy.Core/Services
Rename-Item "AIEnhancedRouteService.cs" "AIEnhancedRouteService.cs.disabled"
bb-build  # Verify clean build
```

- Track `.disabled` files in Git with descriptive commits.
- Document in `GROK-README.md` and `ANTI-REGRESSION-CHECKLIST.md`.
- Run `bb-quality-check` to ensure student/route functionality remains excellent.

### **Greenfield Reset Strategy for Quality Development**

To achieve a clean build (0 errors), temporarily disable experimental services causing compilation errors (e.g., CS0246) by renaming files to `.disabled`. This preserves code for later re-enablement without risking regressions.

**Steps for Disabling Services:**

1. **Identify**: Locate files causing build errors (e.g., `BusBuddy.Core/Services/AIEnhancedRouteService.cs`).
2. **Disable**: Rename to `.disabled` (e.g., `AIEnhancedRouteService.cs.disabled`).
    ```powershell
    cd BusBuddy.Core/Services
    Rename-Item "AIEnhancedRouteService.cs" "AIEnhancedRouteService.cs.disabled"
    ```
3. **Verify**: Run `bb-build` to confirm errors are resolved.
4. **Track**: Commit `.disabled` files with descriptive message:
    ```bash
    git add .
    git commit -m "fix: disable AIEnhancedRouteService to achieve clean build"
    git push
    ```
5. **Document**: Update `GROK-README.md` and `ANTI-REGRESSION-CHECKLIST.md` with disabled components.

**Rules:**

- Only disable experimental services (e.g., XAI, GoogleEarthEngine).
- Never delete files—always use `.disabled` extension.
- Run `bb-quality-check` after disabling to ensure core functionality (students/routes) remains excellent.
- Plan re-enablement with proper architecture and `bb-anti-regression` checks.

**For BusBuddy-specific requirements, also reference:**

- **`vscode-userdata\BusBuddy.instructions.md`** - BusBuddy domain knowledge and excellence standards
- **Integration Note**: BusBuddy prioritizes clean architecture and proper development practices

## 🛠️ **Technology Stack & Versions**

### **Global Tools & SDK Versions**

- **PowerShell**: 7.5.2 (Required minimum version)
- **.NET SDK**: 9.0.303 (per `global.json`)
- **Target Framework**: .NET 8.0-windows (WPF projects)
- **MSBuild SDK**: Microsoft.Build.Traversal 3.4.0
- **Roll Forward Policy**: latestMinor (per `global.json`)

### **Package Versions (Directory.Build.props)**

- **Syncfusion WPF**: 30.1.42 (Essential Studio for WPF, per Directory.Build.props)
- **Entity Framework Core**: 9.0.7 (.NET 9 compatible)
- **Serilog**: 4.3.0 (Pure Serilog implementation)
- **Code Analysis**: Enabled with Recommended analysis mode
- **Practical Ruleset**: `BusBuddy-Practical.ruleset` for quality development

### **Database Configuration Standards**

- **Development**: LocalDB with SQL Server LocalDB instance
- **Production**: Azure SQL Database with secure connection strings
- **Connection Strings**: Environment variable substitution for credentials
- **Database Provider**: Configurable via `appsettings.json` DatabaseProvider setting

**Database Connection Examples:**

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=[Project];Integrated Security=True;MultipleActiveResultSets=True",
        "AzureConnection": "Server=tcp:[server].database.windows.net,1433;Initial Catalog=[database];Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    },
    "DatabaseProvider": "LocalDB"
}
```

### **External Service Integrations**

- **Google Earth Engine**: Project-based authentication with service account keys
- **Azure Services**: Environment-based credential management
- **Syncfusion Licensing**: Environment variable `${SYNCFUSION_LICENSE_KEY}`

### **Build Configuration Standards**

- **Nullable Reference Types**: Enabled throughout solution
- **Implicit Usings**: Enabled for common namespace imports
- **Documentation Generation**: XML documentation files for all public APIs
- **Code Analysis**: .NET analyzers with practical ruleset for quality development
- **Warning Treatment**: Warnings allowed during development phases, errors enforced in production

## ⚠️ **DOCUMENTATION-FIRST MANDATE - ZERO TOLERANCE**

**ABSOLUTE REQUIREMENT: NO CODE WITHOUT OFFICIAL DOCUMENTATION REFERENCE**

All development MUST follow official documentation standards:

- **Microsoft PowerShell**: Reference official docs for ALL PowerShell code
- **Syncfusion WPF**: Reference official docs for ALL UI components
- **Microsoft .NET**: Reference official docs for ALL C# development
- **Entity Framework**: Reference official docs for ALL data access

**Current Status**: Module compliance analysis required - Zero tolerance for code without proper documentation reference before any new development.

## 🚫 **CRITICAL: MANDATORY DOCUMENTATION COMPLIANCE**

**NO CODE WITHOUT PROPER DOCUMENTATION REFERENCE - ZERO TOLERANCE POLICY**

### **Documentation-First Development - ABSOLUTE REQUIREMENT**

- ❌ **FORBIDDEN**: Writing ANY code without referencing official documentation first
- ❌ **FORBIDDEN**: Implementing features based on assumptions or "common patterns"
- ❌ **FORBIDDEN**: Building PowerShell modules without Microsoft PowerShell standards compliance
- ❌ **FORBIDDEN**: Using Syncfusion controls without official Syncfusion documentation reference
- ❌ **FORBIDDEN**: Creating "quick fixes" that violate established standards and best practices

### **MANDATORY DOCUMENTATION SOURCES**

- **Microsoft PowerShell**: [Official PowerShell Documentation](https://docs.microsoft.com/en-us/powershell/) - Required for ALL PowerShell development
- **Microsoft .NET**: [Official .NET Documentation](https://docs.microsoft.com/en-us/dotnet/) - Required for ALL C# development
- **Syncfusion WPF**: [Official Syncfusion Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf) - Required for ALL UI components
- **Entity Framework**: [Official EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/) - Required for ALL data access
- **WPF Framework**: [Official WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - Required for ALL WPF development

### **CRITICAL LESSONS LEARNED FROM POWERSHELL ANALYSIS**

- **Module Analysis**: Large monolithic modules often fail Microsoft compliance standards
- **Write-Host Violations**: Anti-pattern Write-Host usage instead of proper output streams
- **Module Structure**: Architectural violations against Microsoft modularization guidelines
- **Error Handling**: Inconsistent patterns violating Microsoft exception handling standards
- **Export Violations**: Missing Export-ModuleMember declarations required by Microsoft standards

### **MANDATORY DEVELOPMENT PROCESS**

1. **FIRST**: Search and read official documentation for the specific technology/feature
2. **SECOND**: Find documented examples and implementation patterns in official docs
3. **THIRD**: Implement ONLY using documented, officially supported methods
4. **FOURTH**: Validate implementation against official standards and best practices
5. **NEVER**: Proceed without documentation reference or with "I think this works" approaches

### **ZERO TOLERANCE VIOLATIONS**

- **Write-Host in PowerShell**: Use Write-Output, Write-Information, Write-Verbose instead
- **Undocumented Syncfusion patterns**: Only use officially documented control implementations
- **Custom "enhanced" wrappers**: Use official APIs exactly as documented
- **Assumed parameter combinations**: Verify all parameters exist in official API documentation
- **Legacy or deprecated patterns**: Use current, officially supported implementations only

### **DOCUMENTATION VERIFICATION REQUIREMENTS**

- **Before ANY PowerShell code**: Reference Microsoft PowerShell standards documentation
- **Before ANY Syncfusion control**: Reference specific control documentation page
- **Before ANY .NET feature**: Reference official .NET API documentation
- **Include documentation links**: Always provide link to specific documentation page used
- **Code comments**: Include reference to documentation source in code comments

### **Mandatory Anti-Regression Checks**

Before suggesting any code changes or commits, run these checks to prevent regressions:

```powershell
bb-anti-regression  # Scans for Microsoft.Extensions.Logging, standard WPF controls, and Write-Host
bb-xaml-validate    # Ensures only Syncfusion controls in XAML
```

**Rules:**

- Never suggest code that introduces Microsoft.Extensions.Logging (use Serilog).
- Never suggest standard WPF controls (e.g., `<DataGrid>`)—use Syncfusion equivalents (`syncfusion:SfDataGrid`).
- Never suggest Write-Host in PowerShell—use Write-Output or Write-Information.
- If checks fail, fix violations first (e.g., replace logging, upgrade controls) and document in commit message.

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

- **[Project].Core**: Core business logic, models, services, and data access
- **[Project].WPF**: WPF presentation layer with Views, ViewModels, and UI-specific services
- **[Project].Tests**: Comprehensive test suite for all layers

### WPF Project Organization ([Project].WPF)

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

- **Domain Folders**: Group related files by business domain
- **Paired Files**: Keep View and ViewModel files in corresponding folders
- **Naming Convention**: Use consistent naming patterns (e.g., `[Entity]ManagementView.xaml` / `[Entity]ManagementViewModel.cs`)

### Core Project Organization ([Project].Core)

- **Configuration/**: App configuration and settings
- **Data/**: Entity Framework contexts and configurations
- **Extensions/**: Core extension methods
- **Interceptors/**: EF interceptors and data access enhancements
- **Migrations/**: Entity Framework migrations
- **Models/**: Domain models and entities
- **Services/**: Business logic services with interfaces
- **Utilities/**: Core utility classes and helpers

### File Naming Conventions

- **ViewModels**: Use descriptive names ending with `ViewModel` (e.g., `[Entity]ManagementViewModel.cs`)
- **Views**: Use descriptive names ending with `View` (e.g., `[Entity]ManagementView.xaml`)
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

- **App.xaml.cs Integration**: Debug helper classes provide debug functionality accessible via PowerShell
- **Command Line Arguments**: Application supports debug arguments (`--start-debug-filter`, `--export-debug-json`, etc.)
- **Real-time Filtering**: Use debug output filters for live debug output analysis and filtering
- **Actionable Error Detection**: Implement critical issue detection and priority-based error categorization
- **PowerShell Bridge**: All debug methods accessible via custom PowerShell commands

### Debug Helper Method Patterns

- **Static Methods**: All debug helper methods are static and accessible without instantiation
- **Conditional Compilation**: Use `[Conditional("DEBUG")]` for debug-only functionality
- **Structured Output**: Debug output uses structured formatting with priority indicators
- **Event Integration**: Subscribe to `HighPriorityIssueDetected` and `NewEntriesFiltered` events
- **JSON Export**: Support exporting debug data to JSON for external tool integration

### PowerShell Debug Command Patterns

```powershell
# Start debug filter
[prefix]-debug-start    # Calls DebugHelper.StartAutoFilter()

# Export debug data
[prefix]-debug-export   # Calls DebugHelper.ExportToJson()

# Health monitoring
[prefix]-health         # Calls DebugHelper.HealthCheck()

# Test functionality
[prefix]-debug-test     # Calls DebugHelper.TestAutoFilter()
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

### **Phase 1 Architecture (Excellence-Driven Approach)**

- ✅ **Basic MVVM**: Simple ViewModels with INotifyPropertyChanged, defer advanced patterns
- ✅ **Direct Data Access**: Simple Entity Framework queries, defer complex repositories
- ✅ **Basic Navigation**: Simple Frame.Navigate() calls, defer advanced navigation service
- ✅ **Essential Error Handling**: Try/catch on data operations, defer comprehensive patterns
- ⚠️ **Defer**: Complex dependency injection, advanced async patterns, comprehensive validation

### **Phase 1 Quick Patterns**

```csharp
// Quick ViewModel pattern for Phase 1
public class EntitiesViewModel : INotifyPropertyChanged
{
    public ObservableCollection<Entity> Entities { get; set; } = new();

    public async Task LoadEntitiesAsync()
    {
        try
        {
            using var context = new AppContext();
            var entities = await context.Entities.ToListAsync();
            Entities.Clear();
            foreach(var entity in entities) Entities.Add(entity);
        }
        catch (Exception ex)
        {
            // Basic error handling for Phase 1
            MessageBox.Show($"Error loading entities: {ex.Message}");
        }
    }
}

// Quick navigation pattern for Phase 1
private void NavigateToEntities() => ContentFrame.Navigate(new EntitiesView());
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
<DataGrid ItemsSource="{Binding Entities}" AutoGenerateColumns="True" />
<TextBox Text="{Binding SelectedEntity.Name, Mode=TwoWay}" />
```

## Database and Entity Framework Standards - Phase 1 Simplified

### **Phase 1 Database (Direct and Simple)**

- ✅ **Basic DbContext**: Simple context with DbSet properties
- ✅ **Direct Queries**: Basic LINQ queries, defer complex repositories
- ✅ **Simple Migrations**: Basic EF migrations, defer complex schema management
- ✅ **Configurable Connection**: Support LocalDB for dev, Azure SQL for production
- ⚠️ **Defer**: Advanced patterns, connection pooling, complex error handling

### **Database Configuration Patterns**

```csharp
// Multi-environment DbContext configuration
public class AppContext : DbContext
{
    private readonly IConfiguration _configuration;

    public AppContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public DbSet<Entity1> Entity1s { get; set; }
    public DbSet<Entity2> Entity2s { get; set; }
    public DbSet<Entity3> Entity3s { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var provider = _configuration["DatabaseProvider"];
        var connectionString = provider switch
        {
            "Azure" => _configuration.GetConnectionString("AzureConnection"),
            "LocalDB" => _configuration.GetConnectionString("DefaultConnection"),
            _ => _configuration.GetConnectionString("LocalConnection")
        };

        optionsBuilder.UseSqlServer(connectionString);
    }
}

// Simple query pattern for Phase 1
var entities = await context.Entity1s.ToListAsync();
```

### **Azure SQL Database Standards**

- **Connection String Format**: Use environment variables for sensitive data
- **Security**: Enable encryption and certificate validation
- **Connection Timeout**: Set appropriate timeout values (30 seconds)
- **Environment Variables**: `${AZURE_SQL_USER}` and `${AZURE_SQL_PASSWORD}`
- **Multi-Active Result Sets**: Enable for complex operations
- **Connection Pooling**: Leverage EF Core default pooling for production

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

- **Version 30.1.42**: This project uses Syncfusion Essential Studio for WPF version 30.1.42
- **Official Documentation**: [Syncfusion WPF Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf)
- **Theme Documentation**: [FluentDark Theme Guide](https://help.syncfusion.com/wpf/themes/fluent-dark-theme)
- **Control References**: [WPF Control Gallery](https://help.syncfusion.com/wpf/control-gallery)
- **Migration Guides**: [Version Migration Documentation](https://help.syncfusion.com/wpf/upgrade-guide)
- **Target Framework**: WPF projects targeting .NET 8.0-windows (per Directory.Build.props)

### Quality-Focused Core Controls for Phase 1

- **SfDataGrid**: [DataGrid Documentation](https://help.syncfusion.com/wpf/datagrid/getting-started) - Used for all tabular data display
- **DockingManager**: [DockingManager Documentation](https://help.syncfusion.com/wpf/docking/getting-started) - Used for main UI layout
- **NavigationDrawer**: [NavigationDrawer Documentation](https://help.syncfusion.com/wpf/navigation-drawer/getting-started) - Used for side navigation
- **SfChart**: [Chart Documentation](https://help.syncfusion.com/wpf/charts/getting-started) - Used for dashboard metrics

### Implementation Standards

- **Theme Consistency**: Use FluentDark/FluentLight themes consistently across all Syncfusion controls
- **Assembly Management**: Reference Syncfusion.SfGrid.WPF 30.1.42 and theme assemblies
- **Control Standards**: Follow established patterns for DockingManager, NavigationDrawer, and other controls
- **Resource Organization**: Maintain organized resource dictionaries for themes and styles
- **License Management**: Use environment variable `${SYNCFUSION_LICENSE_KEY}` for licensing
- **Performance Optimization**: Use appropriate control settings for optimal performance

### **🚫 CRITICAL: NO SYNCFUSION REGRESSION POLICY**

**ABSOLUTE PROHIBITION: Never Replace Syncfusion Components with Standard WPF Controls**

- ❌ **NEVER replace SfDataGrid with DataGrid** - Fix namespace/reference issues instead
- ❌ **NEVER replace Syncfusion ComboBox with standard ComboBox** - Resolve compilation errors properly
- ❌ **NEVER downgrade working Syncfusion components** - Hard-earned progress must be preserved
- ❌ **NO SHORTCUTS** - Compilation errors must be fixed through proper namespace resolution, not component replacement
- ❌ **NO REGRESSION JUSTIFICATION** - "Fixing errors" is never a valid reason to replace Syncfusion components
- ❌ **UPGRADE, DON'T DOWNGRADE** - Standard DataGrid found in legacy code should be upgraded to SfDataGrid

**MANDATORY ERROR RESOLUTION APPROACH:**

1. **First**: Check namespace declarations and assembly references
2. **Second**: Verify Syncfusion package versions and licensing
3. **Third**: Consult Syncfusion documentation for proper usage patterns
4. **Fourth**: Add missing using statements or update project references
5. **NEVER**: Replace Syncfusion components with standard WPF controls

**SPECIFIC DATAGRIDS POLICY:**

- **Standard DataGrid found**: Replace with SfDataGrid using official Syncfusion patterns
- **Unknown DataGrid elements**: Fix by adding proper Syncfusion namespace declarations
- **DataGrid compilation errors**: Resolve by ensuring Syncfusion.SfGrid.WPF package is properly referenced
- **Never downgrade**: SfDataGrid is always preferred over standard DataGrid for consistency

**HARD-EARNED PROGRESS PROTECTION:**

- **Syncfusion WPF 30.1.42** components represent significant implementation effort
- **Working Syncfusion implementations** must be preserved at all costs
- **Professional UI standards** require maintaining Syncfusion component consistency
- **Technical debt** is created by mixing Syncfusion and standard WPF controls

**ERROR RESOLUTION WITHOUT REGRESSION:**

- **Missing namespaces**: Add `xmlns:syncfusion="http://schemas.syncfusion.com/wpf"`
- **Assembly references**: Verify Syncfusion package installation and versions
- **Licensing issues**: Check license key registration in App.xaml.cs
- **API changes**: Consult migration guides for version-specific updates

**EXAMPLES OF PROPER ERROR RESOLUTION (NO REGRESSION):**

```xml
<!-- ❌ WRONG: Replacing SfDataGrid with DataGrid due to compilation errors -->
<DataGrid ItemsSource="{Binding Students}" />

<!-- ✅ CORRECT: Fix namespace and keep SfDataGrid -->
<syncfusion:SfDataGrid ItemsSource="{Binding Students}"
                       AutoGenerateColumns="False"
                       AllowSorting="True" />
```

### **📚 COMPREHENSIVE SYNCFUSION WPF 30.1.42 ERROR RESOLUTION EXAMPLES**

**Reference Documentation**: https://help.syncfusion.com/wpf/datagrid/getting-started

#### **Error Type 1: "Unknown element type 'syncfusion:SfDataGrid'"**

**❌ WRONG APPROACH - Regression to DataGrid:**

```xml
<!-- DON'T DO THIS - This is regression! -->
<DataGrid ItemsSource="{Binding Vehicles}" AutoGenerateColumns="False">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Bus Number" Binding="{Binding BusNumber}"/>
    </DataGrid.Columns>
</DataGrid>
```

**✅ CORRECT APPROACH - Fix namespace declaration:**

```xml
<!-- Step 1: Add Syncfusion namespace (if missing) -->
<UserControl xmlns:syncfusion="http://schemas.syncfusion.com/wpf">

<!-- Step 2: Use official SfDataGrid pattern from Syncfusion docs -->
<syncfusion:SfDataGrid ItemsSource="{Binding Vehicles}"
                       AutoGenerateColumns="False"
                       AllowEditing="False"
                       AllowSorting="True"
                       AllowFiltering="True"
                       SelectionMode="Single">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn HeaderText="Bus Number"
                                 MappingName="BusNumber"
                                 Width="100"/>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

**Documentation Reference**: https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Grid.SfDataGrid.html

#### **Error Type 2: "StaticResource 'BooleanToVisibilityConverter' not found"**

**❌ WRONG APPROACH - Remove binding:**

```xml
<!-- DON'T DO THIS - Loses functionality! -->
<Border Visibility="Visible">
```

**✅ CORRECT APPROACH - Add required converter:**

```xml
<!-- Step 1: Add converter to UserControl.Resources -->
<UserControl.Resources>
    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
</UserControl.Resources>

<!-- Step 2: Use converter as documented -->
<Border Visibility="{Binding IsMapLoading, Converter={StaticResource BooleanToVisibilityConverter}}">
```

#### **Error Type 3: "Unknown x:Class type" Compilation Error**

**❌ WRONG APPROACH - Remove code-behind functionality:**

```xml
<!-- DON'T DO THIS - Breaks MVVM pattern! -->
<UserControl>
```

**✅ CORRECT APPROACH - Verify namespace and class:**

```csharp
// Step 1: Ensure code-behind file exists with correct namespace
namespace BusBuddy.WPF.Views.GoogleEarth
{
    public partial class GoogleEarthView : UserControl
    {
        public GoogleEarthView()
        {
            InitializeComponent();
        }
    }
}
```

```xml
<!-- Step 2: Match XAML x:Class to code-behind -->
<UserControl x:Class="BusBuddy.WPF.Views.GoogleEarth.GoogleEarthView">
```

#### **Error Type 4: Missing Event Handler**

**❌ WRONG APPROACH - Remove event binding:**

```xml
<!-- DON'T DO THIS - Loses interactive functionality! -->
<ComboBox x:Name="MapLayerComboBox">
```

**✅ CORRECT APPROACH - Implement event handler:**

```csharp
// Step 1: Add event handler to code-behind
private void MapLayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    // Implementation following Microsoft WPF patterns
    if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
    {
        // Handle selection change
    }
}
```

```xml
<!-- Step 2: Keep event binding intact -->
<ComboBox x:Name="MapLayerComboBox"
          SelectionChanged="MapLayerComboBox_SelectionChanged">
```

#### **Error Type 5: Syncfusion Package Reference Issues**

**❌ WRONG APPROACH - Remove Syncfusion controls:**

```xml
<!-- DON'T DO THIS - Massive regression! -->
<StackPanel>
    <TextBlock Text="No data grid available"/>
</StackPanel>
```

**✅ CORRECT APPROACH - Fix package references:**

**Step 1: Verify package installation**

```xml
<!-- In .csproj file -->
<PackageReference Include="Syncfusion.SfGrid.WPF" Version="30.1.42" />
<PackageReference Include="Syncfusion.Themes.FluentDark.WPF" Version="30.1.42" />
```

**Step 2: Register license (App.xaml.cs)**

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    // Register Syncfusion license BEFORE InitializeComponent
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("LICENSE_KEY");

    base.OnStartup(e);
}
```

**Step 3: Use documented SfDataGrid pattern**

```xml
<syncfusion:SfDataGrid ItemsSource="{Binding Data}"
                       AutoGenerateColumns="False"
                       AllowSorting="True">
    <!-- Column definitions as per Syncfusion docs -->
</syncfusion:SfDataGrid>
```

#### **Error Type 6: DataGrid to SfDataGrid Conversion (Upgrading Legacy Code)**

**❌ WRONG APPROACH - Keep standard DataGrid:**

```xml
<!-- DON'T DO THIS - Inconsistent with Syncfusion standards! -->
<DataGrid ItemsSource="{Binding Vehicles}">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Name" Binding="{Binding Name}"/>
    </DataGrid.Columns>
</DataGrid>
```

**✅ CORRECT APPROACH - Upgrade to SfDataGrid using official patterns:**

**From Syncfusion Documentation: https://help.syncfusion.com/wpf/datagrid/columns**

```xml
<!-- Step 1: Add Syncfusion namespace -->
xmlns:syncfusion="http://schemas.syncfusion.com/wpf"

<!-- Step 2: Convert using official column mapping patterns -->
<syncfusion:SfDataGrid ItemsSource="{Binding Vehicles}"
                       AutoGenerateColumns="False"
                       AllowEditing="False"
                       AllowSorting="True"
                       AllowFiltering="True"
                       ShowRowHeader="True"
                       SelectionMode="Single"
                       GridLinesVisibility="Both"
                       HeaderLinesVisibility="All"
                       ColumnSizer="Star">

    <syncfusion:SfDataGrid.Columns>
        <!-- Convert DataGridTextColumn to GridTextColumn -->
        <syncfusion:GridTextColumn HeaderText="Vehicle Name"
                                 MappingName="Name"
                                 Width="150"/>
        <syncfusion:GridTextColumn HeaderText="Status"
                                 MappingName="Status"
                                 Width="100"/>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

### **MANDATORY VERIFICATION STEPS FOR ALL FIXES:**

1. **Documentation Check**: Every Syncfusion property/method MUST exist in official docs
2. **Example Validation**: Copy patterns exactly from Syncfusion sample browser
3. **API Verification**: Use https://help.syncfusion.com/cr/wpf/Syncfusion.html for API reference
4. **No Custom Code**: Never invent Syncfusion patterns not shown in documentation
5. **Preserve Functionality**: All fixes must maintain or enhance existing functionality

**EXISTING HARD-EARNED SYNCFUSION IMPLEMENTATIONS TO PROTECT:**

- **StudentsView.xaml**: Working SfDataGrid with proper column configuration
- **FuelReconciliationDialog.xaml**: Working SfDataGrid with custom styling
- **VehicleManagementView.xaml**: RECENTLY UPGRADED to SfDataGrid - maintain this progress!
- **Multiple Views**: 20+ SfDataGrid instances already successfully implemented
- **Resource Dictionaries**: Validated Syncfusion V30 styling and themes

### **🛡️ BUSBUDDY PROJECT SPECIFIC SYNCFUSION PROTECTION**

**CRITICAL PROJECT STATUS - SYNCFUSION WPF 30.1.42 IMPLEMENTATIONS:**

✅ **SUCCESSFULLY IMPLEMENTED (PRESERVE AT ALL COSTS):**

```xml
<!-- StudentsView.xaml - Perfect SfDataGrid implementation -->
<syncfusion:SfDataGrid Grid.Row="2"
                       Name="StudentsDataGrid"
                       ItemsSource="{Binding Students}"
                       SelectedItem="{Binding SelectedStudent, Mode=TwoWay}"
                       AutoGenerateColumns="False"
                       AllowEditing="False"
                       AllowSorting="True"
                       AllowFiltering="True"
                       SelectionMode="Single">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn HeaderText="Student ID" MappingName="StudentNumber"/>
        <syncfusion:GridTextColumn HeaderText="Name" MappingName="StudentName"/>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

✅ **RECENTLY UPGRADED (MAJOR PROGRESS):**

```xml
<!-- VehicleManagementView.xaml - Successfully converted from DataGrid -->
<syncfusion:SfDataGrid Grid.Column="0"
                       x:Name="VehicleDataGrid"
                       ItemsSource="{Binding FilteredVehicles}"
                       SelectedItem="{Binding SelectedVehicle}"
                       AutoGenerateColumns="False"
                       AllowEditing="False"
                       AllowSorting="True"
                       AllowFiltering="True">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn HeaderText="Bus Number" MappingName="BusNumber"/>
        <syncfusion:GridTextColumn HeaderText="Make" MappingName="Make"/>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

⚠️ **REQUIRES SIMILAR UPGRADE (NO REGRESSION ALLOWED):**

- Any remaining DataGrid instances should be upgraded to SfDataGrid using above patterns
- GoogleEarthView.xaml DataGrid → Convert to SfDataGrid following VehicleManagementView pattern
- VehiclesView.xaml → Add Syncfusion namespace and implement SfDataGrid
- VehicleForm.xaml → Any data display should use SfDataGrid patterns

**BusBuddy-Specific Syncfusion Patterns to Follow:**

1. **Always include**: `AllowSorting="True"`, `AllowFiltering="True"`, `SelectionMode="Single"`
2. **Use MappingName**: Instead of Binding, use MappingName for GridTextColumn
3. **Namespace Standard**: `xmlns:syncfusion="http://schemas.syncfusion.com/wpf"`
4. **Width Management**: Use explicit widths or ColumnSizer="Star" for responsive design

### **🏗️ Syncfusion Implementation Requirements**

**CRITICAL RULE: Only Use Official Syncfusion Documentation**

- **Reference ONLY**: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- **WPF API Reference**: https://help.syncfusion.com/cr/wpf/Syncfusion.html
- **No custom fixes**: Use only documented Syncfusion APIs, methods, and examples
- **No invented code**: Every Syncfusion implementation must be found in official docs
- **Verify before implementing**: Search documentation first, implement only documented patterns
- **API Reference**: Use Syncfusion's complete WPF API reference for all controls and methods

**Documentation-First Development Process:**

1. **Search Syncfusion WPF docs** for the specific control/feature needed
2. **Find official examples** in the documentation or sample browser
3. **Copy exact patterns** from Syncfusion's documented examples
4. **Test with documented parameters** and properties only
5. **No modifications** to documented patterns without verifying in docs

**Common Syncfusion WPF Controls - Documentation Required:**

- **SfDataGrid**: Follow documented binding and column configuration patterns
- **DockingManager**: Use DockingStyle enum for docking operations
- **SfChart**: Use only documented series types and properties
- **NavigationDrawer**: Follow documented navigation patterns
- **SfButton**: Apply only documented style properties and themes

**Forbidden Practices:**

- ❌ **NO custom Syncfusion extensions** or helper methods
- ❌ **NO invented property combinations** not shown in docs
- ❌ **NO assumed API patterns** based on other frameworks
- ❌ **NO "enhanced" wrappers** around Syncfusion controls
- ❌ **NO undocumented parameters** or method calls
- ❌ **ABSOLUTELY NO REGRESSION** from Syncfusion controls to standard WPF controls
- ❌ **NO REPLACEMENT** of working Syncfusion components with DataGrid, ComboBox, or other standard controls
- ❌ **NO SHORTCUTS** - Fix compilation errors by proper namespace/reference resolution, not by component downgrade

### **📖 SYNCFUSION WPF 30.1.42 OFFICIAL DOCUMENTATION PATTERNS**

**CRITICAL: Use ONLY these documented patterns from https://help.syncfusion.com/wpf/datagrid/getting-started**

#### **SfDataGrid Official Basic Pattern:**

```xml
<!-- From official Syncfusion documentation -->
<syncfusion:SfDataGrid x:Name="dataGrid"
                       ItemsSource="{Binding OrderInfoCollection}"
                       AutoGenerateColumns="False">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn MappingName="OrderID" HeaderText="Order ID"/>
        <syncfusion:GridTextColumn MappingName="CustomerID" HeaderText="Customer ID"/>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

#### **SfDataGrid Column Configuration (Official Pattern):**

```xml
<!-- Column types from Syncfusion docs -->
<syncfusion:SfDataGrid.Columns>
    <syncfusion:GridTextColumn MappingName="CustomerName" HeaderText="Customer Name" Width="120"/>
    <syncfusion:GridNumericColumn MappingName="UnitPrice" HeaderText="Unit Price" Width="100"/>
    <syncfusion:GridDateTimeColumn MappingName="OrderDate" HeaderText="Order Date" Width="130"/>
    <syncfusion:GridCheckBoxColumn MappingName="IsClosed" HeaderText="Is Closed" Width="100"/>
</syncfusion:SfDataGrid.Columns>
```

#### **SfDataGrid Selection and Interaction (Official Pattern):**

```xml
<!-- Selection patterns from Syncfusion documentation -->
<syncfusion:SfDataGrid SelectionMode="Single"
                       AllowEditing="True"
                       AllowSorting="True"
                       AllowFiltering="True"
                       ShowRowHeader="True"
                       ColumnSizer="Star">
```

#### **SfDataGrid Styling (Official Pattern):**

```xml
<!-- Official styling approach -->
<syncfusion:SfDataGrid GridLinesVisibility="Both"
                       HeaderLinesVisibility="All"
                       RowHeight="35"
                       HeaderRowHeight="40">
```

**Reference Links for Each Pattern:**

- **Basic Setup**: https://help.syncfusion.com/wpf/datagrid/getting-started
- **Column Types**: https://help.syncfusion.com/wpf/datagrid/columns
- **Selection**: https://help.syncfusion.com/wpf/datagrid/selection
- **Styling**: https://help.syncfusion.com/wpf/datagrid/styles-and-templates
- **API Reference**: https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Grid.SfDataGrid.html

**MANDATORY RULE**: Every SfDataGrid implementation MUST follow one of these documented patterns exactly.

### **🚨 FINAL ENFORCEMENT: ZERO TOLERANCE ANTI-REGRESSION POLICY**

**IMMEDIATE STOP ACTIONS when encountering compilation errors:**

1. ❌ **NEVER** type `<DataGrid` as a solution to SfDataGrid errors
2. ❌ **NEVER** remove `syncfusion:` namespace prefix to "fix" errors
3. ❌ **NEVER** replace working Syncfusion components with standard WPF alternatives
4. ❌ **NEVER** justify regression with "it's faster" or "simpler" reasoning

**MANDATORY FIRST STEPS for any XAML compilation error:**

1. ✅ **CHECK**: Is the Syncfusion namespace declared? `xmlns:syncfusion="http://schemas.syncfusion.com/wpf"`
2. ✅ **VERIFY**: Are Syncfusion packages referenced in the project?
3. ✅ **CONSULT**: Official Syncfusion documentation for the exact control usage
4. ✅ **UPGRADE**: Convert any legacy DataGrid to SfDataGrid using documented patterns

**ZERO TOLERANCE VIOLATIONS:**

- **Write-Host in PowerShell**: Use Write-Output, Write-Information, Write-Verbose instead
- **Undocumented Syncfusion patterns**: Only use officially documented control implementations
- **Custom "enhanced" wrappers**: Use official APIs exactly as documented
- **Assumed parameter combinations**: Verify all parameters exist in official API documentation
- **Legacy or deprecated patterns**: Use current, officially supported implementations only
- **DataGrid regression**: Any DataGrid found should be immediately upgraded to SfDataGrid

**DOCUMENTATION VERIFICATION REQUIREMENTS:**

- **Before ANY PowerShell code**: Reference Microsoft PowerShell standards documentation
- **Before ANY Syncfusion control**: Reference specific control documentation page
- **Before ANY .NET feature**: Reference official .NET API documentation
- **Include documentation links**: Always provide link to specific documentation page used
- **Code comments**: Include reference to documentation source in code comments

**SUCCESS METRICS:**

- ✅ **100% Syncfusion consistency** across all data grids
- ✅ **Zero DataGrid instances** in new development
- ✅ **Documentation links** provided for all Syncfusion implementations
- ✅ **Working SfDataGrid patterns** preserved and replicated

**Required Verification Steps:**

1. **Before any Syncfusion WPF code**: Search help.syncfusion.com/wpf for exact usage
2. **Cross-reference examples**: Find matching code in Syncfusion's WPF sample browser
3. **API validation**: Verify all properties/methods exist in official WPF API reference
4. **Documentation links**: Include reference to specific Syncfusion WPF doc page used

**Documentation Resources (USE THESE ONLY):**

- **Main WPF API Reference**: https://help.syncfusion.com/cr/wpf/Syncfusion.html
- **Getting Started Guides**: Component-specific WPF documentation
- **Sample Browser**: WPF code examples and demonstrations
- **Knowledge Base**: Official solutions to common WPF issues

**Example of Correct Documentation-Based Implementation:**

```csharp
// Based on official Syncfusion RibbonControlAdv documentation
var tabItem = new TabHost
{
    Text = "Dashboard"
};
_ribbonControl.Header.AddMainItem(tabItem); // Documented method

// Based on official DockingManager documentation
_dockingManager.DockControl(panel, this, DockingStyle.Left, 280); // Documented enum
```

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
      "& 'C:\\path\\to\\Project-PowerShell-Profile.ps1';
       & 'C:\\path\\to\\Project-Advanced-Workflows.ps1'"]
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

## PowerShell Development Environment Integration - MICROSOFT STANDARDS MANDATORY

- **PowerShell Core 7.5.2**: Use PowerShell Core for all development scripting and task automation
- **MICROSOFT COMPLIANCE REQUIRED**: ALL PowerShell code MUST follow Microsoft PowerShell Development Guidelines
- **VS Code Integration**: Use robust `code` command integration with automatic VS Code/VS Code Insiders detection
- **Task Explorer Exclusive**: Task Explorer is the ONLY method for task management - no direct terminal commands for builds
- **Debug Helper Integration**: All `DebugHelper` methods from `App.xaml.cs` accessible via PowerShell commands

### **CRITICAL: Microsoft PowerShell Standards Compliance**

- **REFERENCE REQUIRED**: [Microsoft PowerShell Cmdlet Development Guidelines](https://docs.microsoft.com/en-us/powershell/scripting/developer/cmdlet/cmdlet-development-guidelines)
- **Module Standards**: [Microsoft PowerShell Module Guidelines](https://docs.microsoft.com/en-us/powershell/scripting/developer/module/writing-a-windows-powershell-module)
- **Error Handling**: [Microsoft PowerShell Error Handling](https://docs.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-exceptions)
- **Output Streams**: [Microsoft PowerShell Output Streams](https://docs.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-output-streams)

### **FORBIDDEN POWERSHELL ANTI-PATTERNS**

- ❌ **Write-Host**: Use Write-Output, Write-Information, Write-Verbose, Write-Debug instead
- ❌ **Monolithic modules**: Break large modules into focused, single-responsibility modules
- ❌ **Missing Export-ModuleMember**: Always explicitly declare exported functions
- ❌ **Inconsistent error handling**: Use consistent try-catch-throw patterns throughout
- ❌ **Hard-coded paths**: Use proper parameter binding and validation attributes
- ❌ **Direct console output**: Use proper PowerShell output streams and formatting

### **MANDATORY POWERSHELL PATTERNS**

- ✅ **Write-Output**: For pipeline-compatible object output
- ✅ **Write-Information**: For informational messages with -InformationAction support
- ✅ **Write-Verbose**: For detailed operation information with -Verbose support
- ✅ **Write-Debug**: For debugging information with -Debug support
- ✅ **Write-Warning**: For non-terminating warnings
- ✅ **Write-Error**: For terminating and non-terminating errors
- ✅ **Proper parameter attributes**: [Parameter(Mandatory=$true)] for required parameters
- ✅ **Module manifests**: .psd1 files with proper metadata and export declarations

### **BusBuddy Module Violations - IMMEDIATE REMEDIATION REQUIRED**

- **BusBuddy.psm1**: 7,866-line monolithic violation of Microsoft modularization standards
- **Compliance Score**: 45% FAILING - Required (40%), Strongly Encouraged (35%), Advisory (60%)
- **Write-Host Count**: 50+ violations of Microsoft output stream standards
- **Architecture**: Violates single-responsibility principle and Microsoft module design guidelines
- **Export Declarations**: Missing required Export-ModuleMember statements throughout
- **Error Handling**: Inconsistent patterns violating Microsoft exception handling standards

**Quality Development Guidance**:

- Avoid adding new functions to `BusBuddy.psm1` during Phase 1—use existing `bb-*` commands.
- If new PowerShell code is needed, create temporary scripts in `PowerShell/Validation/` and validate with `Invoke-ScriptAnalyzer`.
- Post-Phase 1: Split `BusBuddy.psm1` into smaller modules (e.g., `Build.psm1`, `Quality.psm1`) per Microsoft guidelines.
- Replace all `Write-Host` with `Write-Output` or `Write-Information` in new code.
- Document remediation plan in `GROK-README.md` under "Post-Phase 1 Tasks":
    ```markdown
    - Refactor BusBuddy.psm1 into single-responsibility modules.
    - Eliminate 50+ Write-Host violations with proper output streams.
    - Add Export-ModuleMember for all public functions.
    ```

### **MANDATORY REMEDIATION ACTIONS**

1. **BEFORE ANY NEW POWERSHELL CODE**: Fix existing violations in BusBuddy.psm1
2. **Write-Host Replacement**: Replace ALL Write-Host with appropriate output streams
3. **Module Breakup**: Split monolithic module into focused, single-purpose modules
4. **Export Declarations**: Add proper Export-ModuleMember statements for all public functions
5. **Error Standardization**: Implement consistent Microsoft-compliant error handling patterns
6. **Documentation Links**: Add Microsoft documentation references to all functions

### **NO NEW POWERSHELL WITHOUT COMPLIANCE**

- **Zero Tolerance**: No new PowerShell development until existing violations are fixed
- **Documentation First**: Every PowerShell change must reference Microsoft standards
- **Compliance Validation**: Use Microsoft guidelines to validate all PowerShell implementations
- **Professional Standards**: BusBuddy must meet enterprise PowerShell development standards

### PowerShell Profile Standards

- **Profile Location**: `Project-PowerShell-Profile.ps1` in project root for core functionality
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

### PowerShell 7.5.2 Technical Reference Documentation

- **Reference File**: `Documentation/PowerShell-7.5.2-Reference.md` - Comprehensive feature reference extracted from official PDF documentation
- **Source Material**: `PowerShell/powershell-scripting-powershell-7.5.pdf` (excluded from git via .gitignore)
- **Conversion Tool**: `Tools/Scripts/PDF-to-Markdown-Converter.ps1` - Automated conversion utilities based on OpenAI Community best practices
- **Conversion Methods**: Supports Ghostscript+Tesseract OCR and GPT-4o Vision API approaches
- **Conversion Purpose**: Makes PDF documentation accessible for AI assistance and code development
- **Key Sections**:
    - Threading and Parallel Processing enhancements
    - Error Handling improvements and structured error information
    - Performance optimizations and memory management
    - New cmdlets and parameter enhancements
    - Cross-platform compatibility features
    - BusBuddy-specific implementation examples
- **Usage Pattern**: Reference this documentation when implementing PowerShell 7.5.2 features in Phase 2 development
- **Maintenance**: Update reference file when new PowerShell features are implemented in BusBuddy modules
- **Update Command**: Run `Update-PowerShellReference` from the conversion script to regenerate from PDF

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

- **Direct Commands**: Use native .NET CLI commands for reliability
- **PowerShell Automation**: Leverage bb-\* commands for enhanced workflows
- **Zero Dependencies**: No external API dependencies for core functionality
- **Simple and Fast**: Optimized for speed and reliability over complexity

## Quality Development Command Integration

### Quick Build and Run Commands

```batch
@REM Quick build
dotnet build [Project].WPF/[Project].WPF.csproj

@REM Quick run
dotnet run --project [Project].WPF/[Project].WPF.csproj

@REM Clean build
dotnet clean [Project].sln && dotnet restore [Project].sln && dotnet build [Project].sln
```

### Error Capture Commands

```batch
@REM Capture runtime errors
check-health.bat

@REM Run with error capture
run-with-error-capture.bat
```

### Common Build Fixes

```batch
@REM Fix package cache issues
dotnet nuget locals all --clear

@REM Fix package restore issues
dotnet restore [Project].sln --force --no-cache

@REM Validate build health
dotnet build [Project].sln --verbosity minimal
```

## Troubleshooting Guide

### Common Build and Development Issues

#### NuGet Package Restore Failures

- **Solution**: Clear NuGet cache and restore packages
    ```batch
    dotnet nuget locals all --clear
    dotnet restore --force --no-cache
    ```

#### Entity Framework Migration Issues

- **Solution**: Validate database connection and reset migrations if needed
    ```batch
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

#### Build Error Analysis

- **Solution**: Use check-health.bat to analyze build errors
    ```batch
    check-health.bat
    ```

#### CS0246 Errors (Type or Namespace Not Found)

- **Symptoms**: Errors like "The type or namespace name 'XAIService' could not be found" in build output.
- **Causes**: Missing class definitions (e.g., disabled files), incorrect namespaces, or missing package references.
- **Quality Development Resolution** (Greenfield Reset):

1. **Check Disabled Files**: If the missing type (e.g., `XAIService`) is in a `.disabled` file, confirm it's non-core and keep disabled.
    ```powershell
    Get-ChildItem -Recurse -Filter "*.disabled" | Select-Object Name
    ```
2. **Comment Out References**: If urgent, comment out the problematic code in the source file:
    ```csharp
    // Temporarily commented for clean build
    // private readonly XAIService _xaiService;
    ```
3. **Verify Build**: Run `bb-build` to confirm resolution.
4. **Avoid Adding Dependencies**: Do not add new packages or re-enable complex services during early phases.
5. **Document**: Note in commit message and GROK-README.md:
    ```bash
    git commit -m "fix: comment out XAIService references for clean build"
    ```

**Post-Development**: Re-enable disabled files one-by-one, fixing references with proper packages or namespaces per official documentation.

### Performance Troubleshooting

#### High Memory Usage

- **Monitor**: Use runtime monitoring with error capture
- **Solution**: Implement proper disposal patterns and weak references
- **Tools**: Use dotMemory or PerfView for detailed analysis

#### Slow UI Response

- **Monitor**: Check UI thread blocking and async operation completion
- **Solution**: Move long-running operations to background threads
- **Validation**: Use async/await patterns consistently in UI operations

#### Database Connection Issues

- **Monitor**: Enable EF Core logging and connection resilience metrics
- **Solution**: Use proper error handling for database operations
- **Recovery**: Implement retry policies for transient failures

### Development Environment Issues

#### Build Task Failures

- **Solution**: Use direct .NET CLI commands for reliability
- **Validation**: Check task configuration in `.vscode/tasks.json`
- **Debugging**: Use check-health.bat to validate project status

#### Environment Setup Issues

- **Solution**: Verify .NET SDK installation and versions
    ```batch
    dotnet --info
    dotnet --version
    ```

#### Extension Compatibility Problems

- **Required Extensions**: XAML Styler, Task Explorer
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

### Error Capture with Batch Files

#### Using run-with-error-capture.bat

- **Purpose**: Captures application errors during runtime
- **Usage**: Run directly from command prompt or VS Code task
- **Output**: Saves errors to app_errors.log for analysis
- **Benefits**: Provides clean error output without console clutter

#### Using check-health.bat

- **Purpose**: Performs comprehensive build health check
- **Usage**: Run after build failures to analyze issues
- **Output**: Displays formatted report of build errors
- **Benefits**: Identifies common build issues and suggested fixes

These instructions define **HOW** to build quality software following Microsoft standards and best practices. They are technology and methodology focused, not business domain specific.

**Project-specific requirements (WHAT to build) should be documented separately in:**

- Requirements documents
- User stories
- Business logic specifications
- Domain model definitions
- Feature specifications

**These instructions cover the technical HOW:**

- Documentation-first development methodology
- Microsoft compliance standards
- Code quality patterns
- Build and deployment processes
- Error handling approaches
- Testing strategies

---

## PowerShell 7.5.2 Advanced Features and Standards

### **PowerShell 7.5.2 Core Requirements**

- **Version**: PowerShell Core 7.5.2 minimum required
- **Profile Standard**: Microsoft.PowerShell_profile.ps1 in PowerShell/Profiles/
- **Module Standards**: Microsoft PowerShell Module Guidelines compliance
- **Reference Documentation**: Documentation/PowerShell-7.5.2-Reference.md

### **Threading and Parallel Processing**

```powershell
# ForEach-Object -Parallel Best Practices
$results = $items | ForEach-Object -Parallel {
    param($item)
    try {
        # Process item with proper error handling
        return [PSCustomObject]@{
            Item = $item
            Result = "Success"
            Output = $processedData
        }
    } catch {
        return [PSCustomObject]@{
            Item = $item
            Result = "Failed"
            Error = $_.Exception.Message
        }
    }
} -ThrottleLimit 4

# Start-ThreadJob for Background Tasks
$job = Start-ThreadJob -ScriptBlock {
    param($inputData)
    # Long-running background task
    return $result
} -ArgumentList $data
```

### **Enhanced Error Handling Patterns**

```powershell
# Structured Error Information
try {
    # Operation
    $result = Invoke-Operation
    Write-Output $result
} catch {
    $errorInfo = [PSCustomObject]@{
        Command = $MyInvocation.MyCommand.Name
        Error = $_.Exception.Message
        Line = $_.InvocationInfo.ScriptLineNumber
        Timestamp = Get-Date
    }
    Write-Error -ErrorRecord $_ -CategoryActivity "Operation"
    Write-Information $errorInfo -InformationAction Continue
}

# Pipeline Chain Operators (7.5.2)
dotnet build && dotnet test || Write-Error "Build or test failed"
```

### **Advanced String and Data Processing**

```powershell
# Null Conditional Operators
$config = $settings?.Environment?.Database?.ConnectionString

# Ternary Operators
$mode = $isProduction ? "Release" : "Debug"

# Enhanced JSON Processing
$data = Get-Content config.json | ConvertFrom-Json -Depth 10
$output = $data | ConvertTo-Json -Depth 10 -Compress
```

### **Module Development Standards**

```powershell
# Proper Module Structure
#requires -Version 7.5

[CmdletBinding()]
param()

# Export only public functions
Export-ModuleMember -Function Get-ProjectInfo, Set-ProjectConfig

# Use proper parameter validation
function Get-ProjectInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$ProjectPath
    )

    begin {
        Write-Verbose "Starting project analysis"
    }

    process {
        # Implementation with proper output streams
        Write-Information "Processing $ProjectPath" -InformationAction Continue
    }

    end {
        Write-Verbose "Project analysis complete"
    }
}
```

### **Performance Optimization Patterns**

```powershell
# Memory Management
[System.GC]::Collect() # Use sparingly

# Stream Processing for Large Data
Get-Content large-file.txt | ForEach-Object {
    # Process line by line to avoid loading entire file
    if ($_ -match $pattern) {
        Write-Output $_
    }
}

# Efficient Collection Processing
$results = [System.Collections.Generic.List[PSObject]]::new()
foreach ($item in $collection) {
    $results.Add($processedItem)
}
```

---

## XAML Development Standards

### **XAML Formatting and Structure**

```xml
<!-- Standard XAML Document Structure -->
<UserControl x:Class="Project.Views.EntityView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
             mc:Ignorable="d"
             d:DesignHeight="450" d:DesignWidth="800">

    <Grid>
        <!-- Content organized with proper indentation -->
    </Grid>
</UserControl>
```

### **XAML Coding Standards**

- **Indentation**: 4 spaces per level (as defined in .editorconfig)
- **Namespace Order**: Standard WPF namespaces first, then third-party (Syncfusion)
- **Attribute Organization**: Class and namespace on first line, MC/Design on separate lines
- **Resource References**: Use StaticResource for styles, DynamicResource for themes
- **Naming Convention**: PascalCase for all element names and properties

### **Data Binding Best Practices**

```xml
<!-- Proper Data Binding Patterns -->
<DataGrid ItemsSource="{Binding Entities}"
          SelectedItem="{Binding SelectedEntity, Mode=TwoWay}"
          AutoGenerateColumns="False">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Name"
                           Binding="{Binding Name}"
                           Width="*" />
        <DataGridTextColumn Header="Status"
                           Binding="{Binding Status}"
                           Width="Auto" />
    </DataGrid.Columns>
</DataGrid>

<!-- Command Binding -->
<Button Content="Save"
        Command="{Binding SaveCommand}"
        IsEnabled="{Binding CanSave}"
        Style="{StaticResource PrimaryButtonStyle}" />
```

### **Syncfusion XAML Integration**

```xml
<!-- Syncfusion Control Usage -->
<syncfusion:SfDataGrid ItemsSource="{Binding Entities}"
                       SelectionMode="Single"
                       AllowEditing="True"
                       AllowSorting="True"
                       AllowFiltering="True">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn MappingName="Name"
                                  HeaderText="Entity Name" />
        <syncfusion:GridDateTimeColumn MappingName="CreatedDate"
                                      HeaderText="Created" />
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

### **Resource Dictionary Organization**

```xml
<!-- Theme Resource Dictionary Structure -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Colors Section -->
    <SolidColorBrush x:Key="PrimaryBrush" Color="#007ACC" />
    <SolidColorBrush x:Key="SecondaryBrush" Color="#F0F0F0" />

    <!-- Styles Section -->
    <Style x:Key="PrimaryButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="Padding" Value="10,5" />
    </Style>

    <!-- Templates Section -->
    <DataTemplate x:Key="EntityTemplate">
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="{Binding Name}" FontWeight="Bold" />
        </StackPanel>
    </DataTemplate>

</ResourceDictionary>
```

---

## C# Development Standards

### **C# Language Version and Features**

- **Language Version**: C# 12 (as defined in Directory.Build.props)
- **Target Framework**: .NET 8.0-windows (WPF projects)
- **Nullable Reference Types**: Enabled throughout (Directory.Build.props)
- **Implicit Usings**: Enabled for common namespaces

### **Code Formatting Standards** (from .editorconfig)

```csharp
// Proper C# Formatting
public class EntityService : IEntityService
{
    private readonly ILogger<EntityService> _logger;
    private readonly AppContext _context;

    public EntityService(ILogger<EntityService> logger, AppContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<Entity>> GetEntitiesAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving entities from database");

            var entities = await _context.Entities
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} entities", entities.Count);
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entities");
            throw;
        }
    }
}
```

### **Nullable Reference Types Implementation**

```csharp
// Proper Nullable Implementation
public class Entity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Non-nullable with default
    public string? Description { get; set; }         // Nullable
    public DateTime CreatedDate { get; set; }

    // Collections initialized to prevent null reference
    public List<RelatedEntity> RelatedEntities { get; set; } = new();
}

// Method with nullable parameters
public Entity? FindEntity(string? name)
{
    if (string.IsNullOrEmpty(name))
        return null;

    return _entities.FirstOrDefault(e =>
        e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
```

### **Async/Await Patterns**

```csharp
// Proper Async Implementation
public async Task<Result<Entity>> CreateEntityAsync(CreateEntityRequest request)
{
    using var activity = _logger.BeginScope("CreateEntity");

    try
    {
        // Validate input
        if (request is null)
            return Result<Entity>.Failure("Request cannot be null");

        // Async operation
        var entity = new Entity
        {
            Name = request.Name,
            Description = request.Description,
            CreatedDate = DateTime.UtcNow
        };

        _context.Entities.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Entity {Name} created with ID {Id}",
            entity.Name, entity.Id);

        return Result<Entity>.Success(entity);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create entity {Name}", request.Name);
        return Result<Entity>.Failure($"Creation failed: {ex.Message}");
    }
}
```

### **MVVM ViewModel Patterns**

```csharp
// Proper ViewModel Implementation
public class EntityViewModel : BaseViewModel
{
    private readonly IEntityService _entityService;
    private ObservableCollection<Entity> _entities = new();
    private Entity? _selectedEntity;

    public EntityViewModel(IEntityService entityService)
    {
        _entityService = entityService;
        LoadEntitiesCommand = new RelayCommand(async () => await LoadEntitiesAsync());
        SaveEntityCommand = new RelayCommand(async () => await SaveEntityAsync(), CanSaveEntity);
    }

    public ObservableCollection<Entity> Entities
    {
        get => _entities;
        set => SetProperty(ref _entities, value);
    }

    public Entity? SelectedEntity
    {
        get => _selectedEntity;
        set
        {
            if (SetProperty(ref _selectedEntity, value))
            {
                SaveEntityCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public RelayCommand LoadEntitiesCommand { get; }
    public RelayCommand SaveEntityCommand { get; }

    private async Task LoadEntitiesAsync()
    {
        try
        {
            IsLoading = true;
            var entities = await _entityService.GetEntitiesAsync();
            Entities.Clear();
            foreach (var entity in entities)
            {
                Entities.Add(entity);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load entities: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSaveEntity() => SelectedEntity is not null && !IsLoading;
}
```

---

## YAML Configuration Standards

### **GitHub Actions YAML Structure**

```yaml
# Standard GitHub Actions Format
name: 🚌 Project CI Pipeline

on:
    push:
        branches: [master, main, develop]
    pull_request:
        branches: [master, main]
    workflow_dispatch:
        inputs:
            debug_enabled:
                type: boolean
                description: "Enable debug mode for troubleshooting"
                default: false

env:
    DOTNET_VERSION: "9.0.x"
    SOLUTION_FILE: "Project.sln"
    BUILD_CONFIGURATION: "Release"

jobs:
    build-and-test:
        name: 🏗️ Build & Test
        runs-on: windows-latest
        timeout-minutes: 30

        steps:
            - name: 📥 Checkout Code
              uses: actions/checkout@v4
              with:
                  fetch-depth: 0

            - name: ⚙️ Setup .NET
              uses: actions/setup-dotnet@v4
              with:
                  dotnet-version: ${{ env.DOTNET_VERSION }}

            - name: 📦 Restore Dependencies
              run: dotnet restore ${{ env.SOLUTION_FILE }}

            - name: 🏗️ Build Solution
              run: dotnet build ${{ env.SOLUTION_FILE }} --configuration ${{ env.BUILD_CONFIGURATION }} --no-restore

            - name: 🧪 Run Tests
              run: dotnet test ${{ env.SOLUTION_FILE }} --configuration ${{ env.BUILD_CONFIGURATION }} --no-build --verbosity normal
```

### **YAML Formatting Standards** (from .editorconfig)

- **Indentation**: 2 spaces per level
- **Line Endings**: CRLF (Windows)
- **UTF-8 Encoding**: With BOM
- **Trailing Whitespace**: Trimmed
- **Final Newline**: Required

### **Dependabot Configuration**

```yaml
# .github/dependabot.yml
version: 2
updates:
    - package-ecosystem: "nuget"
      directory: "/"
      schedule:
          interval: "weekly"
          day: "monday"
          time: "09:00"
      open-pull-requests-limit: 10
      reviewers:
          - "project-maintainer"
      labels:
          - "dependencies"
          - "automerge"
```

---

## Configuration File Standards

### **Directory.Build.props Standards**

- **Centralized Configuration**: All project-wide settings in single file
- **Version Management**: Centralized package versions
- **Build Optimization**: Performance and compilation settings
- **Code Analysis**: Practical ruleset with reduced noise
- **Nullable Reference Types**: Enabled with Phase 1 suppressions

### **EditorConfig Implementation**

```editorconfig
# .editorconfig - Code Style Standards
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.{cs,csx}]
# C# Formatting Rules
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_indent_case_contents = true
csharp_space_after_cast = false

# Diagnostic Configuration
dotnet_diagnostic.CA2007.severity = suggestion
dotnet_diagnostic.CS1061.severity = error
```

### **Global.json Configuration**

```json
{
    "sdk": {
        "version": "9.0.303",
        "rollForward": "latestMinor"
    },
    "msbuild-sdks": {
        "Microsoft.Build.Traversal": "3.4.0"
    }
}
```

### **NuGet.config Standards**

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="syncfusion" value="https://nuget.syncfusion.com/nuget_packages/package.svc/" />
  </packageSources>

  <packageManagement>
    <add key="format" value="0" />
    <add key="disabled" value="False" />
  </packageManagement>
</configuration>
```

---

## Build Configuration Standards

### **MSBuild Property Standards**

**TargetFramework**: net9.0-windows for WPF projects

- **LangVersion**: 12 (C# 12 features)
- **Nullable**: enable (nullable reference types)
- **ImplicitUsings**: enable (common namespace imports)
- **GenerateDocumentationFile**: true (XML documentation)

### **Code Analysis Configuration**

- **EnableNETAnalyzers**: true
- **AnalysisMode**: Recommended
- **Custom Ruleset**: Project-Practical.ruleset
- **Practical Suppression**: Low-impact warnings suppressed for MVP

### **Performance Optimization Settings**

```xml
<PropertyGroup>
  <UseSharedCompilation>true</UseSharedCompilation>
  <BuildInParallel>true</BuildInParallel>
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
</PropertyGroup>
```

### **Test Project Configuration**

```xml
<PropertyGroup Condition="'$(IsTestProject)' == 'true'">
  <UseSharedCompilation>false</UseSharedCompilation>
  <BuildInParallel>false</BuildInParallel>
  <GenerateDocumentationFile>false</GenerateDocumentationFile>
</PropertyGroup>
```

---

## VS Code Integration Standards

### **Extensions Requirements**

- **C# Dev Kit**: ms-dotnettools.csdevkit
- **XAML Styler**: ms-dotnettools.xaml
- **Task Explorer**: spmeesseman.vscode-taskexplorer
- **PowerShell**: ms-vscode.powershell

### **Settings Configuration**

```json
{
    "terminal.integrated.profiles.windows": {
        "PowerShell 7.5.2": {
            "path": "pwsh.exe",
            "args": [
                "-NoProfile",
                "-NoExit",
                "-Command",
                "& 'PowerShell/Profiles/Microsoft.PowerShell_profile.ps1'"
            ]
        }
    },
    "terminal.integrated.defaultProfile.windows": "PowerShell 7.5.2",
    "omnisharp.useModernNet": true,
    "dotnet.completion.showCompletionItemsFromUnimportedNamespaces": true
}
```

### **Task Configuration Standards**

```json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build",
            "command": "dotnet",
            "type": "process",
            "args": ["build", "${workspaceFolder}/Project.sln"],
            "group": "build",
            "presentation": {
                "reveal": "silent"
            },
            "problemMatcher": "$msCompile"
        }
    ]
}
```

---

## File Management and Cleanup Best Practices

### **🧹 Temporary File Cleanup**

Always clean up temporary files created during development:

- **Remove `.new`, `.bak`, `.backup`, `.old` files** after successful operations
- **Delete `*_temp`, `*_tmp`, `*_test` files** when no longer needed
- **Clean up `Migration_Backups/` directories** after migration completion
- **Remove duplicate files** (e.g., `file.cs` and `file_new.cs`)

### **🚫 Files to Always Remove**

- Build artifacts: `bin/`, `obj/`, `TestResults/`
- IDE files: `.vs/`, `*.user`, `*.suo`
- Temporary downloads: `*.crdownload`, `*.tmp`
- Backup files: `*.backup_*`, `*_backup*`
- Empty directories that serve no purpose

### **📝 Git Repository Hygiene**

- Use `.gitignore` to prevent tracking build artifacts
- Remove large binary files from git history if accidentally committed
- Stage only source files, never build artifacts
- Clean up redundant documentation after project phases complete
- **Remove trailing whitespace** at the end of lines and files
- **Ensure files end with a single newline** character
- **Use consistent line endings** (CRLF on Windows, LF on Unix)

### **✨ Code Formatting Standards**

- **No trailing whitespace** - remove spaces/tabs at line endings
- **Consistent indentation** - use spaces or tabs consistently (prefer spaces)
- **File endings** - ensure files end with exactly one newline
- **Line length** - keep lines under 120 characters when practical
- **Empty lines** - use sparingly and consistently for logical separation
- **No nullable reference types** - avoid using nullable properties, parameters, or return types in new code

### **🔄 Development Workflow**

When creating temporary files:

1. Use descriptive names with clear temporary indicators
2. Set reminders to clean up after task completion
3. Add temporary patterns to `.gitignore` if needed
4. Use `git clean -fd` to remove untracked files periodically

**File Corruption Assessment Protocol:**

1. **First Check**: Identify specific error messages and their line numbers
2. **Scope Analysis**: Determine if errors are localized (missing method, typo) or systemic
3. **Impact Assessment**: Count affected files and error types
4. **User Consultation**: For 3+ files or complex structural issues, ask user before rebuilding
5. **Documentation**: Always report what was found before proposing solution approach
