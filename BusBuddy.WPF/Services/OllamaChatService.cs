using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Local Ollama-backed chat implementing <see cref="IAiChatService"/>.
    /// Uses Ollama's OpenAI-compatible /v1/chat/completions endpoint.
    /// Gracefully degrades when Ollama is not running.
    /// </summary>
    public class OllamaChatService : IAiChatService
    {
        private static readonly ILogger Logger = Log.ForContext<OllamaChatService>();
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;
        private bool _isInitialized;
        private bool _ollamaReachable;

        public OllamaChatService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = OllamaOptions.Bind(configuration);
            var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 300));
            if (_httpClient.Timeout < timeout)
            {
                _httpClient.Timeout = timeout;
            }
        }

        public async Task<string> GetResponseAsync(string userMessage)
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                if (!_ollamaReachable || !_options.Enabled)
                {
                    return BuildUnavailableMessage();
                }

                var model = string.IsNullOrWhiteSpace(_options.Model)
                    ? "llama3.2"
                    : _options.Model;
                var baseUrl = (_options.BaseUrl ?? "http://localhost:11434/v1").TrimEnd('/');
                var payload = new
                {
                    model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful school transportation assistant for BusBuddy. Be concise and practical." },
                        new { role = "user", content = userMessage ?? string.Empty }
                    },
                    temperature = _options.Temperature,
                    stream = false
                };

                using var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");
                using var response = await _httpClient.PostAsync($"{baseUrl}/chat/completions", content);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Logger.Warning("Ollama chat failed with {Status}: {Body}", response.StatusCode, body);
                    return BuildUnavailableMessage();
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return string.IsNullOrWhiteSpace(text)
                    ? BuildUnavailableMessage()
                    : text.Trim();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Ollama chat request failed; returning graceful fallback");
                _ollamaReachable = false;
                return BuildUnavailableMessage();
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var native = (_options.NativeBaseUrl ?? "http://localhost:11434").TrimEnd('/');
                using var response = await _httpClient.GetAsync($"{native}/api/tags");
                _ollamaReachable = response.IsSuccessStatusCode;
                return _ollamaReachable && _options.Enabled;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Ollama availability check failed");
                _ollamaReachable = false;
                return false;
            }
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _ollamaReachable = await IsAvailableAsync();
            _isInitialized = true;
            if (_ollamaReachable)
            {
                Logger.Information("OllamaChatService ready at {BaseUrl} model {Model}",
                    _options.BaseUrl, _options.Model);
            }
            else
            {
                Logger.Information(
                    "Ollama not reachable at {NativeBase}. Chat will use graceful offline fallback. Start Ollama locally to enable live AI.",
                    _options.NativeBaseUrl);
            }
        }

        private static string BuildUnavailableMessage() =>
            "Local AI (Ollama) is not available right now. Start Ollama on this machine (default http://localhost:11434) and ensure a model is pulled (e.g. `ollama pull llama3.2`). BusBuddy continues to work offline without chat AI.";
    }
}
