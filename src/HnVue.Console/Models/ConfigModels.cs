namespace HnVue.Console.Models;

/// <summary>
/// Configuration section enumeration.
/// SPEC-UI-001: FR-UI-08 System Configuration.
/// </summary>
public enum ConfigSection
{
    Calibration,
    Network,
    Users,
    Logging
}

/// <summary>
/// System configuration.
/// </summary>
public record SystemConfig
{
    public required CalibrationConfig? Calibration { get; init; }
    public required NetworkConfig? Network { get; init; }
    public required UserConfig? Users { get; init; }
    public required LoggingConfig? Logging { get; init; }
}

/// <summary>
/// Calibration configuration.
/// </summary>
public record CalibrationConfig
{
    public required DateTimeOffset LastCalibrationDate { get; init; }
    public required DateTimeOffset NextCalibrationDueDate { get; init; }
    public required bool IsCalibrationValid { get; init; }
    public required CalibrationStatus Status { get; init; }
}

/// <summary>
/// Calibration status enumeration.
/// </summary>
public enum CalibrationStatus
{
    Valid,
    Warning,
    Expired,
    Required
}

/// <summary>
/// Network configuration.
/// </summary>
public record NetworkConfig
{
    public required string DicomAeTitle { get; init; }
    public required string DicomPort { get; init; }
    public required string PacsHostName { get; init; }
    public required int PacsPort { get; init; }
    public required bool MwlEnabled { get; init; }
}

/// <summary>
/// User configuration.
/// </summary>
public record UserConfig
{
    public required IReadOnlyList<User> Users { get; init; }
}

/// <summary>
/// User account.
/// </summary>
public record User
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required UserRole Role { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>
/// User role enumeration.
/// SPEC-SECURITY-001: FR-SEC-01 Role-Based Access Control (RBAC).
/// Supports 7 roles as per medical device cybersecurity requirements.
/// </summary>
public enum UserRole
{
    /// <summary>Unspecified/Unknown role</summary>
    Unspecified,

    /// <summary>Full system access - can manage users, configuration, all operations</summary>
    Administrator,

    /// <summary>Physician/Radiologist - can sign reports, access patient data</summary>
    Radiologist,

    /// <summary>Radiologic Technologist - can perform exposures</summary>
    Technologist,

    /// <summary>Medical Physicist - can perform QC, manage equipment settings</summary>
    Physicist,

    /// <summary>Basic operator - limited operational access</summary>
    Operator,

    /// <summary>Read-only access - view data only, no modifications</summary>
    Viewer,

    /// <summary>Service/Maintenance engineer - calibration and maintenance</summary>
    Service
}

/// <summary>
/// Logging configuration.
/// </summary>
public record LoggingConfig
{
    public required LogLevel MinimumLogLevel { get; init; }
    public required int RetentionDays { get; init; }
    public required bool EnableRemoteLogging { get; init; }
}

/// <summary>
/// Log level enumeration.
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Configuration update request.
/// </summary>
public record ConfigUpdate
{
    public required ConfigSection Section { get; init; }
    public required object UpdateData { get; init; }
}
