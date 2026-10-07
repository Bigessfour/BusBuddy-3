using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>School / destination catalog for intake dropdowns and map markers.</summary>
public interface IDestinationService
{
    Task EnsureDefaultSchoolsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Destination>> GetActiveSchoolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Active schools plus inactive campuses students still reference. Use this for roster combos so
    /// a retire does not make <c>StudentSchoolLinker</c> see an unresolvable FK.
    /// </summary>
    Task<IReadOnlyList<Destination>> GetRosterSchoolsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Destination>> GetActiveDestinationsAsync(
        string? destinationType = null,
        CancellationToken cancellationToken = default);

    Task<Destination?> GetByIdAsync(int destinationId, CancellationToken cancellationToken = default);

    Task<bool> UpdateSchoolTimesAsync(
        int destinationId,
        TimeSpan? startTime,
        TimeSpan? dismissalTime,
        CancellationToken cancellationToken = default);

    /// <summary>Catalog a school campus. Bell times stay null until the clerk enters them.</summary>
    Task<Destination> AddSchoolAsync(
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        TimeSpan? startTime,
        TimeSpan? dismissalTime,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates every clerk-facing school field (name, address, GPS, bell times).
    /// specs/locations.md: Address Validation + geocoding remain the source of coordinates.
    /// </summary>
    Task<Destination> UpdateSchoolAsync(
        int destinationId,
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        TimeSpan? startTime,
        TimeSpan? dismissalTime,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an unused school, or retires it when students, transfers, activities, or trips still
    /// reference it. specs/locations.md: "Soft-retire. Do not delete if routes/trips reference it."
    /// </summary>
    Task<SchoolDeleteResult> DeleteSchoolAsync(
        int destinationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One-off trip site. Coordinates must already be Google-validated; this is not a Route string.
    /// </summary>
    Task<Destination> AddTripDestinationAsync(
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default);

    /// <summary>Match a board name to a validated School or TripDestination. Does not invent a pin.</summary>
    Task<Destination?> FindValidatedPlaceByNameAsync(
        string? name,
        CancellationToken cancellationToken = default);
}
