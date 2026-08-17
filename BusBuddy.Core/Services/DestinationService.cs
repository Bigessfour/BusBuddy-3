using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
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

    public async Task EnsureDefaultSchoolsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var exists = await context.Destinations.AnyAsync(
            d => d.DestinationType == DestinationTypes.School &&
                 d.Name == WileyMapDefaults.SchoolLabel &&
                 !d.IsDeleted,
            cancellationToken).ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        context.Destinations.Add(new Destination
        {
            Name = WileyMapDefaults.SchoolLabel,
            Address = "510 Ward St",
            City = "Wiley",
            State = "CO",
            ZipCode = "81092",
            DestinationType = DestinationTypes.School,
            IsActive = true,
            Latitude = (decimal)WileyMapDefaults.SchoolLatitude,
            Longitude = (decimal)WileyMapDefaults.SchoolLongitude,
            DistrictName = "Wiley RE-13JT",
            GradeMin = "Pre-K",
            GradeMax = "12",
            AgeMinYears = 4,
            AgeMaxYears = 18,
            ContactName = "Main Office",
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information("Seeded default school destination {Name}", WileyMapDefaults.SchoolLabel);
    }

    public async Task<IReadOnlyList<Destination>> GetActiveSchoolsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultSchoolsAsync(cancellationToken).ConfigureAwait(false);
        return await GetActiveDestinationsAsync(DestinationTypes.School, cancellationToken).ConfigureAwait(false);
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
}
