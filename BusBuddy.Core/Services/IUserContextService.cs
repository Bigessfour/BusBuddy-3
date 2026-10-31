namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Operator identity for audit fields on a single-clerk workstation.
    /// BusBuddy has no in-app login. The Windows session account is the default actor.
    /// </summary>
    public interface IUserContextService
    {
        /// <summary>
        /// Gets the bound operator identifier (Windows account by default).
        /// </summary>
        string CurrentUserId { get; }

        /// <summary>
        /// Gets the bound operator display name.
        /// </summary>
        string CurrentUserName { get; }

        /// <summary>
        /// Gets the bound operator email (synthetic local address when unset).
        /// </summary>
        string CurrentUserEmail { get; }

        /// <summary>
        /// True when an operator identity is bound for this process.
        /// Not an authentication check — anyone who can launch the app can use it.
        /// </summary>
        bool HasOperatorIdentity { get; }

        /// <summary>
        /// Rebinds the operator identity used on audit columns.
        /// </summary>
        /// <param name="userId">Operator identifier</param>
        /// <param name="userName">Operator display name</param>
        /// <param name="userEmail">Operator email (may be empty)</param>
        void SetCurrentUser(string userId, string userName, string userEmail);

        /// <summary>
        /// Clears the bound operator identity. Audit writes then use <c>SYSTEM</c>
        /// until rebound or the process restarts (constructor rebinds the Windows account).
        /// </summary>
        void ClearCurrentUser();

        /// <summary>
        /// Gets the string to store on CreatedBy / UpdatedBy style audit fields.
        /// </summary>
        /// <returns>Bound operator id, or <c>SYSTEM</c> when unbound</returns>
        string GetCurrentUserForAudit();
    }
}
