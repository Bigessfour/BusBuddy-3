using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>Driver training / CDE compliance sub-module.</summary>
public sealed class DriverTrainingService : IDriverTrainingService
{
    private static readonly ILogger Logger = Log.ForContext<DriverTrainingService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public DriverTrainingService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<IReadOnlyList<DriverTrainingRecord>> GetRecordsForDriverAsync(
        int driverId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.DriverTrainingRecords.AsNoTracking()
            .Where(r => r.DriverId == driverId)
            .OrderBy(r => r.RequirementName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DriverTrainingRecord>> EnsureMatrixChecklistAsync(
        int driverId,
        bool includeOptionalApplicable = false,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateWriteDbContext();
        var driver = await context.Drivers.AsTracking()
            .FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken)
            .ConfigureAwait(false);
        if (driver is null)
        {
            throw new ArgumentException($"Driver {driverId} not found", nameof(driverId));
        }

        var existing = await context.DriverTrainingRecords.AsTracking()
            .Where(r => r.DriverId == driverId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byCode = existing.ToDictionary(r => r.RequirementCode, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (code, name, validityMonths, oftenApplicable) in CdeDriverTrainingCodes.Catalog)
        {
            if (!oftenApplicable && !includeOptionalApplicable)
            {
                continue;
            }

            if (byCode.ContainsKey(code))
            {
                continue;
            }

            context.DriverTrainingRecords.Add(new DriverTrainingRecord
            {
                DriverId = driverId,
                RequirementCode = code,
                RequirementName = name,
                IsRequired = oftenApplicable,
                IsApplicable = oftenApplicable || includeOptionalApplicable,
                CreatedDate = DateTime.UtcNow
            });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Logger.Information("Seeded {Count} CDE training checklist rows for DriverId={DriverId}", added, driverId);
        }

        return await GetRecordsForDriverAsync(driverId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DriverTrainingRecord> UpsertCompletionAsync(
        int driverId,
        string requirementCode,
        DateTime completedDate,
        DateTime? expiryDate = null,
        string? certificateOrReference = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requirementCode))
        {
            throw new ArgumentException("Requirement code is required", nameof(requirementCode));
        }

        // Write path must track: BusBuddyDbContext defaults to NoTracking.
        await using var context = _contextFactory.CreateWriteDbContext();
        var catalogItem = CdeDriverTrainingCodes.Catalog
            .FirstOrDefault(c => string.Equals(c.Code, requirementCode, StringComparison.OrdinalIgnoreCase));
        var hasCatalog = !string.IsNullOrEmpty(catalogItem.Code);

        var record = await context.DriverTrainingRecords.AsTracking()
            .FirstOrDefaultAsync(
                r => r.DriverId == driverId && r.RequirementCode == requirementCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            record = new DriverTrainingRecord
            {
                DriverId = driverId,
                RequirementCode = requirementCode,
                RequirementName = hasCatalog ? catalogItem.DisplayName : requirementCode,
                IsRequired = hasCatalog && catalogItem.OftenApplicable,
                IsApplicable = true,
                CreatedDate = DateTime.UtcNow
            };
            context.DriverTrainingRecords.Add(record);
        }

        record.CompletedDate = completedDate.Date;
        record.ExpiryDate = expiryDate?.Date
            ?? (hasCatalog && catalogItem.DefaultValidityMonths is int months
                ? completedDate.Date.AddMonths(months)
                : null);
        record.CertificateOrReference = certificateOrReference;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            record.Notes = notes;
        }

        record.UpdatedDate = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RefreshTrainingCompleteFlagAsync(driverId, cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Training upserted DriverId={DriverId} Code={Code} Completed={Completed} Expiry={Expiry}",
            driverId, requirementCode, record.CompletedDate, record.ExpiryDate);

        return record;
    }

    public async Task<bool> RefreshTrainingCompleteFlagAsync(
        int driverId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateWriteDbContext();
        var driver = await context.Drivers.AsTracking()
            .FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken)
            .ConfigureAwait(false);
        if (driver is null)
        {
            return false;
        }

        var required = await context.DriverTrainingRecords
            .Where(r => r.DriverId == driverId && r.IsRequired && r.IsApplicable)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var complete = required.Count > 0 &&
                       required.All(r => r.CompletedDate.HasValue &&
                                         (!r.ExpiryDate.HasValue || r.ExpiryDate.Value.Date >= DateTime.Today));

        if (driver.TrainingComplete != complete)
        {
            driver.TrainingComplete = complete;
            driver.UpdatedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Logger.Information("DriverId={DriverId} TrainingComplete={Complete}", driverId, complete);
        }

        return complete;
    }
}
