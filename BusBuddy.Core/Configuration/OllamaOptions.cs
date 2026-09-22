using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Local Ollama settings. Maps to the Ollama section in appsettings.json.
/// Cloud xAI is not a supported provider.
/// </summary>
public class OllamaOptions
{
    public const string SectionName = "Ollama";
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 300;
    public const int MinMaxTokens = 1;
    public const int MaxMaxTokens = 256_000;
    public const double MinTemperature = 0.0;
    public const double MaxTemperature = 2.0;

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

    [Range(MinTimeoutSeconds, MaxTimeoutSeconds)]
    public int TimeoutSeconds { get; set; } = 60;

    [Range(MinMaxTokens, MaxMaxTokens)]
    public int MaxTokens { get; set; } = 2048;

    [Range(MinTemperature, MaxTemperature)]
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

        if (int.TryParse(section["TimeoutSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout))
        {
            options.TimeoutSeconds = timeout;
        }

        if (int.TryParse(section["MaxTokens"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxTokens))
        {
            options.MaxTokens = maxTokens;
        }

        if (double.TryParse(section["Temperature"], NumberStyles.Float, CultureInfo.InvariantCulture, out var temperature))
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

        options.ApplyBounds();
        return options;
    }

    /// <summary>
    /// Pull numeric settings back inside the <see cref="RangeAttribute"/> limits declared on this type.
    /// </summary>
    public void ApplyBounds()
    {
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds);
        MaxTokens = Math.Clamp(MaxTokens, MinMaxTokens, MaxMaxTokens);
        Temperature = Math.Clamp(Temperature, MinTemperature, MaxTemperature);
    }
}
