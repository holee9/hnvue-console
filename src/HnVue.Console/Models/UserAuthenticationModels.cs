namespace HnVue.Console.Models;

/// <summary>
/// Authentication result from login attempt.
/// SPEC-SECURITY-001: Authentication/Authorization (RBAC).
/// </summary>
public record AuthenticationResult
{
    public required bool Success { get; init; }
    public required UserSession? Session { get; init; }
    public required AuthenticationFailureReason? FailureReason { get; init; }
    public required int RemainingAttempts { get; init; }
}

/// <summary>
/// User session information.
/// SPEC-SECURITY-001: Session management (30-minute timeout).
/// </summary>
public record UserSession
{
    public required string SessionId { get; init; }
    public required User User { get; init; }
    public required string AccessToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required IReadOnlyList<UserRole> GrantedRoles { get; init; }
}

/// <summary>
/// Password validation result.
/// SPEC-SECURITY-001: Password complexity (min 12 chars, upper+lower+number+special).
/// </summary>
public record PasswordValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<PasswordValidationFailure> Failures { get; init; }
}

/// <summary>
/// Password validation failure reasons.
/// </summary>
public enum PasswordValidationFailure
{
    TooShort,
    MissingUppercase,
    MissingLowercase,
    MissingDigit,
    MissingSpecialCharacter,
    ContainsUsername,
    RepeatedCharacter
}

/// <summary>
/// Authentication failure reasons.
/// SPEC-SECURITY-001: Detailed failure reasons for audit logging.
/// </summary>
public enum AuthenticationFailureReason
{
    InvalidCredentials,
    AccountLocked,
    AccountDeactivated,
    SessionExpired,
    InsufficientPermissions,
    PasswordExpired,
    TooManyFailedAttempts
}

/// <summary>
/// Permission definition for RBAC.
/// SPEC-SECURITY-001: Role-Based Access Control.
/// </summary>
public record Permission
{
    public required string PermissionId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ResourceType { get; init; }
    public required IReadOnlyList<string> AllowedActions { get; init; }
}
