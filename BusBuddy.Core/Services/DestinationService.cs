using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

public sealed class DestinationService : IDestinationService
{
    private static readonly ILogger Logger = Log.ForContext<DestinationService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public DestinationService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public Task EnsureDefaultSchoolsAsync(CancellationToken cancellationToken = default)
    {
        Logger.Debug("No default school is seeded; add schools through Destinations");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Destination>> GetActiveSchoolsAsync(CancellationToken cancellationToken = default) =>
        GetActiveDestinationsAsync(DestinationTypes.School, cancellationToken);

    public async Task<IReadOnlyList<Destination>> GetRosterSchoolsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var referencedIds = await context.Students.AsNoTracking()
            .Where(s => s.DestinationId != null)
            .Select(s => s.DestinationId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var list = await context.Destinations.AsNoTracking()
            .Where(d => !d.IsDeleted && d.DestinationType == DestinationTypes.School
                        && (d.IsActive || referencedIds.Contains(d.DestinationId)))
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return list;
    }

    public async Task<IReadOnlyList<Destination>> GetActiveDestinationsAsync(
        string? destinationType = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var query = context.Destinations.AsNoTracking()
            .Where(d => d.IsActive && !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(destinationType))
        {
            query = query.Where(d => d.DestinationType == destinationType);
        }

        var list = await query.OrderBy(d => d.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        Logger.Debug("Loaded {Count} destinations Type={Type}", list.Count, destinationType ?? "*");
        return list;
    }

    public async Task<Destination?> GetByIdAsync(int destinationId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Destinations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DestinationId == destinationId && !d.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> UpdateSchoolTimesAsync(
        int destinationId,
        TimeSpan? startTime,
        TimeSpan? dismissalTime,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateWriteDbContext();
        var dest = await context.Destinations.AsTracking()
            .FirstOrDefaultAsync(d => d.DestinationId == destinationId && !d.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (dest is null)
        {
            return false;
        }

        dest.StartTime = startTime;
        dest.DismissalTime = dismissalTime;
        dest.UpdatedDate = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Updated school times DestinationId={Id} Start={Start} Dismissal={Dismissal}",
            destinationId, startTime, dismissalTime);
        return true;
    }

    public async Task<Destination> AddSchoolAsync(
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        TimeSpan startTime,
        TimeSpan dismissalTime,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSchoolFields(name, address, city, state, zipCode, startTime, dismissalTime);

        await using var context = _contextFactory.CreateWriteDbContext();
        var trimmedName = name.Trim();
        await EnsureUniqueSchoolNameAsync(context, trimmedName, excludeDestinationId: null, cancellationToken)
            .ConfigureAwait(false);

        var dest = new Destination
        {
            Name = trimmedName,
            Address = address.Trim(),
            City = city.Trim(),
            State = state.Trim().ToUpperInvariant(),
            ZipCode = zipCode.Trim(),
            DestinationType = DestinationTypes.School,
            StartTime = startTime,
            DismissalTime = dismissalTime,
            Latitude = LocationCoordinate.IsValidated(latitude, longitude) ? latitude : null,
            Longitude = LocationCoordinate.IsValidated(latitude, longitude) ? longitude : null,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
            CreatedBy = "Clerk"
        };
        context.Destinations.Add(dest);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Added school DestinationId={Id} Name={Name} Start={Start} Dismissal={Dismissal}",
            dest.DestinationId, dest.Name, startTime, dismissalTime);
        return dest;
    }

    public async Task<Destination> UpdateSchoolAsync(
        int destinationId,
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        TimeSpan startTime,
        TimeSpan dismissalTime,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSchoolFields(name, address, city, state, zipCode, startTime, dismissalTime);

        await using var context = _contextFactory.CreateWriteDbContext();
        var dest = await context.Destinations.AsTracking()
            .FirstOrDefaultAsync(
                d => d.DestinationId == destinationId && !d.IsDeleted && d.DestinationType == DestinationTypes.School,
                cancellationToken)
            .ConfigureAwait(false);
        if (dest is null)
        {
            throw new InvalidOperationException($"School {destinationId} was not found.");
        }

        var trimmedName = name.Trim();
        await EnsureUniqueSchoolNameAsync(context, trimmedName, destinationId, cancellationToken)
            .ConfigureAwait(false);

        dest.Name = trimmedName;
        dest.Address = address.Trim();
        dest.City = city.Trim();
        dest.State = state.Trim().ToUpperInvariant();
        dest.ZipCode = zipCode.Trim();
        dest.StartTime = startTime;
        dest.DismissalTime = dismissalTime;
        dest.Latitude = LocationCoordinate.IsValidated(latitude, longitude) ? latitude : null;
        dest.Longitude = LocationCoordinate.IsValidated(latitude, longitude) ? longitude : null;
        dest.UpdatedDate = DateTime.UtcNow;
        dest.UpdatedBy = "Clerk";

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Updated school DestinationId={Id} Name={Name} Start={Start} Dismissal={Dismissal} HasGps={HasGps}",
            dest.DestinationId,
            dest.Name,
            startTime,
            dismissalTime,
            dest.Latitude.HasValue);
        return dest;
    }

    public async Task<SchoolDeleteResult> DeleteSchoolAsync(
        int destinationId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateWriteDbContext();
        var dest = await context.Destinations.AsTracking()
            .FirstOrDefaultAsync(
                d => d.DestinationId == destinationId && !d.IsDeleted && d.DestinationType == DestinationTypes.School,
                cancellationToken)
            .ConfigureAwait(false);
        if (dest is null)
        {
            return SchoolDeleteResult.NotFound;
        }

        var referenced = await SchoolIsReferencedAsync(context, dest, cancellationToken)
            .ConfigureAwait(false);
        if (referenced)
        {
            dest.IsActive = false;
            dest.UpdatedDate = DateTime.UtcNow;
            dest.UpdatedBy = "Clerk";
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Logger.Warning(
                "Retired school DestinationId={Id} Name={Name} because students, trips, or routes still reference it",
                dest.DestinationId,
                dest.Name);
            return SchoolDeleteResult.Retired;
        }

        context.Destinations.Remove(dest);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Warning("Deleted unused school DestinationId={Id} Name={Name}", destinationId, dest.Name);
        return SchoolDeleteResult.Deleted;
    }

    private static void ValidateSchoolFields(
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        TimeSpan startTime,
        TimeSpan dismissalTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode);
        if (state.Trim().Length != 2)
        {
            throw new ArgumentException("State must be a 2-letter abbreviation.", nameof(state));
        }

        if (dismissalTime <= startTime)
        {
            throw new ArgumentException("Dismissal time must be after start time.", nameof(dismissalTime));
        }
    }

    private static async Task EnsureUniqueSchoolNameAsync(
        BusBuddyDbContext context,
        string trimmedName,
        int? excludeDestinationId,
        CancellationToken cancellationToken)
    {
        var exists = await context.Destinations.AsNoTracking()
            .AnyAsync(
                d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School
                     && d.Name == trimmedName
                     && (!excludeDestinationId.HasValue || d.DestinationId != excludeDestinationId.Value),
                cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"A school named '{trimmedName}' is already in the catalog.");
        }
    }

    private static async Task<bool> SchoolIsReferencedAsync(
        BusBuddyDbContext context,
        Destination dest,
        CancellationToken cancellationToken)
    {
        var destinationId = dest.DestinationId;
        if (await context.Students.AnyAsync(s => s.DestinationId == destinationId, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (await context.StudentSchoolTransfers.AnyAsync(
                t => t.FromDestinationId == destinationId || t.ToDestinationId == destinationId,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await context.Activities.AnyAsync(a => a.DestinationId == destinationId, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (await context.TripEvents.AnyAsync(
                t => t.OriginLocationId == destinationId || t.DestinationLocationId == destinationId,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        // specs/locations.md: do not delete if published routes still use the campus.
        if (!string.IsNullOrWhiteSpace(dest.Name)
            && await context.Routes.AnyAsync(r => r.School == dest.Name, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(dest.Name)
               && await context.RouteStops.AnyAsync(s => s.StopName == dest.Name, cancellationToken)
                   .ConfigureAwait(false);
    }

    public async Task<Destination> AddTripDestinationAsync(
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode);
        if (LocationTypes.IsUnresolvedPlaceName(name))
        {
            throw new ArgumentException(
                "See Trip Notes / TBD is not a location. Keep DestinationName on the trip until a clerk validates a place.",
                nameof(name));
        }

        if (!LocationCoordinate.IsValidated(latitude, longitude))
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Trip destinations require Google-validated coordinates before they can be a pin or Confirmed place.");
        }

        await using var context = _contextFactory.CreateWriteDbContext();
        var dest = new Destination
        {
            Name = name.Trim(),
            Address = address.Trim(),
            City = city.Trim(),
            State = state.Trim().ToUpperInvariant(),
            ZipCode = zipCode.Trim(),
            DestinationType = DestinationTypes.TripDestination,
            Latitude = latitude,
            Longitude = longitude,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
            CreatedBy = "Clerk"
        };
        context.Destinations.Add(dest);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Added trip destination DestinationId={Id} Name={Name} Type={Type}",
            dest.DestinationId, dest.Name, dest.LocationType);
        return dest;
    }

    public async Task<Destination?> FindValidatedPlaceByNameAsync(
        string? name,
        CancellationToken cancellationToken = default)
    {
        if (LocationTypes.IsUnresolvedPlaceName(name))
        {
            return null;
        }

        var trimmed = name!.Trim();
        await using var context = _contextFactory.CreateDbContext();
        var candidates = await context.Destinations.AsNoTracking()
            .Where(d => d.IsActive && !d.IsDeleted && d.Name == trimmed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates.FirstOrDefault(d =>
            d.HasValidatedCoordinates && LocationTypes.CanBeTripPlace(d.DestinationType));
    }
}
