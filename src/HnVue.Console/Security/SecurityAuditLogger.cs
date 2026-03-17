using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.Security;

/// <summary>
/// Security event types for audit logging.
/// SPEC-SECURITY-001: FR-SEC-14 - Security Audit Logging
/// </summary>
public enum SecurityEventType
{
    AuthenticationSuccess,
    AuthenticationFailure,
    AuthorizationFailure,
    DataAccess,
    DataModification,
    ConfigurationChange,
    SecurityViolation,
    SecurityPolicyViolation,
    AuthenticationBypassAttempt,
    DataExfiltrationAttempt
}

/// <summary>
/// Security audit logger for compliance and incident response.
/// SPEC-SECURITY-001: FR-SEC-14 - Security Audit Logging
/// Compliance: HIPAA 164.312(b), IEC 6234 6.3.8
/// </summary>
public class SecurityAuditLogger
{
    private readonly IAuditLogService _auditLogService;

    public SecurityAuditLogger(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <summary>
    /// Logs a security event to the audit log
    /// </summary>
    /// <param name="eventType">Type of security event</param>
    /// <param name="userId">User ID (null for system events)</param>
    /// <param name="userName">User name</param>
    /// <param name="resourceId">Resource ID (null if not applicable)</param>
    /// <param name="details">Event details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task LogSecurityEventAsync(
        SecurityEventType eventType,
        string? userId,
        string userName,
        string? resourceId,
        string? details,
        CancellationToken ct)
    {
        var auditEventType = MapToAuditEventType(eventType);
        var outcome = GetOutcome(eventType);
        var sanitizedDetails = SanitizeDetails(details);

        await _auditLogService.LogAsync(
            auditEventType,
            userId ?? "SYSTEM",
            userName,
            sanitizedDetails ?? string.Empty,
            outcome,
            resourceId,
            null,
            ct);
    }

    /// <summary>
    /// Logs authentication attempt
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="success">True if successful, false otherwise</param>
    /// <param name="workstationId">Workstation ID</param>
    /// <param name="failureReason">Failure reason (if failed)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task LogAuthenticationAttemptAsync(
        string username,
        bool success,
        string workstationId,
        string? failureReason = null,
        CancellationToken ct = default)
    {
        var eventType = success ? SecurityEventType.AuthenticationSuccess : SecurityEventType.AuthenticationFailure;
        var details = success
            ? $"User '{username}' authenticated from workstation '{workstationId}'"
            : $"Failed authentication for user '{username}' from workstation '{workstationId}': {failureReason}";

        await LogSecurityEventAsync(eventType, username, username, null, details, ct);
    }

    /// <summary>
    /// Logs authorization failure
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="userName">User name</param>
    /// <param name="resourceId">Resource ID</param>
    /// <param name="permission">Required permission</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task LogAuthorizationFailureAsync(
        string userId,
        string userName,
        string resourceId,
        string permission,
        CancellationToken ct)
    {
        var details = $"User '{userId}' denied access to resource '{resourceId}' (permission: {permission})";
        await LogSecurityEventAsync(SecurityEventType.AuthorizationFailure, userId, userName, resourceId, details, ct);
    }

    /// <summary>
    /// Logs data access event
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="userName">User name</param>
    /// <param name="resourceType">Resource type (e.g., "Patient", "Study")</param>
    /// <param name="resourceId">Resource ID</param>
    /// <param name="accessType">Access type (e.g., "Read", "Write")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task LogDataAccessAsync(
        string userId,
        string userName,
        string resourceType,
        string resourceId,
        string accessType,
        CancellationToken ct)
    {
        var details = $"User '{userId}' accessed {resourceType} '{resourceId}' ({accessType})";
        await LogSecurityEventAsync(SecurityEventType.DataAccess, userId, userName, resourceId, details, ct);
    }

    /// <summary>
    /// Logs security policy violation
    /// </summary>
    /// <param name="userId">User ID (null for system violations)</param>
    /// <param name="userName">User name</param>
    /// <param name="policy">Violated policy</param>
    /// <param name="details">Violation details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task LogSecurityViolationAsync(
        string? userId,
        string userName,
        string policy,
        string details,
        CancellationToken ct)
    {
        var violationDetails = $"Security policy violation: {policy} - {details}";
        await LogSecurityEventAsync(SecurityEventType.SecurityPolicyViolation, userId, userName, null, violationDetails, ct);
    }

    /// <summary>
    /// Logs configuration change event
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="userName">User name</param>
    /// <param name="configKey">Configuration key</param>
    /// <param name="oldValue">Old value</param>
    /// <param name="newValue">New value</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task LogConfigurationChangeAsync(
        string userId,
        string userName,
        string configKey,
        string? oldValue,
        string? newValue,
        CancellationToken ct)
    {
        var sanitizedOldValue = SanitizeValue(oldValue);
        var sanitizedNewValue = SanitizeValue(newValue);
        var details = $"Configuration change: {configKey} from '{sanitizedOldValue}' to '{sanitizedNewValue}'";

        await LogSecurityEventAsync(SecurityEventType.ConfigurationChange, userId, userName, configKey, details, ct);
    }

    /// <summary>
    /// Maps security event type to audit event type
    /// </summary>
    private static AuditEventType MapToAuditEventType(SecurityEventType eventType) => eventType switch
    {
        SecurityEventType.AuthenticationSuccess => AuditEventType.UserLogin,
        SecurityEventType.AuthenticationFailure => AuditEventType.UserLogin,
        SecurityEventType.AuthorizationFailure => AuditEventType.ConfigChange,
        SecurityEventType.DataAccess => AuditEventType.PatientRegistration,
        SecurityEventType.DataModification => AuditEventType.PatientEdit,
        SecurityEventType.ConfigurationChange => AuditEventType.ConfigChange,
        SecurityEventType.SecurityViolation => AuditEventType.SystemError,
        SecurityEventType.SecurityPolicyViolation => AuditEventType.SystemError,
        SecurityEventType.AuthenticationBypassAttempt => AuditEventType.SystemError,
        SecurityEventType.DataExfiltrationAttempt => AuditEventType.SystemError,
        _ => AuditEventType.SystemError
    };

    /// <summary>
    /// Gets audit outcome based on event type
    /// </summary>
    private static AuditOutcome GetOutcome(SecurityEventType eventType) => eventType switch
    {
        SecurityEventType.AuthenticationSuccess => AuditOutcome.Success,
        SecurityEventType.DataAccess => AuditOutcome.Success,
        SecurityEventType.DataModification => AuditOutcome.Success,
        SecurityEventType.ConfigurationChange => AuditOutcome.Success,
        SecurityEventType.AuthenticationFailure => AuditOutcome.Failure,
        SecurityEventType.AuthorizationFailure => AuditOutcome.Failure,
        SecurityEventType.SecurityViolation => AuditOutcome.Failure,
        SecurityEventType.SecurityPolicyViolation => AuditOutcome.Failure,
        SecurityEventType.AuthenticationBypassAttempt => AuditOutcome.Failure,
        SecurityEventType.DataExfiltrationAttempt => AuditOutcome.Failure,
        _ => AuditOutcome.Warning
    };

    /// <summary>
    /// Sanitizes event details to prevent log injection
    /// </summary>
    private static string? SanitizeDetails(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return null;
        }

        // Remove newlines and other control characters
        return SecurityValidator.SanitizeUserInput(details);
    }

    /// <summary>
    /// Sanitizes configuration value for logging
    /// </summary>
    private static string SanitizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        // Mask sensitive values
        if (value.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("key", StringComparison.OrdinalIgnoreCase))
        {
            return "***REDACTED***";
        }

        // Truncate long values
        if (value.Length > 100)
        {
            return value.Substring(0, 100) + "...";
        }

        return value;
    }
}
