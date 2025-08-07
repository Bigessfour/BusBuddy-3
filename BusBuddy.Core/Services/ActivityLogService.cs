using BusBuddy.Core.Data;
using BusBuddy.Core.Logging;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BusBuddy.Core.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly BusBuddy.Core.Data.BusBuddyDbContext _db;
        private static readonly ILogger Logger = Log.ForContext<ActivityLogService>();

        public ActivityLogService(BusBuddyDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(string action, string user, string? details = null)
        {
            try
            {
                Logger.Debug("[ACTIVITY_ENTRY] Starting LogActivity - Action: {Action}, User: {User}", action, user);

                // Truncate details if they exceed the database column size (1000 chars)
                string? truncatedDetails = details;
                if (details != null && details.Length > 995)
                {
                    truncatedDetails = string.Concat(details.AsSpan(0, 990), "[...]");
                }

                var log = new ActivityLog
                {
                    Timestamp = DateTime.Now,
                    Action = action,
                    User = user,
                    Details = truncatedDetails
                };
                _db.ActivityLogs.Add(log);
                await _db.SaveChangesAsync();

                Logger.Debug("[ACTIVITY_EXIT] Completed LogActivity - Added log entry with ID: {LogId}", log.Id);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error logging activity: {Action} by {User}", action, user);
                throw;
            }
        }

        public async Task<IEnumerable<ActivityLog>> GetLogsAsync(int count = 100)
        {
            try
            {
                Logger.Debug("[ACTIVITY_ENTRY] Starting GetLogs - Count: {Count}", count);

                var logs = await _db.ActivityLogs
                    .AsNoTracking() // Read-only query for better performance
                    .OrderByDescending(l => l.Timestamp)
                    .Take(count)
                    .ToListAsync();

                Logger.Debug("[ACTIVITY_EXIT] Completed GetLogs - Retrieved {LogCount} log entries", logs.Count);
                return logs;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity logs");
                throw;
            }
        }

        /// <summary>
        /// Get activity logs with pagination support for better performance
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of records per page (default: 50)</param>
        /// <returns>Paged activity logs</returns>
        public async Task<IEnumerable<ActivityLog>> GetLogsPagedAsync(int pageNumber = 1, int pageSize = 50)
        {
            try
            {
                Logger.Debug("[ACTIVITY_ENTRY] Starting GetLogsPaged - Page: {PageNumber}, Size: {PageSize}", pageNumber, pageSize);

                var logs = await _db.ActivityLogs
                    .AsNoTracking() // Read-only query for better performance
                    .OrderByDescending(l => l.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                Logger.Debug("[ACTIVITY_EXIT] Completed GetLogsPaged - Retrieved {LogCount} log entries for page {PageNumber}", logs.Count, pageNumber);
                return logs;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving paged activity logs");
                throw;
            }
        }

        public async Task<IEnumerable<ActivityLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate, int count = 1000)
        {
            try
            {
                Logger.Debug("[ACTIVITY_ENTRY] Starting GetLogsByDateRange - StartDate: {StartDate}, EndDate: {EndDate}, Count: {Count}", startDate, endDate, count);

                var logs = await _db.ActivityLogs
                    .AsNoTracking() // Read-only query for better performance
                    .Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(count)
                    .ToListAsync();

                Logger.Debug("[ACTIVITY_EXIT] Completed GetLogsByDateRange - Retrieved {LogCount} log entries", logs.Count);
                return logs;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity logs by date range");
                throw;
            }
        }

        public async Task<IEnumerable<ActivityLog>> GetLogsByUserAsync(string user, int count = 100)
        {
            try
            {
                Logger.Debug("[ACTIVITY_ENTRY] Starting GetLogsByUser - User: {User}, Count: {Count}", user, count);

                var logs = await _db.ActivityLogs
                    .AsNoTracking() // Read-only query for better performance
                    .Where(l => l.User == user)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(count)
                    .ToListAsync();

                Logger.Debug("[ACTIVITY_EXIT] Completed GetLogsByUser - Retrieved {LogCount} log entries", logs.Count);
                return logs;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity logs by user");
                throw;
            }
        }

        public async Task<IEnumerable<ActivityLog>> GetLogsByActionAsync(string action, int count = 100)
        {
            try
            {
                Logger.Debug("[ACTIVITY_ENTRY] Starting GetLogsByAction - Action: {Action}, Count: {Count}", action, count);

                var logs = await _db.ActivityLogs
                    .AsNoTracking() // Read-only query for better performance
                    .Where(l => l.Action.Contains(action, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(l => l.Timestamp)
                    .Take(count)
                    .ToListAsync();

                Logger.Debug("[ACTIVITY_EXIT] Completed GetLogsByAction - Retrieved {LogCount} log entries", logs.Count);
                return logs;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity logs by action");
                throw;
            }
        }

        public async Task LogEntityActionAsync<T>(string action, string user, T entity, int? entityId = null) where T : class
        {
            try
            {
                Logger.Debug("[ACTIVITY_ENTRY] Starting LogEntityAction - Action: {Action}, EntityType: {EntityType}", action, typeof(T).Name);

                // Extract entity type name for the log
                string entityType = typeof(T).Name;

                // Try to get the entity ID through reflection if not provided
                int? id = entityId;
                if (id == null)
                {
                    var idProperty = typeof(T).GetProperty("Id");
                    if (idProperty != null)
                    {
                        id = idProperty.GetValue(entity) as int?;
                    }
                }

                // Create a structured details string with important entity info
                var details = new
                {
                    EntityType = entityType,
                    EntityId = id,
                    // Serialize at most 20 properties to avoid excessive detail
                    EntityData = SerializeEntityForLogging(entity)
                };

                // Format the action to include entity info
                string formattedAction = $"{action} {entityType}";
                if (id.HasValue)
                {
                    formattedAction += $" #{id}";
                }

                // Log with structured details
                await LogAsync(formattedAction, user, JsonSerializer.Serialize(details));

                Logger.Debug("[ACTIVITY_EXIT] Completed LogEntityAction - Logged entity action with type {EntityType} and ID {EntityId}", entityType, id);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error logging entity action for type {EntityType}", typeof(T).Name);

                // Fallback to simple logging if serialization fails
                try
                {
                    await LogAsync($"{action} {typeof(T).Name}", user,
                        $"Error creating detailed log: {ex.Message}");
                }
                catch (Exception fallbackEx)
                {
                    Logger.Error(fallbackEx, "Fallback logging also failed for entity action");
                    throw;
                }
            }
        }

        private string SerializeEntityForLogging<T>(T entity) where T : class
        {
            try
            {
                Logger.Debug("[ACTIVITY_ENTRY] Starting SerializeEntityForLogging - EntityType: {EntityType}", typeof(T).Name);

                // Use a temporary dictionary to collect the properties we want to log
                var propertiesToLog = new Dictionary<string, object?>();

                // Get public properties (limit to reasonable number to avoid excessive details)
                var properties = typeof(T).GetProperties()
                    .Where(p => p.CanRead)
                    .Take(20);

                foreach (var prop in properties)
                {
                    // Skip complex navigation properties to avoid excessive serialization
                    if (prop.PropertyType.IsClass &&
                        prop.PropertyType != typeof(string) &&
                        !prop.PropertyType.IsPrimitive)
                    {
                        continue;
                    }

                    try
                    {
                        propertiesToLog[prop.Name] = prop.GetValue(entity);
                    }
                    catch
                    {
                        // Skip properties that can't be read
                        propertiesToLog[prop.Name] = "[error reading property]";
                    }
                }

                var result = JsonSerializer.Serialize(propertiesToLog);
                Logger.Debug("[ACTIVITY_EXIT] Completed SerializeEntityForLogging - Serialized entity with {PropertyCount} properties", propertiesToLog.Count);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error serializing entity of type {EntityType} for logging", typeof(T).Name);
                return "[Entity data unavailable]";
            }
        }
    }
}
