using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Models;
using BusBuddy.Core.Data;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Sports event scheduling service with safety-first transportation management
    /// Enhanced for Phase 2 sports scheduling with NHTSA compliance
    /// </summary>
    public interface ISchedulingService
    {
        Task<IEnumerable<SportsEvent>> GetAllSportsEventsAsync();
        Task<IEnumerable<SportsEvent>> GetUpcomingSportsEventsAsync();
        Task<SportsEvent?> GetSportsEventByIdAsync(int id);
        Task<SportsEvent> CreateSportsEventAsync(SportsEvent sportsEvent);
        Task<SportsEvent> UpdateSportsEventAsync(SportsEvent sportsEvent);
        Task<bool> DeleteSportsEventAsync(int id);
        Task<bool> AssignBusAndDriverAsync(int eventId, int busId, int driverId);
        Task<IEnumerable<SportsEvent>> GetEventsByStatusAsync(string status);
        Task<IEnumerable<SportsEvent>> GetEventsBySportAsync(string sport);
        Task<bool> ValidateEventSafetyAsync(SportsEvent sportsEvent);
        Task<string> GenerateSafetyChecklistAsync(SportsEvent sportsEvent);
    }

    /// <summary>
    /// Implementation of sports event scheduling service
    /// Focuses on safety-first transportation management with NHTSA guidelines
    /// </summary>
    public class SchedulingService : ISchedulingService
    {
        private static readonly ILogger Logger = Log.ForContext<SchedulingService>();
        private readonly BusBuddyDbContext _context;

        public SchedulingService(BusBuddyDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Get all sports events with related data
        /// </summary>
        public async Task<IEnumerable<SportsEvent>> GetAllSportsEventsAsync()
        {
            try
            {
                Logger.Information("Retrieving all sports events");

                var events = await _context.SportsEvents
                    .Include(e => e.Vehicle)
                    .Include(e => e.Driver)
                    .OrderBy(e => e.StartTime)
                    .ToListAsync();

                Logger.Information("Retrieved {Count} sports events", events.Count);
                return events;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving all sports events");
                throw;
            }
        }

        /// <summary>
        /// Get upcoming sports events (future events only)
        /// </summary>
        public async Task<IEnumerable<SportsEvent>> GetUpcomingSportsEventsAsync()
        {
            try
            {
                var now = DateTime.Now;
                Logger.Information("Retrieving upcoming sports events from {Now}", now);

                var events = await _context.SportsEvents
                    .Include(e => e.Vehicle)
                    .Include(e => e.Driver)
                    .Where(e => e.StartTime > now)
                    .OrderBy(e => e.StartTime)
                    .ToListAsync();

                Logger.Information("Retrieved {Count} upcoming sports events", events.Count);
                return events;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving upcoming sports events");
                throw;
            }
        }

        /// <summary>
        /// Get a specific sports event by ID
        /// </summary>
        public async Task<SportsEvent?> GetSportsEventByIdAsync(int id)
        {
            try
            {
                Logger.Information("Retrieving sports event with ID {EventId}", id);

                var sportsEvent = await _context.SportsEvents
                    .Include(e => e.Vehicle)
                    .Include(e => e.Driver)
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (sportsEvent == null)
                {
                    Logger.Warning("Sports event with ID {EventId} not found", id);
                }

                return sportsEvent;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving sports event with ID {EventId}", id);
                throw;
            }
        }

        /// <summary>
        /// Create a new sports event with safety validation
        /// </summary>
        public async Task<SportsEvent> CreateSportsEventAsync(SportsEvent sportsEvent)
        {
            try
            {
                Logger.Information("Creating new sports event: {EventName}", sportsEvent.EventName);

                // Validate safety requirements
                var isSafe = await ValidateEventSafetyAsync(sportsEvent);
                if (!isSafe)
                {
                    throw new InvalidOperationException("Sports event does not meet safety requirements");
                }

                // Set default safety notes if empty
                if (string.IsNullOrEmpty(sportsEvent.SafetyNotes))
                {
                    sportsEvent.SafetyNotes = sportsEvent.GetDefaultSafetyNotes();
                    Logger.Information("Applied default safety notes to event {EventName}", sportsEvent.EventName);
                }

                // Set created timestamp
                sportsEvent.CreatedAt = DateTime.UtcNow;
                sportsEvent.UpdatedAt = DateTime.UtcNow;

                _context.SportsEvents.Add(sportsEvent);
                await _context.SaveChangesAsync();

                Logger.Information("Successfully created sports event {EventName} with ID {EventId}",
                    sportsEvent.EventName, sportsEvent.Id);

                return sportsEvent;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error creating sports event {EventName}", sportsEvent.EventName);
                throw;
            }
        }

        /// <summary>
        /// Update an existing sports event
        /// </summary>
        public async Task<SportsEvent> UpdateSportsEventAsync(SportsEvent sportsEvent)
        {
            try
            {
                Logger.Information("Updating sports event {EventId}: {EventName}", sportsEvent.Id, sportsEvent.EventName);

                // Validate safety requirements
                var isSafe = await ValidateEventSafetyAsync(sportsEvent);
                if (!isSafe)
                {
                    throw new InvalidOperationException("Updated sports event does not meet safety requirements");
                }

                // Update timestamp
                sportsEvent.UpdatedAt = DateTime.UtcNow;

                _context.SportsEvents.Update(sportsEvent);
                await _context.SaveChangesAsync();

                Logger.Information("Successfully updated sports event {EventId}: {EventName}",
                    sportsEvent.Id, sportsEvent.EventName);

                return sportsEvent;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating sports event {EventId}: {EventName}",
                    sportsEvent.Id, sportsEvent.EventName);
                throw;
            }
        }

        /// <summary>
        /// Delete a sports event
        /// </summary>
        public async Task<bool> DeleteSportsEventAsync(int id)
        {
            try
            {
                Logger.Information("Deleting sports event with ID {EventId}", id);

                var sportsEvent = await _context.SportsEvents.FindAsync(id);
                if (sportsEvent == null)
                {
                    Logger.Warning("Sports event with ID {EventId} not found for deletion", id);
                    return false;
                }

                _context.SportsEvents.Remove(sportsEvent);
                await _context.SaveChangesAsync();

                Logger.Information("Successfully deleted sports event {EventId}: {EventName}",
                    id, sportsEvent.EventName);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting sports event with ID {EventId}", id);
                throw;
            }
        }

        /// <summary>
        /// Assign vehicle and driver to a sports event
        /// </summary>
        public async Task<bool> AssignBusAndDriverAsync(int eventId, int busId, int driverId)
        {
            try
            {
                Logger.Information("Assigning bus {BusId} and driver {DriverId} to event {EventId}",
                    busId, driverId, eventId);

                var sportsEvent = await _context.SportsEvents.FindAsync(eventId);
                if (sportsEvent == null)
                {
                    Logger.Warning("Sports event with ID {EventId} not found for assignment", eventId);
                    return false;
                }

                // Validate bus exists and is available
                var bus = await _context.Buses.FindAsync(busId);
                if (bus == null) // Note: Bus status validation would need Bus.Status property
                {
                    Logger.Warning("Bus {BusId} not found for assignment", busId);
                    return false;
                }

                // Validate driver exists and is available
                var driver = await _context.Drivers.FindAsync(driverId);
                if (driver == null) // Note: Driver status validation would need Driver.Status property
                {
                    Logger.Warning("Driver {DriverId} not found for assignment", driverId);
                    return false;
                }

                // Check team size vs bus capacity
                if (sportsEvent.TeamSize > bus.Capacity)
                {
                    Logger.Warning("Bus capacity {Capacity} insufficient for team size {TeamSize}",
                        bus.Capacity, sportsEvent.TeamSize);
                    return false;
                }

                // Make assignment
                sportsEvent.VehicleId = busId;
                sportsEvent.DriverId = driverId;
                sportsEvent.Status = "Assigned";
                sportsEvent.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                Logger.Information("Successfully assigned bus {BusId} and driver {DriverId} to event {EventId}",
                    busId, driverId, eventId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error assigning vehicle and driver to event {EventId}", eventId);
                throw;
            }
        }

        /// <summary>
        /// Get events by status
        /// </summary>
        public async Task<IEnumerable<SportsEvent>> GetEventsByStatusAsync(string status)
        {
            try
            {
                Logger.Information("Retrieving sports events with status {Status}", status);

                var events = await _context.SportsEvents
                    .Include(e => e.Vehicle)
                    .Include(e => e.Driver)
                    .Where(e => e.Status == status)
                    .OrderBy(e => e.StartTime)
                    .ToListAsync();

                Logger.Information("Retrieved {Count} sports events with status {Status}", events.Count, status);
                return events;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving sports events with status {Status}", status);
                throw;
            }
        }

        /// <summary>
        /// Get events by sport type
        /// </summary>
        public async Task<IEnumerable<SportsEvent>> GetEventsBySportAsync(string sport)
        {
            try
            {
                Logger.Information("Retrieving sports events for sport {Sport}", sport);

                var events = await _context.SportsEvents
                    .Include(e => e.Vehicle)
                    .Include(e => e.Driver)
                    .Where(e => string.Equals(e.Sport, sport, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.StartTime)
                    .ToListAsync();

                Logger.Information("Retrieved {Count} sports events for sport {Sport}", events.Count, sport);
                return events;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving sports events for sport {Sport}", sport);
                throw;
            }
        }

        /// <summary>
        /// Validate event safety requirements
        /// </summary>
        public Task<bool> ValidateEventSafetyAsync(SportsEvent sportsEvent)
        {
            try
            {
                Logger.Information("Validating safety for sports event {EventName}", sportsEvent.EventName);

                // Use the built-in safety validation
                var isValid = sportsEvent.IsEventSafe();

                // Additional validation rules can be added here
                if (isValid)
                {
                    // Check for weather-related safety concerns
                    if (!string.IsNullOrEmpty(sportsEvent.WeatherConditions))
                    {
                        var dangerousWeather = new[] { "severe", "storm", "ice", "blizzard", "tornado" };
                        if (dangerousWeather.Any(w => sportsEvent.WeatherConditions.Contains(w, StringComparison.OrdinalIgnoreCase)))
                        {
                            Logger.Warning("Dangerous weather conditions detected for event {EventName}: {Weather}",
                                sportsEvent.EventName, sportsEvent.WeatherConditions);
                            isValid = false;
                        }
                    }

                    // Ensure event is not too far in the future (practical scheduling limit)
                    if (sportsEvent.StartTime > DateTime.Now.AddMonths(6))
                    {
                        Logger.Warning("Event {EventName} scheduled too far in advance: {StartTime}",
                            sportsEvent.EventName, sportsEvent.StartTime);
                        isValid = false;
                    }
                }

                Logger.Information("Safety validation for event {EventName}: {IsValid}",
                    sportsEvent.EventName, isValid ? "PASSED" : "FAILED");

                return Task.FromResult(isValid);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error validating safety for sports event {EventName}", sportsEvent.EventName);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Generate safety checklist for an event
        /// </summary>
        public Task<string> GenerateSafetyChecklistAsync(SportsEvent sportsEvent)
        {
            try
            {
                Logger.Information("Generating safety checklist for event {EventName}", sportsEvent.EventName);

                var checklist = $@"
🛡️ SAFETY CHECKLIST FOR {sportsEvent.EventName.ToUpper(CultureInfo.InvariantCulture)}
═══════════════════════════════════════════════════════════

📅 Event Details:
   • Date: {sportsEvent.StartTime:yyyy-MM-dd}
   • Time: {sportsEvent.StartTime:HH:mm} - {sportsEvent.EndTime:HH:mm}
   • Location: {sportsEvent.Location}
   • Sport: {sportsEvent.Sport}
   • Team Size: {sportsEvent.TeamSize}
   • Type: {(sportsEvent.IsHomeGame ? "Home Game" : "Away Game")}

🚌 Transportation Safety (NHTSA Guidelines):
   ☐ Students arrive 5 minutes early
   ☐ Students stand 10 feet back from vehicle
   ☐ Driver performs complete blind spot check
   ☐ All seats have functional seatbelts
   ☐ Emergency exits clearly marked
   ☐ First aid kit accessible
   ☐ Driver has valid CDL and sports transport certification

🌤️ Weather Considerations:
   • Current Conditions: {(string.IsNullOrEmpty(sportsEvent.WeatherConditions) ? "Not specified" : sportsEvent.WeatherConditions)}
   ☐ Route adjusted for weather conditions
   ☐ Extra travel time allocated if needed
   ☐ Emergency contact informed of weather delays

📞 Emergency Preparedness:
   • Contact: {(string.IsNullOrEmpty(sportsEvent.EmergencyContact) ? "Not specified" : sportsEvent.EmergencyContact)}
   ☐ Emergency contact numbers verified
   ☐ School administration notified
   ☐ Medical emergency procedures reviewed
   ☐ Vehicle emergency kit checked

📋 Additional Safety Notes:
{sportsEvent.SafetyNotes}

✅ Pre-Departure Checklist:
   ☐ Vehicle inspection completed
   ☐ Driver rest period verified (minimum 8 hours)
   ☐ Route planned and reviewed
   ☐ Student roster verified
   ☐ Communication devices tested
   ☐ Weather conditions assessed

Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
";

                Logger.Information("Safety checklist generated for event {EventName}", sportsEvent.EventName);
                return Task.FromResult(checklist);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating safety checklist for event {EventName}", sportsEvent.EventName);
                throw;
            }
        }
    }
}
