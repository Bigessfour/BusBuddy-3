using System;
using System.Data;
using System.Threading.Tasks;
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
                    DatabaseUserMessage.LogFailure(
                        Logger,
                        ex,
                        "Database operation {OperationName} failed permanently after {Attempts} attempts",
                        operationName,
                        attempt + 1);
                    throw;
                }
            }

            throw new InvalidOperationException($"Database operation {operationName} exhausted all retry attempts");
        }
    }

    /// <summary>
    /// Determines if an exception warrants a retry attempt
    /// </summary>
    private static bool ShouldRetry(Exception exception, int currentAttempt, int maxRetries)
    {
        if (currentAttempt >= maxRetries)
        {

            return false;
        }

        return exception switch
        {
            TimeoutException => true,
            _ when DatabaseUserMessage.IsConnectivityFailure(exception) => true,
            _ => false
        };
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
