using System;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using Serilog;

namespace BusBuddy.Core.Utilities
{
    /// <summary>
    /// Helper class for environment-related utilities
    /// </summary>
    public static class EnvironmentHelper
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(EnvironmentHelper));
        /// <summary>
        /// Checks if the application is running in development mode
        /// </summary>
        /// <param name="configuration">Optional configuration to check environment from</param>
        /// <returns>True if running in development environment, false otherwise</returns>
        public static bool IsDevelopment(IConfiguration? configuration = null)
        {
            var environment = configuration?["Environment"]
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            return environment != default &&
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
            string raw = string.Empty;

            if (IsUsingAzureSql(configuration))
            {
                // Priority order for Azure SQL:
                // 1. Managed Identity (for production Azure hosting)
                // 2. Entra ID Default (for local dev with az login)
                // 3. Interactive (for manual login)
                // 4. Traditional SQL auth (legacy fallback)
                // 5. Fallback options
                raw = configuration.GetConnectionString("AzureManagedIdentityConnection") ??
                      configuration.GetConnectionString("AzureEntraIDConnection") ??
                      configuration.GetConnectionString("AzureInteractiveConnection") ??
                      configuration.GetConnectionString("AzureADConnection") ??
                      configuration.GetConnectionString("AzureConnection") ??
                      configuration.GetConnectionString("DefaultConnection") ??
                      "Data Source=BusBuddy.db";
            }
            else if (IsUsingLocalDb(configuration))
            {
                raw = configuration.GetConnectionString("LocalConnection") ??
                      configuration.GetConnectionString("DefaultConnection") ??
                      "Data Source=BusBuddy.db";
            }
            else
            {
                raw = configuration.GetConnectionString("BusBuddyDatabase") ??
                      configuration.GetConnectionString("DefaultConnection") ??
                      "Data Source=BusBuddy.db";
            }

            // Expand any ${ENV_VAR} placeholders
            var expanded = ExpandEnvironmentPlaceholders(raw);

            // If placeholders remain unresolved (common when AZURE_* env vars are not set),
            // fall back to a reliable LocalDB connection for local/dev usage.
            if (!string.IsNullOrWhiteSpace(expanded) && expanded.Contains("${"))
            {
                try
                {
                    Logger?.Warning("Connection string contains unresolved placeholders. Falling back to LocalDB for reliability.");
                }
                catch { /* logging is best-effort here */ }

                var localDbFallback = configuration.GetConnectionString("LocalConnection")
                    ?? "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True";
                return localDbFallback;
            }

            return expanded;
        }

        /// <summary>
        /// Expands ${ENV_VAR} placeholders in configuration strings using current process environment variables.
        /// Leaves placeholders intact if the environment variable is not set.
        /// </summary>
        private static string ExpandEnvironmentPlaceholders(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return Regex.Replace(value, @"\$\{([A-Za-z0-9_]+)\}", match =>
            {
                var varName = match.Groups[1].Value;
                var envValue = Environment.GetEnvironmentVariable(varName);
                return string.IsNullOrEmpty(envValue) ? match.Value : envValue;
            });
        }
    }
}
