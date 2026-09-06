namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Combined validate + geocode outcome from Google Address Validation.</summary>
public sealed class MapsGeocodeResult
{
    public bool Ok { get; set; }
    public string? FormattedAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Precision { get; set; }
    public string? ErrorMessage { get; set; }
    public bool MappingUnconfigured { get; set; }
}
