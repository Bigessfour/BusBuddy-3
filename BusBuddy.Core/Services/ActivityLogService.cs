using BusBuddy.Core.Data;
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
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IUserSettingsService? _userSettings;
        private static readonly ILogger Logger = Log.ForContext<ActivityLogService>();

        public ActivityLogService(IBusBuddyDbContextFactory contextFactory, IUserSettingsService? userSettings = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _userSettings = userSettings;
        }

        public async Task LogAsync(string action, string user, string? details = null)
        {
            if (_userSettings is not null && !_userSettings.EnableActivityLogging)
            {
                Logger.Debug("Activity logging disabled — skipping action {Action} for {User}", action, user);
                return;
            }

            try
            {
                string? truncatedDetails = details;
                if (details != null && details.Length > 995)
                {
                    truncatedDetails = string.Concat(details.AsSpan(0, 990), "[...]");
                }

                await WithWriteContextAsync(async ctx =>
                {
                    ctx.ActivityLogs.Add(new ActivityLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Action = action,
                        User = user,
                        Details = truncatedDetails
                    });
                    await ctx.SaveChangesAsync().ConfigureAwait(false);
                    return 0;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error logging activity: {Action} by {User}", action, user);
                throw;
            }
        }

        public Task<IEnumerable<ActivityLog>> GetLogsAsync(int count = 100) =>
            WithReadContextAsync(ctx =>
                ctx.ActivityLogs
                    .AsNoTracking()
                    .OrderByDescending(l => l.Timestamp)
                    .Take(count)
                    .ToListAsync());

        public Task<IEnumerable<ActivityLog>> GetLogsPagedAsync(int pageNumber = 1, int pageSize = 50) =>
            WithReadContextAsync(ctx =>
                ctx.ActivityLogs
                    .AsNoTracking()
                    .OrderByDescending(l => l.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync());

        public Task<IEnumerable<ActivityLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate, int count = 1000) =>
            WithReadContextAsync(ctx =>
                ctx.ActivityLogs
                    .AsNoTracking()
                    .Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(count)
                    .ToListAsync());

        public Task<IEnumerable<ActivityLog>> GetLogsByUserAsync(string user, int count = 100) =>
            WithReadContextAsync(ctx =>
                ctx.ActivityLogs
                    .AsNoTracking()
                    .Where(l => l.User == user)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(count)
                    .ToListAsync());

        public Task<IEnumerable<ActivityLog>> GetLogsByActionAsync(string action, int count = 100)
        {
#pragma warning disable CA1311, CA1862
            var needle = action.ToLower();
            return WithReadContextAsync(ctx =>
                ctx.ActivityLogs
                    .AsNoTracking()
                    .Where(l => l.Action.ToLower().Contains(needle))
                    .OrderByDescending(l => l.Timestamp)
                    .Take(count)
                    .ToListAsync());
#pragma warning restore CA1311, CA1862
        }

        public async Task LogEntityActionAsync<T>(string action, string user, T entity, int? entityId = null) where T : class
        {
            try
            {
                string entityType = typeof(T).Name;
                int? id = entityId;
                if (id == null)
                {
                    var idProperty = typeof(T).GetProperty("Id");
                    if (idProperty != null)
                    {
                        id = idProperty.GetValue(entity) as int?;
                    }
                }

                var details = new
                {
                    EntityType = entityType,
                    EntityId = id,
                    EntityData = SerializeEntityForLogging(entity)
                };

                string formattedAction = $"{action} {entityType}";
                if (id.HasValue)
                {
                    formattedAction += $" #{id}";
                }

                await LogAsync(formattedAction, user, JsonSerializer.Serialize(details)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error logging entity action for type {EntityType}", typeof(T).Name);
                await LogAsync($"{action} {typeof(T).Name}", user, $"Error creating detailed log: {ex.Message}")
                    .ConfigureAwait(false);
            }
        }

        private async Task<IEnumerable<ActivityLog>> WithReadContextAsync(
            Func<BusBuddyDbContext, Task<List<ActivityLog>>> query)
        {
            var ctx = _contextFactory.CreateDbContext();
            try
            {
                return await query(ctx).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity logs");
                throw;
            }
            finally
            {
                await ctx.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task<T> WithWriteContextAsync<T>(Func<BusBuddyDbContext, Task<T>> work)
        {
            var ctx = _contextFactory.CreateWriteDbContext();
            try
            {
                return await work(ctx).ConfigureAwait(false);
            }
            finally
            {
                await ctx.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static string SerializeEntityForLogging<T>(T entity) where T : class
        {
            try
            {
                var propertiesToLog = new Dictionary<string, object?>();
                var properties = typeof(T).GetProperties()
                    .Where(p => p.CanRead)
                    .Take(20);

                foreach (var prop in properties)
                {
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
                        propertiesToLog[prop.Name] = "[error reading property]";
                    }
                }

                return JsonSerializer.Serialize(propertiesToLog);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error serializing entity of type {EntityType} for logging", typeof(T).Name);
                return "[Entity data unavailable]";
            }
        }
    }
}
