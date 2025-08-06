using Serilog.Core;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.Reflection;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// Custom Serilog enricher that adds BusBuddy-specific contextual information to log events.
    /// Based on best practices from: https://medium.com/codenx/exploring-serilog-enrichers-in-net-8-5bfc2e2f74a4
    /// </summary>
    public class BusBuddyContextEnricher : ILogEventEnricher
    {
        private readonly string _applicationVersion;
        private readonly string _applicationName;
        private readonly DateTime _applicationStartTime;

        public BusBuddyContextEnricher()
        {
            _applicationName = "BusBuddy";
            _applicationStartTime = DateTime.UtcNow;

            // Get application version from assembly
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                _applicationVersion = assembly.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                _applicationVersion = "Unknown";
            }
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // Add application context
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ApplicationName", _applicationName));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ApplicationVersion", _applicationVersion));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ApplicationStartTime", _applicationStartTime));

            // Add runtime information
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ApplicationUptime",
                TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"hh\:mm\:ss")));

            // Add process information
            using var currentProcess = Process.GetCurrentProcess();
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessId", currentProcess.Id));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessName", currentProcess.ProcessName));

            // Add memory information (useful for performance monitoring)
            var workingSet = currentProcess.WorkingSet64 / (1024 * 1024); // Convert to MB
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MemoryUsageMB", workingSet));

            // Add .NET runtime information
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DotNetVersion", Environment.Version.ToString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OSVersion", Environment.OSVersion.ToString()));

            // Add timezone information (useful for distributed systems)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TimeZone", TimeZoneInfo.Local.Id));

            // Add custom correlation context if available
            var correlationId = GetCorrelationId();
            if (!string.IsNullOrEmpty(correlationId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
            }
        }

        private string? GetCorrelationId()
        {
            // Try to get correlation ID from various sources
            // This could be from HTTP headers in web scenarios, or custom context in desktop apps

            // For now, use a simple approach - you can enhance this based on your needs
            return Environment.CurrentManagedThreadId.ToString();
        }
    }
}
