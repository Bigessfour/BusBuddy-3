using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Data;
using Serilog;

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

            try
            {
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

                await _context.SportsEvents.AddAsync(sportsEvent);
                await _context.SaveChangesAsync();

                Logger.Information("SportsEvent {EventName} created with ID: {EventId}",
                    sportsEvent.EventName, sportsEvent.Id);

                return sportsEvent;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error creating sports event {EventName}", sportsEvent?.EventName ?? "Unknown");
                throw;
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
        /// Assigns a vehicle and driver to a sports event asynchronously
        /// </summary>
        /// <param name="eventId">The ID of the sports event</param>
        /// <param name="vehicleId">The ID of the vehicle to assign</param>
        /// <param name="driverId">The ID of the driver to assign</param>
        /// <returns>True if assignment successful</returns>
        public async Task<bool> AssignVehicleAndDriverAsync(int eventId, int vehicleId, int driverId)
        {
            try
            {
                var sportsEvent = await _context.SportsEvents.FindAsync(eventId);
                if (sportsEvent == null)
                {
                    Logger.Warning("SportsEvent with ID {EventId} not found", eventId);
                    return false;
                }

                var vehicle = await _context.Buses.FindAsync(vehicleId);
                if (vehicle == null)
                {
                    Logger.Warning("Bus with ID {VehicleId} not found", vehicleId);
                    return false;
                }

                var driver = await _context.Drivers.FindAsync(driverId);
                if (driver == null)
                {
                    Logger.Warning("Driver with ID {DriverId} not found", driverId);
                    return false;
                }

                // Validate conflicts before assignment
                var noConflicts = await ValidateSchedulingConflictsAsync(
                    sportsEvent.StartTime, sportsEvent.EndTime, vehicleId, driverId);

                if (!noConflicts)
                {
                    Logger.Warning("Scheduling conflict detected for Event {EventId}, Vehicle {VehicleId}, Driver {DriverId}",
                        eventId, vehicleId, driverId);
                    return false;
                }

                // Check vehicle capacity
                if (vehicle.Capacity < sportsEvent.TeamSize)
                {
                    Logger.Warning("Vehicle {VehicleId} capacity {Capacity} insufficient for team size {TeamSize}",
                        vehicleId, vehicle.Capacity, sportsEvent.TeamSize);
                    return false;
                }

                // Assign vehicle and driver
                sportsEvent.VehicleId = vehicleId;
                sportsEvent.DriverId = driverId;
                sportsEvent.Status = "Assigned";

                await _context.SaveChangesAsync();

                Logger.Information("Assignment completed for Event {EventId} with Vehicle {VehicleId} and Driver {DriverId}",
                    eventId, vehicleId, driverId);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error assigning vehicle and driver to event {EventId}", eventId);
                return false;
            }
        }

        /// <summary>
        /// Validates if there are any scheduling conflicts for the given vehicle or driver during the event time
        /// </summary>
        /// <param name="startTime">Event start time</param>
        /// <param name="endTime">Event end time</param>
        /// <param name="vehicleId">The ID of the vehicle to check (optional)</param>
        /// <param name="driverId">The ID of the driver to check (optional)</param>
        /// <returns>True if no conflicts, false otherwise</returns>
        public async Task<bool> ValidateSchedulingConflictsAsync(DateTime startTime, DateTime endTime, int? vehicleId = null, int? driverId = null)
        {
            try
            {
                if (startTime >= endTime)
                {
                    Logger.Warning("Invalid time slot: StartTime {StartTime} must be before EndTime {EndTime}",
                        startTime, endTime);
                    return false;
                }

                var conflicts = 0;

                // Check for vehicle conflicts
                if (vehicleId.HasValue)
                {
                    var vehicleConflicts = await _context.SportsEvents
                        .Where(e => e.VehicleId == vehicleId.Value &&
                                   e.StartTime < endTime && e.EndTime > startTime)
                        .CountAsync();
                    conflicts += vehicleConflicts;
                }

                // Check for driver conflicts
                if (driverId.HasValue)
                {
                    var driverConflicts = await _context.SportsEvents
                        .Where(e => e.DriverId == driverId.Value &&
                                   e.StartTime < endTime && e.EndTime > startTime)
                        .CountAsync();
                    conflicts += driverConflicts;
                }

                var noConflicts = conflicts == 0;

                Logger.Information("Conflict validation for Vehicle {VehicleId}, Driver {DriverId}: {Result}",
                    vehicleId?.ToString(CultureInfo.InvariantCulture) ?? "N/A",
                    driverId?.ToString(CultureInfo.InvariantCulture) ?? "N/A",
                    noConflicts ? "No conflicts" : $"{conflicts} conflicts found");

                return noConflicts;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during scheduling conflict validation");
                return false;
            }
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
            try
            {
                if (startTime >= endTime)
                {
                    Logger.Warning("Invalid time slot: StartTime must be before EndTime");
                    return new List<Bus>();
                }

                // Get assigned vehicle IDs in the time slot
                var assignedVehicleIds = await _context.SportsEvents
                    .Where(e => e.VehicleId.HasValue &&
                               e.StartTime < endTime && e.EndTime > startTime)
                    .Select(e => e.VehicleId!.Value)
                    .Distinct()
                    .ToListAsync();

                // Get available vehicles with sufficient capacity
                var availableVehicles = await _context.Buses
                    .Where(v => !assignedVehicleIds.Contains(v.Id) &&
                               v.Capacity >= minimumCapacity)
                    .OrderBy(v => v.Capacity)
                    .ToListAsync();

                Logger.Information("Found {Count} available vehicles for time slot {StartTime} to {EndTime} with capacity >= {MinCapacity}",
                    availableVehicles.Count, startTime.ToString("yyyy-MM-dd HH:mm"), endTime.ToString("yyyy-MM-dd HH:mm"), minimumCapacity);

                return availableVehicles;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving available vehicles");
                return new List<Bus>();
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
    }
}
