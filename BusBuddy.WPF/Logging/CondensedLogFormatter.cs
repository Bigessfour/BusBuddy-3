using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Globalization;
using System.IO;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// Condensed log formatter that creates more compact and readable log output.
    /// Reduces verbosity while maintaining essential information.
    /// </summary>
    public class CondensedLogFormatter : ITextFormatter
    {
        private const string DefaultOutputTemplate = "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message}{NewLine}";
        private const string DetailedOutputTemplate = "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [{Category}] {Message}{NewLine}";
        private const string AggregatedOutputTemplate = "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [AGGREGATED] {Message} (occurred {Count}x){NewLine}";

        private readonly bool _includeProperties;
        private readonly bool _showAggregatedOnly;

        public CondensedLogFormatter(bool includeProperties = false, bool showAggregatedOnly = false)
        {
            _includeProperties = includeProperties;
            _showAggregatedOnly = showAggregatedOnly;
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            // Check if this event should be suppressed due to aggregation
            if (_showAggregatedOnly && ShouldSuppressEvent(logEvent))
            {
                return; // Skip this event
            }

            // Use different templates based on event characteristics
            var template = SelectTemplate(logEvent);
            FormatEvent(logEvent, output, template);
        }

        private bool ShouldSuppressEvent(LogEvent logEvent)
        {
            // Suppress events that are aggregated unless they're the summary
            if (logEvent.Properties.TryGetValue("IsAggregated", out var isAggregated) &&
                isAggregated is ScalarValue { Value: true })
            {
                // Only show every 5th occurrence of aggregated events
                if (logEvent.Properties.TryGetValue("EventOccurrenceCount", out var count) &&
                    count is ScalarValue { Value: int countValue })
                {
                    return countValue % 5 != 0; // Show every 5th occurrence
                }
                return true; // Suppress by default
            }

            return false;
        }

        private string SelectTemplate(LogEvent logEvent)
        {
            // Use aggregated template for aggregated events
            if (logEvent.Properties.TryGetValue("IsAggregated", out var isAggregated) &&
                isAggregated is ScalarValue { Value: true })
            {
                return AggregatedOutputTemplate;
            }

            // Use detailed template for categorized events
            if (logEvent.Properties.ContainsKey("LogCategory"))
            {
                return DetailedOutputTemplate;
            }

            // Use default template for simple events
            return DefaultOutputTemplate;
        }

        private void FormatEvent(LogEvent logEvent, TextWriter output, string template)
        {
            // Format timestamp
            var timestamp = logEvent.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

            // Format level with padding
            var level = FormatLevel(logEvent.Level);

            // Get category if available
            var category = GetPropertyValue(logEvent, "LogCategory") ?? "APP";

            // Get aggregation count if available
            var count = GetPropertyValue(logEvent, "EventOccurrenceCount") ?? "1";

            // Create condensed message
            var message = CreateCondensedMessage(logEvent);

            // Format the final output
            var formattedLine = template
                .Replace("{Timestamp:HH:mm:ss.fff}", timestamp)
                .Replace("{Level:u3}", level)
                .Replace("{Category}", category)
                .Replace("{Message}", message)
                .Replace("{Count}", count)
                .Replace("{NewLine}", Environment.NewLine);

            output.Write(formattedLine);

            // Add properties if requested and this is an important event
            if (_includeProperties && ShouldIncludeProperties(logEvent))
            {
                WriteImportantProperties(logEvent, output);
            }
        }

        private string FormatLevel(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => "VRB",
                LogEventLevel.Debug => "DBG",
                LogEventLevel.Information => "INF",
                LogEventLevel.Warning => "WRN",
                LogEventLevel.Error => "ERR",
                LogEventLevel.Fatal => "FTL",
                _ => level.ToString().Substring(0, 3).ToUpperInvariant()
            };
        }

        private string CreateCondensedMessage(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();

            // Condense common patterns
            message = message
                .Replace("BusBuddy WPF application", "App")
                .Replace("MainViewModel", "MainVM")
                .Replace("ViewModel", "VM")
                .Replace("completed successfully", "✓")
                .Replace("initialization", "init")
                .Replace("milliseconds", "ms")
                .Replace(" successfully", "")
                .Replace("Database validation", "DB check");

            // Truncate very long messages
            if (message.Length > 120)
            {
                message = message.Substring(0, 117) + "...";
            }

            return message;
        }

        private string? GetPropertyValue(LogEvent logEvent, string propertyName)
        {
            if (logEvent.Properties.TryGetValue(propertyName, out var propertyValue))
            {
                return propertyValue?.ToString()?.Trim('"');
            }
            return null;
        }

        private bool ShouldIncludeProperties(LogEvent logEvent)
        {
            // Include properties for errors, warnings, or performance metrics
            return logEvent.Level >= LogEventLevel.Warning ||
                   (logEvent.Properties.ContainsKey("IsPerformanceMetric") &&
                    logEvent.Properties["IsPerformanceMetric"] is ScalarValue { Value: true });
        }

        private void WriteImportantProperties(LogEvent logEvent, TextWriter output)
        {
            var importantProps = new[] { "MemoryUsageMB", "ElapsedMs", "ThreadType", "UIOperationType", "CurrentView" };

            var propValues = new System.Collections.Generic.List<string>();
            foreach (var prop in importantProps)
            {
                var value = GetPropertyValue(logEvent, prop);
                if (!string.IsNullOrEmpty(value))
                {
                    propValues.Add($"{prop}={value}");
                }
            }

            if (propValues.Count > 0)
            {
                output.Write($"    [{string.Join(", ", propValues)}]{Environment.NewLine}");
            }
        }
    }
}
