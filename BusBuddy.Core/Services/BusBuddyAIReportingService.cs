// Services/BusBuddyAIReportingService.cs
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Advanced AI Reporting Service for BusBuddy with caching, context awareness, and performance monitoring
    /// Implements all expert recommendations from xAI API consultation
    /// </summary>
    public class BusBuddyAIReportingService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private static readonly ILogger Logger = Log.ForContext<BusBuddyAIReportingService>();
        private readonly TransportationContext _transportationContext;
        private readonly ContextAwarePromptBuilder _promptBuilder;
        private readonly string _apiKey;
        private readonly string _apiUrl = "https://api.x.ai/v1/chat/completions";

        public BusBuddyAIReportingService(
            HttpClient httpClient,
            IMemoryCache cache,
            TransportationContext transportationContext,
            ContextAwarePromptBuilder promptBuilder,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(transportationContext);
            ArgumentNullException.ThrowIfNull(promptBuilder);
            ArgumentNullException.ThrowIfNull(configuration);

            _httpClient = httpClient;
            _cache = cache;
            _transportationContext = transportationContext;
            _promptBuilder = promptBuilder;
            _apiKey = configuration["XAI:ApiKey"] ?? throw new ArgumentException("XAI:ApiKey configuration is required");
        }

        /// <summary>
        /// Generate comprehensive AI reports with context awareness and performance monitoring
        /// </summary>
        public async Task<AIReportResponse> GenerateReportAsync(string reportType, string? location = null, Dictionary<string, object>? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(reportType);

            var startTime = DateTime.UtcNow;
            var operationId = Guid.NewGuid().ToString();

            try
            {
                Logger.Information("Starting AI report generation: {ReportType}, Operation: {OperationId}", reportType, operationId);

                // Get cached transportation context
                var contextData = await _transportationContext.GetContextDataAsync("BusBuddy");

                // Build context-aware prompt
                var prompt = await _promptBuilder.BuildReportPromptAsync(reportType, location ?? "Unknown", contextData, parameters ?? new Dictionary<string, object>());

                // Make AI API call with retry logic
                var aiResponse = await CallAIWithRetryAsync(prompt, operationId);

                // Parse and enrich response
                var reportResponse = new AIReportResponse
                {
                    ReportType = reportType,
                    Content = aiResponse.Content,
                    GeneratedAt = DateTime.UtcNow,
                    OperationId = operationId,
                    TokenUsage = aiResponse.Usage,
                    ContextData = contextData
                };

                // Cache the response
                CacheReportAsync(reportType, reportResponse);

                var duration = DateTime.UtcNow - startTime;
                Logger.Information("AI report generated successfully: {ReportType}, Duration: {Duration}ms, Tokens: {Tokens}",
                    reportType, duration.TotalMilliseconds, aiResponse.Usage?.TotalTokens);

                return reportResponse;
            }
            catch (HttpRequestException ex)
            {
                var duration = DateTime.UtcNow - startTime;
                Logger.Error(ex, "AI report generation failed due to HTTP error: {ReportType}, Duration: {Duration}ms, Operation: {OperationId}",
                    reportType, duration.TotalMilliseconds, operationId);
                throw;
            }
            catch (JsonException ex)
            {
                var duration = DateTime.UtcNow - startTime;
                Logger.Error(ex, "AI report generation failed due to JSON parsing error: {ReportType}, Duration: {Duration}ms, Operation: {OperationId}",
                    reportType, duration.TotalMilliseconds, operationId);
                throw;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                Logger.Error(ex, "AI report generation failed: {ReportType}, Duration: {Duration}ms, Operation: {OperationId}",
                    reportType, duration.TotalMilliseconds, operationId);
                throw;
            }
        }

        /// <summary>
        /// AI API call with exponential backoff retry logic
        /// </summary>
        private async Task<AIApiResponse> CallAIWithRetryAsync(string prompt, string operationId, int maxRetries = 3)
        {
            var retryDelay = TimeSpan.FromSeconds(1);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var requestPayload = new
                    {
                        messages = new[]
                        {
                            new { role = "system", content = "You are Bus Buddy AI, an expert transportation management assistant. Provide detailed, actionable insights for school bus operations." },
                            new { role = "user", content = prompt }
                        },
                        model = "grok-4-latest",
                        temperature = 0.3,
                        max_tokens = 2000
                    };

                    var jsonContent = JsonSerializer.Serialize(requestPayload);
                    using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                    var response = await _httpClient.PostAsync(_apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content.ReadAsStringAsync();

                        return new AIApiResponse
                        {
                            Content = ExtractContentFromResponse(responseJson),
                            Usage = ExtractUsageFromResponse(responseJson)
                        };
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        Logger.Warning("Rate limit hit, attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                            attempt, maxRetries, retryDelay.TotalSeconds);

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelay);
                            retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2); // Exponential backoff
                            continue;
                        }
                    }

                    throw new HttpRequestException($"AI API call failed: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
                catch (HttpRequestException) when (attempt < maxRetries)
                {
                    Logger.Warning("AI API call failed, attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                        attempt, maxRetries, retryDelay.TotalSeconds);
                    await Task.Delay(retryDelay);
                    retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2);
                }
                catch (TaskCanceledException ex) when (attempt < maxRetries)
                {
                    Logger.Warning(ex, "AI API call timed out, attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                        attempt, maxRetries, retryDelay.TotalSeconds);
                    await Task.Delay(retryDelay);
                    retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2);
                }
            }

            throw new InvalidOperationException($"AI API call failed after {maxRetries} attempts");
        }

        /// <summary>
        /// Cache AI report responses to improve performance and reduce costs
        /// </summary>
        private void CacheReportAsync(string reportType, AIReportResponse response)
        {
            var cacheKey = $"AIReport_{reportType}_{DateTime.UtcNow:yyyyMMddHH}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                SlidingExpiration = TimeSpan.FromMinutes(30),
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(cacheKey, response, cacheOptions);
            Logger.Debug("Cached AI report: {CacheKey}", cacheKey);
        }

        /// <summary>
        /// Get cached report if available
        /// </summary>
        public bool TryGetCachedReport(string reportType, out AIReportResponse? cachedReport)
        {
            var cacheKey = $"AIReport_{reportType}_{DateTime.UtcNow:yyyyMMddHH}";
            return _cache.TryGetValue(cacheKey, out cachedReport);
        }

        private static string ExtractContentFromResponse(string responseJson)
        {
            try
            {
                using var document = JsonDocument.Parse(responseJson);
                return document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "Error parsing AI response";
            }
            catch (JsonException ex)
            {
                Logger.Error(ex, "Failed to extract content from AI response");
                return "Error parsing AI response";
            }
        }

        private static TokenUsage ExtractUsageFromResponse(string responseJson)
        {
            try
            {
                using var document = JsonDocument.Parse(responseJson);
                var usage = document.RootElement.GetProperty("usage");
                return new TokenUsage
                {
                    PromptTokens = usage.GetProperty("prompt_tokens").GetInt32(),
                    CompletionTokens = usage.GetProperty("completion_tokens").GetInt32(),
                    TotalTokens = usage.GetProperty("total_tokens").GetInt32()
                };
            }
            catch (JsonException ex)
            {
                Logger.Error(ex, "Failed to extract usage from AI response");
                return new TokenUsage();
            }
        }
    }

    /// <summary>
    /// Transportation context caching service for AI context awareness
    /// </summary>
    public class TransportationContext
    {
        private readonly IMemoryCache _cache;
        private static readonly ILogger Logger = Log.ForContext<TransportationContext>();
        private const string CACHE_KEY_PREFIX = "TransportationContext_";
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        public TransportationContext(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<TransportationData> GetContextDataAsync(string contextId)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{contextId}";

            if (_cache.TryGetValue(cacheKey, out TransportationData? cachedData) && cachedData != null)
            {
                Logger.Debug("Retrieved transportation context from cache: {ContextId}", contextId);
                return cachedData;
            }

            // Fetch from database/service
            var data = await FetchContextDataFromSourceAsync(contextId);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = _cacheDuration,
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(cacheKey, data, cacheOptions);
            Logger.Debug("Cached transportation context: {ContextId}", contextId);

            return data;
        }

        public void InvalidateCache(string contextId)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{contextId}";
            _cache.Remove(cacheKey);
            Logger.Debug("Invalidated transportation context cache: {ContextId}", contextId);
        }

        private static Task<TransportationData> FetchContextDataFromSourceAsync(string contextId)
        {
            // Simulate fetching real transportation data
            var data = new TransportationData
            {
                ContextId = contextId,
                TotalBuses = 25,
                TotalRoutes = 12,
                TotalStudents = 850,
                AverageEfficiency = 87.5,
                ActiveDrivers = 20,
                CurrentWeather = "Clear, 72°F",
                TrafficStatus = "Normal"
            };

            return Task.FromResult(data);
        }
    }

    /// <summary>
    /// Context-aware prompt builder with real-time data integration
    /// </summary>
    public class ContextAwarePromptBuilder
    {
        private static readonly ILogger Logger = Log.ForContext<ContextAwarePromptBuilder>();

        public ContextAwarePromptBuilder()
        {
        }

        public Task<string> BuildReportPromptAsync(string reportType, string location, TransportationData context, Dictionary<string, object> parameters)
        {
            var basePrompt = PromptTemplates.GetReportPrompt(reportType);
            var contextPrompt = BuildContextSection(context);
            var parametersPrompt = BuildParametersSection(parameters);

            var fullPrompt = $@"{basePrompt}

CURRENT SYSTEM CONTEXT:
{contextPrompt}

{parametersPrompt}

Please provide a comprehensive analysis with specific recommendations and actionable insights for the Bus Buddy transportation system.";

            Logger.Debug("Built context-aware prompt for {ReportType}, length: {Length} characters", reportType, fullPrompt.Length);
            return Task.FromResult(fullPrompt);
        }

        private static string BuildContextSection(TransportationData context)
        {
            return $@"- Fleet Size: {context.TotalBuses} buses across {context.TotalRoutes} routes
- Student Population: {context.TotalStudents} students
- System Efficiency: {context.AverageEfficiency:F1}%
- Active Drivers: {context.ActiveDrivers}
- Weather Conditions: {context.CurrentWeather}
- Traffic Status: {context.TrafficStatus}";
        }

        private static string BuildParametersSection(Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return "";
            }

            var parameterLines = new List<string>();
            foreach (var param in parameters)
            {
                parameterLines.Add($"- {param.Key}: {param.Value}");
            }

            return $"ADDITIONAL PARAMETERS:\n{string.Join("\n", parameterLines)}";
        }
    }

    /// <summary>
    /// Custom prompt templates for different Bus Buddy scenarios
    /// </summary>
    public static class PromptTemplates
    {
        public static string GetReportPrompt(string reportType)
        {
            return reportType switch
            {
                "RouteOptimization" => @"Analyze the current bus route system for optimization opportunities. Consider factors like travel time, fuel efficiency, student pickup/dropoff locations, traffic patterns, and driver schedules. Provide specific recommendations for route improvements.",

                "MaintenancePlanning" => @"Evaluate the bus fleet maintenance status and provide predictive maintenance recommendations. Consider vehicle age, mileage, usage patterns, and maintenance history. Prioritize maintenance tasks by urgency and cost-effectiveness.",

                "SafetyAnalysis" => @"Conduct a comprehensive safety analysis of the transportation system. Review driver performance, route safety, weather impact, emergency preparedness, and compliance with safety regulations. Identify risk areas and mitigation strategies.",

                "PerformanceReport" => @"Generate a comprehensive performance report for the Bus Buddy transportation system. Analyze efficiency metrics, on-time performance, cost management, student satisfaction, and operational excellence indicators.",

                "CostOptimization" => @"Analyze transportation costs and identify optimization opportunities. Consider fuel costs, maintenance expenses, driver scheduling, route efficiency, and operational overhead. Provide cost-saving recommendations with projected ROI.",

                "StudentManagement" => @"Review student transportation management including pickup/dropoff efficiency, capacity utilization, special needs accommodation, and parent communication. Suggest improvements for student experience and operational efficiency.",

                _ => @"Provide a general analysis and recommendations for the Bus Buddy school transportation management system. Cover key operational areas including efficiency, safety, cost management, and service quality."
            };
        }

        public static string GetMaintenancePrompt(string vehicleId, string issueDescription)
        {
            return $@"VEHICLE MAINTENANCE ANALYSIS
Vehicle ID: {vehicleId}
Issue Description: {issueDescription}

As Bus Buddy AI maintenance expert, provide:
1. Potential root causes of the issue
2. Step-by-step diagnostic procedures
3. Required tools and parts
4. Safety precautions
5. Estimated repair time and cost
6. Preventive measures to avoid recurrence";
        }

        public static string GetSafetyPrompt(string location, string situation)
        {
            return $@"SAFETY SITUATION ANALYSIS
Location: {location}
Situation: {situation}

As Bus Buddy AI safety specialist, provide:
1. Immediate safety actions required
2. Risk assessment and mitigation strategies
3. Emergency contact procedures
4. Regulatory compliance considerations
5. Communication with stakeholders
6. Follow-up safety measures";
        }

        public static string GetRoutePlanningPrompt(string startLocation, string endLocation, string constraints)
        {
            return $@"ROUTE OPTIMIZATION REQUEST
Start Location: {startLocation}
End Location: {endLocation}
Constraints: {constraints}

As Bus Buddy AI route optimization expert, provide:
1. Primary recommended route with rationale
2. Alternative route options
3. Estimated travel times for each option
4. Fuel efficiency considerations
5. Traffic and weather impact analysis
6. Safety and accessibility factors";
        }
    }

    // Data Models
    public class AIReportResponse
    {
        public required string ReportType { get; set; }
        public required string Content { get; set; }
        public DateTime GeneratedAt { get; set; }
        public required string OperationId { get; set; }
        public required TokenUsage TokenUsage { get; set; }
        public required TransportationData ContextData { get; set; }
    }

    public class AIApiResponse
    {
        public required string Content { get; set; }
        public required TokenUsage Usage { get; set; }
    }

    public class TokenUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public class TransportationData
    {
        public required string ContextId { get; set; }
        public int TotalBuses { get; set; }
        public int TotalRoutes { get; set; }
        public int TotalStudents { get; set; }
        public double AverageEfficiency { get; set; }
        public int ActiveDrivers { get; set; }
        public required string CurrentWeather { get; set; }
        public required string TrafficStatus { get; set; }
    }
}
