using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Local Ollama settings. Maps to the Ollama section in appsettings.json.
/// Cloud xAI is not a supported provider.
/// </summary>
public class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>
    /// OpenAI-compatible base URL (default port 11434).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";

    /// <summary>
    /// Native Ollama HTTP API root (tags / health checks).
    /// </summary>
    public string NativeBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model name served by Ollama (e.g. llama3.2, mistral).
    /// </summary>
    public string Model { get; set; } = "llama3.2";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    [Range(1, 256000)]
    public int MaxTokens { get; set; } = 2048;

    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// When false, chat and route AI skip HTTP and use offline fallback text.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public static OllamaOptions Bind(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var options = new OllamaOptions();

        var baseUrl = section["BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            options.BaseUrl = baseUrl.TrimEnd('/');
        }

        var native = section["NativeBaseUrl"];
        if (!string.IsNullOrWhiteSpace(native))
        {
            options.NativeBaseUrl = native.TrimEnd('/');
        }

        var model = section["Model"] ?? section["OllamaModel"];
        if (!string.IsNullOrWhiteSpace(model))
        {
            options.Model = model;
        }

        if (int.TryParse(section["TimeoutSeconds"], out var timeout))
        {
            options.TimeoutSeconds = timeout;
        }

        if (int.TryParse(section["MaxTokens"], out var maxTokens))
        {
            options.MaxTokens = maxTokens;
        }

        if (double.TryParse(section["Temperature"], out var temperature))
        {
            options.Temperature = temperature;
        }

        if (bool.TryParse(section["Enabled"], out var enabled))
        {
            options.Enabled = enabled;
        }
        else if (bool.TryParse(section["UseLiveAPI"], out var useLive))
        {
            options.Enabled = useLive;
        }

        return options;
    }
}
