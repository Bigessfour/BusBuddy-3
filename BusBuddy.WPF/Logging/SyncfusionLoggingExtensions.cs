using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// Extension methods for enhanced Syncfusion control logging
    /// </summary>
    public static class SyncfusionLoggingExtensions
    {
        private static readonly Dictionary<string, int> _controlInstanceCounts = new();
        private static readonly object _lockObject = new();

        /// <summary>
        /// Logs Syncfusion control lifecycle events
        /// </summary>
        public static void LogSyncfusionLifecycle(this ILogger logger, string controlType, string lifecycleEvent,
            string? controlName = null, object? additionalData = null, [CallerMemberName] string callerMethod = "")
        {
            var instanceId = GetNextInstanceId(controlType);

            using (LogContext.PushProperty("SyncfusionOperation", "Lifecycle"))
            using (LogContext.PushProperty("ControlType", controlType))
            using (LogContext.PushProperty("LifecycleEvent", lifecycleEvent))
            using (LogContext.PushProperty("InstanceId", instanceId))
            using (LogContext.PushProperty("CallerMethod", callerMethod))
            {
                var logMessage = controlName != null
                    ? "[SF_LIFECYCLE] {ControlType} '{ControlName}' (Instance #{InstanceId}) {LifecycleEvent} in {CallerMethod}"
                    : "[SF_LIFECYCLE] {ControlType} (Instance #{InstanceId}) {LifecycleEvent} in {CallerMethod}";

                if (additionalData != null)
                {
                    using (LogContext.PushProperty("AdditionalData", additionalData))
                    {
                        logger.Information(logMessage + " - Data: {AdditionalData}",
                            controlType, controlName ?? "Unnamed", instanceId, lifecycleEvent, callerMethod, additionalData);
                    }
                }
                else
                {
                    logger.Information(logMessage,
                        controlType, controlName ?? "Unnamed", instanceId, lifecycleEvent, callerMethod);
                }
            }
        }

        /// <summary>
        /// Logs Syncfusion theme-related operations
        /// </summary>
        public static void LogSyncfusionTheme(this ILogger logger, string operation, string? themeName = null,
            string? targetControl = null, [CallerMemberName] string callerMethod = "")
        {
            using (LogContext.PushProperty("SyncfusionOperation", "Theme"))
            using (LogContext.PushProperty("ThemeOperation", operation))
            using (LogContext.PushProperty("CallerMethod", callerMethod))
            {
                if (themeName != null && targetControl != null)
                {
                    using (LogContext.PushProperty("ThemeName", themeName))
                    using (LogContext.PushProperty("TargetControl", targetControl))
                    {
                        logger.Information("[SF_THEME] Theme operation '{ThemeOperation}' applying '{ThemeName}' to '{TargetControl}' in {CallerMethod}",
                            operation, themeName, targetControl, callerMethod);
                    }
                }
                else if (themeName != null)
                {
                    using (LogContext.PushProperty("ThemeName", themeName))
                    {
                        logger.Information("[SF_THEME] Theme operation '{ThemeOperation}' with theme '{ThemeName}' in {CallerMethod}",
                            operation, themeName, callerMethod);
                    }
                }
                else
                {
                    logger.Information("[SF_THEME] Theme operation '{ThemeOperation}' in {CallerMethod}",
                        operation, callerMethod);
                }
            }
        }

        /// <summary>
        /// Logs Syncfusion data operations (binding, filtering, sorting, etc.)
        /// </summary>
        public static void LogSyncfusionDataOperation(this ILogger logger, string controlType, string operation,
            int? itemCount = null, string? dataSourceType = null, TimeSpan? duration = null, [CallerMemberName] string callerMethod = "")
        {
            using (LogContext.PushProperty("SyncfusionOperation", "DataOperation"))
            using (LogContext.PushProperty("ControlType", controlType))
            using (LogContext.PushProperty("DataOperation", operation))
            using (LogContext.PushProperty("CallerMethod", callerMethod))
            {
                var logProperties = new List<string>();

                if (itemCount.HasValue)
                {
                    using (LogContext.PushProperty("ItemCount", itemCount.Value))
                    {
                        logProperties.Add($"Items: {itemCount.Value}");
                    }

                }

                if (!string.IsNullOrEmpty(dataSourceType))
                {
                    using (LogContext.PushProperty("DataSourceType", dataSourceType))
                    {
                        logProperties.Add($"Source: {dataSourceType}");
                    }

                }

                if (duration.HasValue)
                {
                    var durationMs = duration.Value.TotalMilliseconds;
                    using (LogContext.PushProperty("DurationMs", durationMs))
                    {
                        logProperties.Add($"Duration: {durationMs:F1}ms");
                    }


                    if (durationMs > 500)
                    {
                        logger.Warning("[SF_DATA_SLOW] Syncfusion {ControlType} data operation '{DataOperation}' took {DurationMs:F1}ms (SLOW) in {CallerMethod} - {Details}",
                            controlType, operation, durationMs, callerMethod, string.Join(", ", logProperties));
                        return;
                    }
                }

                var details = logProperties.Count > 0 ? $" - {string.Join(", ", logProperties)}" : "";
                logger.Information("[SF_DATA] Syncfusion {ControlType} data operation '{DataOperation}' in {CallerMethod}{Details}",
                    controlType, operation, callerMethod, details);
            }
        }

        /// <summary>
        /// Logs Syncfusion UI events (selection changes, user interactions, etc.)
        /// </summary>
        public static void LogSyncfusionUIEvent(this ILogger logger, string controlType, string eventName,
            object? eventArgs = null, string? controlName = null, [CallerMemberName] string callerMethod = "")
        {
            using (LogContext.PushProperty("SyncfusionOperation", "UIEvent"))
            using (LogContext.PushProperty("ControlType", controlType))
            using (LogContext.PushProperty("EventName", eventName))
            using (LogContext.PushProperty("CallerMethod", callerMethod))
            {
                if (controlName != null)
                {
                    using (LogContext.PushProperty("ControlName", controlName))
                    {
                        if (eventArgs != null)
                        {
                            using (LogContext.PushProperty("EventArgs", eventArgs))
                            {
                                logger.Debug("[SF_EVENT] Syncfusion {ControlType} '{ControlName}' fired '{EventName}' in {CallerMethod} - Args: {EventArgs}",
                                    controlType, controlName, eventName, callerMethod, eventArgs);
                            }
                        }
                        else
                        {
                            logger.Debug("[SF_EVENT] Syncfusion {ControlType} '{ControlName}' fired '{EventName}' in {CallerMethod}",
                                controlType, controlName, eventName, callerMethod);
                        }
                    }
                }
                else
                {
                    if (eventArgs != null)
                    {
                        using (LogContext.PushProperty("EventArgs", eventArgs))
                        {
                            logger.Debug("[SF_EVENT] Syncfusion {ControlType} fired '{EventName}' in {CallerMethod} - Args: {EventArgs}",
                                controlType, eventName, callerMethod, eventArgs);
                        }
                    }
                    else
                    {
                        logger.Debug("[SF_EVENT] Syncfusion {ControlType} fired '{EventName}' in {CallerMethod}",
                            controlType, eventName, callerMethod);
                    }
                }
            }
        }

        /// <summary>
        /// Logs Syncfusion performance metrics
        /// </summary>
        public static void LogSyncfusionPerformance(this ILogger logger, string controlType, string operation,
            TimeSpan duration, string? performanceContext = null, [CallerMemberName] string callerMethod = "")
        {
            var durationMs = duration.TotalMilliseconds;

            using (LogContext.PushProperty("SyncfusionOperation", "Performance"))
            using (LogContext.PushProperty("ControlType", controlType))
            using (LogContext.PushProperty("Operation", operation))
            using (LogContext.PushProperty("DurationMs", durationMs))
            using (LogContext.PushProperty("CallerMethod", callerMethod))
            {
                var performanceRating = durationMs switch
                {
                    > 1000 => "VERY SLOW",
                    > 500 => "SLOW",
                    > 100 => "MODERATE",
                    _ => "FAST"
                };

                if (!string.IsNullOrEmpty(performanceContext))
                {
                    using (LogContext.PushProperty("PerformanceContext", performanceContext))
                    {
                        // Use appropriate log level based on duration
                        if (durationMs > 1000)
                        {
                            logger.Warning("[SF_PERF] Syncfusion {ControlType} operation '{Operation}' took {DurationMs:F1}ms ({PerformanceRating}) in {CallerMethod} - Context: {PerformanceContext}",
                                controlType, operation, durationMs, performanceRating, callerMethod, performanceContext);
                        }
                        else if (durationMs > 500)
                        {
                            logger.Information("[SF_PERF] Syncfusion {ControlType} operation '{Operation}' took {DurationMs:F1}ms ({PerformanceRating}) in {CallerMethod} - Context: {PerformanceContext}",
                                controlType, operation, durationMs, performanceRating, callerMethod, performanceContext);
                        }
                        else
                        {
                            logger.Debug("[SF_PERF] Syncfusion {ControlType} operation '{Operation}' took {DurationMs:F1}ms ({PerformanceRating}) in {CallerMethod} - Context: {PerformanceContext}",
                                controlType, operation, durationMs, performanceRating, callerMethod, performanceContext);
                        }
                    }
                }
                else
                {
                    // Use appropriate log level based on duration
                    if (durationMs > 1000)
                    {
                        logger.Warning("[SF_PERF] Syncfusion {ControlType} operation '{Operation}' took {DurationMs:F1}ms ({PerformanceRating}) in {CallerMethod}",
                            controlType, operation, durationMs, performanceRating, callerMethod);
                    }
                    else if (durationMs > 500)
                    {
                        logger.Information("[SF_PERF] Syncfusion {ControlType} operation '{Operation}' took {DurationMs:F1}ms ({PerformanceRating}) in {CallerMethod}",
                            controlType, operation, durationMs, performanceRating, callerMethod);
                    }
                    else
                    {
                        logger.Debug("[SF_PERF] Syncfusion {ControlType} operation '{Operation}' took {DurationMs:F1}ms ({PerformanceRating}) in {CallerMethod}",
                            controlType, operation, durationMs, performanceRating, callerMethod);
                    }
                }
            }
        }

        /// <summary>
        /// Logs Syncfusion error conditions with enhanced context
        /// </summary>
        public static void LogSyncfusionError(this ILogger logger, Exception exception, string controlType,
            string operation, string? controlName = null, object? additionalContext = null, [CallerMemberName] string callerMethod = "")
        {
            using (LogContext.PushProperty("SyncfusionOperation", "Error"))
            using (LogContext.PushProperty("ControlType", controlType))
            using (LogContext.PushProperty("Operation", operation))
            using (LogContext.PushProperty("CallerMethod", callerMethod))
            using (LogContext.PushProperty("ErrorType", exception.GetType().Name))
            {
                if (controlName != null && additionalContext != null)
                {
                    using (LogContext.PushProperty("ControlName", controlName))
                    using (LogContext.PushProperty("AdditionalContext", additionalContext))
                    {
                        logger.Error(exception, "[SF_ERROR] Syncfusion {ControlType} '{ControlName}' error during '{Operation}' in {CallerMethod} - Context: {AdditionalContext} - Error: {ErrorMessage}",
                            controlType, controlName, operation, callerMethod, additionalContext, exception.Message);
                    }
                }
                else if (controlName != null)
                {
                    using (LogContext.PushProperty("ControlName", controlName))
                    {
                        logger.Error(exception, "[SF_ERROR] Syncfusion {ControlType} '{ControlName}' error during '{Operation}' in {CallerMethod} - Error: {ErrorMessage}",
                            controlType, controlName, operation, callerMethod, exception.Message);
                    }
                }
                else
                {
                    logger.Error(exception, "[SF_ERROR] Syncfusion {ControlType} error during '{Operation}' in {CallerMethod} - Error: {ErrorMessage}",
                        controlType, operation, callerMethod, exception.Message);
                }
            }
        }

        /// <summary>
        /// Logs Syncfusion diagnostic information
        /// </summary>
        public static void LogSyncfusionDiagnostic(this ILogger logger, string controlType, string diagnosticInfo,
            object? diagnosticData = null, [CallerMemberName] string callerMethod = "")
        {
            using (LogContext.PushProperty("SyncfusionOperation", "Diagnostic"))
            using (LogContext.PushProperty("ControlType", controlType))
            using (LogContext.PushProperty("DiagnosticInfo", diagnosticInfo))
            using (LogContext.PushProperty("CallerMethod", callerMethod))
            {
                if (diagnosticData != null)
                {
                    using (LogContext.PushProperty("DiagnosticData", diagnosticData))
                    {
                        logger.Debug("[SF_DIAG] Syncfusion {ControlType} diagnostic: {DiagnosticInfo} in {CallerMethod} - Data: {DiagnosticData}",
                            controlType, diagnosticInfo, callerMethod, diagnosticData);
                    }
                }
                else
                {
                    logger.Debug("[SF_DIAG] Syncfusion {ControlType} diagnostic: {DiagnosticInfo} in {CallerMethod}",
                        controlType, diagnosticInfo, callerMethod);
                }
            }
        }

        /// <summary>
        /// Helper method to track control instance counts
        /// </summary>
        private static int GetNextInstanceId(string controlType)
        {
            lock (_lockObject)
            {
                if (!_controlInstanceCounts.ContainsKey(controlType))
                {
                    _controlInstanceCounts[controlType] = 0;
                }
                return ++_controlInstanceCounts[controlType];
            }
        }
    }
}
