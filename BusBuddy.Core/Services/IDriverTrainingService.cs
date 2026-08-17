using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

public interface IDriverTrainingService
{
    Task<IReadOnlyList<DriverTrainingRecord>> GetRecordsForDriverAsync(
        int driverId,
        CancellationToken cancellationToken = default);

    /// <summary>Ensure CDE matrix checklist rows exist for the driver (idempotent by RequirementCode).</summary>
    Task<IReadOnlyList<DriverTrainingRecord>> EnsureMatrixChecklistAsync(
        int driverId,
        bool includeOptionalApplicable = false,
        CancellationToken cancellationToken = default);

    Task<DriverTrainingRecord> UpsertCompletionAsync(
        int driverId,
        string requirementCode,
        DateTime completedDate,
        DateTime? expiryDate = null,
        string? certificateOrReference = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<bool> RefreshTrainingCompleteFlagAsync(int driverId, CancellationToken cancellationToken = default);
}
