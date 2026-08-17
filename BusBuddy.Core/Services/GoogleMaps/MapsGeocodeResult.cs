namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Combined validate + geocode outcome from Google Address Validation.</summary>
public sealed class MapsGeocodeResult
{
    public bool Ok { get; init; }
    public string? FormattedAddress { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Precision { get; init; }
    public string? ErrorMessage { get; init; }
    public bool MappingUnconfigured { get; init; }
}
