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

        await using var context = _contextFactory.CreateWriteDbContext();
        var trimmedName = name.Trim();
        var exists = await context.Destinations.AsNoTracking()
            .AnyAsync(
                d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School
                     && d.Name == trimmedName,
                cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"A school named '{trimmedName}' is already in the catalog.");
        }

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
            "Added school DestinationId={Id} Name={Name} Start={Start} Dismissal={Dismissal}",
            dest.DestinationId, dest.Name, startTime, dismissalTime);
        return dest;
    }
}
