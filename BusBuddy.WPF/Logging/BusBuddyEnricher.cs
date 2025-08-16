using Serilog.Core;
using Serilog.Events;
using System;
using System.Reflection;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// Custom Serilog enricher for BusBuddy-specific contextual properties
    /// Adds application version, build configuration, and runtime environment details
    /// Reference: https://github.com/serilog/serilog/wiki/Enrichment
    /// </summary>
    public class BusBuddyEnricher : ILogEventEnricher
    {
        private static readonly Lazy<LogEventProperty> VersionProperty = new(() =>
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";
            return new LogEventProperty("ApplicationVersion", new ScalarValue(version));
        });

        private static readonly Lazy<LogEventProperty> BuildConfigProperty = new(() =>
        {
#if DEBUG
            var config = "Debug";
#else
            var config = "Release";
#endif
            return new LogEventProperty("BuildConfiguration", new ScalarValue(config));
        });

        private static readonly Lazy<LogEventProperty> RuntimeProperty = new(() =>
        {
            var runtime = Environment.Version.ToString();
            return new LogEventProperty("DotNetVersion", new ScalarValue(runtime));
        });

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // Add application version
            logEvent.AddPropertyIfAbsent(VersionProperty.Value);

            // Add build configuration
            logEvent.AddPropertyIfAbsent(BuildConfigProperty.Value);

            // Add .NET runtime version
            logEvent.AddPropertyIfAbsent(RuntimeProperty.Value);

            // Add current database provider if available
            if (logEvent.Properties.ContainsKey("SourceContext"))
            {
                var sourceContext = logEvent.Properties["SourceContext"].ToString();
                if (sourceContext.Contains("Data") || sourceContext.Contains("Repository"))
                {
                    var dbProvider = Environment.GetEnvironmentVariable("BUSBUDDY_DB_PROVIDER") ?? "LocalDB";
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DatabaseProvider", dbProvider));
                }
            }
        }
    }
}
