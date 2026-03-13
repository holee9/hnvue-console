namespace HnVue.Console.Security.Models;

/// <summary>
/// WORM (Write Once, Read Many) storage entry for audit logs.
/// SPEC-SECURITY-001: FR-SEC-06 - Audit Log Integrity with WORM storage.
/// </summary>
public sealed record WormEntry
{
    /// <summary>
    /// Unique identifier for this audit entry.
    /// </summary>
    public required string EntryId { get; init; }

    /// <summary>
    /// Timestamp when the event occurred (NTP synchronized).
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Type of audit event.
    /// </summary>
    public required int EventType { get; init; }

    /// <summary>
    /// User who initiated the event.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Display name of the user.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Human-readable description of the event.
    /// </summary>
    public required string EventDescription { get; init; }

    /// <summary>
    /// Event outcome (Success, Failure, Warning).
    /// </summary>
    public required int Outcome { get; init; }

    /// <summary>
    /// Associated patient ID (if applicable).
    /// </summary>
    public string? PatientId { get; init; }

    /// <summary>
    /// Associated study ID (if applicable).
    /// </summary>
    public string? StudyId { get; init; }

    /// <summary>
    /// SHA-256 hash of the previous entry for chain integrity.
    /// </summary>
    public string? PreviousEntryHash { get; init; }

    /// <summary>
    /// SHA-256 hash of this entry for integrity verification.
    /// </summary>
    public required string CurrentEntryHash { get; init; }

    /// <summary>
    /// Whether timestamp is NTP synchronized.
    /// </summary>
    public required bool NtpSynchronized { get; init; }

    /// <summary>
    /// Workstation ID where the event originated.
    /// </summary>
    public string? WorkstationId { get; init; }
}
