using HnVue.Console.Models;
using System.Collections.Concurrent;

namespace HnVue.Console.Services;

/// <summary>
/// Mock user service for development.
/// SPEC-UI-001: FR-UI-08 System Configuration (Users section).
/// SPEC-SECURITY-001: Authentication/Authorization (RBAC), Session Management, Account Lockout.
/// </summary>
public class MockUserService : IUserService
{
    private readonly IReadOnlyList<User> _mockUsers;
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
    private readonly ConcurrentDictionary<string, int> _failedLoginAttempts = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lockedAccounts = new();

    private const int MaxFailedLoginAttempts = 5;
    private const int SessionTimeoutMinutes = 30;
    private const int LockoutDurationMinutes = 15;
    private const int MinimumPasswordLength = 12;

    // Mock password for development use only — matches test fixture expectations.
    // Production systems must use proper credential storage.
    private const string MockPassword = "password123";

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
                UserId = "rad01",
                UserName = "Dr. House",
                Role = UserRole.Radiologist,
                IsActive = true
            },
            new()
            {
                UserId = "tech01",
                UserName = "Technician Johnson",
                Role = UserRole.Technologist,
                IsActive = true
            },
            new()
            {
                UserId = "phys01",
                UserName = "Dr. Quantum",
                Role = UserRole.Physicist,
                IsActive = true
            },
            new()
            {
                UserId = "operator1",
                UserName = "Operator Smith",
                Role = UserRole.Operator,
                IsActive = true
            },
            new()
            {
                UserId = "viewer01",
                UserName = "Viewer Jane",
                Role = UserRole.Viewer,
                IsActive = true
            },
            new()
            {
                UserId = "service01",
                UserName = "Service Engineer Davis",
                Role = UserRole.Service,
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
        // Mock current user - administrator role
        var currentUser = _mockUsers[0];
        return Task.FromResult(currentUser);
    }

    public Task<UserRole> GetCurrentUserRoleAsync(CancellationToken ct)
    {
        // Mock current user role - administrator
        return Task.FromResult(UserRole.Administrator);
    }

    public Task<bool> CanAccessSectionAsync(ConfigSection section, CancellationToken ct)
    {
        // Mock role-based access - administrator can access all sections
        var currentRole = UserRole.Administrator;

        var result = section switch
        {
            ConfigSection.Calibration => currentRole >= UserRole.Service,
            ConfigSection.Network => currentRole >= UserRole.Administrator,
            ConfigSection.Users => currentRole >= UserRole.Administrator,
            ConfigSection.Logging => currentRole >= UserRole.Administrator,
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

    #region SPEC-SECURITY-001: Authentication & Session Management

    public Task<AuthenticationResult> AuthenticateAsync(string username, string password, string workstationId, CancellationToken ct)
    {
        // Check if account is locked
        if (_lockedAccounts.TryGetValue(username, out var lockedAt))
        {
            if (DateTimeOffset.UtcNow - lockedAt < TimeSpan.FromMinutes(LockoutDurationMinutes))
            {
                return Task.FromResult(new AuthenticationResult
                {
                    Success = false,
                    Session = null,
                    FailureReason = AuthenticationFailureReason.AccountLocked,
                    RemainingAttempts = 0
                });
            }
            // Lockout expired
            _lockedAccounts.TryRemove(username, out _);
            _failedLoginAttempts.TryRemove(username, out _);
        }

        // Mock authentication - validate both username and password.
        // Only the mock password is accepted; any other value triggers a failed attempt.
        var user = _mockUsers.FirstOrDefault(u => u.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user == null || string.IsNullOrEmpty(password) || password != MockPassword)
        {
            var remaining = RecordFailedAttempt(username);
            return Task.FromResult(new AuthenticationResult
            {
                Success = false,
                Session = null,
                FailureReason = AuthenticationFailureReason.InvalidCredentials,
                RemainingAttempts = remaining
            });
        }

        // Success - create session
        _failedLoginAttempts.TryRemove(username, out _);
        var session = new UserSession
        {
            SessionId = Guid.NewGuid().ToString(),
            User = user,
            AccessToken = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(SessionTimeoutMinutes),
            GrantedRoles = new List<UserRole> { user.Role }.AsReadOnly()
        };
        _sessions[session.SessionId] = session;

        return Task.FromResult(new AuthenticationResult
        {
            Success = true,
            Session = session,
            FailureReason = null,
            RemainingAttempts = MaxFailedLoginAttempts
        });
    }

    public Task<bool> LogoutAsync(string sessionId, CancellationToken ct)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.FromResult(true);
    }

    public Task<UserSession?> GetCurrentSessionAsync(string sessionId, CancellationToken ct)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            if (session.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return Task.FromResult<UserSession?>(session);
            }
            _sessions.TryRemove(sessionId, out _);
        }
        return Task.FromResult<UserSession?>(null);
    }

    public Task<bool> ValidateSessionAsync(string sessionId, CancellationToken ct)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(session.ExpiresAt > DateTimeOffset.UtcNow);
        }
        return Task.FromResult(false);
    }

    public Task<UserSession?> RefreshSessionAsync(string sessionId, CancellationToken ct)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            if (session.ExpiresAt > DateTimeOffset.UtcNow)
            {
                var refreshed = session with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(SessionTimeoutMinutes) };
                _sessions[sessionId] = refreshed;
                return Task.FromResult<UserSession?>(refreshed);
            }
            _sessions.TryRemove(sessionId, out _);
        }
        return Task.FromResult<UserSession?>(null);
    }

    public Task<bool> HasPermissionAsync(string userId, string permissionId, CancellationToken ct)
    {
        var user = _mockUsers.FirstOrDefault(u => u.UserId == userId);
        if (user == null || !user.IsActive)
        {
            return Task.FromResult(false);
        }

        var permissions = GetPermissionsForRole(user.Role);
        return Task.FromResult(permissions.Any(p => p.PermissionId == permissionId));
    }

    public Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(string userId, CancellationToken ct)
    {
        var user = _mockUsers.FirstOrDefault(u => u.UserId == userId);
        if (user == null || !user.IsActive)
        {
            return Task.FromResult<IReadOnlyList<Permission>>(Array.Empty<Permission>());
        }
        return Task.FromResult(GetPermissionsForRole(user.Role));
    }

    public PasswordValidationResult ValidatePasswordComplexity(string password, string? username = null)
    {
        var failures = new List<PasswordValidationFailure>();

        if (password.Length < MinimumPasswordLength)
            failures.Add(PasswordValidationFailure.TooShort);

        if (!password.Any(char.IsUpper))
            failures.Add(PasswordValidationFailure.MissingUppercase);

        if (!password.Any(char.IsLower))
            failures.Add(PasswordValidationFailure.MissingLowercase);

        if (!password.Any(char.IsDigit))
            failures.Add(PasswordValidationFailure.MissingDigit);

        if (!password.Any(c => char.IsPunctuation(c) || char.IsSymbol(c)))
            failures.Add(PasswordValidationFailure.MissingSpecialCharacter);

        if (!string.IsNullOrEmpty(username) && password.Contains(username, StringComparison.OrdinalIgnoreCase))
            failures.Add(PasswordValidationFailure.ContainsUsername);

        return new PasswordValidationResult
        {
            IsValid = failures.Count == 0,
            Failures = failures
        };
    }

    public Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        // Validate new password complexity
        var validation = ValidatePasswordComplexity(newPassword, userId);
        if (!validation.IsValid)
        {
            return Task.FromResult(false);
        }

        // Mock implementation
        System.Diagnostics.Debug.WriteLine($"Password changed for user: {userId}");
        return Task.FromResult(true);
    }

    public int GetRemainingLoginAttempts(string username)
    {
        if (_lockedAccounts.TryGetValue(username, out var lockedAt))
        {
            if (DateTimeOffset.UtcNow - lockedAt < TimeSpan.FromMinutes(LockoutDurationMinutes))
            {
                return 0;
            }
            // Lockout expired
            _lockedAccounts.TryRemove(username, out _);
            _failedLoginAttempts.TryRemove(username, out _);
            return MaxFailedLoginAttempts;
        }

        if (_failedLoginAttempts.TryGetValue(username, out var attempts))
        {
            return Math.Max(0, MaxFailedLoginAttempts - attempts);
        }

        return MaxFailedLoginAttempts;
    }

    public Task<bool> UnlockUserAsync(string userId, CancellationToken ct)
    {
        var user = _mockUsers.FirstOrDefault(u => u.UserId == userId);
        if (user != null)
        {
            _lockedAccounts.TryRemove(user.UserName, out _);
            _failedLoginAttempts.TryRemove(user.UserName, out _);
        }
        return Task.FromResult(true);
    }

    private int RecordFailedAttempt(string username)
    {
        var attempts = _failedLoginAttempts.AddOrUpdate(username, 1, (_, count) => count + 1);

        if (attempts >= MaxFailedLoginAttempts)
        {
            _lockedAccounts[username] = DateTimeOffset.UtcNow;
            return 0;
        }

        return MaxFailedLoginAttempts - attempts;
    }

    private static IReadOnlyList<Permission> GetPermissionsForRole(UserRole role) => role switch
    {
        UserRole.Administrator => new[]
        {
            new Permission { PermissionId = "system.admin", Name = "System Administration", Description = "Full system access", ResourceType = "System", AllowedActions = new[] { "Read", "Write", "Delete", "Execute" } },
            new Permission { PermissionId = "users.manage", Name = "Manage Users", Description = "Create, update, delete users", ResourceType = "User", AllowedActions = new[] { "Read", "Write", "Delete" } },
            new Permission { PermissionId = "config.all", Name = "All Configuration", Description = "Access all configuration sections", ResourceType = "Configuration", AllowedActions = new[] { "Read", "Write" } },
            new Permission { PermissionId = "reports.view", Name = "View Reports", Description = "View all reports", ResourceType = "Report", AllowedActions = new[] { "Read" } },
            new Permission { PermissionId = "exposure.execute", Name = "Execute Exposure", Description = "Execute X-ray exposures", ResourceType = "Exposure", AllowedActions = new[] { "Execute" } }
        },
        UserRole.Radiologist => new[]
        {
            new Permission { PermissionId = "reports.view", Name = "View Reports", Description = "View all reports", ResourceType = "Report", AllowedActions = new[] { "Read" } },
            new Permission { PermissionId = "reports.sign", Name = "Sign Reports", Description = "Digitally sign reports", ResourceType = "Report", AllowedActions = new[] { "Write", "Execute" } },
            new Permission { PermissionId = "patients.view", Name = "View Patients", Description = "View patient information", ResourceType = "Patient", AllowedActions = new[] { "Read" } },
            new Permission { PermissionId = "exposure.execute", Name = "Execute Exposure", Description = "Execute X-ray exposures", ResourceType = "Exposure", AllowedActions = new[] { "Execute" } }
        },
        UserRole.Technologist => new[]
        {
            new Permission { PermissionId = "exposure.execute", Name = "Execute Exposure", Description = "Execute X-ray exposures", ResourceType = "Exposure", AllowedActions = new[] { "Execute" } },
            new Permission { PermissionId = "patients.view", Name = "View Patients", Description = "View patient information", ResourceType = "Patient", AllowedActions = new[] { "Read" } },
            new Permission { PermissionId = "worklist.view", Name = "View Worklist", Description = "View worklist items", ResourceType = "Worklist", AllowedActions = new[] { "Read" } },
            new Permission { PermissionId = "exposure.prepare", Name = "Prepare Exposure", Description = "Prepare exposure parameters", ResourceType = "Exposure", AllowedActions = new[] { "Read", "Write" } }
        },
        UserRole.Physicist => new[]
        {
            new Permission { PermissionId = "qc.perform", Name = "Perform QC", Description = "Perform quality control tests", ResourceType = "QC", AllowedActions = new[] { "Read", "Execute" } },
            new Permission { PermissionId = "calibration.manage", Name = "Manage Calibration", Description = "Manage system calibration", ResourceType = "Calibration", AllowedActions = new[] { "Read", "Write", "Execute" } },
            new Permission { PermissionId = "equipment.config", Name = "Equipment Configuration", Description = "Configure equipment parameters", ResourceType = "Equipment", AllowedActions = new[] { "Read", "Write" } },
            new Permission { PermissionId = "reports.view", Name = "View Reports", Description = "View QC reports", ResourceType = "Report", AllowedActions = new[] { "Read" } }
        },
        UserRole.Operator => new[]
        {
            new Permission { PermissionId = "exposure.execute", Name = "Execute Exposure", Description = "Execute X-ray exposures", ResourceType = "Exposure", AllowedActions = new[] { "Execute" } },
            new Permission { PermissionId = "patients.view", Name = "View Patients", Description = "View patient information (limited)", ResourceType = "Patient", AllowedActions = new[] { "Read" } },
            new Permission { PermissionId = "worklist.view", Name = "View Worklist", Description = "View worklist items", ResourceType = "Worklist", AllowedActions = new[] { "Read" } }
        },
        UserRole.Viewer => new[]
        {
            new Permission { PermissionId = "reports.view", Name = "View Reports", Description = "View reports only", ResourceType = "Report", AllowedActions = new[] { "Read" } },
            new Permission { PermissionId = "patients.view", Name = "View Patients", Description = "View patient information (read-only)", ResourceType = "Patient", AllowedActions = new[] { "Read" } }
        },
        UserRole.Service => new[]
        {
            new Permission { PermissionId = "system.maintenance", Name = "System Maintenance", Description = "Perform system maintenance", ResourceType = "System", AllowedActions = new[] { "Execute" } },
            new Permission { PermissionId = "calibration.manage", Name = "Manage Calibration", Description = "Manage system calibration", ResourceType = "Calibration", AllowedActions = new[] { "Read", "Write", "Execute" } },
            new Permission { PermissionId = "logs.view", Name = "View Logs", Description = "View system logs", ResourceType = "Log", AllowedActions = new[] { "Read" } },
            new Permission { PermissionId = "diagnostics.execute", Name = "Run Diagnostics", Description = "Execute diagnostic tests", ResourceType = "System", AllowedActions = new[] { "Execute" } }
        },
        _ => Array.Empty<Permission>()
    };

    #endregion
}
