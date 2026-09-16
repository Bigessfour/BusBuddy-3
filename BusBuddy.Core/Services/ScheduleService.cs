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
            ArgumentNullException.ThrowIfNull(schedule);

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

                ScheduleTimestampNormalizer.NormalizeForPersist(schedule);

                var context = _contextFactory.CreateWriteDbContext();
                try
                {
                    // Validation
                    if (!await context.Routes.AnyAsync(r => r.RouteId == schedule.RouteId))
                    {
                        Logger.Error("Invalid route ID: {RouteId}", schedule.RouteId);
                        throw new ArgumentException("Invalid route ID.");
                    }
                    if (!await context.Buses.AnyAsync(b => b.BusId == schedule.BusId))
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
            ArgumentNullException.ThrowIfNull(schedule);

            using (LogContext.PushProperty("Operation", "UpdateScheduleAsync"))
            using (LogContext.PushProperty("ScheduleId", schedule.ScheduleId))
            using (LogContext.PushProperty("SportsCategory", schedule.SportsCategory))
            using (LogContext.PushProperty("Location", schedule.Location))
            using (LogContext.PushProperty("Opponent", schedule.Opponent))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.Information("Starting to update schedule. IsSportsTrip: {IsSportsTrip}", schedule.IsSportsTrip);

                ScheduleTimestampNormalizer.NormalizeForPersist(schedule);

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
                    if (!await context.Buses.AnyAsync(b => b.BusId == schedule.BusId))
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

        /// <inheritdoc />
        public async Task<bool> AddDailyFromPublishedRouteAsync(int routeId, DateTime utcDay)
        {
            if (routeId <= 0)
            {
                return false;
            }

            using (LogContext.PushProperty("Operation", "AddDailyFromPublishedRouteAsync"))
            using (LogContext.PushProperty("RouteId", routeId))
            {
                var context = _contextFactory.CreateDbContext();
                Route? published;
                try
                {
                    published = await context.Routes.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.RouteId == routeId)
                        .ConfigureAwait(false);
                }
                finally
                {
                    await context.DisposeAsync();
                }

                if (published is null || !PublishedRouteFleet.HasPairing(published))
                {
                    Logger.Information(
                        "Hop 5 skipped RouteId={RouteId} — missing route or session pairing",
                        routeId);
                    return false;
                }

                var day = DateTime.SpecifyKind(utcDay.Date, DateTimeKind.Utc);
                var departure = day.Add(PublishedRouteFleet.BeginTime(published));
                var arrival = departure.AddMinutes(published.EstimatedDuration ?? 45);
                if (arrival <= departure)
                {
                    arrival = departure.AddMinutes(45);
                }

                await AddScheduleAsync(new Schedule
                {
                    RouteId = published.RouteId,
                    BusId = PublishedRouteFleet.VehicleId(published)!.Value,
                    DriverId = PublishedRouteFleet.DriverId(published)!.Value,
                    ScheduleDate = day,
                    DepartureTime = departure,
                    ArrivalTime = arrival,
                    Location = published.School,
                    Notes = $"Daily schedule for {published.RouteName}",
                    Status = "Scheduled",
                    CreatedDate = DateTime.UtcNow
                }).ConfigureAwait(false);
                return true;
            }
        }
    }
}
