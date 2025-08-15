# BusBuddy AI Assistant Instructions — 2025-08-15

Purpose: Keep BusBuddy stable and compliant. MVP is done; focus on production hardening, Syncfusion-only UI, and PowerShell/.NET compliance. Always prefer bb* commands and run anti-regression gates before proposing changes.

1) Current state summary
- MVP: Achieved (students + route assignment) — validated via bbMvpCheck; commit 29b7dc1
- SDK/TFM: .NET SDK 9.0.303; WPF projects target net9.0-windows
- Packages (Directory.Build.props): Syncfusion WPF 30.2.5; EF Core 9.0.8; Serilog 4.3.0
- PowerShell: 7.5.2; compliance ≈45%; known issues:
  - 35 empty catch blocks in BusBuddy.psm1 — fix required
  - PSUseShouldProcess violations — add SupportsShouldProcess/ShouldProcess
  - No Write-Host — use Write-Information/Write-Output
- Non-MVP integrations: XAI and Google Earth Engine deferred/disabled
- UI: Syncfusion-only policy — no standard WPF controls in new or refactored code

2) Goals (now that MVP is complete)
- Production hardening: stability, logging (Serilog), error handling
- UI migration cleanup: remove/upgrade any remaining standard WPF grids to Syncfusion
- Compliance fixes: PowerShell module conformance and XAML validation
- Defer: XAI and Google Earth Engine until post-hardening

3) Command surface (bb*) — use these first
- Entrypoint: PowerShell 7.5.2. Ensure module import via PowerShell/Profiles/Import-BusBuddyModule.ps1
- Discovery: bbCommands (lists active aliases)
- Primary workflow:
  - bbHealth — environment/project health
  - bbBuild — build solution
  - bbRun — run WPF app
  - bbTest — run tests
  - bbMvpCheck — validate core MVP scenarios (students/routes)
  - bbAntiRegression — scan for disallowed APIs/patterns
  - bbXamlValidate — Syncfusion-only XAML validation
  - bbDevSession — optional full dev session
  - bbRefresh — rewire aliases/module for current session
- Notes:
  - Prefer bb* over raw dotnet. Use dotnet only for diagnostics if bb* unavailable.
  - If aliases are missing, run bbRefresh (or re-run Import-BusBuddyModule.ps1).

4) Mandatory pre-change gates (prevent regressions)
- Always run bbHealth, then bbAntiRegression and bbXamlValidate before proposing or committing changes.
- Hard rules:
  - No Microsoft.Extensions.Logging (use Serilog only).
  - No standard WPF DataGrid or other stock controls where a Syncfusion equivalent exists.
  - No Write-Host in PowerShell (use Write-Information/Write-Output).
- If gates fail: fix violations first, then proceed.

5) Documentation-first (zero tolerance)
- Provide official documentation links when proposing code:
  - PowerShell: https://learn.microsoft.com/powershell/
  - .NET/C#: https://learn.microsoft.com/dotnet/
  - WPF: https://learn.microsoft.com/dotnet/desktop/wpf/
  - EF Core: https://learn.microsoft.com/ef/core/
  - Syncfusion WPF: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- Include source links in code comments where practical.

6) PowerShell standards and remediation (current focus)
- Version: PowerShell 7.5.2; run under PSEdition Core
- Module import: PowerShell/Profiles/Import-BusBuddyModule.ps1 (ensures bb* aliases)
- Parallel defaults: ForEach-Object -Parallel -ThrottleLimit 12 (tune per task)
- Output streams: replace Write-Host with Write-Information/Write-Output
  - Streams ref: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
- Error handling:
  - Replace empty catch blocks; log with Write-Information and rethrow/handle appropriately
  - Try/Catch guidance: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Try_Catch_Finally
- ShouldProcess compliance:
  - Add [CmdletBinding(SupportsShouldProcess = $true)] for impactful functions
  - Use $PSCmdlet.ShouldProcess() and ConfirmImpact where applicable
  - Docs: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process and about_Functions_CmdletBindingAttribute
- Exports:
  - Explicit Export-ModuleMember for public functions
  - Module guidelines: https://learn.microsoft.com/powershell/scripting/developer/module/writing-a-windows-powershell-module

7) Environment and configuration
- Required environment variables (examples):
  - BUSBUDDY_CONNECTION — default connection string name/key
  - SYNCFUSION_LICENSE_KEY — required before any Syncfusion UI initialization
  - AZURE_SQL_USER / AZURE_SQL_PASSWORD — used when DatabaseProvider=Azure
- Database provider set via appsettings.json: LocalDB (dev) or Azure (prod)
- EF Core 9.0.8 usage — prefer async operations; document any migration steps

8) Syncfusion-only UI policy (WPF)
- Never replace Syncfusion controls with standard WPF controls
- Always fix namespaces/references instead of downgrading
- SfDataGrid patterns:
  - Docs (Getting Started): https://help.syncfusion.com/wpf/datagrid/getting-started
  - API: https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Grid.SfDataGrid.html
- Validate UI with bbXamlValidate before commit

9) Logging (Serilog only)
- Use static logger per class via Log.ForContext<T>()
- Structured logging with templates and properties
- No Console.WriteLine/Debug.WriteLine/Trace.WriteLine in production code
- Log exceptions with context; prefer enrichment via LogContext
- Replace any Microsoft.Extensions.Logging usage with Serilog

10) Non-MVP integrations (keep disabled until scheduled)
- XAI and Google Earth Engine stay deferred
- If compilation breaks due to these, disable by renaming file to .disabled and document the decision
- After disabling, run bbBuild and bbMvpCheck to confirm stability

11) Minimal change workflow (per PR or local change)
- Before: bbRefresh; bbHealth
- Validate: bbAntiRegression; bbXamlValidate
- Build/Test: bbBuild; bbTest
- MVP assurance: bbMvpCheck
- Only then propose edits; include doc links and ensure no regressions

12) File hygiene and formatting
- Remove trailing whitespace; ensure single final newline
- Keep lines ≲120 chars where practical
- Clean temp files (*.new, *.bak, *.old, *_tmp) and build artifacts (bin/, obj/)
- Use CRLF on Windows as per repo settings

13) Cross-references
- Project README.md — setup, run instructions, and architecture overview
- vscode-userdata/BusBuddy.instructions.md — domain specifics and preferences
- Directory.Build.props — centralized versions and analyzer settings
- NuGet.config — sources (nuget.org, syncfusion)
- Global.json — SDK pin (9.0.303)

Appendix: Quick checks
- If bb* aliases are missing: run bbRefresh or execute PowerShell/Profiles/Import-BusBuddyModule.ps1
- If XAML fails on Syncfusion types: check xmlns:syncfusion="http://schemas.syncfusion.com/wpf" and package references; validate license registration early in App
- If PS analyzer flags ShouldProcess: add SupportsShouldProcess and guard with $PSCmdlet.ShouldProcess()

Reminder: Be concise, preserve working Syncfusion implementations, and never regress to standard WPF controls. Always run bbAntiRegression and bbXamlValidate before suggesting changes.
### **External Service Integrations**
- **Google Earth Engine**: Project-based authentication with service account keys
- **Azure Services**: Environment-based credential management
- **Syncfusion Licensing**: Environment variable `${SYNCFUSION_LICENSE_KEY}`

### **Build Configuration Standards**
- **Nullable Reference Types**: Enabled throughout solution
- **Implicit Usings**: Enabled for common namespace imports
- **Documentation Generation**: XML documentation files for all public APIs
- **Code Analysis**: .NET analyzers with practical ruleset for MVP development
- **Warning Treatment**: Warnings allowed during MVP, errors enforced in production

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
 **Target Framework**: .NET 9.0-windows (WPF projects)
    // Data operation
    var data = await context.Drivers.ToListAsync();
    return data;
 **Syncfusion WPF**: 30.2.5 (per Directory.Build.props)
 **Entity Framework Core**: 9.0.8 (.NET 9 compatible)
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
bb-dev-session    # Start a full dev session (if available in module)
bbRefresh         # Reinitialize aliases/module for current session
Get-BusBuddyAliasStatus # Safe alias status under StrictMode
Suspend-BusBuddyCleanup # Pause cleanup (sets Env:BB_CLEANUP_PAUSED=1)
Resume-BusBuddyCleanup  # Resume cleanup (clears Env:BB_CLEANUP_PAUSED)
- When adding new comments, use em dashes (`—`) instead of double dashes

- **[Project].Tests**: Comprehensive test suite for all layers
- **Controls/**: Custom user controls and control templates
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

- **Extensions/**: Core extension methods
- **Interceptors/**: EF interceptors and data access enhancements
- **Migrations/**: Entity Framework migrations
- **Models/**: Domain models and entities
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

### **Phase 1 Architecture (Minimum Viable)**
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

- Framework: NUnit (use repo-pinned version, e.g., 4.3.1). Docs: https://nunit.org/ and https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-nunit
- Commands:
  - bbTest — runs tests (aliases to Invoke-BusBuddyTest/Start-BusBuddyTest; hyphen alias bb-test remains for compatibility)
  - bbTestWatch — watch mode (dotnet watch test)
  - bbTestReport — generates reports in Documentation/Reports/
- Filters (examples):
  - Category filters: --filter "TestCategory=Scheduler" or --filter "Category=Scheduler"
  - Suite filters (custom NUnit property): --filter "TestSuite=Unit" or "TestSuite=Integration|TestSuite=Core|TestSuite=WPF"
- Outputs:
  - TRX: TestResults/*.trx via --logger "trx;LogFileName=BusBuddy.Tests.trx"
  - Coverage: --collect:"XPlat Code Coverage" (use coverlet data for CI reports)
  - Coverage status: 75%+ achieved, 85% target
  - Reports: Documentation/Reports/ (HTML/markdown summaries via bbTestReport)
- Local examples:
  - Quick: bbTest --filter "TestCategory=Scheduler"
  - Watch changed tests: bbTestWatch --filter "TestSuite=Unit"
  - With TRX + coverage: bbTest --logger "trx;LogFileName=BusBuddy.Tests.trx" --collect:"XPlat Code Coverage"
- CI examples (GitHub Actions):
  - dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj --configuration Release --logger "trx;LogFileName=ci.trx" --collect:"XPlat Code Coverage" --filter "TestSuite=Core|TestSuite=Integration"
- Self-contained tests:
  - Use EF Core InMemory provider for data tests. Docs: https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy and https://learn.microsoft.com/ef/core/testing/in-memory
  - Prefer mocks for services (e.g., Moq) and isolated test setup/teardown.
- NUnit categorization:
  - [Category("Scheduler")] and/or [Property("TestSuite","Unit")] on tests to support filters.
- Anti-regression gates before proposing changes:
  - Run bbTest and bbXamlValidate; fix failures first.
- Legacy:
  - Run-Phase4-NUnitTests-Modular.ps1 is archived; use bbTest/bbTestWatch/bbTestReport instead.

```csharp
// Example: EF Core InMemory context (docs: EF Core testing with InMemory)
[TestFixture, Category("Scheduler"), Property("TestSuite","Unit")]
public class SchedulerServiceTests
{
    [Test]
    public async Task GeneratesDailyRoutePlan()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        // ...seed entities...

        var svc = new SchedulerService(db /*, mocks... */);
        var plan = await svc.GenerateDailyPlanAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Routes, Is.Not.Empty);
    }
}
```

## CI Workflows — GitHub Actions (ci.yml) [Updated]

- Triggers:
  - push and pull_request on branches: main, develop
  - workflow_dispatch for manual runs
- Secrets required: SYNCFUSION_LICENSE_KEY, AZURE_SQL_USER, AZURE_SQL_PASSWORD (and related Azure secrets as needed)
- Artifacts: SchedulerTests.trx, migration-script.sql, coverage reports (XPlat Code Coverage)
- Known CI issues:
  - YAML indentation must be 2 spaces throughout
  - Ensure Azure SQL firewall cleanup runs with if: always() so resources are cleaned up even on failure

Example ci.yml (excerpt):
```yaml
name: BusBuddy CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
  workflow_dispatch:

jobs:
  scheduler-tests:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x
      - name: Restore
        run: dotnet restore
      - name: Test (Scheduler only, TRX + Coverage)
        run: >
          dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj
          --configuration Release
          --logger "trx;LogFileName=SchedulerTests.trx"
          --collect:"XPlat Code Coverage"
          --filter "TestCategory=Scheduler"
      - name: Upload TRX
        uses: actions/upload-artifact@v4
        with:
          name: SchedulerTests.trx
          path: **/TestResults/**/*.trx

  build-and-test:
    runs-on: windows-latest
    strategy:
      matrix:
        platform: [ x64, x86 ]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x
      - name: Build (matrix)
        run: >
          dotnet build BusBuddy.sln
          -p:Platform=${{ matrix.platform }}
          --configuration Release
          --verbosity detailed
      - name: Test (Core + Integration, TRX + Coverage)
        run: >
          dotnet test BusBuddy.sln
          --configuration Release
          --no-build
          --logger "trx;LogFileName=AllTests_${{ matrix.platform }}.trx"
          --collect:"XPlat Code Coverage"
          --filter "TestSuite=Core|TestSuite=Integration"
      - name: Upload test artifacts
        uses: actions/upload-artifact@v4
        with:
          name: TestResults_${{ matrix.platform }}
          path: |
            **/TestResults/**/*.trx
            **/TestResults/**/coverage.cobertura.xml

  quality-analysis:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: PowerShell 7.5.2
        shell: pwsh
        run: $PSVersionTable
      - name: ScriptAnalyzer (PSUseShouldProcess, empty catch, Write-Host)
        shell: pwsh
        run: >
          Install-Module PSScriptAnalyzer -Scope CurrentUser -Force;
          Invoke-ScriptAnalyzer -Path PowerShell -Recurse -Severity Error,Warning
          -EnableExit -Settings PSAvoidUsingWriteHost,PSUseShouldProcess,PSAvoidEmptyCatchBlock
          | Tee-Object -FilePath Documentation/Reports/PSScriptAnalyzerReport.txt
      - name: Upload analysis report
        uses: actions/upload-artifact@v4
        with:
          name: PSScriptAnalyzerReport
          path: Documentation/Reports/PSScriptAnalyzerReport.txt

  security-scan:
    permissions:
      security-events: write
      actions: read
      contents: read
    uses: github/codeql-action/.github/workflows/codeql.yml@v3

  repository-health:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: License and env wiring
        shell: pwsh
        env:
          SYNCFUSION_LICENSE_KEY: ${{ secrets.SYNCFUSION_LICENSE_KEY }}
        run: |
          if ($env:SYNCFUSION_LICENSE_KEY) {
            Write-Information "Syncfusion license key present." -InformationAction Continue
          } else {
            Write-Warning "Syncfusion license key missing."
          }
      - name: bb-health diagnostics
        shell: pwsh
        run: |
          ./PowerShell/Profiles/Import-BusBuddyModule.ps1
          bb-health
      - name: Generate migration script (example)
        shell: pwsh
        run: dotnet ef migrations script -o Documentation/Reports/migration-script.sql
      - name: Upload migration script
        uses: actions/upload-artifact@v4
        with:
          name: migration-script.sql
          path: Documentation/Reports/migration-script.sql
      - name: Azure SQL firewall cleanup
        if: always()
        shell: pwsh
        run: |
          Write-Information "Ensure Azure SQL firewall rules cleaned up." -InformationAction Continue
          # ...invoke cleanup if used earlier...
```

Notes:
- Coverage: Use --collect:"XPlat Code Coverage". Surface coverage summary in PR checks; target 85% (≥75% current).
- Post-MVP: Re-enable deferred features only after bb-anti-regression passes in CI.

3) Command surface (bb*) — use these first
- Entrypoint: PowerShell 7.5.2. Ensure module import via PowerShell/Profiles/Import-BusBuddyModule.ps1
- Discovery: bbCommands (lists active aliases)
- Primary workflow:
  - bbHealth — environment/project health
  - bbBuild — build solution
  - bbRun — run WPF app
  - bbTest — run tests
  - bbMvpCheck — validate core MVP scenarios (students/routes)
  - bbAntiRegression — scan for disallowed APIs/patterns
  - bbXamlValidate — Syncfusion-only XAML validation
  - bbDevSession — optional full dev session
  - bbRefresh — rewire aliases/module for current session
- Notes:
  - Prefer bb* over raw dotnet. Use dotnet only for diagnostics if bb* unavailable.
  - If aliases are missing, run bbRefresh (or re-run Import-BusBuddyModule.ps1).

4) Mandatory pre-change gates (prevent regressions)
- Always run bbHealth, then bbAntiRegression and bbXamlValidate before proposing or committing changes.
- Hard rules:
  - No Microsoft.Extensions.Logging (use Serilog only).
  - No standard WPF DataGrid or other stock controls where a Syncfusion equivalent exists.
  - No Write-Host in PowerShell (use Write-Information/Write-Output).
- If gates fail: fix violations first, then proceed.

5) Documentation-first (zero tolerance)
- Provide official documentation links when proposing code:
  - PowerShell: https://learn.microsoft.com/powershell/
  - .NET/C#: https://learn.microsoft.com/dotnet/
  - WPF: https://learn.microsoft.com/dotnet/desktop/wpf/
  - EF Core: https://learn.microsoft.com/ef/core/
  - Syncfusion WPF: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- Include source links in code comments where practical.

6) PowerShell standards and remediation (current focus)
- Version: PowerShell 7.5.2; run under PSEdition Core
- Module import: PowerShell/Profiles/Import-BusBuddyModule.ps1 (ensures bb* aliases)
- Parallel defaults: ForEach-Object -Parallel -ThrottleLimit 12 (tune per task)
- Output streams: replace Write-Host with Write-Information/Write-Output
  - Streams ref: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
- Error handling:
  - Replace empty catch blocks; log with Write-Information and rethrow/handle appropriately
  - Try/Catch guidance: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Try_Catch_Finally
- ShouldProcess compliance:
  - Add [CmdletBinding(SupportsShouldProcess = $true)] for impactful functions
  - Use $PSCmdlet.ShouldProcess() and ConfirmImpact where applicable
  - Docs: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process and about_Functions_CmdletBindingAttribute
- Exports:
  - Explicit Export-ModuleMember for public functions
  - Module guidelines: https://learn.microsoft.com/powershell/scripting/developer/module/writing-a-windows-powershell-module

7) Environment and configuration
- Required environment variables (examples):
  - BUSBUDDY_CONNECTION — default connection string name/key
  - SYNCFUSION_LICENSE_KEY — required before any Syncfusion UI initialization
  - AZURE_SQL_USER / AZURE_SQL_PASSWORD — used when DatabaseProvider=Azure
- Database provider set via appsettings.json: LocalDB (dev) or Azure (prod)
- EF Core 9.0.8 usage — prefer async operations; document any migration steps

8) Syncfusion-only UI policy (WPF)
- Never replace Syncfusion controls with standard WPF controls
- Always fix namespaces/references instead of downgrading
- SfDataGrid patterns:
  - Docs (Getting Started): https://help.syncfusion.com/wpf/datagrid/getting-started
  - API: https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Grid.SfDataGrid.html
- Validate UI with bbXamlValidate before commit

9) Logging (Serilog only)
- Use static logger per class via Log.ForContext<T>()
- Structured logging with templates and properties
- No Console.WriteLine/Debug.WriteLine/Trace.WriteLine in production code
- Log exceptions with context; prefer enrichment via LogContext
- Replace any Microsoft.Extensions.Logging usage with Serilog

10) Non-MVP integrations (keep disabled until scheduled)
- XAI and Google Earth Engine stay deferred
- If compilation breaks due to these, disable by renaming file to .disabled and document the decision
- After disabling, run bbBuild and bbMvpCheck to confirm stability

11) Minimal change workflow (per PR or local change)
- Before: bbRefresh; bbHealth
- Validate: bbAntiRegression; bbXamlValidate
- Build/Test: bbBuild; bbTest
- MVP assurance: bbMvpCheck
- Only then propose edits; include doc links and ensure no regressions

12) File hygiene and formatting
- Remove trailing whitespace; ensure single final newline
- Keep lines ≲120 chars where practical
- Clean temp files (*.new, *.bak, *.old, *_tmp) and build artifacts (bin/, obj/)
- Use CRLF on Windows as per repo settings

13) Cross-references
- Project README.md — setup, run instructions, and architecture overview
- vscode-userdata/BusBuddy.instructions.md — domain specifics and preferences
- Directory.Build.props — centralized versions and analyzer settings
- NuGet.config — sources (nuget.org, syncfusion)
- Global.json — SDK pin (9.0.303)

Appendix: Quick checks
- If bb* aliases are missing: run bbRefresh or execute PowerShell/Profiles/Import-BusBuddyModule.ps1
- If XAML fails on Syncfusion types: check xmlns:syncfusion="http://schemas.syncfusion.com/wpf" and package references; validate license registration early in App
- If PS analyzer flags ShouldProcess: add SupportsShouldProcess and guard with $PSCmdlet.ShouldProcess()

Reminder: Be concise, preserve working Syncfusion implementations, and never regress to standard WPF controls. Always run bbAntiRegression and bbXamlValidate before suggesting changes.
### **External Service Integrations**
- **Google Earth Engine**: Project-based authentication with service account keys
- **Azure Services**: Environment-based credential management
- **Syncfusion Licensing**: Environment variable `${SYNCFUSION_LICENSE_KEY}`

### **Build Configuration Standards**
- **Nullable Reference Types**: Enabled throughout solution
- **Implicit Usings**: Enabled for common namespace imports
- **Documentation Generation**: XML documentation files for all public APIs
- **Code Analysis**: .NET analyzers with practical ruleset for MVP development
- **Warning Treatment**: Warnings allowed during MVP, errors enforced in production

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
 **Target Framework**: .NET 9.0-windows (WPF projects)
    // Data operation
    var data = await context.Drivers.ToListAsync();
    return data;
 **Syncfusion WPF**: 30.2.5 (per Directory.Build.props)
 **Entity Framework Core**: 9.0.8 (.NET 9 compatible)
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
bb-dev-session    # Start a full dev session (if available in module)
bbRefresh         # Reinitialize aliases/module for current session
Get-BusBuddyAliasStatus # Safe alias status under StrictMode
Suspend-BusBuddyCleanup # Pause cleanup (sets Env:BB_CLEANUP_PAUSED=1)
Resume-BusBuddyCleanup  # Resume cleanup (clears Env:BB_CLEANUP_PAUSED)
- When adding new comments, use em dashes (`—`) instead of double dashes

- **[Project].Tests**: Comprehensive test suite for all layers
- **Controls/**: Custom user controls and control templates
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

- **Extensions/**: Core extension methods
- **Interceptors/**: EF interceptors and data access enhancements
- **Migrations/**: Entity Framework migrations
- **Models/**: Domain models and entities
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

### **Phase 1 Architecture (Minimum Viable)**
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

- Framework: NUnit (use repo-pinned version, e.g., 4.3.1). Docs: https://nunit.org/ and https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-nunit
- Commands:
  - bbTest — runs tests (aliases to Invoke-BusBuddyTest/Start-BusBuddyTest; hyphen alias bb-test remains for compatibility)
  - bbTestWatch — watch mode (dotnet watch test)
  - bbTestReport — generates reports in Documentation/Reports/
- Filters (examples):
  - Category filters: --filter "TestCategory=Scheduler" or --filter "Category=Scheduler"
  - Suite filters (custom NUnit property): --filter "TestSuite=Unit" or "TestSuite=Integration|TestSuite=Core|TestSuite=WPF"
- Outputs:
  - TRX: TestResults/*.trx via --logger "trx;LogFileName=BusBuddy.Tests.trx"
  - Coverage: --collect:"XPlat Code Coverage" (use coverlet data for CI reports)
  - Coverage status: 75%+ achieved, 85% target
  - Reports: Documentation/Reports/ (HTML/markdown summaries via bbTestReport)
- Local examples:
  - Quick: bbTest --filter "TestCategory=Scheduler"
  - Watch changed tests: bbTestWatch --filter "TestSuite=Unit"
  - With TRX + coverage: bbTest --logger "trx;LogFileName=BusBuddy.Tests.trx" --collect:"XPlat Code Coverage"
- CI examples (GitHub Actions):
  - dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj --configuration Release --logger "trx;LogFileName=ci.trx" --collect:"XPlat Code Coverage" --filter "TestSuite=Core|TestSuite=Integration"
- Self-contained tests:
  - Use EF Core InMemory provider for data tests. Docs: https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy and https://learn.microsoft.com/ef/core/testing/in-memory
  - Prefer mocks for services (e.g., Moq) and isolated test setup/teardown.
- NUnit categorization:
  - [Category("Scheduler")] and/or [Property("TestSuite","Unit")] on tests to support filters.
- Anti-regression gates before proposing changes:
  - Run bbTest and bbXamlValidate; fix failures first.
- Legacy:
  - Run-Phase4-NUnitTests-Modular.ps1 is archived; use bbTest/bbTestWatch/bbTestReport instead.

```csharp
// Example: EF Core InMemory context (docs: EF Core testing with InMemory)
[TestFixture, Category("Scheduler"), Property("TestSuite","Unit")]
public class SchedulerServiceTests
{
    [Test]
    public async Task GeneratesDailyRoutePlan()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        // ...seed entities...

        var svc = new SchedulerService(db /*, mocks... */);
        var plan = await svc.GenerateDailyPlanAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Routes, Is.Not.Empty);
    }
}
```

## CI Workflows — GitHub Actions (ci.yml) [Updated]

- Triggers:
  - push and pull_request on branches: main, develop
  - workflow_dispatch for manual runs
- Secrets required: SYNCFUSION_LICENSE_KEY, AZURE_SQL_USER, AZURE_SQL_PASSWORD (and related Azure secrets as needed)
- Artifacts: SchedulerTests.trx, migration-script.sql, coverage reports (XPlat Code Coverage)
- Known CI issues:
  - YAML indentation must be 2 spaces throughout
  - Ensure Azure SQL firewall cleanup runs with if: always() so resources are cleaned up even on failure

Example ci.yml (excerpt):
```yaml
name: BusBuddy CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
  workflow_dispatch:

jobs:
  scheduler-tests:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x
      - name: Restore
        run: dotnet restore
      - name: Test (Scheduler only, TRX + Coverage)
        run: >
          dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj
          --configuration Release
          --logger "trx;LogFileName=SchedulerTests.trx"
          --collect:"XPlat Code Coverage"
          --filter "TestCategory=Scheduler"
      - name: Upload TRX
        uses: actions/upload-artifact@v4
        with:
          name: SchedulerTests.trx
          path: **/TestResults/**/*.trx

  build-and-test:
    runs-on: windows-latest
    strategy:
      matrix:
        platform: [ x64, x86 ]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x
      - name: Build (matrix)
        run: >
          dotnet build BusBuddy.sln
          -p:Platform=${{ matrix.platform }}
          --configuration Release
          --verbosity detailed
      - name: Test (Core + Integration, TRX + Coverage)
        run: >
          dotnet test BusBuddy.sln
          --configuration Release
          --no-build
          --logger "trx;LogFileName=AllTests_${{ matrix.platform }}.trx"
          --collect:"XPlat Code Coverage"
          --filter "TestSuite=Core|TestSuite=Integration"
      - name: Upload test artifacts
        uses: actions/upload-artifact@v4
        with:
          name: TestResults_${{ matrix.platform }}
          path: |
            **/TestResults/**/*.trx
            **/TestResults/**/coverage.cobertura.xml

  quality-analysis:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: PowerShell 7.5.2
        shell: pwsh
        run: $PSVersionTable
      - name: ScriptAnalyzer (PSUseShouldProcess, empty catch, Write-Host)
        shell: pwsh
        run: >
          Install-Module PSScriptAnalyzer -Scope CurrentUser -Force;
          Invoke-ScriptAnalyzer -Path PowerShell -Recurse -Severity Error,Warning
          -EnableExit -Settings PSAvoidUsingWriteHost,PSUseShouldProcess,PSAvoidEmptyCatchBlock
          | Tee-Object -FilePath Documentation/Reports/PSScriptAnalyzerReport.txt
      - name: Upload analysis report
        uses: actions/upload-artifact@v4
        with:
          name: PSScriptAnalyzerReport
          path: Documentation/Reports/PSScriptAnalyzerReport.txt

  security-scan:
    permissions:
      security-events: write
      actions: read
      contents: read
    uses: github/codeql-action/.github/workflows/codeql.yml@v3

  repository-health:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: License and env wiring
        shell: pwsh
        env:
          SYNCFUSION_LICENSE_KEY: ${{ secrets.SYNCFUSION_LICENSE_KEY }}
        run: |
          if ($env:SYNCFUSION_LICENSE_KEY) {
            Write-Information "Syncfusion license key present." -InformationAction Continue
          } else {
            Write-Warning "Syncfusion license key missing."
          }
      - name: bb-health diagnostics
        shell: pwsh
        run: |
          ./PowerShell/Profiles/Import-BusBuddyModule.ps1
          bb-health
      - name: Generate migration script (example)
        shell: pwsh
        run: dotnet ef migrations script -o Documentation/Reports/migration-script.sql
      - name: Upload migration script
        uses: actions/upload-artifact@v4
        with:
          name: migration-script.sql
          path: Documentation/Reports/migration-script.sql
      - name: Azure SQL firewall cleanup
        if: always()
        shell: pwsh
        run: |
          Write-Information "Ensure Azure SQL firewall rules cleaned up." -InformationAction Continue
          # ...invoke cleanup if used earlier...
```

Notes:
- Coverage: Use --collect:"XPlat Code Coverage". Surface coverage summary in PR checks; target 85% (≥75% current).
- Post-MVP: Re-enable deferred features only after bb-anti-regression passes in CI.

3) Command surface (bb*) — use these first
- Entrypoint: PowerShell 7.5.2. Ensure module import via PowerShell/Profiles/Import-BusBuddyModule.ps1
- Discovery: bbCommands (lists active aliases)
- Primary workflow:
  - bbHealth — environment/project health
  - bbBuild — build solution
  - bbRun — run WPF app
  - bbTest — run tests
  - bbMvpCheck — validate core MVP scenarios (students/routes)
  - bbAntiRegression — scan for disallowed APIs/patterns
  - bbXamlValidate — Syncfusion-only XAML validation
  - bbDevSession — optional full dev session
  - bbRefresh — rewire aliases/module for current session
- Notes:
  - Prefer bb* over raw dotnet. Use dotnet only for diagnostics if bb* unavailable.
  - If aliases are missing, run bbRefresh (or re-run Import-BusBuddyModule.ps1).

4) Mandatory pre-change gates (prevent regressions)
- Always run bbHealth, then bbAntiRegression and bbXamlValidate before proposing or committing changes.
- Hard rules:
  - No Microsoft.Extensions.Logging (use Serilog only).
  - No standard WPF DataGrid or other stock controls where a Syncfusion equivalent exists.
  - No Write-Host in PowerShell (use Write-Information/Write-Output).
- If gates fail: fix violations first, then proceed.

5) Documentation-first (zero tolerance)
- Provide official documentation links when proposing code:
  - PowerShell: https://learn.microsoft.com/powershell/
  - .NET/C#: https://learn.microsoft.com/dotnet/
  - WPF: https://learn.microsoft.com/dotnet/desktop/wpf/
  - EF Core: https://learn.microsoft.com/ef/core/
  - Syncfusion WPF: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- Include source links in code comments where practical.

6) PowerShell standards and remediation (current focus)
- Version: PowerShell 7.5.2; run under PSEdition Core
- Module import: PowerShell/Profiles/Import-BusBuddyModule.ps1 (ensures bb* aliases)
- Parallel defaults: ForEach-Object -Parallel -ThrottleLimit 12 (tune per task)
- Output streams: replace Write-Host with Write-Information/Write-Output
  - Streams ref: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
- Error handling:
  - Replace empty catch blocks; log with Write-Information and rethrow/handle appropriately
  - Try/Catch guidance: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Try_Catch_Finally
- ShouldProcess compliance:
  - Add [CmdletBinding(SupportsShouldProcess = $true)] for impactful functions
  - Use $PSCmdlet.ShouldProcess() and ConfirmImpact where applicable
  - Docs: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process and about_Functions_CmdletBindingAttribute
- Exports:
  - Explicit Export-ModuleMember for public functions
  - Module guidelines: https://learn.microsoft.com/powershell/scripting/developer/module/writing-a-windows-powershell-module

7) Environment and configuration
- Required environment variables (examples):
  - BUSBUDDY_CONNECTION — default connection string name/key
  - SYNCFUSION_LICENSE_KEY — required before any Syncfusion UI initialization
  - AZURE_SQL_USER / AZURE_SQL_PASSWORD — used when DatabaseProvider=Azure
- Database provider set via appsettings.json: LocalDB (dev) or Azure (prod)
- EF Core 9.0.8 usage — prefer async operations; document any migration steps

8) Syncfusion-only UI policy (WPF)
- Never replace Syncfusion controls with standard WPF controls
- Always fix namespaces/references instead of downgrading
- SfDataGrid patterns:
  - Docs (Getting Started): https://help.syncfusion.com/wpf/datagrid/getting-started
  - API: https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Grid.SfDataGrid.html
- Validate UI with bbXamlValidate before commit

9) Logging (Serilog only)
- Use static logger per class via Log.ForContext<T>()
- Structured logging with templates and properties
- No Console.WriteLine/Debug.WriteLine/Trace.WriteLine in production code
- Log exceptions with context; prefer enrichment via LogContext
- Replace any Microsoft.Extensions.Logging usage with Serilog

10) Non-MVP integrations (keep disabled until scheduled)
- XAI and Google Earth Engine stay deferred
- If compilation breaks due to these, disable by renaming file to .disabled and document the decision
- After disabling, run bbBuild and bbMvpCheck to confirm stability

11) Minimal change workflow (per PR or local change)
- Before: bbRefresh; bbHealth
- Validate: bbAntiRegression; bbXamlValidate
- Build/Test: bbBuild; bbTest
- MVP assurance: bbMvpCheck
- Only then propose edits; include doc links and ensure no regressions

12) File hygiene and formatting
- Remove trailing whitespace; ensure single final newline
- Keep lines ≲120 chars where practical
- Clean temp files (*.new, *.bak, *.old, *_tmp) and build artifacts (bin/, obj/)
- Use CRLF on Windows as per repo settings

13) Cross-references
- Project README.md — setup, run instructions, and architecture overview
- vscode-userdata/BusBuddy.instructions.md — domain specifics and preferences
- Directory.Build.props — centralized versions and analyzer settings
- NuGet.config — sources (nuget.org, syncfusion)
- Global.json — SDK pin (9.0.303)

Appendix: Quick checks
- If bb* aliases are missing: run bbRefresh or execute PowerShell/Profiles/Import-BusBuddyModule.ps1
- If XAML fails on Syncfusion types: check xmlns:syncfusion="http://schemas.syncfusion.com/wpf" and package references; validate license registration early in App
- If PS analyzer flags ShouldProcess: add SupportsShouldProcess and guard with $PSCmdlet.ShouldProcess()

Reminder: Be concise, preserve working Syncfusion implementations, and never regress to standard WPF controls. Always run bbAntiRegression and bbXamlValidate before suggesting changes.
### **External Service Integrations**
- **Google Earth Engine**: Project-based authentication with service account keys
- **Azure Services**: Environment-based credential management
- **Syncfusion Licensing**: Environment variable `${SYNCFUSION_LICENSE_KEY}`

### **Build Configuration Standards**
- **Nullable Reference Types**: Enabled throughout solution
- **Implicit Usings**: Enabled for common namespace imports
- **Documentation Generation**: XML documentation files for all public APIs
- **Code Analysis**: .NET analyzers with practical ruleset for MVP development
- **Warning Treatment**: Warnings allowed during MVP, errors enforced in production

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
 **Target Framework**: .NET 9.0-windows (WPF projects)
    // Data operation
    var data = await context.Drivers.ToListAsync();
    return data;
 **Syncfusion WPF**: 30.2.5 (per Directory.Build.props)
 **Entity Framework Core**: 9.0.8 (.NET 9 compatible)
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
bb-dev-session    # Start a full dev session (if available in module)
bbRefresh         # Reinitialize aliases/module for current session
Get-BusBuddyAliasStatus # Safe alias status under StrictMode
Suspend-BusBuddyCleanup # Pause cleanup (sets Env:BB_CLEANUP_PAUSED=1)
Resume-BusBuddyCleanup  # Resume cleanup (clears Env:BB_CLEANUP_PAUSED)
- When adding new comments, use em dashes (`—`) instead of double dashes

- **[Project].Tests**: Comprehensive test suite for all layers
- **Controls/**: Custom user controls and control templates
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

- **Extensions/**: Core extension methods
- **Interceptors/**: EF interceptors and data access enhancements
- **Migrations/**: Entity Framework migrations
- **Models/**: Domain models and entities
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

### **Phase 1 Architecture (Minimum Viable)**
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

- Framework: NUnit (use repo-pinned version, e.g., 4.3.1). Docs: https://nunit.org/ and https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-nunit
- Commands:
  - bbTest — runs tests (aliases to Invoke-BusBuddyTest/Start-BusBuddyTest; hyphen alias bb-test remains for compatibility)
  - bbTestWatch — watch mode (dotnet watch test)
  - bbTestReport — generates reports in Documentation/Reports/
- Filters (examples):
  - Category filters: --filter "TestCategory=Scheduler" or --filter "Category=Scheduler"
  - Suite filters (custom NUnit property): --filter "TestSuite=Unit" or "TestSuite=Integration|TestSuite=Core|TestSuite=WPF"
- Outputs:
  - TRX: TestResults/*.trx via --logger "trx;LogFileName=BusBuddy.Tests.trx"
  - Coverage: --collect:"XPlat Code Coverage" (use coverlet data for CI reports)
  - Coverage status: 75%+ achieved, 85% target
  - Reports: Documentation/Reports/ (HTML/markdown summaries via bbTestReport)
- Local examples:
  - Quick: bbTest --filter "TestCategory=Scheduler"
  - Watch changed tests: bbTestWatch --filter "TestSuite=Unit"
  - With TRX + coverage: bbTest --logger "trx;LogFileName=BusBuddy.Tests.trx" --collect:"XPlat Code Coverage"
- CI examples (GitHub Actions):
  - dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj --configuration Release --logger "trx;LogFileName=ci.trx" --collect:"XPlat Code Coverage" --filter "TestSuite=Core|TestSuite=Integration"
- Self-contained tests:
  - Use EF Core InMemory provider for data tests. Docs: https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy and https://learn.microsoft.com/ef/core/testing/in-memory
  - Prefer mocks for services (e.g., Moq) and isolated test setup/teardown.
- NUnit categorization:
  - [Category("Scheduler")] and/or [Property("TestSuite","Unit")] on tests to support filters.
- Anti-regression gates before proposing changes:
  - Run bbTest and bbXamlValidate; fix failures first.
- Legacy:
  - Run-Phase4-NUnitTests-Modular.ps1 is archived; use bbTest/bbTestWatch/bbTestReport instead.

```csharp
// Example: EF Core InMemory context (docs: EF Core testing with InMemory)
[TestFixture, Category("Scheduler"), Property("TestSuite","Unit")]
public class SchedulerServiceTests
{
    [Test]
    public async Task GeneratesDailyRoutePlan()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        // ...seed entities...

        var svc = new SchedulerService(db /*, mocks... */);
        var plan = await svc.GenerateDailyPlanAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Routes, Is.Not.Empty);
    }
}
```

## CI Workflows — GitHub Actions (ci.yml) [Updated]

- Triggers:
  - push and pull_request on branches: main, develop
  - workflow_dispatch for manual runs
- Secrets required: SYNCFUSION_LICENSE_KEY, AZURE_SQL_USER, AZURE_SQL_PASSWORD (and related Azure secrets as needed)
- Artifacts: SchedulerTests.trx, migration-script.sql, coverage reports (XPlat Code Coverage)
- Known CI issues:
  - YAML indentation must be 2 spaces throughout
  - Ensure Azure SQL firewall cleanup runs with if: always() so resources are cleaned up even on failure

Example ci.yml (excerpt):
```yaml
name: BusBuddy CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
  workflow_dispatch:

jobs:
  scheduler-tests:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x
      - name: Restore
        run: dotnet restore
      - name: Test (Scheduler only, TRX + Coverage)
        run: >
          dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj
          --configuration Release
          --logger "trx;LogFileName=SchedulerTests.trx"
          --collect:"XPlat Code Coverage"
          --filter "TestCategory=Scheduler"
      - name: Upload TRX
        uses: actions/upload-artifact@v4
        with:
          name: SchedulerTests.trx
          path: **/TestResults/**/*.trx

  build-and-test:
    runs-on: windows-latest
    strategy:
      matrix:
        platform: [ x64, x86 ]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x
      - name: Build (matrix)
        run: >
          dotnet build BusBuddy.sln
          -p:Platform=${{ matrix.platform }}
          --configuration Release
          --verbosity detailed
      - name: Test (Core + Integration, TRX + Coverage)
        run: >
          dotnet test BusBuddy.sln
          --configuration Release
          --no-build
          --logger "trx;LogFileName=AllTests_${{ matrix.platform }}.trx"
          --collect:"XPlat Code Coverage"
          --filter "TestSuite=Core|TestSuite=Integration"
      - name: Upload test artifacts
        uses: actions/upload-artifact@v4
        with:
          name: TestResults_${{ matrix.platform }}
          path: |
            **/TestResults/**/*.trx
            **/TestResults/**/coverage.cobertura.xml

  quality-analysis:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: PowerShell 7.5.2
        shell: pwsh
        run: $PSVersionTable
      - name: ScriptAnalyzer (PSUseShouldProcess, empty catch, Write-Host)
        shell: pwsh
        run: >
          Install-Module PSScriptAnalyzer -Scope CurrentUser -Force;
          Invoke-ScriptAnalyzer -Path PowerShell -Recurse -Severity Error,Warning
          -EnableExit -Settings PSAvoidUsingWriteHost,PSUseShouldProcess,PSAvoidEmptyCatchBlock
          | Tee-Object -FilePath Documentation/Reports/PSScriptAnalyzerReport.txt
      - name: Upload analysis report
        uses: actions/upload-artifact@v4
        with:
          name: PSScriptAnalyzerReport
          path: Documentation/Reports/PSScriptAnalyzerReport.txt

  security-scan:
    permissions:
      security-events: write
      actions: read
      contents: read
    uses: github/codeql-action/.github/workflows/codeql.yml@v3

  repository-health:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: License and env wiring
        shell: pwsh
        env:
          SYNCFUSION_LICENSE_KEY: ${{ secrets.SYNCFUSION_LICENSE_KEY }}
        run: |
          if ($env:SYNCFUSION_LICENSE_KEY) {
            Write-Information "Syncfusion license key present." -InformationAction Continue
          } else {
            Write-Warning "Syncfusion license key missing."
          }
      - name: bb-health diagnostics
        shell: pwsh
        run: |
          ./PowerShell/Profiles/Import-BusBuddyModule.ps1
          bb-health
      - name: Generate migration script (example)
        shell: pwsh
        run: dotnet ef migrations script -o Documentation/Reports/migration-script.sql
      - name: Upload migration script
        uses: actions/upload-artifact@v4
        with:
          name: migration-script.sql
          path: Documentation/Reports/migration-script.sql
      - name: Azure SQL firewall cleanup
        if: always()
        shell: pwsh
        run: |
          Write-Information "Ensure Azure SQL firewall rules cleaned up." -InformationAction Continue
          # ...invoke cleanup if used earlier...
```

Notes:
- Coverage: Use --collect:"XPlat Code Coverage". Surface coverage summary in PR checks; target 85% (≥75% current).
- Post-MVP: Re-enable deferred features only after bb-anti-regression passes in CI.

## Build Standards [Updated]

- Command: bb-build (alias to dotnet build) uses --verbosity detailed by default
  - Enforce 0 errors; do not merge with build errors
  - Document and fix warnings, prioritizing nullable reference warnings until eliminated
- Local usage:
  - bb-build                         # verbose build for diagnostics
  - bb-test --collect:"XPlat Code Coverage"
  - bb-health                        # environment and project diagnostics
- CI usage:
  - dotnet build BusBuddy.sln --configuration Release --verbosity detailed
  - dotnet test ... --logger "trx;LogFileName=ci.trx" --collect:"XPlat Code Coverage"
- Troubleshooting:
  - Run bb-health first; check SDK pin (global.json), NuGet feeds, and missing env vars
  - Use bb-commands/bbRefresh if aliases missing, else dotnet --info for SDK state

## File Management and Cleanup Best Practices [Updated]

- Clean artifacts (local and CI):
  - Remove bin/, obj/, TestResults/, .vs/ from source control; keep TestResults/ outputs only as CI artifacts
  - Delete temporary and backup files: *.new, *.bak, *.backup, *.old, *_temp, *_tmp, *_test
  - Post-MVP: Remove stale .disabled files once replacements are implemented and bb-anti-regression passes
- Git hygiene:
  - Ensure .gitignore excludes build artifacts and IDE folders; do not commit generated TRX/coverage locally
  - Keep commits small and descriptive; avoid committing large binaries
- Formatting:
  - No trailing whitespace; single final newline per file
  - Windows CRLF endings as per repo settings
  - Lines ≤120 chars when practical
- CI/report locations:
  - TRX under TestResults/ (CI uploads artifacts)
  - Coverage (XPlat) exported by dotnet test; bbTestReport composes HTML/markdown in Documentation/Reports/
- Safety and diagnostics:
  - Use bb-health to diagnose environment issues
  - Run bb-test and bb-xaml-validate before proposing changes
  - Align with MVP: re-enable deferred features only after bb-anti-regression succeeds locally and in CI

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
      "args": ["-NoProfile", "-NoExit", "-Command", 
               "& 'PowerShell/Profiles/Microsoft.PowerShell_profile.ps1'"]
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

