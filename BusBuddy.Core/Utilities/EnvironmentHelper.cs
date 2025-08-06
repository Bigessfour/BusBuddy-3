using System;
using Microsoft.Extensions.Configuration;

namespace BusBuddy.Core.Utilities
{
    /// <summary>
    /// Helper class for environment-related utilities
    /// </summary>
    public static class EnvironmentHelper
    {
        /// <summary>
        /// Checks if the application is running in development mode
        /// </summary>
        /// <param name="configuration">Optional configuration to check environment from</param>
        /// <returns>True if running in development environment, false otherwise</returns>
        public static bool IsDevelopment(IConfiguration? configuration = null)
        {
            var environment = configuration?["Environment"]
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            return environment != null &&
                   environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the application is running in production mode
        /// </summary>
        /// <param name="configuration">Optional configuration to check environment from</param>
        /// <returns>True if running in production environment, false otherwise</returns>
        public static bool IsProduction(IConfiguration? configuration = null)
        {
            var environment = configuration?["Environment"]
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production";

            return environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the current environment name
        /// </summary>
        /// <param name="configuration">Optional configuration to get environment from</param>
        /// <returns>The environment name (Development, Staging, Production) or "Production" if not set</returns>
        public static string GetEnvironmentName(IConfiguration? configuration = null)
        {
            return configuration?["Environment"]
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production";
        }

        /// <summary>
        /// Checks if sensitive data logging should be enabled
        /// </summary>
        /// <param name="configuration">Optional configuration to check settings from</param>
        /// <returns>True if sensitive data logging should be enabled, false otherwise</returns>
        public static bool IsSensitiveDataLoggingEnabled(IConfiguration? configuration = null)
        {
            // Only enable sensitive data logging in Development mode
            if (IsDevelopment(configuration))
            {
                // Additionally check for an override setting that can explicitly disable it
                var disableSensitiveLogging = configuration?["DisableSensitiveDataLogging"]
                    ?? Environment.GetEnvironmentVariable("DISABLE_SENSITIVE_DATA_LOGGING");

                return string.IsNullOrEmpty(disableSensitiveLogging) ||
                       !disableSensitiveLogging.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Determines if the application is using LocalDB for data storage
        /// </summary>
        /// <param name="configuration">Optional configuration to check database provider</param>
        /// <returns>True if using LocalDB, false otherwise</returns>
        public static bool IsUsingLocalDb(IConfiguration? configuration = null)
        {
            var provider = configuration?["DatabaseProvider"] ?? "LocalDB";
            return provider.Equals("LocalDB", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if the application is using Azure SQL for data storage
        /// </summary>
        /// <param name="configuration">Optional configuration to check database provider</param>
        /// <returns>True if using Azure SQL, false otherwise</returns>
        public static bool IsUsingAzureSql(IConfiguration? configuration = null)
        {
            var provider = configuration?["DatabaseProvider"] ?? "LocalDB";
            return provider.Equals("Azure", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if the application is using SQLite for data storage (Phase 1 compatibility)
        /// </summary>
        /// <param name="configuration">Optional configuration to check database provider</param>
        /// <returns>True if using SQLite, false otherwise</returns>
        public static bool IsUsingSqlite(IConfiguration? configuration = null)
        {
            var provider = configuration?["DatabaseProvider"] ?? "LocalDB";
            return provider.Equals("Local", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the appropriate connection string based on the environment and configuration
        /// </summary>
        /// <param name="configuration">Configuration to get connection strings from</param>
        /// <returns>The appropriate connection string for the current environment and database provider</returns>
        public static string GetConnectionString(IConfiguration configuration)
        {
            if (IsUsingAzureSql(configuration))
            {
                return configuration.GetConnectionString("AzureConnection") ??
                       configuration.GetConnectionString("DefaultConnection") ??
                       "Data Source=BusBuddy.db";
            }
            else if (IsUsingLocalDb(configuration))
            {
                return configuration.GetConnectionString("LocalConnection") ??
                       configuration.GetConnectionString("DefaultConnection") ??
                       "Data Source=BusBuddy.db";
            }
            else
            {
                return configuration.GetConnectionString("BusBuddyDatabase") ??
                       configuration.GetConnectionString("DefaultConnection") ??
                       "Data Source=BusBuddy.db";
            }
        }
    }
}
