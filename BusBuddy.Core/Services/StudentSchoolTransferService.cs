using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Creates/lists school-to-school (inter-district) transfer assignments with timed pickup/dropoff.
/// Route planning can later use these as waypoints between FromDestination and ToDestination.
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

    public StudentSchoolTransferService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
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

        await using var context = _contextFactory.CreateDbContext();
        transfer.CreatedDate = DateTime.UtcNow;
        transfer.IsActive = true;
        context.StudentSchoolTransfers.Add(transfer);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Keep attending campus aligned with receiving school when DestinationId is set.
        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == transfer.StudentId, cancellationToken)
            .ConfigureAwait(false);
        if (student is not null)
        {
            var toSchool = await context.Destinations.FirstOrDefaultAsync(
                d => d.DestinationId == transfer.ToDestinationId, cancellationToken).ConfigureAwait(false);
            if (toSchool is not null)
            {
                student.DestinationId = toSchool.DestinationId;
                student.School = toSchool.Name;
                student.UpdatedDate = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        Logger.Information(
            "School transfer assigned StudentId={StudentId} From={From} To={To} Pickup={Pickup} Dropoff={Dropoff}",
            transfer.StudentId,
            transfer.FromDestinationId,
            transfer.ToDestinationId,
            transfer.PickupTime,
            transfer.DropoffTime);

        return transfer;
    }

    public async Task<IReadOnlyList<StudentSchoolTransfer>> GetActiveTransfersForStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.StudentSchoolTransfers.AsNoTracking()
            .Where(t => t.StudentId == studentId && t.IsActive)
            .OrderByDescending(t => t.EffectiveDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
