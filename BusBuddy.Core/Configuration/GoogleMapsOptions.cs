namespace BusBuddy.Core.Configuration;

/// <summary>
/// Google Maps Platform options (Address Validation, Routes). Bound from the <c>GoogleMaps</c> config section.
/// </summary>
public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>API key; prefer env <c>GOOGLE_MAPS_API_KEY</c> over placeholder appsettings values.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>GCP billing / quota project (e.g. <c>new-coursera-490518</c>).</summary>
    public string QuotaProject { get; set; } = "new-coursera-490518";

    /// <summary>Enable USPS CASS for Address Validation.</summary>
    public bool EnableUspsCass { get; set; } = true;

    /// <summary>Region code for Address Validation (US).</summary>
    public string RegionCode { get; set; } = "US";
}
