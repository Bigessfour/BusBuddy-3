using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using System.Diagnostics;
using System.Text;

namespace BusBuddy.Core.Extensions;

/// <summary>
/// Extensions for enhanced EF Core debugging capabilities following Microsoft best practices
/// Provides comprehensive debugging tools for SQL Server Express interactions
/// </summary>
public static class EFCoreDebuggingExtensions
{
    private static readonly Serilog.ILogger Logger = Log.ForContext("SourceContext", "EFCoreDebuggingExtensions");

    /// <summary>
    /// Configure DbContext for comprehensive debugging and logging
    /// </summary>
    public static DbContextOptionsBuilder EnableComprehensiveDebugging(
        this DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        return optionsBuilder
            .EnableSensitiveDataLogging() // Show parameter values in logs
            .EnableDetailedErrors() // Include detailed error information
            .EnableServiceProviderCaching() // Cache service provider for performance
            .LogTo(message =>
            {
                Logger.Debug(message);
            }); // Removed Microsoft logging level, use Serilog logger directly where needed
    }

    /// <summary>
    /// Get detailed change tracker debug information
    /// </summary>
    public static string GetChangeTrackerDebugInfo(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var debugInfo = new StringBuilder();

        debugInfo.AppendLine("=== Change Tracker Debug Information ===");
        debugInfo.AppendLine($"Auto Detect Changes: {context.ChangeTracker.AutoDetectChangesEnabled}");
        debugInfo.AppendLine($"Lazy Loading: {context.ChangeTracker.LazyLoadingEnabled}");
        debugInfo.AppendLine($"Query Tracking Behavior: {context.ChangeTracker.QueryTrackingBehavior}");
        debugInfo.AppendLine($"Cascade Delete Timing: {context.ChangeTracker.CascadeDeleteTiming}");
        debugInfo.AppendLine($"Delete Orphan Timing: {context.ChangeTracker.DeleteOrphansTiming}");

        debugInfo.AppendLine("\n=== Tracked Entities ===");
        var entries = context.ChangeTracker.Entries().ToList();
        debugInfo.AppendLine($"Total Tracked Entities: {entries.Count}");

        var groupedEntries = entries.GroupBy(e => e.State);
        foreach (var group in groupedEntries)
        {
            debugInfo.AppendLine($"\n{group.Key} Entities: {group.Count()}");
            foreach (var entry in group.Take(5)) // Limit to first 5 for readability
            {
                debugInfo.AppendLine($"  - {entry.Entity.GetType().Name}: {GetEntityKeyInfo(entry)}");

                if (entry.State == EntityState.Modified)
                {
                    var modifiedProperties = entry.Properties
                        .Where(p => p.IsModified)
                        .Select(p => $"{p.Metadata.Name} ({p.OriginalValue} -> {p.CurrentValue})");

                    if (modifiedProperties.Any())
                    {
                        debugInfo.AppendLine($"    Modified: {string.Join(", ", modifiedProperties)}");
                    }
                }
            }
        }

        debugInfo.AppendLine("\n=== Change Tracker Full Debug View ===");
        debugInfo.AppendLine(context.ChangeTracker.DebugView.LongView);

        return debugInfo.ToString();
    }

    /// <summary>
    /// Get entity key information for debugging
    /// </summary>
    private static string GetEntityKeyInfo(EntityEntry entry)
    {
        try
        {
            var keyValues = entry.Properties
                .Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => $"{p.Metadata.Name}={p.CurrentValue}")
                .ToList();

            return keyValues.Count > 0 ? string.Join(", ", keyValues) : "No Key";
        }
        catch
        {
            return "Key Info Unavailable";
        }
    }

    /// <summary>
    /// Execute query with comprehensive performance and debugging information
    /// </summary>
    public static async Task<T> ExecuteWithDebugInfoAsync<T>(
        this DbContext context,
        Func<Task<T>> queryFunc,
        string? queryName = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(queryFunc);
        var stopwatch = Stopwatch.StartNew();
        var beforeTrackingInfo = context.ChangeTracker.Entries().Count();

        try
        {
            Logger.Debug("Starting query execution: {QueryName}", queryName ?? "Unknown");

            var result = await queryFunc();

            stopwatch.Stop();
            var afterTrackingInfo = context.ChangeTracker.Entries().Count();

            Logger.Information(
                "Query completed: {QueryName}, Duration: {Duration}ms, " +
                "Entities before: {Before}, Entities after: {After}",
                queryName ?? "Unknown",
                stopwatch.ElapsedMilliseconds,
                beforeTrackingInfo,
                afterTrackingInfo);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Logger.Error(ex,
                "Query failed: {QueryName}, Duration: {Duration}ms, Error: {Error}",
                queryName ?? "Unknown",
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw;
        }
    }

    /// <summary>
    /// Get database connection and query statistics
    /// </summary>
    public static async Task<DatabaseDiagnostics> GetDatabaseDiagnosticsAsync(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var diagnostics = new DatabaseDiagnostics();

        try
        {
            // Connection information
            var connection = context.Database.GetDbConnection();
            diagnostics.ConnectionString = connection.ConnectionString;
            diagnostics.DatabaseName = connection.Database;
            diagnostics.ServerVersion = connection.ServerVersion;

            // Test connection
            var canConnect = await context.Database.CanConnectAsync();
            diagnostics.CanConnect = canConnect;

            if (canConnect)
            {
                // Get database size and other statistics via raw SQL
                var sizeQuery = @"
                    SELECT
                        DB_NAME() as DatabaseName,
                        SUM(CAST(FILEPROPERTY(name, 'SpaceUsed') AS bigint) * 8192.) / 1024 / 1024 as SizeMB,
                        COUNT(*) as FileCount
                    FROM sys.database_files
                    WHERE type IN (0, 1)"; // Data and log files

                try
                {
                    var connection2 = context.Database.GetDbConnection();
                    await connection2.OpenAsync();

                    using var command = connection2.CreateCommand();
                    command.CommandText = sizeQuery;

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        diagnostics.DatabaseSizeMB = reader.GetDouble(1); // SizeMB column
                        diagnostics.FileCount = reader.GetInt32(2); // FileCount column
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.DatabaseSizeMB = -1; // Indicates error
                    diagnostics.Notes.Add($"Could not get database size: {ex.Message}");
                }
            }

            // Migration information
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            diagnostics.AppliedMigrations = appliedMigrations.ToList();
            diagnostics.PendingMigrations = pendingMigrations.ToList();

            // Performance counters
            diagnostics.TrackedEntitiesCount = context.ChangeTracker.Entries().Count();
            diagnostics.ModifiedEntitiesCount = context.ChangeTracker.Entries()
                .Count(e => e.State == EntityState.Modified);
            diagnostics.AddedEntitiesCount = context.ChangeTracker.Entries()
                .Count(e => e.State == EntityState.Added);
            diagnostics.DeletedEntitiesCount = context.ChangeTracker.Entries()
                .Count(e => e.State == EntityState.Deleted);
        }
        catch (Exception ex)
        {
            diagnostics.Notes.Add($"Error collecting diagnostics: {ex.Message}");
        }

        return diagnostics;
    }

    /// <summary>
    /// Clear change tracker and reset context state for debugging
    /// </summary>
    public static void ResetForDebugging(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ChangeTracker.Clear();
        context.ChangeTracker.AutoDetectChangesEnabled = true;
        context.ChangeTracker.LazyLoadingEnabled = false;
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
    }

    /// <summary>
    /// Generate SQL script for creating the database schema
    /// </summary>
    public static async Task<string> GenerateDebugMigrationScriptAsync(
        this DbContext context,
        string? fromMigration = null,
        string? toMigration = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            // For debugging purposes, generate a script that shows the current schema
            var debugScript = new StringBuilder();
            debugScript.AppendLine("-- =============================================================================");
            debugScript.AppendLine($"-- Schema Debug Script Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            debugScript.AppendLine($"-- Database Provider: {context.Database.ProviderName}");
            debugScript.AppendLine($"-- From Migration: {fromMigration ?? "Beginning"}");
            debugScript.AppendLine($"-- To Migration: {toMigration ?? "Latest"}");
            debugScript.AppendLine("-- =============================================================================");
            debugScript.AppendLine();

            // Get applied and pending migrations for context
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            debugScript.AppendLine("-- Applied Migrations:");
            foreach (var migration in appliedMigrations)
            {
                debugScript.AppendLine($"-- - {migration}");
            }

            debugScript.AppendLine();
            debugScript.AppendLine("-- Pending Migrations:");
            foreach (var migration in pendingMigrations)
            {
                debugScript.AppendLine($"-- - {migration}");
            }

            if (pendingMigrations.Any())
            {
                debugScript.AppendLine();
                debugScript.AppendLine("-- WARNING: Pending migrations detected. Run 'dotnet ef database update' to apply them.");
            }

            return debugScript.ToString();
        }
        catch (Exception ex)
        {
            return $"-- Error generating migration script: {ex.Message}";
        }
    }
}

/// <summary>
/// Comprehensive database diagnostics information for debugging
/// </summary>
public class DatabaseDiagnostics
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = string.Empty;
    public bool CanConnect { get; set; }
    public double DatabaseSizeMB { get; set; }
    public int FileCount { get; set; }
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> PendingMigrations { get; set; } = new();
    public int TrackedEntitiesCount { get; set; }
    public int ModifiedEntitiesCount { get; set; }
    public int AddedEntitiesCount { get; set; }
    public int DeletedEntitiesCount { get; set; }
    public List<string> Notes { get; set; } = new();
    public DateTime CollectionTime { get; set; } = DateTime.UtcNow;
}
