namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Combined validate + geocode outcome from Google Address Validation / Geocoding.</summary>
public sealed class MapsGeocodeResult
{
    public bool Ok { get; set; }
    public string? FormattedAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Google place id from Address Validation / Geocoding; may be stored indefinitely.</summary>
    public string? PlaceId { get; set; }

    public string? Precision { get; set; }
    public string? ErrorMessage { get; set; }
    public bool MappingUnconfigured { get; set; }

    /// <summary>UTC time this result was written to <see cref="MapsAddressCache"/> (lat/lng TTL).</summary>
    public DateTimeOffset? CachedAtUtc { get; set; }
}
