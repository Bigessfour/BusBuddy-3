namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service for managing current user context and authentication
    /// </summary>
    public interface IUserContextService
    {
        /// <summary>
        /// Gets the current authenticated user's identifier
        /// </summary>
        string CurrentUserId { get; }

        /// <summary>
        /// Gets the current authenticated user's display name
        /// </summary>
        string CurrentUserName { get; }

        /// <summary>
        /// Gets the current authenticated user's email
        /// </summary>
        string CurrentUserEmail { get; }

        /// <summary>
        /// Checks if a user is currently authenticated
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Sets the current user context (for future authentication integration)
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="userName">User display name</param>
        /// <param name="userEmail">User email</param>
        void SetCurrentUser(string userId, string userName, string userEmail);

        /// <summary>
        /// Clears the current user context (logout)
        /// </summary>
        void ClearCurrentUser();

        /// <summary>
        /// Gets the current user for audit trail purposes
        /// </summary>
        /// <returns>User identifier for database audit fields</returns>
        string GetCurrentUserForAudit();
    }
}
