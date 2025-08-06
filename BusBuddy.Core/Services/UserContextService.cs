using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Implementation of user context service for Bus Buddy application
    /// Provides current user information for audit trails and authentication
    /// </summary>
    public class UserContextService : IUserContextService
    {
        private static readonly ILogger Logger = Log.ForContext<UserContextService>();
        private string _currentUserId = string.Empty;
        private string _currentUserName = string.Empty;
        private string _currentUserEmail = string.Empty;

        public UserContextService()
        {
            // For now, set a default system user
            // TODO: Replace with actual authentication when implemented
            SetDefaultUser();
        }

        /// <summary>
        /// Gets the current authenticated user's identifier
        /// </summary>
        public string CurrentUserId => _currentUserId;

        /// <summary>
        /// Gets the current authenticated user's display name
        /// </summary>
        public string CurrentUserName => _currentUserName;

        /// <summary>
        /// Gets the current authenticated user's email
        /// </summary>
        public string CurrentUserEmail => _currentUserEmail;

        /// <summary>
        /// Checks if a user is currently authenticated
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_currentUserId);

        /// <summary>
        /// Sets the current user context
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="userName">User display name</param>
        /// <param name="userEmail">User email</param>
        public void SetCurrentUser(string userId, string userName, string userEmail)
        {
            _currentUserId = userId ?? throw new ArgumentNullException(nameof(userId));
            _currentUserName = userName ?? throw new ArgumentNullException(nameof(userName));
            _currentUserEmail = userEmail ?? string.Empty;

            Logger.Information("User context set for user: {UserId} ({UserName})", userId, userName);
        }

        /// <summary>
        /// Clears the current user context (logout)
        /// </summary>
        public void ClearCurrentUser()
        {
            var previousUser = _currentUserId;
            _currentUserId = string.Empty;
            _currentUserName = string.Empty;
            _currentUserEmail = string.Empty;

            Logger.Information("User context cleared for user: {UserId}", previousUser);
        }

        /// <summary>
        /// Gets the current user for audit trail purposes
        /// </summary>
        /// <returns>User identifier for database audit fields</returns>
        public string GetCurrentUserForAudit()
        {
            if (IsAuthenticated)
            {
                return _currentUserId;
            }

            // Fallback to system user if no user is authenticated
            return "SYSTEM";
        }

        /// <summary>
        /// Sets up a default system user for initial operation
        /// </summary>
        private void SetDefaultUser()
        {
            // For development and initial deployment, use a default administrator account
            // This should be replaced with proper authentication in production
            SetCurrentUser(
                userId: "ADMIN_001",
                userName: $"Administrator ({Environment.UserName})",
                userEmail: "admin@busbuddy.local"
            );

            Logger.Debug("Default user context established for development");
        }
    }
}
