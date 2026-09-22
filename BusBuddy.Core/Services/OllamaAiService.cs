using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Local Ollama chat completions for report commentary.
    /// When Ollama is not reachable, callers receive mock text — that is expected, not an actionable error.
    /// </summary>
    public class OllamaAiService
    {
        private static readonly ILogger Logger = Log.ForContext<OllamaAiService>();
        public static readonly string ChatCompletionsEndpoint = "/chat/completions";
        public static readonly string DefaultModel = "llama3.2";

        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;
        private readonly bool _isConfigured;

        public OllamaAiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            ArgumentNullException.ThrowIfNull(configuration);
            _options = OllamaOptions.Bind(configuration);

            if (!_options.Enabled)
            {
                _isConfigured = false;
                Logger.Information("OllamaAiService disabled via Ollama:Enabled. Using mock commentary.");
                return;
            }

            _isConfigured = true;
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BusBuddy/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 300));
            Logger.Information("OllamaAiService configured with local endpoint: {BaseUrl} model {Model}",
                _options.BaseUrl, _options.Model);
        }

        public bool IsConfigured => _isConfigured;

        /// <summary>
        /// Short operator-facing commentary for reports. Uses Ollama when reachable; otherwise a mock line.
        /// </summary>
        public async Task<string> GetShortCommentaryAsync(string topic, string facts)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                topic = "report";
            }

            facts ??= string.Empty;
            if (!_isConfigured)
            {
                return $"Mock insight for {topic}: {facts}";
            }

            try
            {
                var request = new ChatCompletionRequest
                {
                    Model = string.IsNullOrWhiteSpace(_options.Model) ? DefaultModel : _options.Model,
                    Messages = new[]
                    {
                        new ChatCompletionMessage
                        {
                            Role = "system",
                            Content = "You are a school transportation coordinator assistant. Reply in one or two short sentences."
                        },
                        new ChatCompletionMessage
                        {
                            Role = "user",
                            Content = $"Topic: {topic}\nFacts: {facts}"
                        }
                    },
                    Temperature = 0.2,
                    MaxTokens = 120
                };

                var response = await CallOllamaAsync(ChatCompletionsEndpoint, request).ConfigureAwait(false);
                if (IsFailedApiResponse(response))
                {
                    return $"Mock insight for {topic}: {facts}";
                }

                var content = response.Choices[0].Message.Content?.Trim();
                return string.IsNullOrWhiteSpace(content) ? $"Mock insight for {topic}: {facts}" : content;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Report commentary fell back to mock for {Topic}", topic);
                return $"Mock insight for {topic}: {facts}";
            }
        }

        private async Task<ChatCompletionResponse> CallOllamaAsync(string endpoint, ChatCompletionRequest request)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = false
                };

                var jsonRequest = JsonSerializer.Serialize(request, jsonOptions);
                using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                Logger.Debug("Calling Ollama endpoint: {BaseUrl}{Endpoint}", _options.BaseUrl, endpoint);
                var httpResponse = await _httpClient.PostAsync(_options.BaseUrl + endpoint, content);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    Logger.Warning("Ollama call failed with status {StatusCode}: {ErrorContent}",
                        httpResponse.StatusCode, errorContent);

                    return FailedResponse($"API Error: {httpResponse.StatusCode} - {errorContent}");
                }

                var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                var response = JsonSerializer.Deserialize<ChatCompletionResponse>(jsonResponse, jsonOptions);

                Logger.Debug("Ollama response received successfully");
                return response ?? new ChatCompletionResponse { Choices = Array.Empty<ChatCompletionChoice>() };
            }
            catch (Exception ex)
            {
                Logger.Information(ex,
                    "Ollama is not reachable at {BaseUrl}. Using offline fallback. Start Ollama locally (default http://localhost:11434) to enable live AI.",
                    _options.BaseUrl);
                return FailedResponse($"Network Error: {ex.Message}");
            }
        }

        private static ChatCompletionResponse FailedResponse(string content) =>
            new()
            {
                Choices = new[]
                {
                    new ChatCompletionChoice
                    {
                        Message = new ChatCompletionMessage { Content = content }
                    }
                }
            };

        /// <summary>
        /// True when <see cref="CallOllamaAsync"/> returned a synthetic error payload instead of model output.
        /// </summary>
        private static bool IsFailedApiResponse(ChatCompletionResponse? response)
        {
            if (response?.Choices == null || response.Choices.Length == 0)
            {
                return true;
            }

            var content = response.Choices[0]?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return true;
            }

            return content.StartsWith("API Error:", StringComparison.OrdinalIgnoreCase)
                   || content.StartsWith("Network Error:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
