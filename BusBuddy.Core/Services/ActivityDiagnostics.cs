#if DEBUG
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>DEBUG-only activity metrics. Not activity CRUD or PDF.</summary>
public static class ActivityDiagnostics
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ActivityDiagnostics));

    public static async Task<Dictionary<string, object>> GetActivityDiagnosticsAsync(
        BusBuddyDbContext context,
        int activityId)
    {
        try
        {
            Logger.Information("Retrieving diagnostics for activity {ActivityId}", activityId);

            var activity = await context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Driver)
                .Include(a => a.Route)
                .FirstOrDefaultAsync(a => a.ActivityId == activityId);

            if (activity == null)
            {
                return new Dictionary<string, object>
                {
                    ["Error"] = $"Activity with ID {activityId} not found"
                };
            }

            var duration = (activity.ReturnTime - activity.LeaveTime).TotalHours;
            var daysUntilActivity = (activity.Date.Date - DateTime.Today).TotalDays;
            var conflictCount = await context.Activities
                .CountAsync(a =>
                    a.ActivityId != activityId &&
                    a.Date.Date == activity.Date.Date &&
                    ((a.LeaveTime <= activity.LeaveTime && a.ReturnTime > activity.LeaveTime) ||
                     (a.LeaveTime < activity.ReturnTime && a.ReturnTime >= activity.ReturnTime) ||
                     (a.LeaveTime >= activity.LeaveTime && a.ReturnTime <= activity.ReturnTime)) &&
                    (a.DriverId == activity.DriverId || a.AssignedVehicleId == activity.AssignedVehicleId));

            var driverActivityCount = await context.Activities
                .CountAsync(a => a.DriverId == activity.DriverId);

            var vehicleActivityCount = await context.Activities
                .CountAsync(a => a.AssignedVehicleId == activity.AssignedVehicleId);

            return new Dictionary<string, object>
            {
                ["ActivityId"] = activity.ActivityId,
                ["ActivityType"] = activity.ActivityType ?? "Unknown",
                ["Date"] = activity.Date,
                ["TimeRange"] = $"{activity.LeaveTime} - {activity.ReturnTime}",
                ["Duration"] = duration,
                ["Status"] = activity.Status ?? "Unknown",
                ["Description"] = activity.Description ?? "No description",
                ["Destination"] = activity.Destination ?? "No destination",
                ["Driver"] = activity.Driver != null ? $"{activity.Driver.FullName}" : "No driver",
                ["Vehicle"] = activity.AssignedVehicle != null ? $"{activity.AssignedVehicle.BusNumber}" : "No vehicle",
                ["Route"] = activity.Route != null ? activity.Route.RouteName : "No route",
                ["RequestedBy"] = activity.RequestedBy ?? "Unknown",
                ["CreatedDate"] = activity.CreatedDate,
                ["UpdatedDate"] = activity.UpdatedDate ?? DateTime.MinValue,
                ["ApprovalDate"] = activity.ApprovalDate ?? DateTime.MinValue,
                ["ApprovedBy"] = activity.ApprovedBy ?? "Not approved",
                ["DaysUntilActivity"] = daysUntilActivity,
                ["PotentialConflicts"] = conflictCount,
                ["DriverTotalActivities"] = driverActivityCount,
                ["VehicleTotalActivities"] = vehicleActivityCount,
                ["IsValidTimeRange"] = activity.LeaveTime < activity.ReturnTime,
                ["Notes"] = activity.Notes ?? "No notes",
                ["ExpectedPassengers"] = activity.ExpectedPassengers ?? 0,
                ["HasRecurringSeries"] = activity.RecurringSeriesId.HasValue,
                ["RecurringSeriesId"] = activity.RecurringSeriesId ?? 0
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activity diagnostics");
            return new Dictionary<string, object>
            {
                ["Error"] = ex.Message,
                ["StackTrace"] = ex.StackTrace ?? "No stack trace"
            };
        }
    }

    public static async Task<Dictionary<string, object>> GetScheduleOperationMetricsAsync(BusBuddyDbContext context)
    {
        try
        {
            Logger.Information("Retrieving schedule operation metrics");

            var now = DateTime.UtcNow;
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var nextWeek = today.AddDays(7);
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            var nextMonth = thisMonth.AddMonths(1);

            var totalCount = await context.Activities.CountAsync();
            var todayCount = await context.Activities.CountAsync(a => a.Date.Date == today);
            var tomorrowCount = await context.Activities.CountAsync(a => a.Date.Date == tomorrow);
            var nextWeekCount = await context.Activities.CountAsync(a => a.Date >= today && a.Date < nextWeek);
            var thisMonthCount = await context.Activities.CountAsync(a => a.Date >= thisMonth && a.Date < nextMonth);

            var pendingApprovalCount = await context.Activities.CountAsync(a => a.Status == "PendingApproval");
            var approvedCount = await context.Activities.CountAsync(a => a.Status == "Approved");
            var completedCount = await context.Activities.CountAsync(a => a.Status == "Completed");
            var cancelledCount = await context.Activities.CountAsync(a => a.Status == "Cancelled");

            var typeDistribution = await context.Activities
                .GroupBy(a => a.ActivityType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type ?? "Unknown", x => x.Count);

            var mostActiveDrivers = await context.Activities
                .Where(a => a.Date >= thisMonth && a.Date < nextMonth && a.DriverId > 0)
                .GroupBy(a => a.DriverId)
                .Select(g => new { DriverId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            var driverNames = await context.Drivers
                .Where(d => mostActiveDrivers.Select(m => m.DriverId).Contains(d.DriverId))
                .ToDictionaryAsync(d => d.DriverId, d => d.FullName);

            var topDrivers = mostActiveDrivers
                .Select(d => new
                {
                    DriverId = d.DriverId,
                    Name = d.DriverId.HasValue && driverNames.TryGetValue(d.DriverId.Value, out var driverName)
                        ? driverName
                        : $"Driver {d.DriverId}",
                    Count = d.Count
                })
                .ToDictionary(d => d.Name, d => d.Count);

            return new Dictionary<string, object>
            {
                ["TotalActivityCount"] = totalCount,
                ["TodayActivityCount"] = todayCount,
                ["TomorrowActivityCount"] = tomorrowCount,
                ["NextWeekActivityCount"] = nextWeekCount,
                ["ThisMonthActivityCount"] = thisMonthCount,
                ["PendingApprovalCount"] = pendingApprovalCount,
                ["ApprovedCount"] = approvedCount,
                ["CompletedCount"] = completedCount,
                ["CancelledCount"] = cancelledCount,
                ["TypeDistribution"] = typeDistribution,
                ["MostActiveDrivers"] = topDrivers,
                ["GeneratedAt"] = now
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving schedule operation metrics");
            return new Dictionary<string, object>
            {
                ["Error"] = ex.Message,
                ["StackTrace"] = ex.StackTrace ?? "No stack trace"
            };
        }
    }
}
#endif
