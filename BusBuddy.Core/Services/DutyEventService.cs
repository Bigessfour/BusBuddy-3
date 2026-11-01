using System.Diagnostics;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Duty / inspection events shared by driver and bus (#112).
/// Specs: drivers.md — one child collection; Defect → maintenance; no second fuel table.
/// </summary>
public class DutyEventService : IDutyEventService
{
    public const string DefectWorkLabel = "Defect report";
    public const string DefectVendorLabel = "Driver report";

    private static readonly ILogger Logger = Log.ForContext<DutyEventService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;

    private readonly record struct PreTripKey(int BusId, int DriverId, DutySession Session);

    public DutyEventService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
        Logger.Debug("DutyEventService constructed");
    }

    public async Task<IReadOnlyList<DutyEvent>> GetForDateAsync(DateTime dateUtc)
    {
        var day = UtcDay(dateUtc);
        var next = day.AddDays(1);
        Logger.Information("Loading duty events for {Date:yyyy-MM-dd}", day);
        using var context = _contextFactory.CreateDbContext();
        var rows = await context.DutyEvents
            .Include(d => d.Driver)
            .Include(d => d.Bus)
            .Where(d => d.EventedAt >= day && d.EventedAt < next)
            .OrderByDescending(d => d.EventedAt)
            .ToListAsync();
        Logger.Information("Loaded {Count} duty events for {Date:yyyy-MM-dd}", rows.Count, day);
        return rows;
    }

    public async Task<IReadOnlyList<DutyEvent>> GetByBusAndDriverOnDateAsync(
        int busId,
        int driverId,
        DateTime dateUtc)
    {
        var day = UtcDay(dateUtc);
        var next = day.AddDays(1);
        using var context = _contextFactory.CreateDbContext();
        return await context.DutyEvents
            .Include(d => d.Driver)
            .Include(d => d.Bus)
            .Where(d =>
                d.BusId == busId
                && d.DriverId == driverId
                && d.EventedAt >= day
                && d.EventedAt < next)
            .OrderByDescending(d => d.EventedAt)
            .ToListAsync();
    }

    public async Task<DutyEvent?> GetByIdAsync(int dutyEventId)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.DutyEvents
            .Include(d => d.Driver)
            .Include(d => d.Bus)
            .FirstOrDefaultAsync(d => d.DutyEventId == dutyEventId);
    }

    public async Task<DutyEvent> CreateAsync(DutyEvent dutyEvent)
    {
        DutyEventValidator.ValidateForPersist(dutyEvent);
        DetachGraphs(dutyEvent);
        dutyEvent.CreatedDate = DateTime.UtcNow;

        Logger.Information(
            "Creating duty event DriverId={DriverId} BusId={BusId} Session={Session} Type={EventType}",
            dutyEvent.DriverId, dutyEvent.BusId, dutyEvent.Session, dutyEvent.EventType);

        using var context = _contextFactory.CreateWriteDbContext();

        if (dutyEvent.FuelId is int fuelId)
        {
            var fuelExists = await context.FuelRecords.AnyAsync(f => f.FuelId == fuelId);
            if (!fuelExists)
            {
                throw new ArgumentException("Linked fuel record was not found.", nameof(dutyEvent));
            }
        }

        // Relational: wrap duty + defect maintenance in one transaction so a mid-fail cannot orphan.
        // InMemory has no real transactions — still one logical unit via rollback of the second write.
        var useTransaction = context.Database.IsRelational();
        await using var transaction = useTransaction
            ? await context.Database.BeginTransactionAsync()
            : null;

        try
        {
            context.DutyEvents.Add(dutyEvent);
            await context.SaveChangesAsync();

            if (dutyEvent.EventType == DutyEventType.Defect)
            {
                var maintenance = BuildDefectMaintenance(dutyEvent);
                MaintenanceRecordValidator.ValidateForPersist(maintenance);
                context.MaintenanceRecords.Add(maintenance);
                await context.SaveChangesAsync();
                Logger.Information(
                    "Defect duty {DutyEventId} created maintenance {MaintenanceId}",
                    dutyEvent.DutyEventId, maintenance.MaintenanceId);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }
        }
        catch (DbUpdateException ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }

            // InMemory: remove the duty row if maintenance failed after the first SaveChanges.
            if (!useTransaction && dutyEvent.DutyEventId > 0 && dutyEvent.EventType == DutyEventType.Defect)
            {
                var orphan = await context.DutyEvents.FindAsync(dutyEvent.DutyEventId);
                if (orphan is not null)
                {
                    context.DutyEvents.Remove(orphan);
                    await context.SaveChangesAsync();
                }
            }

            Logger.Error(ex, "EF failed creating duty event BusId={BusId} DriverId={DriverId}",
                dutyEvent.BusId, dutyEvent.DriverId);
            throw new InvalidOperationException(FormatDbError(ex), ex);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }

        Logger.Information("Created duty event {DutyEventId} Type={EventType}",
            dutyEvent.DutyEventId, dutyEvent.EventType);

        await context.Entry(dutyEvent).Reference(d => d.Driver).LoadAsync();
        await context.Entry(dutyEvent).Reference(d => d.Bus).LoadAsync();
        return dutyEvent;
    }

    public async Task<bool> DeleteAsync(int dutyEventId)
    {
        Logger.Information("Deleting duty event {DutyEventId}", dutyEventId);
        using var context = _contextFactory.CreateWriteDbContext();
        var row = await context.DutyEvents.FindAsync(dutyEventId);
        if (row is null)
        {
            Logger.Warning("Duty event {DutyEventId} not found for delete", dutyEventId);
            return false;
        }

        // Clear maintenance links so SetNull FK is explicit before delete.
        var linked = await context.MaintenanceRecords
            .Where(m => m.SourceDutyEventId == dutyEventId)
            .ToListAsync();
        foreach (var m in linked)
        {
            m.SourceDutyEventId = null;
        }

        context.DutyEvents.Remove(row);
        await context.SaveChangesAsync();
        Logger.Information("Deleted duty event {DutyEventId}", dutyEventId);
        return true;
    }

    public async Task<IReadOnlyList<MissingPreTripRow>> GetMissingPreTripsAsync(DateTime dateUtc)
    {
        var day = UtcDay(dateUtc);
        var next = day.AddDays(1);
        var stopwatch = Stopwatch.StartNew();
        Logger.Information("Computing missing pre-trips for {Date:yyyy-MM-dd}", day);

        using var context = _contextFactory.CreateDbContext();
        var routes = await context.Routes
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync();

        var schedules = await context.Schedules
            .AsNoTracking()
            .Where(s => s.ScheduleDate >= day && s.ScheduleDate < next)
            .ToListAsync();

        var preTripRows = await context.DutyEvents
            .AsNoTracking()
            .Where(d =>
                d.EventType == DutyEventType.PreTrip
                && d.EventedAt >= day
                && d.EventedAt < next)
            .Select(d => new { d.BusId, d.DriverId, d.Session })
            .ToListAsync();
        var preTripSet = preTripRows
            .Select(d => new PreTripKey(d.BusId, d.DriverId, d.Session))
            .ToHashSet();

        var busIds = routes
            .SelectMany(r => new[] { r.AMVehicleId, r.PMVehicleId })
            .Concat(schedules.Select(s => (int?)s.BusId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var driverIds = routes
            .SelectMany(r => new[] { r.AMDriverId, r.PMDriverId })
            .Concat(schedules.Select(s => (int?)s.DriverId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var buses = await context.Buses
            .AsNoTracking()
            .Where(b => busIds.Contains(b.BusId))
            .ToDictionaryAsync(b => b.BusId, b => b.BusNumber);
        var drivers = await context.Drivers
            .AsNoTracking()
            .Where(d => driverIds.Contains(d.DriverId))
            .ToDictionaryAsync(d => d.DriverId, d => d.DriverName);

        var missing = new List<MissingPreTripRow>();
        foreach (var route in routes)
        {
            // RouteDailyPairing keys off Route.Date for schedule day — NoTracking clone is safe.
            route.Date = day;
            var routeSchedules = schedules.Where(s => s.RouteId == route.RouteId).ToList();

            AddMissingIfNeeded(
                missing, preTripSet, buses, drivers, route, DutySession.AM, RouteTimeSlot.AM, routeSchedules);
            AddMissingIfNeeded(
                missing, preTripSet, buses, drivers, route, DutySession.PM, RouteTimeSlot.PM, routeSchedules);
        }

        stopwatch.Stop();
        Logger.Information(
            "Missing pre-trips for {Date:yyyy-MM-dd}: {Count} in {ElapsedMs}ms",
            day, missing.Count, stopwatch.ElapsedMilliseconds);
        return missing;
    }

    private static void AddMissingIfNeeded(
        List<MissingPreTripRow> missing,
        HashSet<PreTripKey> preTrips,
        IReadOnlyDictionary<int, string> buses,
        IReadOnlyDictionary<int, string> drivers,
        Route route,
        DutySession session,
        RouteTimeSlot slot,
        List<Schedule> routeSchedules)
    {
        var pairing = RouteDailyPairing.Resolve(route, slot, routeSchedules);
        if (pairing.BusId is not int busId || pairing.DriverId is not int driverId)
        {
            return;
        }

        if (preTrips.Contains(new PreTripKey(busId, driverId, session)))
        {
            return;
        }

        buses.TryGetValue(busId, out var busNumber);
        drivers.TryGetValue(driverId, out var driverName);
        missing.Add(new MissingPreTripRow
        {
            RouteId = route.RouteId,
            RouteName = route.RouteName,
            Session = session,
            BusId = busId,
            BusNumber = busNumber ?? busId.ToString(),
            DriverId = driverId,
            DriverName = driverName ?? driverId.ToString()
        });
    }

    private static Maintenance BuildDefectMaintenance(DutyEvent dutyEvent) =>
        new()
        {
            Date = DateTime.SpecifyKind(dutyEvent.EventedAt.Date, DateTimeKind.Utc),
            VehicleId = dutyEvent.BusId,
            OdometerReading = dutyEvent.OdometerReading ?? 0,
            MaintenanceCompleted = DefectWorkLabel,
            Vendor = DefectVendorLabel,
            RepairCost = 0,
            Description = dutyEvent.Notes,
            Notes = dutyEvent.Notes,
            Status = "Scheduled",
            Priority = "High",
            SourceDutyEventId = dutyEvent.DutyEventId,
            CreatedDate = DateTime.UtcNow
        };

    private static DateTime UtcDay(DateTime dateUtc)
    {
        var day = dateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateUtc.Date, DateTimeKind.Utc)
            : dateUtc.ToUniversalTime().Date;
        return DateTime.SpecifyKind(day, DateTimeKind.Utc);
    }

    private static void DetachGraphs(DutyEvent dutyEvent)
    {
        dutyEvent.Driver = null!;
        dutyEvent.Bus = null!;
        dutyEvent.Fuel = null;
    }

    private static string FormatDbError(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? ex.Message;
        if (inner.Contains("foreign key", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("FK_DutyEvents", StringComparison.OrdinalIgnoreCase))
        {
            return "Could not save duty event — the selected driver or bus is missing or invalid.";
        }

        return DatabaseUserMessage.ForOperation(ex, "save duty event");
    }
}
