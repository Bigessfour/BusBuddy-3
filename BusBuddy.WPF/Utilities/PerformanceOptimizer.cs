using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Comprehensive performance monitoring and optimization utility
    /// Provides startup timing, UI thread relief, resource usage tracking, and performance metrics
    /// </summary>
    public static class PerformanceOptimizer
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(PerformanceOptimizer));
        private static readonly ConcurrentDictionary<string, Stopwatch> _activeTimers = new();
        private static readonly ConcurrentDictionary<string, List<TimeSpan>> _performanceMetrics = new();
        private static readonly object _metricsLock = new object();

        // Performance targets
        public const int TARGET_DASHBOARD_LOAD_MS = 500;
        public const int TARGET_VIEW_SWITCH_MS = 200;
        public const int TARGET_STARTUP_MS = 3000;

        // Background task management
        private static readonly SemaphoreSlim _backgroundTaskSemaphore = new(Environment.ProcessorCount * 2);
        private static readonly CancellationTokenSource _globalCancellationSource = new();

        /// <summary>
        /// Start timing a performance-critical operation
        /// </summary>
        public static void StartTiming(string operationName)
        {
            var stopwatch = Stopwatch.StartNew();
            _activeTimers.AddOrUpdate(operationName, stopwatch, (key, existing) =>
            {
                existing.Stop();
                Logger.Warning("Timer {OperationName} was already running, restarting", operationName);
                return stopwatch;
            });

            Logger.Debug("Started timing operation: {OperationName}", operationName);
        }

        /// <summary>
        /// Stop timing and record performance metrics
        /// </summary>
        public static TimeSpan StopTiming(string operationName)
        {
            if (_activeTimers.TryRemove(operationName, out var stopwatch))
            {
                stopwatch.Stop();
                var elapsed = stopwatch.Elapsed;

                // Record the metric
                RecordMetric(operationName, elapsed);

                // Log with performance assessment
                var performance = AssessPerformance(operationName, elapsed);
                using (LogContext.PushProperty("PerformanceAssessment", performance))
                using (LogContext.PushProperty("ElapsedMs", elapsed.TotalMilliseconds))
                {
                    if (performance == "Excellent" || performance == "Good")
                    {
                        Logger.Information("✅ Operation {OperationName} completed in {ElapsedMs}ms ({Performance})",
                                         operationName, elapsed.TotalMilliseconds, performance);
                    }
                    else if (performance == "Acceptable")
                    {
                        Logger.Warning("⚠️ Operation {OperationName} completed in {ElapsedMs}ms ({Performance})",
                                     operationName, elapsed.TotalMilliseconds, performance);
                    }
                    else
                    {
                        Logger.Error("❌ Operation {OperationName} completed in {ElapsedMs}ms ({Performance})",
                                   operationName, elapsed.TotalMilliseconds, performance);
                    }
                }

                return elapsed;
            }

            Logger.Warning("Timer {OperationName} was not found or already stopped", operationName);
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Execute a timed operation with automatic performance tracking
        /// </summary>
        public static async Task<T> ExecuteTimedAsync<T>(string operationName, Func<Task<T>> operation)
        {
            StartTiming(operationName);
            try
            {
                var result = await operation();
                return result;
            }
            finally
            {
                StopTiming(operationName);
            }
        }

        /// <summary>
        /// Execute a timed operation with automatic performance tracking (synchronous)
        /// </summary>
        public static T ExecuteTimed<T>(string operationName, Func<T> operation)
        {
            StartTiming(operationName);
            try
            {
                return operation();
            }
            finally
            {
                StopTiming(operationName);
            }
        }

        /// <summary>
        /// Execute multiple cache warm-up operations in parallel
        /// </summary>
        public static async Task WarmupCachesParallelAsync(IEnumerable<Func<Task>> warmupOperations)
        {
            using (LogContext.PushProperty("Operation", "CacheWarmup"))
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.Information("Starting parallel cache warm-up operations");

                try
                {
                    var tasks = warmupOperations.Select(async operation =>
                    {
                        await _backgroundTaskSemaphore.WaitAsync(_globalCancellationSource.Token);
                        try
                        {
                            await operation();
                        }
                        finally
                        {
                            _backgroundTaskSemaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                    stopwatch.Stop();

                    Logger.Information("✅ Parallel cache warm-up completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "❌ Cache warm-up failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }
            }
        }

        /// <summary>
        /// Move heavy operations off the UI thread with performance tracking
        /// </summary>
        public static async Task<T> ExecuteOffUIThreadAsync<T>(string operationName, Func<T> operation)
        {
            using (LogContext.PushProperty("Threading", "Background"))
            using (LogContext.PushProperty("Operation", operationName))
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.Debug("Moving operation {OperationName} to background thread", operationName);

                try
                {
                    await _backgroundTaskSemaphore.WaitAsync(_globalCancellationSource.Token);

                    var result = await Task.Run(() =>
                    {
                        var threadId = Environment.CurrentManagedThreadId;
                        Logger.Debug("Executing {OperationName} on background thread {ThreadId}", operationName, threadId);

                        return operation();
                    }, _globalCancellationSource.Token);

                    stopwatch.Stop();
                    RecordMetric($"Background_{operationName}", stopwatch.Elapsed);

                    Logger.Information("✅ Background operation {OperationName} completed in {ElapsedMs}ms",
                                     operationName, stopwatch.ElapsedMilliseconds);

                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "❌ Background operation {OperationName} failed after {ElapsedMs}ms",
                               operationName, stopwatch.ElapsedMilliseconds);
                    throw;
                }
                finally
                {
                    _backgroundTaskSemaphore.Release();
                }
            }
        }

        /// <summary>
        /// Simulate high-load conditions for testing
        /// </summary>
        public static async Task SimulateHighLoadAsync(string operationName, Func<Task> operation, int iterations = 100)
        {
            using (LogContext.PushProperty("LoadTest", "HighLoad"))
            using (LogContext.PushProperty("Operation", operationName))
            {
                Logger.Information("Starting high-load simulation for {OperationName} with {Iterations} iterations",
                                 operationName, iterations);

                var overallStopwatch = Stopwatch.StartNew();
                var results = new List<TimeSpan>();
                var failures = 0;

                using var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrency

                var tasks = Enumerable.Range(0, iterations).Select(async i =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var iterationStopwatch = Stopwatch.StartNew();
                        await operation();
                        iterationStopwatch.Stop();

                        lock (results)
                        {
                            results.Add(iterationStopwatch.Elapsed);
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failures);
                        Logger.Warning(ex, "High-load test iteration {Iteration} failed", i);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
                overallStopwatch.Stop();

                // Calculate statistics
                if (results.Count > 0)
                {
                    var avgMs = results.Average(r => r.TotalMilliseconds);
                    var minMs = results.Min(r => r.TotalMilliseconds);
                    var maxMs = results.Max(r => r.TotalMilliseconds);
                    var p95Ms = results.OrderBy(r => r.TotalMilliseconds).Skip((int)(results.Count * 0.95)).First().TotalMilliseconds;

                    Logger.Information("🔥 High-load test results for {OperationName}: " +
                                     "Total: {TotalMs}ms, Avg: {AvgMs:F2}ms, Min: {MinMs:F2}ms, Max: {MaxMs:F2}ms, " +
                                     "P95: {P95Ms:F2}ms, Success: {Success}/{Total}, Failures: {Failures}",
                                     operationName, overallStopwatch.ElapsedMilliseconds, avgMs, minMs, maxMs, p95Ms,
                                     results.Count, iterations, failures);

                    // Check for memory leaks (simplified check)
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    var memoryAfter = GC.GetTotalMemory(false);
                    Logger.Information("📊 Memory after high-load test: {MemoryMB:F2} MB", memoryAfter / 1024.0 / 1024.0);
                }
                else
                {
                    Logger.Error("❌ All high-load test iterations failed for {OperationName}", operationName);
                }
            }
        }

        /// <summary>
        /// Get performance statistics for an operation
        /// </summary>
        public static PerformanceStats GetPerformanceStats(string operationName)
        {
            lock (_metricsLock)
            {
                if (_performanceMetrics.TryGetValue(operationName, out var metrics) && metrics.Count > 0)
                {
                    return new PerformanceStats
                    {
                        OperationName = operationName,
                        Count = metrics.Count,
                        AverageMs = metrics.Average(m => m.TotalMilliseconds),
                        MinMs = metrics.Min(m => m.TotalMilliseconds),
                        MaxMs = metrics.Max(m => m.TotalMilliseconds),
                        LastMs = metrics.Last().TotalMilliseconds
                    };
                }
            }

            return new PerformanceStats { OperationName = operationName };
        }

        /// <summary>
        /// Generate a comprehensive performance report
        /// </summary>
        public static string GeneratePerformanceReport()
        {
            using (LogContext.PushProperty("Report", "Performance"))
            {
                var report = new System.Text.StringBuilder();
                report.AppendLine("📊 Performance Monitoring Report");
                report.AppendLine("================================");
                report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine();

                lock (_metricsLock)
                {
                    if (_performanceMetrics.Count == 0)
                    {
                        report.AppendLine("No performance metrics recorded.");
                        return report.ToString();
                    }

                    foreach (var kvp in _performanceMetrics.OrderBy(k => k.Key))
                    {
                        var stats = GetPerformanceStats(kvp.Key);
                        var assessment = AssessPerformance(kvp.Key, TimeSpan.FromMilliseconds(stats.AverageMs));

                        report.AppendLine($"Operation: {kvp.Key}");
                        report.AppendLine($"  Count: {stats.Count}");
                        report.AppendLine($"  Average: {stats.AverageMs:F2}ms ({assessment})");
                        report.AppendLine($"  Min: {stats.MinMs:F2}ms");
                        report.AppendLine($"  Max: {stats.MaxMs:F2}ms");
                        report.AppendLine($"  Last: {stats.LastMs:F2}ms");
                        report.AppendLine();
                    }
                }

                var totalOperations = _performanceMetrics.Values.Sum(v => v.Count);
                report.AppendLine($"Total Operations Tracked: {totalOperations}");

                Logger.Information("Generated performance report with {OperationCount} tracked operations",
                                 _performanceMetrics.Count);

                return report.ToString();
            }
        }

        #region Private Helper Methods

        private static void RecordMetric(string operationName, TimeSpan elapsed)
        {
            lock (_metricsLock)
            {
                if (!_performanceMetrics.ContainsKey(operationName))
                {
                    _performanceMetrics[operationName] = new List<TimeSpan>();
                }

                _performanceMetrics[operationName].Add(elapsed);

                // Keep only last 100 measurements to prevent memory growth
                if (_performanceMetrics[operationName].Count > 100)
                {
                    _performanceMetrics[operationName].RemoveAt(0);
                }
            }
        }

        private static string AssessPerformance(string operationName, TimeSpan elapsed)
        {
            var thresholds = operationName.ToLowerInvariant() switch
            {
                var name when name.Contains("dashboard") || name.Contains("load") =>
                    (Excellent: 200, Good: 500, Acceptable: 1000),
                var name when name.Contains("view") || name.Contains("switch") || name.Contains("navigate") =>
                    (Excellent: 100, Good: 200, Acceptable: 500),
                var name when name.Contains("startup") || name.Contains("initialize") =>
                    (Excellent: 1000, Good: 3000, Acceptable: 5000),
                _ => (Excellent: 100, Good: 250, Acceptable: 500)
            };

            var ms = elapsed.TotalMilliseconds;
            return ms switch
            {
                var x when x <= thresholds.Excellent => "Excellent",
                var x when x <= thresholds.Good => "Good",
                var x when x <= thresholds.Acceptable => "Acceptable",
                _ => "Needs Improvement"
            };
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Clean up resources and cancel background operations
        /// </summary>
        public static void Shutdown()
        {
            using (LogContext.PushProperty("Operation", "PerformanceOptimizerShutdown"))
            {
                Logger.Information("Shutting down performance optimizer");

                _globalCancellationSource.Cancel();
                _backgroundTaskSemaphore.Dispose();

                // Log final performance summary
                var report = GeneratePerformanceReport();
                Logger.Information("Final performance report:\n{Report}", report);
            }
        }

        #endregion
    }

    /// <summary>
    /// Performance statistics for an operation
    /// </summary>
    public class PerformanceStats
    {
        public string OperationName { get; set; } = string.Empty;
        public int Count { get; set; }
        public double AverageMs { get; set; }
        public double MinMs { get; set; }
        public double MaxMs { get; set; }
        public double LastMs { get; set; }
    }
}
