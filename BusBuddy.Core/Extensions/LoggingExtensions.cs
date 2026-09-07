using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace BusBuddy.Core.Utilities
{
    /// <summary>
    /// Provides extension methods for enhanced logging functionality
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Measures and logs the execution time of an operation
        /// </summary>
        public static T TrackPerformance<T>(this ILogger logger, string operationName, Func<T> operation,
            string? contextName = null, object? contextValue = null)
        {
            using (LogContext.PushProperty("OperationName", operationName))
            {
                if (contextName != null && contextValue != null)
                {
                    using (LogContext.PushProperty(contextName, contextValue))
                    {
                        return ExecuteWithTracking(logger, operationName, operation);
                    }
                }
                else
                {
                    return ExecuteWithTracking(logger, operationName, operation);
                }
            }
        }

        /// <summary>
        /// Measures and logs the execution time of an asynchronous operation
        /// </summary>
        public static async Task<T> TrackPerformanceAsync<T>(this ILogger logger, string operationName,
            Func<Task<T>> asyncOperation, string? contextName = null, object? contextValue = null)
        {
            using (LogContext.PushProperty("OperationName", operationName))
            {
                if (contextName != null && contextValue != null)
                {
                    using (LogContext.PushProperty(contextName, contextValue))
                    {
                        return await ExecuteWithTrackingAsync(logger, operationName, asyncOperation);
                    }
                }
                else
                {
                    return await ExecuteWithTrackingAsync(logger, operationName, asyncOperation);
                }
            }
        }

        /// <summary>
        /// Helper method to execute and track a synchronous operation
        /// </summary>
        private static T ExecuteWithTracking<T>(ILogger logger, string operationName, Func<T> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = operation();
                stopwatch.Stop();

                using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                {
                    logger.Information("{OperationName} completed in {Duration}ms",
                        operationName, stopwatch.ElapsedMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                {
                    logger.Error(ex, "{OperationName} failed after {Duration}ms",
                        operationName, stopwatch.ElapsedMilliseconds);
                }

                throw;
            }
        }

        /// <summary>
        /// Helper method to execute and track an asynchronous operation
        /// </summary>
        private static async Task<T> ExecuteWithTrackingAsync<T>(ILogger logger, string operationName, Func<Task<T>> asyncOperation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await asyncOperation();
                stopwatch.Stop();

                using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                {
                    logger.Information("{OperationName} completed in {Duration}ms",
                        operationName, stopwatch.ElapsedMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                {
                    logger.Error(ex, "{OperationName} failed after {Duration}ms",
                        operationName, stopwatch.ElapsedMilliseconds);
                }

                throw;
            }
        }
    }
}
