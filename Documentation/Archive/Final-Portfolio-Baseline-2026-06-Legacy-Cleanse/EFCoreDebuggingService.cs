using System.IO;
using BusBuddy.Core.Data;
using BusBuddy.Core.Extensions;
using BusBuddy.Core.Interceptors;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BusBuddy.Core.Services;

/// <summary>
/// Comprehensive EF Core debugging service following Microsoft debugging best practices
/// Provides tools for debugging migrations, models, repositories, and SQL Server Express interactions
/// </summary>
public class EFCoreDebuggingService
{
    private static readonly ILogger Logger = Log.ForContext<EFCoreDebuggingService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly DatabaseDebuggingInterceptor? _interceptor;

    public EFCoreDebuggingService(
        IBusBuddyDbContextFactory contextFactory,
        DatabaseDebuggingInterceptor? interceptor = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _interceptor = interceptor;
    }

    /// <summary>
    /// Generate comprehensive EF Core debugging report
    /// </summary>
    public async Task<EFCoreDebugReport> GenerateFullDebugReportAsync()
    {
        var report = new EFCoreDebugReport();
        var stopwatch = Stopwatch.StartNew();

        Logger.Information("Starting comprehensive EF Core debug report generation");

        try
        {
            using var context = _contextFactory.CreateDbContext();

            // 1. Database diagnostics
            report.DatabaseDiagnostics = await context.GetDatabaseDiagnosticsAsync();

            // 2. Change tracker information
            report.ChangeTrackerInfo = context.GetChangeTrackerDebugInfo();

            // 3. Migration status
            await PopulateMigrationInfoAsync(context, report);

            // 4. Query performance if interceptor is available
            if (_interceptor != null)
            {
                report.QueryPerformanceStats = _interceptor.GetPerformanceStats();
                report.RecentQueries = _interceptor.GetRecentQueries(10);
            }

            // 5. Schema validation
            await ValidateSchemaAsync(context, report);

            // 6. Test basic operations
            await TestBasicOperationsAsync(context, report);

            stopwatch.Stop();
            report.GenerationTimeMs = stopwatch.ElapsedMilliseconds;

            Logger.Information("EF Core debug report completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            report.Errors.Add($"Error generating debug report: {ex.Message}");
            Logger.Error(ex, "Error generating EF Core debug report");
        }

        return report;
    }

    /// <summary>
    /// Debug specific migration issues
    /// </summary>
    public async Task<MigrationDebugInfo> DebugMigrationsAsync()
    {
        var debugInfo = new MigrationDebugInfo();

        try
        {
            using var context = _contextFactory.CreateDbContext();

            // Get migration information
            var applied = await context.Database.GetAppliedMigrationsAsync();
            var pending = await context.Database.GetPendingMigrationsAsync();

            debugInfo.AppliedMigrations = applied.ToList();
            debugInfo.PendingMigrations = pending.ToList();

            // Generate migration script
            debugInfo.MigrationScript = await context.GenerateDebugMigrationScriptAsync();

            // Test database creation
            var canCreate = await context.Database.EnsureCreatedAsync();
            debugInfo.CanCreateDatabase = !canCreate; // Returns false if DB already exists

            // Check for migration conflicts
            await CheckMigrationConflictsAsync(context, debugInfo);

            Logger.Information("Migration debugging completed. Applied: {Applied}, Pending: {Pending}",
                debugInfo.AppliedMigrations.Count, debugInfo.PendingMigrations.Count);
        }
        catch (Exception ex)
        {
            debugInfo.Errors.Add($"Migration debug error: {ex.Message}");
            Logger.Error(ex, "Error debugging migrations");
        }

        return debugInfo;
    }

    /// <summary>
    /// Debug specific query performance issues
    /// </summary>
    public async Task<QueryDebugInfo> DebugQueryPerformanceAsync(string entityType = "Bus")
    {
        var debugInfo = new QueryDebugInfo
        {
            EntityType = entityType,
            TestResults = new List<QueryTestResult>()
        };

        try
        {
            using var context = _contextFactory.CreateDbContext();

            // Test different query patterns
            await TestQueryPattern(context, debugInfo, "Simple Count", async () =>
                await context.Buses.CountAsync());

            await TestQueryPattern(context, debugInfo, "Where Filter", async () =>
                await context.Buses.Where(v => v.Status == "Active").CountAsync());

            await TestQueryPattern(context, debugInfo, "Include Navigation", async () =>
                await context.Buses.Include(v => v.AMRoutes).Take(5).ToListAsync());

            await TestQueryPattern(context, debugInfo, "Complex Join", async () =>
                await context.Routes
                    .Include(r => r.AMVehicle)
                    .Include(r => r.AMDriver)
                    .Where(r => r.Date >= DateTime.Today.AddDays(-7))
                    .Take(10)
                    .ToListAsync());

            await TestQueryPattern(context, debugInfo, "GroupBy Aggregation", async () =>
                await context.Routes
                    .GroupBy(r => r.RouteName)
                    .Select(g => new { RouteName = g.Key, Count = g.Count() })
                    .ToListAsync());

            Logger.Information("Query performance debugging completed for {EntityType}", entityType);
        }
        catch (Exception ex)
        {
            debugInfo.Errors.Add($"Query debug error: {ex.Message}");
            Logger.Error(ex, "Error debugging query performance for {EntityType}", entityType);
        }

        return debugInfo;
    }

    /// <summary>
    /// Export debugging information to files for analysis
    /// </summary>
    public async Task<string> ExportDebugDataAsync(string outputDirectory)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var reportPath = Path.Combine(outputDirectory, $"EFCore_Debug_Report_{timestamp}.json");

            // Generate full report
            var report = await GenerateFullDebugReportAsync();

            // Serialize to JSON with formatting
            var json = JsonSerializer.Serialize(report, JsonOptions);
            await File.WriteAllTextAsync(reportPath, json);

            // Also create a human-readable text report
            var textReportPath = Path.Combine(outputDirectory, $"EFCore_Debug_Report_{timestamp}.txt");
            await File.WriteAllTextAsync(textReportPath, FormatReportAsText(report));

            Logger.Information("Debug data exported to {ReportPath} and {TextReportPath}", reportPath, textReportPath);
            return reportPath;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting debug data to {OutputDirectory}", outputDirectory);
            throw;
        }
    }

    private async Task PopulateMigrationInfoAsync(BusBuddyDbContext context, EFCoreDebugReport report)
    {
        var applied = await context.Database.GetAppliedMigrationsAsync();
        var pending = await context.Database.GetPendingMigrationsAsync();

        report.AppliedMigrations = applied.ToList();
        report.PendingMigrations = pending.ToList();
        report.HasPendingMigrations = pending.Any();
    }

    private async Task ValidateSchemaAsync(BusBuddyDbContext context, EFCoreDebugReport report)
    {
        var validationResults = new List<string>();

        try
        {
            // Test each entity set
            var entityTypes = new (string Name, Func<Task<object>> TestFunc)[]
            {
                ("Buses", async () => await context.Buses.Take(1).ToListAsync()),
                ("Drivers", async () => await context.Drivers.Take(1).ToListAsync()),
                ("Routes", async () => await context.Routes.Take(1).ToListAsync()),
                ("Students", async () => await context.Students.Take(1).ToListAsync())
            };

            foreach (var (name, testFunc) in entityTypes)
            {
                try
                {
                    await testFunc();
                    validationResults.Add($"{name}: Schema Valid");
                }
                catch (Exception ex)
                {
                    validationResults.Add($"{name}: Schema Error - {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            validationResults.Add($"Schema validation failed: {ex.Message}");
        }

        report.SchemaValidationResults = validationResults;
    }

    private async Task TestBasicOperationsAsync(BusBuddyDbContext context, EFCoreDebugReport report)
    {
        var operationResults = new List<string>();

        try
        {
            // Test read operations
            var busCount = await context.Buses.CountAsync();
            operationResults.Add($"Bus Count: {busCount}");

            var driverCount = await context.Drivers.CountAsync();
            operationResults.Add($"Driver Count: {driverCount}");

            var routeCount = await context.Routes.CountAsync();
            operationResults.Add($"Route Count: {routeCount}");

            // Test a simple query with filtering
            var activeBusCount = await context.Buses.CountAsync(v => v.Status == "Active");
            operationResults.Add($"Active Bus Count: {activeBusCount}");
        }
        catch (Exception ex)
        {
            operationResults.Add($"Basic operations failed: {ex.Message}");
        }

        report.BasicOperationResults = operationResults;
    }

    private async Task CheckMigrationConflictsAsync(BusBuddyDbContext context, MigrationDebugInfo debugInfo)
    {
        try
        {
            // Check if there are any SQL syntax issues by using our debug script generator
            var script = await context.GenerateDebugMigrationScriptAsync();

            if (script.Contains("ERROR") || script.Contains("CONFLICT"))
            {
                debugInfo.ConflictWarnings.Add("Potential SQL conflicts detected in generated script");
            }

            // Check for common migration issues
            var pending = debugInfo.PendingMigrations;
            if (pending.Count > 10)
            {
                debugInfo.ConflictWarnings.Add($"Large number of pending migrations ({pending.Count}) - consider squashing");
            }

            if (pending.Any(m => m.Contains("DropColumn") || m.Contains("DropTable")))
            {
                debugInfo.ConflictWarnings.Add("Destructive migrations detected - review carefully before applying");
            }
        }
        catch (Exception ex)
        {
            debugInfo.ConflictWarnings.Add($"Could not check for conflicts: {ex.Message}");
        }
    }

    private async Task TestQueryPattern<T>(BusBuddyDbContext context, QueryDebugInfo debugInfo, string testName, Func<Task<T>> queryFunc)
    {
        var stopwatch = Stopwatch.StartNew();
        var beforeEntities = context.ChangeTracker.Entries().Count();

        try
        {
            var result = await queryFunc();
            stopwatch.Stop();

            var afterEntities = context.ChangeTracker.Entries().Count();
            var entitiesAdded = afterEntities - beforeEntities;

            debugInfo.TestResults.Add(new QueryTestResult
            {
                TestName = testName,
                Success = true,
                DurationMs = stopwatch.ElapsedMilliseconds,
                EntitiesTracked = entitiesAdded,
                ResultInfo = result?.ToString() ?? "null"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            debugInfo.TestResults.Add(new QueryTestResult
            {
                TestName = testName,
                Success = false,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            });
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string FormatReportAsText(EFCoreDebugReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== EF CORE DEBUG REPORT ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Generation Time: {report.GenerationTimeMs}ms");
        sb.AppendLine();

        // Database diagnostics
        if (report.DatabaseDiagnostics != null)
        {
            sb.AppendLine("=== DATABASE DIAGNOSTICS ===");
            sb.AppendLine($"Database: {report.DatabaseDiagnostics.DatabaseName}");
            sb.AppendLine($"Can Connect: {report.DatabaseDiagnostics.CanConnect}");
            sb.AppendLine($"Server Version: {report.DatabaseDiagnostics.ServerVersion}");
            sb.AppendLine($"Size: {report.DatabaseDiagnostics.DatabaseSizeMB:F2} MB");
            sb.AppendLine($"Tracked Entities: {report.DatabaseDiagnostics.TrackedEntitiesCount}");
            sb.AppendLine();
        }

        // Migrations
        sb.AppendLine("=== MIGRATIONS ===");
        sb.AppendLine($"Applied Migrations: {report.AppliedMigrations.Count}");
        sb.AppendLine($"Pending Migrations: {report.PendingMigrations.Count}");
        if (report.PendingMigrations.Any())
        {
            sb.AppendLine("Pending:");
            foreach (var migration in report.PendingMigrations)
            {
                sb.AppendLine($"  - {migration}");
            }
        }
        sb.AppendLine();

        // Query performance
        if (report.QueryPerformanceStats != null)
        {
            sb.AppendLine("=== QUERY PERFORMANCE ===");
            sb.AppendLine($"Total Queries: {report.QueryPerformanceStats.TotalQueries}");
            sb.AppendLine($"Average Duration: {report.QueryPerformanceStats.AverageDurationMs:F2}ms");
            sb.AppendLine($"Slow Queries: {report.QueryPerformanceStats.SlowQueries.Count}");
            sb.AppendLine();
        }

        // Errors
        if (report.Errors.Any())
        {
            sb.AppendLine("=== ERRORS ===");
            foreach (var error in report.Errors)
            {
                sb.AppendLine($"  - {error}");
            }
        }

        return sb.ToString();
    }
}

// Supporting classes for debugging reports
public class EFCoreDebugReport
{
    public DatabaseDiagnostics? DatabaseDiagnostics { get; set; }
    public string ChangeTrackerInfo { get; set; } = string.Empty;
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> PendingMigrations { get; set; } = new();
    public bool HasPendingMigrations { get; set; }
    public QueryPerformanceStats? QueryPerformanceStats { get; set; }
    public List<QueryExecutionInfo> RecentQueries { get; set; } = new();
    public List<string> SchemaValidationResults { get; set; } = new();
    public List<string> BasicOperationResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public long GenerationTimeMs { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class MigrationDebugInfo
{
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> PendingMigrations { get; set; } = new();
    public string MigrationScript { get; set; } = string.Empty;
    public bool CanCreateDatabase { get; set; }
    public List<string> ConflictWarnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class QueryDebugInfo
{
    public string EntityType { get; set; } = string.Empty;
    public List<QueryTestResult> TestResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class QueryTestResult
{
    public string TestName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long DurationMs { get; set; }
    public int EntitiesTracked { get; set; }
    public string? ResultInfo { get; set; }
    public string? Error { get; set; }
}
