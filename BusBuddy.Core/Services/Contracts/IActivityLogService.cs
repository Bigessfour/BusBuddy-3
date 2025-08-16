using BusBuddy.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusBuddy.Core.Services.Contracts;

public interface IActivityLogService
{
    Task LogAsync(string action, string user, string details = "");
    Task<IEnumerable<ActivityLog>> GetLogsAsync(int count = 100);
    Task<IEnumerable<ActivityLog>> GetLogsPagedAsync(int pageNumber = 1, int pageSize = 50);
    Task<IEnumerable<ActivityLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate, int count = 1000);
    Task<IEnumerable<ActivityLog>> GetLogsByUserAsync(string user, int count = 100);
    Task<IEnumerable<ActivityLog>> GetLogsByActionAsync(string action, int count = 100);
    Task LogEntityActionAsync<T>(string action, string user, T entity, int entityId = 0) where T : class;
}
