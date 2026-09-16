using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.RouteDetermination;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

public sealed class PickupStopService : IPickupStopService
{
    private static readonly ILogger Logger = Log.ForContext<PickupStopService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public PickupStopService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<IReadOnlyList<PickupStop>> GetActiveStopsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var list = await context.PickupStops.AsNoTracking()
            .Where(s => s.Active)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Logger.Debug("Loaded {Count} active pickup stops", list.Count);
        return list;
    }

    public async Task<PickupStop?> GetByIdAsync(int pickupStopId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.PickupStops.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PickupStopId == pickupStopId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PickupStop> AddStopAsync(
        string name,
        string? address,
        decimal latitude,
        decimal longitude,
        string stopType = PickupStopTypes.Corner,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureValidatedCoordinates(latitude, longitude);

        await using var context = _contextFactory.CreateWriteDbContext();
        var stop = new PickupStop
        {
            Active = true,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = Environment.UserName
        };
        ApplyCatalogFields(stop, name, address, latitude, longitude, stopType, notes);
        context.PickupStops.Add(stop);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Added pickup stop PickupStopId={Id} Name={Name} Lat={Lat} Lon={Lon} Type={Type}",
            stop.PickupStopId, stop.Name, stop.Latitude, stop.Longitude, stop.StopType);
        return stop;
    }

    public async Task<PickupStop> UpdateStopAsync(
        int pickupStopId,
        string name,
        string? address,
        decimal latitude,
        decimal longitude,
        string stopType = PickupStopTypes.Corner,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureValidatedCoordinates(latitude, longitude);

        await using var context = _contextFactory.CreateWriteDbContext();
        var stop = await context.PickupStops.AsTracking()
            .FirstOrDefaultAsync(s => s.PickupStopId == pickupStopId, cancellationToken)
            .ConfigureAwait(false);
        if (stop is null)
        {
            throw new InvalidOperationException($"Pickup stop {pickupStopId} was not found.");
        }

        ApplyCatalogFields(stop, name, address, latitude, longitude, stopType, notes);
        context.Entry(stop).State = EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Updated pickup stop PickupStopId={Id} Name={Name} Lat={Lat} Lon={Lon} Type={Type}",
            stop.PickupStopId, stop.Name, stop.Latitude, stop.Longitude, stop.StopType);
        return stop;
    }

    public async Task<CatalogDeleteResult> RetireStopAsync(
        int pickupStopId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateWriteDbContext();
        var stop = await context.PickupStops.AsTracking()
            .FirstOrDefaultAsync(s => s.PickupStopId == pickupStopId, cancellationToken)
            .ConfigureAwait(false);
        if (stop is null)
        {
            return CatalogDeleteResult.NotFound;
        }

        var referenced = await PickupStopIsReferencedAsync(context, stop, cancellationToken)
            .ConfigureAwait(false);
        if (referenced)
        {
            stop.Active = false;
            context.Entry(stop).State = EntityState.Modified;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Logger.Warning(
                "Retired pickup stop PickupStopId={Id} Name={Name} because students or published route stops still reference it",
                stop.PickupStopId,
                stop.Name);
            return CatalogDeleteResult.Retired;
        }

        context.PickupStops.Remove(stop);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Warning("Deleted unused pickup stop PickupStopId={Id} Name={Name}", pickupStopId, stop.Name);
        return CatalogDeleteResult.Deleted;
    }

    private static async Task<bool> PickupStopIsReferencedAsync(
        BusBuddyDbContext context,
        PickupStop stop,
        CancellationToken cancellationToken)
    {
        if (await context.Students.AnyAsync(s => s.PickupStopId == stop.PickupStopId, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        // RouteStop is denormalized (no PickupStopId / LocationId). Match the published name
        // the same way school retire matches Route.School / RouteStop.StopName.
        return !string.IsNullOrWhiteSpace(stop.Name)
               && await context.RouteStops.AnyAsync(s => s.StopName == stop.Name, cancellationToken)
                   .ConfigureAwait(false);
    }

    private static void ApplyCatalogFields(
        PickupStop stop,
        string name,
        string? address,
        decimal latitude,
        decimal longitude,
        string? stopType,
        string? notes)
    {
        stop.Name = name.Trim();
        stop.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        stop.Latitude = latitude;
        stop.Longitude = longitude;
        stop.StopType = NormalizeStopType(stopType);
        stop.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private static void EnsureValidatedCoordinates(decimal latitude, decimal longitude)
    {
        if (!LocationCoordinate.IsValidated(latitude, longitude))
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Catalog stops require validated coordinates (not 0,0 or the US centroid).");
        }
    }

    private static string NormalizeStopType(string? stopType)
    {
        if (string.Equals(stopType, PickupStopTypes.RuralHome, StringComparison.OrdinalIgnoreCase))
        {
            // A student home is not a catalog stop unless a clerk publishes it as PickupStop (corner/intersection).
            return PickupStopTypes.Corner;
        }

        return string.IsNullOrWhiteSpace(stopType) ? PickupStopTypes.Corner : stopType.Trim();
    }

    public async Task<PickupStop?> FindNearestAsync(
        double latitude,
        double longitude,
        double maxMeters = 400,
        CancellationToken cancellationToken = default)
    {
        var stops = await GetActiveStopsAsync(cancellationToken).ConfigureAwait(false);
        if (stops.Count == 0)
        {
            return null;
        }

        PickupStop? best = null;
        var bestMeters = double.MaxValue;
        foreach (var stop in stops)
        {
            if (!stop.HasValidatedCoordinates)
            {
                continue;
            }

            var miles = RoutePacker.HaversineMiles(latitude, longitude, (double)stop.Latitude, (double)stop.Longitude);
            var meters = miles * 1609.344;
            if (meters <= maxMeters && meters < bestMeters)
            {
                bestMeters = meters;
                best = stop;
            }
        }

        if (best is not null)
        {
            Logger.Debug(
                "Nearest pickup stop PickupStopId={Id} Name={Name} DistanceM={M:F0}",
                best.PickupStopId, best.Name, bestMeters);
        }

        return best;
    }
}
