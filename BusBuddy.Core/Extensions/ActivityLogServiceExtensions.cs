using BusBuddy.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusBuddy.Core.Extensions
{
    /// <summary>
    /// Extension methods for IActivityLogService to handle common logging scenarios
    /// </summary>
    public static class ActivityLogServiceExtensions
    {
        /// <summary>
        /// Logs an exception with properly formatted and truncated details
        /// </summary>
        public static async Task LogExceptionAsync(this IActivityLogService logService,
            string action,
            string user,
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(logService);
            ArgumentNullException.ThrowIfNull(exception);

            // Create a well-structured exception detail that focuses on the most important information
            // while staying within database column size limits
            string details = FormatExceptionForLogging(exception);

            await logService.LogAsync(action, user, details);
        }

        /// <summary>
        /// Logs a CRUD operation for an entity with appropriate context
        /// </summary>
        public static async Task LogCreateAsync<T>(this IActivityLogService logService, string user, T entity) where T : class
        {
            ArgumentNullException.ThrowIfNull(logService);
            await logService.LogEntityActionAsync("Created", user, entity);
        }

        /// <summary>
        /// Logs an update operation for an entity with appropriate context
        /// </summary>
        public static async Task LogUpdateAsync<T>(this IActivityLogService logService, string user, T entity) where T : class
        {
            ArgumentNullException.ThrowIfNull(logService);
            await logService.LogEntityActionAsync("Updated", user, entity);
        }

        /// <summary>
        /// Logs a delete operation for an entity with appropriate context
        /// </summary>
        public static async Task LogDeleteAsync<T>(this IActivityLogService logService, string user, T entity) where T : class
        {
            ArgumentNullException.ThrowIfNull(logService);
            await logService.LogEntityActionAsync("Deleted", user, entity);
        }

        /// <summary>
        /// Logs a view operation for an entity with appropriate context
        /// </summary>
        public static async Task LogViewAsync<T>(this IActivityLogService logService, string user, T entity) where T : class
        {
            ArgumentNullException.ThrowIfNull(logService);
            await logService.LogEntityActionAsync("Viewed", user, entity);
        }

        /// <summary>
        /// Logs a bulk operation on multiple entities
        /// </summary>
        public static async Task LogBulkOperationAsync<T>(this IActivityLogService logService,
            string operation,
            string user,
            IEnumerable<T> entities,
            string? additionalDetails = null) where T : class
        {
            string entityType = typeof(T).Name;
            int count = 0;

            // Count entities without materializing the entire collection if it's an IQueryable
            foreach (var _ in entities)
            {
                count++;
                if (count > 100)
                {
                    break; // Limit counting to avoid performance issues
                }

            }

            string countDisplay = count > 100 ? "100+" : count.ToString();
            string details = $"Bulk operation on {countDisplay} {entityType} entities";

            if (!string.IsNullOrEmpty(additionalDetails))
            {
                details += $". {additionalDetails}";
            }

            await logService.LogAsync($"{operation} {entityType} (Bulk)", user, details);
        }

        /// <summary>
        /// Logs user authentication events
        /// </summary>
        public static async Task LogUserAuthenticationAsync(this IActivityLogService logService,
            string user,
            bool success,
            string? additionalInfo = null)
        {
            string action = success ? "User Login" : "Failed Login Attempt";
            string details = additionalInfo ?? string.Empty;

            await logService.LogAsync(action, user, details);
        }

        /// <summary>
        /// Logs a system event that isn't tied to a specific entity
        /// </summary>
        public static async Task LogSystemEventAsync(this IActivityLogService logService,
            string eventName,
            string user,
            string? details = null)
        {
            ArgumentNullException.ThrowIfNull(logService);
            await logService.LogAsync($"System: {eventName}", user, details);
        }

        /// <summary>
        /// Logs a configuration change
        /// </summary>
        public static async Task LogConfigurationChangeAsync(this IActivityLogService logService,
            string user,
            string configSection,
            string? oldValue = null,
            string? newValue = null)
        {
            ArgumentNullException.ThrowIfNull(logService);
            string details = $"Changed configuration in section: {configSection}";

            if (oldValue != null && newValue != null)
            {
                details += $"\nPrevious value: {oldValue}\nNew value: {newValue}";
            }

            await logService.LogAsync("Configuration Change", user, details);
        }

        /// <summary>
        /// Formats an exception for database logging with size constraints in mind
        /// </summary>
        private static string FormatExceptionForLogging(Exception exception)
        {
            // Max length is 990 to leave room for "[...]" if needed
            const int maxLength = 990;

            // Start with the most important information
            var formatted = $"Type: {exception.GetType().Name}\n" +
                            $"Message: {exception.Message}\n";

            // Add stack trace information if there's room (first 3 frames are usually most relevant)
            var stackTrace = exception.StackTrace;
            if (stackTrace != null)
            {
                var frames = stackTrace.Split('\n');
                var relevantFrames = frames.Take(Math.Min(3, frames.Length));
                formatted += $"Stack: {string.Join(" | ", relevantFrames)}\n";
            }

            // Add inner exception info if present
            if (exception.InnerException != null)
            {
                formatted += $"Inner: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
            }

            // Truncate if necessary
            if (formatted.Length > maxLength)
            {
                return string.Concat(formatted.AsSpan(0, maxLength), "[...]");
            }

            return formatted;
        }
    }
}
