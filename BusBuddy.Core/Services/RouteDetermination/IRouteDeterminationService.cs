namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>Year-start route build, assign fitness, and clerk override (spec 008).</summary>
public interface IRouteDeterminationService
{
    Task<RouteGenerationResult> GenerateAndAssignAsync(
        int schoolDestinationId,
        RouteTimeSlotKind slot,
        FleetKind fleetKind,
        RouteGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<AssignFitnessResult> RecalculateOnAssignAsync(
        int studentId,
        int routeId,
        RouteTimeSlotKind slot,
        bool overrideSeating = false,
        CancellationToken cancellationToken = default);

    Task<ClerkOverrideResult> ApplyClerkOverrideAsync(
        int studentId,
        int fromRouteId,
        int toRouteId,
        RouteTimeSlotKind slot,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>Recompute RouteStop times for every active route that serves this school.</summary>
    Task<RouteGenerationResult> RegenerateSchedulesForSchoolAsync(
        int schoolDestinationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish stop clocks for one route from confirmed school bells.
    /// A missed morning bell returns a warning and does not overwrite stored clocks.
    /// </summary>
    Task<RouteGenerationResult> ApplyPublishedClocksAsync(
        int routeId,
        CancellationToken cancellationToken = default);
}
