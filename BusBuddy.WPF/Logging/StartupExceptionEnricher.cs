using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// Specialized Serilog enricher for startup and dispatcher exceptions.
    /// Provides enhanced context, actionable insights, and categorization for startup failures.
    /// Focuses on making exceptions more actionable during the critical startup phase.
    /// </summary>
    public class StartupExceptionEnricher : ILogEventEnricher
    {
        private static readonly ConcurrentDictionary<string, StartupExceptionPattern> _exceptionPatterns = new();
        private static readonly DateTime _applicationStartTime = DateTime.UtcNow;
        private static readonly object _lockObject = new object();

        // Critical startup phases for enhanced tracking
        private static readonly HashSet<string> _criticalStartupPhases = new()
        {
            "STARTUP", "INITIALIZATION", "CONFIGURATION", "THEME_SETUP", "DATABASE_INIT",
            "SERILOG_INIT", "DEPENDENCY_INJECTION", "XAML_PARSING", "SYNCFUSION_INIT",
            "DISPATCHER_INIT", "VIEWMODEL_INIT", "CACHE_PREWARMING"
        };

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var messageTemplate = logEvent.MessageTemplate.Text;
            var isException = logEvent.Exception != null;
            var logLevel = logEvent.Level;
            var currentPhase = GetStartupPhase(messageTemplate);

            // Add startup timing context
            var startupElapsed = (DateTime.UtcNow - _applicationStartTime).TotalSeconds;
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("StartupElapsedSeconds", startupElapsed));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("StartupPhase", currentPhase));

            // Enhanced exception context for actionable insights
            if (isException)
            {
                EnrichException(logEvent, propertyFactory, messageTemplate, logLevel);
            }

            // Enhanced dispatcher exception handling
            if (IsDispatcherException(messageTemplate, logEvent.Exception))
            {
                EnrichDispatcherException(logEvent, propertyFactory, messageTemplate);
            }

            // Critical startup failure detection
            if (IsCriticalStartupFailure(messageTemplate, logLevel, logEvent.Exception))
            {
                EnrichCriticalStartupFailure(logEvent, propertyFactory, messageTemplate);
            }

            // XAML parsing failure enhancement
            if (IsXamlParsingFailure(messageTemplate, logEvent.Exception))
            {
                EnrichXamlParsingFailure(logEvent, propertyFactory, logEvent.Exception);
            }

            // Syncfusion-specific exception enhancement
            if (IsSyncfusionException(messageTemplate, logEvent.Exception))
            {
                EnrichSyncfusionException(logEvent, propertyFactory, logEvent.Exception);
            }

            // Startup performance tracking
            if (IsStartupPerformanceEvent(messageTemplate))
            {
                EnrichStartupPerformance(logEvent, propertyFactory, messageTemplate);
            }
        }

        private static void EnrichException(LogEvent logEvent, ILogEventPropertyFactory propertyFactory,
            string messageTemplate, LogEventLevel logLevel)
        {
            var exception = logEvent.Exception;
            if (exception == null)
            {
                return;
            }

            // Exception categorization for actionable insights
            var exceptionCategory = CategorizeException(exception);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExceptionCategory", exceptionCategory));

            // Exception severity based on impact
            var severity = GetExceptionSeverity(exception, logLevel);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExceptionSeverity", severity));

            // Actionable recommendations
            var recommendations = GetActionableRecommendations(exception, messageTemplate);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ActionableRecommendations", recommendations));

            // Root cause analysis
            var rootCause = AnalyzeRootCause(exception);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RootCauseAnalysis", rootCause));

            // Recovery suggestions
            var recoverySuggestions = GetRecoverySuggestions(exception);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RecoverySuggestions", recoverySuggestions));

            // Exception tracking for patterns
            TrackExceptionPattern(exception, messageTemplate);
        }

        private static void EnrichDispatcherException(LogEvent logEvent, ILogEventPropertyFactory propertyFactory,
            string messageTemplate)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsDispatcherException", true));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherExceptionType", "StartupDispatcher"));

            // Enhanced dispatcher context
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherThreadId",
                        dispatcher.Thread.ManagedThreadId));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherIsShuttingDown",
                        dispatcher.HasShutdownStarted));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherHasShutdownFinished",
                        dispatcher.HasShutdownFinished));
                }
            }
            catch
            {
                // Ignore dispatcher access errors during shutdown
            }

            // Dispatcher exception recommendations
            var dispatcherRecommendations = new List<string>
            {
                "Verify UI thread access patterns",
                "Check for recursive exception handling",
                "Review XAML parsing and binding issues",
                "Ensure proper async/await usage in UI operations",
                "Validate Syncfusion control initialization sequence"
            };

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherRecommendations",
                string.Join("; ", dispatcherRecommendations)));
        }

        private static void EnrichCriticalStartupFailure(LogEvent logEvent, ILogEventPropertyFactory propertyFactory,
            string messageTemplate)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsCriticalStartupFailure", true));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("StartupFailureImpact", "High"));

            // Critical failure recovery actions
            var recoveryActions = new List<string>
            {
                "Check application configuration files",
                "Verify database connection strings",
                "Ensure Syncfusion license is valid",
                "Check system dependencies (.NET runtime, Visual C++ redistributables)",
                "Review recent configuration changes",
                "Verify file system permissions"
            };

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CriticalRecoveryActions",
                string.Join("; ", recoveryActions)));

            // Startup failure categorization
            var failureCategory = GetStartupFailureCategory(messageTemplate);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("StartupFailureCategory", failureCategory));
        }

        private static void EnrichXamlParsingFailure(LogEvent logEvent, ILogEventPropertyFactory propertyFactory,
            Exception? exception)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsXamlParsingFailure", true));

            if (exception != null)
            {
                // Extract XAML parsing details
                var xamlDetails = ExtractXamlParsingDetails(exception);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("XamlParsingDetails", xamlDetails));

                // XAML-specific recommendations
                var xamlRecommendations = new List<string>
                {
                    "Check for duplicate resource keys",
                    "Verify StaticResource references exist",
                    "Validate XAML syntax and structure",
                    "Ensure proper namespace declarations",
                    "Check for circular resource references",
                    "Verify Syncfusion control properties"
                };

                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("XamlRecommendations",
                    string.Join("; ", xamlRecommendations)));
            }
        }

        private static void EnrichSyncfusionException(LogEvent logEvent, ILogEventPropertyFactory propertyFactory,
            Exception? exception)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsSyncfusionException", true));

            if (exception != null)
            {
                // Syncfusion-specific context
                var syncfusionContext = AnalyzeSyncfusionException(exception);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionContext", syncfusionContext));

                // Syncfusion recommendations
                var syncfusionRecommendations = new List<string>
                {
                    "Verify Syncfusion license registration",
                    "Check theme application order",
                    "Validate control property bindings",
                    "Ensure proper SfSkinManager usage",
                    "Review FluentDark theme compatibility",
                    "Check for control version conflicts"
                };

                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionRecommendations",
                    string.Join("; ", syncfusionRecommendations)));
            }
        }

        private static void EnrichStartupPerformance(LogEvent logEvent, ILogEventPropertyFactory propertyFactory,
            string messageTemplate)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsStartupPerformanceEvent", true));

            // Extract timing information
            var timing = ExtractTimingFromMessage(messageTemplate);
            if (timing.HasValue)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExtractedTimingMs", timing.Value));

                // Performance classification
                var performanceClass = ClassifyPerformance(timing.Value);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("PerformanceClass", performanceClass));
            }

            // Performance recommendations
            var performanceRecommendations = new List<string>
            {
                "Consider lazy loading for non-essential components",
                "Optimize database queries and caching",
                "Review synchronous vs asynchronous operations",
                "Minimize startup dependencies",
                "Consider background initialization"
            };

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("PerformanceRecommendations",
                string.Join("; ", performanceRecommendations)));
        }

        private static string GetStartupPhase(string messageTemplate)
        {
            foreach (var phase in _criticalStartupPhases)
            {
                if (messageTemplate.Contains(phase, StringComparison.OrdinalIgnoreCase))
                {
                    return phase;
                }
            }
            return "UNKNOWN";
        }

        private static bool IsDispatcherException(string messageTemplate, Exception? exception)
        {
            return messageTemplate.Contains("DISPATCHER", StringComparison.OrdinalIgnoreCase) ||
                   messageTemplate.Contains("DispatcherUnhandledException", StringComparison.OrdinalIgnoreCase) ||
                   exception?.GetType().Name.Contains("Dispatcher") == true;
        }

        private static bool IsCriticalStartupFailure(string messageTemplate, LogEventLevel logLevel, Exception? exception)
        {
            return logLevel >= LogEventLevel.Error &&
                   (messageTemplate.Contains("STARTUP", StringComparison.OrdinalIgnoreCase) ||
                    messageTemplate.Contains("INITIALIZATION", StringComparison.OrdinalIgnoreCase) ||
                    messageTemplate.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
                    IsCriticalException(exception));
        }

        private static bool IsXamlParsingFailure(string messageTemplate, Exception? exception)
        {
            return messageTemplate.Contains("XAML", StringComparison.OrdinalIgnoreCase) ||
                   exception is System.Windows.Markup.XamlParseException;
        }

        private static bool IsSyncfusionException(string messageTemplate, Exception? exception)
        {
            return messageTemplate.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) ||
                   exception?.GetType().FullName?.Contains("Syncfusion") == true ||
                   exception?.StackTrace?.Contains("Syncfusion") == true;
        }

        private static bool IsStartupPerformanceEvent(string messageTemplate)
        {
            return messageTemplate.Contains("STARTUP_PERF", StringComparison.OrdinalIgnoreCase) ||
                   messageTemplate.Contains("ElapsedMs", StringComparison.OrdinalIgnoreCase) ||
                   messageTemplate.Contains("completed in", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCriticalException(Exception? exception)
        {
            if (exception == null)
            {
                return false;
            }

            return exception is OutOfMemoryException ||
                   exception is StackOverflowException ||
                   exception is AccessViolationException ||
                   exception is AppDomainUnloadedException ||
                   exception is BadImageFormatException ||
                   exception is InvalidProgramException;
        }

        private static string CategorizeException(Exception exception)
        {
            return exception switch
            {
                System.Windows.Markup.XamlParseException => "XAML_PARSING",
                Microsoft.Data.SqlClient.SqlException => "DATABASE_CONNECTION",
                System.IO.FileNotFoundException => "MISSING_FILE",
                System.Configuration.ConfigurationException => "CONFIGURATION_ERROR",
                System.InvalidOperationException when exception.Message.Contains("DbContext") => "DATABASE_CONTEXT",
                System.InvalidOperationException when exception.Message.Contains("Dispatcher") => "DISPATCHER_THREAD",
                System.TypeInitializationException => "TYPE_INITIALIZATION",
                System.Reflection.ReflectionTypeLoadException => "ASSEMBLY_LOADING",
                _ when exception.GetType().FullName?.Contains("Syncfusion") == true => "SYNCFUSION_CONTROL",
                _ => "GENERAL_EXCEPTION"
            };
        }

        private static string GetExceptionSeverity(Exception exception, LogEventLevel logLevel)
        {
            if (IsCriticalException(exception))
            {
                return "CRITICAL";
            }
            if (logLevel >= LogEventLevel.Error)
            {
                return "HIGH";
            }
            if (logLevel >= LogEventLevel.Warning)
            {
                return "MEDIUM";
            }
            return "LOW";
        }

        private static string GetActionableRecommendations(Exception exception, string messageTemplate)
        {
            var recommendations = new List<string>();

            // Exception-specific recommendations
            switch (exception)
            {
                case System.Windows.Markup.XamlParseException:
                    recommendations.Add("Review XAML syntax and resource references");
                    recommendations.Add("Check for duplicate resource keys");
                    break;
                case Microsoft.Data.SqlClient.SqlException:
                    recommendations.Add("Verify database connection string");
                    recommendations.Add("Check database server availability");
                    break;
                case System.IO.FileNotFoundException:
                    recommendations.Add("Verify file paths and deployment");
                    recommendations.Add("Check build output and copy settings");
                    break;
                case System.Configuration.ConfigurationException:
                    recommendations.Add("Review appsettings.json configuration");
                    recommendations.Add("Validate configuration file structure");
                    break;
            }

            // Message-specific recommendations
            if (messageTemplate.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
            {
                recommendations.Add("Check Syncfusion license registration");
                recommendations.Add("Verify theme application sequence");
            }

            return recommendations.Count > 0 ? string.Join("; ", recommendations) : "Review exception details and stack trace";
        }

        private static string AnalyzeRootCause(Exception exception)
        {
            var innerException = exception.InnerException;
            if (innerException != null)
            {
                return $"Root cause: {innerException.GetType().Name}: {innerException.Message}";
            }
            return $"Direct cause: {exception.GetType().Name}: {exception.Message}";
        }

        private static string GetRecoverySuggestions(Exception exception)
        {
            return exception switch
            {
                System.Windows.Markup.XamlParseException => "Restart application; Check XAML files; Clear temporary files",
                Microsoft.Data.SqlClient.SqlException => "Check database connection; Restart database service; Verify credentials",
                System.IO.FileNotFoundException => "Reinstall application; Check file permissions; Verify deployment",
                System.Configuration.ConfigurationException => "Reset configuration; Check file format; Restore from backup",
                _ => "Restart application; Check logs; Contact support if persistent"
            };
        }

        private static void TrackExceptionPattern(Exception exception, string messageTemplate)
        {
            var messageLength = exception.Message?.Length ?? 0;
            var truncatedMessage = exception.Message?.Substring(0, Math.Min(100, messageLength)) ?? "";
            var pattern = $"{exception.GetType().Name}:{truncatedMessage}";

            lock (_lockObject)
            {
                var exceptionPattern = _exceptionPatterns.GetOrAdd(pattern, _ => new StartupExceptionPattern
                {
                    Pattern = pattern,
                    FirstOccurrence = DateTime.UtcNow,
                    Count = 0
                });

                exceptionPattern.Count++;
                exceptionPattern.LastOccurrence = DateTime.UtcNow;
            }
        }

        private static string ExtractXamlParsingDetails(Exception exception)
        {
            if (exception is System.Windows.Markup.XamlParseException xamlEx)
            {
                return $"Line: {xamlEx.LineNumber}, Position: {xamlEx.LinePosition}, Source: {xamlEx.BaseUri}";
            }
            return "Unable to extract XAML parsing details";
        }

        private static string AnalyzeSyncfusionException(Exception exception)
        {
            var context = new List<string>();

            if (exception.Message.Contains("license"))
            {
                context.Add("License-related issue");
            }
            if (exception.Message.Contains("theme"))
            {
                context.Add("Theme-related issue");
            }
            if (exception.Message.Contains("control"))
            {
                context.Add("Control initialization issue");
            }
            if (exception.StackTrace?.Contains("SfSkinManager") == true)
            {
                context.Add("Skin manager issue");
            }

            return context.Count > 0 ? string.Join("; ", context) : "General Syncfusion issue";
        }

        private static double? ExtractTimingFromMessage(string messageTemplate)
        {
            var timingMatch = Regex.Match(messageTemplate, @"(\d+(?:\.\d+)?)\s*ms");
            if (timingMatch.Success && double.TryParse(timingMatch.Groups[1].Value, out var timing))
            {
                return timing;
            }
            return null;
        }

        private static string ClassifyPerformance(double timingMs)
        {
            return timingMs switch
            {
                < 100 => "FAST",
                < 500 => "ACCEPTABLE",
                < 1000 => "SLOW",
                _ => "VERY_SLOW"
            };
        }

        private static string GetStartupFailureCategory(string messageTemplate)
        {
            if (messageTemplate.Contains("configuration", StringComparison.OrdinalIgnoreCase))
            {
                return "CONFIGURATION_FAILURE";
            }
            if (messageTemplate.Contains("database", StringComparison.OrdinalIgnoreCase))
            {
                return "DATABASE_FAILURE";
            }
            if (messageTemplate.Contains("theme", StringComparison.OrdinalIgnoreCase))
            {
                return "THEME_FAILURE";
            }
            if (messageTemplate.Contains("dependency", StringComparison.OrdinalIgnoreCase))
            {
                return "DEPENDENCY_FAILURE";
            }
            if (messageTemplate.Contains("xaml", StringComparison.OrdinalIgnoreCase))
            {
                return "XAML_FAILURE";
            }
            return "UNKNOWN_FAILURE";
        }

        private sealed class StartupExceptionPattern
        {
            public string Pattern { get; set; } = string.Empty;
            public DateTime FirstOccurrence { get; set; }
            public DateTime LastOccurrence { get; set; }
            public int Count { get; set; }
        }
    }
}
