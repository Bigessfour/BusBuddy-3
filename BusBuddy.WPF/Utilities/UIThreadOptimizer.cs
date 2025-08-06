using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using Serilog.Context;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Extensions
{
    /// <summary>
    /// Extensions for UI thread relief and performance optimization
    /// </summary>
    public static class UIThreadOptimizer
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(UIThreadOptimizer));

        /// <summary>
        /// Execute an operation on the UI thread with performance tracking
        /// </summary>
        public static async Task<T> ExecuteOnUIThreadAsync<T>(string operationName, Func<T> operation)
        {
            using (LogContext.PushProperty("Threading", "UIThread"))
            using (LogContext.PushProperty("Operation", operationName))
            {
                PerformanceOptimizer.StartTiming($"UIThread_{operationName}");

                try
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        // Already on UI thread
                        Logger.Debug("Executing {OperationName} on current UI thread", operationName);
                        return operation();
                    }
                    else
                    {
                        // Need to invoke on UI thread
                        Logger.Debug("Dispatching {OperationName} to UI thread", operationName);
                        return await Application.Current.Dispatcher.InvokeAsync(operation, DispatcherPriority.Normal);
                    }
                }
                finally
                {
                    PerformanceOptimizer.StopTiming($"UIThread_{operationName}");
                }
            }
        }

        /// <summary>
        /// Execute an async operation on the UI thread with performance tracking
        /// </summary>
        public static async Task<T> ExecuteOnUIThreadAsync<T>(string operationName, Func<Task<T>> operation)
        {
            using (LogContext.PushProperty("Threading", "UIThreadAsync"))
            using (LogContext.PushProperty("Operation", operationName))
            {
                PerformanceOptimizer.StartTiming($"UIThreadAsync_{operationName}");

                try
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        // Already on UI thread
                        Logger.Debug("Executing async {OperationName} on current UI thread", operationName);
                        return await operation();
                    }
                    else
                    {
                        // Need to invoke on UI thread
                        Logger.Debug("Dispatching async {OperationName} to UI thread", operationName);
                        return await Application.Current.Dispatcher.InvokeAsync(operation, DispatcherPriority.Normal).Result;
                    }
                }
                finally
                {
                    PerformanceOptimizer.StopTiming($"UIThreadAsync_{operationName}");
                }
            }
        }

        /// <summary>
        /// Execute an operation off the UI thread with automatic marshalling back if needed
        /// </summary>
        public static async Task<T> ExecuteOffUIThreadWithResultAsync<T>(string operationName, Func<T> operation, bool marshalResult = false)
        {
            using (LogContext.PushProperty("Threading", "BackgroundWithResult"))
            using (LogContext.PushProperty("Operation", operationName))
            {
                Logger.Debug("Executing {OperationName} on background thread", operationName);

                var result = await PerformanceOptimizer.ExecuteOffUIThreadAsync(operationName, operation);

                if (marshalResult)
                {
                    // If marshalling is requested, ensure we return on UI thread
                    return await ExecuteOnUIThreadAsync($"Marshal_{operationName}", () => result);
                }

                return result;
            }
        }

        /// <summary>
        /// Debounce UI operations to prevent excessive calls
        /// </summary>
        public static void DebounceUIOperation(string operationName, Action operation, TimeSpan delay)
        {
            // Simple implementation - could be enhanced with more sophisticated debouncing
            var timer = new DispatcherTimer
            {
                Interval = delay
            };

            timer.Tick += (s, e) =>
            {
                timer.Stop();

                using (LogContext.PushProperty("Operation", $"Debounced_{operationName}"))
                {
                    Logger.Debug("Executing debounced operation {OperationName}", operationName);

                    try
                    {
                        operation();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Debounced operation {OperationName} failed", operationName);
                    }
                }
            };

            timer.Start();
        }

        /// <summary>
        /// Execute initialization with lazy loading and background data fetching
        /// </summary>
        public static async Task<T> ExecuteLazyInitializationAsync<T>(
            string operationName,
            Func<T> immediateInit,
            Func<Task>? backgroundInit = null) where T : class
        {
            using (LogContext.PushProperty("Pattern", "LazyInitialization"))
            using (LogContext.PushProperty("Operation", operationName))
            {
                Logger.Information("Starting lazy initialization for {OperationName}", operationName);

                // Immediate initialization (fast, minimal)
                var result = await ExecuteOnUIThreadAsync($"Immediate_{operationName}", immediateInit);

                // Background initialization (heavy operations)
                if (backgroundInit != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Logger.Debug("Starting background initialization for {OperationName}", operationName);
                            await backgroundInit();
                            Logger.Information("✅ Background initialization completed for {OperationName}", operationName);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "❌ Background initialization failed for {OperationName}", operationName);
                        }
                    });
                }

                return result;
            }
        }

        /// <summary>
        /// Create a performance-optimized timer that runs operations off the UI thread
        /// </summary>
        public static DispatcherTimer CreateOptimizedTimer(
            string timerName,
            TimeSpan interval,
            Func<Task> operation,
            bool runOnUIThread = false)
        {
            var timer = new DispatcherTimer
            {
                Interval = interval
            };

            timer.Tick += async (s, e) =>
            {
                using (LogContext.PushProperty("Timer", timerName))
                {
                    Logger.Debug("Timer {TimerName} tick executing", timerName);

                    try
                    {
                        if (runOnUIThread)
                        {
                            await operation();
                        }
                        else
                        {
                            // Run off UI thread for better performance
                            await PerformanceOptimizer.ExecuteOffUIThreadAsync($"Timer_{timerName}", async () =>
                            {
                                await operation();
                                return Task.CompletedTask;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Timer {TimerName} operation failed", timerName);
                    }
                }
            };

            Logger.Information("Created optimized timer {TimerName} with {IntervalMs}ms interval",
                             timerName, interval.TotalMilliseconds);

            return timer;
        }
    }
}
