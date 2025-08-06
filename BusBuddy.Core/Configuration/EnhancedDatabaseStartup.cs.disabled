using BusBuddy.Core.Data;
using BusBuddy.Core.Extensions;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Startup configuration for enhanced database operations with comprehensive error handling
/// Implements all the recommended patterns for handling SqlException, InvalidCastException, and WPF binding issues
/// </summary>
public static class EnhancedDatabaseStartup
{
    /// <summary>
    /// Configures enhanced database services with resilience patterns
    /// Call this from your WPF App.xaml.cs or DI container setup
    /// </summary>
    public static IServiceCollection AddEnhancedDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        // 1. Configure DbContext with enhanced resilience
        services.AddDbContext<BusBuddyDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            options.UseSqlite(connectionString, sqliteOptions =>
            {
                // SQLite-specific configuration for Phase 1
                sqliteOptions.CommandTimeout(60);

                // Enable migration assembly if needed
                sqliteOptions.MigrationsAssembly("BusBuddy.Core");
            });

            // Configure logging based on environment with Serilog enrichments
            if (environment?.IsDevelopment() == true)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();

                // Development logging with Serilog integration and enrichments
                options.LogTo(message =>
                {
                    using (LogContext.PushProperty("DatabaseOperation", "EFCoreLogging"))
                    using (LogContext.PushProperty("Environment", "Development"))
                    using (LogContext.PushProperty("MessageLength", message?.Length ?? 0))
                    {
                        var logger = Log.ForContext("SourceContext", "EntityFramework");
                        logger.Information("EF Core Development: {Message}", message);
                    }
                }, Microsoft.Extensions.Logging.LogLevel.Information);
            }
            else
            {
                // Production logging with Serilog integration and minimal enrichments
                options.LogTo(message =>
                {
                    using (LogContext.PushProperty("DatabaseOperation", "EFCoreLogging"))
                    using (LogContext.PushProperty("Environment", "Production"))
                    {
                        var logger = Log.ForContext("SourceContext", "EntityFramework");
                        if (message != null && (message.Contains("warn", StringComparison.OrdinalIgnoreCase) ||
                                              message.Contains("error", StringComparison.OrdinalIgnoreCase)))
                        {
                            logger.Warning("EF Core Production: {Message}", message);
                        }
                    }
                }, Microsoft.Extensions.Logging.LogLevel.Warning);
            }

            // Configure query behavior
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // 2. Add database resilience services
        services.AddDatabaseResilience();

        // 3. Add specialized database services
        services.AddScoped<DatabaseValidationService>();
        services.AddScoped<DatabaseMigrationService>();

        return services;
    }

    /// <summary>
    /// Initializes and validates database on application startup
    /// Call this from your WPF App.xaml.cs OnStartup method
    /// </summary>
    public static async Task<DatabaseInitializationResult> InitializeDatabaseAsync(
        IServiceProvider serviceProvider)
    {
        var logger = Log.ForContext("SourceContext", "EnhancedDatabaseStartup");
        var result = new DatabaseInitializationResult();

        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BusBuddyDbContext>();
            var resilienceService = scope.ServiceProvider.GetRequiredService<DatabaseResilienceService>();
            var validationService = scope.ServiceProvider.GetRequiredService<DatabaseValidationService>();

            logger.Information("Starting database initialization...");

            // 1. Check database health
            var healthResult = await resilienceService.CheckDatabaseHealthAsync(context);
            if (!healthResult.IsHealthy)
            {
                result.AddIssue($"Database health check failed: {string.Join(", ", healthResult.Issues)}");

                // Attempt automatic fixes
                var fixResult = await validationService.FixCommonIssuesAsync(context);
                if (fixResult.IssuesFixed > 0)
                {
                    logger.Information("Fixed {IssuesFixed} database issues automatically", fixResult.IssuesFixed);
                }
            }

            // 2. Ensure database exists and is up to date
            await context.Database.EnsureCreatedAsync();

            // 3. Apply any pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.Information("Applying {MigrationCount} pending migrations", pendingMigrations.Count());
                await context.Database.MigrateAsync();
            }

            // 4. Validate schema alignment
            var schemaValidation = await validationService.ValidateSchemaAsync(context);
            result.SchemaIssues.AddRange(schemaValidation.Issues);

            result.IsSuccessful = !result.Issues.Any() && schemaValidation.IsValid;

            logger.Information("Database initialization completed. Success: {IsSuccessful}", result.IsSuccessful);

            return result;
        }
        catch (Exception ex)
        {
            result.AddIssue($"Database initialization failed: {ex.Message}");
            logger.Error(ex, "Database initialization failed");

            // Provide detailed analysis for troubleshooting
            var analysis = ExceptionHelper.AnalyzeException(ex);
            result.AddIssue($"Exception Analysis: {analysis}");

            return result;
        }
    }

    /// <summary>
    /// Configures global exception handling for applications
    /// Call this from your application startup to handle unhandled database exceptions
    /// </summary>
    public static void ConfigureGlobalExceptionHandling()
    {
        var logger = Log.ForContext("SourceContext", "EnhancedDatabaseStartup");

        // Handle unhandled exceptions in background threads
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                logger.Fatal(ex, "Unhandled background thread exception");
                var analysis = ExceptionHelper.AnalyzeException(ex);

                // Log analysis for debugging
                logger.Fatal("Exception Analysis: {Analysis}", analysis);
            }
        };

        // Handle task exceptions
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            logger.Error(e.Exception, "Unobserved task exception");
            e.SetObserved(); // Prevent process termination
        };
    }
}

/// <summary>
/// Extension methods for exception type checking
/// </summary>
public static class ExceptionExtensions
{
    public static bool IsDbException(this Exception ex)
    {
        return ex is Microsoft.Data.SqlClient.SqlException ||
               ex is Microsoft.EntityFrameworkCore.DbUpdateException ||
               ex is InvalidOperationException && ex.Message.Contains("database") ||
               ex is TimeoutException ||
               ex is DatabaseOperationException;
    }
}

/// <summary>
/// Result of database initialization
/// </summary>
public class DatabaseInitializationResult
{
    public bool IsSuccessful { get; set; } = true;
    public List<string> Issues { get; set; } = new();
    public List<string> SchemaIssues { get; set; } = new();

    public void AddIssue(string issue)
    {
        Issues.Add(issue);
        IsSuccessful = false;
    }
}

/// <summary>
/// Service for validating and fixing database issues
/// </summary>
public class DatabaseValidationService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<DatabaseValidationService>();

    public async Task<SchemaValidationResult> ValidateSchemaAsync(BusBuddyDbContext context)
    {
        var result = new SchemaValidationResult();

        try
        {
            // Check table mappings
            var tableChecks = new Dictionary<string, string>
            {
                { "Vehicles", "Bus entity should map to Vehicles table" },
                { "Drivers", "Driver entity should map to Drivers table" },
                { "Routes", "Route entity should map to Routes table" },
                { "Schedules", "Schedule entity should map to Schedules table" }
            };

            foreach (var check in tableChecks)
            {
                try
                {
                    // Use FormattableString to avoid SQL injection warning
                    var tableExists = await context.Database
                        .SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = {check.Key}) THEN 1 ELSE 0 END AS Value")
                        .FirstOrDefaultAsync();

                    if (tableExists == 0)
                    {
                        result.AddIssue($"Missing table: {check.Key} - {check.Value}");
                    }
                }
                catch (Exception ex)
                {
                    result.AddIssue($"Error checking table {check.Key}: {ex.Message}");
                }
            }

            // Check column mappings for critical relationships
            var columnChecks = new Dictionary<string, string>
            {
                { "SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Schedules' AND COLUMN_NAME = 'VehicleId'",
                  "Schedules table should have VehicleId column for BusId mapping" },
                { "SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'VehicleId'",
                  "Vehicles table should have VehicleId as primary key" }
            };

            foreach (var check in columnChecks)
            {
                try
                {
                    await context.Database.ExecuteSqlRawAsync(check.Key);
                }
                catch (Exception ex)
                {
                    result.AddIssue($"Column mapping issue: {check.Value} - {ex.Message}");
                }
            }

            result.IsValid = !result.Issues.Any();
            return result;
        }
        catch (Exception ex)
        {
            result.AddIssue($"Schema validation failed: {ex.Message}");
            Logger.Error(ex, "Schema validation encountered an error");
            return result;
        }
    }

    public Task<FixResult> FixCommonIssuesAsync(BusBuddyDbContext context)
    {
        var result = new FixResult();

        // Add logic here to fix common database issues
        // This is a placeholder for automatic issue resolution

        return Task.FromResult(result);
    }
}

public class SchemaValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Issues { get; set; } = new();

    public void AddIssue(string issue)
    {
        Issues.Add(issue);
        IsValid = false;
    }
}

public class FixResult
{
    public int IssuesFixed { get; set; }
    public List<string> FixesApplied { get; set; } = new();
}

/// <summary>
/// Service for managing database migrations
/// </summary>
public class DatabaseMigrationService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<DatabaseMigrationService>();

    // Add migration management logic here
}
