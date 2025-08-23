using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service for managing Activity/Schedule operations
/// </summary>
public class ActivityService : IActivityService
{
    private readonly BusBuddy.Core.Data.BusBuddyDbContext _context;
    private readonly PdfReportService _pdfReportService;
    private static readonly ILogger Logger = Log.ForContext<ActivityService>();

    public ActivityService(BusBuddyDbContext context, PdfReportService pdfReportService)
    {
        _context = context;
        _pdfReportService = pdfReportService;
    }

    public async Task<IEnumerable<Activity>> GetAllActivitiesAsync()
    {
        try
        {
            Logger.Information("Retrieving all activities");
            return await _context.Activities
            .Include(a => a.AssignedVehicle)
            .Include(a => a.Route)
            .Include(a => a.Driver)
            .OrderByDescending(a => a.Date)
            .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities");
            throw;
        }
    }

    public async Task<Activity?> GetActivityByIdAsync(int id)
    {
        try
        {
            Logger.Information("Retrieving activity with ID: {ActivityId}", id);
            return await _context.Activities
            .Include(a => a.AssignedVehicle)
            .Include(a => a.Route)
            .Include(a => a.Driver)
            .FirstOrDefaultAsync(a => a.ActivityId == id);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activity with ID: {ActivityId}", id);
            throw;
        }
    }

    public async Task<Activity> CreateActivityAsync(Activity activity)
    {
        try
        {
            Logger.Information("Creating new activity for date: {ActivityDate}", activity.Date);

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            Logger.Information("Successfully created activity with ID: {ActivityId}", activity.ActivityId);
            return activity;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating activity");
            throw;
        }
    }

    public async Task<Activity> UpdateActivityAsync(Activity activity)
    {
        try
        {
            Logger.Information("Updating activity with ID: {ActivityId}", activity.ActivityId);

            _context.Activities.Update(activity);
            await _context.SaveChangesAsync();

            Logger.Information("Successfully updated activity with ID: {ActivityId}", activity.ActivityId);
            return activity;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating activity with ID: {ActivityId}", activity.ActivityId);
            throw;
        }
    }

    public async Task<bool> DeleteActivityAsync(int id)
    {
        try
        {
            Logger.Information("Deleting activity with ID: {ActivityId}", id);

            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
            {
                Logger.Warning("Activity with ID {ActivityId} not found for deletion", id);
                return false;
            }

            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            Logger.Information("Successfully deleted activity with ID: {ActivityId}", id);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting activity with ID: {ActivityId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            Logger.Information("Retrieving activities between {StartDate} and {EndDate}", startDate, endDate);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.LeaveTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities for date range");
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByRouteAsync(int routeId)
    {
        try
        {
            Logger.Information("Retrieving activities for route ID: {RouteId}", routeId);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.RouteId == routeId)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities for route ID: {RouteId}", routeId);
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByDriverAsync(int driverId)
    {
        try
        {
            Logger.Information("Retrieving activities for driver ID: {DriverId}", driverId);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.DriverId == driverId)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities for driver ID: {DriverId}", driverId);
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByVehicleAsync(int vehicleId)
    {
        try
        {
            Logger.Information("Retrieving activities for vehicle ID: {VehicleId}", vehicleId);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.AssignedVehicleId == vehicleId)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities for vehicle ID: {VehicleId}", vehicleId);
            throw;
        }
    }

    // Additional interface implementations
    public async Task<IEnumerable<Activity>> GetActivitiesByDateAsync(DateTime activityDate)
    {
        try
        {
            Logger.Information("Retrieving activities for date: {Date}", activityDate);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.Date.Date == activityDate.Date)
                .OrderBy(a => a.LeaveTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities for date: {Date}", activityDate);
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetUpcomingActivitiesAsync(int days = 7)
    {
        try
        {
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(days);

            Logger.Information("Retrieving upcoming activities for next {Days} days", days);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.LeaveTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving upcoming activities");
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByTypeAsync(string activityType)
    {
        try
        {
            Logger.Information("Retrieving activities for type: {ActivityType}", activityType);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.ActivityType == activityType)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities for type: {ActivityType}", activityType);
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByStatusAsync(string status)
    {
        try
        {
            Logger.Information("Retrieving activities with status: {Status}", status);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.Status == status)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities with status: {Status}", status);
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByRequestorAsync(string requestedBy)
    {
        try
        {
            Logger.Information("Retrieving activities requested by: {RequestedBy}", requestedBy);
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.RequestedBy == requestedBy)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities requested by: {RequestedBy}", requestedBy);
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> SearchActivitiesAsync(string searchTerm)
    {
        try
        {
            Logger.Information("Searching activities with term: {SearchTerm}", searchTerm);

            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a =>
                    a.ActivityType.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (a.Description != null && a.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    a.Destination.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.RequestedBy.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (a.Notes != null && a.Notes.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error searching activities with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    public async Task<bool> AssignDriverToActivityAsync(int activityId, int driverId)
    {
        try
        {
            Logger.Information("Assigning driver {DriverId} to activity {ActivityId}", driverId, activityId);

            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null)
            {
                Logger.Warning("Activity {ActivityId} not found for driver assignment", activityId);
                return false;
            }

            activity.DriverId = driverId;
            activity.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Logger.Information("Successfully assigned driver {DriverId} to activity {ActivityId}", driverId, activityId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error assigning driver {DriverId} to activity {ActivityId}", driverId, activityId);
            throw;
        }
    }

    public async Task<bool> AssignVehicleToActivityAsync(int activityId, int vehicleId)
    {
        try
        {
            Logger.Information("Assigning vehicle {VehicleId} to activity {ActivityId}", vehicleId, activityId);

            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null)
            {
                Logger.Warning("Activity {ActivityId} not found for vehicle assignment", activityId);
                return false;
            }

            activity.AssignedVehicleId = vehicleId;
            activity.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Logger.Information("Successfully assigned vehicle {VehicleId} to activity {ActivityId}", vehicleId, activityId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error assigning vehicle {VehicleId} to activity {ActivityId}", vehicleId, activityId);
            throw;
        }
    }

    public async Task<bool> UpdateActivityStatusAsync(int activityId, string status)
    {
        try
        {
            Logger.Information("Updating activity {ActivityId} status to {Status}", activityId, status);

            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null)
            {
                Logger.Warning("Activity {ActivityId} not found for status update", activityId);
                return false;
            }

            activity.Status = status;
            activity.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Logger.Information("Successfully updated activity {ActivityId} status to {Status}", activityId, status);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating activity {ActivityId} status to {Status}", activityId, status);
            throw;
        }
    }

    public async Task<List<Driver>> GetAvailableDriversForActivityAsync(DateTime activityDate, TimeSpan startTime, TimeSpan endTime)
    {
        try
        {
            Logger.Information("Finding available drivers for activity on {Date} from {StartTime} to {EndTime}",
                activityDate, startTime, endTime);

            // Get all active drivers
            var allDrivers = await _context.Drivers
                .Where(d => d.Status == "Active")
                .ToListAsync();

            // Get IDs of drivers who are already scheduled during this time
            var busyDriverIds = await _context.Activities
                .Where(a =>
                    a.Date.Date == activityDate.Date &&
                    a.Status != "Cancelled" &&
                    ((a.LeaveTime <= startTime && a.ReturnTime > startTime) || // Overlaps start time
                     (a.LeaveTime < endTime && a.ReturnTime >= endTime) || // Overlaps end time
                     (a.LeaveTime >= startTime && a.ReturnTime <= endTime))) // Within time range
                .Select(a => a.DriverId)
                .Distinct()
                .ToListAsync();

            // Return drivers who aren't busy
            return allDrivers
                .Where(d => !busyDriverIds.Contains(d.DriverId))
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error finding available drivers for activity");
            throw;
        }
    }

    public async Task<List<Bus>> GetAvailableVehiclesForActivityAsync(DateTime activityDate, TimeSpan startTime, TimeSpan endTime)
    {
        try
        {
            Logger.Information("Finding available vehicles for activity on {Date} from {StartTime} to {EndTime}",
                activityDate, startTime, endTime);

            // Get all active buses
            var allBuses = await _context.Buses
                .Where(v => v.Status == "Active")
                .ToListAsync();

            // Get IDs of buses that are already scheduled during this time
            var busyBusIds = await _context.Activities
                .Where(a =>
                    a.Date.Date == activityDate.Date &&
                    a.Status != "Cancelled" &&
                    ((a.LeaveTime <= startTime && a.ReturnTime > startTime) || // Overlaps start time
                     (a.LeaveTime < endTime && a.ReturnTime >= endTime) || // Overlaps end time
                     (a.LeaveTime >= startTime && a.ReturnTime <= endTime))) // Within time range
                .Select(a => a.AssignedVehicleId)
                .Distinct()
                .ToListAsync();

            // Return buses that aren't busy
            return allBuses
                .Where(v => !busyBusIds.Contains(v.BusId))
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error finding available vehicles for activity");
            throw;
        }
    }

    public async Task<bool> IsDriverAvailableForActivityAsync(int driverId, DateTime activityDate, TimeSpan startTime, TimeSpan endTime)
    {
        try
        {
            Logger.Information("Checking if driver {DriverId} is available on {Date} from {StartTime} to {EndTime}",
                driverId, activityDate, startTime, endTime);

            // Check if the driver is scheduled during this time
            var conflicts = await _context.Activities
                .Where(a =>
                    a.DriverId == driverId &&
                    a.Date.Date == activityDate.Date &&
                    a.Status != "Cancelled" &&
                    ((a.LeaveTime <= startTime && a.ReturnTime > startTime) || // Overlaps start time
                     (a.LeaveTime < endTime && a.ReturnTime >= endTime) || // Overlaps end time
                     (a.LeaveTime >= startTime && a.ReturnTime <= endTime))) // Within time range
                .AnyAsync();

            return !conflicts;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error checking driver availability");
            throw;
        }
    }

    public async Task<bool> IsVehicleAvailableForActivityAsync(int vehicleId, DateTime activityDate, TimeSpan startTime, TimeSpan endTime)
    {
        try
        {
            Logger.Information("Checking if vehicle {VehicleId} is available on {Date} from {StartTime} to {EndTime}",
                vehicleId, activityDate, startTime, endTime);

            // Check if the vehicle is scheduled during this time
            var conflicts = await _context.Activities
                .Where(a =>
                    a.AssignedVehicleId == vehicleId &&
                    a.Date.Date == activityDate.Date &&
                    a.Status != "Cancelled" &&
                    ((a.LeaveTime <= startTime && a.ReturnTime > startTime) || // Overlaps start time
                     (a.LeaveTime < endTime && a.ReturnTime >= endTime) || // Overlaps end time
                     (a.LeaveTime >= startTime && a.ReturnTime <= endTime))) // Within time range
                .AnyAsync();

            return !conflicts;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error checking vehicle availability");
            throw;
        }
    }

    public async Task<List<Activity>> CreateRecurringActivitiesAsync(
        Activity baseActivity,
        DateTime startDate,
        DateTime endDate,
        RecurrenceType recurrenceType,
        int recurrenceInterval,
        List<DayOfWeek>? daysOfWeek = null)
    {
        var activities = new List<Activity>();

        try
        {
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                bool shouldCreateActivity = recurrenceType switch
                {
                    RecurrenceType.Daily => true,
                    RecurrenceType.Weekly => daysOfWeek?.Contains(currentDate.DayOfWeek) ?? true,
                    RecurrenceType.Monthly => currentDate.Day == startDate.Day,
                    RecurrenceType.Yearly => currentDate.DayOfYear == startDate.DayOfYear,
                    _ => false
                };

                if (shouldCreateActivity)
                {
                    var newActivity = new Activity
                    {
                        ActivityName = baseActivity.ActivityName,
                        Destination = baseActivity.Destination,
                        ActivityDate = currentDate,
                        DepartureTime = baseActivity.DepartureTime,
                        EstimatedArrival = baseActivity.EstimatedArrival,
                        RequestedBy = baseActivity.RequestedBy,
                        AssignedVehicleId = baseActivity.AssignedVehicleId,
                        AssignedDriverId = baseActivity.AssignedDriverId,
                        Notes = baseActivity.Notes,
                        CreatedDate = DateTime.UtcNow,
                        UpdatedDate = DateTime.UtcNow
                    };

                    _context.Activities.Add(newActivity);
                    activities.Add(newActivity);
                }

                currentDate = recurrenceType switch
                {
                    RecurrenceType.Daily => currentDate.AddDays(recurrenceInterval),
                    RecurrenceType.Weekly => currentDate.AddDays(7),
                    RecurrenceType.Monthly => currentDate.AddMonths(1),
                    RecurrenceType.Yearly => currentDate.AddYears(1),
                    _ => currentDate.AddDays(1)
                };
            }

            await _context.SaveChangesAsync();
            Logger.Information("Created {Count} recurring activities", activities.Count);

            return activities;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating recurring activities");
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetRecurringSeriesAsync(int activityId)
    {
        try
        {
            Logger.Information("Retrieving recurring series for activity {ActivityId}", activityId);

            // First get the activity to check if it's part of a series
            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null)
            {
                Logger.Warning("Activity {ActivityId} not found", activityId);
                return new List<Activity>();
            }

            // If it has a recurring series ID, use that, otherwise use its own ID
            var seriesId = activity.RecurringSeriesId ?? activity.ActivityId;

            // Get all activities in the series
            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.RecurringSeriesId == seriesId || a.ActivityId == seriesId)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.LeaveTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving recurring series for activity {ActivityId}", activityId);
            throw;
        }
    }

    public async Task<bool> UpdateRecurringSeriesAsync(Activity updatedActivity, bool updateAll)
    {
        try
        {
            Logger.Information("Updating recurring series for activity {ActivityId}, updateAll: {UpdateAll}",
                updatedActivity.ActivityId, updateAll);

            if (!updateAll)
            {
                // Update only this instance
                _context.Activities.Update(updatedActivity);
                await _context.SaveChangesAsync();
                return true;
            }

            // Get all activities in the series
            var seriesId = updatedActivity.RecurringSeriesId ?? updatedActivity.ActivityId;
            var activitiesInSeries = await _context.Activities
                .Where(a => (a.RecurringSeriesId == seriesId || a.ActivityId == seriesId) &&
                           a.Date >= updatedActivity.Date) // Only update this and future occurrences
                .ToListAsync();

            foreach (var activity in activitiesInSeries)
            {
                // Update properties but keep original date/time
                activity.ActivityType = updatedActivity.ActivityType;
                activity.Description = updatedActivity.Description;
                activity.Destination = updatedActivity.Destination;
                activity.DriverId = updatedActivity.DriverId;
                activity.AssignedVehicleId = updatedActivity.AssignedVehicleId;
                activity.RouteId = updatedActivity.RouteId;
                activity.RequestedBy = updatedActivity.RequestedBy;
                activity.Status = updatedActivity.Status;
                activity.ExpectedPassengers = updatedActivity.ExpectedPassengers;
                activity.Notes = updatedActivity.Notes;
                activity.UpdatedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            Logger.Information("Successfully updated {Count} activities in recurring series", activitiesInSeries.Count);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating recurring series for activity {ActivityId}", updatedActivity.ActivityId);
            throw;
        }
    }

    public async Task<bool> DeleteRecurringSeriesAsync(int activityId, bool deleteAll)
    {
        try
        {
            Logger.Information("Deleting recurring series for activity {ActivityId}, deleteAll: {DeleteAll}",
                activityId, deleteAll);

            // First get the activity to check if it's part of a series
            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null)
            {
                Logger.Warning("Activity {ActivityId} not found for deletion", activityId);
                return false;
            }

            if (!deleteAll)
            {
                // Delete only this instance
                _context.Activities.Remove(activity);
                await _context.SaveChangesAsync();
                return true;
            }

            // Get all activities in the series
            var seriesId = activity.RecurringSeriesId ?? activity.ActivityId;
            var activitiesInSeries = await _context.Activities
                .Where(a => (a.RecurringSeriesId == seriesId || a.ActivityId == seriesId) &&
                           a.Date >= activity.Date) // Only delete this and future occurrences
                .ToListAsync();

            _context.Activities.RemoveRange(activitiesInSeries);
            await _context.SaveChangesAsync();

            Logger.Information("Successfully deleted {Count} activities in recurring series", activitiesInSeries.Count);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting recurring series for activity {ActivityId}", activityId);
            throw;
        }
    }

    public async Task<bool> SubmitActivityForApprovalAsync(int activityId)
    {
        try
        {
            Logger.Information("Submitting activity {ActivityId} for approval", activityId);

            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null)
            {
                Logger.Warning("Activity {ActivityId} not found for approval submission", activityId);
                return false;
            }

            activity.Status = "PendingApproval";
            activity.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Logger.Information("Successfully submitted activity {ActivityId} for approval", activityId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error submitting activity {ActivityId} for approval", activityId);
            throw;
        }
    }

    public async Task<bool> ApproveActivityAsync(int activityId, string approvedBy)
    {
        try
        {
            Logger.Information("Approving activity {ActivityId} by {ApprovedBy}", activityId, approvedBy);

            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null)
            {
                Logger.Warning("Activity {ActivityId} not found for approval", activityId);
                return false;
            }

            activity.Status = "Approved";
            activity.ApprovedBy = approvedBy;
            activity.ApprovalDate = DateTime.UtcNow;
            activity.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Logger.Information("Successfully approved activity {ActivityId} by {ApprovedBy}", activityId, approvedBy);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error approving activity {ActivityId} by {ApprovedBy}", activityId, approvedBy);
            throw;
        }
    }

    public async Task<bool> RejectActivityAsync(int activityId, string rejectedBy, string rejectionReason)
    {
        try
        {
            Logger.Information("Rejecting activity {ActivityId} by {RejectedBy}: {Reason}",
                activityId, rejectedBy, rejectionReason);

            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null)
            {
                Logger.Warning("Activity {ActivityId} not found for rejection", activityId);
                return false;
            }

            activity.Status = "Rejected";
            activity.ApprovedBy = rejectedBy; // Reuse this field for the person who made the decision
            activity.ApprovalDate = DateTime.UtcNow;
            activity.Notes = string.IsNullOrEmpty(activity.Notes)
                ? $"Rejection reason: {rejectionReason}"
                : $"{activity.Notes}\nRejection reason: {rejectionReason}";
            activity.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Logger.Information("Successfully rejected activity {ActivityId} by {RejectedBy}", activityId, rejectedBy);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error rejecting activity {ActivityId} by {RejectedBy}", activityId, rejectedBy);
            throw;
        }
    }

    public async Task<IEnumerable<Activity>> GetPendingApprovalActivitiesAsync()
    {
        try
        {
            Logger.Information("Retrieving activities pending approval");

            return await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.Status == "PendingApproval")
                .OrderBy(a => a.Date)
                .ThenBy(a => a.LeaveTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving activities pending approval");
            throw;
        }
    }

    public async Task<List<Activity>> DetectScheduleConflictsAsync(Activity newActivity)
    {
        try
        {
            Logger.Information("Detecting schedule conflicts for activity on {Date} from {StartTime} to {EndTime}",
                newActivity.Date, newActivity.LeaveTime, newActivity.ReturnTime);

            // Check for conflicts with other activities
            var conflicts = await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Driver)
                .Where(a =>
                    a.ActivityId != newActivity.ActivityId && // Exclude the activity itself if it already exists
                    a.Date.Date == newActivity.Date.Date &&
                    a.Status != "Cancelled" &&
                    ((a.LeaveTime <= newActivity.LeaveTime && a.ReturnTime > newActivity.LeaveTime) || // Overlaps start time
                     (a.LeaveTime < newActivity.ReturnTime && a.ReturnTime >= newActivity.ReturnTime) || // Overlaps end time
                     (a.LeaveTime >= newActivity.LeaveTime && a.ReturnTime <= newActivity.ReturnTime)) && // Within time range
                    (a.DriverId == newActivity.DriverId || a.AssignedVehicleId == newActivity.AssignedVehicleId)) // Same driver or vehicle
                .ToListAsync();

            Logger.Information("Found {Count} schedule conflicts", conflicts.Count);
            return conflicts;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error detecting schedule conflicts");
            throw;
        }
    }

    public async Task<List<string>> ValidateActivityAsync(Activity activity)
    {
        var errors = new List<string>();

        try
        {
            Logger.Information("Validating activity for {Date}", activity.Date);

            // Check required fields
            if (string.IsNullOrEmpty(activity.ActivityType))
            {
                errors.Add("Activity type is required");
            }

            if (string.IsNullOrEmpty(activity.Description))
            {
                errors.Add("Description is required");
            }

            if (string.IsNullOrEmpty(activity.Destination))
            {
                errors.Add("Destination is required");
            }

            if (activity.LeaveTime >= activity.ReturnTime)
            {
                errors.Add("Leave time must be before return time");
            }

            if (activity.Date.Date < DateTime.Today)
            {
                errors.Add("Activity date cannot be in the past");
            }

            // Check for schedule conflicts

            if (activity.DriverId > 0 || activity.AssignedVehicleId > 0)
            {
                var conflicts = await DetectScheduleConflictsAsync(activity);

                if (conflicts.Count > 0)
                {
                    if (conflicts.Any(c => c.DriverId == activity.DriverId && activity.DriverId > 0))
                    {
                        errors.Add("Driver is already scheduled during this time");
                    }


                    if (conflicts.Any(c => c.AssignedVehicleId == activity.AssignedVehicleId && activity.AssignedVehicleId > 0))
                    {
                        errors.Add("Vehicle is already scheduled during this time");
                    }

                }
            }

            // Check if driver exists and is active
            if (activity.DriverId > 0)
            {
                var driver = await _context.Drivers.FindAsync(activity.DriverId);
                if (driver == null)
                {
                    errors.Add("Selected driver does not exist");
                }

                else if (driver.Status != "Active")
                {
                    errors.Add("Selected driver is not active");
                }

            }

            // Check if vehicle exists and is active
            if (activity.AssignedVehicleId > 0)
            {
                var bus = await _context.Buses.FindAsync(activity.AssignedVehicleId);
                if (bus == null)
                {
                    errors.Add("Selected bus does not exist");
                }
                else if (bus.Status != "Active")
                {
                    errors.Add("Selected bus is not active");
                }

            }

            Logger.Information("Activity validation complete with {Count} errors", errors.Count);
            return errors;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error validating activity");
            errors.Add($"Validation error: {ex.Message}");
            return errors;
        }
    }

    public async Task<Dictionary<string, int>> GetActivityStatisticsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            Logger.Information("Generating activity statistics from {StartDate} to {EndDate}", startDate, endDate);

            var activities = await _context.Activities
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .ToListAsync();

            var stats = new Dictionary<string, int>();

            // Count by activity type
            var typeStats = activities
                .GroupBy(a => a.ActivityType)
                .ToDictionary(g => $"Type_{g.Key}", g => g.Count());

            // Count by status
            var statusStats = activities
                .GroupBy(a => a.Status)
                .ToDictionary(g => $"Status_{g.Key}", g => g.Count());

            // Count by day of week
            var dowStats = activities
                .GroupBy(a => a.Date.DayOfWeek)
                .ToDictionary(g => $"DayOfWeek_{g.Key}", g => g.Count());

            // Count by month
            var monthStats = activities
                .GroupBy(a => a.Date.Month)
                .ToDictionary(g => $"Month_{g.Key.ToString()}", g => g.Count());

            // Merge all stats
            foreach (var item in typeStats.Concat(statusStats).Concat(dowStats).Concat(monthStats))
            {
                stats[item.Key] = item.Value;
            }

            // Add totals
            stats["Total_Activities"] = activities.Count;
            stats["Total_Drivers"] = activities.Select(a => a.DriverId).Distinct().Count();
            stats["Total_Vehicles"] = activities.Select(a => a.AssignedVehicleId).Distinct().Count();

            Logger.Information("Successfully generated activity statistics with {Count} metrics", stats.Count);
            return stats;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error generating activity statistics");
            throw;
        }
    }

    public async Task<Dictionary<string, double>> GetActivityMetricsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            Logger.Information("Generating activity metrics from {StartDate} to {EndDate}", startDate, endDate);

            var activities = await _context.Activities
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .ToListAsync();

            var metrics = new Dictionary<string, double>();

            // Average duration in hours
            metrics["Avg_Duration"] = activities.Count > 0
                ? activities.Average(a => (a.ReturnTime - a.LeaveTime).TotalHours)
                : 0;

            // Average passengers
            metrics["Avg_Passengers"] = activities.Count > 0 && activities.Any(a => a.ExpectedPassengers.HasValue && a.ExpectedPassengers > 0)
                ? activities.Where(a => a.ExpectedPassengers.HasValue && a.ExpectedPassengers.Value > 0).Average(a => a.ExpectedPassengers!.Value)
                : 0;

            // Activities per day
            var daysInRange = (endDate - startDate).Days + 1;
            metrics["Activities_Per_Day"] = daysInRange > 0
                ? (double)activities.Count / daysInRange
                : 0;

            // Utilization percentage (days with at least one activity / total days)
            var daysWithActivities = activities.Select(a => a.Date.Date).Distinct().Count();
            metrics["Day_Utilization_Pct"] = daysInRange > 0
                ? (double)daysWithActivities / daysInRange * 100
                : 0;

            // Percentage by status
            foreach (var status in activities.Select(a => a.Status).Distinct())
            {
                var statusCount = activities.Count(a => a.Status == status);
                metrics[$"Pct_Status_{status}"] = activities.Count > 0
                    ? (double)statusCount / activities.Count * 100
                    : 0;
            }

            Logger.Information("Successfully generated activity metrics with {Count} values", metrics.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error generating activity metrics");
            throw;
        }
    }

    public async Task<string> ExportActivitiesToCsvAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            startDate ??= DateTime.Today.AddMonths(-1);
            endDate ??= DateTime.Today.AddMonths(1);

            Logger.Information("Exporting activities to CSV from {StartDate} to {EndDate}", startDate, endDate);

            var activities = await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Driver)
                .Include(a => a.Route)
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.LeaveTime)
                .ToListAsync();

            var sb = new StringBuilder();

            // Header row
            sb.AppendLine("ActivityId,Date,Type,Description,Destination,LeaveTime,ReturnTime,Driver,Vehicle,Route,RequestedBy,Status,Passengers,Notes");

            // Data rows
            foreach (var activity in activities)
            {
                sb.AppendLine(string.Join(",",
                    activity.ActivityId,
                    activity.Date.ToString("yyyy-MM-dd"),
                    EscapeCsvField(activity.ActivityType),
                    EscapeCsvField(activity.Description),
                    EscapeCsvField(activity.Destination),
                    activity.LeaveTime.ToString(@"hh\:mm"),
                    activity.ReturnTime.ToString(@"hh\:mm"),
                    EscapeCsvField(activity.Driver?.FullName ?? ""),
                    EscapeCsvField(activity.AssignedVehicle?.BusNumber ?? ""),
                    EscapeCsvField(activity.Route?.RouteName ?? ""),
                    EscapeCsvField(activity.RequestedBy),
                    EscapeCsvField(activity.Status),
                    activity.ExpectedPassengers,
                    EscapeCsvField(activity.Notes)
                ));
            }

            Logger.Information("Successfully exported {Count} activities to CSV", activities.Count);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting activities to CSV");
            throw;
        }
    }

    private string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {

            return string.Empty;
        }


        bool needsQuotes = field.Contains(',') || field.Contains('"') || field.Contains('\n');
        if (needsQuotes)
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    public async Task<byte[]> GenerateActivityReportAsync(int activityId)
    {
        try
        {
            Logger.Information("Generating PDF report for activity {ActivityId}", activityId);

            var activity = await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .FirstOrDefaultAsync(a => a.ActivityId == activityId);

            if (activity == null)
            {
                throw new ArgumentException($"Activity with ID {activityId} not found");
            }

            // Generate professional PDF activity report using the dedicated service
            return _pdfReportService.GenerateActivityReport(activity);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error generating activity report");
            throw;
        }
    }

    public async Task<byte[]> GenerateActivityCalendarReportAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            Logger.Information("Generating PDF calendar report from {StartDate} to {EndDate}", startDate, endDate);

            // Get activities for the specified date range
            var activities = await _context.Activities
                .Include(a => a.AssignedVehicle)
                .Include(a => a.Route)
                .Include(a => a.Driver)
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ToListAsync();

            // Generate professional PDF calendar report using the dedicated service
            return _pdfReportService.GenerateActivityCalendarReport(activities, startDate, endDate);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error generating activity calendar report");
            throw;
        }
    }

#if DEBUG
    public async Task<Dictionary<string, object>> GetActivityDiagnosticsAsync(int activityId)
    {
        try
        {
            Logger.Information("Retrieving diagnostics for activity {ActivityId}", activityId);

            var activity = await _context.Activities
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

            // Calculate additional metrics
            var duration = (activity.ReturnTime - activity.LeaveTime).TotalHours;
            var daysUntilActivity = (activity.Date.Date - DateTime.Today).TotalDays;
            var conflictCount = await _context.Activities
                .CountAsync(a =>
                    a.ActivityId != activityId &&
                    a.Date.Date == activity.Date.Date &&
                    ((a.LeaveTime <= activity.LeaveTime && a.ReturnTime > activity.LeaveTime) ||
                     (a.LeaveTime < activity.ReturnTime && a.ReturnTime >= activity.ReturnTime) ||
                     (a.LeaveTime >= activity.LeaveTime && a.ReturnTime <= activity.ReturnTime)) &&
                    (a.DriverId == activity.DriverId || a.AssignedVehicleId == activity.AssignedVehicleId));

            var driverActivityCount = await _context.Activities
                .CountAsync(a => a.DriverId == activity.DriverId);

            var vehicleActivityCount = await _context.Activities
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

    public async Task<Dictionary<string, object>> GetScheduleOperationMetricsAsync()
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

            var totalCount = await _context.Activities.CountAsync();
            var todayCount = await _context.Activities.CountAsync(a => a.Date.Date == today);
            var tomorrowCount = await _context.Activities.CountAsync(a => a.Date.Date == tomorrow);
            var nextWeekCount = await _context.Activities.CountAsync(a => a.Date >= today && a.Date < nextWeek);
            var thisMonthCount = await _context.Activities.CountAsync(a => a.Date >= thisMonth && a.Date < nextMonth);

            var pendingApprovalCount = await _context.Activities.CountAsync(a => a.Status == "PendingApproval");
            var approvedCount = await _context.Activities.CountAsync(a => a.Status == "Approved");
            var completedCount = await _context.Activities.CountAsync(a => a.Status == "Completed");
            var cancelledCount = await _context.Activities.CountAsync(a => a.Status == "Cancelled");

            var typeDistribution = await _context.Activities
                .GroupBy(a => a.ActivityType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type ?? "Unknown", x => x.Count);

            var mostActiveDrivers = await _context.Activities
                .Where(a => a.Date >= thisMonth && a.Date < nextMonth && a.DriverId > 0)
                .GroupBy(a => a.DriverId)
                .Select(g => new { DriverId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            var driverNames = await _context.Drivers
                .Where(d => mostActiveDrivers.Select(m => m.DriverId).Contains(d.DriverId))
                .ToDictionaryAsync(d => d.DriverId, d => d.FullName);

            var topDrivers = mostActiveDrivers
                .Select(d => new
                {
                    DriverId = d.DriverId,
                    Name = d.DriverId.HasValue && driverNames.TryGetValue(d.DriverId.Value, out var driverName) ? driverName : $"Driver {d.DriverId}",
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
#endif
}
