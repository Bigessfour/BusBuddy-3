using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace BusBuddy.Core.Configuration
{
    /// <summary>
    /// Helper class for environment-specific configuration and development features
    /// </summary>
    public static class DevelopmentHelper
    {
        /// <summary>
        /// Check if application is running in development mode
        /// </summary>
        public static bool IsDevelopment(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            var environment = configuration["Environment"] ?? "Production";
            return environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get optimized logging configuration for current environment
        /// </summary>
        public static LogEventLevel GetOptimalLogLevel(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            return IsDevelopment(configuration) ? LogEventLevel.Debug : LogEventLevel.Information;
        }

        /// <summary>
        /// Get performance monitoring settings based on environment
        /// </summary>
        public static bool ShouldEnablePerformanceMonitoring(IConfiguration configuration)
        {
            return IsDevelopment(configuration);
        }

        /// <summary>
        /// Get recommended page size for data queries based on environment
        /// </summary>
        public static int GetOptimalPageSize(IConfiguration configuration)
        {
            return IsDevelopment(configuration) ? 50 : 100; // Smaller pages in dev for easier debugging
        }

        /// <summary>
        /// Check if seed data features should be enabled
        /// </summary>
        public static bool ShouldEnableSeedData(IConfiguration configuration)
        {
            return IsDevelopment(configuration);
        }

        /// <summary>
        /// Get memory usage warning threshold based on environment
        /// </summary>
        public static long GetMemoryWarningThreshold(IConfiguration configuration)
        {
            return IsDevelopment(configuration) ? 200 * 1024 * 1024 : 500 * 1024 * 1024; // 200MB dev, 500MB prod
        }

        /// <summary>
        /// Get database query timeout based on environment
        /// </summary>
        public static int GetDatabaseTimeout(IConfiguration configuration)
        {
            return IsDevelopment(configuration) ? 30 : 15; // Longer timeout in dev for debugging
        }
    }
}
