using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace BusBuddy.Core.Extensions
{
    /// <summary>
    /// Extensions for BusBuddy.Core.Data.BusBuddyDbContext to improve logging and performance tracking
    /// </summary>
    public static class DbContextExtensions
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "BusBuddyDbContextExtensions");

        /// <summary>
        /// Executes a query with detailed performance logging
        /// </summary>
        public static async Task<T> ExecuteWithLoggingAsync<T>(
            this DbContext dbContext,
            string queryName,
            Func<Task<T>> queryFunc,
            string? additionalContextName = null,
            object? additionalContextValue = null)
        {
            var stopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("QueryName", queryName))
            using (LogContext.PushProperty("DatabaseOperation", true))
            {
                if (additionalContextName != null && additionalContextValue != null)
                {
                    using (LogContext.PushProperty(additionalContextName, additionalContextValue))
                    {
                        Logger.Debug("Starting database query: {QueryName}", queryName);
                        try
                        {
                            var result = await queryFunc();
                            stopwatch.Stop();

                            using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                            {
                                Logger.Information("Database query {QueryName} completed in {Duration}ms",
                                    queryName, stopwatch.ElapsedMilliseconds);
                            }

                            return result;
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();

                            using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                            {
                                Logger.Error(ex, "Database query {QueryName} failed after {Duration}ms",
                                    queryName, stopwatch.ElapsedMilliseconds);
                            }

                            throw;
                        }
                    }
                }
                else
                {
                    Logger.Debug("Starting database query: {QueryName}", queryName);
                    try
                    {
                        var result = await queryFunc();
                        stopwatch.Stop();

                        using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                        {
                            Logger.Information("Database query {QueryName} completed in {Duration}ms",
                                queryName, stopwatch.ElapsedMilliseconds);
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();

                        using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                        {
                            Logger.Error(ex, "Database query {QueryName} failed after {Duration}ms",
                                queryName, stopwatch.ElapsedMilliseconds);
                        }

                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Executes an update/insert/delete operation with detailed performance logging
        /// </summary>
        public static async Task<int> SaveChangesWithLoggingAsync(
            this DbContext dbContext,
            string operationName,
            string? additionalContextName = null,
            object? additionalContextValue = null)
        {
            var stopwatch = Stopwatch.StartNew();

            using (LogContext.PushProperty("OperationName", operationName))
            using (LogContext.PushProperty("DatabaseOperation", true))
            {
                if (additionalContextName != null && additionalContextValue != null)
                {
                    using (LogContext.PushProperty(additionalContextName, additionalContextValue))
                    {
                        Logger.Debug("Starting database operation: {OperationName}", operationName);
                        try
                        {
                            var entriesBeforeSave = dbContext.ChangeTracker.Entries().Count();
                            var result = await dbContext.SaveChangesAsync();
                            stopwatch.Stop();

                            using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                            using (LogContext.PushProperty("ChangedEntities", result))
                            {
                                Logger.Information("Database operation {OperationName} completed in {Duration}ms. Changed {ChangedEntities} entities.",
                                    operationName, stopwatch.ElapsedMilliseconds, result);
                            }

                            return result;
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();

                            using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                            {
                                Logger.Error(ex, "Database operation {OperationName} failed after {Duration}ms",
                                    operationName, stopwatch.ElapsedMilliseconds);
                            }

                            throw;
                        }
                    }
                }
                else
                {
                    Logger.Debug("Starting database operation: {OperationName}", operationName);
                    try
                    {
                        var entriesBeforeSave = dbContext.ChangeTracker.Entries().Count();
                        var result = await dbContext.SaveChangesAsync();
                        stopwatch.Stop();

                        using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                        using (LogContext.PushProperty("ChangedEntities", result))
                        {
                            Logger.Information("Database operation {OperationName} completed in {Duration}ms. Changed {ChangedEntities} entities.",
                                operationName, stopwatch.ElapsedMilliseconds, result);
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();

                        using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                        {
                            Logger.Error(ex, "Database operation {OperationName} failed after {Duration}ms",
                                operationName, stopwatch.ElapsedMilliseconds);
                        }

                        throw;
                    }
                }
            }
        }
    }
}
