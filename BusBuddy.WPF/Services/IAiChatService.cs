using System.Threading.Tasks;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Local AI chat for transportation operators. Implemented by <see cref="OllamaChatService"/>.
    /// </summary>
    public interface IAiChatService
    {
        Task<string> GetResponseAsync(string userMessage);

        Task<bool> IsAvailableAsync();

        Task InitializeAsync();
    }
}
