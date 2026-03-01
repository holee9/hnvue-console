using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock user service for development.
/// SPEC-UI-001: FR-UI-08 System Configuration (Users section).
/// </summary>
public class MockUserService : IUserService
{
    private readonly IReadOnlyList<User> _mockUsers;

    public MockUserService()
    {
        _mockUsers = new List<User>
        {
            new()
            {
                UserId = "admin",
                UserName = "System Administrator",
                Role = UserRole.Administrator,
                IsActive = true
            },
            new()
            {
                UserId = "supervisor1",
                UserName = "Dr. Smith",
                Role = UserRole.Supervisor,
                IsActive = true
            },
            new()
            {
                UserId = "operator1",
                UserName = "Technician Johnson",
                Role = UserRole.Operator,
                IsActive = true
            },
            new()
            {
                UserId = "engineer1",
                UserName = "Service Engineer Davis",
                Role = UserRole.ServiceEngineer,
                IsActive = true
            }
        };
    }

    public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct)
    {
        return Task.FromResult(_mockUsers);
    }

    public Task<User> GetCurrentUserAsync(CancellationToken ct)
    {
        // Mock current user - supervisor role
        var currentUser = _mockUsers[1];
        return Task.FromResult(currentUser);
    }

    public Task<UserRole> GetCurrentUserRoleAsync(CancellationToken ct)
    {
        // Mock current user role - supervisor
        return Task.FromResult(UserRole.Supervisor);
    }

    public Task<bool> CanAccessSectionAsync(ConfigSection section, CancellationToken ct)
    {
        // Mock role-based access - supervisor can access most sections
        var currentRole = UserRole.Supervisor;

        var result = section switch
        {
            ConfigSection.Calibration => currentRole >= UserRole.ServiceEngineer,
            ConfigSection.Network => currentRole >= UserRole.Supervisor,
            ConfigSection.Users => currentRole >= UserRole.Administrator,
            ConfigSection.Logging => currentRole >= UserRole.Supervisor,
            _ => false
        };

        return Task.FromResult(result);
    }

    public Task CreateUserAsync(User user, string password, CancellationToken ct)
    {
        // Mock implementation
        System.Diagnostics.Debug.WriteLine($"Creating user: {user.UserId}");
        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(User user, CancellationToken ct)
    {
        // Mock implementation
        System.Diagnostics.Debug.WriteLine($"Updating user: {user.UserId}");
        return Task.CompletedTask;
    }

    public Task DeactivateUserAsync(string userId, CancellationToken ct)
    {
        // Mock implementation
        System.Diagnostics.Debug.WriteLine($"Deactivating user: {userId}");
        return Task.CompletedTask;
    }

    public Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct)
    {
        // Mock implementation - accept any non-empty credentials
        var result = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
        return Task.FromResult(result);
    }
}
