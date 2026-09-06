namespace BusBuddy.Core.Configuration;

/// <summary>
/// Google Maps Platform options (Address Validation, Routes). Bound from the <c>GoogleMaps</c> config section.
/// </summary>
public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>Canonical Maps billing / quota / <c>gcloud</c> project. Do not header traffic to the legacy Coursera project.</summary>
    public const string DefaultQuotaProject = "busbuddy-507301";

    /// <summary>API key; prefer env <c>GOOGLE_MAPS_API_KEY</c> over placeholder appsettings values.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>GCP billing / quota project (e.g. <c>busbuddy-507301</c>).</summary>
    public string QuotaProject { get; set; } = DefaultQuotaProject;

    /// <summary>
    /// Resolve <c>X-Goog-User-Project</c>: <c>GCP_BILLING_PROJECT</c>, then <c>GOOGLE_CLOUD_PROJECT</c>,
    /// then bound config, then <see cref="DefaultQuotaProject"/>.
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

        return DefaultQuotaProject;
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

    /// <summary>Places Autocomplete bias radius in meters (~50 mi default).</summary>
    public double AutocompleteBiasRadiusMeters { get; set; } = 80_000;
}
