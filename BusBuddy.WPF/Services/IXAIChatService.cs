using System.Threading.Tasks;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Service interface for XAI Chat functionality
    /// Provides AI-powered chat assistance for transportation management
    /// </summary>
    public interface IXAIChatService
    {
        /// <summary>
        /// Get an AI response to a user message
        /// </summary>
        /// <param name="userMessage">The user's message</param>
        /// <returns>AI response message</returns>
        Task<string> GetResponseAsync(string userMessage);

        /// <summary>
        /// Check if the AI service is available
        /// </summary>
        /// <returns>True if service is available</returns>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Initialize the AI service
        /// </summary>
        Task InitializeAsync();
    }
}
