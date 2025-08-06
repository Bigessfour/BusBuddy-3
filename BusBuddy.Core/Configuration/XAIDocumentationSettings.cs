namespace BusBuddy.Configuration
{
    /// <summary>
    /// Configuration settings for xAI API documentation URLs
    /// Provides programmatic access to official xAI documentation links
    /// </summary>
    public class XAIDocumentationSettings
    {
        /// <summary>
        /// Base URL for xAI documentation (https://docs.x.ai)
        /// </summary>
        public string BaseUrl { get; set; } = "https://docs.x.ai";

        /// <summary>
        /// Relative path to the overview guide (/docs/overview)
        /// </summary>
        public string OverviewGuide { get; set; } = "/docs/overview";

        /// <summary>
        /// Relative path to the chat API guide (/docs/guides/chat)
        /// </summary>
        public string ChatGuide { get; set; } = "/docs/guides/chat";

        /// <summary>
        /// Relative path to the API reference (/docs/api-reference)
        /// </summary>
        public string ApiReference { get; set; } = "/docs/api-reference";

        /// <summary>
        /// Relative path to the models guide (/docs/models)
        /// </summary>
        public string ModelsGuide { get; set; } = "/docs/models";

        /// <summary>
        /// Relative path to the authentication guide (/docs/authentication)
        /// </summary>
        public string AuthenticationGuide { get; set; } = "/docs/authentication";

        /// <summary>
        /// Relative path to the rate limits guide (/docs/rate-limits)
        /// </summary>
        public string RateLimitsGuide { get; set; } = "/docs/rate-limits";

        /// <summary>
        /// Last updated date for documentation tracking
        /// </summary>
        public string LastUpdated { get; set; } = "2025-07-04";

        /// <summary>
        /// Documentation version
        /// </summary>
        public string Version { get; set; } = "v1";

        /// <summary>
        /// Gets the full URL for a relative documentation path
        /// </summary>
        /// <param name="relativePath">The relative path to append to the base URL</param>
        /// <returns>The complete documentation URL</returns>
        public string GetFullUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {

                return BaseUrl;
            }


            return $"{BaseUrl.TrimEnd('/')}{relativePath}";
        }

        /// <summary>
        /// Gets the full URL for the overview guide
        /// </summary>
        public string GetOverviewUrl() => GetFullUrl(OverviewGuide);

        /// <summary>
        /// Gets the full URL for the chat API guide
        /// </summary>
        public string GetChatGuideUrl() => GetFullUrl(ChatGuide);

        /// <summary>
        /// Gets the full URL for the API reference
        /// </summary>
        public string GetApiReferenceUrl() => GetFullUrl(ApiReference);

        /// <summary>
        /// Gets the full URL for the models guide
        /// </summary>
        public string GetModelsGuideUrl() => GetFullUrl(ModelsGuide);

        /// <summary>
        /// Gets the full URL for the authentication guide
        /// </summary>
        public string GetAuthenticationGuideUrl() => GetFullUrl(AuthenticationGuide);

        /// <summary>
        /// Gets the full URL for the rate limits guide
        /// </summary>
        public string GetRateLimitsGuideUrl() => GetFullUrl(RateLimitsGuide);

        /// <summary>
        /// Validates that all documentation URLs are properly formatted
        /// </summary>
        /// <returns>True if all URLs are valid, false otherwise</returns>
        public bool ValidateUrls()
        {
            try
            {
                var urlsToCheck = new[]
                {
                    GetOverviewUrl(),
                    GetChatGuideUrl(),
                    GetApiReferenceUrl(),
                    GetModelsGuideUrl(),
                    GetAuthenticationGuideUrl(),
                    GetRateLimitsGuideUrl()
                };

                foreach (var url in urlsToCheck)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                    {

                        return false;
                    }

                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all documentation URLs as a dictionary
        /// </summary>
        /// <returns>Dictionary of documentation name to URL mappings</returns>
        public Dictionary<string, string> GetAllUrls()
        {
            return new Dictionary<string, string>
            {
                { "Overview", GetOverviewUrl() },
                { "ChatGuide", GetChatGuideUrl() },
                { "ApiReference", GetApiReferenceUrl() },
                { "ModelsGuide", GetModelsGuideUrl() },
                { "AuthenticationGuide", GetAuthenticationGuideUrl() },
                { "RateLimitsGuide", GetRateLimitsGuideUrl() }
            };
        }
    }
}
