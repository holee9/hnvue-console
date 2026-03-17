using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// User service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-08 System Configuration (Users section).
/// SPEC-SECURITY-001: Authentication/Authorization (RBAC), Session Management, Account Lockout.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets all users.
    /// </summary>
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct);

    /// <summary>
    /// Gets current user information.
    /// </summary>
    Task<User> GetCurrentUserAsync(CancellationToken ct);

    /// <summary>
    /// Gets current user role.
    /// </summary>
    Task<UserRole> GetCurrentUserRoleAsync(CancellationToken ct);

    /// <summary>
    /// Checks if user can access a configuration section.
    /// </summary>
    Task<bool> CanAccessSectionAsync(ConfigSection section, CancellationToken ct);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    Task CreateUserAsync(User user, string password, CancellationToken ct);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    Task UpdateUserAsync(User user, CancellationToken ct);

    /// <summary>
    /// Deactivates a user.
    /// </summary>
    Task DeactivateUserAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Validates user credentials.
    /// </summary>
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct);

    #region SPEC-SECURITY-001: Authentication & Session Management

    /// <summary>
    /// Authenticates user with credentials and returns session info.
    /// Implements 5 failed login lockout policy.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(string username, string password, string workstationId, CancellationToken ct);

    /// <summary>
    /// Logs out the current user and invalidates the session.
    /// </summary>
    Task<bool> LogoutAsync(string sessionId, CancellationToken ct);

    /// <summary>
    /// Gets the current active session.
    /// </summary>
    Task<UserSession?> GetCurrentSessionAsync(string sessionId, CancellationToken ct);

    /// <summary>
    /// Validates if a session is still active (30-minute timeout).
    /// </summary>
    Task<bool> ValidateSessionAsync(string sessionId, CancellationToken ct);

    /// <summary>
    /// Refreshes session timeout (extends by 30 minutes).
    /// </summary>
    Task<UserSession?> RefreshSessionAsync(string sessionId, CancellationToken ct);

    /// <summary>
    /// Checks if user has specific permission.
    /// </summary>
    Task<bool> HasPermissionAsync(string userId, string permissionId, CancellationToken ct);

    /// <summary>
    /// Gets all permissions for a user based on their role.
    /// </summary>
    Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Validates password complexity requirements.
    /// Min 12 chars, upper+lower+number+special.
    /// </summary>
    PasswordValidationResult ValidatePasswordComplexity(string password, string? username = null);

    /// <summary>
    /// Changes user password after validating current password.
    /// </summary>
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct);

    /// <summary>
    /// Gets the number of remaining login attempts before lockout.
    /// </summary>
    int GetRemainingLoginAttempts(string username);

    /// <summary>
    /// Unlocks a locked user account (admin only).
    /// </summary>
    Task<bool> UnlockUserAsync(string userId, CancellationToken ct);

    #endregion
}
