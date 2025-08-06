using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Services
{
    public class ScheduleService : IScheduleService
    {
        private static readonly ILogger Logger = Log.ForContext<ScheduleService>();
        private readonly IBusBuddyDbContextFactory _contextFactory;

        public ScheduleService(IBusBuddyDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            Logger.Debug("ScheduleService initialized with context factory");
        }

        public async Task<IEnumerable<Schedule>> GetSchedulesAsync()
        {
            using (LogContext.PushProperty("Operation", "GetSchedulesAsync"))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Information("Starting to retrieve all schedules");

                var context = _contextFactory.CreateDbContext();
                try
                {
                    var schedules = await context.Schedules
                        .Include(s => s.Route)
                        .Include(s => s.Bus)
                        .Include(s => s.Driver)
                        .AsNoTracking()
                        .ToListAsync();

                    stopwatch.Stop();
                    Logger.Information("Successfully retrieved {ScheduleCount} schedules in {ElapsedMs}ms",
                        schedules.Count, stopwatch.ElapsedMilliseconds);

                    return schedules;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "Error retrieving schedules after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task<Schedule?> GetScheduleByIdAsync(int id)
        {
            using (LogContext.PushProperty("Operation", "GetScheduleByIdAsync"))
            using (LogContext.PushProperty("ScheduleId", id))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Information("Starting to retrieve schedule by ID");

                var context = _contextFactory.CreateDbContext();
                try
                {
                    var schedule = await context.Schedules
                        .Include(s => s.Route)
                        .Include(s => s.Bus)
                        .Include(s => s.Driver)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.ScheduleId == id);

                    stopwatch.Stop();
                    if (schedule != null)
                    {
                        Logger.Information("Successfully retrieved schedule in {ElapsedMs}ms. SportsCategory: {SportsCategory}, Location: {Location}",
                            stopwatch.ElapsedMilliseconds, schedule.SportsCategory, schedule.Location);
                    }
                    else
                    {
                        Logger.Warning("Schedule not found after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    }

                    return schedule;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "Error retrieving schedule by ID after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task AddScheduleAsync(Schedule schedule)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            using (LogContext.PushProperty("Operation", "AddScheduleAsync"))
            using (LogContext.PushProperty("SportsCategory", schedule.SportsCategory))
            using (LogContext.PushProperty("Location", schedule.Location))
            using (LogContext.PushProperty("Opponent", schedule.Opponent))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Information("Starting to add new schedule. IsSportsTrip: {IsSportsTrip}", schedule.IsSportsTrip);

                if (schedule.DepartureTime >= schedule.ArrivalTime)
                {
                    Logger.Error("Invalid schedule times: DepartureTime {DepartureTime} >= ArrivalTime {ArrivalTime}",
                        schedule.DepartureTime, schedule.ArrivalTime);
                    throw new ArgumentException("Departure time must be before arrival time.");
                }

                // Apply trip derivation logic before saving
                Logger.Debug("Applying trip derivation logic before saving");
                DeriveTripDetails(schedule);

                var context = _contextFactory.CreateWriteDbContext();
                try
                {
                    // Validation
                    if (!await context.Routes.AnyAsync(r => r.RouteId == schedule.RouteId))
                    {
                        Logger.Error("Invalid route ID: {RouteId}", schedule.RouteId);
                        throw new ArgumentException("Invalid route ID.");
                    }
                    if (!await context.Vehicles.AnyAsync(b => b.VehicleId == schedule.BusId))
                    {
                        Logger.Error("Invalid bus ID: {BusId}", schedule.BusId);
                        throw new ArgumentException("Invalid bus ID.");
                    }
                    if (!await context.Drivers.AnyAsync(d => d.DriverId == schedule.DriverId))
                    {
                        Logger.Error("Invalid driver ID: {DriverId}", schedule.DriverId);
                        throw new ArgumentException("Invalid driver ID.");
                    }

                    await context.Schedules.AddAsync(schedule);
                    await context.SaveChangesAsync();

                    stopwatch.Stop();
                    Logger.Information("Successfully added schedule with ID {ScheduleId} in {ElapsedMs}ms. Final DestinationTown: {DestinationTown}",
                        schedule.ScheduleId, stopwatch.ElapsedMilliseconds, schedule.DestinationTown);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "Error adding schedule after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task UpdateScheduleAsync(Schedule schedule)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            using (LogContext.PushProperty("Operation", "UpdateScheduleAsync"))
            using (LogContext.PushProperty("ScheduleId", schedule.ScheduleId))
            using (LogContext.PushProperty("SportsCategory", schedule.SportsCategory))
            using (LogContext.PushProperty("Location", schedule.Location))
            using (LogContext.PushProperty("Opponent", schedule.Opponent))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Information("Starting to update schedule. IsSportsTrip: {IsSportsTrip}", schedule.IsSportsTrip);

                // Apply trip derivation logic before saving
                Logger.Debug("Applying trip derivation logic before updating");
                DeriveTripDetails(schedule);

                var context = _contextFactory.CreateWriteDbContext();
                try
                {
                    var existing = await context.Schedules.FindAsync(schedule.ScheduleId);
                    if (existing == null)
                    {
                        Logger.Error("Schedule not found for update");
                        throw new InvalidOperationException("Schedule not found.");
                    }

                    if (schedule.DepartureTime >= schedule.ArrivalTime)
                    {
                        Logger.Error("Invalid schedule times: DepartureTime {DepartureTime} >= ArrivalTime {ArrivalTime}",
                            schedule.DepartureTime, schedule.ArrivalTime);
                        throw new ArgumentException("Departure time must be before arrival time.");
                    }

                    // Validation
                    if (!await context.Routes.AnyAsync(r => r.RouteId == schedule.RouteId))
                    {
                        Logger.Error("Invalid route ID: {RouteId}", schedule.RouteId);
                        throw new ArgumentException("Invalid route ID.");
                    }
                    if (!await context.Vehicles.AnyAsync(b => b.VehicleId == schedule.BusId))
                    {
                        Logger.Error("Invalid bus ID: {BusId}", schedule.BusId);
                        throw new ArgumentException("Invalid bus ID.");
                    }
                    if (!await context.Drivers.AnyAsync(d => d.DriverId == schedule.DriverId))
                    {
                        Logger.Error("Invalid driver ID: {DriverId}", schedule.DriverId);
                        throw new ArgumentException("Invalid driver ID.");
                    }

                    context.Entry(existing).CurrentValues.SetValues(schedule);
                    await context.SaveChangesAsync();

                    stopwatch.Stop();
                    Logger.Information("Successfully updated schedule in {ElapsedMs}ms. Final DestinationTown: {DestinationTown}",
                        stopwatch.ElapsedMilliseconds, schedule.DestinationTown);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "Error updating schedule after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }
        }

        public async Task DeleteScheduleAsync(int id)
        {
            using (LogContext.PushProperty("Operation", "DeleteScheduleAsync"))
            using (LogContext.PushProperty("ScheduleId", id))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Information("Starting to delete schedule");

                var context = _contextFactory.CreateWriteDbContext();
                try
                {
                    var schedule = await context.Schedules.FindAsync(id);
                    if (schedule == null)
                    {
                        Logger.Warning("Schedule not found for deletion");
                        return;
                    }

                    Logger.Debug("Deleting schedule with SportsCategory: {SportsCategory}, Location: {Location}",
                        schedule.SportsCategory, schedule.Location);

                    context.Schedules.Remove(schedule);
                    await context.SaveChangesAsync();

                    stopwatch.Stop();
                    Logger.Information("Successfully deleted schedule in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "Error deleting schedule after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// Filters schedules by sports category for dropdown focus
        /// </summary>
        /// <param name="category">Sports category to filter by (e.g., "Volleyball", "Football", "Activity")</param>
        /// <returns>Filtered list of schedules</returns>
        public async Task<IEnumerable<Schedule>> GetSchedulesByCategoryAsync(string category)
        {
            using (LogContext.PushProperty("Operation", "GetSchedulesByCategoryAsync"))
            using (LogContext.PushProperty("Category", category))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Information("Starting to retrieve schedules by category");

                var context = _contextFactory.CreateDbContext();
                try
                {
                    var query = context.Schedules
                        .Include(s => s.Route)
                        .Include(s => s.Bus)
                        .Include(s => s.Driver)
                        .AsNoTracking();

                    if (!string.IsNullOrEmpty(category))
                    {
                        if (category.Equals("Sports", StringComparison.OrdinalIgnoreCase))
                        {
                            // Return all sports categories (exclude "Activity" and null)
                            query = query.Where(s => !string.IsNullOrEmpty(s.SportsCategory) && s.SportsCategory != "Activity");
                            Logger.Debug("Filtering for all sports categories");
                        }
                        else if (category.Equals("Routes", StringComparison.OrdinalIgnoreCase))
                        {
                            // Return regular routes (null or "Activity" sports category)
                            query = query.Where(s => string.IsNullOrEmpty(s.SportsCategory) || s.SportsCategory == "Activity");
                            Logger.Debug("Filtering for regular routes");
                        }
                        else
                        {
                            // Filter by specific sports category
                            query = query.Where(s => s.SportsCategory == category);
                            Logger.Debug("Filtering for specific sports category");
                        }
                    }

                    var schedules = await query.ToListAsync();

                    stopwatch.Stop();
                    Logger.Information("Successfully retrieved {ScheduleCount} schedules by category in {ElapsedMs}ms",
                        schedules.Count, stopwatch.ElapsedMilliseconds);

                    // Log category breakdown for diagnostics
                    if (schedules.Count > 0)
                    {
                        var categoryBreakdown = schedules.GroupBy(s => s.SportsCategory ?? "None")
                            .ToDictionary(g => g.Key, g => g.Count());
                        Logger.Debug("Category breakdown: {@CategoryBreakdown}", categoryBreakdown);
                    }

                    return schedules;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "Error retrieving schedules by category after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// Derives trip details from schedule properties
        /// Auto-sets DestinationTown if Location indicates "Away" game
        /// </summary>
        /// <param name="schedule">Schedule to derive details for</param>
        public void DeriveTripDetails(Schedule schedule)
        {
            if (schedule == null)
            {
                Logger.Warning("DeriveTripDetails called with null schedule");
                return;
            }

            using (LogContext.PushProperty("Operation", "DeriveTripDetails"))
            using (LogContext.PushProperty("ScheduleId", schedule.ScheduleId))
            using (LogContext.PushProperty("SportsCategory", schedule.SportsCategory))
            using (LogContext.PushProperty("Location", schedule.Location))
            {
                Logger.Debug("Starting trip details derivation");

                // Only apply derivation logic for sports trips
                if (string.IsNullOrEmpty(schedule.SportsCategory) || schedule.SportsCategory == "Activity")
                {
                    Logger.Debug("Skipping derivation for non-sports trip");
                    return;
                }

                var originalDestinationTown = schedule.DestinationTown;

                // Auto-derive destination town from location for away games
                if (!string.IsNullOrEmpty(schedule.Location))
                {
                    var location = schedule.Location.Trim();
                    Logger.Debug("Processing location: {Location}", location);

                    // Check if it's an away game
                    if (location.Contains("away", StringComparison.OrdinalIgnoreCase) ||
                        location.Contains('@', StringComparison.OrdinalIgnoreCase) ||
                        location.Contains("at ", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Debug("Detected away game from location");

                        // Try to extract town/city from the location
                        var townCandidate = ExtractTownFromLocation(location);
                        if (!string.IsNullOrEmpty(townCandidate))
                        {
                            schedule.DestinationTown = townCandidate;
                            Logger.Information("Auto-derived destination town: {DestinationTown} from location: {Location}",
                                townCandidate, location);
                        }
                        else
                        {
                            Logger.Warning("Could not extract town from away game location: {Location}", location);
                        }
                    }
                    else if (location.Contains("home", StringComparison.OrdinalIgnoreCase))
                    {
                        // For home games, clear destination town
                        schedule.DestinationTown = null;
                        Logger.Debug("Cleared destination town for home game");
                    }
                }

                // Set default depart and scheduled times if not provided
                if (schedule.DepartTime == null && schedule.ScheduledTime != null)
                {
                    // Default: depart 1 hour before the scheduled event time
                    schedule.DepartTime = schedule.ScheduledTime.Value.Subtract(TimeSpan.FromHours(1));
                    Logger.Debug("Auto-set DepartTime to 1 hour before ScheduledTime: {DepartTime}", schedule.DepartTime);
                }

                // Ensure departure time is set based on depart time for sports trips
                if (schedule.DepartTime.HasValue)
                {
                    var originalDepartureTime = schedule.DepartureTime;
                    var departDateTime = schedule.ScheduleDate.Date.Add(schedule.DepartTime.Value);
                    schedule.DepartureTime = departDateTime;
                    Logger.Debug("Updated DepartureTime from {OriginalDepartureTime} to {DepartureTime}",
                        originalDepartureTime, schedule.DepartureTime);
                }

                // Ensure arrival time is set based on scheduled time for sports trips
                if (schedule.ScheduledTime.HasValue)
                {
                    var originalArrivalTime = schedule.ArrivalTime;
                    var arrivalDateTime = schedule.ScheduleDate.Date.Add(schedule.ScheduledTime.Value);
                    schedule.ArrivalTime = arrivalDateTime;
                    Logger.Debug("Updated ArrivalTime from {OriginalArrivalTime} to {ArrivalTime}",
                        originalArrivalTime, schedule.ArrivalTime);
                }

                Logger.Information("Trip details derivation completed. DestinationTown changed from {OriginalDestinationTown} to {NewDestinationTown}",
                    originalDestinationTown, schedule.DestinationTown);
            }
        }

        /// <summary>
        /// Extracts town/city name from a location string
        /// </summary>
        /// <param name="location">Location string to parse</param>
        /// <returns>Extracted town name or null if not found</returns>
        private string? ExtractTownFromLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                Logger.Debug("ExtractTownFromLocation called with null/empty location");
                return null;
            }

            using (LogContext.PushProperty("Operation", "ExtractTownFromLocation"))
            using (LogContext.PushProperty("InputLocation", location))
            {
                Logger.Debug("Attempting to extract town from location");

                // Remove common prefixes
                var cleanedLocation = location.Replace("away at ", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("at ", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("@ ", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("away", "", StringComparison.OrdinalIgnoreCase)
                                     .Trim();

                Logger.Debug("Cleaned location: {CleanedLocation}", cleanedLocation);

                // Split by common separators and take the first meaningful part
                var parts = cleanedLocation.Split(new[] { ',', '-', '(', ')', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 2)
                    {
                        Logger.Information("Successfully extracted town: {ExtractedTown} from location: {OriginalLocation}",
                            trimmed, location);
                        return trimmed;
                    }
                }

                Logger.Warning("Could not extract meaningful town from location: {Location}", location);
                return null;
            }
        }
    }
}
