using BusBuddy.Core.Services.Interfaces;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Canonical Maps Platform entry point for address validation + geocoding (US clerk paths).
/// Wraps <see cref="GoogleAddressValidationClient"/> with caching; never uses hash coordinates.
/// </summary>
public interface IMapsGeoService : IGeocodingService
{
    /// <summary>True when <c>GOOGLE_MAPS_API_KEY</c> (or configured key) is present.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Postal-grade validate + geocode via Address Validation API (Geocoding API fallback on 403).
    /// Results are cached by normalized address (FR-004).
    /// </summary>
    Task<MapsGeocodeResult> ValidateAndGeocodeAsync(
        string? street,
        string? city,
        string? state,
        string? zip,
        CancellationToken cancellationToken = default);

    /// <summary>Geocode only — returns null when validation fails or mapping is unconfigured.</summary>
    Task<(double latitude, double longitude)?> GeocodeAsync(
        string? street,
        string? city,
        string? state,
        string? zip,
        CancellationToken cancellationToken = default);
}
