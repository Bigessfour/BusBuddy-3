using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Local Ollama chat completions for route commentary and optimization notes.
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
                Logger.Information("OllamaAiService disabled via Ollama:Enabled. Using mock optimization.");
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

        /// <summary>
        /// Route optimization notes via local Ollama. Falls back to mock text when Ollama is offline.
        /// </summary>
        public async Task<RouteOptimizationResult> OptimizeRoutesAsync(RouteOptimizationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                Logger.Information("Starting Ollama route optimization for route {RouteId}", request.RouteId);

                if (!_isConfigured)
                {
                    return await GenerateMockOptimization(request);
                }

                var prompt = BuildRouteOptimizationPrompt(request);
                var model = string.IsNullOrWhiteSpace(_options.Model) ? DefaultModel : _options.Model;
                var ollamaRequest = new ChatCompletionRequest
                {
                    Model = model,
                    Messages = new[]
                    {
                        new ChatCompletionMessage
                        {
                            Role = "system",
                            Content = GetRouteOptimizationSystemPrompt()
                        },
                        new ChatCompletionMessage
                        {
                            Role = "user",
                            Content = prompt
                        }
                    },
                    Temperature = _options.Temperature,
                    MaxTokens = _options.MaxTokens > 0 ? _options.MaxTokens : 2048
                };

                var response = await CallOllamaAsync(ChatCompletionsEndpoint, ollamaRequest);
                if (IsFailedApiResponse(response))
                {
                    Logger.Warning(
                        "Live Ollama optimization unavailable for route {RouteId}; using mock fallback",
                        request.RouteId);
                    return await GenerateMockOptimization(request);
                }

                return ParseOptimizationResponse(response, request, model);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Ollama route optimization fell back to mock for route {RouteId}", request.RouteId);
                return await GenerateMockOptimization(request);
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

        private static string BuildRouteOptimizationPrompt(RouteOptimizationRequest request)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Analyze and optimize the following school bus route:");
            prompt.AppendLine($"Route ID: {request.RouteId}");
            prompt.AppendLine($"Current Performance: {request.CurrentPerformance}");
            prompt.AppendLine($"Target Metrics: {request.TargetMetrics}");

            if (request.Constraints.Count > 0)
            {
                prompt.AppendLine("Constraints:");
                foreach (var constraint in request.Constraints)
                {
                    prompt.AppendLine($"- {constraint}");
                }
            }

            prompt.AppendLine();
            prompt.AppendLine("Please provide specific optimization recommendations including:");
            prompt.AppendLine("1. Route efficiency improvements");
            prompt.AppendLine("2. Time optimization strategies");
            prompt.AppendLine("3. Fuel efficiency recommendations");
            prompt.AppendLine("4. Safety considerations");
            prompt.AppendLine("5. Implementation steps");

            return prompt.ToString();
        }

        private static string GetRouteOptimizationSystemPrompt()
        {
            return @"You are an expert transportation logistics AI specializing in school bus route optimization.
You have deep knowledge of:
- Route planning and efficiency algorithms
- School transportation safety regulations
- Fleet management best practices
- Geographic optimization strategies
- Student transportation logistics
- Fuel efficiency optimization
- Time management for school schedules

Provide actionable, practical recommendations that can be implemented by transportation coordinators.
Focus on measurable improvements and safety compliance.";
        }

        private RouteOptimizationResult ParseOptimizationResponse(
            ChatCompletionResponse response,
            RouteOptimizationRequest request,
            string model)
        {
            var content = response.Choices?.FirstOrDefault()?.Message?.Content ?? "No optimization available";

            return new RouteOptimizationResult
            {
                RouteId = request.RouteId,
                OptimizationSuggestions = content,
                EfficiencyGain = ExtractEfficiencyGain(content),
                TimeReduction = ExtractTimeReduction(content),
                FuelSavings = ExtractFuelSavings(content),
                SafetyImprovements = ExtractSafetyImprovements(content),
                ImplementationSteps = ExtractImplementationSteps(content),
                GeneratedAt = DateTime.UtcNow,
                AIModel = model
            };
        }

        private static async Task<RouteOptimizationResult> GenerateMockOptimization(RouteOptimizationRequest request)
        {
            await Task.Delay(500);

            return new RouteOptimizationResult
            {
                RouteId = request.RouteId,
                OptimizationSuggestions = $"Mock optimization for route {request.RouteId}: Consider consolidating stops within 0.5 miles, optimize pickup sequence by grade level, and implement GPS tracking for real-time adjustments.",
                EfficiencyGain = 12.5,
                TimeReduction = 8.0,
                FuelSavings = 15.0,
                SafetyImprovements = new List<string> { "Reduced intersection crossings", "Optimized stop locations" },
                ImplementationSteps = new List<string>
                {
                    "Review current route data",
                    "Identify consolidation opportunities",
                    "Test optimized route",
                    "Implement changes gradually"
                },
                GeneratedAt = DateTime.UtcNow,
                AIModel = "Mock-AI"
            };
        }

        private static double ExtractEfficiencyGain(string content) =>
            ExtractPercentage(content, new[] { "efficiency", "improvement", "gain" });

        private static double ExtractTimeReduction(string content) =>
            ExtractPercentage(content, new[] { "time", "reduction", "faster" });

        private static double ExtractFuelSavings(string content) =>
            ExtractPercentage(content, new[] { "fuel", "savings", "consumption" });

        private static List<string> ExtractSafetyImprovements(string content)
        {
            var improvements = new List<string>();
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains("safety", StringComparison.OrdinalIgnoreCase) && line.Length > 10)
                {
                    improvements.Add(line.Trim());
                }
            }

            return improvements.Count > 0 ? improvements : new List<string> { "General safety compliance maintained" };
        }

        private static List<string> ExtractImplementationSteps(string content)
        {
            var steps = new List<string>();
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("1.", StringComparison.Ordinal) || line.Trim().StartsWith("2.", StringComparison.Ordinal) ||
                    line.Trim().StartsWith("3.", StringComparison.Ordinal) || line.Trim().StartsWith("4.", StringComparison.Ordinal) ||
                    line.Trim().StartsWith("5.", StringComparison.Ordinal))
                {
                    steps.Add(line.Trim());
                }
            }

            return steps.Count > 0 ? steps : new List<string> { "Review and implement recommendations gradually" };
        }

        private static double ExtractPercentage(string content, string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                var index = content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    var nearText = content.Substring(Math.Max(0, index - 50), Math.Min(100, content.Length - Math.Max(0, index - 50)));
                    var match = System.Text.RegularExpressions.Regex.Match(nearText, @"(\d+\.?\d*)%");
                    if (match.Success && double.TryParse(match.Groups[1].Value, out var percentage))
                    {
                        return percentage;
                    }
                }
            }
            return 0.0;
        }
    }

    /// <summary>
    /// Route optimization result model
    /// </summary>
    public class RouteOptimizationResult
    {
        public string RouteId { get; set; } = string.Empty;
        public string OptimizationSuggestions { get; set; } = string.Empty;
        public double EfficiencyGain { get; set; }
        public double TimeReduction { get; set; }
        public double FuelSavings { get; set; }
        public List<string> SafetyImprovements { get; set; } = new();
        public List<string> ImplementationSteps { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
        public string AIModel { get; set; } = string.Empty;
    }
}
