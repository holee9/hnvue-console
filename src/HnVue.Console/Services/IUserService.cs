using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// User service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-08 System Configuration (Users section).
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
}
