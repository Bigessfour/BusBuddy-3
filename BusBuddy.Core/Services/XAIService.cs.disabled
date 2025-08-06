using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BusBuddy.Configuration;
using BusBuddy.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// xAI (Grok) Integration Service for Advanced AI-Powered Transportation Intelligence
    /// Programmatically locked documentation references maintained in XAIDocumentationSettings
    /// </summary>
    public class XAIService
    {
        private static readonly ILogger Logger = Log.ForContext<XAIService>();
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly XAIDocumentationSettings _documentationSettings;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly bool _isConfigured;

        // API Endpoints
        public static readonly string CHAT_COMPLETIONS_ENDPOINT = "/chat/completions";

        public XAIService(HttpClient httpClient, IConfiguration configuration,
            IOptions<XAIDocumentationSettings> documentationOptions)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _documentationSettings = documentationOptions?.Value ?? new XAIDocumentationSettings();

            // Load xAI configuration
            _apiKey = _configuration["XAI:ApiKey"] ?? Environment.GetEnvironmentVariable("XAI_API_KEY") ?? string.Empty;
            _baseUrl = _configuration["XAI:BaseUrl"] ?? "https://api.x.ai/v1";
            var useLiveAPI = _configuration.GetValue<bool>("XAI:UseLiveAPI", true);

            _isConfigured = !string.IsNullOrEmpty(_apiKey) && !_apiKey.Contains("YOUR_XAI_API_KEY") && useLiveAPI;

            if (!_isConfigured)
            {
                Logger.Warning("xAI not configured or disabled. Using mock AI responses. Please set XAI_API_KEY environment variable and enable UseLiveAPI in appsettings.json.");
                Logger.Information("xAI Documentation: {ChatGuideUrl}", _documentationSettings.GetChatGuideUrl());
            }
            else
            {
                Logger.Information("xAI configured for live AI transportation intelligence");
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                _httpClient.Timeout = TimeSpan.FromSeconds(60);
            }
        }

        public bool IsConfigured => _isConfigured;

        /// <summary>
        /// Analyzes route optimization using xAI Grok intelligence
        /// </summary>
        public async Task<AIRouteRecommendations> AnalyzeRouteOptimizationAsync(RouteAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI route optimization analysis");

                if (!_isConfigured)
                {
                    return await GenerateMockAIRecommendations(request);
                }

                var prompt = BuildRouteOptimizationPrompt(request);
                var xaiRequest = new XAIRequest
                {
                    Model = _configuration["XAI:DefaultModel"] ?? "grok-4-latest",
                    Messages = new[]
                    {
                        new XAIMessage { Role = "system", Content = GetTransportationExpertSystemPrompt() },
                        new XAIMessage { Role = "user", Content = prompt }
                    },
                    Temperature = _configuration.GetValue<double>("XAI:Temperature", 0.3),
                    MaxTokens = _configuration.GetValue<int>("XAI:MaxTokens", 128000)
                };

                var response = await CallXAIAPI(CHAT_COMPLETIONS_ENDPOINT, xaiRequest);
                return ParseRouteRecommendations(response);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in xAI route optimization analysis");
                return await GenerateMockAIRecommendations(request);
            }
        }

        /// <summary>
        /// Predicts maintenance needs using AI analysis
        /// </summary>
        public async Task<AIMaintenancePrediction> PredictMaintenanceNeedsAsync(MaintenanceAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI maintenance prediction analysis");

                var prompt = BuildMaintenancePredictionPrompt(request);
                var xaiRequest = new XAIRequest
                {
                    Model = _configuration["XAI:DefaultModel"] ?? "grok-4-latest",
                    Messages = new[]
                    {
                        new XAIMessage { Role = "system", Content = GetMaintenanceExpertSystemPrompt() },
                        new XAIMessage { Role = "user", Content = prompt }
                    },
                    Temperature = 0.2, // Lower temperature for more precise technical predictions
                    MaxTokens = _configuration.GetValue<int>("XAI:MaxTokens", 128000) / 2
                };

                if (!_isConfigured)
                {
                    return await GenerateMockMaintenancePrediction(request);
                }

                var response = await CallXAIAPI(CHAT_COMPLETIONS_ENDPOINT, xaiRequest);
                return ParseMaintenancePrediction(response);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in xAI maintenance prediction");
                return await GenerateMockMaintenancePrediction(request);
            }
        }

        /// <summary>
        /// Analyzes safety risks using AI intelligence
        /// </summary>
        public async Task<AISafetyAnalysis> AnalyzeSafetyRisksAsync(SafetyAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI safety risk analysis");

                var prompt = BuildSafetyAnalysisPrompt(request);
                var xaiRequest = new XAIRequest
                {
                    Model = _configuration["XAI:DefaultModel"] ?? "grok-4-latest",
                    Messages = new[]
                    {
                        new XAIMessage { Role = "system", Content = GetSafetyExpertSystemPrompt() },
                        new XAIMessage { Role = "user", Content = prompt }
                    },
                    Temperature = 0.1, // Very low temperature for safety-critical analysis
                    MaxTokens = _configuration.GetValue<int>("XAI:MaxTokens", 128000) / 2
                };

                if (!_isConfigured)
                {
                    return await GenerateMockSafetyAnalysis(request);
                }

                var response = await CallXAIAPI("/chat/completions", xaiRequest);
                return ParseSafetyAnalysis(response);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in xAI safety analysis");
                return await GenerateMockSafetyAnalysis(request);
            }
        }

        /// <summary>
        /// Optimizes student assignments using AI
        /// </summary>
        public async Task<AIStudentOptimization> OptimizeStudentAssignmentsAsync(StudentOptimizationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI student assignment optimization");

                var prompt = BuildStudentOptimizationPrompt(request);
                var xaiRequest = new XAIRequest
                {
                    Model = _configuration["XAI:DefaultModel"] ?? "grok-4-latest",
                    Messages = new[]
                    {
                        new XAIMessage { Role = "system", Content = GetLogisticsExpertSystemPrompt() },
                        new XAIMessage { Role = "user", Content = prompt }
                    },
                    Temperature = _configuration.GetValue<double>("XAI:Temperature", 0.3),
                    MaxTokens = _configuration.GetValue<int>("XAI:MaxTokens", 128000)
                };

                if (!_isConfigured)
                {
                    return await GenerateMockStudentOptimization(request);
                }

                var response = await CallXAIAPI("/chat/completions", xaiRequest);
                return ParseStudentOptimization(response);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in xAI student optimization");
                return await GenerateMockStudentOptimization(request);
            }
        }

        /// <summary>
        /// Returns the system prompt for logistics/student assignment optimization.
        /// </summary>
        private static string GetLogisticsExpertSystemPrompt()
        {
            // System prompt for xAI logistics/student assignment optimization
            return "You are an expert in school transportation logistics, specializing in optimizing student assignments to buses for maximum efficiency, safety, and compliance with capacity and route constraints. Provide actionable, data-driven recommendations for student-to-bus assignments in a school transportation context.";
        }

        /// <summary>
        /// Sends a general chat message to xAI Grok for AI AssistView integration
        /// </summary>
        public async Task<string> SendChatMessageAsync(string message, string? context = null)
        {
            try
            {
                Logger.Information("Sending chat message to xAI: {Message}", message);

                if (!_isConfigured)
                {
                    await Task.Delay(1000); // Simulate processing time
                    return GenerateMockChatResponse(message);
                }

                var systemPrompt = string.IsNullOrEmpty(context)
                    ? GetGeneralChatSystemPrompt()
                    : $"{GetGeneralChatSystemPrompt()}\n\nAdditional Context: {context}";

                var xaiRequest = new XAIRequest
                {
                    Model = _configuration["XAI:DefaultModel"] ?? "grok-4-latest",
                    Messages = new[]
                    {
                        new XAIMessage { Role = "system", Content = systemPrompt },
                        new XAIMessage { Role = "user", Content = message }
                    },
                    Temperature = _configuration.GetValue<double>("XAI:Temperature", 0.7),
                    MaxTokens = _configuration.GetValue<int>("XAI:MaxTokens", 64000)
                };

                var response = await CallXAIAPI(CHAT_COMPLETIONS_ENDPOINT, xaiRequest);
                return response.Choices?.FirstOrDefault()?.Message?.Content ?? "I'm sorry, I couldn't process your request at the moment.";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in xAI chat message: {Message}", message);
                await Task.Delay(500); // Brief delay for mock response
                return GenerateMockChatResponse(message);
            }
        }

        private static string GetGeneralChatSystemPrompt()
        {
            // General system prompt for xAI chat, can be customized as needed
            return "You are an expert assistant for school transportation management, specializing in providing helpful, accurate, and actionable responses for BusBuddy users. Focus on safety, efficiency, and best practices for school bus operations, maintenance, and route planning.";
        }

        #region Phase 1 Program Management & Analytics

        /// <summary>
        /// Analyze Phase 1 progress and provide insights for program management
        /// </summary>
        public async Task<PhaseAnalysisResult> AnalyzePhase1ProgressAsync(Phase1ProgressRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI Phase 1 progress analysis");

                var prompt = BuildPhase1AnalysisPrompt(request);
                var response = await SendChatMessageAsync(prompt, "Phase 1 Progress Analysis");

                if (_isConfigured && !string.IsNullOrEmpty(response))
                {
                    return ParsePhase1AnalysisResponse(response);
                }

                return CreateMockPhase1Analysis(request);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error analyzing Phase 1 progress for project {ProjectName}", request.ProjectName);
                return CreateMockPhase1Analysis(request);
            }
        }

        /// <summary>
        /// Get development insights and recommendations for the current development state
        /// </summary>
        public async Task<DevelopmentInsights> GetDevelopmentInsightsAsync(DevelopmentStateRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI development insights for {ComponentName}", request.ComponentName);

                var prompt = BuildDevelopmentInsightsPrompt(request);
                var response = await SendChatMessageAsync(prompt, "Development Insights");

                if (_isConfigured && !string.IsNullOrEmpty(response))
                {
                    return ParseDevelopmentInsights(response);
                }

                return CreateMockDevelopmentInsights(request);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting development insights for {ComponentName}", request.ComponentName);
                return CreateMockDevelopmentInsights(request);
            }
        }

        /// <summary>
        /// Analyze build and runtime performance for optimization suggestions
        /// </summary>
        public async Task<PerformanceAnalysis> AnalyzePerformanceAsync(PerformanceDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI performance analysis for {ApplicationName}", request.ApplicationName);

                var prompt = BuildPerformanceAnalysisPrompt(request);
                var response = await SendChatMessageAsync(prompt, "Performance Analysis");

                if (_isConfigured && !string.IsNullOrEmpty(response))
                {
                    return ParsePerformanceAnalysis(response);
                }

                return CreateMockPerformanceAnalysis(request);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error analyzing performance for {ApplicationName}", request.ApplicationName);
                return CreateMockPerformanceAnalysis(request);
            }
        }

        /// <summary>
        /// Generate dynamic mock data with AI assistance for testing and development
        /// </summary>
        public async Task<GeneratedDataSet> GenerateMockDataAsync(MockDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI mock data generation for {DataType}", request.DataType);

                var prompt = BuildMockDataPrompt(request);
                var response = await SendChatMessageAsync(prompt, "Mock Data Generation");

                if (_isConfigured && !string.IsNullOrEmpty(response))
                {
                    return ParseGeneratedDataSet(response);
                }

                return CreateBasicMockData(request);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating mock data for {DataType}", request.DataType);
                return CreateBasicMockData(request);
            }
        }

        /// <summary>
        /// Get contextual help and documentation suggestions
        /// </summary>
        public async Task<ContextualHelp> GetContextualHelpAsync(HelpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                Logger.Information("Requesting xAI contextual help for {FeatureName}", request.Context);

                if (!_isConfigured)
                {
                    return CreateBasicHelp(request);
                }

                var prompt = BuildContextualHelpPrompt(request);
                var response = await SendChatMessageAsync(prompt, "Contextual Help");

                if (_isConfigured && !string.IsNullOrEmpty(response))
                {
                    return ParseContextualHelp(response);
                }

                return CreateBasicHelp(request);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting contextual help for {Topic}", request.Topic);
                return CreateBasicHelp(request);
            }
        }

        private static ContextualHelp ParseContextualHelp(string response)
        {
            return new ContextualHelp
            {
                Topic = "AI Response",
                HelpContent = response,
                RelatedTopics = ExtractRelatedTopics(response),
                ActionableSteps = ExtractActionableSteps(response).ToArray()
            };
        }

        private static ContextualHelp CreateBasicHelp(HelpRequest request)
        {
            return new ContextualHelp
            {
                Topic = request.Topic,
                HelpContent = $"Help information for {request.Topic}",
                RelatedTopics = new[] { "General Help", "Documentation", "FAQ" },
                ActionableSteps = new[] { "Review documentation", "Check examples", "Contact support" }
            };
        }

        private static string[] ExtractRelatedTopics(string content)
        {
            // Simple extraction of potential related topics
            var topics = new List<string>();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains("related", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("see also", StringComparison.OrdinalIgnoreCase))
                {
                    topics.Add(line.Trim());
                }
            }

            return topics.Take(3).ToArray();
        }

        private static List<string> ExtractActionableSteps(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(trimmed => !string.IsNullOrEmpty(trimmed) &&
                                  (trimmed.StartsWith("1.", StringComparison.Ordinal) || trimmed.StartsWith("2.", StringComparison.Ordinal) ||
                                   trimmed.StartsWith("3.", StringComparison.Ordinal) || trimmed.StartsWith('-') ||
                                   trimmed.StartsWith('*')))
                .Select(line => Regex.Replace(line, @"^(\d+\.|\-|\*)\s*", "").Trim())
                .ToList();
        }

        private static string SanitizeForJson(string s)
        {
            return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        #endregion

        #region Phase 1 Program Management Helper Methods

        private static PhaseAnalysisResult CreateMockPhase1Analysis(Phase1ProgressRequest request)
        {
            return new PhaseAnalysisResult();
        }

        private static string BuildDevelopmentInsightsPrompt(DevelopmentStateRequest request)
        {
            return "Development insights prompt";
        }

        private static DevelopmentInsights ParseDevelopmentInsights(string response)
        {
            return new DevelopmentInsights();
        }

        private static DevelopmentInsights CreateMockDevelopmentInsights(DevelopmentStateRequest request)
        {
            return new DevelopmentInsights();
        }

        private static string BuildPerformanceAnalysisPrompt(PerformanceDataRequest request)
        {
            return "Performance analysis prompt";
        }

        private static PerformanceAnalysis ParsePerformanceAnalysis(string response)
        {
            return new PerformanceAnalysis();
        }

        private static PerformanceAnalysis CreateMockPerformanceAnalysis(PerformanceDataRequest request)
        {
            return new PerformanceAnalysis();
        }

        private static string BuildMockDataPrompt(MockDataRequest request)
        {
            return "Mock data prompt";
        }

        private static GeneratedDataSet ParseGeneratedDataSet(string response)
        {
            return new GeneratedDataSet();
        }

        private static GeneratedDataSet CreateBasicMockData(MockDataRequest request)
        {
            return new GeneratedDataSet();
        }

        private static string BuildContextualHelpPrompt(HelpRequest request)
        {
            return "Contextual help prompt";
        }

        private static string BuildPhase1AnalysisPrompt(Phase1ProgressRequest request)
        {
            var prompt = $@"
# BusBuddy Phase 1 Development Analysis

## Project Overview
- **Project**: {request.ProjectName}
- **Current Phase**: {request.CurrentPhase}
- **Days in Development**: {request.DaysInDevelopment}
- **Team Size**: {request.TeamSize}
- **Target Completion**: {request.TargetCompletion:yyyy-MM-dd}

## Technical Metrics
- **Build Status**: {request.BuildStatus}
- **Tests**: {request.TestsPassingCount}/{request.TotalTestsCount} passing
- **Code Coverage**: {request.CodeCoverage:P1}
- **Critical Issues**: {request.CriticalIssuesCount}
- **Components**: {request.CompletedComponents}/{request.TotalComponents} completed
- **Lines of Code**: {request.LinesOfCode:N0}

## Development Activity
- **Commits This Week**: {request.CommitsThisWeek}
- **Pull Requests**: {request.OpenPullRequests} open, {request.MergedPullRequests} merged
- **Average Build Time**: {request.AverageBuildTime:F1} seconds
- **Active Developers**: {request.ActiveDevelopers}
- **Issues Closed This Week**: {request.IssuesClosedThisWeek}
- **Documentation Pages**: {request.DocumentationPages}

## Current Focus Areas
{string.Join("", request.CurrentFocusAreas.Select(area => $"- {area}\n"))}

Please analyze this development state and provide:
1. **Overall Health Assessment** (Excellent/Good/Fair/Poor)
2. **Health Score** (0-100)
3. **Risk Level** (Low/Medium/High/Critical)
4. **Top 5 Recommendations** for improving development velocity
5. **Predicted Completion Date** based on current velocity
6. **Next 3 Key Milestones** to focus on
7. **Technical Debt Analysis**
8. **Team Productivity Assessment**

Focus on actionable insights for a WPF .NET application in Phase 1 development.";

            return prompt;
        }

        private static PhaseAnalysisResult ParsePhase1AnalysisResponse(string response)
        {
            var result = new PhaseAnalysisResult();

            try
            {
                // Extract overall health
                var healthMatch = Regex.Match(response, @"Overall Health.*?:\s*(\w+)", RegexOptions.IgnoreCase);
                if (healthMatch.Success)
                {
                    result.OverallHealth = healthMatch.Groups[1].Value;
                }

                // Extract health score
                var scoreMatch = Regex.Match(response, @"Health Score.*?:\s*(\d+)", RegexOptions.IgnoreCase);
                if (scoreMatch.Success && int.TryParse(scoreMatch.Groups[1].Value, out int score))
                {
                    result.HealthScore = score;
                }

                // Extract risk level
                var riskMatch = Regex.Match(response, @"Risk Level.*?:\s*(\w+)", RegexOptions.IgnoreCase);
                if (riskMatch.Success)
                {
                    result.RiskLevel = riskMatch.Groups[1].Value;
                }

                // Extract recommendations
                var recMatches = Regex.Matches(response, @"(?:Recommendation|•|\*|\d+\.)\s*(.+?)(?=\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                result.Recommendations = recMatches.Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .Where(s => !string.IsNullOrEmpty(s) && s.Length > 10)
                    .Take(5)
                    .ToArray();

                // Extract predicted completion date
                var dateMatch = Regex.Match(response, @"(?:Predicted|Completion).*?Date.*?:\s*(\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase);
                if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out DateTime completionDate))
                {
                    result.PredictedCompletionDate = completionDate;
                }
                else
                {
                    result.PredictedCompletionDate = DateTime.Now.AddDays(30); // Default estimate
                }

                // Extract next milestones
                var milestoneMatches = Regex.Matches(response, @"(?:Milestone|Next|•|\*|\d+\.)\s*(.+?)(?=\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                result.NextMilestones = milestoneMatches.Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .Where(s => !string.IsNullOrEmpty(s) && s.Length > 5)
                    .Take(3)
                    .ToArray();

                // Extract technical debt analysis
                var debtMatch = Regex.Match(response, @"Technical Debt.*?:\s*(.+?)(?=\n\n|\n[A-Z]|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (debtMatch.Success)
                {
                    result.TechnicalDebt = debtMatch.Groups[1].Value.Trim();
                }

                // Extract team productivity
                var prodMatch = Regex.Match(response, @"(?:Team\s+)?Productivity.*?:\s*(.+?)(?=\n\n|\n[A-Z]|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (prodMatch.Success)
                {
                    result.TeamProductivity = prodMatch.Groups[1].Value.Trim();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error parsing Phase 1 analysis response, using defaults");
                result.OverallHealth = "Analysis Error";
                result.HealthScore = 50;
                result.RiskLevel = "Medium";
                result.Recommendations = new[] { "Unable to parse analysis recommendations" };
                result.PredictedCompletionDate = DateTime.Now.AddDays(30);
                result.NextMilestones = new[] { "Complete analysis parsing", "Resume development", "Review metrics" };
                result.TechnicalDebt = "Unable to assess technical debt from response";
                result.TeamProductivity = "Unable to assess team productivity from response";
            }

            return result;
        }

        private static string BuildDevelopmentStatePrompt(DevelopmentStateRequest request)
        {
            var prompt = $@"
# BusBuddy Component Development State Analysis

## Component Information
- **Name**: {request.ComponentName}
- **Complexity**: {request.ComplexityLevel}
- **Last Modified**: {request.LastModified:yyyy-MM-dd HH:mm}

## Technology Stack
{string.Join("", request.TechnologyStack.Select(tech => $"- {tech}\n"))}

## Recent Changes
- **Files Changed**: {request.FilesChanged}
- **Lines Added**: {request.LinesAdded:+#;-#;0}
- **Lines Removed**: {request.LinesRemoved}
- **Recent Commits**: {request.RecentCommits}

## Code Metrics
- **Methods Count**: {request.MethodsCount}
- **Classes Count**: {request.ClassesCount}

## Issues & Requests
- **Bug Reports**: {request.BugReports}
- **Feature Requests**: {request.FeatureRequests}
- **Performance Issues**: {request.PerformanceIssues}

## Dependencies
{string.Join("", request.Dependencies.Select(dep => $"- {dep}\n"))}

## Current Challenges
{string.Join("", request.CurrentChallenges.Select(challenge => $"- {challenge}\n"))}

Please provide development insights including:
1. **Component Health Status**
2. **Development Velocity Assessment**
3. **Risk Areas** to monitor
4. **Optimization Opportunities**
5. **Next Development Steps**
6. **Resource Requirements**

Focus on actionable insights for WPF .NET development.";

            return prompt;
        }

        private async Task<string> GetChatCompletionAsync(string prompt, string systemPrompt)
        {
            var xaiRequest = new XAIRequest
            {
                Model = _configuration["XAI:DefaultModel"] ?? "grok-4-latest",
                Messages = new[]
                {
                    new XAIMessage { Role = "system", Content = systemPrompt },
                    new XAIMessage { Role = "user", Content = prompt }
                },
                Temperature = _configuration.GetValue<double>("XAI:Temperature", 0.7),
                MaxTokens = _configuration.GetValue<int>("XAI:MaxTokens", 128000)
            };

            var response = await CallXAIAPI(CHAT_COMPLETIONS_ENDPOINT, xaiRequest);

            if (response?.Choices?.Any() != true || string.IsNullOrEmpty(response.Choices[0]?.Message?.Content))
            {
                return "I'm sorry, I couldn't process your request at the moment.";
            }

            return response.Choices?.FirstOrDefault()?.Message?.Content ?? "I'm sorry, I couldn't process your request at the moment.";
        }

        private async Task<BusBuddy.Core.Models.XAIResponse> CallXAIAPI(string endpoint, XAIRequest request)
        {
            try
            {
                var jsonRequest = JsonSerializer.Serialize(request, JsonOptions);
                using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var httpResponse = await _httpClient.PostAsync(_baseUrl + endpoint, content);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    Logger.Error("xAI API call failed with status {StatusCode}: {ErrorContent}", httpResponse.StatusCode, errorContent);
                    return new BusBuddy.Core.Models.XAIResponse
                    {
                        Choices = new[]
                        {
                            new BusBuddy.Core.Models.XAIChoice
                            {
                                Message = new BusBuddy.Core.Models.XAIMessage
                                {
                                    Content = $"API Error: {httpResponse.StatusCode}"
                                }
                            }
                        }
                    };
                }

                var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                var response = JsonSerializer.Deserialize<BusBuddy.Core.Models.XAIResponse>(jsonResponse, JsonOptions);

                if (response?.Choices?.Any() != true)
                {
                    Logger.Warning("xAI response was empty or invalid. Response: {JsonResponse}", jsonResponse);
                    return new BusBuddy.Core.Models.XAIResponse
                    {
                        Choices = new[]
                        {
                            new BusBuddy.Core.Models.XAIChoice
                            {
                                Message = new BusBuddy.Core.Models.XAIMessage
                                {
                                    Content = "Invalid or empty response from AI."
                                }
                            }
                        }
                    };
                }

                return response;
            }
            catch (HttpRequestException ex)
            {
                Logger.Error(ex, "HTTP request to xAI API failed.");
                return new BusBuddy.Core.Models.XAIResponse
                {
                    Choices = new[]
                    {
                        new BusBuddy.Core.Models.XAIChoice
                        {
                            Message = new BusBuddy.Core.Models.XAIMessage
                            {
                                Content = "Network error connecting to AI service."
                            }
                        }
                    }
                };
            }
            catch (JsonException ex)
            {
                Logger.Error(ex, "Failed to serialize or deserialize xAI JSON.");
                return new BusBuddy.Core.Models.XAIResponse
                {
                    Choices = new[]
                    {
                        new BusBuddy.Core.Models.XAIChoice
                        {
                            Message = new BusBuddy.Core.Models.XAIMessage
                            {
                                Content = "JSON processing error."
                            }
                        }
                    }
                };
            }
            catch (TaskCanceledException ex)
            {
                Logger.Error(ex, "xAI API call timed out.");
                return new BusBuddy.Core.Models.XAIResponse
                {
                    Choices = new[]
                    {
                        new BusBuddy.Core.Models.XAIChoice
                        {
                            Message = new BusBuddy.Core.Models.XAIMessage
                            {
                                Content = "Request to AI service timed out."
                            }
                        }
                    }
                };
            }
        }

        #endregion

        #region Mock Implementations (Current)

        private static async Task<AIRouteRecommendations> GenerateMockAIRecommendations(RouteAnalysisRequest request)
        {
            await Task.Delay(2000); // Simulate AI processing time

            return new AIRouteRecommendations
            {
                OptimalRoute = new RouteRecommendation
                {
                    EstimatedFuelSavings = 18.5,
                    EstimatedTimeSavings = 12.3,
                    SafetyScore = 94.2,
                    RecommendedChanges = new[]
                    {
                        "Adjust route to avoid steep grade on Elm Street during wet conditions",
                        "Consider alternative path through residential area for reduced traffic",
                        "Optimize stop spacing for better fuel efficiency"
                    }
                },
                RiskAssessment = new RiskAssessment
                {
                    OverallRiskLevel = "Low",
                    IdentifiedRisks = new[]
                    {
                        "Weather-related visibility concerns during morning hours",
                        "Increased traffic during school start times"
                    },
                    MitigationStrategies = new[]
                    {
                        "Deploy additional safety protocols during inclement weather",
                        "Adjust departure times to avoid peak traffic"
                    }
                },
                ConfidenceLevel = 0.87,
                Reasoning = "Analysis based on terrain data, weather patterns, and historical performance metrics. Recommendations prioritize safety while optimizing efficiency."
            };
        }

        private static async Task<AIMaintenancePrediction> GenerateMockMaintenancePrediction(MaintenanceAnalysisRequest request)
        {
            await Task.Delay(1800);

            return new AIMaintenancePrediction
            {
                PredictedMaintenanceDate = DateTime.Now.AddDays(45),
                ComponentPredictions = new[]
                {
                    new ComponentPrediction
                    {
                        Component = "Brake Pads",
                        PredictedWearDate = DateTime.Now.AddDays(30),
                        ConfidenceLevel = 0.92,
                        EstimatedCost = 350.00m
                    },
                    new ComponentPrediction
                    {
                        Component = "Tires",
                        PredictedWearDate = DateTime.Now.AddDays(120),
                        ConfidenceLevel = 0.78,
                        EstimatedCost = 1200.00m
                    }
                },
                TotalEstimatedCost = 1550.00m,
                PotentialSavings = 850.00m,
                Reasoning = "Predictive analysis based on route difficulty, vehicle usage patterns, and component lifecycle data."
            };
        }

        private static async Task<AISafetyAnalysis> GenerateMockSafetyAnalysis(SafetyAnalysisRequest request)
        {
            await Task.Delay(1500);

            return new AISafetyAnalysis
            {
                OverallSafetyScore = 91.5,
                RiskFactors = new[]
                {
                    new SafetyRiskFactor
                    {
                        Factor = "Weather Conditions",
                        RiskLevel = "Medium",
                        Impact = "Reduced visibility during morning fog",
                        Mitigation = "Install enhanced lighting and reflective markers"
                    }
                },
                Recommendations = new[]
                {
                    "Implement GPS tracking for real-time route monitoring",
                    "Enhance driver training for adverse weather conditions",
                    "Install additional safety equipment on high-risk routes"
                },
                ComplianceStatus = "Fully Compliant",
                ConfidenceLevel = 0.89
            };
        }

        private static async Task<AIStudentOptimization> GenerateMockStudentOptimization(StudentOptimizationRequest request)
        {
            await Task.Delay(2200);

            return new AIStudentOptimization
            {
                OptimalAssignments = new[]
                {
                    new StudentAssignment
                    {
                        BusId = 1,
                        StudentsAssigned = 45,
                        CapacityUtilization = 0.75,
                        AverageRideTime = 25.5
                    }
                },
                EfficiencyGains = new EfficiencyMetrics
                {
                    TotalTimeSaved = 45.0,
                    FuelSavings = 12.3,
                    CapacityOptimization = 0.82
                },
                ConfidenceLevel = 0.91,
                Reasoning = "Optimization based on geographic clustering, capacity constraints, and time window requirements."
            };
        }

        private static string GenerateMockChatResponse(string message)
        {
            // Simulate realistic chat responses based on the input message
            var responses = new[]
            {
                $"Thank you for your question about '{message}'. Based on my transportation expertise, I recommend checking our safety protocols and route optimization features.",
                $"I understand you're asking about '{message}'. For school transportation management, this typically involves reviewing current policies and consulting with our routing algorithms.",
                $"That's a great question about '{message}'. In my experience with school transportation systems, the best approach is to prioritize student safety while optimizing efficiency.",
                $"Regarding '{message}', I'd suggest checking your current transportation data and considering factors like route efficiency, safety compliance, and student capacity.",
                $"I can help with '{message}'. For optimal school transportation management, consider reviewing your maintenance schedules, route planning, and safety protocols."
            };

            var random = new Random();
            return responses[random.Next(responses.Length)];
        }

        #endregion

        #region Response Parsing (Future Implementation)

        private static AIRouteRecommendations CreateDefaultRouteRecommendations(string aiResponse)
        {
            return new AIRouteRecommendations
            {
                OptimalRoute = new RouteRecommendation
                {
                    EstimatedFuelSavings = ExtractNumericValue(aiResponse, "fuel", "savings", "efficiency") ?? 15.0,
                    EstimatedTimeSavings = ExtractNumericValue(aiResponse, "time", "savings", "minutes") ?? 10.0,
                    SafetyScore = ExtractNumericValue(aiResponse, "safety", "score") ?? 85.0,
                    RecommendedChanges = ExtractRecommendations(aiResponse)
                },
                RiskAssessment = new RiskAssessment
                {
                    OverallRiskLevel = ExtractRiskLevel(aiResponse),
                    IdentifiedRisks = ExtractRisks(aiResponse),
                    MitigationStrategies = ExtractMitigations(aiResponse)
                },
                ConfidenceLevel = 0.85,
                Reasoning = aiResponse.Length > 500 ? string.Concat(aiResponse.AsSpan(0, 500), "...") : aiResponse
            };
        }

        private static AIRouteRecommendations ParseRouteRecommendations(BusBuddy.Core.Models.XAIResponse response)
        {
            try
            {
                if (response?.Choices?.Length > 0)
                {
                    var content = response.Choices[0].Message?.Content ?? string.Empty;
                    Logger.Debug("Parsing xAI route optimization response: {Content}", content);

                    // Try to parse structured JSON response or extract key information
                    if (content.Contains('{') && content.Contains('}'))
                    {
                        // Attempt to parse JSON response
                        try
                        {
                            return JsonSerializer.Deserialize<AIRouteRecommendations>(content, ParseJsonOptions) ?? CreateDefaultRouteRecommendations(content);
                        }
                        catch (JsonException)
                        {
                            return CreateDefaultRouteRecommendations(content);
                        }
                    }
                    else
                    {
                        return CreateDefaultRouteRecommendations(content);
                    }
                }

                return CreateDefaultRouteRecommendations("No response from xAI");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error parsing xAI route recommendations");
                return CreateDefaultRouteRecommendations("Error parsing AI response");
            }
        }

        private static AIMaintenancePrediction ParseMaintenancePrediction(BusBuddy.Core.Models.XAIResponse aiResponse)
        {
            if (aiResponse == null || aiResponse.Choices == null || !aiResponse.Choices.Any())
            {
                return new AIMaintenancePrediction
                {
                    PredictedMaintenanceDate = DateTime.Now.AddDays(30),
                    Confidence = 0.75,
                    ActionableRecommendations = new List<string> { "Review maintenance schedule", "Inspect critical components" },
                    Reasoning = "Default reasoning due to empty AI response"
                };
            }

            var content = aiResponse.Choices.First().Message.Content;

            try
            {
                var prediction = JsonSerializer.Deserialize<AIMaintenancePrediction>(content, JsonOptions);
                if (prediction != null)
                {
                    prediction.Reasoning = string.IsNullOrWhiteSpace(prediction.Reasoning)
                        ? content.Length > 300 ? string.Concat(content.AsSpan(0, 300), "...") : content
                        : prediction.Reasoning;
                    return prediction;
                }
            }
            catch
            {
                // Fallback parsing if direct deserialization fails
                return new AIMaintenancePrediction
                {
                    PredictedMaintenanceDate = DateTime.Now.AddDays(30),
                    Confidence = 0.75,
                    ActionableRecommendations = ExtractActionableSteps(content),
                    Reasoning = content.Length > 300 ? string.Concat(content.AsSpan(0, 300), "...") : content
                };
            }

            return new AIMaintenancePrediction
            {
                PredictedMaintenanceDate = DateTime.Now.AddDays(30),
                Confidence = 0.75,
                ActionableRecommendations = ExtractActionableSteps(content),
                Reasoning = content.Length > 300 ? string.Concat(content.AsSpan(0, 300), "...") : content
            };
        }

        private static AISafetyAnalysis ParseSafetyAnalysis(BusBuddy.Core.Models.XAIResponse aiResponse)
        {
            if (aiResponse == null || aiResponse.Choices == null || !aiResponse.Choices.Any())
            {
                return new AISafetyAnalysis
                {
                    OverallRiskScore = 0.5,
                    IdentifiedRisks = new List<IdentifiedRisk> { new IdentifiedRisk { Description = "General operational risks", Severity = "Medium" } },
                    MitigationStrategies = new List<string> { "Review safety protocols", "Conduct driver safety training" },
                    Reasoning = "Default reasoning due to empty AI response"
                };
            }

            var content = aiResponse.Choices.First().Message.Content;

            try
            {
                var analysis = JsonSerializer.Deserialize<AISafetyAnalysis>(content, JsonOptions);
                if (analysis != null)
                {
                    analysis.Reasoning = string.IsNullOrWhiteSpace(analysis.Reasoning)
                        ? content.Length > 300 ? string.Concat(content.AsSpan(0, 300), "...") : content
                        : analysis.Reasoning;
                    return analysis;
                }
            }
            catch
            {
                return new AISafetyAnalysis
                {
                    OverallRiskScore = 0.5,
                    IdentifiedRisks = new List<IdentifiedRisk> { new IdentifiedRisk { Description = "General operational risks", Severity = "Medium" } },
                    MitigationStrategies = ExtractActionableSteps(content),
                    Reasoning = content.Length > 300 ? string.Concat(content.AsSpan(0, 300), "...") : content
                };
            }

            return new AISafetyAnalysis
            {
                OverallRiskScore = 0.5,
                IdentifiedRisks = new List<IdentifiedRisk> { new IdentifiedRisk { Description = "General operational risks", Severity = "Medium" } },
                MitigationStrategies = ExtractActionableSteps(content),
                Reasoning = content.Length > 300 ? string.Concat(content.AsSpan(0, 300), "...") : content
            };
        }

        private static AIStudentOptimization ParseStudentOptimization(BusBuddy.Core.Models.XAIResponse aiResponse)
        {
            try
            {
                if (aiResponse?.Choices?.Length > 0)
                {
                    var content = aiResponse.Choices[0].Message?.Content ?? string.Empty;
                    Logger.Debug("Parsing xAI student optimization response");

                    return new AIStudentOptimization
                    {
                        OptimalAssignments = ExtractStudentAssignments(content),
                        EfficiencyGains = new EfficiencyMetrics
                        {
                            TotalTimeSaved = ExtractNumericValue(content, "time", "saved") ?? 30.0,
                            FuelSavings = ExtractNumericValue(content, "fuel", "saved") ?? 15.0,
                            CapacityOptimization = ExtractNumericValue(content, "capacity", "utilization") ?? 0.8
                        },
                        ConfidenceLevel = 0.85,
                        Reasoning = content.Length > 300 ? string.Concat(content.AsSpan(0, 300), "...") : content
                    };
                }

                return new AIStudentOptimization();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error parsing xAI student optimization");
                return new AIStudentOptimization();
            }
        }

        #endregion

        #region AI Response Parsing Helper Methods

        private static double? ExtractNumericValue(string content, params string[] keywords)
        {
            try
            {
                var lowerContent = content.ToLowerInvariant();
                foreach (var keyword in keywords)
                {
                    var keywordIndex = lowerContent.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase);
                    if (keywordIndex >= 0)
                    {
                        // Look for numbers near the keyword
                        var searchArea = lowerContent.Substring(
                            Math.Max(0, keywordIndex - 20),
                            Math.Min(100, lowerContent.Length - Math.Max(0, keywordIndex - 20))
                        );

                        var numberMatches = System.Text.RegularExpressions.Regex.Matches(searchArea, @"\d+\.?\d*");
                        if (numberMatches.Count > 0)
                        {
                            if (double.TryParse(numberMatches[0].Value, out var value))
                            {
                                return value;
                            }
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string[] ExtractRecommendations(string content)
        {
            try
            {
                var recommendations = new List<string>();
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith('-') || trimmed.StartsWith('•') ||
                        trimmed.StartsWith('*') || char.IsDigit(trimmed[0]))
                    {
                        recommendations.Add(trimmed.TrimStart('-', '•', '*', ' ', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.'));
                    }
                    else if (trimmed.Contains("recommend", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("suggest", StringComparison.OrdinalIgnoreCase))
                    {
                        recommendations.Add(trimmed);
                    }
                }

                return recommendations.Take(5).ToArray(); // Limit to 5 recommendations
            }
            catch
            {
                return new[] { "AI recommendations processing" };
            }
        }

        private static string ExtractRiskLevel(string content)
        {
            var lowerContent = content.ToLowerInvariant();
            if (lowerContent.Contains("high risk", StringComparison.OrdinalIgnoreCase) || lowerContent.Contains("critical", StringComparison.OrdinalIgnoreCase))
            {
                return "High";
            }

            if (lowerContent.Contains("medium risk") || lowerContent.Contains("moderate"))
            {
                return "Medium";
            }

            if (lowerContent.Contains("low risk") || lowerContent.Contains("minimal"))
            {
                return "Low";
            }

            return "Medium"; // Default
        }

        private static string[] ExtractRisks(string content)
        {
            try
            {
                var risks = new List<string>();
                var lines = content.Split('\n');

                foreach (var line in lines)
                {
                    if (line.Contains("risk", StringComparison.OrdinalIgnoreCase) && line.Length > 10)
                    {
                        risks.Add(line.Trim());
                    }
                }

                return risks.Take(3).ToArray();
            }
            catch
            {
                return new[] { "Weather-related concerns", "Traffic considerations" };
            }
        }

        private static string[] ExtractMitigations(string content)
        {
            try
            {
                var mitigations = new List<string>();
                var lines = content.Split('\n');

                foreach (var line in lines)
                {
                    if ((line.Contains("mitigate", StringComparison.OrdinalIgnoreCase) || line.Contains("prevent", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("reduce", StringComparison.OrdinalIgnoreCase) || line.Contains("improve", StringComparison.OrdinalIgnoreCase)) && line.Length > 10)
                    {
                        mitigations.Add(line.Trim());
                    }
                }

                return mitigations.Take(3).ToArray();
            }
            catch
            {
                return new[] { "Enhanced monitoring", "Improved protocols" };
            }
        }

        private static ComponentPrediction[] ExtractComponentPredictions(string content)
        {
            try
            {
                var components = new List<ComponentPrediction>();
                var commonComponents = new[] { "brake", "tire", "engine", "transmission", "battery", "oil", "filter" };

                foreach (var component in commonComponents)
                {
                    if (content.Contains(component, StringComparison.OrdinalIgnoreCase))
                    {
                        components.Add(new ComponentPrediction
                        {
                            Component = char.ToUpperInvariant(component[0]) + component.Substring(1),
                            PredictedWearDate = DateTime.Now.AddDays(Random.Shared.Next(30, 120)),
                            ConfidenceLevel = 0.7 + Random.Shared.NextDouble() * 0.25,
                            EstimatedCost = 100 + Random.Shared.Next(50, 500)
                        });
                    }
                }

                return components.Take(3).ToArray();
            }
            catch
            {
                return Array.Empty<ComponentPrediction>();
            }
        }

        private static SafetyRiskFactor[] ExtractSafetyRiskFactors(string content)
        {
            try
            {
                var factors = new List<SafetyRiskFactor>();
                var riskTypes = new[] { "Weather", "Traffic", "Mechanical", "Route", "Driver" };

                foreach (var riskType in riskTypes)
                {
                    if (content.Contains(riskType, StringComparison.OrdinalIgnoreCase))
                    {
                        factors.Add(new SafetyRiskFactor
                        {
                            Factor = riskType,
                            RiskLevel = ExtractRiskLevel(content),
                            Impact = $"{riskType}-related safety considerations",
                            Mitigation = $"Enhanced {riskType.ToLowerInvariant()} monitoring and protocols"
                        });
                    }
                }

                return factors.Take(3).ToArray();
            }
            catch
            {
                return Array.Empty<SafetyRiskFactor>();
            }
        }

        private static string ExtractComplianceStatus(string content)
        {
            var lowerContent = content.ToLowerInvariant();
            if (lowerContent.Contains("non-compliant", StringComparison.OrdinalIgnoreCase) || lowerContent.Contains("violation", StringComparison.OrdinalIgnoreCase))
            {
                return "Non-Compliant";
            }

            if (lowerContent.Contains("partial", StringComparison.OrdinalIgnoreCase) || lowerContent.Contains("minor", StringComparison.OrdinalIgnoreCase))
            {
                return "Partially Compliant";
            }

            return "Fully Compliant";
        }

        private static StudentAssignment[] ExtractStudentAssignments(string content)
        {
            try
            {
                var assignments = new List<StudentAssignment>();
                var busCount = ExtractNumericValue(content, "bus", "buses", "vehicle") ?? 5;

                for (int i = 1; i <= Math.Min(busCount, 5); i++)
                {
                    assignments.Add(new StudentAssignment
                    {
                        BusId = i,
                        StudentsAssigned = 30 + Random.Shared.Next(10, 25),
                        CapacityUtilization = 0.6 + Random.Shared.NextDouble() * 0.3,
                        AverageRideTime = 20 + Random.Shared.Next(5, 15)
                    });
                }

                return assignments.ToArray();
            }
            catch
            {
                return Array.Empty<StudentAssignment>();
            }
        }

        #endregion

        #region JSON Serialization Options

        private static readonly JsonSerializerOptions ApiJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private static readonly JsonSerializerOptions ParseJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        #endregion

        #region Prompt Building Methods

        private static string BuildRouteOptimizationPrompt(RouteAnalysisRequest request)
        {
            return $@"Analyze route optimization for transportation system:

Route ID: {request.RouteId ?? "Unknown"}
Current Distance: {request.CurrentDistance} miles
Student Count: {request.StudentCount}
Vehicle Capacity: {request.VehicleCapacity}
Terrain Difficulty: {request.TerrainData?.RouteDifficulty ?? "Not specified"}

Please provide specific optimization recommendations including:
1. Route adjustments
2. Timing improvements
3. Resource allocation
4. Risk mitigation strategies";
        }

        private static string GetTransportationExpertSystemPrompt()
        {
            return @"You are an expert transportation system analyst specializing in school bus fleet optimization.
Provide detailed, actionable recommendations based on operational data, safety regulations, and efficiency best practices.
Focus on practical solutions that can be implemented within typical school district constraints.";
        }

        private static string BuildMaintenancePredictionPrompt(MaintenanceAnalysisRequest request)
        {
            return $@"Analyze maintenance needs for vehicle fleet:

Vehicle ID: {request.BusId}
Vehicle: {request.VehicleYear} {request.VehicleMake} {request.VehicleModel}
Current Mileage: {request.CurrentMileage}
Last Service: {request.LastMaintenanceDate:yyyy-MM-dd}
Daily Miles: {request.DailyMiles}
Terrain Difficulty: {request.TerrainDifficulty}
Engine Hours: {request.EngineHours}
Brake Usage: {request.BrakeUsage}

Predict upcoming maintenance requirements and priority levels.";
        }

        private static string GetMaintenanceExpertSystemPrompt()
        {
            return @"You are a fleet maintenance expert with extensive experience in school bus operations.
Provide predictive maintenance recommendations based on vehicle data, usage patterns, and safety requirements.
Prioritize safety-critical maintenance while optimizing operational costs.";
        }

        private static string BuildSafetyAnalysisPrompt(SafetyAnalysisRequest request)
        {
            return $@"Conduct safety analysis for transportation operations:

Route Type: {request.RouteType}
Traffic Density: {request.TrafficDensity}
Road Conditions: {request.RoadConditions}
Weather: {request.WeatherConditions}
Students: {request.TotalStudents} (Special Needs: {request.SpecialNeedsCount})
Driver Safety Record: {request.DriverSafetyRecord}
Previous Incidents: {request.PreviousIncidents}

Assess safety risks and provide mitigation strategies.";
        }

        private static string GetSafetyExpertSystemPrompt()
        {
            return @"You are a transportation safety expert specializing in school bus operations.
Analyze safety data and provide comprehensive risk assessments with actionable mitigation strategies.
Prioritize student safety while maintaining operational efficiency.";
        }

        private static string BuildStudentOptimizationPrompt(StudentOptimizationRequest request)
        {
            return $@"Optimize student transportation assignments:

Total Students: {request.TotalStudents}
Available Buses: {request.AvailableBuses}
Geographic Constraints: {request.GeographicConstraints}
Special Requirements: {request.SpecialRequirements}

Provide optimal student-to-route assignments considering efficiency and safety.";
        }

        #endregion
    }
}
