using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Service for implementing database resilience patterns and handling transient faults
/// Provides retry logic, circuit breaker pattern, and comprehensive error handling
/// </summary>
public class DatabaseResilienceService
{
    private static readonly ILogger Logger = Log.ForContext<DatabaseResilienceService>();
    private readonly TimeSpan _circuitBreakerTimeout = TimeSpan.FromMinutes(5);
    private DateTime _lastFailureTime = DateTime.MinValue; private int _consecutiveFailures;
    private const int MaxConsecutiveFailures = 3;

    public DatabaseResilienceService()
    {
    }

    /// <summary>
    /// Executes a database operation with retry logic and circuit breaker pattern
    /// </summary>
    public async Task<T> ExecuteWithResilienceAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxRetries = 3,
        TimeSpan baseDelay = default)
    {
        if (IsCircuitBreakerOpen())
        {
            throw new InvalidOperationException($"Circuit breaker is open for database operations. Last failure: {_lastFailureTime}");
        }

        var delay = baseDelay == default ? TimeSpan.FromSeconds(1) : baseDelay;
        Exception lastException = new InvalidOperationException("Database operation failed");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Logger.Debug("Executing database operation '{OperationName}', attempt {Attempt}/{MaxRetries}",
                    operationName, attempt, maxRetries);

                var result = await operation();

                // Reset circuit breaker on success
                ResetCircuitBreaker();

                Logger.Debug("Database operation '{OperationName}' completed successfully", operationName);
                return result;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt, maxRetries))
            {
                lastException = ex;
                var currentDelay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(2, attempt - 1));

                Logger.Warning(ex,
                    "Database operation '{OperationName}' failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms. Error: {ErrorMessage}",
                    operationName, attempt, maxRetries, currentDelay.TotalMilliseconds, ex.Message);

                await Task.Delay(currentDelay);
            }
            catch (Exception ex)
            {
                lastException = ex;
                RecordFailure();

                Logger.Error(ex,
                    "Database operation '{OperationName}' failed permanently on attempt {Attempt}/{MaxRetries}. Error: {ErrorMessage}",
                    operationName, attempt, maxRetries, ex.Message);

                // Don't retry non-transient errors
                break;
            }
        }

        RecordFailure();
        var analysisReport = ExceptionHelper.AnalyzeException(lastException!);

        throw new DatabaseOperationException(
            $"Database operation '{operationName}' failed after {maxRetries} attempts.\n\n{analysisReport}",
            lastException!);
    }

    /// <summary>
    /// Determines if an exception should trigger a retry
    /// </summary>
    private static bool ShouldRetry(Exception ex, int currentAttempt, int maxRetries)
    {
        if (currentAttempt >= maxRetries)
        {
            return false;
        }


        return ex switch
        {
            SqlException sqlEx => IsTransientSqlError(sqlEx),
            InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            TaskCanceledException => true,
            TimeoutException => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if SQL error is transient and worth retrying
    /// </summary>
    private static bool IsTransientSqlError(SqlException ex)
    {
        var transientErrors = new[]
        {
            -2,     // Timeout
            2,      // Connection timeout
            20,     // Instance unavailable
            64,     // Connection lost
            233,    // Connection init error
            10053,  // Transport error
            10054,  // Connection reset
            10060,  // Network unreachable
            40197,  // Service unavailable
            40501,  // Service busy
            40613,  // Database unavailable
            49918,  // Cannot process request
            49919,  // Cannot process create or update request
            49920   // Cannot process request. Too many operations in progress
        };

        return transientErrors.Contains(ex.Number);
    }

    /// <summary>
    /// Records a failure for circuit breaker pattern
    /// </summary>
    private void RecordFailure()
    {
        _consecutiveFailures++;
        _lastFailureTime = DateTime.UtcNow;

        Logger.Warning("Database failure recorded. Consecutive failures: {ConsecutiveFailures}", _consecutiveFailures);
    }

    /// <summary>
    /// Resets the circuit breaker after successful operation
    /// </summary>
    private void ResetCircuitBreaker()
    {
        if (_consecutiveFailures > 0)
        {
            Logger.Information("Database circuit breaker reset after successful operation");
            _consecutiveFailures = 0;
            _lastFailureTime = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Checks if circuit breaker is open (blocking operations)
    /// </summary>
    private bool IsCircuitBreakerOpen()
    {
        if (_consecutiveFailures < MaxConsecutiveFailures)
        {
            return false;
        }


        if (DateTime.UtcNow - _lastFailureTime > _circuitBreakerTimeout)
        {
            Logger.Information("Database circuit breaker timeout expired, allowing retry");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates database connection and schema
    /// </summary>
    public async Task<DatabaseHealthResult> CheckDatabaseHealthAsync(DbContext context)
    {
        var result = new DatabaseHealthResult();

        try
        {
            // Check basic connectivity
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                result.AddIssue("Cannot connect to database");
                return result;
            }

            // Check if database exists
            var connectionState = context.Database.GetDbConnection().State;
            if (connectionState != ConnectionState.Open)
            {
                await context.Database.GetDbConnection().OpenAsync();
            }

            // Verify key tables exist
            var tableChecks = new[]
            {
                // Only check for Buses, Drivers, Routes, Schedules
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Buses'",
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Drivers'",
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Routes'",
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Schedules'"
            };

            foreach (var check in tableChecks)
            {
                try
                {
                    await context.Database.ExecuteSqlRawAsync(check);
                }
                catch (Exception ex)
                {
                    result.AddIssue($"Table validation failed: {ex.Message}");
                }
            }

            result.IsHealthy = result.Issues.Count == 0;

            Logger.Information("Database health check completed. Healthy: {IsHealthy}, Issues: {IssueCount}",
                result.IsHealthy, result.Issues.Count);

            return result;
        }
        catch (Exception ex)
        {
            result.AddIssue($"Health check failed: {ex.Message}");
            Logger.Error(ex, "Database health check encountered an error");
            return result;
        }
    }
}

/// <summary>
/// Result of database health check
/// </summary>
public class DatabaseHealthResult
{
    public bool IsHealthy { get; set; } = true;
    public List<string> Issues { get; set; } = new();

    public void AddIssue(string issue)
    {
        Issues.Add(issue);
        IsHealthy = false;
    }
}

/// <summary>
/// Custom exception for database operation failures
/// </summary>
public class DatabaseOperationException : Exception
{
    public DatabaseOperationException(string message) : base(message) { }
    public DatabaseOperationException(string message, Exception innerException) : base(message, innerException) { }
}
