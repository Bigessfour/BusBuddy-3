using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.Core.Extensions;

/// <summary>
/// Extension methods for enhanced database operations with resilience patterns
/// Implements the recommended patterns for handling SqlException and InvalidCastException
/// </summary>
public static class DatabaseOperationExtensions
{
    /// <summary>
    /// Configures database services with resilience patterns
    /// </summary>
    public static IServiceCollection AddDatabaseResilience(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseResilienceService>();
        services.AddScoped<SafeDatabaseOperations>();
        return services;
    }

    /// <summary>
    /// Executes a query with automatic retry and exception handling
    /// </summary>
    public static async Task<List<T>> SafeQueryAsync<T>(
        this DbContext context,
        IQueryable<T> query,
        string operationName = "Query") where T : class
    {
        var logger = Log.ForContext<SafeDatabaseOperations>();
        try
        {
            return await ExceptionHelper.ExecuteWithRetryAsync(async () =>
            {
                // Add connection validation before query
                if (!await context.Database.CanConnectAsync())
                {
                    throw new InvalidOperationException("Database connection is not available");
                }

                // Use AsNoTracking for better performance and to avoid some casting issues
                return await query.AsNoTracking().ToListAsync();
            });
        }
        catch (SqlException sqlEx)
        {
            logger.Error(sqlEx, "SQL error during {OperationName}: {ErrorAnalysis}",
                operationName, ExceptionHelper.AnalyzeSqlException(sqlEx));
            throw new DatabaseOperationException($"Database query failed: {operationName}", sqlEx);
        }
        catch (InvalidCastException castEx)
        {
            logger.Error(castEx, "Type casting error during {OperationName}: {ErrorAnalysis}",
                operationName, ExceptionHelper.AnalyzeInvalidCastException(castEx));
            throw new DatabaseOperationException($"Data type mismatch in query: {operationName}", castEx);
        }
    }

    /// <summary>
    /// Safely executes a database command with transaction support
    /// </summary>
    public static async Task<T> SafeExecuteAsync<T>(
        this DbContext context,
        Func<Task<T>> operation,
        string operationName = "Operation",
        bool useTransaction = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);

        var logger = Log.ForContext<SafeDatabaseOperations>();
        if (useTransaction)
        {
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    logger.Debug("Starting database operation: {OperationName}", operationName);

                    var result = await operation();
                    await transaction.CommitAsync();

                    logger.Debug("Successfully completed database operation: {OperationName}", operationName);
                    return result;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Rolling back transaction for operation: {OperationName}", operationName);
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        else
        {
            return await ExceptionHelper.ExecuteWithRetryAsync(operation);
        }
    }

    /// <summary>
    /// Validates entity before saving to prevent common exceptions
    /// </summary>
    public static void ValidateEntity<T>(this T entity) where T : class
    {
        var logger = Log.ForContext<SafeDatabaseOperations>();
        var entityType = typeof(T);
        var properties = entityType.GetProperties();

        foreach (var property in properties)
        {
            var value = property.GetValue(entity);

            // Check for required string properties that are null or empty
            if (property.PropertyType == typeof(string))
            {
                var isRequired = property.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Length > 0;
                if (isRequired && string.IsNullOrWhiteSpace(value?.ToString()))
                {
                    logger.Warning("Required property {PropertyName} is null or empty for entity {EntityType}",
                        property.Name, entityType.Name);
                }
            }

            // Check for foreign key properties that might be invalid
            if (property.Name.EndsWith("Id", StringComparison.Ordinal) && property.PropertyType == typeof(int))
            {
                var intValue = (int?)value ?? 0;
                if (intValue <= 0)
                {
                    logger.Warning("Foreign key property {PropertyName} has invalid value {Value} for entity {EntityType}",
                        property.Name, intValue, entityType.Name);
                }
            }
        }
    }
}

/// <summary>
/// Service providing safe database operations with comprehensive error handling
/// </summary>
public class SafeDatabaseOperations
{
    private static readonly ILogger Logger = Log.ForContext<SafeDatabaseOperations>();
    private readonly DatabaseResilienceService _resilienceService;

    public SafeDatabaseOperations(DatabaseResilienceService resilienceService)
    {
        _resilienceService = resilienceService;
    }

    /// <summary>
    /// Safely retrieves buses with enhanced error handling
    /// </summary>
    public async Task<List<Models.Bus>> GetBusesAsync(BusBuddy.Core.Data.BusBuddyDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await _resilienceService.ExecuteWithResilienceAsync(async () =>
        {
            return await context.SafeQueryAsync(
                context.Buses.Where(v => v.Status == "Active"),
                "GetBuses");
        }, "GetBuses");
    }

    /// <summary>
    /// Safely retrieves drivers with enhanced error handling
    /// </summary>
    public async Task<List<Models.Driver>> GetDriversAsync(BusBuddyDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await _resilienceService.ExecuteWithResilienceAsync(async () =>
        {
            return await context.SafeQueryAsync(
                context.Drivers.Where(d => d.Status == "Active"),
                "GetDrivers");
        }, "GetDrivers");
    }

    /// <summary>
    /// Safely retrieves routes with enhanced error handling
    /// </summary>
    public async Task<List<Models.Route>> GetRoutesAsync(BusBuddyDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await _resilienceService.ExecuteWithResilienceAsync(async () =>
        {
            try
            {
                // Use explicit joins to avoid lazy loading issues
                var routes = await context.Routes
                    .Include(r => r.AMVehicle)
                    .Include(r => r.PMVehicle)
                    .Include(r => r.AMDriver)
                    .Include(r => r.PMDriver)
                    .AsNoTracking()
                    .ToListAsync();

                return routes;
            }
            catch (InvalidCastException ex) when (ex.Message.Contains("BusId"))
            {
                // Handle the specific BusId mapping issue
                Logger.Warning("BusId mapping issue detected, falling back to basic route query");

                return await context.Routes
                    .AsNoTracking()
                    .ToListAsync();
            }
        }, "GetRoutes");
    }

    /// <summary>
    /// Safely saves an entity with validation and error handling
    /// </summary>
    public async Task<T> SaveEntityAsync<T>(BusBuddyDbContext context, T entity) where T : class
    {
        return await _resilienceService.ExecuteWithResilienceAsync(async () =>
        {
            entity.ValidateEntity();

            return await context.SafeExecuteAsync(async () =>
            {
                context.Set<T>().Add(entity);
                await context.SaveChangesAsync();
                return entity;
            }, $"Save{typeof(T).Name}");
        }, $"SaveEntity{typeof(T).Name}");
    }

    /// <summary>
    /// Safely updates an entity with validation and error handling
    /// </summary>
    public async Task<T> UpdateEntityAsync<T>(BusBuddyDbContext context, T entity) where T : class
    {
        return await _resilienceService.ExecuteWithResilienceAsync(async () =>
        {
            entity.ValidateEntity();

            return await context.SafeExecuteAsync(async () =>
            {
                context.Set<T>().Update(entity);
                await context.SaveChangesAsync();
                return entity;
            }, $"Update{typeof(T).Name}");
        }, $"UpdateEntity{typeof(T).Name}");
    }

    /// <summary>
    /// Performs database health check and reports issues
    /// </summary>
    public async Task<DatabaseHealthResult> CheckDatabaseHealthAsync(BusBuddyDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await _resilienceService.CheckDatabaseHealthAsync(context);
    }
}
