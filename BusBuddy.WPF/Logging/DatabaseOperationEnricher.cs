using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// Database operation enricher that adds context for database-related operations.
    /// Helps track database performance, connection issues, and query timing.
    /// </summary>
    public class DatabaseOperationEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // Check if this is a database-related log event
            var messageTemplate = logEvent.MessageTemplate.Text;
            var isDatabaseLog = messageTemplate.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
                               messageTemplate.Contains("SQL", StringComparison.OrdinalIgnoreCase) ||
                               messageTemplate.Contains("Entity", StringComparison.OrdinalIgnoreCase) ||
                               messageTemplate.Contains("DbContext", StringComparison.OrdinalIgnoreCase) ||
                               messageTemplate.Contains("Azure", StringComparison.OrdinalIgnoreCase);

            if (isDatabaseLog)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsDatabaseOperation", true));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DatabaseContext", "BusBuddyDB"));

                // Add database provider context if available
                if (messageTemplate.Contains("Azure", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DatabaseProvider", "AzureSQL"));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsCloudDatabase", true));
                }
                else if (messageTemplate.Contains("Local", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Express", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DatabaseProvider", "SQLServerExpress"));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsCloudDatabase", false));
                }

                // Add operation type context
                if (messageTemplate.Contains("migration", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DatabaseOperationType", "Migration"));
                }
                else if (messageTemplate.Contains("validation", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DatabaseOperationType", "Validation"));
                }
                else if (messageTemplate.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("select", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DatabaseOperationType", "Query"));
                }
                else if (messageTemplate.Contains("insert", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("update", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("delete", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DatabaseOperationType", "Modification"));
                }
            }

            // Add startup context for database initialization
            if (messageTemplate.Contains("STARTUP", StringComparison.OrdinalIgnoreCase) && isDatabaseLog)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsStartupDatabaseOperation", true));
            }
        }
    }
}
