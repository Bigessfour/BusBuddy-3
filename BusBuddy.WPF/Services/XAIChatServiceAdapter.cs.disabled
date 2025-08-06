using System;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Services;
using Serilog;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Adapter service that wraps OptimizedXAIService to implement IXAIChatService
    /// This provides a consistent interface while leveraging the performance-optimized core service
    /// </summary>
    public class XAIChatServiceAdapter : IXAIChatService
    {
        private readonly OptimizedXAIService _optimizedXAIService;
        private static readonly ILogger Logger = Log.ForContext<XAIChatServiceAdapter>();

        public XAIChatServiceAdapter(OptimizedXAIService optimizedXAIService)
        {
            _optimizedXAIService = optimizedXAIService ?? throw new ArgumentNullException(nameof(optimizedXAIService));
            Logger.Information("🚀 XAI Chat Service Adapter initialized with OptimizedXAIService backend");
        }

        /// <summary>
        /// Get an AI response to a user message using the optimized service
        /// </summary>
        public async Task<string> GetResponseAsync(string userMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userMessage))
                {
                    return "I'm here to help! Please ask me a question about transportation management, bus scheduling, or any other BusBuddy features.";
                }

                // Use the optimized service for the actual API call
                var response = await _optimizedXAIService.ProcessRequestAsync(userMessage);

                Logger.Information("✅ XAI Chat response generated successfully for message length: {MessageLength}", userMessage.Length);
                return response;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Failed to get XAI Chat response");
                return "I apologize, but I'm having trouble processing your request right now. The XAI service may be temporarily unavailable. Please try again in a moment.";
            }
        }

        /// <summary>
        /// Check if the AI service is available
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // Use a simple test prompt to check availability
                var testResponse = await _optimizedXAIService.ProcessRequestAsync("Test");
                return !string.IsNullOrEmpty(testResponse);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Initialize the AI service (delegated to OptimizedXAIService)
        /// </summary>
        public Task InitializeAsync()
        {
            Logger.Information("🎯 XAI Chat Service Adapter initialization - using OptimizedXAIService");
            // OptimizedXAIService initializes itself, so this is a no-op
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get token usage information from the optimized service
        /// </summary>
        public Task<string> GetTokenUsageAsync()
        {
            try
            {
                // This would require exposing token budget info from OptimizedXAIService
                // For now, return a placeholder
                return Task.FromResult("Token usage monitoring available through OptimizedXAIService");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not retrieve token usage information");
                return Task.FromResult("Token usage information unavailable");
            }
        }

        /// <summary>
        /// Clear the cache in the optimized service
        /// </summary>
        public async Task ClearCacheAsync()
        {
            try
            {
                // This would require exposing cache clearing from OptimizedXAIService
                Logger.Information("Cache clear requested - delegating to OptimizedXAIService");
                await Task.CompletedTask; // Placeholder until cache interface is exposed
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not clear XAI service cache");
            }
        }
    }
}
