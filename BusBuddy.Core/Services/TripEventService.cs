using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.Trips;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Clerk trip board on Core <see cref="TripEvent"/>. Route != Trip: never sets RouteId, never clones Route.
/// </summary>
public sealed class TripEventService : ITripEventService
{
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IRoutingService? _routingService;
    private static readonly ILogger Logger = Log.ForContext<TripEventService>();

    public TripEventService(
        IBusBuddyDbContextFactory contextFactory,
        IRoutingService? routingService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _routingService = routingService;
    }

    public async Task<IEnumerable<TripEvent>> GetAllTripsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.TripEvents
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.OriginLocation)
            .Include(t => t.DestinationLocation)
            .OrderBy(t => t.TripDate)
            .ThenBy(t => t.PickupTime)
            .ToListAsync();
    }

    public async Task<TripEvent?> GetTripByIdAsync(int id)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.TripEvents
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.OriginLocation)
            .Include(t => t.DestinationLocation)
            .FirstOrDefaultAsync(t => t.TripEventId == id);
    }

    public async Task<IEnumerable<TripEvent>> GetTripsByTypeAsync(TripType tripType)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.TripEvents
            .AsNoTracking()
            .Where(t => t.Type == tripType)
            .OrderBy(t => t.TripDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TripEvent>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.TripEvents
            .AsNoTracking()
            .Where(t => t.TripDate >= startDate.Date && t.TripDate <= endDate.Date)
            .OrderBy(t => t.TripDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TripEvent>> GetUnassignedTripsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.TripEvents
            .AsNoTracking()
            .Where(t => t.Status == TripStatus.MissingInfo || t.Status == TripStatus.Draft)
            .OrderBy(t => t.TripDate)
            .ToListAsync();
    }

    public async Task AddTripAsync(TripEvent tripEvent)
    {
        ArgumentNullException.ThrowIfNull(tripEvent);
        tripEvent.RouteId = null;
        tripEvent.POCName ??= string.Empty;
        ApplyLeaveReturn(tripEvent);
        if (string.IsNullOrWhiteSpace(tripEvent.Status))
        {
            tripEvent.Status = TripEvent.InferBoardStatus(tripEvent);
        }

        using var context = _contextFactory.CreateWriteDbContext();
        context.TripEvents.Add(tripEvent);
        await context.SaveChangesAsync();
    }

    public async Task UpdateTripAsync(TripEvent tripEvent)
    {
        ArgumentNullException.ThrowIfNull(tripEvent);
        tripEvent.RouteId = null;
        ApplyLeaveReturn(tripEvent);
        tripEvent.UpdatedDate = DateTime.UtcNow;

        using var context = _contextFactory.CreateWriteDbContext();
        context.TripEvents.Update(tripEvent);
        await context.SaveChangesAsync();
    }

    public async Task DeleteTripAsync(int id)
    {
        using var context = _contextFactory.CreateWriteDbContext();
        var trip = await context.TripEvents.FindAsync(id);
        if (trip is null)
        {
            return;
        }

        context.TripEvents.Remove(trip);
        await context.SaveChangesAsync();
    }

    public async Task<bool> HasConflictsAsync(
        int? vehicleId,
        int? driverId,
        DateTime startTime,
        DateTime endTime,
        int? excludeTripId = null)
    {
        var conflicts = await GetConflictingTripsAsync(vehicleId, driverId, startTime, endTime);
        return conflicts.Any(t => !excludeTripId.HasValue || t.TripEventId != excludeTripId.Value);
    }

    public async Task<IEnumerable<TripEvent>> GetConflictingTripsAsync(
        int? vehicleId,
        int? driverId,
        DateTime startTime,
        DateTime endTime)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.TripEvents.AsNoTracking().AsQueryable();
        if (vehicleId.HasValue)
        {
            query = query.Where(t => t.VehicleId == vehicleId);
        }

        if (driverId.HasValue)
        {
            query = query.Where(t => t.DriverId == driverId);
        }

        return await query
            .Where(t => t.LeaveTime < endTime && (t.ReturnTime ?? t.LeaveTime.AddHours(4)) > startTime)
            .Where(t => t.Status != TripStatus.Cancelled)
            .ToListAsync();
    }

    public async Task<TripBoardImportResult> ImportBoardCsvAsync(string csv, CancellationToken cancellationToken = default)
    {
        var parsed = TripBoardCsvParser.Parse(csv);
        var warnings = new List<string>();
        var created = 0;
        var updated = 0;
        var missingInfo = 0;
        var oos = new HashSet<string>(parsed.OutOfServiceBusNumbers, StringComparer.OrdinalIgnoreCase);

        using var context = _contextFactory.CreateWriteDbContext();
        var buses = await context.Buses.ToListAsync(cancellationToken);
        var drivers = await context.Drivers.ToListAsync(cancellationToken);

        foreach (var row in parsed.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.ExternalTicketNo) && row.TripDate is null)
            {
                continue;
            }

            var ticket = row.ExternalTicketNo;
            TripEvent? trip = null;
            if (!string.IsNullOrEmpty(ticket))
            {
                trip = await context.TripEvents
                    .FirstOrDefaultAsync(t => t.ExternalTicketNo == ticket, cancellationToken);
            }

            var isNew = trip is null;
            trip ??= new TripEvent
            {
                Type = InferTripType(row.GroupOrActivity),
                POCName = string.Empty,
                CreatedDate = DateTime.UtcNow
            };

            var previousStatus = TripStatus.Normalize(trip.Status);
            var previousBoard = isNew ? (BoardSnapshot?)null : Snapshot(trip);
            ApplyRow(trip, row, buses, drivers, oos, warnings);
            trip.RouteId = null;
            await AttachValidatedTripPlaceAsync(context, trip, cancellationToken);

            var inferred = TripEvent.InferBoardStatus(trip);
            if (IsTerminal(previousStatus))
            {
                trip.Status = previousStatus;
            }
            else if (inferred == TripStatus.MissingInfo)
            {
                trip.Status = TripStatus.MissingInfo;
            }
            else if (IsConfirmedFamily(previousStatus)
                && previousBoard is BoardSnapshot before
                && HasMaterialChange(before, trip))
            {
                trip.Status = TripStatus.Changed;
            }
            else if (IsConfirmedFamily(previousStatus))
            {
                trip.Status = previousStatus;
            }
            else
            {
                trip.Status = inferred;
            }

            if (trip.Status == TripStatus.MissingInfo)
            {
                missingInfo++;
            }

            if (isNew)
            {
                context.TripEvents.Add(trip);
                created++;
            }
            else
            {
                trip.UpdatedDate = DateTime.UtcNow;
                updated++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        LinkRelatedTrips(context, parsed.Rows);
        await context.SaveChangesAsync(cancellationToken);

        Logger.Information(
            "Trip board import upserted {Created} new and {Updated} existing trips",
            created,
            updated);

        return new TripBoardImportResult
        {
            Created = created,
            Updated = updated,
            Upserted = created + updated,
            MissingInfo = missingInfo,
            Warnings = warnings
        };
    }

    public async Task<Result> ConfirmTripAsync(int tripEventId, CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateWriteDbContext();
        var trip = await context.TripEvents
            .Include(t => t.DestinationLocation)
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t => t.TripEventId == tripEventId, cancellationToken);

        if (trip is null)
        {
            return Result.Failure("Trip not found.");
        }

        if (!trip.HasValidatedDestination)
        {
            return Result.Failure("Confirmed requires a validated destination.");
        }

        if (!trip.HasTimes || (!trip.ReturnClockTime.HasValue && !trip.ReturnTime.HasValue))
        {
            return Result.Failure("Confirmed requires pickup and return times.");
        }

        if (!trip.DriverId.HasValue)
        {
            return Result.Failure("Confirmed requires an assigned driver.");
        }

        if (trip.IsMultiAsset)
        {
            return Result.Failure("Multi-asset trips cannot be confirmed as a single bus. Split assets first.");
        }

        if (!trip.VehicleId.HasValue)
        {
            return Result.Failure("Confirmed requires an assigned bus.");
        }

        if (trip.Vehicle is not null && !trip.Vehicle.IsAvailable)
        {
            return Result.Failure($"Bus {trip.Vehicle.BusNumber} is not available (Out of Service).");
        }

        trip.Status = TripStatus.Confirmed;
        trip.UpdatedDate = DateTime.UtcNow;
        context.Entry(trip).Property(t => t.Status).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task RefreshPathMilesAsync(int tripEventId, CancellationToken cancellationToken = default)
    {
        if (_routingService is null)
        {
            return;
        }

        using var context = _contextFactory.CreateWriteDbContext();
        var trip = await context.TripEvents
            .Include(t => t.OriginLocation)
            .Include(t => t.DestinationLocation)
            .FirstOrDefaultAsync(t => t.TripEventId == tripEventId, cancellationToken);

        if (trip is null || !trip.HasValidatedOrigin || !trip.HasValidatedDestination)
        {
            return;
        }

        var origin = (
            (double)trip.OriginLocation!.Latitude!.Value,
            (double)trip.OriginLocation.Longitude!.Value);
        var dest = (
            (double)trip.DestinationLocation!.Latitude!.Value,
            (double)trip.DestinationLocation.Longitude!.Value);

        var path = await _routingService.ComputeDrivePathAsync(
            origin,
            dest,
            Array.Empty<(double, double)>(),
            cancellationToken);

        if (path.DistanceMeters.GetValueOrDefault() > 0)
        {
            var miles = Math.Round(path.DistanceMeters!.Value / 1609.344m, 2);
            trip.PathMiles = miles;
            trip.UpdatedDate = DateTime.UtcNow;
            context.Entry(trip).Property(t => t.PathMiles).IsModified = true;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ApplyRow(
        TripEvent trip,
        TripBoardRow row,
        IReadOnlyList<Bus> buses,
        IReadOnlyList<Driver> drivers,
        ISet<string> outOfService,
        ICollection<string> warnings)
    {
        trip.ExternalTicketNo = row.ExternalTicketNo;
        trip.TripDate = row.TripDate?.Date ?? trip.TripDate;
        if (trip.TripDate == default && row.TripDate is DateTime d)
        {
            trip.TripDate = d.Date;
        }

        trip.SchoolYear = SchoolYearFrom(trip.TripDate == default ? DateTime.Today : trip.TripDate);
        trip.RequestingSchool = row.RequestingSchool;
        trip.GroupOrActivity = row.GroupOrActivity;
        trip.DestinationName = row.DestinationName;
        trip.Destination = row.DestinationName;
        trip.OriginName = row.OriginName;
        trip.PickupTime = row.PickupTime;
        trip.ReturnClockTime = row.ReturnClockTime;
        trip.ReturnIsNextDay = row.ReturnIsNextDay;
        trip.PlannedHeadcount = row.PlannedHeadcount;
        trip.ActualHeadcount = row.ActualHeadcount;
        trip.PlannedMiles = row.PlannedMiles;
        trip.TripNotes = row.Notes;
        trip.IsOvernightPending = row.IsOvernightPending;
        trip.IsMultiAsset = row.IsMultiAsset;
        trip.AssignedBusNumber = row.IsMultiAsset ? null : row.BusNumber;
        trip.StudentCount = row.PlannedHeadcount ?? trip.StudentCount;
        trip.CustomType = row.GroupOrActivity;
        ApplyLeaveReturn(trip);

        if (LocationTypes.IsUnresolvedPlaceName(trip.DestinationName))
        {
            trip.DestinationLocationId = null;
        }

        if (row.IsMultiAsset)
        {
            trip.VehicleId = null;
            warnings.Add(
                $"Ticket {row.ExternalTicketNo}: 'All buses and SPED' is multi-asset; BusId was not set.");
        }
        else if (!string.IsNullOrWhiteSpace(row.BusNumber))
        {
            if (IsOutOfService(row.BusNumber, outOfService))
            {
                trip.VehicleId = null;
                warnings.Add(
                    $"Ticket {row.ExternalTicketNo}: bus {row.BusNumber} is Out of Service and was not assigned.");
            }
            else
            {
                var bus = ResolveBus(buses, row.BusNumber);
                if (bus is null)
                {
                    trip.VehicleId = null;
                    warnings.Add(
                        $"Ticket {row.ExternalTicketNo}: bus '{row.BusNumber}' was not found in the fleet.");
                }
                else
                {
                    trip.VehicleId = bus.BusId;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(row.DriverName))
        {
            var driver = ResolveDriver(drivers, row.DriverName);
            if (driver is null)
            {
                trip.DriverId = null;
                warnings.Add(
                    $"Ticket {row.ExternalTicketNo}: driver '{row.DriverName}' was not found.");
            }
            else
            {
                trip.DriverId = driver.DriverId;
            }
        }
    }

    private static void LinkRelatedTrips(BusBuddyDbContext context, IReadOnlyList<TripBoardRow> rows)
    {
        var byTicket = new Dictionary<string, TripEvent>(StringComparer.OrdinalIgnoreCase);
        foreach (var trip in context.ChangeTracker.Entries<TripEvent>().Select(e => e.Entity))
        {
            if (!string.IsNullOrEmpty(trip.ExternalTicketNo))
            {
                byTicket[trip.ExternalTicketNo] = trip;
            }
        }

        foreach (var row in rows.Where(r => !string.IsNullOrEmpty(r.LinkedTicketNo)))
        {
            if (string.IsNullOrEmpty(row.ExternalTicketNo))
            {
                continue;
            }

            if (byTicket.TryGetValue(row.ExternalTicketNo, out var child)
                && byTicket.TryGetValue(row.LinkedTicketNo!, out var parent)
                && parent.TripEventId != 0)
            {
                child.LinkedTripId = parent.TripEventId;
            }
        }

        foreach (var row in rows.Where(r => r.LinkBothTeams && r.TripDate is not null))
        {
            if (string.IsNullOrEmpty(row.ExternalTicketNo)
                || !byTicket.TryGetValue(row.ExternalTicketNo, out var self))
            {
                continue;
            }

            var partner = byTicket.Values.FirstOrDefault(t =>
                t.TripEventId != self.TripEventId
                && t.TripDate.Date == row.TripDate!.Value.Date
                && string.Equals(t.DestinationName, row.DestinationName, StringComparison.OrdinalIgnoreCase));
            if (partner is not null)
            {
                self.LinkedTripId ??= partner.TripEventId;
                partner.LinkedTripId ??= self.TripEventId;
            }
        }
    }

    private static async Task AttachValidatedTripPlaceAsync(
        BusBuddyDbContext context,
        TripEvent trip,
        CancellationToken cancellationToken)
    {
        trip.DestinationLocationId = null;
        trip.DestinationLocation = null;
        var tracked = context.Entry(trip);
        if (tracked.State != EntityState.Detached)
        {
            tracked.Property(t => t.DestinationLocationId).CurrentValue = null;
            tracked.Property(t => t.DestinationLocationId).IsModified = true;
        }

        var name = string.IsNullOrWhiteSpace(trip.DestinationName) ? trip.Destination : trip.DestinationName;
        if (string.IsNullOrWhiteSpace(name) || LocationTypes.IsUnresolvedPlaceName(name))
        {
            return;
        }

        var candidates = await context.Destinations
            .AsNoTracking()
            .Where(d => d.IsActive && !d.IsDeleted)
            .ToListAsync(cancellationToken);
        var place = candidates.FirstOrDefault(d =>
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)
            && d.HasValidatedCoordinates
            && LocationTypes.CanBeTripPlace(d.DestinationType));
        if (place is not null)
        {
            trip.DestinationLocationId = place.DestinationId;
        }
    }

    internal static string SchoolYearFrom(DateTime date) =>
        date.Month >= 7 ? $"{date.Year}-{date.Year + 1}" : $"{date.Year - 1}-{date.Year}";

    internal static void ApplyLeaveReturn(TripEvent trip)
    {
        var date = trip.TripDate == default ? trip.LeaveTime.Date : trip.TripDate.Date;
        trip.TripDate = date;
        trip.LeaveTime = date + (trip.PickupTime ?? TimeSpan.Zero);
        if (trip.ReturnClockTime.HasValue)
        {
            var ret = date + trip.ReturnClockTime.Value;
            if (trip.ReturnIsNextDay)
            {
                ret = ret.AddDays(1);
            }

            trip.ReturnTime = ret;
        }
    }

    internal static Bus? ResolveBus(IEnumerable<Bus> buses, string busNumber)
    {
        var key = NormalizeBusNumber(busNumber);
        return buses.FirstOrDefault(b => NormalizeBusNumber(b.BusNumber) == key);
    }

    internal static Driver? ResolveDriver(IEnumerable<Driver> drivers, string name)
    {
        var trimmed = name.Trim();
        var exact = drivers.FirstOrDefault(d =>
            d.DriverName.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var last = LastToken(trimmed);
        return drivers.FirstOrDefault(d =>
            LastToken(d.DriverName).Equals(last, StringComparison.OrdinalIgnoreCase));
    }

    internal static string NormalizeBusNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            number.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    private static bool IsOutOfService(string busNumber, ISet<string> outOfService)
    {
        var key = NormalizeBusNumber(busNumber);
        return outOfService.Any(o => NormalizeBusNumber(o) == key);
    }

    private static string LastToken(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? name : parts[^1];
    }

    private static TripType InferTripType(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return TripType.Field;
        }

        if (group.Contains("Football", StringComparison.OrdinalIgnoreCase))
        {
            return group.Contains("JH", StringComparison.OrdinalIgnoreCase) || group.Contains("MS", StringComparison.OrdinalIgnoreCase)
                ? TripType.Athletic_JH_Football
                : TripType.Athletic_Football;
        }

        if (group.Contains("Volleyball", StringComparison.OrdinalIgnoreCase))
        {
            return TripType.Athletic_Volleyball;
        }

        if (group.Contains("Softball", StringComparison.OrdinalIgnoreCase))
        {
            return TripType.Athletic_Softball;
        }

        if (group.Contains("Basketball", StringComparison.OrdinalIgnoreCase))
        {
            return TripType.Athletic_Basketball;
        }

        if (group.Contains("Field Trip", StringComparison.OrdinalIgnoreCase))
        {
            return TripType.Field;
        }

        return TripType.Custom;
    }

    private static bool IsTerminal(string? status) =>
        status is TripStatus.Cancelled or TripStatus.Completed;

    private static bool IsConfirmedFamily(string? status) =>
        status is TripStatus.Confirmed or TripStatus.Changed;

    private static bool HasMaterialChange(BoardSnapshot before, TripEvent after) =>
        before != Snapshot(after);

    private static BoardSnapshot Snapshot(TripEvent trip) => new(
        trip.TripDate.Date,
        Norm(trip.DestinationName) ?? Norm(trip.Destination),
        Norm(trip.OriginName),
        trip.PickupTime,
        trip.ReturnClockTime,
        trip.ReturnIsNextDay,
        trip.PlannedHeadcount,
        trip.VehicleId,
        trip.DriverId,
        Norm(trip.AssignedBusNumber),
        trip.IsMultiAsset,
        Norm(trip.GroupOrActivity),
        Norm(trip.RequestingSchool));

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private readonly record struct BoardSnapshot(
        DateTime TripDate,
        string? DestinationName,
        string? OriginName,
        TimeSpan? PickupTime,
        TimeSpan? ReturnClockTime,
        bool ReturnIsNextDay,
        int? PlannedHeadcount,
        int? VehicleId,
        int? DriverId,
        string? AssignedBusNumber,
        bool IsMultiAsset,
        string? GroupOrActivity,
        string? RequestingSchool);
}
