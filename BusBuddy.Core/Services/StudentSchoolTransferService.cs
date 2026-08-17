using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Creates/lists school-to-school (inter-district) transfer assignments with timed pickup/dropoff.
/// Required: from/to schools, pickup address, dropoff address, pickup time, dropoff time.
/// </summary>
public interface IStudentSchoolTransferService
{
    Task<StudentSchoolTransfer> AssignTransferAsync(StudentSchoolTransfer transfer, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentSchoolTransfer>> GetActiveTransfersForStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default);
}

public sealed class StudentSchoolTransferService : IStudentSchoolTransferService
{
    private static readonly ILogger Logger = Log.ForContext<StudentSchoolTransferService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IRouteWaypointRebuildService? _waypointRebuild;

    public StudentSchoolTransferService(
        IBusBuddyDbContextFactory contextFactory,
        IRouteWaypointRebuildService? waypointRebuild = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _waypointRebuild = waypointRebuild;
    }

    public async Task<StudentSchoolTransfer> AssignTransferAsync(
        StudentSchoolTransfer transfer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (transfer.StudentId <= 0)
        {
            throw new ArgumentException("StudentId is required", nameof(transfer));
        }

        if (transfer.FromDestinationId <= 0 || transfer.ToDestinationId <= 0)
        {
            throw new ArgumentException("From and To school destinations are required", nameof(transfer));
        }

        if (transfer.FromDestinationId == transfer.ToDestinationId)
        {
            throw new ArgumentException("From and To schools must differ for a transfer", nameof(transfer));
        }

        if (string.IsNullOrWhiteSpace(transfer.PickupAddress))
        {
            throw new ArgumentException("Pickup location is required", nameof(transfer));
        }

        if (string.IsNullOrWhiteSpace(transfer.DropoffAddress))
        {
            throw new ArgumentException("Dropoff location is required", nameof(transfer));
        }

        if (!transfer.PickupTime.HasValue)
        {
            throw new ArgumentException("Requested transfer pickup time is required", nameof(transfer));
        }

        if (!transfer.DropoffTime.HasValue)
        {
            throw new ArgumentException("Requested transfer dropoff time is required", nameof(transfer));
        }

        if (transfer.DropoffTime <= transfer.PickupTime)
        {
            throw new ArgumentException("Dropoff time must be after pickup time", nameof(transfer));
        }

        await using var context = _contextFactory.CreateDbContext();

        var fromSchool = await context.Destinations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DestinationId == transfer.FromDestinationId, cancellationToken)
            .ConfigureAwait(false);
        var toSchool = await context.Destinations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DestinationId == transfer.ToDestinationId, cancellationToken)
            .ConfigureAwait(false);

        if (fromSchool is null || toSchool is null)
        {
            throw new ArgumentException("From or To school destination was not found", nameof(transfer));
        }

        if (!transfer.PickupLatitude.HasValue || !transfer.PickupLongitude.HasValue)
        {
            transfer.PickupLatitude = fromSchool.Latitude;
            transfer.PickupLongitude = fromSchool.Longitude;
        }

        if (!transfer.DropoffLatitude.HasValue || !transfer.DropoffLongitude.HasValue)
        {
            transfer.DropoffLatitude = toSchool.Latitude;
            transfer.DropoffLongitude = toSchool.Longitude;
        }

        transfer.CreatedDate = DateTime.UtcNow;
        transfer.IsActive = true;

        var prior = await context.StudentSchoolTransfers
            .Where(t => t.StudentId == transfer.StudentId && t.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var old in prior)
        {
            old.IsActive = false;
            old.EndDate ??= DateTime.Today;
        }

        context.StudentSchoolTransfers.Add(transfer);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == transfer.StudentId, cancellationToken)
            .ConfigureAwait(false);
        if (student is not null)
        {
            student.DestinationId = toSchool.DestinationId;
            student.School = toSchool.Name;
            student.UpdatedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        Logger.Information(
            "School transfer assigned StudentId={StudentId} From={From} To={To} Pickup={Pickup}@{PickupTime} Dropoff={Dropoff}@{DropoffTime}",
            transfer.StudentId,
            transfer.FromDestinationId,
            transfer.ToDestinationId,
            transfer.PickupAddress,
            transfer.PickupTime,
            transfer.DropoffAddress,
            transfer.DropoffTime);

        if (_waypointRebuild is not null)
        {
            try
            {
                await _waypointRebuild.RebuildForStudentRoutesAsync(transfer.StudentId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Waypoint rebuild after transfer failed StudentId={StudentId}", transfer.StudentId);
            }
        }

        return transfer;
    }

    public async Task<IReadOnlyList<StudentSchoolTransfer>> GetActiveTransfersForStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.StudentSchoolTransfers.AsNoTracking()
            .Include(t => t.FromDestination)
            .Include(t => t.ToDestination)
            .Where(t => t.StudentId == studentId && t.IsActive)
            .OrderByDescending(t => t.EffectiveDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
