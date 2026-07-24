using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Configuration options for AI providers (local Ollama preferred; legacy xAI optional).
/// Maps to the XAI section in appsettings.json for backward compatibility.
/// </summary>
public class XaiOptions
{
    public const string SectionName = "XAI";

    /// <summary>
    /// Active provider: Ollama (default), Xai, or Disabled.
    /// </summary>
    public string Provider { get; set; } = "Ollama";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string BaseUrl { get; set; } = "https://api.x.ai/v1";

    /// <summary>
    /// OpenAI-compatible base URL for local Ollama (default port 11434).
    /// </summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434/v1";

    /// <summary>
    /// Native Ollama HTTP API root (tags / health checks).
    /// </summary>
    public string OllamaNativeBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model name served by Ollama (e.g. llama3.2, mistral).
    /// </summary>
    public string OllamaModel { get; set; } = "llama3.2";

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

    public bool IsOllama =>
        string.Equals(Provider, "Ollama", StringComparison.OrdinalIgnoreCase);

    public bool IsDisabled =>
        string.Equals(Provider, "Disabled", StringComparison.OrdinalIgnoreCase);

    public bool IsXai =>
        string.Equals(Provider, "Xai", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Provider, "XAI", StringComparison.OrdinalIgnoreCase);
}
