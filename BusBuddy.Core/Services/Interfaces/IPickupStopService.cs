using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces;

/// <summary>District pickup stop catalog for shared boarding locations.</summary>
public interface IPickupStopService
{
    Task<IReadOnlyList<PickupStop>> GetActiveStopsAsync(CancellationToken cancellationToken = default);

    Task<PickupStop?> GetByIdAsync(int pickupStopId, CancellationToken cancellationToken = default);

    Task<PickupStop> AddStopAsync(
        string name,
        string? address,
        decimal latitude,
        decimal longitude,
        string stopType = PickupStopTypes.Corner,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>Nearest active stop within <paramref name="maxMeters"/> (Haversine), or null.</summary>
    Task<PickupStop?> FindNearestAsync(
        double latitude,
        double longitude,
        double maxMeters = 400,
        CancellationToken cancellationToken = default);
}
