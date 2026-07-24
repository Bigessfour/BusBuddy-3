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
    /// Local Ollama-backed chat implementing <see cref="IXAIChatService"/>.
    /// Uses Ollama's OpenAI-compatible /v1/chat/completions endpoint.
    /// Gracefully degrades when Ollama is not running.
    /// </summary>
    public class OllamaChatService : IXAIChatService
    {
        private static readonly ILogger Logger = Log.ForContext<OllamaChatService>();
        private readonly HttpClient _httpClient;
        private readonly XaiOptions _options;
        private bool _isInitialized;
        private bool _ollamaReachable;

        public OllamaChatService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = BindOptions(configuration);
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

                if (!_ollamaReachable || _options.IsDisabled || !_options.UseLiveAPI)
                {
                    return BuildUnavailableMessage();
                }

                var model = string.IsNullOrWhiteSpace(_options.OllamaModel)
                    ? "llama3.2"
                    : _options.OllamaModel;
                var baseUrl = (_options.OllamaBaseUrl ?? "http://localhost:11434/v1").TrimEnd('/');
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
                var native = (_options.OllamaNativeBaseUrl ?? "http://localhost:11434").TrimEnd('/');
                using var response = await _httpClient.GetAsync($"{native}/api/tags");
                _ollamaReachable = response.IsSuccessStatusCode;
                return _ollamaReachable && _options.UseLiveAPI && !_options.IsDisabled;
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
                    _options.OllamaBaseUrl, _options.OllamaModel);
            }
            else
            {
                Logger.Warning(
                    "Ollama not reachable at {NativeBase}. Chat will use graceful offline fallback. Start Ollama locally to enable live AI.",
                    _options.OllamaNativeBaseUrl);
            }
        }

        private static string BuildUnavailableMessage() =>
            "Local AI (Ollama) is not available right now. Start Ollama on this machine (default http://localhost:11434) and ensure a model is pulled (e.g. `ollama pull llama3.2`). BusBuddy continues to work offline without chat AI.";

        private static XaiOptions BindOptions(IConfiguration configuration)
        {
            var section = configuration.GetSection(XaiOptions.SectionName);
            var options = new XaiOptions
            {
                Provider = section["Provider"] ?? "Ollama",
                ApiKey = section["ApiKey"] ?? string.Empty,
                BaseUrl = section["BaseUrl"] ?? "https://api.x.ai/v1",
                OllamaBaseUrl = section["OllamaBaseUrl"] ?? "http://localhost:11434/v1",
                OllamaNativeBaseUrl = section["OllamaNativeBaseUrl"] ?? "http://localhost:11434",
                OllamaModel = section["OllamaModel"] ?? "llama3.2",
                DefaultModel = section["DefaultModel"] ?? "grok-4-latest",
                PriorityLevel = section["PriorityLevel"] ?? "Standard"
            };

            if (int.TryParse(section["TimeoutSeconds"], out var timeout))
            {
                options.TimeoutSeconds = timeout;
            }

            if (double.TryParse(section["Temperature"], out var temperature))
            {
                options.Temperature = temperature;
            }

            if (bool.TryParse(section["UseLiveAPI"], out var useLive))
            {
                options.UseLiveAPI = useLive;
            }

            if (string.IsNullOrWhiteSpace(options.Provider))
            {
                options.Provider = "Ollama";
            }

            return options;
        }
    }
}
