using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace BusBuddy.Core.Logging
{
    /// <summary>
    /// Serilog enricher to track and flag redundant database queries
    /// </summary>
    public class QueryTrackingEnricher : ILogEventEnricher
    {
        private static readonly ConcurrentDictionary<string, Tuple<DateTime, int>> _recentQueries = new();
        private static readonly TimeSpan _redundancyWindow = TimeSpan.FromSeconds(5);

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (!logEvent.Properties.TryGetValue("QueryName", out var queryNameProperty) ||
                !(queryNameProperty is ScalarValue scalarValue) ||
                !(scalarValue.Value is string queryName))
            {
                return;
            }

            // Only process database query operations
            if (!logEvent.Properties.ContainsKey("DatabaseOperation"))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var isRedundant = false;
            var queryCount = 1;

            // Check if this is a redundant query (same name within redundancy window)
            if (_recentQueries.TryGetValue(queryName, out var lastQuery))
            {
                var lastTime = lastQuery.Item1;
                var lastCount = lastQuery.Item2;

                if ((now - lastTime) <= _redundancyWindow)
                {
                    isRedundant = true;
                    queryCount = lastCount + 1;
                }
            }

            // Update the query tracking dictionary
            _recentQueries[queryName] = new Tuple<DateTime, int>(now, queryCount);

            // Add properties to the log event
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsRedundantQuery", isRedundant));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("QueryCount", queryCount));

            // For redundant queries, add more context
            if (isRedundant && queryCount > 2)
            {
                // Add a note about redundancy
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                    "RedundancyNote",
                    $"This query has been executed {queryCount} times in the last {_redundancyWindow.TotalSeconds} seconds"));

                // Add a warning tag for highly redundant queries
                if (queryCount >= 3)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RedundancyWarning", true));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RedundancySeverity", "High"));
                }
            }

            // Add query tracking information to log events
            var queryTrackingProperty = propertyFactory.CreateProperty("QueryTracking", "Enabled");
            logEvent.AddPropertyIfAbsent(queryTrackingProperty);

            // Add timestamp for query tracking
            var timestampProperty = propertyFactory.CreateProperty("QueryTimestamp", DateTimeOffset.Now);
            logEvent.AddPropertyIfAbsent(timestampProperty);
        }
    }
}
