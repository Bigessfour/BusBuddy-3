using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using System.Data.Common;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace BusBuddy.Core.Interceptors;

/// <summary>
/// EF Core interceptor for debugging SQL queries and performance monitoring
/// Captures detailed information about database interactions for SQL Server Express debugging
/// </summary>
public class DatabaseDebuggingInterceptor : DbCommandInterceptor
{
    private static readonly ILogger Logger = Log.ForContext<DatabaseDebuggingInterceptor>();
    private readonly ConcurrentDictionary<DbCommand, Stopwatch> _commandTimers = new();
    private readonly ConcurrentQueue<QueryExecutionInfo> _recentQueries = new();
    private const int MaxRecentQueries = 100;

    public DatabaseDebuggingInterceptor()
    {
        // No parameters needed - using static Logger
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StartCommandTiming(command, "ExecuteReaderAsync");
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StopCommandTiming(command, eventData, "ExecuteReaderAsync");
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StartCommandTiming(command, "ExecuteScalarAsync");
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StopCommandTiming(command, eventData, "ExecuteScalarAsync");
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StartCommandTiming(command, "ExecuteNonQueryAsync");
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StopCommandTiming(command, eventData, "ExecuteNonQueryAsync", result);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(eventData);
        HandleCommandError(command, eventData);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(eventData);
        HandleCommandError(command, eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void StartCommandTiming(DbCommand command, string operation)
    {
        var stopwatch = Stopwatch.StartNew();
        _commandTimers[command] = stopwatch;

        Logger.Debug("Starting {Operation}: {CommandText}",
            operation,
            TruncateCommandText(command.CommandText));
    }

    private void StopCommandTiming(DbCommand command, CommandExecutedEventData eventData, string operation, object? result = null)
    {
        if (_commandTimers.TryRemove(command, out var stopwatch))
        {
            stopwatch.Stop();

            var queryInfo = new QueryExecutionInfo
            {
                CommandText = command.CommandText,
                Operation = operation,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Success = true,
                Result = result?.ToString(),
                Timestamp = DateTime.UtcNow,
                Parameters = ExtractParameters(command)
            };

            LogQueryExecution(queryInfo);
            AddToRecentQueries(queryInfo);
        }
    }

    private void HandleCommandError(DbCommand command, CommandErrorEventData eventData)
    {
        if (_commandTimers.TryRemove(command, out var stopwatch))
        {
            stopwatch.Stop();

            var queryInfo = new QueryExecutionInfo
            {
                CommandText = command.CommandText,
                Operation = "Failed",
                DurationMs = stopwatch.ElapsedMilliseconds,
                Success = false,
                Error = eventData.Exception.Message,
                Timestamp = DateTime.UtcNow,
                Parameters = ExtractParameters(command)
            };

            LogQueryExecution(queryInfo, isError: true);
            AddToRecentQueries(queryInfo);
        }
    }

    private static void LogQueryExecution(QueryExecutionInfo queryInfo, bool isError = false)
    {
        if (isError)
        {
            Logger.Error("SQL Command Failed: {Operation} in {Duration}ms - {Error}\nSQL: {CommandText}",
                queryInfo.Operation,
                queryInfo.DurationMs,
                queryInfo.Error,
                TruncateCommandText(queryInfo.CommandText));
        }
        else if (queryInfo.DurationMs > 1000) // Log slow queries
        {
            Logger.Warning("Slow SQL Query: {Operation} took {Duration}ms\nSQL: {CommandText}",
                queryInfo.Operation,
                queryInfo.DurationMs,
                TruncateCommandText(queryInfo.CommandText));
        }
        else
        {
            Logger.Debug("SQL Command Completed: {Operation} in {Duration}ms",
                queryInfo.Operation,
                queryInfo.DurationMs);
        }
    }

    private void AddToRecentQueries(QueryExecutionInfo queryInfo)
    {
        _recentQueries.Enqueue(queryInfo);

        // Keep only the most recent queries
        while (_recentQueries.Count > MaxRecentQueries)
        {
            _recentQueries.TryDequeue(out _);
        }
    }

    private static string TruncateCommandText(string commandText, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(commandText) || commandText.Length <= maxLength)
        {
            return commandText;
        }

        return string.Concat(commandText.AsSpan(0, maxLength), "...");
    }

    private static Dictionary<string, object?> ExtractParameters(DbCommand command)
    {
        var parameters = new Dictionary<string, object?>();

        foreach (DbParameter parameter in command.Parameters)
        {
            parameters[parameter.ParameterName] = parameter.Value;
        }

        return parameters;
    }

    /// <summary>
    /// Get recent query execution information for debugging
    /// </summary>
    public List<QueryExecutionInfo> GetRecentQueries(int count = 20)
    {
        return _recentQueries.TakeLast(count).ToList();
    }

    /// <summary>
    /// Get performance statistics for recent queries
    /// </summary>
    public QueryPerformanceStats GetPerformanceStats()
    {
        var queries = _recentQueries.ToList();

        if (queries.Count == 0)
        {
            return new QueryPerformanceStats();
        }

        return new QueryPerformanceStats
        {
            TotalQueries = queries.Count,
            SuccessfulQueries = queries.Count(q => q.Success),
            FailedQueries = queries.Count(q => !q.Success),
            AverageDurationMs = queries.Average(q => q.DurationMs),
            MaxDurationMs = queries.Max(q => q.DurationMs),
            MinDurationMs = queries.Min(q => q.DurationMs),
            SlowQueries = queries.Where(q => q.DurationMs > 1000).ToList(),
            RecentErrors = queries.Where(q => !q.Success).TakeLast(5).ToList()
        };
    }

    /// <summary>
    /// Clear recent query history
    /// </summary>
    public void ClearHistory()
    {
        while (_recentQueries.TryDequeue(out _)) { }
        _commandTimers.Clear();
    }
}

/// <summary>
/// Information about a query execution for debugging purposes
/// </summary>
public class QueryExecutionInfo
{
    public string CommandText { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

/// <summary>
/// Performance statistics for query debugging
/// </summary>
public class QueryPerformanceStats
{
    public int TotalQueries { get; set; }
    public int SuccessfulQueries { get; set; }
    public int FailedQueries { get; set; }
    public double AverageDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public long MinDurationMs { get; set; }
    public List<QueryExecutionInfo> SlowQueries { get; set; } = new();
    public List<QueryExecutionInfo> RecentErrors { get; set; } = new();
}
