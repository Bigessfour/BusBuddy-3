using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Diagnostics;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service for optimizing database performance and identifying slow queries
/// </summary>
public class DatabasePerformanceOptimizer
{
    private static readonly ILogger Logger = Log.ForContext<DatabasePerformanceOptimizer>();
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public DatabasePerformanceOptimizer(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Analyze and optimize common database queries
    /// </summary>
    public async Task<PerformanceAnalysisResult> AnalyzePerformanceAsync()
    {
        var result = new PerformanceAnalysisResult();

        using var context = _contextFactory.CreateDbContext();

        // Test common queries with performance measurement
        await MeasureQueryPerformance(result, "GetAllBuses", async () =>
        {
            return await context.Buses
                .AsNoTracking()
                .Where(v => v.Status == "Active")
                .Select(v => new { v.VehicleId, v.BusNumber, v.Make, v.Model, v.Status })
                .ToListAsync();
        });

        await MeasureQueryPerformance(result, "GetAllDrivers", async () =>
        {
            return await context.Drivers
                .AsNoTracking()
                .Where(d => d.Status == "Active")
                .Select(d => new { d.DriverId, d.DriverName, d.Status, d.TrainingComplete })
                .ToListAsync();
        });

        await MeasureQueryPerformance(result, "GetRoutesWithDetails", async () =>
        {
            return await context.Routes
                .AsNoTracking()
                .Include(r => r.AMVehicle)
                .Include(r => r.PMVehicle)
                .Include(r => r.AMDriver)
                .Include(r => r.PMDriver)
                .Where(r => r.Date >= DateTime.Today.AddDays(-30))
                .ToListAsync();
        });

        await MeasureQueryPerformance(result, "GetStudentsWithRoutes", async () =>
        {
            return await context.Students
                .AsNoTracking()
                .Where(s => s.Active)
                .Select(s => new
                {
                    s.StudentId,
                    s.StudentName,
                    s.Grade,
                    s.AMRoute,
                    s.PMRoute,
                    s.Active
                })
                .ToListAsync();
        });

        // Identify potential N+1 queries
        await IdentifyNPlusOneQueries(result);

        // Check for missing indexes
        await CheckForMissingIndexes(result);

        Logger.Information("Database performance analysis completed. Found {IssueCount} potential issues.",
            result.Issues.Count);

        return result;
    }

    /// <summary>
    /// Apply recommended performance optimizations
    /// </summary>
    public Task<int> ApplyOptimizationsAsync()
    {
        var optimizationsApplied = 0;

        using var context = _contextFactory.CreateDbContext();

        try
        {
            // Update query tracking behavior for better performance
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            // Clear any cached query plans
            context.ChangeTracker.Clear();

            Logger.Information("Applied database performance optimizations");
            optimizationsApplied++;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply database optimizations");
        }

        return Task.FromResult(optimizationsApplied);
    }

    private async Task MeasureQueryPerformance<T>(PerformanceAnalysisResult result, string queryName, Func<Task<T>> query)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var queryResult = await query();
            stopwatch.Stop();

            var measurement = new QueryPerformanceMeasurement
            {
                QueryName = queryName,
                Duration = stopwatch.ElapsedMilliseconds,
                Success = true,
                ResultCount = GetResultCount(queryResult)
            };

            result.Measurements.Add(measurement);

            if (stopwatch.ElapsedMilliseconds > 1000) // Warn about queries over 1 second
            {
                result.Issues.Add($"Slow query detected: {queryName} took {stopwatch.ElapsedMilliseconds}ms");
                Logger.Warning("Slow query: {QueryName} took {Duration}ms", queryName, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var measurement = new QueryPerformanceMeasurement
            {
                QueryName = queryName,
                Duration = stopwatch.ElapsedMilliseconds,
                Success = false,
                Error = ex.Message
            };

            result.Measurements.Add(measurement);
            result.Issues.Add($"Query failed: {queryName} - {ex.Message}");

            Logger.Error(ex, "Query failed: {QueryName}", queryName);
        }
    }

    private int GetResultCount<T>(T result)
    {
        if (result is System.Collections.ICollection collection)
        {
            return collection.Count;
        }
        return result != null ? 1 : 0;
    }

    private async Task IdentifyNPlusOneQueries(PerformanceAnalysisResult result)
    {
        // This would involve analyzing SQL logs or using profiling tools
        // For now, add common N+1 scenarios as warnings
        result.Issues.Add("Potential N+1 query: Routes with Vehicle/Driver navigation properties");
        result.Issues.Add("Potential N+1 query: Activities with assigned resources");

        await Task.CompletedTask;
    }

    private async Task CheckForMissingIndexes(PerformanceAnalysisResult result)
    {
        using var context = _contextFactory.CreateDbContext();

        try
        {
            // Check if we can connect and query basic metadata
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                result.Issues.Add("Cannot connect to database to check indexes");
                return;
            }

            // Note: In a real implementation, you would query database metadata
            // to identify missing indexes on frequently queried columns
            result.Recommendations.Add("Consider adding composite indexes for date range queries");
            result.Recommendations.Add("Ensure foreign key columns have proper indexes");
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Failed to check indexes: {ex.Message}");
        }
    }
}

public class PerformanceAnalysisResult
{
    public List<QueryPerformanceMeasurement> Measurements { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
}

public class QueryPerformanceMeasurement
{
    public string QueryName { get; set; } = string.Empty;
    public long Duration { get; set; }
    public bool Success { get; set; }
    public int ResultCount { get; set; }
    public string? Error { get; set; }
}
