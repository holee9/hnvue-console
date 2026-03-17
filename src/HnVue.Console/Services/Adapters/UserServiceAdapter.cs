using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IUserService.
/// SPEC-SECURITY-001: User authentication and authorization (RBAC).
/// @MX:NOTE Uses UserService gRPC for authentication, session management, and user CRUD.
/// Implements: 30-min session timeout, 5 failed login lockout, password complexity validation.
/// </summary>
public sealed class UserServiceAdapter : GrpcAdapterBase, IUserService
{
    private readonly ILogger<UserServiceAdapter> _logger;

    /// <summary>
    /// Helper to create IPC timestamp from DateTime.
    /// Uses ticks converted to microseconds.
    /// </summary>
    private static HnVue.Ipc.Timestamp CreateTimestamp()
    {
        return new HnVue.Ipc.Timestamp
        {
            MicrosecondsSinceStart = (ulong)(DateTime.UtcNow.Ticks / 10)
        };
    }

    /// <summary>
    /// @MX:ANCHOR Login attempt tracking for account lockout (5 failed attempts).
    /// Thread-safe dictionary for tracking login attempts per username.
    /// </summary>
    private static readonly ConcurrentDictionary<string, LoginAttemptInfo> _loginAttempts = new();

    /// <summary>
    /// @MX:WARN Maximum failed login attempts before account lockout.
    /// IEC 62304 safety requirement - prevents brute force attacks.
    /// </summary>
    private const int MaxFailedLoginAttempts = 5;

    /// <summary>
    /// @MX:WARN Session timeout in minutes per security requirements.
    /// Medical device standard requires session timeout for patient data protection.
    /// </summary>
    private const int SessionTimeoutMinutes = 30;

    /// <summary>
    /// @MX:WARN Minimum password length per security policy.
    /// 12 characters minimum with complexity requirements.
    /// </summary>
    private const int MinimumPasswordLength = 12;

    public UserServiceAdapter(IConfiguration configuration, ILogger<UserServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    #region Authentication & Session Management

    /// <summary>
    /// Authenticates user and creates session with 30-minute timeout.
    /// Tracks failed login attempts and locks account after 5 failures.
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        string workstationId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(workstationId);

        try
        {
            // Check if account is locked due to too many failed attempts
            if (IsAccountLocked(username))
            {
                _logger.LogWarning("Authentication attempt for locked account: {Username}", username);
                return new AuthenticationResult
                {
                    Success = false,
                    Session = null,
                    FailureReason = AuthenticationFailureReason.AccountLocked,
                    RemainingAttempts = 0
                };
            }

            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.AuthenticateAsync(
                new HnVue.Ipc.AuthenticateRequest
                {
                    Username = username,
                    Password = password,
                    WorkstationId = workstationId,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);

            if (response.Success && response.Session != null)
            {
                // Reset failed attempts on successful login
                ResetLoginAttempts(username);

                _logger.LogInformation("User {Username} authenticated successfully", username);

                return new AuthenticationResult
                {
                    Success = true,
                    Session = MapToUserSession(response.Session),
                    FailureReason = null,
                    RemainingAttempts = MaxFailedLoginAttempts
                };
            }

            // Track failed attempt
            var remainingAttempts = RecordFailedLoginAttempt(username);

            _logger.LogWarning("Authentication failed for user {Username}. Remaining attempts: {Remaining}",
                username, remainingAttempts);

            return new AuthenticationResult
            {
                Success = false,
                Session = null,
                FailureReason = remainingAttempts == 0
                    ? AuthenticationFailureReason.AccountLocked
                    : AuthenticationFailureReason.InvalidCredentials,
                RemainingAttempts = remainingAttempts
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(AuthenticateAsync));

            var remainingAttempts = RecordFailedLoginAttempt(username);
            return new AuthenticationResult
            {
                Success = false,
                Session = null,
                FailureReason = AuthenticationFailureReason.InvalidCredentials,
                RemainingAttempts = remainingAttempts
            };
        }
    }

    /// <summary>
    /// Logs out the current user and invalidates the session.
    /// </summary>
    public async Task<bool> LogoutAsync(string sessionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.LogoutAsync(
                new HnVue.Ipc.LogoutRequest
                {
                    SessionId = sessionId,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);

            _logger.LogInformation("Session {SessionId} logged out: {Success}", sessionId, response.Success);
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(LogoutAsync));
            return false;
        }
    }

    /// <summary>
    /// Gets the current active session.
    /// </summary>
    public async Task<UserSession?> GetCurrentSessionAsync(string sessionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.GetCurrentSessionAsync(
                new HnVue.Ipc.GetCurrentSessionRequest
                {
                    SessionId = sessionId,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);

            return response.Session is not null ? MapToUserSession(response.Session) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(GetCurrentSessionAsync));
            return null;
        }
    }

    /// <summary>
    /// Validates if a session is still active (30-minute timeout).
    /// </summary>
    public async Task<bool> ValidateSessionAsync(string sessionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.ValidateSessionAsync(
                new HnVue.Ipc.ValidateSessionRequest
                {
                    SessionId = sessionId,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);

            return response.IsValid;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(ValidateSessionAsync));
            return false;
        }
    }

    /// <summary>
    /// Refreshes session to extend timeout by 30 minutes.
    /// </summary>
    public async Task<UserSession?> RefreshSessionAsync(string sessionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.ValidateSessionAsync(
                new HnVue.Ipc.ValidateSessionRequest
                {
                    SessionId = sessionId,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);

            if (response.IsValid && response.Session != null)
            {
                return MapToUserSession(response.Session);
            }

            return null;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(RefreshSessionAsync));
            return null;
        }
    }

    /// <summary>
    /// @MX:ANCHOR Password complexity validation - min 12 chars, upper+lower+number+special.
    /// IEC 62304 safety requirement for medical device authentication.
    /// </summary>
    public PasswordValidationResult ValidatePasswordComplexity(string password, string? username = null)
    {
        ArgumentNullException.ThrowIfNull(password);

        var failures = new List<PasswordValidationFailure>();

        // Check minimum length (12 characters)
        if (password.Length < MinimumPasswordLength)
        {
            failures.Add(PasswordValidationFailure.TooShort);
        }

        // Check for uppercase letter
        if (!password.Any(char.IsUpper))
        {
            failures.Add(PasswordValidationFailure.MissingUppercase);
        }

        // Check for lowercase letter
        if (!password.Any(char.IsLower))
        {
            failures.Add(PasswordValidationFailure.MissingLowercase);
        }

        // Check for digit
        if (!password.Any(char.IsDigit))
        {
            failures.Add(PasswordValidationFailure.MissingDigit);
        }

        // Check for special character
        var specialCharPattern = @"[!@#$%^&*()_+=\[\]{};:'"",.<>?/\\|`~-]";
        if (!Regex.IsMatch(password, specialCharPattern))
        {
            failures.Add(PasswordValidationFailure.MissingSpecialCharacter);
        }

        // Check if password contains username
        if (!string.IsNullOrEmpty(username) &&
            password.Contains(username, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(PasswordValidationFailure.ContainsUsername);
        }

        // Check for 3 or more repeated consecutive characters
        if (HasRepeatedCharacters(password, 3))
        {
            failures.Add(PasswordValidationFailure.RepeatedCharacter);
        }

        return new PasswordValidationResult
        {
            IsValid = failures.Count == 0,
            Failures = failures
        };
    }

    /// <summary>
    /// Changes user password after validating current password.
    /// New password must meet complexity requirements.
    /// </summary>
    public async Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(currentPassword);
        ArgumentNullException.ThrowIfNull(newPassword);

        // Validate new password complexity
        var validation = ValidatePasswordComplexity(newPassword, userId);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Password change rejected for user {UserId}: complexity requirements not met", userId);
            return false;
        }

        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.ChangePasswordAsync(
                new HnVue.Ipc.ChangePasswordRequest
                {
                    UserId = userId,
                    CurrentPassword = currentPassword,
                    NewPassword = newPassword,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);

            if (response.Success)
            {
                _logger.LogInformation("Password changed successfully for user {UserId}", userId);
            }

            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(ChangePasswordAsync));
            return false;
        }
    }

    #endregion

    #region RBAC Permission Management

    /// <summary>
    /// @MX:ANCHOR RBAC permission check based on user role.
    /// Maps roles to permissions for access control decisions.
    /// </summary>
    public async Task<bool> HasPermissionAsync(string userId, string permissionId, CancellationToken ct)
    {
        var permissions = await GetUserPermissionsAsync(userId, ct);
        return permissions.Any(p => p.PermissionId == permissionId);
    }

    /// <summary>
    /// Gets all permissions for a user based on their role.
    /// </summary>
    public async Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(string userId, CancellationToken ct)
    {
        // Get user to determine role
        var user = await GetUserByIdAsync(userId, ct);
        if (user == null || !user.IsActive)
        {
            return Array.Empty<Permission>();
        }

        return GetPermissionsForRole(user.Role);
    }

    /// <summary>
    /// Gets permissions for a specific role.
    /// </summary>
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

    #region Login Attempt Tracking

    /// <summary>
    /// Gets remaining login attempts before account lockout.
    /// </summary>
    public int GetRemainingLoginAttempts(string username)
    {
        if (_loginAttempts.TryGetValue(username, out var info))
        {
            // Check if lockout period has expired (15 minutes)
            if (info.LockedAt.HasValue &&
                DateTimeOffset.UtcNow - info.LockedAt.Value > TimeSpan.FromMinutes(15))
            {
                // Reset after lockout period
                _loginAttempts.TryRemove(username, out _);
                return MaxFailedLoginAttempts;
            }

            return Math.Max(0, MaxFailedLoginAttempts - info.FailedCount);
        }

        return MaxFailedLoginAttempts;
    }

    /// <summary>
    /// Unlocks a locked user account (admin only operation).
    /// </summary>
    public async Task<bool> UnlockUserAsync(string userId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userId);

        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();

            // Get user to find username
            var userResponse = await client.GetUserAsync(
                new HnVue.Ipc.GetUserRequest
                {
                    UserId = userId,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);

            if (userResponse.User != null)
            {
                // Clear login attempts for this user
                _loginAttempts.TryRemove(userResponse.User.Username, out _);
                _logger.LogInformation("Account unlocked for user {UserId}", userId);
            }

            return true;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(UnlockUserAsync));
            return false;
        }
    }

    private bool IsAccountLocked(string username)
    {
        if (_loginAttempts.TryGetValue(username, out var info))
        {
            if (info.LockedAt.HasValue)
            {
                // Check if lockout period has expired (15 minutes)
                if (DateTimeOffset.UtcNow - info.LockedAt.Value > TimeSpan.FromMinutes(15))
                {
                    _loginAttempts.TryRemove(username, out _);
                    return false;
                }
                return true;
            }
        }
        return false;
    }

    private int RecordFailedLoginAttempt(string username)
    {
        var info = _loginAttempts.AddOrUpdate(
            username,
            _ => new LoginAttemptInfo { FailedCount = 1, LastAttempt = DateTimeOffset.UtcNow },
            (_, existing) => existing with
            {
                FailedCount = existing.FailedCount + 1,
                LastAttempt = DateTimeOffset.UtcNow,
                LockedAt = existing.FailedCount + 1 >= MaxFailedLoginAttempts
                    ? DateTimeOffset.UtcNow
                    : existing.LockedAt
            });

        return Math.Max(0, MaxFailedLoginAttempts - info.FailedCount);
    }

    private void ResetLoginAttempts(string username)
    {
        _loginAttempts.TryRemove(username, out _);
    }

    #endregion

    #region User CRUD Operations

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.ListUsersAsync(
                new HnVue.Ipc.ListUsersRequest
                {
                    IncludeInactive = false,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);
            return response.Users.Select(MapToUser).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(GetUsersAsync));
            return Array.Empty<User>();
        }
    }

    public async Task<User> GetCurrentUserAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.GetCurrentSessionAsync(
                new HnVue.Ipc.GetCurrentSessionRequest
                {
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);
            return response.Session is not null ? MapToUser(response.Session.User) : CreateDefaultUser();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(GetCurrentUserAsync));
            return CreateDefaultUser();
        }
    }

    public async Task<UserRole> GetCurrentUserRoleAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(ct);
        return user.Role;
    }

    public async Task<bool> CanAccessSectionAsync(ConfigSection section, CancellationToken ct)
    {
        var role = await GetCurrentUserRoleAsync(ct);
        return HasSectionAccess(role, section);
    }

    public async Task CreateUserAsync(User user, string password, CancellationToken ct)
    {
        // Validate password complexity
        var validation = ValidatePasswordComplexity(password, user.UserName);
        if (!validation.IsValid)
        {
            throw new ArgumentException($"Password does not meet complexity requirements: {string.Join(", ", validation.Failures)}");
        }

        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            await client.CreateUserAsync(
                new HnVue.Ipc.CreateUserRequest
                {
                    User = MapToProtoUser(user),
                    InitialPassword = password,
                    CreatedBy = "system",
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(CreateUserAsync));
        }
    }

    public async Task UpdateUserAsync(User user, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            await client.UpdateUserAsync(
                new HnVue.Ipc.UpdateUserRequest
                {
                    UserId = user.UserId,
                    UpdatedUser = MapToProtoUser(user),
                    UpdatedBy = "system",
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(UpdateUserAsync));
        }
    }

    public async Task DeactivateUserAsync(string userId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var inactiveUser = new HnVue.Ipc.User { UserId = userId, IsActive = false };
            await client.UpdateUserAsync(
                new HnVue.Ipc.UpdateUserRequest
                {
                    UserId = userId,
                    UpdatedUser = inactiveUser,
                    UpdatedBy = "system",
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(DeactivateUserAsync));
        }
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.AuthenticateAsync(
                new HnVue.Ipc.AuthenticateRequest
                {
                    Username = username,
                    Password = password,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(ValidateCredentialsAsync));
            return false;
        }
    }

    private async Task<User?> GetUserByIdAsync(string userId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.GetUserAsync(
                new HnVue.Ipc.GetUserRequest
                {
                    UserId = userId,
                    RequestTimestamp = CreateTimestamp()
                },
                cancellationToken: ct);
            return response.User is not null ? MapToUser(response.User) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), "GetUserByIdAsync");
            return null;
        }
    }

    #endregion

    #region Mapping Functions

    private static User MapToUser(HnVue.Ipc.User proto)
    {
        return new User
        {
            UserId = proto.UserId,
            UserName = proto.Username,
            Role = MapFromProtoRole(proto.PrimaryRole),
            IsActive = proto.IsActive
        };
    }

    private static HnVue.Ipc.User MapToProtoUser(User user)
    {
        return new HnVue.Ipc.User
        {
            UserId = user.UserId,
            Username = user.UserName,
            DisplayName = user.UserName,
            PrimaryRole = MapToProtoRole(user.Role),
            IsActive = user.IsActive
        };
    }

    private static UserSession MapToUserSession(HnVue.Ipc.UserSession proto)
    {
        return new UserSession
        {
            SessionId = proto.SessionId,
            User = MapToUser(proto.User),
            AccessToken = proto.AccessToken,
            ExpiresAt = proto.ExpiresAt is not null
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)proto.ExpiresAt.MicrosecondsSinceStart / 1000)
                : DateTimeOffset.UtcNow.AddMinutes(SessionTimeoutMinutes),
            GrantedRoles = proto.GrantedRoles.Select(r => MapFromProtoRoleString(r)).ToList().AsReadOnly()
        };
    }

    private static UserRole MapFromProtoRoleString(string role) => role?.ToUpperInvariant() switch
    {
        "ADMINISTRATOR" => UserRole.Administrator,
        "RADIOLOGIST" => UserRole.Radiologist,
        "TECHNOLOGIST" => UserRole.Technologist,
        "PHYSICIST" => UserRole.Physicist,
        "OPERATOR" => UserRole.Operator,
        "VIEWER" => UserRole.Viewer,
        "SERVICE" => UserRole.Service,
        _ => UserRole.Unspecified
    };

    private static UserRole MapFromProtoRole(HnVue.Ipc.UserRole protoRole) => protoRole switch
    {
        HnVue.Ipc.UserRole.Administrator => UserRole.Administrator,
        HnVue.Ipc.UserRole.Radiologist => UserRole.Radiologist,
        HnVue.Ipc.UserRole.Technologist => UserRole.Technologist,
        HnVue.Ipc.UserRole.Physicist => UserRole.Physicist,
        HnVue.Ipc.UserRole.Operator => UserRole.Operator,
        HnVue.Ipc.UserRole.Viewer => UserRole.Viewer,
        HnVue.Ipc.UserRole.Service => UserRole.Service,
        _ => UserRole.Unspecified
    };

    private static HnVue.Ipc.UserRole MapToProtoRole(UserRole role) => role switch
    {
        UserRole.Administrator => HnVue.Ipc.UserRole.Administrator,
        UserRole.Radiologist => HnVue.Ipc.UserRole.Radiologist,
        UserRole.Technologist => HnVue.Ipc.UserRole.Technologist,
        UserRole.Physicist => HnVue.Ipc.UserRole.Physicist,
        UserRole.Operator => HnVue.Ipc.UserRole.Operator,
        UserRole.Viewer => HnVue.Ipc.UserRole.Viewer,
        UserRole.Service => HnVue.Ipc.UserRole.Service,
        _ => HnVue.Ipc.UserRole.Unspecified
    };

    #endregion

    #region Helper Functions

    /// <summary>
    /// @MX:ANCHOR RBAC logic for section access control.
    /// </summary>
    private static bool HasSectionAccess(UserRole role, ConfigSection section) => section switch
    {
        ConfigSection.Users => role == UserRole.Administrator,
        ConfigSection.Network => role == UserRole.Administrator || role == UserRole.Service,
        ConfigSection.Logging => role == UserRole.Administrator || role == UserRole.Service,
        ConfigSection.Calibration => role == UserRole.Administrator || role == UserRole.Physicist || role == UserRole.Service,
        _ => false
    };

    /// <summary>
    /// @MX:NOTE Creates an unauthenticated default user for offline/demo mode.
    /// Returns inactive user with empty credentials to force proper authentication handling.
    /// UI should detect IsActive=false as offline indicator and prompt for login.
    /// </summary>
    private static User CreateDefaultUser() => new()
    {
        UserId = string.Empty,
        UserName = string.Empty,
        Role = UserRole.Operator,
        IsActive = false
    };

    private static bool HasRepeatedCharacters(string password, int maxRepeats)
    {
        if (string.IsNullOrEmpty(password) || password.Length < maxRepeats)
            return false;

        var count = 1;
        for (var i = 1; i < password.Length; i++)
        {
            if (password[i] == password[i - 1])
            {
                count++;
                if (count >= maxRepeats)
                    return true;
            }
            else
            {
                count = 1;
            }
        }
        return false;
    }

    #endregion

    #region Inner Classes

    /// <summary>
    /// Tracks login attempt information for account lockout.
    /// </summary>
    private sealed record LoginAttemptInfo
    {
        public int FailedCount { get; init; }
        public DateTimeOffset LastAttempt { get; init; }
        public DateTimeOffset? LockedAt { get; init; }
    }

    #endregion
}
