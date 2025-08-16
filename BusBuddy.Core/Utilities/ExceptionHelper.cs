using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Utility class for handling and diagnosing common exceptions in BusBuddy
/// Specifically designed to address SqlException, InvalidCastException, and ArgumentException issues
/// </summary>
public static class ExceptionHelper
{
    /// <summary>
    /// Analyzes and provides detailed information about SQL Server exceptions
    /// </summary>
    public static string AnalyzeSqlException(SqlException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var analysis = $"SQL Exception Analysis:\n";
        analysis += $"Error Number: {ex.Number}\n";
        analysis += $"Severity: {ex.Class}\n";
        analysis += $"State: {ex.State}\n";
        analysis += $"Server: {ex.Server ?? "Unknown"}\n";
        analysis += $"Procedure: {ex.Procedure ?? "N/A"}\n";
        analysis += $"Line Number: {ex.LineNumber}\n";
        analysis += $"Message: {ex.Message}\n\n";

        // Common SQL Server error interpretations
        analysis += ex.Number switch
        {
            2 => "RECOMMENDATION: Check if SQL Server instance is running and connection string is correct.\n",
            18 => "RECOMMENDATION: Login failed - verify credentials and database permissions.\n",
            207 => "RECOMMENDATION: Invalid column name - check entity property to column mappings.\n",
            208 => "RECOMMENDATION: Invalid object name - table may not exist or has different name.\n",
            515 => "RECOMMENDATION: Cannot insert NULL value - check required fields and default values.\n",
            547 => "RECOMMENDATION: Foreign key constraint violation - verify referenced data exists.\n",
            2002 => "RECOMMENDATION: Connection timeout - check network connectivity and server load.\n",
            2146 => "RECOMMENDATION: Connection was forcibly closed - check connection pooling settings.\n",
            _ => "RECOMMENDATION: Check SQL Server logs and verify database schema alignment.\n"
        };

        return analysis;
    }

    /// <summary>
    /// Analyzes Entity Framework specific exceptions
    /// </summary>
    public static string AnalyzeEfException(DbUpdateException ex)
    {
        ArgumentNullException.ThrowIfNull(ex, nameof(ex));

        var analysis = $"Entity Framework Exception Analysis:\n";
        analysis += $"Message: {ex.Message}\n";

        if (ex.InnerException is SqlException sqlEx)
        {
            analysis += "\nUnderlying SQL Exception:\n";
            analysis += AnalyzeSqlException(sqlEx);
        }

        if (ex.Entries.Any())
        {
            analysis += "\nAffected Entities:\n";
            foreach (var entry in ex.Entries)
            {
                analysis += $"- Entity Type: {entry.Entity.GetType().Name}\n";
                analysis += $"  State: {entry.State}\n";

                if (entry.State == EntityState.Modified)
                {
                    var modifiedProps = entry.Properties
                        .Where(p => p.IsModified)
                        .Select(p => $"{p.Metadata.Name}: {p.CurrentValue}")
                        .ToList();

                    if (modifiedProps.Any())
                    {
                        analysis += $"  Modified Properties: {string.Join(", ", modifiedProps)}\n";
                    }
                }
            }
        }

        return analysis;
    }

    /// <summary>
    /// Analyzes InvalidCastException which commonly occurs with NULL values
    /// </summary>
    public static string AnalyzeInvalidCastException(InvalidCastException ex)
    {
        ArgumentNullException.ThrowIfNull(ex, nameof(ex));

        var analysis = $"Invalid Cast Exception Analysis:\n";
        analysis += $"Message: {ex.Message}\n";
        analysis += $"Source: {ex.Source}\n\n";

        analysis += "COMMON CAUSES:\n";
        analysis += "1. Database NULL values being cast to non-nullable types\n";
        analysis += "2. Enum values not matching database values\n";
        analysis += "3. DateTime format mismatches\n";
        analysis += "4. Decimal precision issues\n";
        analysis += "5. String to numeric conversion failures\n\n";

        analysis += "RECOMMENDATIONS:\n";
        analysis += "1. Check for NULL values in database columns\n";
        analysis += "2. Verify entity property types match database column types\n";
        analysis += "3. Add NULL handling in entity property setters\n";
        analysis += "4. Use nullable types where appropriate\n";
        analysis += "5. Implement custom value converters for complex types\n";

        return analysis;
    }

    /// <summary>
    /// Analyzes ArgumentException which can occur during WPF binding or validation
    /// </summary>
    public static string AnalyzeArgumentException(ArgumentException ex)
    {
        ArgumentNullException.ThrowIfNull(ex, nameof(ex));

        var analysis = $"Argument Exception Analysis:\n";
        analysis += $"Message: {ex.Message}\n";
        analysis += $"Parameter Name: {ex.ParamName ?? "Unknown"}\n";
        analysis += $"Source: {ex.Source}\n\n";

        if (ex.Source?.Contains("WindowsBase") == true || ex.Source?.Contains("PresentationFramework") == true)
        {
            analysis += "WPF-SPECIFIC RECOMMENDATIONS:\n";
            analysis += "1. Check data binding expressions for syntax errors\n";
            analysis += "2. Verify bound properties exist and are accessible\n";
            analysis += "3. Ensure ViewModels implement INotifyPropertyChanged correctly\n";
            analysis += "4. Check for circular binding references\n";
            analysis += "5. Validate converter implementations\n";
        }
        else
        {
            analysis += "GENERAL RECOMMENDATIONS:\n";
            analysis += "1. Validate input parameters before method calls\n";
            analysis += "2. Check for empty or invalid string values\n";
            analysis += "3. Verify configuration settings are properly formatted\n";
            analysis += "4. Ensure required dependencies are initialized\n";
        }

        return analysis;
    }

    /// <summary>
    /// Comprehensive exception analyzer that handles multiple exception types
    /// </summary>
    public static string AnalyzeException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex, nameof(ex));

        var analysis = $"Exception Analysis Report\n";
        analysis += $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
        analysis += new string('=', 50) + "\n\n";

        switch (ex)
        {
            case SqlException sqlEx:
                analysis += AnalyzeSqlException(sqlEx);
                break;

            case DbUpdateException dbEx:
                analysis += AnalyzeEfException(dbEx);
                break;

            case InvalidCastException castEx:
                analysis += AnalyzeInvalidCastException(castEx);
                break;

            case ArgumentException argEx:
                analysis += AnalyzeArgumentException(argEx);
                break;

            default:
                analysis += $"General Exception Analysis:\n";
                analysis += $"Type: {ex.GetType().Name}\n";
                analysis += $"Message: {ex.Message}\n";
                analysis += $"Source: {ex.Source}\n";

                if (ex.InnerException != null)
                {
                    analysis += $"\nInner Exception:\n";
                    analysis += AnalyzeException(ex.InnerException);
                }
                break;
        }

        analysis += $"\nStack Trace:\n{ex.StackTrace}\n";
        return analysis;
    }

    /// <summary>
    /// Safe method to execute database operations with automatic retry and detailed error reporting
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
    {
        ArgumentNullException.ThrowIfNull(operation, nameof(operation));

        Exception lastException = new InvalidOperationException("Retry operation failed");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (SqlException sqlEx) when (IsTransientError(sqlEx) && attempt < maxRetries)
            {
                lastException = sqlEx;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                await Task.Delay(delay);
                continue;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == maxRetries)
                {
                    var analysis = AnalyzeException(ex);
                    throw new InvalidOperationException($"Operation failed after {maxRetries} attempts.\n\n{analysis}", ex);
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed with unknown error");
    }

    /// <summary>
    /// Determines if a SQL exception is transient (temporary) and worth retrying
    /// </summary>
    private static bool IsTransientError(SqlException sqlException)
    {
        // Common transient error numbers in SQL Server
        var transientErrors = new[]
        {
            -2,    // Timeout
            20,    // Instance unavailable
            64,    // Connection lost
            233,   // Connection init error
            10053, // Transport error
            10054, // Connection reset
            10060, // Network unreachable
            40197, // Service unavailable
            40501, // Service busy
            40613  // Database unavailable
        };

        return transientErrors.Contains(sqlException.Number);
    }
}
