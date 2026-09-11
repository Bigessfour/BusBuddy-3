namespace BusBuddy.Core.Services;

/// <summary>
/// Persistent fuel vendor/location names for the fueling dropdown.
/// Merges clerk-saved catalog entries with distinct locations already on fuel records.
/// </summary>
public interface IFuelLocationCatalog
{
    /// <summary>Locations for the dropdown (saved catalog + DB history + seed defaults).</summary>
    Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Remember a location so it appears on future fuelings (yearly vendor bid).</summary>
    Task RememberAsync(string location, CancellationToken cancellationToken = default);
}
