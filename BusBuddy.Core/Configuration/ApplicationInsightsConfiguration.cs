using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace BusBuddy.Core.Configuration
{
    /// <summary>
    /// Application Insights configuration and setup helper
    /// Based on Microsoft Application Insights documentation
    /// </summary>
    public static class ApplicationInsightsConfiguration
    {
        /// <summary>
        /// Configure Application Insights telemetry for BusBuddy
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Application configuration</param>
        public static IServiceCollection AddApplicationInsights(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var appInsightsSection = configuration.GetSection("ApplicationInsights");

            if (appInsightsSection.Exists())
            {
                var instrumentationKey = appInsightsSection["InstrumentationKey"];
                var connectionString = appInsightsSection["ConnectionString"];

                if (!string.IsNullOrEmpty(instrumentationKey) || !string.IsNullOrEmpty(connectionString))
                {
                    // Add Application Insights telemetry with basic configuration
                    services.AddApplicationInsightsTelemetry(options =>
                    {
                        if (!string.IsNullOrEmpty(connectionString))
                        {
                            options.ConnectionString = connectionString;
                        }
                        else if (!string.IsNullOrEmpty(instrumentationKey))
                        {
                            // Use connection string format for instrumentation key
                            options.ConnectionString = $"InstrumentationKey={instrumentationKey}";
                        }

                        // Configure basic telemetry options
                        options.EnableAdaptiveSampling = appInsightsSection.GetValue<bool>("EnableAdaptiveSampling", true);
                        options.EnableDependencyTrackingTelemetryModule = appInsightsSection.GetValue<bool>("EnableDependencyTracking", true);
                        options.EnablePerformanceCounterCollectionModule = appInsightsSection.GetValue<bool>("EnablePerformanceCounterCollectionModule", false);
                        options.EnableQuickPulseMetricStream = appInsightsSection.GetValue<bool>("EnableQuickPulseMetricStream", false);
                    });

                    // Note: Advanced sampling configuration removed due to API changes in Application Insights 2.23.0
                    // Adaptive sampling is handled automatically when enabled above
                }
            }

            return services;
        }

        /// <summary>
        /// Check if Application Insights is configured and enabled
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <returns>True if Application Insights is properly configured</returns>
        public static bool IsApplicationInsightsEnabled(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var appInsightsSection = configuration.GetSection("ApplicationInsights");

            if (!appInsightsSection.Exists())
                return false;

            var instrumentationKey = appInsightsSection["InstrumentationKey"];
            var connectionString = appInsightsSection["ConnectionString"];

            return !string.IsNullOrEmpty(instrumentationKey) || !string.IsNullOrEmpty(connectionString);
        }

        /// <summary>
        /// Get Application Insights telemetry configuration summary
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <returns>Configuration summary for logging</returns>
        public static string GetTelemetryConfigurationSummary(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            if (!IsApplicationInsightsEnabled(configuration))
            {
                return "Application Insights: Disabled";
            }

            var appInsightsSection = configuration.GetSection("ApplicationInsights");
            var samplingPercentage = appInsightsSection.GetValue<double>("SamplingPercentage", 100.0);
            var adaptiveSampling = appInsightsSection.GetValue<bool>("EnableAdaptiveSampling", true);
            var dependencyTracking = appInsightsSection.GetValue<bool>("EnableDependencyTracking", true);

            return $"Application Insights: Enabled (Sampling: {samplingPercentage}%, Adaptive: {adaptiveSampling}, Dependencies: {dependencyTracking})";
        }
    }
}
