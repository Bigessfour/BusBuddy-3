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
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        var normalizedType = string.IsNullOrWhiteSpace(stopType) ? PickupStopTypes.Corner : stopType.Trim();
        await using var context = _contextFactory.CreateWriteDbContext();
        var stop = new PickupStop
        {
            Name = name.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            StopType = normalizedType,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Active = true,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = Environment.UserName
        };
        context.PickupStops.Add(stop);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Added pickup stop PickupStopId={Id} Name={Name} Lat={Lat} Lon={Lon} Type={Type}",
            stop.PickupStopId, stop.Name, stop.Latitude, stop.Longitude, stop.StopType);
        return stop;
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
