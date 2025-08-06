using BusBuddy.Core.Data;
using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Data.UnitOfWork;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Linq.Expressions;
using System.Text;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Implementation of the IActivityScheduleService interface
    /// Provides operations for managing activity schedules
    /// </summary>
    public class ActivityScheduleService : IActivityScheduleService
    {
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private static readonly ILogger Logger = Log.ForContext<ActivityScheduleService>();
        private readonly IUnitOfWork _unitOfWork;

        public ActivityScheduleService(
            IBusBuddyDbContextFactory contextFactory,
            IUnitOfWork unitOfWork)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        #region CRUD Operations

        public async Task<IEnumerable<ActivitySchedule>> GetAllActivitySchedulesAsync()
        {
            try
            {
                Logger.Information("Retrieving all activity schedules");

                // Use QueryNoTracking to get a queryable and then apply includes and ordering
                var query = _unitOfWork.ActivitySchedules.QueryNoTracking()
                    .Include(a => a.ScheduledVehicle)
                    .Include(a => a.ScheduledDriver)
                    .OrderByDescending(a => a.ScheduledDate);

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving all activity schedules");
                throw;
            }
        }

        public async Task<ActivitySchedule?> GetActivityScheduleByIdAsync(int id)
        {
            try
            {
                Logger.Information("Retrieving activity schedule with ID: {ActivityScheduleId}", id);

                // Use QueryNoTracking to get a queryable and then apply includes
                return await _unitOfWork.ActivitySchedules.QueryNoTracking()
                    .Include(a => a.ScheduledVehicle)
                    .Include(a => a.ScheduledDriver)
                    .FirstOrDefaultAsync(a => a.ActivityScheduleId == id);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity schedule with ID: {ActivityScheduleId}", id);
                throw;
            }
        }

        public async Task<ActivitySchedule> CreateActivityScheduleAsync(ActivitySchedule activitySchedule)
        {
            ArgumentNullException.ThrowIfNull(activitySchedule);

            try
            {
                Logger.Information("Creating new activity schedule for date: {ScheduledDate}", activitySchedule.ScheduledDate);

                // Validate that the schedule doesn't have conflicts
                if (await HasConflictsAsync(activitySchedule))
                {
                    throw new InvalidOperationException("The scheduled activity conflicts with existing schedules");
                }

                // Validate driver and vehicle availability
                if (!await IsDriverAvailableAsync(activitySchedule.ScheduledDriverId, activitySchedule.ScheduledDate,
                    activitySchedule.ScheduledLeaveTime, activitySchedule.ScheduledEventTime))
                {
                    throw new InvalidOperationException("The selected driver is not available during the scheduled time");
                }

                if (!await IsVehicleAvailableAsync(activitySchedule.ScheduledVehicleId, activitySchedule.ScheduledDate,
                    activitySchedule.ScheduledLeaveTime, activitySchedule.ScheduledEventTime))
                {
                    throw new InvalidOperationException("The selected vehicle is not available during the scheduled time");
                }

                // Set created date and time
                activitySchedule.CreatedDate = DateTime.UtcNow;

                // Add to repository
                await _unitOfWork.ActivitySchedules.AddAsync(activitySchedule);
                await _unitOfWork.SaveChangesAsync();

                Logger.Information("Successfully created activity schedule with ID: {ActivityScheduleId}", activitySchedule.ActivityScheduleId);
                return activitySchedule;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error creating activity schedule");
                throw;
            }
        }

        public async Task<ActivitySchedule> UpdateActivityScheduleAsync(ActivitySchedule activitySchedule)
        {
            if (activitySchedule == null)
            {

                throw new ArgumentNullException(nameof(activitySchedule));
            }


            try
            {
                Logger.Information("Updating activity schedule with ID: {ActivityScheduleId}", activitySchedule.ActivityScheduleId);

                // Get the existing record
                var existingSchedule = await _unitOfWork.ActivitySchedules.GetByIdAsync(activitySchedule.ActivityScheduleId);
                if (existingSchedule == null)
                {
                    throw new InvalidOperationException($"Activity schedule with ID {activitySchedule.ActivityScheduleId} not found");
                }

                // Validate that the update doesn't create conflicts
                if (await HasConflictsAsync(activitySchedule))
                {
                    throw new InvalidOperationException("The updated schedule conflicts with existing schedules");
                }

                // Update properties
                existingSchedule.ScheduledDate = activitySchedule.ScheduledDate;
                existingSchedule.TripType = activitySchedule.TripType;
                existingSchedule.ScheduledVehicleId = activitySchedule.ScheduledVehicleId;
                existingSchedule.ScheduledDestination = activitySchedule.ScheduledDestination;
                existingSchedule.ScheduledLeaveTime = activitySchedule.ScheduledLeaveTime;
                existingSchedule.ScheduledEventTime = activitySchedule.ScheduledEventTime;
                existingSchedule.ScheduledRiders = activitySchedule.ScheduledRiders;
                existingSchedule.ScheduledDriverId = activitySchedule.ScheduledDriverId;
                existingSchedule.RequestedBy = activitySchedule.RequestedBy;
                existingSchedule.Status = activitySchedule.Status;
                existingSchedule.Notes = activitySchedule.Notes;
                existingSchedule.UpdatedDate = DateTime.UtcNow;
                existingSchedule.UpdatedBy = activitySchedule.UpdatedBy;

                // Update in repository
                _unitOfWork.ActivitySchedules.Update(existingSchedule);
                await _unitOfWork.SaveChangesAsync();

                Logger.Information("Successfully updated activity schedule with ID: {ActivityScheduleId}", activitySchedule.ActivityScheduleId);
                return existingSchedule;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating activity schedule with ID: {ActivityScheduleId}", activitySchedule.ActivityScheduleId);
                throw;
            }
        }

        public async Task<bool> DeleteActivityScheduleAsync(int id)
        {
            try
            {
                Logger.Information("Deleting activity schedule with ID: {ActivityScheduleId}", id);

                var activitySchedule = await _unitOfWork.ActivitySchedules.GetByIdAsync(id);
                if (activitySchedule == null)
                {
                    Logger.Warning("Activity schedule with ID: {ActivityScheduleId} not found for deletion", id);
                    return false;
                }

                _unitOfWork.ActivitySchedules.Remove(activitySchedule);
                await _unitOfWork.SaveChangesAsync();

                Logger.Information("Successfully deleted activity schedule with ID: {ActivityScheduleId}", id);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting activity schedule with ID: {ActivityScheduleId}", id);
                throw;
            }
        }

        #endregion

        #region Filtering Operations

        public async Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                Logger.Information("Retrieving activity schedules between {StartDate} and {EndDate}", startDate, endDate);

                return await _unitOfWork.ActivitySchedules.FindAsync(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity schedules for date range");
                throw;
            }
        }

        public async Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByDriverAsync(int driverId)
        {
            try
            {
                Logger.Information("Retrieving activity schedules for driver ID: {DriverId}", driverId);

                return await _unitOfWork.ActivitySchedules.FindAsync(a => a.ScheduledDriverId == driverId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity schedules for driver ID: {DriverId}", driverId);
                throw;
            }
        }

        public async Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByVehicleAsync(int vehicleId)
        {
            try
            {
                Logger.Information("Retrieving activity schedules for vehicle ID: {VehicleId}", vehicleId);

                return await _unitOfWork.ActivitySchedules.FindAsync(a => a.ScheduledVehicleId == vehicleId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity schedules for vehicle ID: {VehicleId}", vehicleId);
                throw;
            }
        }

        public async Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByStatusAsync(string status)
        {
            try
            {
                Logger.Information("Retrieving activity schedules with status: {Status}", status);

                return await _unitOfWork.ActivitySchedules.FindAsync(a => a.Status == status);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity schedules with status: {Status}", status);
                throw;
            }
        }

        public async Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByTripTypeAsync(string tripType)
        {
            try
            {
                Logger.Information("Retrieving activity schedules with trip type: {TripType}", tripType);

                return await _unitOfWork.ActivitySchedules.FindAsync(a => a.TripType == tripType);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity schedules with trip type: {TripType}", tripType);
                throw;
            }
        }

        public async Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByDestinationAsync(string destination)
        {
            try
            {
                Logger.Information("Retrieving activity schedules with destination: {Destination}", destination);

                return await _unitOfWork.ActivitySchedules.FindAsync(a => a.ScheduledDestination.Contains(destination));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving activity schedules with destination: {Destination}", destination);
                throw;
            }
        }

        #endregion

        #region Scheduling Operations

        public async Task<bool> IsVehicleAvailableAsync(int vehicleId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime)
        {
            try
            {
                // Check if there are any conflicting schedules for this vehicle
                var conflicts = await _unitOfWork.ActivitySchedules.FindAsync(a =>
                    a.ScheduledVehicleId == vehicleId &&
                    a.ScheduledDate.Date == scheduleDate.Date &&
                    a.Status != "Cancelled" &&
                    ((a.ScheduledLeaveTime <= startTime && a.ScheduledEventTime > startTime) || // Overlaps start time
                     (a.ScheduledLeaveTime < endTime && a.ScheduledEventTime >= endTime) || // Overlaps end time
                     (a.ScheduledLeaveTime >= startTime && a.ScheduledEventTime <= endTime))); // Within time range

                return !conflicts.Any();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking vehicle availability for vehicle ID: {VehicleId}", vehicleId);
                throw;
            }
        }

        public async Task<bool> IsDriverAvailableAsync(int driverId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime)
        {
            try
            {
                // Check if there are any conflicting schedules for this driver
                var conflicts = await _unitOfWork.ActivitySchedules.FindAsync(a =>
                    a.ScheduledDriverId == driverId &&
                    a.ScheduledDate.Date == scheduleDate.Date &&
                    a.Status != "Cancelled" &&
                    ((a.ScheduledLeaveTime <= startTime && a.ScheduledEventTime > startTime) || // Overlaps start time
                     (a.ScheduledLeaveTime < endTime && a.ScheduledEventTime >= endTime) || // Overlaps end time
                     (a.ScheduledLeaveTime >= startTime && a.ScheduledEventTime <= endTime))); // Within time range

                return !conflicts.Any();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking driver availability for driver ID: {DriverId}", driverId);
                throw;
            }
        }

        public async Task<IEnumerable<Driver>> GetAvailableDriversAsync(DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Get all active drivers
                var allDrivers = await context.Drivers
                    .Where(d => d.Status == "Active")
                    .ToListAsync();

                // Get all drivers with conflicting schedules for the given time period
                var busyDriverIds = await context.ActivitySchedule
                    .Where(a =>
                        a.ScheduledDate.Date == scheduleDate.Date &&
                        a.Status != "Cancelled" &&
                        ((a.ScheduledLeaveTime <= startTime && a.ScheduledEventTime > startTime) || // Overlaps start time
                         (a.ScheduledLeaveTime < endTime && a.ScheduledEventTime >= endTime) || // Overlaps end time
                         (a.ScheduledLeaveTime >= startTime && a.ScheduledEventTime <= endTime))) // Within time range
                    .Select(a => a.ScheduledDriverId)
                    .Distinct()
                    .ToListAsync();

                // Filter out busy drivers
                return allDrivers.Where(d => !busyDriverIds.Contains(d.DriverId)).ToList();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting available drivers for date: {Date}", scheduleDate);
                throw;
            }
        }

        public async Task<IEnumerable<Bus>> GetAvailableVehiclesAsync(DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Get all active buses
                var allBuses = await context.Vehicles
                    .Where(v => v.Status == "Active")
                    .ToListAsync();

                // Get all buses with conflicting schedules for the given time period
                var busyBusIds = await context.ActivitySchedule
                    .Where(a =>
                        a.ScheduledDate.Date == scheduleDate.Date &&
                        a.Status != "Cancelled" &&
                        ((a.ScheduledLeaveTime <= startTime && a.ScheduledEventTime > startTime) || // Overlaps start time
                         (a.ScheduledLeaveTime < endTime && a.ScheduledEventTime >= endTime) || // Overlaps end time
                         (a.ScheduledLeaveTime >= startTime && a.ScheduledEventTime <= endTime))) // Within time range
                    .Select(a => a.ScheduledVehicleId)
                    .Distinct()
                    .ToListAsync();

                // Filter out busy buses
                return allBuses.Where(b => !busyBusIds.Contains(b.VehicleId)).ToList();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting available vehicles for date: {Date}", scheduleDate);
                throw;
            }
        }

        public async Task<bool> ConfirmActivityScheduleAsync(int activityScheduleId)
        {
            try
            {
                var activitySchedule = await _unitOfWork.ActivitySchedules.GetByIdAsync(activityScheduleId);
                if (activitySchedule == null)
                {
                    Logger.Warning("Activity schedule with ID: {ActivityScheduleId} not found for confirmation", activityScheduleId);
                    return false;
                }

                activitySchedule.Status = "Confirmed";
                activitySchedule.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.ActivitySchedules.Update(activitySchedule);
                await _unitOfWork.SaveChangesAsync();

                Logger.Information("Successfully confirmed activity schedule with ID: {ActivityScheduleId}", activityScheduleId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error confirming activity schedule with ID: {ActivityScheduleId}", activityScheduleId);
                throw;
            }
        }

        public async Task<bool> CancelActivityScheduleAsync(int activityScheduleId, string? reason = null)
        {
            try
            {
                var activitySchedule = await _unitOfWork.ActivitySchedules.GetByIdAsync(activityScheduleId);
                if (activitySchedule == null)
                {
                    Logger.Warning("Activity schedule with ID: {ActivityScheduleId} not found for cancellation", activityScheduleId);
                    return false;
                }

                activitySchedule.Status = "Cancelled";
                activitySchedule.UpdatedDate = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(reason))
                {
                    activitySchedule.Notes = string.IsNullOrEmpty(activitySchedule.Notes)
                        ? $"Cancellation reason: {reason}"
                        : $"{activitySchedule.Notes}\nCancellation reason: {reason}";
                }

                _unitOfWork.ActivitySchedules.Update(activitySchedule);
                await _unitOfWork.SaveChangesAsync();

                Logger.Information("Successfully cancelled activity schedule with ID: {ActivityScheduleId}", activityScheduleId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error cancelling activity schedule with ID: {ActivityScheduleId}", activityScheduleId);
                throw;
            }
        }

        public async Task<bool> CompleteActivityScheduleAsync(int activityScheduleId)
        {
            try
            {
                var activitySchedule = await _unitOfWork.ActivitySchedules.GetByIdAsync(activityScheduleId);
                if (activitySchedule == null)
                {
                    Logger.Warning("Activity schedule with ID: {ActivityScheduleId} not found for completion", activityScheduleId);
                    return false;
                }

                activitySchedule.Status = "Completed";
                activitySchedule.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.ActivitySchedules.Update(activitySchedule);
                await _unitOfWork.SaveChangesAsync();

                Logger.Information("Successfully completed activity schedule with ID: {ActivityScheduleId}", activityScheduleId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error completing activity schedule with ID: {ActivityScheduleId}", activityScheduleId);
                throw;
            }
        }

        #endregion

        #region Conflict Detection

        public async Task<IEnumerable<ActivitySchedule>> FindScheduleConflictsAsync(DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime, int? excludeActivityScheduleId = null)
        {
            try
            {
                var baseQuery = _unitOfWork.ActivitySchedules.Query()
                    .Where(a =>
                        a.ScheduledDate.Date == scheduleDate.Date &&
                        a.Status != "Cancelled" &&
                        ((a.ScheduledLeaveTime <= startTime && a.ScheduledEventTime > startTime) || // Overlaps start time
                         (a.ScheduledLeaveTime < endTime && a.ScheduledEventTime >= endTime) || // Overlaps end time
                         (a.ScheduledLeaveTime >= startTime && a.ScheduledEventTime <= endTime))); // Within time range

                if (excludeActivityScheduleId.HasValue)
                {
                    baseQuery = baseQuery.Where(a => a.ActivityScheduleId != excludeActivityScheduleId.Value);
                }

                return await baseQuery
                    .Include(a => a.ScheduledVehicle)
                    .Include(a => a.ScheduledDriver)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error finding schedule conflicts for date: {Date}", scheduleDate);
                throw;
            }
        }

        public async Task<bool> HasConflictsAsync(ActivitySchedule activitySchedule)
        {
            if (activitySchedule == null)
            {

                throw new ArgumentNullException(nameof(activitySchedule));
            }


            try
            {
                // Find conflicts with other schedules for the same date and time range, excluding this schedule
                var conflicts = await FindScheduleConflictsAsync(
                    activitySchedule.ScheduledDate,
                    activitySchedule.ScheduledLeaveTime,
                    activitySchedule.ScheduledEventTime,
                    activitySchedule.ActivityScheduleId);

                // Filter conflicts for the same driver or vehicle
                return conflicts.Any(c =>
                    c.ScheduledDriverId == activitySchedule.ScheduledDriverId ||
                    c.ScheduledVehicleId == activitySchedule.ScheduledVehicleId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking for scheduling conflicts");
                throw;
            }
        }

        #endregion

        #region Analytics and Reporting

        public async Task<Dictionary<string, int>> GetActivityScheduleStatisticsByTripTypeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var schedules = await _unitOfWork.ActivitySchedules.FindAsync(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate);

                return schedules
                    .GroupBy(a => a.TripType)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting activity schedule statistics by trip type");
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetActivityScheduleStatisticsByDriverAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var driverStats = await context.ActivitySchedule
                    .Where(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate)
                    .GroupBy(a => a.ScheduledDriverId)
                    .Select(g => new { DriverId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var driverNames = await context.Drivers
                    .Where(d => driverStats.Select(s => s.DriverId).Contains(d.DriverId))
                    .ToDictionaryAsync(d => d.DriverId, d => $"{d.FirstName} {d.LastName}");

                return driverStats.ToDictionary(
                    s => driverNames.TryGetValue(s.DriverId, out var name) ? name : $"Driver {s.DriverId}",
                    s => s.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting activity schedule statistics by driver");
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetActivityScheduleStatisticsByVehicleAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var vehicleStats = await context.ActivitySchedule
                    .Where(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate)
                    .GroupBy(a => a.ScheduledVehicleId)
                    .Select(g => new { VehicleId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var vehicleNames = await context.Vehicles
                    .Where(v => vehicleStats.Select(s => s.VehicleId).Contains(v.VehicleId))
                    .ToDictionaryAsync(v => v.VehicleId, v => $"{v.Make} {v.Model} ({v.BusNumber})");

                return vehicleStats.ToDictionary(
                    s => vehicleNames.TryGetValue(s.VehicleId, out var name) ? name : $"Vehicle {s.VehicleId}",
                    s => s.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting activity schedule statistics by vehicle");
                throw;
            }
        }

        public async Task<Dictionary<DateTime, int>> GetActivityScheduleStatisticsByDateAsync(DateTime startDate, DateTime endDate, string status)
        {
            try
            {
                var schedules = await _unitOfWork.ActivitySchedules.FindAsync(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate && a.Status == status);

                return schedules
                    .GroupBy(a => a.ScheduledDate.Date)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting activity schedule statistics by date");
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetActivityScheduleStatisticsByStatusAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var schedules = await _unitOfWork.ActivitySchedules.FindAsync(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate);

                return schedules
                    .GroupBy(a => a.Status)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting activity schedule statistics by status");
                throw;
            }
        }

        public async Task<string> ExportActivitySchedulesToCsvAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var schedules = await _unitOfWork.ActivitySchedules.FindAsync(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate);

                var sb = new StringBuilder();

                // Header
                sb.AppendLine("ActivityScheduleId,Date,Trip Type,Destination,Leave Time,Event Time,Driver,Vehicle,Requested By,Status,Riders,Notes");

                // Data rows
                foreach (var schedule in schedules)
                {
                    sb.AppendLine(string.Join(",",
                        schedule.ActivityScheduleId,
                        schedule.ScheduledDate.ToString("yyyy-MM-dd"),
                        EscapeCsvField(schedule.TripType),
                        EscapeCsvField(schedule.ScheduledDestination),
                        schedule.ScheduledLeaveTime.ToString(),
                        schedule.ScheduledEventTime.ToString(),
                        EscapeCsvField(schedule.ScheduledDriver != null ? $"{schedule.ScheduledDriver.FirstName} {schedule.ScheduledDriver.LastName}" : ""),
                        EscapeCsvField(schedule.ScheduledVehicle != null ? $"{schedule.ScheduledVehicle.Make} {schedule.ScheduledVehicle.Model} ({schedule.ScheduledVehicle.LicenseNumber})" : ""),
                        EscapeCsvField(schedule.RequestedBy),
                        EscapeCsvField(schedule.Status),
                        schedule.ScheduledRiders,
                        EscapeCsvField(schedule.Notes)
                    ));
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error exporting activity schedules to CSV");
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

        #endregion

        #region DEBUG Instrumentation

#if DEBUG
        public async Task<Dictionary<string, object>> GetActivityScheduleDiagnosticsAsync(int activityScheduleId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var activitySchedule = await context.ActivitySchedule
                    .Include(a => a.ScheduledDriver)
                    .Include(a => a.ScheduledVehicle)
                    .FirstOrDefaultAsync(a => a.ActivityScheduleId == activityScheduleId);

                if (activitySchedule == null)
                {
                    return new Dictionary<string, object>
                    {
                        ["Error"] = $"Activity schedule with ID {activityScheduleId} not found"
                    };
                }

                // Calculate some metrics
                var scheduleDate = activitySchedule.ScheduledDate;
                var conflictCount = await context.ActivitySchedule
                    .CountAsync(a =>
                        a.ActivityScheduleId != activityScheduleId &&
                        a.ScheduledDate.Date == scheduleDate.Date &&
                        a.Status != "Cancelled" &&
                        ((a.ScheduledLeaveTime <= activitySchedule.ScheduledLeaveTime && a.ScheduledEventTime > activitySchedule.ScheduledLeaveTime) ||
                         (a.ScheduledLeaveTime < activitySchedule.ScheduledEventTime && a.ScheduledEventTime >= activitySchedule.ScheduledEventTime) ||
                         (a.ScheduledLeaveTime >= activitySchedule.ScheduledLeaveTime && a.ScheduledEventTime <= activitySchedule.ScheduledEventTime)));

                var driverScheduleCount = await context.ActivitySchedule
                    .CountAsync(a => a.ScheduledDriverId == activitySchedule.ScheduledDriverId);

                var vehicleScheduleCount = await context.ActivitySchedule
                    .CountAsync(a => a.ScheduledVehicleId == activitySchedule.ScheduledVehicleId);

                return new Dictionary<string, object>
                {
                    ["ActivityScheduleId"] = activitySchedule.ActivityScheduleId,
                    ["TripType"] = activitySchedule.TripType,
                    ["ScheduledDate"] = activitySchedule.ScheduledDate,
                    ["ScheduledTimeRange"] = $"{activitySchedule.ScheduledLeaveTime} - {activitySchedule.ScheduledEventTime}",
                    ["Duration"] = (activitySchedule.ScheduledEventTime - activitySchedule.ScheduledLeaveTime).ToString(),
                    ["Status"] = activitySchedule.Status,
                    ["Destination"] = activitySchedule.ScheduledDestination,
                    ["Driver"] = activitySchedule.ScheduledDriver != null ? $"{activitySchedule.ScheduledDriver.FirstName} {activitySchedule.ScheduledDriver.LastName}" : "Unknown",
                    ["Vehicle"] = activitySchedule.ScheduledVehicle != null ? $"{activitySchedule.ScheduledVehicle.Make} {activitySchedule.ScheduledVehicle.Model}" : "Unknown",
                    ["RequestedBy"] = activitySchedule.RequestedBy,
                    ["CreatedDate"] = activitySchedule.CreatedDate,
                    ["UpdatedDate"] = activitySchedule.UpdatedDate ?? DateTime.MinValue,
                    ["PotentialConflicts"] = conflictCount,
                    ["DriverTotalSchedules"] = driverScheduleCount,
                    ["VehicleTotalSchedules"] = vehicleScheduleCount,
                    ["HasValidTimeRange"] = activitySchedule.ScheduledLeaveTime < activitySchedule.ScheduledEventTime,
                    ["Notes"] = activitySchedule.Notes ?? "No notes",
                    ["ContextType"] = context.GetType().FullName ?? "Unknown",
                    ["DatabaseProvider"] = context.Database.ProviderName ?? "Unknown"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting activity schedule diagnostics for ID: {ActivityScheduleId}", activityScheduleId);
                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["StackTrace"] = ex.StackTrace ?? "No stack trace"
                };
            }
        }

        public async Task<Dictionary<string, object>> GetActivityScheduleOperationMetricsAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var now = DateTime.UtcNow;
                var startOfDay = DateTime.UtcNow.Date;
                var oneWeekAgo = startOfDay.AddDays(-7);
                var oneMonthAgo = startOfDay.AddMonths(-1);

                var totalCount = await context.ActivitySchedule.CountAsync();
                var todayCount = await context.ActivitySchedule.CountAsync(a => a.ScheduledDate.Date == startOfDay);
                var weekCount = await context.ActivitySchedule.CountAsync(a => a.ScheduledDate >= oneWeekAgo);
                var monthCount = await context.ActivitySchedule.CountAsync(a => a.ScheduledDate >= oneMonthAgo);

                var statusCounts = await context.ActivitySchedule
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count);

                var tripTypeCounts = await context.ActivitySchedule
                    .GroupBy(a => a.TripType)
                    .Select(g => new { TripType = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.TripType, x => x.Count);

                var busUseFrequency = await context.ActivitySchedule
                    .GroupBy(a => a.ScheduledVehicleId)
                    .Select(g => new { VehicleId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                var busDetails = await context.Vehicles
                    .Where(v => busUseFrequency.Select(b => b.VehicleId).Contains(v.VehicleId))
                    .ToDictionaryAsync(v => v.VehicleId, v => $"{v.Make} {v.Model} ({v.BusNumber})");

                var topBuses = busUseFrequency
                    .Select(b => new
                    {
                        VehicleId = b.VehicleId,
                        Name = busDetails.ContainsKey(b.VehicleId) ? busDetails[b.VehicleId] : $"Bus {b.VehicleId}",
                        Count = b.Count
                    })
                    .ToDictionary(b => b.Name, b => b.Count);

                return new Dictionary<string, object>
                {
                    ["TotalScheduleCount"] = totalCount,
                    ["TodayScheduleCount"] = todayCount,
                    ["LastWeekScheduleCount"] = weekCount,
                    ["LastMonthScheduleCount"] = monthCount,
                    ["SchedulesByStatus"] = statusCounts,
                    ["SchedulesByTripType"] = tripTypeCounts,
                    ["TopUsedVehicles"] = topBuses,
                    ["DatabaseProvider"] = context.Database.ProviderName ?? "Unknown",
                    ["ServerVersion"] = context.Database.ProviderName != null && context.Database.ProviderName.Contains("SqlServer") ?
                        "SQL Server" : "Unknown",
                    ["GeneratedAt"] = now
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting activity schedule operation metrics");
                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["StackTrace"] = ex.StackTrace ?? "No stack trace"
                };
            }
        }
#endif

        #endregion
    }
}
