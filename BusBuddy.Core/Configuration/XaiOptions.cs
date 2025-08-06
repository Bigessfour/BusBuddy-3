using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Configuration options for xAI Grok API integration.
/// Maps to the XAI section in appsettings.azure.json.
/// </summary>
public class XaiOptions
{
    public const string SectionName = "XAI";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string BaseUrl { get; set; } = "https://api.x.ai/v1";

    public string DefaultModel { get; set; } = "grok-4-latest";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    [Range(1, 10)]
    public int RetryAttempts { get; set; } = 3;

    [Range(1, 256000)]
    public int MaxTokens { get; set; } = 128000;

    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.3;

    public bool UseLiveAPI { get; set; } = true;
    public bool EnableRouteOptimization { get; set; } = true;
    public bool EnableMaintenancePrediction { get; set; } = true;
    public bool EnableSafetyAnalysis { get; set; } = true;
    public bool EnableStudentOptimization { get; set; } = true;
    public bool EnableConversationalAI { get; set; } = true;
    public bool CacheAIResponses { get; set; } = true;

    [Range(1, 168)]
    public int CacheExpiryHours { get; set; } = 24;

    [Range(1, 1000)]
    public int RateLimitPerMinute { get; set; } = 60;

    public string PriorityLevel { get; set; } = "Standard";
}
