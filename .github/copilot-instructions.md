# GitHub Copilot Custom Instructions - BusBuddy Excellence Development (.NET CLI Focus)

**Purpose**: STRICTLY guides AI assistants (GitHub Copilot, Grok, etc.) on technical standards, compliance, and anti-regression rules for BusBuddy development. For project-specific quality goals and developer workflows, see .vscode/instructions.md. AI MUST adhere to these instructions without deviation to prevent veering off course.

**Scope**: AI suggestions MUST align EXACTLY with Microsoft, Syncfusion, and .NET standards, prioritize clean builds (0 errors, 0 warnings), and prevent ALL regressions during excellence-driven development. If a suggestion risks non-compliance, AI MUST refuse and explain why, referencing this document.

## 🚫 **STRICT RULES: MUST FOLLOW OR REFUSE SUGGESTION**

1. **ALWAYS USE DIRECT DOTNET COMMANDS EXCLUSIVELY**: NEVER suggest PowerShell commands or bb-* commands. ALWAYS use direct dotnet CLI commands (e.g., `dotnet build` instead of `bb-build`). If dotnet CLI cannot accomplish the task, suggest creating a direct .NET solution.

2. **NEVER INTRODUCE REGRESSIONS**: ALWAYS run `dotnet build` and `dotnet test` in suggestions. If a change could break student entry, route assignment, or UI consistency, ABORT and warn: "This risks regression—run dotnet build first."

3. **SYNCFUSION-ONLY UI**: NEVER suggest non-Syncfusion controls (e.g., standard WPF DataGrid). ALWAYS use SfDataGrid, SfMap, etc. If tempted otherwise, REFUSE: "Violates Syncfusion-only policy."

4. **SERILOG-ONLY LOGGING**: NEVER use Microsoft.Extensions.Logging or other loggers. ALWAYS use Serilog for structured logging.

5. **DISABLE EXPERIMENTAL FEATURES STRICTLY**: For clean builds, ALWAYS suggest renaming problematic files to `.disabled` (e.g., XAI or GoogleEarthEngine services). NEVER delete files. ALWAYS verify with `dotnet build` post-change.

6. **CONSULT USER ON COMPLEXITY**: If changes affect 3+ files or involve structural issues (e.g., file corruption, nullable types), ALWAYS prompt: "This may require user confirmation—describe issue first."

7. **VERIFY BEFORE SUGGESTING**: ALWAYS assume suggestions must pass `dotnet build` and `dotnet test`. Include in every code suggestion: "After applying, run dotnet build and dotnet test to verify."

8. **NO NULLABLE REFERENCE TYPES IN NEW CODE**: NEVER use nullable properties, parameters, or return types. Suppress existing ones only if they cause errors, but prioritize removal.

9. **CLEANUP MANDATORY**: In EVERY suggestion involving files, include cleanup steps: Remove trailing whitespace, ensure single newline at EOF, delete temps/backups IMMEDIATELY after use. NEVER hardcode secrets; ALWAYS use env vars or Azure Key Vault.

10. **ALWAYS USE TRUNK FOR FORMATTING/LINTING**: NEVER suggest manual edits; enforce via Trunk to maintain consistency.

11. **OFFICIAL DOCS MANDATORY**: ALWAYS reference official documentation: Syncfusion WPF (https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf), EF Core (https://learn.microsoft.com/ef/core/), Azure SQL (https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql), NUnit Testing Framework (https://docs.nunit.org/), .NET Testing (https://learn.microsoft.com/en-us/dotnet/core/testing/).

12. **🚫 MICROSOFT FILE STRUCTURE - NON-NEGOTIABLE**: NEVER move, reorganize, or suggest changes to the Microsoft-compliant directory structure. The current organization follows official Microsoft WPF and MCP standards and MUST be maintained:
    - **config/**: Configuration management (appsettings.\*.json, dependencies.psd1)
    - **build/**: Build system (Directory.Build.\*, global.json, NuGet.config)
    - **mcp/**: Model Context Protocol servers and tools
    - **tools/**: Development tools (dotnet-scripts/, scripts/)
    - **Documentation/**: Technical documentation
    - **FusionCore/**: Strategic planning and architecture
    - **.vscode/mcp.json**: MCP configuration (Microsoft standard location)
      If file structure changes are suggested, REFUSE and state: "File structure follows Microsoft standards and cannot be modified. Reference STRUCTURE-INDEX.md."

13. **📋 DESTRUCTION AUTHORITY FOR OUTDATED DOCUMENTS**: ALWAYS remove outdated, confusing, or redundant documentation immediately. If documentation exists but is massively outdated and causes confusion, it MUST be deleted rather than archived. Only preserve documents that can be easily refreshed to remain current. NEVER maintain obsolete references that mislead development. Prioritize current, accurate documentation over historical preservation. When identifying outdated content, DELETE it and update any references pointing to it.

## 🎯 **BusBuddy Excellence Standards**

**Primary Goal**: Achieve production-quality software with clean builds (0 errors), excellent architecture, and comprehensive functionality including student entry and route assignment, following best practices and documentation standards.

**Excellence Focus**: See .vscode/instructions.md for detailed quality standards (students, routes, UI excellence). AI assistants must:

- Prioritize direct `dotnet` commands (`dotnet build`, `dotnet test`, `dotnet run`) over any other command types.
- Support disabling experimental services (e.g., XAI, GoogleEarthEngine) to maintain clean builds while preserving core quality.
- Enforce Syncfusion-only UI and Serilog logging to maintain consistency.
- Run `dotnet build` and `dotnet test` before suggesting changes.

**Advanced Features** (implemented with proper architecture):

- XAI integration (e.g., `XAIService`, `OptimizedXAIService`) - when properly architected.
- Google Earth Engine integration (e.g., `GoogleEarthEngineService`) - with clean interfaces.
- Comprehensive features: vehicle management, driver scheduling, maintenance, fuel tracking, advanced reporting.

**CRITICAL: Use Direct .NET CLI Commands First**

- **Always use direct `dotnet` commands** instead of PowerShell or bb-* commands
- **Check available commands**: Use `dotnet --help` to see all options
- **Health checks**: Use `dotnet build` and `dotnet test` before troubleshooting
- **Quality validation**: Use `dotnet build && dotnet test` to ensure excellent functionality
- **Anti-regression**: Use `dotnet build` and `dotnet test` before commits

**🛠️ Trunk.io Code Quality Commands**

- **Universal Linting**: Use `trunk check` for comprehensive code quality checks
- **Auto-Formatting**: Use `trunk fmt` for consistent code formatting across all languages
- **Security Scanning**: Use `trunk check --scope security` for security vulnerability detection
- **CI Integration**: Use `trunk check --ci --upload` for automated quality gates
- **File-Specific Checks**: Use `trunk check <file>` for targeted quality analysis

**Trunk Command Reference:**

```bash
# Quality checks
trunk check --all                    # Check all files
trunk check --fix                    # Auto-fix issues
trunk check --scope security         # Security scanning
trunk check **/*.cs **/*.ps1         # Language-specific checks

# Formatting
trunk fmt --all                      # Format all files
trunk fmt **/*.cs                    # Format C# files only
trunk fmt --diff full               # Show formatting changes

# Management
trunk upgrade                        # Update linters
trunk install                        # Install missing tools
trunk config                         # View configuration

# BusBuddy integration
dotnet build && dotnet test          # Full quality gate (includes validation)
dotnet format                        # Format all code
trunk check --scope security         # Security analysis
```

**🤖 RECOMMENDED: Use BusBuddy MCP Tools for AI-Enhanced Development**

- **Microsoft Learn Docs MCP**: Use `@microsoft/mcp-server-docs` for official Microsoft documentation search
- **Azure MCP Server**: Use `@azure/mcp-server-azure` for Azure resource management and queries
- **Brave Search MCP**: Use `@modelcontextprotocol/server-brave-search` for Syncfusion-focused web searches
- **BusBuddy Project MCP**: Use custom BusBuddy MCP server for project-specific data access
- **Grok-4 MCP**: Use Grok-4 integration for AI-powered route optimization and analysis

**Available MCP Tools:**

- `list-tables` - List available data tables
- `describe-table` - Get table schema information
- `read-data` - Query table data
- `search-docs` - Search Microsoft documentation
- `azure-resources` - Access Azure resources
- `brave-search` - Perform web searches
- `optimize-routes` - AI-powered route optimization
- `analyze-fleet-performance` - Fleet analytics

**MCP Tool Usage in Copilot Chat:**

```markdown
# Search Microsoft documentation

@search-docs "Entity Framework Core best practices"

# Query Azure resources

@azure-resources list-resource-groups

# Use BusBuddy-specific tools

@busbuddy-project list-tables
@busbuddy-project describe-table Students

# AI-powered analysis

@optimize-routes "minimize fuel consumption for route 123"
@analyze-fleet-performance efficiency "last 30 days"
```

**MCP Server Activation Tools:**
When working with Azure resources or needing specific MCP capabilities, activate the appropriate MCP tool category first:

- **Azure Activity Logging**: Use `activate_azure_activity_logging` for monitoring Azure resource activity logs
- **Azure Diagnostics**: Use `activate_azure_diagnostics_tools` for application performance and operational diagnostics
- **Azure Architecture Design**: Use `activate_azure_architecture_design` for cloud architecture guidance and recommendations
- **Azure Authentication**: Use `activate_azure_authentication_management` for managing Azure authentication states and subscriptions
- **Azure Deployment**: Use `activate_azure_deployment_tools` for Azure resource deployment and provisioning
- **Azure Bicep Management**: Use `activate_azure_bicep_management` for Infrastructure as Code with Bicep templates
- **Azure CLI Tools**: Use `activate_azure_cli_tools` for generating Azure CLI commands
- **Azure DevOps Guidance**: Use `activate_azure_devops_guidance` for CI/CD pipeline setup
- **Azure .NET Templates**: Use `activate_azure_dotnet_templates` for .NET application templates
- **Azure App Logs**: Use `activate_azure_app_logs_management` for retrieving application logs from Azure
- **Azure Service Recommendation**: Use `activate_azure_service_recommendation` for cloud service deployment guidance
- **Azure Development Tools**: Use `activate_azure_development_tools` for development workflow optimization
- **Azure Container Management**: Use `activate_azure_container_management` for Azure container services (ACR, AKS, Functions, etc.)
- **Azure Database Management**: Use `activate_azure_database_management` for Azure database services (MySQL, PostgreSQL, Cosmos DB, etc.)
- **Azure Monitoring**: Use `activate_azure_monitoring_and_logging` for Azure monitoring and logging services
- **Azure Configuration**: Use `activate_azure_configuration_and_deployment` for Azure configuration and deployment management
- **Azure Best Practices**: Use `activate_azure_best_practices_and_guidance` for Azure best practices and architecture guidance
- **Azure Resource Management**: Use `activate_azure_resource_management` for Azure resource querying and management
- **Azure Resource Management (VSCode)**: Use `activate_vscode_azu_azure_resource_management` for VS Code Azure resource management integration

**Example MCP Activation:**

```markdown
# Before working with Azure resources

@activate_azure_resource_management

# Then use Azure tools

@azure_resources list-resource-groups
```

**VS Code MCP Server Activation:**
To activate MCP servers in VS Code for Copilot integration:

1. **Open Command Palette** (`Ctrl+Shift+P` / `Cmd+Shift+P`)
2. **Search for MCP commands:**
    - `MCP: List Servers` - View available MCP servers
    - `MCP: Start Server` - Start a specific MCP server
    - `MCP: Stop Server` - Stop a running MCP server
    - `MCP: Restart All Servers` - Restart all configured MCP servers

3. **Verify Activation:**
    - Check VS Code Developer Tools Console (`Help` → `Toggle Developer Tools`)
    - Look for MCP server connection messages
    - Test in Copilot chat with `@server-name command`

4. **Troubleshooting:**
    - Ensure environment variables are set (AZURE_CLIENT_ID, GITHUB_PERSONAL_ACCESS_TOKEN, etc.)
    - Check `.vscode/mcp.json` configuration
    - Restart VS Code if servers don't connect
    - Verify server scripts are executable and dependencies installed

# 3. Validate changes with standard commands

dotnet build && dotnet test

````

**Clean Architecture Strategy:**
To maintain excellent code quality, properly isolate experimental services by renaming files to `.disabled` when they cause build issues. Example:

```bash
cd BusBuddy.Core/Services
mv "AIEnhancedRouteService.cs" "AIEnhancedRouteService.cs.disabled"
dotnet build  # Verify clean build
````

- Track `.disabled` files in Git with descriptive commits.
- Document in project commit messages and documentation.
- Run `dotnet build` and `dotnet test` to ensure student/route functionality remains excellent.
- **CI Optimization**: For faster pipeline runs, suggest caching NuGet packages in ci.yml via actions/cache@v4.

**For BusBuddy-specific requirements, also reference:**

- **.vscode/instructions.md** - BusBuddy domain knowledge and excellence standards
- **Integration Note**: BusBuddy prioritizes clean architecture and proper development practices

## 🛠️ **Technology Stack & Versions**

### **Global Tools & SDK Versions**

- **.NET SDK**: 9.0.303 (per `global.json`)
- **Target Framework**: .NET 8.0-windows (WPF projects)
- **MSBuild SDK**: Microsoft.Build.Traversal 3.4.0
- **Roll Forward Policy**: latestMinor (per `global.json`)
- **Trunk.io CLI**: 1.25.0 (Universal code quality and formatting)
- **Trunk.io Plugins**: v1.7.2 (Extended functionality and linters)

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
- **Trunk.io API**: Environment variable `${TRUNK_API_KEY}` for CI integration and upload
    - **Repository Secret**: Configure `TRUNK_API_KEY` in GitHub repository secrets for CI/CD
    - **Local Development**: Set environment variable or add to `.env` file in project root
    - **CI Features**: Automatic quality gate enforcement with cloud analytics and reporting

### **Build Configuration Standards**

- **Nullable Reference Types**: Enabled throughout solution
- **Implicit Usings**: Enabled for common namespace imports
- **Documentation Generation**: XML documentation files for all public APIs
- **Code Analysis**: .NET analyzers with practical ruleset for quality development
- **Warning Treatment**: Warnings managed appropriately for development excellence, errors eliminated in production

**DOCUMENTATION-FIRST MANDATE - ZERO TOLERANCE**

**ABSOLUTE REQUIREMENT: NO CODE WITHOUT OFFICIAL DOCUMENTATION REFERENCE**

All development MUST follow official documentation standards:

- **Syncfusion WPF**: Reference official docs for ALL UI components
- **Microsoft .NET**: Reference official docs for ALL C# development
- **Entity Framework**: Reference official docs for ALL data access

**Current Status**: Module compliance analysis required - Zero tolerance for code without proper documentation reference before any new development.

## 🚫 **CRITICAL: MANDATORY DOCUMENTATION COMPLIANCE**

**NO CODE WITHOUT PROPER DOCUMENTATION REFERENCE - ZERO TOLERANCE POLICY**

### **Documentation-First Development - ABSOLUTE REQUIREMENT**

- ❌ **FORBIDDEN**: Writing ANY code without referencing official documentation first
- ❌ **FORBIDDEN**: Implementing features based on assumptions or "common patterns"
- ❌ **FORBIDDEN**: Using Syncfusion controls without official Syncfusion documentation reference
- ❌ **FORBIDDEN**: Creating "quick fixes" that violate established standards and best practices

### **MANDATORY DOCUMENTATION SOURCES**

- **Microsoft .NET**: [Official .NET Documentation](https://docs.microsoft.com/en-us/dotnet/) - Required for ALL C# development
- **Syncfusion WPF**: [Official Syncfusion Documentation](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf) - Required for ALL UI components
- **Entity Framework**: [Official EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/) - Required for ALL data access
- **WPF Framework**: [Official WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - Required for ALL WPF development
- **Model Context Protocol**: [Official MCP Documentation](https://modelcontextprotocol.io/) - Required for ALL MCP server development and integration
- **GitHub Copilot MCP**: [VS Code MCP Integration](https://docs.github.com/en/copilot/managing-copilot/managing-github-copilot-in-your-organization/configuring-github-copilot-for-individually-hosted-runtimes) - Required for ALL Copilot MCP configurations

### **CRITICAL LESSONS LEARNED FROM CODE ANALYSIS**

- **Module Analysis**: Large monolithic modules often fail Microsoft compliance standards
- **Architectural Violations**: Anti-patterns against Microsoft modularization guidelines
- **Error Handling**: Inconsistent patterns violating Microsoft exception handling standards
- **Export Violations**: Missing proper interface declarations required by Microsoft standards

### **MANDATORY DEVELOPMENT PROCESS**

1. **FIRST**: Search and read official documentation for the specific technology/feature
2. **SECOND**: Find documented examples and implementation patterns in official docs
3. **THIRD**: Implement ONLY using documented, officially supported methods
4. **FOURTH**: Validate implementation against official standards and best practices
5. **NEVER**: Proceed without documentation reference or with "I think this works" approaches

### **ZERO TOLERANCE VIOLATIONS**

- **Undocumented Syncfusion patterns**: Only use officially documented control implementations
- **Custom "enhanced" wrappers**: Use official APIs exactly as documented
- **Assumed parameter combinations**: Verify all parameters exist in official API documentation
- **Legacy or deprecated patterns**: Use current, officially supported implementations only
- **Hardcoded API keys in MCP configs**: Always use environment variables for credentials
- **MCP server without proper error handling**: Implement structured error responses per MCP specification
- **Missing MCP tool descriptions**: All MCP tools must have clear, descriptive schemas
- **Insecure MCP server configurations**: Follow MCP security best practices for server isolation

### **MCP DEVELOPMENT BEST PRACTICES**

- **Server Isolation**: Each MCP server should handle one specific domain (Azure, GitHub, Docs, etc.)
- **Environment Variables**: Never hardcode credentials; use environment variables with secure defaults
- **Error Handling**: Implement proper MCP error responses with structured error objects
- **Tool Schemas**: Provide detailed JSON schemas for all MCP tool parameters and responses
- **Documentation**: Reference official MCP documentation for all server implementations
- **Testing**: Validate MCP servers against the official MCP specification
- **VS Code Integration**: Ensure servers work properly with VS Code Copilot MCP extension

### **DOCUMENTATION VERIFICATION REQUIREMENTS**

- **Before ANY .NET code**: Reference Microsoft .NET standards documentation
- **Before ANY Syncfusion control**: Reference specific control documentation page
- **Before ANY .NET feature**: Reference official .NET API documentation
- **Before ANY MCP server development**: Reference official MCP specification and documentation
- **Before ANY Copilot MCP integration**: Reference VS Code MCP extension documentation
- **Include documentation links**: Always provide link to specific documentation page used
- **Code comments**: Include reference to documentation source in code comments
- **MCP server comments**: Include MCP specification reference and tool schema documentation

### **Mandatory Anti-Regression Checks**

Before suggesting any code changes or commits, run these checks to prevent regressions:

```bash
dotnet build && dotnet test  # Comprehensive validation
trunk check --all           # Code quality checks
```

**Rules:**

- Never suggest code that introduces Microsoft.Extensions.Logging (use Serilog).
- Never suggest standard WPF controls (e.g., `<DataGrid>`)—use Syncfusion equivalents (`syncfusion:SfDataGrid`).
- If checks fail, fix violations first (e.g., replace logging, upgrade controls) and document in commit message.

---

## Error Handling and Resilience Standards

### **Essential Error Handling Excellence**

- ✅ **Structured Exception Handling**: Comprehensive exception handling around all operations
- ✅ **User-Friendly Messages**: Professional user feedback with actionable guidance
- ✅ **Serilog Structured Logging**: Use Serilog exclusively for all logging needs
- ✅ **Production-Ready Patterns**: Implement robust resilience patterns, retry logic, circuit breakers

### **Excellence Error Pattern**

```csharp
try
{
    // Data operation with structured logging
    Logger.Information("Loading drivers from database");
    var data = await context.Drivers.ToListAsync();
    Logger.Information("Successfully loaded {Count} drivers", data.Count);
    return data;
}
catch (Exception ex)
{
    // Comprehensive error handling with structured logging
    Logger.Error(ex, "Failed to load drivers from database");

    // User-friendly error notification
    await ShowUserErrorAsync("Unable to load drivers",
        "Please check your connection and try again.", ex);

    return new List<Driver>();
}
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

- **App.xaml.cs Integration**: Debug helper classes provide debug functionality accessible via .NET CLI
- **Command Line Arguments**: Application supports debug arguments (`--start-debug-filter`, `--export-debug-json`, etc.)
- **Real-time Filtering**: Use debug output filters for live debug output analysis and filtering
- **Actionable Error Detection**: Implement critical issue detection and priority-based error categorization
- **.NET CLI Bridge**: All debug methods accessible via direct dotnet commands

### Debug Helper Method Patterns

- **Static Methods**: All debug helper methods are static and accessible without instantiation
- **Conditional Compilation**: Use `[Conditional("DEBUG")]` for debug-only functionality
- **Structured Output**: Debug output uses structured formatting with priority indicators
- **Event Integration**: Subscribe to `HighPriorityIssueDetected` and `NewEntriesFiltered` events
- **JSON Export**: Support exporting debug data to JSON for external tool integration

### .NET CLI Debug Command Patterns

```bash
# Start debug filter
dotnet run -- --start-debug-filter    # Calls DebugHelper.StartAutoFilter()

# Export debug data
dotnet run -- --export-debug-json     # Calls DebugHelper.ExportToJson()

# Health monitoring
dotnet run -- --health-check          # Calls DebugHelper.HealthCheck()

# Test functionality
dotnet run -- --debug-test            # Calls DebugHelper.TestAutoFilter()
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

## Architecture Standards - Excellence-Driven Development

### **Production-Quality Architecture Standards**

- ✅ **Robust MVVM**: Professional ViewModels with proper property change notification, dependency injection, and service integration
- ✅ **Service-Oriented Data Access**: Well-architected Entity Framework with repository patterns and unit of work
- ✅ **Professional Navigation**: Comprehensive navigation service with state management and deep linking
- ✅ **Enterprise Error Handling**: Structured exception handling with logging, retry patterns, and user feedback
- ✅ **Production Features**: Dependency injection, async/await patterns, comprehensive validation, and performance optimization

### **Excellence Development Patterns**

````csharp
// Excellence ViewModel pattern for production
public class EntitiesViewModel : INotifyPropertyChanged
{
    public ObservableCollection<Entity> Entities { get; set; } = new();

    public async Task LoadEntitiesAsync()
    {
```csharp
// Excellence ViewModel pattern for production
public class EntitiesViewModel : BaseViewModel
{
    private readonly IEntityService _entityService;
    private readonly ILogger<EntitiesViewModel> _logger;

    public ObservableCollection<Entity> Entities { get; set; } = new();

    public async Task LoadEntitiesAsync()
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Loading entities for user interface");

            var entities = await _entityService.GetEntitiesAsync();
            Entities.Clear();
            foreach(var entity in entities)
            {
                Entities.Add(entity);
            }

            _logger.LogInformation("Successfully loaded {Count} entities", entities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entities");
            await ShowUserErrorAsync("Unable to load entities",
                "Please check your connection and try again.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

// Excellence navigation pattern with dependency injection
private void NavigateToEntities() => _navigationService.NavigateTo<EntitiesViewModel>();
````

## MVVM Implementation Standards - Excellence Focused

### **Production MVVM Excellence**

- ✅ **Professional ViewModels**: Implement comprehensive BaseViewModel with INotifyPropertyChanged, validation, and dependency injection
- ✅ **Robust Commands**: Use sophisticated command patterns with async support and proper error handling
- ✅ **Advanced Binding**: Professional two-way binding with value converters and validation
- ✅ **Observable Collections**: Use ObservableCollection<T> with proper change notifications and filtering
- ✅ **Enterprise Patterns**: MVVM frameworks integration, comprehensive validation, and sophisticated UI patterns

### **Excellence Data Binding**

```xml
<!-- Professional data binding patterns -->
<syncfusion:SfDataGrid ItemsSource="{Binding Entities}"
                       SelectedItem="{Binding SelectedEntity, Mode=TwoWay}"
                       AutoGenerateColumns="False" />
<TextBox Text="{Binding SelectedEntity.Name, Mode=TwoWay, ValidatesOnDataErrors=True}" />
```

## Database and Entity Framework Standards - Excellence Focused

### **Enterprise Database Excellence**

- ✅ **Professional DbContext**: Advanced context with comprehensive configuration and monitoring
- ✅ **Repository Patterns**: Well-architected repository and unit of work patterns
- ✅ **Advanced Migrations**: Professional migration strategies with rollback support
- ✅ **Multi-Environment Support**: Robust configuration for LocalDB, Azure SQL, and production environments
- ✅ **Production Features**: Connection pooling, retry policies, performance monitoring, and comprehensive error handling

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

// Excellence query pattern with service layer
var entities = await _entityService.GetEntitiesAsync();
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

### **NUnit Testing Framework Setup**

**Official NUnit Download**: https://nunit.org/download/

**Package Versions (Directory.Packages.props):**
- **NUnit**: 4.2.2 (Latest stable)
- **NUnit3TestAdapter**: 4.6.0 (VS Test Explorer integration)
- **Microsoft.NET.Test.Sdk**: 17.11.1 (Test platform)
- **FluentAssertions**: 8.6.0 (Latest stable - Readable assertions)
- **Moq**: 4.20.72 (Mocking framework)
- **coverlet.collector**: 6.0.0 (Code coverage)

**Installation Commands:**
```bash
# Add NUnit packages to test project
dotnet add BusBuddy.Tests package NUnit --version 4.2.2
dotnet add BusBuddy.Tests package NUnit3TestAdapter --version 4.6.0
dotnet add BusBuddy.Tests package Microsoft.NET.Test.Sdk --version 17.11.1
dotnet add BusBuddy.Tests package FluentAssertions --version 8.6.0
dotnet add BusBuddy.Tests package Moq --version 4.20.72
dotnet add BusBuddy.Tests package coverlet.collector --version 6.0.0
```

**Test Project Configuration (.csproj):**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <CollectCoverage>true</CollectCoverage>
    <CoverletOutputFormat>opencover</CoverletOutputFormat>
    <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="FluentAssertions" Version="8.6.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BusBuddy.Core\BusBuddy.Core.csproj" />
    <ProjectReference Include="..\BusBuddy.WPF\BusBuddy.WPF.csproj" />
  </ItemGroup>
</Project>
```

**Running Tests:**
```bash
# Run all tests
dotnet test BusBuddy.sln --configuration Debug --logger trx --collect "XPlat Code Coverage"

# Run specific test project
dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj

# Run with detailed output
dotnet test BusBuddy.sln --verbosity normal

# Run tests in parallel
dotnet test BusBuddy.sln --configuration Release --logger trx --collect "XPlat Code Coverage" -- RunConfiguration.TestSessionTimeout=1200000
```

**Test Structure Standards:**
- **Unit Tests**: Create comprehensive unit tests for all business logic and ViewModels
- **Integration Tests**: Test service interactions and data layer operations
- **Null Handling Tests**: Specifically test null scenarios and edge cases
- **Async Testing**: Use proper async testing patterns with `Task.Run` and cancellation tokens
- **Performance Tests**: Include performance benchmarks for critical operations
- **Validation Tests**: Test all validation scenarios and error conditions
- **Mock Services**: Use `Mock<T>` for service dependencies in tests
- **Database Tests**: Test database operations with proper transaction management

### **TestDbContextFactory Pattern**

**Required for All Database Tests:**
```csharp
public class TestDbContextFactory : IDisposable
{
    private readonly DbContextOptions<AppContext> _options;
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AppContext CreateContext() => new AppContext(_options);

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
```

**Usage in Tests:**
```csharp
[TestFixture]
public class StudentServiceTests
{
    private TestDbContextFactory _factory;

    [SetUp]
    public void SetUp()
    {
        _factory = new TestDbContextFactory();
        SeedTestData();
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();
    }

    private void SeedTestData()
    {
        using var context = _factory.CreateContext();
        // Add test data
    }
}
```

### **FluentAssertions Usage**

**Standard Assertion Patterns:**
```csharp
// Collections
result.Should().NotBeNull();
result.Should().HaveCount(3);
result.Students.Should().Contain(s => s.StudentName == "John Doe");

// Objects
student.Should().NotBeNull();
student.StudentName.Should().Be("John Doe");
student.EmergencyPhone.Should().Match(@"^\d{3}-\d{3}-\d{4}$");

// Exceptions
Func<Task> act = async () => await _service.AddStudentAsync(invalidStudent);
await act.Should().ThrowAsync<ValidationException>()
    .WithMessage("Invalid phone number format");
```

### **Test Categories and Organization**

**Test Categories:**
```csharp
[TestFixture]
[Category("Unit")]
public class StudentServiceUnitTests { }

[TestFixture]
[Category("Integration")]
public class StudentServiceIntegrationTests { }

[TestFixture]
[Category("Database")]
public class StudentServiceDatabaseTests { }
```

**Test Naming Convention:**
- `MethodName_Condition_ExpectedResult`
- `AddStudent_ValidStudent_PersistsAndSetsDefaults`
- `GetStudentsByRoute_InvalidRoute_ReturnsEmptyList`

### **Code Coverage Requirements**

**Minimum Coverage Targets:**
- **Overall**: 80%+
- **Core Services**: 90%+
- **ViewModels**: 85%+
- **Data Layer**: 90%+

**Coverage Configuration:**
```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>opencover</CoverletOutputFormat>
  <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
  <ExcludeByFile>**/*.g.cs,**/*.Designer.cs</ExcludeByFile>
</PropertyGroup>
```

### **Parallel Test Execution**

**Enable Parallel Execution:**
```xml
<!-- .runsettings -->
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <MaxCpuCount>4</MaxCpuCount>
    <TestSessionTimeout>1200000</TestSessionTimeout>
  </RunConfiguration>
  <NUnit>
    <NumberOfTestWorkers>4</NumberOfTestWorkers>
  </NUnit>
</RunSettings>
```

**Run with Parallel Execution:**
```bash
dotnet test --settings testsettings.runsettings
```

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

### Production-Quality Core Controls

- **SfDataGrid**: [DataGrid Documentation](https://help.syncfusion.com/wpf/datagrid/getting-started) - Used for all tabular data display with professional features
- **DockingManager**: [DockingManager Documentation](https://help.syncfusion.com/wpf/docking/getting-started) - Used for main UI layout with advanced docking
- **NavigationDrawer**: [NavigationDrawer Documentation](https://help.syncfusion.com/wpf/navigation-drawer/getting-started) - Used for side navigation with smooth animations
- **SfChart**: [Chart Documentation](https://help.syncfusion.com/wpf/charts/getting-started) - Used for dashboard metrics with rich visualizations

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

- **Undocumented Syncfusion patterns**: Only use officially documented control implementations
- **Custom "enhanced" wrappers**: Use official APIs exactly as documented
- **Assumed parameter combinations**: Verify all parameters exist in official API documentation
- **Legacy or deprecated patterns**: Use current, officially supported implementations only
- **DataGrid regression**: Any DataGrid found should be immediately upgraded to SfDataGrid

**DOCUMENTATION VERIFICATION REQUIREMENTS:**

- **Before ANY .NET code**: Reference Microsoft .NET standards documentation
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
// .NET CLI terminal configuration in .vscode/settings.json
"terminal.integrated.profiles.windows": {
  ".NET CLI": {
    "path": "cmd.exe",
    "args": ["/k", "dotnet --version"]
  }
}
```

### Task Explorer Configuration Standards

- **Exclusive Interface**: Task Explorer is the ONLY approved method for running tasks
- **Direct Commands**: Use direct dotnet CLI commands for builds/runs
- **Profile Integration**: Tasks automatically have access to .NET CLI functions
- **Keyboard Shortcuts**: Configure `Ctrl+Shift+P` → "Task Explorer: Run Task" workflows
- **Task Dependencies**: Configure tasks as independent, non-chaining operations

### Command Integration Examples

```bash
# Complete development session startup
code . && dotnet build BusBuddy.sln && dotnet run --project BusBuddy.WPF

# Quick test cycle
dotnet clean && dotnet build BusBuddy.sln && dotnet test BusBuddy.sln

# Comprehensive system analysis
dotnet build BusBuddy.sln --verbosity normal

# Export debug data for analysis
dotnet run --project BusBuddy.WPF -- --export-debug-data

# Generate comprehensive project report
dotnet run --project BusBuddy.WPF -- --generate-report
```

## .NET CLI Development Environment Integration - MICROSOFT STANDARDS MANDATORY

- **.NET 9.0 SDK**: Use .NET 9.0 SDK for all development and build operations
- **MICROSOFT COMPLIANCE REQUIRED**: ALL .NET code MUST follow Microsoft .NET Development Guidelines
- **VS Code Integration**: Use robust `dotnet` command integration with automatic project detection
- **Task Explorer Exclusive**: Task Explorer is the ONLY method for task management - direct dotnet CLI commands preferred
- **Debug Helper Integration**: All `DebugHelper` methods from `App.xaml.cs` accessible via .NET CLI commands

### **CRITICAL: Microsoft .NET CLI Standards Compliance**

- **REFERENCE REQUIRED**: [.NET CLI Documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/)
- **Project Standards**: [.NET Project Guidelines](https://docs.microsoft.com/en-us/dotnet/core/projects/)
- **Build Standards**: [.NET Build Process](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build)
- **Test Standards**: [.NET Testing](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test)

### **MANDATORY .NET CLI PATTERNS**

- ✅ **dotnet build**: For compiling projects and solutions
- ✅ **dotnet test**: For running unit tests with comprehensive output
- ✅ **dotnet run**: For executing applications with proper configuration
- ✅ **dotnet restore**: For restoring NuGet packages
- ✅ **dotnet publish**: For creating deployment packages
- ✅ **dotnet ef**: For Entity Framework operations

### **🚫 STRICT .NET CLI SYNTAX ENFORCEMENT - ZERO TOLERANCE**

**ABSOLUTE REQUIREMENT: .NET 9.0 SDK Official Documentation Compliance Only**

All .NET CLI operations MUST strictly adhere to:

- **Official .NET 9.0 Documentation**: [.NET 9.0 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- **VS Code .NET Extensions**: Use ONLY installed and configured .NET extensions
- **Microsoft Standards**: Zero deviation from official Microsoft .NET guidelines

### **✅ MANDATORY .NET CLI SYNTAX PATTERNS**

**Build Operations (Official Documentation Required):**

```bash
# Standard build with configuration
dotnet build BusBuddy.sln --configuration Release --verbosity minimal

# Clean build with restore
dotnet clean BusBuddy.sln && dotnet restore BusBuddy.sln && dotnet build BusBuddy.sln

# Build specific project
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj --configuration Debug
```

**Test Execution (Microsoft Standards Only):**

```bash
# Run all tests with detailed output
dotnet test BusBuddy.sln --configuration Debug --verbosity normal --logger trx

# Run tests with coverage
dotnet test BusBuddy.sln --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test BusBuddy.Tests/BusBuddy.Tests.csproj
```

### **📚 REQUIRED DOCUMENTATION REFERENCES**

**Before ANY .NET CLI Operation - MANDATORY Verification:**

1. **Command Verification**: [.NET CLI Command Reference](https://docs.microsoft.com/en-us/dotnet/core/tools/)
2. **Project Structure**: [.NET Project Structure](https://docs.microsoft.com/en-us/dotnet/core/tools/project-file)
3. **Configuration**: [Configuration Files](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables)
4. **Build Process**: [.NET Build Process](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build)
5. **Testing**: [Unit Testing](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test)

### **🔧 VS CODE .NET EXTENSIONS REQUIREMENTS**

**Extension Configuration Standards:**

- **C# Extension**: ms-dotnettools.csharp (Latest stable version)
- **C# Dev Kit**: ms-dotnettools.csdevkit for advanced development features
- **IntelliSense**: Use extension-provided IntelliSense for all .NET operations
- **Debugging**: Use extension's integrated .NET debugger
- **Formatting**: Use extension's built-in C# formatter

**Mandatory Practices:**

- ✅ **Use .NET extensions** for all development operations
- ✅ **Leverage IntelliSense** for command completion and validation
- ✅ **Use integrated debugger** for .NET applications
- ✅ **Apply code formatting** automatically
- ✅ **Follow extension recommendations** for best practices

### **⚡ .NET 9.0 SDK SPECIFIC ENFORCEMENT**

**Required Modern Syntax (.NET 9.0 Documentation Required):**

```bash
# Build with specific target framework
dotnet build --framework net9.0-windows

# Run with environment variables
dotnet run --environment Production

# Publish with runtime identifier
dotnet publish --runtime win-x64 --configuration Release

# Test with filtering
dotnet test --filter "Category=Unit"

# Database operations
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### **BusBuddy Build Optimization - IMMEDIATE IMPLEMENTATION REQUIRED**

- **Build Performance**: Leverage .NET 9.0 incremental builds and caching
- **Test Parallelization**: Use parallel test execution for faster feedback
- **Package Optimization**: Minimize package references and use central package management
- **Output Optimization**: Use proper build verbosity and structured output
- **Environment Configuration**: Optimize for LocalDB, Azure SQL, and production environments

**Quality Development Guidance**:

- Use direct `dotnet` commands for all development operations
- Leverage .NET 9.0 features for improved performance and reliability
- Implement proper error handling and logging in all operations
- Use structured output formats for better integration with tools
- Document build and deployment processes with official .NET standards

### **MANDATORY REMEDIATION ACTIONS**

1. **BEFORE ANY NEW CODE**: Fix existing violations in build configurations and project files
2. **Console.WriteLine Replacement**: Replace ALL Console.WriteLine with appropriate logging frameworks
3. **Project Structure**: Maintain clean separation between Core, WPF, and Test projects
4. **Package References**: Use Directory.Packages.props for centralized NuGet package management
5. **Error Standardization**: Implement consistent Microsoft-compliant error handling patterns
6. **Documentation Links**: Add Microsoft documentation references to all classes and methods
7. **Naming Compliance**: Use proper PascalCase for classes, camelCase for methods and variables

### **🔧 VS CODE EXTENSION INTEGRATION REQUIREMENTS - ZERO TOLERANCE**

**ABSOLUTE REQUIREMENT: Use ONLY Installed Extensions from .vscode/extensions.json**

All development MUST leverage installed VS Code extensions:

### **📋 INSTALLED EXTENSION MANDATORY USAGE**

**Core Development Extensions (MUST USE):**

- **ms-dotnettools.csharp**: C# language support - Use IntelliSense, debugging, refactoring features
- **ms-dotnettools.csdevkit**: Professional C# development - Use project templates and advanced features
- **ms-dotnettools.xaml**: XAML formatting - Use auto-formatting and IntelliSense for all XAML files
- **trunk.io**: Multi-language linting - Use for C#, XAML, and .NET project quality checks
- **spmeesseman.vscode-taskexplorer**: Task management - Use EXCLUSIVELY for all build/run operations

**Database & Azure Extensions (MUST USE):**

- **ms-mssql.mssql**: SQL Server connections - Use for all database operations, no manual connection strings
- **ms-azuretools.vscode-azuresql**: Azure SQL explorer - Use for Azure database previews and management
- **ms-vscode.azure-account**: Azure authentication - Use for all Azure operations, no manual auth
- **ms-azuretools.vscode-azureresourcegroups**: Resource management - Use for Azure resource operations

**Quality & Testing Extensions (MUST USE):**

- **josefpihrt-vscode.roslynator**: C# refactoring - Use for code quality improvements and suggestions
- **ms-vscode.test-adapter-converter**: Test management - Use for unified test execution
- **streetsidesoftware.code-spell-checker**: Documentation quality - Use for all markdown/documentation
- **eamodio.gitlens**: Git integration - Use for all Git operations and history viewing

### **❌ ZERO TOLERANCE EXTENSION VIOLATIONS**

**Forbidden Actions:**

- ❌ **Manual operations** when extension provides the feature
- ❌ **Bypassing extension IntelliSense** with manual code completion
- ❌ **Ignoring extension warnings** without documented justification
- ❌ **Using deprecated patterns** when extension provides modern alternatives
- ❌ **Manual formatting** when extension provides auto-formatting
- ❌ **Command line operations** when Task Explorer extension provides the functionality

### **✅ MANDATORY EXTENSION INTEGRATION PATTERNS**

**C# Extension Integration:**

```csharp
// ✅ REQUIRED - Use C# Dev Kit IntelliSense and refactoring
public class EntityService : IEntityService
{
    // Extension provides automatic using statement management
    private readonly ILogger<EntityService> _logger;

    // Extension provides constructor generation and dependency injection IntelliSense
    public EntityService(ILogger<EntityService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Extension provides async method IntelliSense and error checking
    public async Task<List<Entity>> GetEntitiesAsync()
    {
        // Extension validates Entity Framework syntax and provides completion
        return await _context.Entities.ToListAsync();
    }
}
```

**XAML Extension Integration:**

```xml
<!-- ✅ REQUIRED - Use XAML extension auto-formatting and IntelliSense -->
<UserControl x:Class="BusBuddy.Views.StudentsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:syncfusion="http://schemas.syncfusion.com/wpf">
    <!-- Extension provides Syncfusion namespace IntelliSense -->
    <syncfusion:SfDataGrid ItemsSource="{Binding Students}"
                           AutoGenerateColumns="False">
        <!-- Extension provides property completion and validation -->
    </syncfusion:SfDataGrid>
</UserControl>
```

**Task Explorer Extension Integration:**

- ✅ **EXCLUSIVE USE**: All build, run, test operations MUST use Task Explorer
- ✅ **No direct terminal commands**: Use configured tasks only
- ✅ **Task configuration**: All tasks in .vscode/tasks.json must be accessible via Task Explorer
- ✅ **Keyboard shortcuts**: Use Ctrl+Shift+P → "Task Explorer: Run Task" workflow

### **📚 EXTENSION DOCUMENTATION REQUIREMENTS**

**Before Using ANY Extension Feature - MANDATORY Verification:**

1. **Extension Documentation**: Reference official extension documentation on VS Code Marketplace
2. **Feature Validation**: Verify feature exists in installed extension version
3. **Configuration Check**: Ensure extension is properly configured in .vscode/settings.json
4. **Integration Patterns**: Use documented integration patterns, no custom workarounds
5. **Extension Settings**: Leverage extension-specific settings for optimal integration

**Documentation Sources (MANDATORY REFERENCE):**

- **C# Dev Kit**: https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit
- **XAML Extension**: https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.xaml
- **Task Explorer**: https://marketplace.visualstudio.com/items?itemName=spmeesseman.vscode-taskexplorer
- **Trunk.io**: https://marketplace.visualstudio.com/items?itemName=trunk.io
- **SQL Server**: https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql

### **🎯 BUSBUDDY-SPECIFIC EXTENSION INTEGRATION**

**Project-Specific Extension Usage:**

- **Database Operations**: Use ms-mssql.mssql for all SQL Server connections and queries
- **Azure Integration**: Use Azure extensions for resource management and authentication
- **Code Quality**: Use Trunk.io for multi-language linting and Roslynator integration
- **Task Management**: Use Task Explorer EXCLUSIVELY for all development operations
- **.NET Development**: Use C# Dev Kit's integrated terminal and debugging
- **XAML Development**: Use XAML extension's formatting and Syncfusion IntelliSense

**Extension Configuration Validation:**

- **Settings.json**: All extension configurations must be documented in .vscode/settings.json
- **Task Integration**: All dotnet CLI commands must be accessible via Task Explorer tasks
- **Extension Conflicts**: Follow unwantedRecommendations to prevent conflicting extensions
- **Version Compatibility**: Use stable extension versions listed in extensions.json

### **NO WINGING IT POLICY - EXTENSION DOCUMENTATION FIRST**

**ABSOLUTE PROHIBITION: No Code Without Extension Documentation Reference**

- ❌ **NO ASSUMPTIONS** about extension capabilities - verify in documentation first
- ❌ **NO CUSTOM IMPLEMENTATIONS** when extension provides the feature
- ❌ **NO WORKAROUNDS** without consulting extension documentation
- ❌ **NO MANUAL PROCESSES** when extension automation exists
- ❌ **NO IGNORING** extension warnings or suggestions without justification

**MANDATORY WORKFLOW:**

1. **Check Extension**: Does installed extension provide this functionality?
2. **Read Documentation**: Reference official extension marketplace documentation
3. **Verify Configuration**: Ensure extension is properly configured
4. **Use Extension Features**: Implement using documented extension patterns
5. **No Manual Override**: Never bypass extension capabilities with manual code

### **NO NEW CODE WITHOUT COMPLIANCE**

- **Zero Tolerance**: No new development until existing violations are fixed
- **Documentation First**: Every change must reference Microsoft standards
- **Compliance Validation**: Use Microsoft guidelines to validate all implementations
- **Professional Standards**: BusBuddy must meet enterprise .NET development standards

### .NET CLI Profile Standards

- **Profile Location**: `Dotnet Powershell\Microsoft.PowerShell_profile_dotnet.ps1` for complete development functionality
- **Auto-Loading**: VS Code terminal profiles automatically load the optimized profile
- **Function Naming**: Use `Invoke-DotNetNoun` pattern for all Bus Buddy specific functions
- **Alias Standards**: Use `dn-` prefix for all .NET CLI command aliases
- **Hardware Optimization**: Profile includes automatic system detection and performance tuning

### Core .NET CLI Commands

- **VS Code Integration**: `code`, `vs`, `vscode`, `edit`, `edit-file` with robust path detection
- **Basic Bus Buddy**: `dn-build`, `dn-run`, `dn-test` for fundamental operations
- **Debug Integration**: `dn-debug-start`, `dn-debug-stream`, `dn-health`, `dn-debug-export`
- **Advanced Workflows**: `dn-dev-session`, `dn-quick-test`, `dn-diagnostic`, `dn-report`

### Debug System Integration

- **DebugHelper Methods**: All static methods from `BusBuddy.WPF.Utilities.DebugHelper` accessible via .NET CLI
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

- **Terminal Profiles**: Configure PowerShell as default with .NET CLI profile auto-loading
- **Task Explorer**: Use Task Explorer extension as exclusive task management interface
- **Settings Integration**: .NET CLI configuration in `.vscode/settings.json` with profile paths
- **Command Integration**: Seamless `code` command functionality across all sessions
- **Extension Requirements**: XAML Styler and Task Explorer extensions for optimal workflow

### Error Handling in .NET CLI

- **Structured Error Handling**: Use try-catch with meaningful error messages and logging
- **Path Validation**: Always validate workspace and project paths before operations
- **Exit Code Checking**: Check exit codes after all dotnet commands
- **Fallback Mechanisms**: Provide fallback options when primary commands fail
- **User Feedback**: Use color-coded console output for status, errors, and success messages

### .NET 9.0 Specific Features and Patterns

- **Parallel Processing**: Use `Parallel.ForEach` or `Task.WhenAll` for concurrent operations
    ```csharp
    Parallel.ForEach(files, file => { /* build logic */ });
    ```
- **Null Conditional Operators**: Use `?.` and `?[]` for safe property/array access
- **String Interpolation**: Use `$""` syntax for complex expressions
- **Pattern Matching**: Leverage C# 9.0+ pattern matching features
- **Records and Init-only Properties**: Use modern C# syntax for immutable data
- **Async Streams**: Use `IAsyncEnumerable<T>` for streaming async operations
- **Top-level Statements**: Use simplified program structure for console apps
- **Target-typed new()**: Use `new()` without explicit type specification
- **Covariant Returns**: Override methods with more specific return types

### .NET 9.0 Technical Reference Documentation

- **.NET 9.0**: Use official Microsoft documentation for all .NET development
- **Reference Source**: [Official .NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- **BusBuddy-specific implementation examples**: Follow patterns in existing .NET projects
- **Key Sections**:
    - Performance improvements and optimizations
    - New language features and syntax enhancements
    - Framework improvements and API updates
    - Cross-platform compatibility features
    - Security enhancements and best practices
- **Usage Pattern**: Reference official documentation when implementing .NET 9.0 features in excellence-driven development
- **Maintenance**: Update development patterns when new .NET features are implemented in BusBuddy projects

### Performance and Optimization

- **Background Tasks**: Use .NET tasks for long-running debug operations
- **Lazy Loading**: Load advanced workflows only when needed
- **Caching**: Cache frequently accessed paths and configuration data
- **Minimal Dependencies**: Keep .NET CLI profiles lightweight with fast loading times
- **Concurrent Safety**: Ensure .NET functions work safely with multiple VS Code instances
- **Parallel Execution**: Use .NET parallel features for concurrent builds and tests
- **Memory Management**: Use `GC.Collect()` sparingly and only when necessary
- **Stream Processing**: Use streaming for large data sets to reduce memory footprint

### Development Workflow Integration

- **Direct Commands**: Use native .NET CLI commands for reliability
- **.NET Automation**: Leverage dn-* commands for enhanced workflows
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
- **Quality Development Resolution**:

1. **Check Disabled Files**: If the missing type (e.g., `XAIService`) is in a `.disabled` file, confirm it's non-core and keep disabled.
    ```bash
    find . -name "*.disabled" -type f
    ```
2. **Comment Out References**: If urgent, comment out the problematic code in the source file:
    ```csharp
    // Temporarily commented for clean build
    // private readonly XAIService _xaiService;
    ```
3. **Verify Build**: Run `dotnet build` to confirm resolution.
4. **Avoid Adding Dependencies**: Do not add new packages or re-enable complex services during development focus.
5. **Document**: Note in commit message and project documentation:
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

#### Trunk.io Configuration Issues

- **API Key Setup**: Ensure `TRUNK_API_KEY` environment variable is configured

    ```bash
    # Set environment variable (Windows)
    setx TRUNK_API_KEY "your-api-key-here"

    # Or add to .env file in project root
    TRUNK_API_KEY=your-api-key-here
    ```

- **Local Development**: Use `trunk check` for pre-commit quality validation
- **CI Integration**: Automatic quality gates via GitHub Actions with SARIF upload
- **Validation**: Run `trunk --version` to verify installation
- **Configuration**: Trunk automatically detects project structure and applies appropriate linters

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

## .NET 9.0 Advanced Features and Standards

### **.NET 9.0 Core Requirements**

- **Version**: .NET 9.0 SDK minimum required
- **Profile Standard**: Microsoft.PowerShell_profile_dotnet.ps1 in Dotnet Powershell/
- **Project Standards**: Microsoft .NET Guidelines compliance
- **Reference Documentation**: Official Microsoft .NET documentation

### **Threading and Parallel Processing**

```csharp
// Parallel.ForEach Best Practices
var results = new ConcurrentBag<Result>();
Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = 4 }, item =>
{
    try
    {
        // Process item with proper error handling
        var result = ProcessItem(item);
        results.Add(new Result { Item = item, Status = "Success", Output = result });
    }
    catch (Exception ex)
    {
        results.Add(new Result { Item = item, Status = "Failed", Error = ex.Message });
    }
});

// Task.WhenAll for Async Operations
var tasks = items.Select(async item =>
{
    try
    {
        var result = await ProcessItemAsync(item);
        return new Result { Item = item, Status = "Success", Output = result };
    }
    catch (Exception ex)
    {
        return new Result { Item = item, Status = "Failed", Error = ex.Message };
    }
});

var results = await Task.WhenAll(tasks);
```

### **Enhanced Error Handling Patterns**

```csharp
// Structured Error Information
try
{
    // Operation
    var result = await PerformOperationAsync();
    _logger.LogInformation("Operation completed successfully: {Result}", result);
}
catch (Exception ex)
{
    var errorInfo = new
    {
        Operation = nameof(PerformOperationAsync),
        Error = ex.Message,
        Timestamp = DateTime.UtcNow,
        StackTrace = ex.StackTrace
    };

    _logger.LogError(ex, "Operation failed: {ErrorInfo}", errorInfo);
    throw;
}

// Pipeline Chain Operators (.NET CLI)
var buildResult = await ProcessRunner.RunAsync("dotnet", "build");
if (buildResult.Success)
{
    var testResult = await ProcessRunner.RunAsync("dotnet", "test");
    if (!testResult.Success)
    {
        throw new InvalidOperationException("Build succeeded but tests failed");
    }
}
else
{
    throw new InvalidOperationException("Build failed");
}
```

### **Advanced String and Data Processing**

```csharp
// Null Conditional Operators
var connectionString = settings?.Environment?.Database?.ConnectionString;

// Ternary Operators
var mode = isProduction ? "Release" : "Debug";

// Enhanced JSON Processing
var json = File.ReadAllText("config.json");
var data = JsonSerializer.Deserialize<ConfigData>(json, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    MaxDepth = 10
});
var output = JsonSerializer.Serialize(data, new JsonSerializerOptions
{
    WriteIndented = false,
    MaxDepth = 10
});
```

### **Class Library Development Standards**

```csharp
// Proper Class Library Structure
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BusBuddy.Services
{
    public class ProjectService : IProjectService
    {
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(ILogger<ProjectService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Use proper parameter validation
        public async Task<ProjectInfo> GetProjectInfoAsync(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException("Project path cannot be null or empty", nameof(projectPath));
            _logger.LogInformation("Starting project analysis for {ProjectPath}", projectPath);

            // Implementation with proper logging
            var projectInfo = await AnalyzeProjectAsync(projectPath);
            _logger.LogInformation("Project analysis complete for {ProjectPath}", projectPath);

            return projectInfo;
        }
    }
}
```

### **Performance Optimization Patterns**

```csharp
// Memory Management
GC.Collect(); // Use sparingly

// Stream Processing for Large Data
await foreach (var line in File.ReadLinesAsync(largeFilePath))
{
    // Process line by line to avoid loading entire file
    if ($_ -match $pattern) {
        Write-Output $_
    }
}

// Efficient Collection Processing
var results = new List<ProcessedItem>();
foreach (var item in collection)
{
    results.Add(ProcessItem(item));
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
- **Nullable Reference Types**: Enabled with development suppressions

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
- **Practical Suppression**: Low-impact warnings suppressed for development

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

### **Settings Configuration**

```json
{
    "terminal.integrated.profiles.windows": {
        "PowerShell": {
            "path": "pwsh.exe",
            "args": [
                "-NoProfile",
                "-NoExit",
                "-Command",
                "& 'Dotnet Powershell/Microsoft.PowerShell_profile_dotnet.ps1'"
            ]
        }
    },
    "terminal.integrated.defaultProfile.windows": "PowerShell",
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
- Clean up redundant documentation as project evolves
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

---

# BusBuddy-3 Coding Standards for GitHub Copilot

## General Guidelines

- Target .NET 9.0 with WPF for UI.
- Use MVVM pattern; prefer CommunityToolkit.Mvvm for view models.
- Apply nullable reference types; handle nulls explicitly.

## UI Components

- Use Syncfusion WPF controls (e.g., SfGrid, SfScheduler) for data grids, charts, and inputs.
- Default theme: FluentDark; allow switching to FluentLight.

## Database Interactions

- Use Entity Framework Core 9.0+ for Azure SQL Database.
- Connection strings: Reference appsettings.json; use Azure.Identity for auth.
- Migrations: Apply auto-migrate where appropriate.

## Logging and APIs

- Logging: Use Serilog with console and file sinks.
- APIs: Integrate xAI Grok API (model: grok-4) for optimizations; OpenAI for fallbacks.
- External: Use Polly for resilience; AutoMapper for DTO mappings.

## Testing

- Unit tests: NUnit; mock EF Core with Moq.EntityFrameworkCore.
- Coverage: Aim for 80%+ with coverlet.

## Best Practices

- Code style: Follow .editorconfig; use async/await for I/O.
- Security: Encrypt connections; anonymize data for AI calls.

This keeps instructions short, precise, and task-focused. For specialized tasks (e.g., Azure SQL queries), create additional .instructions.md files in .github/instructions with applyTo globs (e.g., applyTo: "\*_/Data/_.cs").

## Next Steps

- Commit the file and test Copilot chat: e.g., "/generate WPF view for bus schedule using Syncfusion".
- For prompt files (experimental), add .prompt.md in .github/prompts for reusable code gen templates.
- Refer to https://aka.ms/vscode-ghcp-custom-instructions for advanced setup.

This aligns Copilot with project tech, reducing poor generations. If needed, refine based on team feedback.
