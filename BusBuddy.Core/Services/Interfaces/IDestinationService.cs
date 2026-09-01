using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces;

/// <summary>School / destination catalog for intake dropdowns and map markers.</summary>
public interface IDestinationService
{
    Task EnsureDefaultSchoolsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Destination>> GetActiveSchoolsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Destination>> GetActiveDestinationsAsync(
        string? destinationType = null,
        CancellationToken cancellationToken = default);

    Task<Destination?> GetByIdAsync(int destinationId, CancellationToken cancellationToken = default);

    Task<bool> UpdateSchoolTimesAsync(
        int destinationId,
        TimeSpan? startTime,
        TimeSpan? dismissalTime,
        CancellationToken cancellationToken = default);

    /// <summary>Catalog a school campus. Start and dismissal times are required for route generation.</summary>
    Task<Destination> AddSchoolAsync(
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        TimeSpan startTime,
        TimeSpan dismissalTime,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationToken cancellationToken = default);
}
