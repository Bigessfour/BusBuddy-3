using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Workstation operator identity for audit trails.
    /// Threat model: one locked-down clerk PC. OS account = audit actor. No app login.
    /// </summary>
    public class UserContextService : IUserContextService
    {
        private static readonly ILogger Logger = Log.ForContext<UserContextService>();
        private string _currentUserId = string.Empty;
        private string _currentUserName = string.Empty;
        private string _currentUserEmail = string.Empty;

        public UserContextService()
        {
            BindWindowsSessionOperator();
        }

        /// <inheritdoc />
        public string CurrentUserId => _currentUserId;

        /// <inheritdoc />
        public string CurrentUserName => _currentUserName;

        /// <inheritdoc />
        public string CurrentUserEmail => _currentUserEmail;

        /// <inheritdoc />
        public bool HasOperatorIdentity => !string.IsNullOrEmpty(_currentUserId);

        /// <inheritdoc />
        public void SetCurrentUser(string userId, string userName, string userEmail)
        {
            _currentUserId = userId ?? throw new ArgumentNullException(nameof(userId));
            _currentUserName = userName ?? throw new ArgumentNullException(nameof(userName));
            _currentUserEmail = userEmail ?? string.Empty;

            Logger.Information("Operator identity bound: {UserId} ({UserName})", userId, userName);
        }

        /// <inheritdoc />
        public void ClearCurrentUser()
        {
            var previousUser = _currentUserId;
            _currentUserId = string.Empty;
            _currentUserName = string.Empty;
            _currentUserEmail = string.Empty;

            Logger.Information("Operator identity cleared (was {UserId}); audit actor is SYSTEM until rebound", previousUser);
        }

        /// <inheritdoc />
        public string GetCurrentUserForAudit()
        {
            if (HasOperatorIdentity)
            {
                return _currentUserId;
            }

            return "SYSTEM";
        }

        /// <summary>
        /// Binds the interactive Windows session account as the clerk operator.
        /// </summary>
        private void BindWindowsSessionOperator()
        {
            var user = Environment.UserName;
            if (string.IsNullOrWhiteSpace(user))
            {
                user = "SYSTEM";
            }

            SetCurrentUser(
                userId: user,
                userName: user,
                userEmail: $"{user}@busbuddy.local"
            );

            Logger.Debug("Default operator identity bound from Windows session account");
        }
    }
}
