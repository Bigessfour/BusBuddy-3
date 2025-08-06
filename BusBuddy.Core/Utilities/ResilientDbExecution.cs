using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Provides resilient database execution patterns with comprehensive error handling and retry logic
/// Implements enterprise-grade patterns for handling transient database failures
/// </summary>
public static class ResilientDbExecution
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ResilientDbExecution));

    /// <summary>
    /// Executes a database query with resilient error handling and retry logic
    /// </summary>
    /// <typeparam name="T">Return type of the query</typeparam>
    /// <param name="operation">The database operation to execute</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <returns>Result of the operation</returns>
    public static async Task<T> ExecuteWithResilienceAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxRetries = 3)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        using (LogContext.PushProperty("Operation", operationName))
        using (LogContext.PushProperty("MaxRetries", maxRetries))
        {
            Logger.Debug("Starting resilient database operation: {OperationName}", operationName);

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await operation();

                    if (attempt > 0)
                    {
                        Logger.Information("Database operation {OperationName} succeeded on attempt {Attempt}",
                            operationName, attempt + 1);
                    }

                    return result;
                }
                catch (Exception ex) when (ShouldRetry(ex, attempt, maxRetries))
                {
                    var delay = CalculateBackoffDelay(attempt);
                    Logger.Warning("Database operation {OperationName} failed on attempt {Attempt}, retrying in {Delay}ms: {Error}",
                        operationName, attempt + 1, delay, ex.Message);

                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Database operation {OperationName} failed permanently after {Attempts} attempts",
                        operationName, attempt + 1);
                    throw;
                }
            }

            throw new InvalidOperationException($"Database operation {operationName} exhausted all retry attempts");
        }
    }

    /// <summary>
    /// Executes a database command with transaction support and resilient error handling
    /// </summary>
    /// <param name="context">Database context</param>
    /// <param name="operation">The database operation to execute</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="useTransaction">Whether to wrap in a transaction</param>
    /// <returns>Task representing the operation</returns>
    public static async Task ExecuteWithTransactionAsync(
        DbContext context,
        Func<Task> operation,
        string operationName,
        bool useTransaction = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        using (LogContext.PushProperty("Operation", operationName))
        using (LogContext.PushProperty("UseTransaction", useTransaction))
        {
            if (!useTransaction)
            {
                await operation();
                return;
            }

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    Logger.Debug("Starting transactional database operation: {OperationName}", operationName);

                    await operation();
                    await transaction.CommitAsync();

                    Logger.Debug("Successfully completed transactional operation: {OperationName}", operationName);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Rolling back transaction for operation: {OperationName}", operationName);
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
    }

    /// <summary>
    /// Validates database connectivity with detailed diagnostics
    /// </summary>
    /// <param name="context">Database context to test</param>
    /// <returns>True if connection is healthy</returns>
    public static async Task<bool> ValidateConnectionAsync(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using (LogContext.PushProperty("Operation", "ConnectionValidation"))
        {
            try
            {
                Logger.Debug("Validating database connection");

                var canConnect = await context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    Logger.Warning("Database connection validation failed");
                    return false;
                }

                // Test with a simple query
                var connectionString = context.Database.GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand("SELECT 1", connection);
                var result = await command.ExecuteScalarAsync();

                Logger.Information("Database connection validation successful");
                return result != null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Database connection validation failed with exception");
                return false;
            }
        }
    }

    /// <summary>
    /// Determines if an exception warrants a retry attempt
    /// </summary>
    private static bool ShouldRetry(Exception exception, int currentAttempt, int maxRetries)
    {
        if (currentAttempt >= maxRetries)
            return false;

        return exception switch
        {
            SqlException sqlEx => IsTransientSqlError(sqlEx),
            TimeoutException => true,
            InvalidOperationException invOpEx when invOpEx.Message.Contains("timeout") => true,
            DbUpdateException dbEx when dbEx.InnerException is SqlException sqlInner => IsTransientSqlError(sqlInner),
            _ => false
        };
    }

    /// <summary>
    /// Determines if a SQL exception is transient and worth retrying
    /// </summary>
    private static bool IsTransientSqlError(SqlException sqlException)
    {
        // Common transient SQL error codes
        int[] transientErrorCodes = {
                -2,     // Timeout
                2,      // Timeout
                53,     // Network path not found
                121,    // Semaphore timeout
                232,    // Pipe broken
                10053,  // Connection aborted
                10054,  // Connection reset
                10060,  // Connection timeout
                40197,  // Service busy
                40501,  // Service busy
                40613   // Database unavailable
            };

        return Array.Exists(transientErrorCodes, code => code == sqlException.Number);
    }

    /// <summary>
    /// Calculates exponential backoff delay for retry attempts
    /// </summary>
    private static int CalculateBackoffDelay(int attempt)
    {
        var baseDelay = 1000; // 1 second
        var maxDelay = 10000; // 10 seconds

        var delay = Math.Min(baseDelay * Math.Pow(2, attempt), maxDelay);
        return (int)delay;
    }
}
