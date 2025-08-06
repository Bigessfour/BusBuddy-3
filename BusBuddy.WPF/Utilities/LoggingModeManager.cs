using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Manages logging overhead and performance modes for BusBuddy development cycles
    /// Provides toggleable "light" modes to minimize performance impact during quick iterations
    /// </summary>
    public static class LoggingModeManager
    {
        private static readonly Serilog.ILogger Logger = Log.ForContext(typeof(LoggingModeManager));
        private static readonly ConcurrentDictionary<string, LoggingMode> _activeContexts = new();
        private static LoggingMode _globalMode = LoggingMode.Standard;
        private static bool _isInitialized;

        // Performance counters
        private static readonly ConcurrentDictionary<string, long> _operationCounts = new();
        private static readonly ConcurrentDictionary<string, double> _totalDurations = new();

        /// <summary>
        /// Available logging modes with different performance characteristics
        /// </summary>
        public enum LoggingMode
        {
            /// <summary>Light mode — minimal logging, maximum performance for quick cycles</summary>
            Light = 0,
            /// <summary>Essential mode — only critical errors and warnings</summary>
            Essential = 1,
            /// <summary>Standard mode — balanced logging for normal development</summary>
            Standard = 2,
            /// <summary>Detailed mode — comprehensive logging for debugging</summary>
            Detailed = 3,
            /// <summary>Diagnostic mode — maximum logging for issue investigation</summary>
            Diagnostic = 4
        }

        /// <summary>
        /// Configuration for each logging mode
        /// </summary>
        public static class ModeConfiguration
        {
            public static readonly Dictionary<LoggingMode, LogModeConfig> Modes = new()
            {
                [LoggingMode.Light] = new LogModeConfig
                {
                    MinimumLevel = LogEventLevel.Error,
                    EnableUILogging = false,
                    EnablePerformanceLogging = false,
                    EnableSyncfusionLogging = false,
                    EnableDatabaseLogging = false,
                    EnableDebugHelper = false,
                    MaxLogFileSize = 1024 * 1024, // 1MB
                    RetainedFileCount = 3,
                    Description = "Light mode — minimal overhead for quick dev cycles"
                },
                [LoggingMode.Essential] = new LogModeConfig
                {
                    MinimumLevel = LogEventLevel.Warning,
                    EnableUILogging = false,
                    EnablePerformanceLogging = false,
                    EnableSyncfusionLogging = false,
                    EnableDatabaseLogging = false,
                    EnableDebugHelper = false,
                    MaxLogFileSize = 2 * 1024 * 1024, // 2MB
                    RetainedFileCount = 5,
                    Description = "Essential mode — errors and warnings only"
                },
                [LoggingMode.Standard] = new LogModeConfig
                {
                    MinimumLevel = LogEventLevel.Information,
                    EnableUILogging = true,
                    EnablePerformanceLogging = true,
                    EnableSyncfusionLogging = false,
                    EnableDatabaseLogging = true,
                    EnableDebugHelper = false,
                    MaxLogFileSize = 5 * 1024 * 1024, // 5MB
                    RetainedFileCount = 7,
                    Description = "Standard mode — balanced logging for normal development"
                },
                [LoggingMode.Detailed] = new LogModeConfig
                {
                    MinimumLevel = LogEventLevel.Debug,
                    EnableUILogging = true,
                    EnablePerformanceLogging = true,
                    EnableSyncfusionLogging = true,
                    EnableDatabaseLogging = true,
                    EnableDebugHelper = true,
                    MaxLogFileSize = 10 * 1024 * 1024, // 10MB
                    RetainedFileCount = 14,
                    Description = "Detailed mode — comprehensive logging for debugging"
                },
                [LoggingMode.Diagnostic] = new LogModeConfig
                {
                    MinimumLevel = LogEventLevel.Verbose,
                    EnableUILogging = true,
                    EnablePerformanceLogging = true,
                    EnableSyncfusionLogging = true,
                    EnableDatabaseLogging = true,
                    EnableDebugHelper = true,
                    MaxLogFileSize = 20 * 1024 * 1024, // 20MB
                    RetainedFileCount = 30,
                    Description = "Diagnostic mode — maximum logging for issue investigation"
                }
            };
        }

        /// <summary>
        /// Configuration for a specific logging mode
        /// </summary>
        public class LogModeConfig
        {
            public LogEventLevel MinimumLevel { get; set; }
            public bool EnableUILogging { get; set; }
            public bool EnablePerformanceLogging { get; set; }
            public bool EnableSyncfusionLogging { get; set; }
            public bool EnableDatabaseLogging { get; set; }
            public bool EnableDebugHelper { get; set; }
            public long MaxLogFileSize { get; set; }
            public int RetainedFileCount { get; set; }
            public string Description { get; set; } = string.Empty;
        }

        /// <summary>
        /// Initialize the logging mode manager
        /// </summary>
        public static void Initialize(LoggingMode initialMode = LoggingMode.Standard)
        {
            if (_isInitialized)
            {
                return;
            }


            _globalMode = initialMode;
            _isInitialized = true;

            // Update existing configurations
            ApplyModeConfiguration(_globalMode);

            Logger.Information("🎛️ Logging Mode Manager initialized with {Mode} mode", _globalMode);
            LogModeChange("System", LoggingMode.Standard, _globalMode, "Initialization");
        }

        /// <summary>
        /// Set the global logging mode
        /// </summary>
        /// <param name="mode">New logging mode</param>
        /// <param name="context">Context for the mode change</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetGlobalMode(LoggingMode mode, string context = "Manual")
        {
            var previousMode = _globalMode;
            _globalMode = mode;

            ApplyModeConfiguration(mode);
            LogModeChange("Global", previousMode, mode, context);

            // Clear performance counters when switching to/from light mode
            if (mode == LoggingMode.Light || previousMode == LoggingMode.Light)
            {
                ClearPerformanceCounters();
            }
        }

        /// <summary>
        /// Get the current global logging mode
        /// </summary>
        public static LoggingMode GetGlobalMode() => _globalMode;

        /// <summary>
        /// Get configuration for the current global mode
        /// </summary>
        public static LogModeConfig GetCurrentConfig() => ModeConfiguration.Modes[_globalMode];

        /// <summary>
        /// Set a context-specific logging mode (overrides global for specific operations)
        /// </summary>
        /// <param name="contextId">Unique identifier for the context</param>
        /// <param name="mode">Logging mode for this context</param>
        public static void SetContextMode(string contextId, LoggingMode mode)
        {
            _activeContexts.AddOrUpdate(contextId, mode, (key, oldValue) => mode);
            Logger.Debug("📍 Context {ContextId} logging mode set to {Mode}", contextId, mode);
        }

        /// <summary>
        /// Remove a context-specific logging mode
        /// </summary>
        /// <param name="contextId">Context identifier to remove</param>
        public static void RemoveContextMode(string contextId)
        {
            _activeContexts.TryRemove(contextId, out _);
            Logger.Debug("📍 Context {ContextId} logging mode removed", contextId);
        }

        /// <summary>
        /// Get the effective logging mode for a given context
        /// </summary>
        /// <param name="contextId">Context identifier</param>
        /// <returns>Effective logging mode (context-specific or global)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LoggingMode GetEffectiveMode(string? contextId = null)
        {
            if (!string.IsNullOrEmpty(contextId) && _activeContexts.TryGetValue(contextId, out var contextMode))
            {
                return contextMode;
            }
            return _globalMode;
        }

        /// <summary>
        /// Check if logging is enabled for a specific level and context
        /// </summary>
        /// <param name="level">Log event level to check</param>
        /// <param name="contextId">Optional context identifier</param>
        /// <returns>True if logging is enabled for this level</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEnabled(LogEventLevel level, string? contextId = null)
        {
            var effectiveMode = GetEffectiveMode(contextId);
            var config = ModeConfiguration.Modes[effectiveMode];
            return level >= config.MinimumLevel;
        }

        /// <summary>
        /// Check if UI logging is enabled for the current mode
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUILoggingEnabled(string? contextId = null)
        {
            var effectiveMode = GetEffectiveMode(contextId);
            return ModeConfiguration.Modes[effectiveMode].EnableUILogging;
        }

        /// <summary>
        /// Check if performance logging is enabled for the current mode
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPerformanceLoggingEnabled(string? contextId = null)
        {
            var effectiveMode = GetEffectiveMode(contextId);
            return ModeConfiguration.Modes[effectiveMode].EnablePerformanceLogging;
        }

        /// <summary>
        /// Check if Syncfusion logging is enabled for the current mode
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSyncfusionLoggingEnabled(string? contextId = null)
        {
            var effectiveMode = GetEffectiveMode(contextId);
            return ModeConfiguration.Modes[effectiveMode].EnableSyncfusionLogging;
        }

        /// <summary>
        /// Check if database logging is enabled for the current mode
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDatabaseLoggingEnabled(string? contextId = null)
        {
            var effectiveMode = GetEffectiveMode(contextId);
            return ModeConfiguration.Modes[effectiveMode].EnableDatabaseLogging;
        }

        /// <summary>
        /// Check if debug helper is enabled for the current mode
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDebugHelperEnabled(string? contextId = null)
        {
            var effectiveMode = GetEffectiveMode(contextId);
            return ModeConfiguration.Modes[effectiveMode].EnableDebugHelper;
        }

        /// <summary>
        /// Track performance impact of logging operations
        /// </summary>
        /// <param name="operationType">Type of operation being tracked</param>
        /// <param name="duration">Duration of the operation in milliseconds</param>
        public static void TrackPerformanceImpact(string operationType, double duration)
        {
            if (_globalMode == LoggingMode.Light)
            {
                return; // Skip tracking in light mode
            }


            _operationCounts.AddOrUpdate(operationType, 1, (key, count) => count + 1);
            _totalDurations.AddOrUpdate(operationType, duration, (key, total) => total + duration);
        }

        /// <summary>
        /// Get performance statistics for logging operations
        /// </summary>
        /// <returns>Dictionary of operation statistics</returns>
        public static Dictionary<string, PerformanceStats> GetPerformanceStats()
        {
            var stats = new Dictionary<string, PerformanceStats>();

            foreach (var kvp in _operationCounts)
            {
                var operationType = kvp.Key;
                var count = kvp.Value;
                var totalDuration = _totalDurations.GetValueOrDefault(operationType, 0);

                stats[operationType] = new PerformanceStats
                {
                    OperationType = operationType,
                    Count = count,
                    TotalDuration = totalDuration,
                    AverageDuration = count > 0 ? totalDuration / count : 0,
                    LastUpdated = DateTime.Now
                };
            }

            return stats;
        }

        /// <summary>
        /// Clear all performance counters
        /// </summary>
        public static void ClearPerformanceCounters()
        {
            _operationCounts.Clear();
            _totalDurations.Clear();
            Logger.Debug("🧹 Performance counters cleared");
        }

        /// <summary>
        /// Create a lightweight logging scope that respects the current mode
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="contextId">Optional context identifier</param>
        /// <returns>Disposable logging scope</returns>
        public static IDisposable CreateLoggingScope(string operationName, string? contextId = null)
        {
            return new LoggingScope(operationName, contextId);
        }

        /// <summary>
        /// Create a conditional logger that only logs if the current mode allows it
        /// </summary>
        /// <typeparam name="T">Type for the logger context</typeparam>
        /// <param name="contextId">Optional context identifier</param>
        /// <returns>Conditional logger instance</returns>
        public static ConditionalLogger<T> CreateConditionalLogger<T>(string? contextId = null)
        {
            return new ConditionalLogger<T>(contextId);
        }

        /// <summary>
        /// Quick mode switching for development workflows
        /// </summary>
        public static class QuickModes
        {
            /// <summary>Switch to light mode for quick development cycles</summary>
            public static void EnableLightMode() => SetGlobalMode(LoggingMode.Light, "Quick-Light");

            /// <summary>Switch to essential mode for minimal logging</summary>
            public static void EnableEssentialMode() => SetGlobalMode(LoggingMode.Essential, "Quick-Essential");

            /// <summary>Switch to standard mode for normal development</summary>
            public static void EnableStandardMode() => SetGlobalMode(LoggingMode.Standard, "Quick-Standard");

            /// <summary>Switch to detailed mode for debugging</summary>
            public static void EnableDetailedMode() => SetGlobalMode(LoggingMode.Detailed, "Quick-Detailed");

            /// <summary>Switch to diagnostic mode for investigation</summary>
            public static void EnableDiagnosticMode() => SetGlobalMode(LoggingMode.Diagnostic, "Quick-Diagnostic");
        }

        /// <summary>
        /// Apply configuration for the specified logging mode
        /// </summary>
        private static void ApplyModeConfiguration(LoggingMode mode)
        {
            var config = ModeConfiguration.Modes[mode];

#if DEBUG
            // Update DebugConfig based on the mode (only available in DEBUG builds)
            try
            {
                DebugConfig.EnableVerboseLogging = config.EnableUILogging;
                DebugConfig.EnablePerformanceTracking = config.EnablePerformanceLogging;
                DebugConfig.EnableUITracking = config.EnableUILogging;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to update DebugConfig for mode {Mode}", mode);
            }
#endif

            Logger.Debug("⚙️ Applied configuration for {Mode} mode: MinLevel={MinLevel}, UI={UI}, Perf={Perf}",
                mode, config.MinimumLevel, config.EnableUILogging, config.EnablePerformanceLogging);
        }

        /// <summary>
        /// Log mode changes for audit purposes
        /// </summary>
        private static void LogModeChange(string scope, LoggingMode fromMode, LoggingMode toMode, string context)
        {
            var fromConfig = ModeConfiguration.Modes[fromMode];
            var toConfig = ModeConfiguration.Modes[toMode];

            Logger.Information("🎛️ Logging mode changed: {Scope} {FromMode} → {ToMode} ({Context})",
                scope, fromMode, toMode, context);

            // Only log detailed information if not in light mode
            if (toMode != LoggingMode.Light)
            {
                Logger.Debug("   From: {FromDescription}", fromConfig.Description);
                Logger.Debug("   To: {ToDescription}", toConfig.Description);
                Logger.Debug("   Impact: MinLevel {FromLevel} → {ToLevel}, Files {FromFiles} → {ToFiles}",
                    fromConfig.MinimumLevel, toConfig.MinimumLevel,
                    fromConfig.RetainedFileCount, toConfig.RetainedFileCount);
            }
        }

        /// <summary>
        /// Performance statistics for logging operations
        /// </summary>
        public class PerformanceStats
        {
            public string OperationType { get; set; } = string.Empty;
            public long Count { get; set; }
            public double TotalDuration { get; set; }
            public double AverageDuration { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        /// <summary>
        /// Lightweight logging scope implementation
        /// </summary>
        private sealed class LoggingScope : IDisposable
        {
            private readonly string _operationName;
            private readonly string? _contextId;
            private readonly Stopwatch _stopwatch;
            private readonly bool _shouldLog;
            private bool _disposed;

            public LoggingScope(string operationName, string? contextId)
            {
                _operationName = operationName;
                _contextId = contextId;
                _stopwatch = Stopwatch.StartNew();
                _shouldLog = IsPerformanceLoggingEnabled(contextId);

                if (_shouldLog)
                {
                    Logger.Debug("🔄 Started: {OperationName}", _operationName);
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }


                _stopwatch.Stop();

                if (_shouldLog)
                {
                    var duration = _stopwatch.Elapsed.TotalMilliseconds;
                    Logger.Debug("✅ Completed: {OperationName} in {Duration:F2}ms", _operationName, duration);
                    TrackPerformanceImpact(_operationName, duration);
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Conditional logger that respects the current logging mode
        /// </summary>
        public class ConditionalLogger<T>
        {
            private readonly Serilog.ILogger _logger;
            private readonly string? _contextId;

            public ConditionalLogger(string? contextId)
            {
                _logger = Log.ForContext<T>();
                _contextId = contextId;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void LogVerbose(string messageTemplate, params object[] propertyValues)
            {
                if (IsEnabled(LogEventLevel.Verbose, _contextId))
                {
                    _logger.Verbose(messageTemplate, propertyValues);
                }

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void LogDebug(string messageTemplate, params object[] propertyValues)
            {
                if (IsEnabled(LogEventLevel.Debug, _contextId))
                {
                    _logger.Debug(messageTemplate, propertyValues);
                }

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void LogInformation(string messageTemplate, params object[] propertyValues)
            {
                if (IsEnabled(LogEventLevel.Information, _contextId))
                {
                    _logger.Information(messageTemplate, propertyValues);
                }

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void LogWarning(string messageTemplate, params object[] propertyValues)
            {
                if (IsEnabled(LogEventLevel.Warning, _contextId))
                {
                    _logger.Warning(messageTemplate, propertyValues);
                }

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void LogError(string messageTemplate, params object[] propertyValues)
            {
                if (IsEnabled(LogEventLevel.Error, _contextId))
                {
                    _logger.Error(messageTemplate, propertyValues);
                }

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void LogError(Exception exception, string messageTemplate, params object[] propertyValues)
            {
                if (IsEnabled(LogEventLevel.Error, _contextId))
                {

                    _logger.Error(exception, messageTemplate, propertyValues);
                }

            }
        }
    }
}
