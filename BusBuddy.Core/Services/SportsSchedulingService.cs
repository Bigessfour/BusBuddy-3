using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Data;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Implementation of sports event scheduling service
    /// Enhanced for Phase 2 sports scheduling with safety integration
    /// Follows NHTSA safety guidelines and transportation best practices
    /// Phase 1 implementation with simplified patterns and basic error handling
    /// </summary>
    public class SportsSchedulingService : ISportsSchedulingService
    {
        private readonly BusBuddy.Core.Data.BusBuddyDbContext _context;
        private static readonly ILogger Logger = Log.ForContext<SportsSchedulingService>();

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        /// <param name="context">Entity Framework DbContext</param>
        public SportsSchedulingService(BusBuddyDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Creates a new sports event asynchronously
        /// </summary>
        /// <param name="sportsEvent">The sports event details to create</param>
        /// <returns>The created SportsEvent object</returns>
        public async Task<SportsEvent> CreateSportsEventAsync(SportsEvent sportsEvent)
        {
            if (sportsEvent == null)
            {
                Logger.Warning("Attempted to create a null SportsEvent");
                throw new ArgumentNullException(nameof(sportsEvent));
            }

            string pipelineStep = "Validation";
            using (LogContext.PushProperty("Operation", "CreateSportsEvent"))
            using (LogContext.PushProperty("EventName", sportsEvent.EventName))
            using (LogContext.PushProperty("PipelineStep", pipelineStep))
            {
                try
                {
                    Logger.Information("Starting sports event creation pipeline for {EventName}", sportsEvent.EventName);

                    // Validate event times
                    if (sportsEvent.StartTime >= sportsEvent.EndTime)
                    {
                        Logger.Warning("Invalid event times: StartTime {StartTime} must be before EndTime {EndTime}",
                            sportsEvent.StartTime, sportsEvent.EndTime);
                        throw new InvalidOperationException("StartTime must be before EndTime.");
                    }

                    // Set default safety notes if empty
                    if (string.IsNullOrEmpty(sportsEvent.SafetyNotes))
                    {
                        sportsEvent.SafetyNotes = sportsEvent.GetDefaultSafetyNotes();
                        Logger.Information("Applied default safety notes to sports event {EventName}", sportsEvent.EventName);
                    }

                    // Validate safety requirements
                    if (!sportsEvent.IsEventSafe())
                    {
                        Logger.Warning("Sports event {EventName} failed safety validation", sportsEvent.EventName);
                        throw new InvalidOperationException("Sports event does not meet safety requirements.");
                    }

                    LogContext.PushProperty("PipelineStep", "DatabaseSave");
                    Logger.Debug("Pipeline step: Saving to database");

                    await _context.SportsEvents.AddAsync(sportsEvent);
                    await _context.SaveChangesAsync();

                    Logger.Information("SportsEvent {EventName} created with ID: {EventId} - Pipeline completed successfully",
                        sportsEvent.EventName, sportsEvent.Id);

                    return sportsEvent;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to create SportsEvent {EventName} at step {PipelineStep}",
                        sportsEvent?.EventName ?? "Unknown", pipelineStep);
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets sports events within a date range
        /// Gets sports events within a date range
        /// </summary>
        /// <param name="startDate">Start date filter (optional)</param>
        /// <param name="endDate">End date filter (optional)</param>
        /// <returns>List of sports events</returns>
        public async Task<List<SportsEvent>> GetSportsEventsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.SportsEvents
                    .Include(e => e.Vehicle)
                    .Include(e => e.Driver)
                    .AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(e => e.StartTime >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(e => e.EndTime <= endDate.Value);
                }

                var events = await query
                    .OrderBy(e => e.StartTime)
                    .ToListAsync();

                Logger.Information("Retrieved {Count} sports events between {StartDate} and {EndDate}",
                    events.Count, startDate?.ToString("yyyy-MM-dd") ?? "N/A", endDate?.ToString("yyyy-MM-dd") ?? "N/A");

                return events;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving sports events");
                return new List<SportsEvent>();
            }
        }

        /// <summary>
        /// Assigns a bus and driver to a sports event asynchronously
        /// </summary>
        /// <param name="eventId">The ID of the sports event</param>
        /// <param name="busId">The ID of the bus to assign</param>
        /// <param name="driverId">The ID of the driver to assign</param>
        /// <returns>True if assignment successful</returns>
        public async Task<bool> AssignBusAndDriverAsync(int eventId, int? busId, int? driverId)
        {
            string pipelineStep = "Validation";
            using (LogContext.PushProperty("Operation", "AssignBusAndDriver"))
            using (LogContext.PushProperty("EventId", eventId))
            using (LogContext.PushProperty("DriverId", driverId))
            using (LogContext.PushProperty("PipelineStep", pipelineStep))
            {
                if (busId.HasValue)
                {
                    LogContext.PushProperty("BusId", busId.Value);
                }

                try
                {
                    Logger.Information("Starting bus and driver assignment pipeline for Event {EventId}", eventId);

                    var sportsEvent = await _context.SportsEvents.FindAsync(eventId);
                    if (sportsEvent == null)
                    {
                        Logger.Warning("SportsEvent with ID {EventId} not found", eventId);
                        return false;
                    }

                    if (!busId.HasValue)
                    {
                        Logger.Warning("BusId is required for assignment");
                        return false;
                    }

                    if (!driverId.HasValue)
                    {
                        Logger.Warning("DriverId is required for assignment");
                        return false;
                    }

                    pipelineStep = "BusLookup";
                    LogContext.PushProperty("PipelineStep", pipelineStep);
                    var bus = await _context.Buses.FindAsync(busId.Value);
                    if (bus == null)
                    {
                        Logger.Warning("Bus with ID {BusId} not found", busId.Value);
                        return false;
                    }

                    // Validate bus status and maintenance
                    if (bus.Status != "Active")
                    {
                        Logger.Warning("Bus {BusId} ({BusNumber}) is not active (Status: {Status})", busId, bus.BusNumber, bus.Status);
                        return false;
                    }

                    // Check maintenance status
                    if (bus.NextMaintenanceDue.HasValue && bus.NextMaintenanceDue.Value <= sportsEvent.StartTime)
                    {
                        Logger.Warning("Bus {BusId} ({BusNumber}) requires maintenance before {EventDate}",
                            busId, bus.BusNumber, bus.NextMaintenanceDue.Value);
                        return false;
                    }

                    // Check GPS tracking capability if needed
                    if (sportsEvent.RequiresGPSTracking && !bus.GPSTracking)
                    {
                        Logger.Warning("Bus {BusId} ({BusNumber}) does not have GPS tracking but event requires it",
                            busId, bus.BusNumber);
                        return false;
                    }

                    pipelineStep = "DriverLookup";
                    LogContext.PushProperty("PipelineStep", pipelineStep);
                    var driver = await _context.Drivers.FindAsync(driverId.Value);
                    if (driver == null)
                    {
                        Logger.Warning("Driver with ID {DriverId} not found", driverId.Value);
                        return false;
                    }

                    pipelineStep = "ConflictValidation";
                    LogContext.PushProperty("PipelineStep", pipelineStep);
                    // Validate conflicts before assignment
                    var noConflicts = await ValidateSchedulingConflictsAsync(
                        sportsEvent.StartTime, sportsEvent.EndTime, busId.Value, driverId.Value);

                    if (!noConflicts)
                    {
                        Logger.Warning("Scheduling conflict detected for Event {EventId}, Bus {BusId}, Driver {DriverId}",
                            eventId, busId.Value, driverId.Value);
                        return false;
                    }

                    // Check bus capacity using new SeatingCapacity property
                    if (bus.SeatingCapacity < sportsEvent.TeamSize)
                    {
                        Logger.Warning("Bus {BusId} ({BusNumber}) seating capacity {Capacity} insufficient for team size {TeamSize}",
                            busId, bus.BusNumber, bus.SeatingCapacity, sportsEvent.TeamSize);
                        return false;
                    }

                    // Check special equipment requirements
                    if (!string.IsNullOrEmpty(sportsEvent.RequiredEquipment) &&
                        (string.IsNullOrEmpty(bus.SpecialEquipment) ||
                         !bus.SpecialEquipment.Contains(sportsEvent.RequiredEquipment, StringComparison.OrdinalIgnoreCase)))
                    {
                        Logger.Warning("Bus {BusId} ({BusNumber}) missing required equipment: {RequiredEquipment}",
                            busId, bus.BusNumber, sportsEvent.RequiredEquipment);
                        return false;
                    }

                    LogContext.PushProperty("PipelineStep", "Assignment");
                    // Assign bus and driver
                    sportsEvent.BusId = busId.Value;
                    sportsEvent.DriverId = driverId.Value;
                    sportsEvent.Status = "Assigned";

                    await _context.SaveChangesAsync();

                    Logger.Information("Assignment completed for Event {EventId} with Bus {BusId} ({BusNumber}) and Driver {DriverId} - Pipeline completed successfully",
                        eventId, busId.Value, bus.BusNumber, driverId.Value);

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Pipeline failure in AssignBusAndDriver for Event {EventId} at step {PipelineStep}",
                        eventId, pipelineStep);
                    return false;
                }
            }
        }

        /// <summary>
        /// Validates if there are any scheduling conflicts for the given bus or driver during the event time
        /// </summary>
        /// <param name="startTime">Event start time</param>
        /// <param name="endTime">Event end time</param>
        /// <param name="busId">The ID of the bus to check (optional)</param>
        /// <param name="driverId">The ID of the driver to check (optional)</param>
        /// <returns>True if no conflicts, false otherwise</returns>
        public async Task<bool> ValidateSchedulingConflictsAsync(DateTime startTime, DateTime endTime, int? busId = null, int? driverId = null)
        {
            string pipelineStep = "Validation";
            using (LogContext.PushProperty("Operation", "ValidateSchedulingConflicts"))
            using (LogContext.PushProperty("StartTime", startTime))
            using (LogContext.PushProperty("EndTime", endTime))
            using (LogContext.PushProperty("BusId", busId))
            using (LogContext.PushProperty("DriverId", driverId))
            using (LogContext.PushProperty("PipelineStep", pipelineStep))
            {
                try
                {
                    Logger.Information("Starting conflict validation for time slot {StartTime} to {EndTime}", startTime, endTime);

                    int conflicts = 0;

                    pipelineStep = "BusConflictCheck";
                    LogContext.PushProperty("PipelineStep", pipelineStep);
                    // Check for bus conflicts
                    if (busId.HasValue)
                    {
                        Logger.Debug("Checking conflicts for Bus ID {BusId}", busId.Value);
                        var busConflicts = await _context.SportsEvents
                            .Where(e => e.BusId == busId.Value &&
                                       e.StartTime < endTime && e.EndTime > startTime)
                            .CountAsync();
                        conflicts += busConflicts;
                        Logger.Debug("Found {ConflictCount} conflicts for Bus ID {BusId}", busConflicts, busId.Value);
                    }

                    pipelineStep = "DriverConflictCheck";
                    LogContext.PushProperty("PipelineStep", pipelineStep);
                    // Check for driver conflicts
                    if (driverId.HasValue)
                    {
                        Logger.Debug("Checking conflicts for Driver ID {DriverId}", driverId.Value);
                        var driverConflicts = await _context.SportsEvents
                            .Where(e => e.DriverId == driverId.Value &&
                                       e.StartTime < endTime && e.EndTime > startTime)
                            .CountAsync();
                        conflicts += driverConflicts;
                        Logger.Debug("Found {ConflictCount} conflicts for Driver ID {DriverId}", driverConflicts, driverId.Value);
                    }

                    var noConflicts = conflicts == 0;

                    LogContext.PushProperty("PipelineStep", "ResultAnalysis");
                    if (noConflicts)
                    {
                        Logger.Information("✅ Conflict validation PASSED for Bus {BusId}, Driver {DriverId} - No scheduling conflicts found",
                            busId?.ToString(CultureInfo.InvariantCulture) ?? "N/A",
                            driverId?.ToString(CultureInfo.InvariantCulture) ?? "N/A");
                    }
                    else
                    {
                        Logger.Warning("❌ Conflict validation FAILED for Bus {BusId}, Driver {DriverId} - Found {ConflictCount} scheduling conflicts",
                            busId?.ToString(CultureInfo.InvariantCulture) ?? "N/A",
                            driverId?.ToString(CultureInfo.InvariantCulture) ?? "N/A", conflicts);
                    }

                    LogContext.PushProperty("PipelineStep", "Complete");
                    Logger.Debug("Conflict validation pipeline completed");

                    return noConflicts;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Pipeline failure in ValidateSchedulingConflicts at step {PipelineStep}",
                        pipelineStep);
                    return false;
                }
            }
        }

        /// <summary>
        /// Retrieves a list of available buses for a given time slot
        /// </summary>
        /// <param name="startTime">The start time of the time slot</param>
        /// <param name="endTime">The end time of the time slot</param>
        /// <param name="minimumCapacity">Minimum bus capacity required</param>
        /// <returns>A list of available Bus objects</returns>
        public async Task<List<Bus>> GetAvailableBusesAsync(DateTime startTime, DateTime endTime, int minimumCapacity = 1)
        {
            string pipelineStep = "Validation";
            using (LogContext.PushProperty("Operation", "GetAvailableBuses"))
            using (LogContext.PushProperty("StartTime", startTime))
            using (LogContext.PushProperty("EndTime", endTime))
            using (LogContext.PushProperty("MinimumCapacity", minimumCapacity))
            using (LogContext.PushProperty("PipelineStep", pipelineStep))
            {
                try
                {
                    Logger.Information("Starting available buses query pipeline for time slot {StartTime} to {EndTime}",
                        startTime, endTime);

                    if (startTime >= endTime)
                    {
                        Logger.Warning("Invalid time range: StartTime {StartTime} >= EndTime {EndTime}", startTime, endTime);
                        return new List<Bus>();
                    }

                    pipelineStep = "ConflictCheck";
                    LogContext.PushProperty("PipelineStep", pipelineStep);
                    // Get assigned bus IDs in the time slot
                    var assignedBusIds = await _context.SportsEvents
                        .Where(e => e.BusId.HasValue &&
                                   e.StartTime < endTime && e.EndTime > startTime)
                        .Select(e => e.BusId!.Value)
                        .Distinct()
                        .ToListAsync();

                    Logger.Debug("Found {AssignedCount} buses already assigned in time slot", assignedBusIds.Count);

                    LogContext.PushProperty("PipelineStep", "BusFiltering");
                    // Get available buses with sufficient capacity and proper status
                    var availableBuses = await _context.Buses
                        .Where(v => !assignedBusIds.Contains(v.BusId) &&
                                   v.SeatingCapacity >= minimumCapacity &&
                                   v.Status == "Active" &&
                                   (v.NextMaintenanceDue == null || v.NextMaintenanceDue > endTime))
                        .OrderBy(v => v.SeatingCapacity)
                        .ToListAsync();

                    Logger.Information("Found {AvailableCount} available buses for time slot {StartTime} to {EndTime} with capacity >= {MinCapacity}",
                        availableBuses.Count, startTime.ToString("yyyy-MM-dd HH:mm"), endTime.ToString("yyyy-MM-dd HH:mm"), minimumCapacity);

                    // Log details of available buses for pipeline tracing
                    foreach (var bus in availableBuses)
                    {
                        Logger.Debug("Available bus: {BusNumber} (ID: {BusId}) - Capacity: {Capacity}, Status: {Status}, NextMaint: {NextMaint}",
                            bus.BusNumber, bus.BusId, bus.SeatingCapacity, bus.Status,
                            bus.NextMaintenanceDue?.ToString("yyyy-MM-dd") ?? "None");
                    }

                    LogContext.PushProperty("PipelineStep", "Complete");
                    Logger.Debug("Available buses query pipeline completed successfully");

                    return availableBuses;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Pipeline failure in GetAvailableBuses at step {PipelineStep}",
                        pipelineStep);
                    return new List<Bus>();
                }
            }
        }

        /// <summary>
        /// Retrieves a list of available drivers for a given time slot
        /// </summary>
        /// <param name="startTime">The start time of the time slot</param>
        /// <param name="endTime">The end time of the time slot</param>
        /// <returns>A list of available Driver objects</returns>
        public async Task<List<Driver>> GetAvailableDriversAsync(DateTime startTime, DateTime endTime)
        {
            try
            {
                if (startTime >= endTime)
                {
                    Logger.Warning("Invalid time slot: StartTime must be before EndTime");
                    return new List<Driver>();
                }

                // Get assigned driver IDs in the time slot
                var assignedDriverIds = await _context.SportsEvents
                    .Where(e => e.DriverId.HasValue &&
                               e.StartTime < endTime && e.EndTime > startTime)
                    .Select(e => e.DriverId!.Value)
                    .Distinct()
                    .ToListAsync();

                // Get available drivers
                var availableDrivers = await _context.Drivers
                    .Where(d => !assignedDriverIds.Contains(d.DriverId))
                    .OrderBy(d => d.DriverName)
                    .ToListAsync();

                Logger.Information("Found {Count} available drivers for time slot {StartTime} to {EndTime}",
                    availableDrivers.Count, startTime.ToString("yyyy-MM-dd HH:mm"), endTime.ToString("yyyy-MM-dd HH:mm"));

                return availableDrivers;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving available drivers");
                return new List<Driver>();
            }
        }

        /// <summary>
        /// Updates the status of a sports event
        /// </summary>
        /// <param name="eventId">The ID of the sports event</param>
        /// <param name="status">The new status</param>
        /// <returns>True if update successful</returns>
        public async Task<bool> UpdateEventStatusAsync(int eventId, string status)
        {
            try
            {
                var sportsEvent = await _context.SportsEvents.FindAsync(eventId);
                if (sportsEvent == null)
                {
                    Logger.Warning("SportsEvent with ID {EventId} not found for status update", eventId);
                    return false;
                }

                var oldStatus = sportsEvent.Status;
                sportsEvent.Status = status;

                await _context.SaveChangesAsync();

                Logger.Information("Updated event {EventId} status from {OldStatus} to {NewStatus}",
                    eventId, oldStatus, status);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating event status for event {EventId}", eventId);
                return false;
            }
        }

        /// <summary>
        /// Gets upcoming sports events within the specified number of days
        /// </summary>
        /// <param name="days">Number of days to look ahead</param>
        /// <returns>List of upcoming sports events</returns>
        public async Task<List<SportsEvent>> GetUpcomingSportsEventsAsync(int days = 7)
        {
            try
            {
                var startDate = DateTime.Now;
                var endDate = startDate.AddDays(days);

                var upcomingEvents = await _context.SportsEvents
                    .Include(e => e.Vehicle)
                    .Include(e => e.Driver)
                    .Where(e => e.StartTime >= startDate && e.StartTime <= endDate)
                    .OrderBy(e => e.StartTime)
                    .ToListAsync();

                Logger.Information("Retrieved {Count} upcoming sports events in next {Days} days",
                    upcomingEvents.Count, days);

                return upcomingEvents;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving upcoming sports events");
                return new List<SportsEvent>();
            }
        }

        /// <summary>
        /// Assigns a bus and driver to a sports event asynchronously
        /// </summary>
        /// <param name="eventId">The ID of the sports event</param>
        /// <param name="busId">The ID of the bus to assign</param>
        /// <param name="driverId">The ID of the driver to assign</param>
        /// <returns>True if assignment successful</returns>
        public async Task<bool> AssignVehicleAndDriverAsync(int eventId, int busId, int driverId)
        {
            // Delegate to the existing AssignBusAndDriverAsync method
            return await AssignBusAndDriverAsync(eventId, busId, driverId);
        }

        /// <summary>
        /// Retrieves a list of available vehicles for a given time slot
        /// </summary>
        /// <param name="startTime">The start time of the time slot</param>
        /// <param name="endTime">The end time of the time slot</param>
        /// <param name="minimumCapacity">Minimum vehicle capacity required</param>
        /// <returns>A list of available Bus objects</returns>
        public async Task<List<Bus>> GetAvailableVehiclesAsync(DateTime startTime, DateTime endTime, int minimumCapacity = 1)
        {
            // Delegate to the existing GetAvailableBusesAsync method
            return await GetAvailableBusesAsync(startTime, endTime, minimumCapacity);
        }
    }
}
