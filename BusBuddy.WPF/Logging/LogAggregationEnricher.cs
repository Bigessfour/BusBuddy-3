using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serilog.Core;
using Serilog.Events;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// Log aggregation enricher that groups similar log events to reduce verbosity.
    /// Provides condensed logging by tracking patterns and frequencies.
    /// </summary>
    public class LogAggregationEnricher : ILogEventEnricher
    {
        private static readonly ConcurrentDictionary<string, LogEventGroup> _eventGroups = new();
        private static readonly object _lockObject = new object();

        // Configuration for aggregation behavior
        private const int AGGREGATION_WINDOW_SECONDS = 5;
        private const int MAX_SIMILAR_EVENTS = 3;
        private const int CLEANUP_INTERVAL_SECONDS = 30;

        // Timer to periodically clean up old event groups
        private static readonly Timer _aggregationTimer = new Timer(CleanupExpiredGroups, null,
            TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS),
            TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS));

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var messageTemplate = logEvent.MessageTemplate.Text;
            var logLevel = logEvent.Level;

            // Create a signature for grouping similar events
            var eventSignature = CreateEventSignature(messageTemplate, logLevel);

            lock (_lockObject)
            {
                var eventGroup = _eventGroups.GetOrAdd(eventSignature, _ => new LogEventGroup
                {
                    Signature = eventSignature,
                    FirstOccurrence = DateTimeOffset.Now,
                    LastOccurrence = DateTimeOffset.Now,
                    Count = 0,
                    Level = logLevel,
                    OriginalTemplate = messageTemplate
                });

                eventGroup.Count++;
                eventGroup.LastOccurrence = DateTimeOffset.Now;

                // Add aggregation properties
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("EventSignature", eventSignature));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("EventOccurrenceCount", eventGroup.Count));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("FirstOccurrence", eventGroup.FirstOccurrence));

                // Determine if this event should be aggregated (suppressed)
                var shouldAggregate = ShouldAggregateEvent(eventGroup, messageTemplate);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsAggregated", shouldAggregate));

                if (shouldAggregate)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("AggregationReason",
                        GetAggregationReason(eventGroup)));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SimilarEventsCount", eventGroup.Count));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TimeSpanSinceFirst",
                        (DateTimeOffset.Now - eventGroup.FirstOccurrence).TotalSeconds));
                }

                // Add categorization for better organization
                var category = CategorizeLogEvent(messageTemplate);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LogCategory", category));

                // Add condensed summary for frequent events
                if (eventGroup.Count > 1)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CondensedSummary",
                        CreateCondensedSummary(eventGroup, messageTemplate)));
                }
            }
        }

        private static string CreateEventSignature(string messageTemplate, LogEventLevel level)
        {
            // Create a signature by normalizing the message template
            var normalized = messageTemplate
                .Replace("{", "")
                .Replace("}", "")
                .Replace("ms", "X")
                .Replace("MB", "X");

            // Remove specific values that would make events appear different
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\d+", "X");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}", "GUID");

            return $"{level}:{normalized}";
        }

        private static bool ShouldAggregateEvent(LogEventGroup eventGroup, string messageTemplate)
        {
            // Don't aggregate startup events (they're important to see individually)
            if (messageTemplate.Contains("STARTUP", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Don't aggregate error events (always show errors)
            if (eventGroup.Level >= LogEventLevel.Warning)
            {
                return false;
            }

            // Aggregate if we've seen this event multiple times within the window
            var timeSinceFirst = DateTimeOffset.Now - eventGroup.FirstOccurrence;
            return eventGroup.Count > MAX_SIMILAR_EVENTS &&
                   timeSinceFirst.TotalSeconds <= AGGREGATION_WINDOW_SECONDS * 3;
        }

        private static string GetAggregationReason(LogEventGroup eventGroup)
        {
            if (eventGroup.Count > MAX_SIMILAR_EVENTS * 2)
            {
                return "HighFrequency";
            }
            else if (eventGroup.Count > MAX_SIMILAR_EVENTS)
            {
                return "RepeatedEvent";
            }
            else
            {
                return "SimilarPattern";
            }
        }

        private static string CategorizeLogEvent(string messageTemplate)
        {
            if (messageTemplate.Contains("STARTUP", StringComparison.OrdinalIgnoreCase))
            {
                return "Startup";
            }
            else if (messageTemplate.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
                     messageTemplate.Contains("View", StringComparison.OrdinalIgnoreCase))
            {
                return "UserInterface";
            }
            else if (messageTemplate.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
                     messageTemplate.Contains("SQL", StringComparison.OrdinalIgnoreCase))
            {
                return "Database";
            }
            else if (messageTemplate.Contains("cache", StringComparison.OrdinalIgnoreCase))
            {
                return "Caching";
            }
            else if (messageTemplate.Contains("ms", StringComparison.OrdinalIgnoreCase) ||
                     messageTemplate.Contains("performance", StringComparison.OrdinalIgnoreCase))
            {
                return "Performance";
            }
            else
            {
                return "General";
            }
        }

        private static string CreateCondensedSummary(LogEventGroup eventGroup, string messageTemplate)
        {
            var timeSinceFirst = DateTimeOffset.Now - eventGroup.FirstOccurrence;
            return $"Similar event occurred {eventGroup.Count}x over {timeSinceFirst.TotalSeconds:F1}s";
        }

        private static void CleanupExpiredGroups(object? state)
        {
            lock (_lockObject)
            {
                var expiredKeys = _eventGroups
                    .Where(kvp => (DateTimeOffset.Now - kvp.Value.LastOccurrence).TotalMinutes > 5)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _eventGroups.TryRemove(key, out _);
                }
            }
        }

        private class LogEventGroup
        {
            public string Signature { get; set; } = string.Empty;
            public DateTimeOffset FirstOccurrence { get; set; }
            public DateTimeOffset LastOccurrence { get; set; }
            public int Count { get; set; }
            public LogEventLevel Level { get; set; }
            public string OriginalTemplate { get; set; } = string.Empty;
        }
    }
}
