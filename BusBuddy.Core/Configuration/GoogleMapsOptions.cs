namespace BusBuddy.Core.Configuration;

/// <summary>
/// Google Maps Platform options (Address Validation, Routes). Bound from the <c>GoogleMaps</c> config section.
/// </summary>
public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>
    /// Canonical GCP project that should <em>own</em> <c>GOOGLE_MAPS_API_KEY</c> (billing follows the key).
    /// Do not create Maps keys under the legacy Coursera project.
    /// </summary>
    public const string CanonicalProjectId = "busbuddy-507301";

    /// <summary>Places Autocomplete (New) locationBias circle radius max, in meters.</summary>
    public const double MaxAutocompleteBiasRadiusMeters = 50_000;

    /// <summary>API key; prefer env <c>GOOGLE_MAPS_API_KEY</c> over placeholder appsettings values.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional <c>X-Goog-User-Project</c>. Leave empty for API-key clients — Google bills the key's project.
    /// Setting this with an API key often returns HTTP 403 (<c>serviceUsageConsumer</c>) even when the key
    /// already belongs to <see cref="CanonicalProjectId"/>.
    /// </summary>
    public string QuotaProject { get; set; } = string.Empty;

    /// <summary>
    /// Resolve optional <c>X-Goog-User-Project</c>: <c>GCP_BILLING_PROJECT</c>, then <c>GOOGLE_CLOUD_PROJECT</c>,
    /// then bound config. Empty means omit the header (correct for Maps Platform API keys).
    /// </summary>
    public static string ResolveQuotaProject(string? boundQuotaProject = null)
    {
        var billing = Environment.GetEnvironmentVariable("GCP_BILLING_PROJECT");
        if (!string.IsNullOrWhiteSpace(billing))
        {
            return billing.Trim();
        }

        var cloud = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
        if (!string.IsNullOrWhiteSpace(cloud))
        {
            return cloud.Trim();
        }

        if (!string.IsNullOrWhiteSpace(boundQuotaProject))
        {
            return boundQuotaProject.Trim();
        }

        return string.Empty;
    }

    /// <summary>Which input set <see cref="QuotaProject"/> (for diagnostics; never logs secret values).</summary>
    public static string DescribeQuotaSource(string? boundQuotaProject = null)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GCP_BILLING_PROJECT")))
        {
            return "env:GCP_BILLING_PROJECT";
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")))
        {
            return "env:GOOGLE_CLOUD_PROJECT";
        }

        if (!string.IsNullOrWhiteSpace(boundQuotaProject))
        {
            return "config:GoogleMaps:QuotaProject";
        }

        return "none";
    }

    /// <summary>Where the Maps API key was resolved from (presence only).</summary>
    public static string DescribeApiKeySource(string? boundApiKey = null)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")))
        {
            return "env:GOOGLE_MAPS_API_KEY";
        }

        if (!string.IsNullOrWhiteSpace(boundApiKey)
            && !boundApiKey.StartsWith("${", StringComparison.Ordinal))
        {
            return "config:GoogleMaps:ApiKey";
        }

        return "none";
    }

    /// <summary>Enable USPS CASS for Address Validation.</summary>
    public bool EnableUspsCass { get; set; } = true;

    /// <summary>Region code for Address Validation (US).</summary>
    public string RegionCode { get; set; } = "US";

    /// <summary>
    /// Optional Places Autocomplete bias. When unset, bias comes from clerk district settings
    /// (depot / bbox); when those are also unset, autocomplete is region=US with no circle.
    /// </summary>
    public double? AutocompleteBiasLatitude { get; set; }

    public double? AutocompleteBiasLongitude { get; set; }

    /// <summary>
    /// Places Autocomplete (New) locationBias circle radius in meters. Vendor max is <see cref="MaxAutocompleteBiasRadiusMeters"/>.
    /// </summary>
    public double AutocompleteBiasRadiusMeters { get; set; } = MaxAutocompleteBiasRadiusMeters;

    /// <summary>
    /// Clamp autocomplete bias to a complete, in-range coordinate pair and a vendor-legal radius.
    /// A partial or invalid pair is cleared so Places falls through to district geography.
    /// </summary>
    public void Normalize()
    {
        if (double.IsNaN(AutocompleteBiasRadiusMeters)
            || double.IsInfinity(AutocompleteBiasRadiusMeters)
            || AutocompleteBiasRadiusMeters <= 0
            || AutocompleteBiasRadiusMeters > MaxAutocompleteBiasRadiusMeters)
        {
            AutocompleteBiasRadiusMeters = MaxAutocompleteBiasRadiusMeters;
        }

        var latitudeOk = AutocompleteBiasLatitude is double latitude && latitude is >= -90 and <= 90;
        var longitudeOk = AutocompleteBiasLongitude is double longitude && longitude is >= -180 and <= 180;
        if (!latitudeOk || !longitudeOk)
        {
            AutocompleteBiasLatitude = null;
            AutocompleteBiasLongitude = null;
        }
    }
}
