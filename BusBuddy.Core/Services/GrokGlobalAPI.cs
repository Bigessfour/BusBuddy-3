using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Global xAI Grok API service for route optimization and AI analysis
    /// Based on official xAI API documentation: https://docs.x.ai
    /// </summary>
    public class GrokGlobalAPI
    {
        private static readonly ILogger Logger = Log.ForContext<GrokGlobalAPI>();
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly bool _isConfigured;

        // xAI API Constants (per official docs)
        public static readonly string CHAT_COMPLETIONS_ENDPOINT = "/chat/completions";
        public static readonly string DEFAULT_MODEL = "grok-4-latest";
        public static readonly string API_BASE_URL = "https://api.x.ai/v1";

        public GrokGlobalAPI(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Load xAI configuration from environment and appsettings
            _apiKey = _configuration["XAI:ApiKey"] ?? Environment.GetEnvironmentVariable("XAI_API_KEY") ?? string.Empty;
            _baseUrl = _configuration["XAI:BaseUrl"] ?? API_BASE_URL;
            var useLiveAPIString = _configuration["XAI:UseLiveAPI"] ?? "true";
            var useLiveAPI = bool.TryParse(useLiveAPIString, out var parsed) ? parsed : true;

            _isConfigured = !string.IsNullOrEmpty(_apiKey) && !_apiKey.Contains("${XAI_API_KEY}") && useLiveAPI;

            if (_isConfigured)
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "BusBuddy/1.0");
                var timeoutString = _configuration["XAI:TimeoutSeconds"] ?? "60";
                var timeoutSeconds = int.TryParse(timeoutString, out var parsedTimeout) ? parsedTimeout : 60;
                _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                Logger.Information("GrokGlobalAPI configured with xAI endpoint: {BaseUrl}", _baseUrl);
            }
            else
            {
                Logger.Warning("GrokGlobalAPI not configured. Set XAI_API_KEY environment variable for live AI features.");
            }
        }

        public bool IsConfigured => _isConfigured;

        /// <summary>
        /// Call bb-routes for optimization using xAI Grok intelligence
        /// This is the main method requested in the user requirements
        /// </summary>
        public async Task<RouteOptimizationResult> OptimizeRoutesAsync(RouteOptimizationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                Logger.Information("Starting Grok route optimization for route {RouteId}", request.RouteId);

                if (!_isConfigured)
                {
                    return await GenerateMockOptimization(request);
                }

                var prompt = BuildRouteOptimizationPrompt(request);
                var grokRequest = new XAIRequest
                {
                    Model = _configuration["XAI:DefaultModel"] ?? DEFAULT_MODEL,
                    Messages = new[]
                    {
                        new XAIMessage
                        {
                            Role = "system",
                            Content = GetRouteOptimizationSystemPrompt()
                        },
                        new XAIMessage
                        {
                            Role = "user",
                            Content = prompt
                        }
                    },
                    Temperature = double.TryParse(_configuration["XAI:Temperature"], out var temp) ? temp : 0.3,
                    MaxTokens = int.TryParse(_configuration["XAI:MaxTokens"], out var maxTokens) ? maxTokens : 4000
                };

                var response = await CallGrokAPI(CHAT_COMPLETIONS_ENDPOINT, grokRequest);
                return ParseOptimizationResponse(response, request);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in Grok route optimization for route {RouteId}", request.RouteId);
                return await GenerateMockOptimization(request);
            }
        }

        /// <summary>
        /// Generic Grok API call method following xAI documentation standards
        /// </summary>
        private async Task<XAIResponse> CallGrokAPI(string endpoint, XAIRequest request)
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

                Logger.Debug("Calling xAI API endpoint: {Endpoint}", endpoint);
                var httpResponse = await _httpClient.PostAsync(_baseUrl + endpoint, content);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    Logger.Error("xAI API call failed with status {StatusCode}: {ErrorContent}",
                        httpResponse.StatusCode, errorContent);

                    return new XAIResponse
                    {
                        Choices = new[]
                        {
                            new XAIChoice
                            {
                                Message = new XAIMessage
                                {
                                    Content = $"API Error: {httpResponse.StatusCode} - {errorContent}"
                                }
                            }
                        }
                    };
                }

                var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                var response = JsonSerializer.Deserialize<XAIResponse>(jsonResponse, jsonOptions);

                Logger.Debug("xAI API response received successfully");
                return response ?? new XAIResponse { Choices = Array.Empty<XAIChoice>() };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "HTTP request to xAI API failed");
                return new XAIResponse
                {
                    Choices = new[]
                    {
                        new XAIChoice
                        {
                            Message = new XAIMessage
                            {
                                Content = $"Network Error: {ex.Message}"
                            }
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Build route optimization prompt for Grok
        /// </summary>
        private string BuildRouteOptimizationPrompt(RouteOptimizationRequest request)
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

        /// <summary>
        /// System prompt for route optimization
        /// </summary>
        private string GetRouteOptimizationSystemPrompt()
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

        /// <summary>
        /// Parse Grok response into structured optimization result
        /// </summary>
        private RouteOptimizationResult ParseOptimizationResponse(XAIResponse response, RouteOptimizationRequest request)
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
                AIModel = DEFAULT_MODEL
            };
        }

        /// <summary>
        /// Generate mock optimization for testing/fallback
        /// </summary>
        private async Task<RouteOptimizationResult> GenerateMockOptimization(RouteOptimizationRequest request)
        {
            await Task.Delay(500); // Simulate processing time

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

        // Helper methods for parsing AI response
        private double ExtractEfficiencyGain(string content) =>
            ExtractPercentage(content, new[] { "efficiency", "improvement", "gain" });

        private double ExtractTimeReduction(string content) =>
            ExtractPercentage(content, new[] { "time", "reduction", "faster" });

        private double ExtractFuelSavings(string content) =>
            ExtractPercentage(content, new[] { "fuel", "savings", "consumption" });

        private List<string> ExtractSafetyImprovements(string content)
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

        private List<string> ExtractImplementationSteps(string content)
        {
            var steps = new List<string>();
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("1.") || line.Trim().StartsWith("2.") ||
                    line.Trim().StartsWith("3.") || line.Trim().StartsWith("4.") ||
                    line.Trim().StartsWith("5."))
                {
                    steps.Add(line.Trim());
                }
            }

            return steps.Count > 0 ? steps : new List<string> { "Review and implement recommendations gradually" };
        }

        private double ExtractPercentage(string content, string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                var index = content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    // Look for percentage patterns near the keyword
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
