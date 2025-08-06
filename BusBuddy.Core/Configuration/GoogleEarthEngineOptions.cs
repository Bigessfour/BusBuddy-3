using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Configuration options for Google Earth Engine integration.
/// Maps to the GoogleEarthEngine section in appsettings.azure.json.
/// </summary>
public class GoogleEarthEngineOptions
{
    public const string SectionName = "GoogleEarthEngine";

    [Required]
    public string ProjectId { get; set; } = string.Empty;

    [Required]
    public string ServiceAccountEmail { get; set; } = string.Empty;

    [Required]
    public string ServiceAccountKeyPath { get; set; } = string.Empty;

    [Required]
    public string BaseUrl { get; set; } = "https://earthengine.googleapis.com/v1alpha";

    public string CodeEditorUrl { get; set; } = "https://code.earthengine.google.com/";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(1, 10)]
    public int RetryAttempts { get; set; } = 3;

    [Range(1, 168)]
    public int CacheExpiryHours { get; set; } = 1;

    [Range(1, 50)]
    public int MaxConcurrentRequests { get; set; } = 5;

    public bool EnableSatelliteImagery { get; set; } = true;
    public bool EnableTerrainAnalysis { get; set; } = true;
    public bool EnableTrafficAnalysis { get; set; } = true;
    public bool EnableWeatherAnalysis { get; set; } = true;

    [Range(1, 1000)]
    public int DefaultImageResolution { get; set; } = 30;

    [Range(1000, 1000000)]
    public int MaxRouteLength { get; set; } = 100000;

    public ServiceAreaOptions ServiceArea { get; set; } = new();
}

/// <summary>
/// Service area configuration for Google Earth Engine operations.
/// </summary>
public class ServiceAreaOptions
{
    public string Name { get; set; } = "Bus Buddy Service Area";

    [Range(-90, 90)]
    public double MinLatitude { get; set; }

    [Range(-90, 90)]
    public double MaxLatitude { get; set; }

    [Range(-180, 180)]
    public double MinLongitude { get; set; }

    [Range(-180, 180)]
    public double MaxLongitude { get; set; }
}
