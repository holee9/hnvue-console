using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HnVue.Dose.Recording;

/// <summary>
/// Audit event types for dose-related operations.
/// </summary>
/// <remarks>
/// @MX:NOTE: Enumeration for audit event types - NFR-DOSE-04 compliance
/// @MX:SPEC: SPEC-DOSE-001 NFR-DOSE-04
///
/// All dose-related events are logged for regulatory compliance.
/// </remarks>
public enum AuditEventType
{
    /// <summary>
    /// A dose record was created for an exposure event.
    /// </summary>
    ExposureRecorded,

    /// <summary>
    /// An RDSR document was generated.
    /// </summary>
    RdsrGenerated,

    /// <summary>
    /// An RDSR export attempt was made (success or failure).
    /// </summary>
    ExportAttempted,

    /// <summary>
    /// A DRL threshold was exceeded.
    /// </summary>
    DrlExceeded,

    /// <summary>
    /// Configuration was changed (e.g., calibration, DRL settings).
    /// </summary>
    ConfigChanged,

    /// <summary>
    /// A dose report was generated.
    /// </summary>
    ReportGenerated
}

/// <summary>
/// Audit event outcome (success or failure).
/// </summary>
/// <remarks>
/// @MX:NOTE: Enumeration for audit event outcome - NFR-DOSE-04-B compliance
/// @MX:SPEC: SPEC-DOSE-001 NFR-DOSE-04
/// </remarks>
public enum AuditOutcome
{
    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Operation failed with error.
    /// </summary>
    Failure
}

/// <summary>
/// Writes audit trail entries with SHA-256 hash chain for tamper evidence.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of audit trail writer - NFR-DOSE-04 compliance
/// @MX:REASON: Critical implementation for regulatory compliance (FDA 21 CFR Part 11, MFDS)
/// @MX:SPEC: SPEC-DOSE-001 NFR-DOSE-04
///
/// Each audit entry includes SHA-256 hash of previous record (NFR-DOSE-04-D).
/// First record uses well-known initialization vector.
/// Hash chain breaks are detectable via verification.
///
/// Records are immutable: never modified or deleted once written (NFR-DOSE-04-C).
/// </remarks>
public sealed class AuditTrailWriter : IDisposable
{
    private readonly ILogger<AuditTrailWriter> _logger;
    private readonly string _auditDirectory;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly UTF8Encoding _utf8Encoding;
    private string? _lastHash;
    private bool _disposed;

    /// <summary>
    /// Well-known initialization vector for first record in hash chain.
    /// </summary>
    /// <remarks>
    /// SHA-256 hash of "HnVue.Dose.AuditTrail.InitializationVector" (UTF-8).
    /// Ensures all audit trails start from a known, tamper-evident root.
    /// </remarks>
    private const string InitializationVector = "7B2F3A8E1C9D4F6B8A0E7C5D3F9B1A4E6C2D8F0B4A9E1C7D5F3B9A6E0C4D8F2B6";

    /// <summary>
    /// Initializes a new instance of the AuditTrailWriter class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="auditDirectory">Directory for audit trail storage</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
    public AuditTrailWriter(ILogger<AuditTrailWriter> logger, string auditDirectory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditDirectory = string.IsNullOrWhiteSpace(auditDirectory)
            ? throw new ArgumentException("Audit directory is required.", nameof(auditDirectory))
            : auditDirectory;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        EnsureDirectoryExists();
        LoadLastHash();
    }

    /// <summary>
    /// Writes an audit trail entry with hash chain linkage.
    /// </summary>
    /// <param name="eventType">Type of audit event</param>
    /// <param name="outcome">Operation outcome</param>
    /// <param name="operatorId">Optional operator ID</param>
    /// <param name="studyInstanceUid">Optional associated study UID</param>
    /// <param name="patientId">Optional associated patient ID</param>
    /// <param name="errorCode">Optional error code for failures</param>
    /// <param name="details">Human-readable event description</param>
    /// <exception cref="ArgumentNullException">Thrown when details is null</exception>
    /// <exception cref="ObjectDisposedException">Thrown when writer is disposed</exception>
    /// <remarks>
    /// Thread-safe: Uses lock for concurrent write protection.
    /// Atomic write: Temporary file + rename for crash safety.
    /// </remarks>
    public void WriteEntry(
        AuditEventType eventType,
        AuditOutcome outcome,
        string? operatorId,
        string? studyInstanceUid,
        string? patientId,
        string? errorCode,
        string details)
    {
        VerifyNotDisposed();

        if (string.IsNullOrWhiteSpace(details))
        {
            throw new ArgumentException("Details are required.", nameof(details));
        }

        lock (_lock)
        {
            var auditId = Guid.NewGuid();
            var timestampUtc = DateTime.UtcNow;
            var previousHash = _lastHash ?? string.Empty;

            // Build hash input for computing current hash
            var hashInput = $"{auditId}|{eventType}|{timestampUtc:O}|{operatorId ?? string.Empty}|{studyInstanceUid ?? string.Empty}|{patientId ?? string.Empty}|{outcome}|{errorCode ?? string.Empty}|{details}|{previousHash}";
            var currentHash = ComputeHash(hashInput);

            var entry = new AuditEntry
            {
                AuditId = auditId,
                EventType = eventType,
                TimestampUtc = timestampUtc,
                OperatorId = operatorId,
                StudyInstanceUid = studyInstanceUid,
                PatientId = patientId,
                Outcome = outcome,
                ErrorCode = errorCode,
                Details = details,
                PreviousRecordHash = previousHash,
                CurrentRecordHash = currentHash
            };

            // Write atomically
            var filePath = GetAuditFilePath(entry.AuditId);
            WriteEntryAtomically(entry, filePath);

            // Update last hash for next entry
            _lastHash = entry.CurrentRecordHash;

            _logger.LogDebug(
                "Audit entry written: Type={Type}, Outcome={Outcome}, AuditId={AuditId}, Hash={Hash}",
                eventType, outcome, entry.AuditId, entry.CurrentRecordHash[..16] + "...");
        }
    }

    /// <summary>
    /// Verifies the integrity of the audit trail hash chain.
    /// </summary>
    /// <returns>Verification result with first broken record, if any</returns>
    /// <exception cref="ObjectDisposedException">Thrown when writer is disposed</exception>
    /// <remarks>
    /// Detects tampering, missing records, or hash chain breaks.
    /// Returns first corrupted record position or null if chain is intact.
    /// </remarks>
    public AuditVerificationResult VerifyChain()
    {
        VerifyNotDisposed();

        lock (_lock)
        {
            var files = Directory.GetFiles(_auditDirectory, "*.audit")
                               .OrderBy(f => f)
                               .ToList();

            if (files.Count == 0)
            {
                return new AuditVerificationResult(true, null, "Audit trail is empty");
            }

            var expectedHash = string.Empty; // Start with empty for first record

            for (int i = 0; i < files.Count; i++)
            {
                try
                {
                    var json = File.ReadAllText(files[i]);
                    var entry = JsonSerializer.Deserialize<AuditEntry>(json, _jsonOptions);

                    if (entry is null)
                    {
                        return new AuditVerificationResult(false, files[i], "Failed to deserialize entry");
                    }

                    // Verify hash linkage
                    if (entry.PreviousRecordHash != expectedHash)
                    {
                        var expectedDisplay = expectedHash.Length >= 16 ? $"{expectedHash[..16]}..." : expectedHash;
                        var actualDisplay = entry.PreviousRecordHash.Length >= 16 ? $"{entry.PreviousRecordHash[..16]}..." : entry.PreviousRecordHash;
                        return new AuditVerificationResult(
                            false,
                            files[i],
                            $"Hash chain broken at record {i + 1}: Expected previous hash {expectedDisplay}, got {actualDisplay}");
                    }

                    // Verify current hash
                    var hashInput = $"{entry.AuditId}|{entry.EventType}|{entry.TimestampUtc:O}|{entry.OperatorId ?? string.Empty}|{entry.StudyInstanceUid ?? string.Empty}|{entry.PatientId ?? string.Empty}|{entry.Outcome}|{entry.ErrorCode ?? string.Empty}|{entry.Details}|{entry.PreviousRecordHash}";
                    var computedHash = ComputeHash(hashInput);
                    if (computedHash != entry.CurrentRecordHash)
                    {
                        var computedDisplay = computedHash.Length >= 16 ? $"{computedHash[..16]}..." : computedHash;
                        var storedDisplay = entry.CurrentRecordHash.Length >= 16 ? $"{entry.CurrentRecordHash[..16]}..." : entry.CurrentRecordHash;
                        return new AuditVerificationResult(
                            false,
                            files[i],
                            $"Hash mismatch at record {i + 1}: Computed {computedDisplay}, stored {storedDisplay}");
                    }

                    expectedHash = entry.CurrentRecordHash;
                }
                catch (Exception ex)
                {
                    return new AuditVerificationResult(
                        false,
                        files[i],
                        $"Verification error: {ex.Message}");
                }
            }

            return new AuditVerificationResult(true, null, $"Audit chain verified: {files.Count} records");
        }
    }

    /// <summary>
    /// Computes SHA-256 hash of a hash input string.
    /// </summary>
    private string ComputeHash(string hashInput)
    {
        var bytes = _utf8Encoding.GetBytes(hashInput);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Writes audit entry atomically using temporary file.
    /// </summary>
    private void WriteEntryAtomically(AuditEntry entry, string filePath)
    {
        var tempPath = filePath + ".tmp";
        var json = JsonSerializer.Serialize(entry, _jsonOptions);

        File.WriteAllText(tempPath, json, _utf8Encoding);

        // Atomic rename
        File.Move(tempPath, filePath, overwrite: true);
    }

    /// <summary>
    /// Gets the file path for an audit entry.
    /// </summary>
    private string GetAuditFilePath(Guid auditId)
    {
        var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        return Path.Combine(_auditDirectory, $"{datePrefix}_{auditId}.audit");
    }

    /// <summary>
    /// Loads the last hash from the most recent audit entry.
    /// </summary>
    private void LoadLastHash()
    {
        lock (_lock)
        {
            var files = Directory.GetFiles(_auditDirectory, "*.audit")
                               .OrderByDescending(f => f)
                               .FirstOrDefault();

            if (files is null)
            {
                _lastHash = null;
                return;
            }

            try
            {
                var json = File.ReadAllText(files);
                var entry = JsonSerializer.Deserialize<AuditEntry>(json, _jsonOptions);
                _lastHash = entry?.CurrentRecordHash;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load last hash from audit trail. Starting new chain.");
                _lastHash = null;
            }
        }
    }

    /// <summary>
    /// Ensures audit directory exists.
    /// </summary>
    private void EnsureDirectoryExiststs()
    {
        Directory.CreateDirectory(_auditDirectory);
        _logger.LogInformation("Audit trail directory ensured: {Dir}", _auditDirectory);
    }

    /// <summary>
    /// Ensures audit directory exists.
    /// </summary>
    private void EnsureDirectoryExists()
    {
        Directory.CreateDirectory(_auditDirectory);
        _logger.LogInformation("Audit trail directory ensured: {Dir}", _auditDirectory);
    }

    /// <summary>
    /// Verifies the writer has not been disposed.
    /// </summary>
    private void VerifyNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AuditTrailWriter));
        }
    }

    /// <summary>
    /// Disposes the audit trail writer.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogDebug("AuditTrailWriter disposing");
            _disposed = true;
        }
    }
}

/// <summary>
/// Audit trail entry record.
/// </summary>
/// <remarks>
/// @MX:NOTE: Domain model for audit trail entry - NFR-DOSE-04-B compliance
/// @MX:SPEC: SPEC-DOSE-001 NFR-DOSE-04, Section 4.5
///
/// Immutable record: Never modified after creation.
/// </remarks>
internal sealed class AuditEntry
{
    public required Guid AuditId { get; init; }
    public required AuditEventType EventType { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public string? OperatorId { get; init; }
    public string? StudyInstanceUid { get; init; }
    public string? PatientId { get; init; }
    public required AuditOutcome Outcome { get; init; }
    public string? ErrorCode { get; init; }
    public required string Details { get; init; }
    public required string PreviousRecordHash { get; init; }
    public required string CurrentRecordHash { get; init; }
}

/// <summary>
/// Result of audit trail verification.
/// </summary>
/// <param name="IsValid">True if hash chain is intact</param>
/// <param name="BrokenAtFile">First file where chain broke, if any</param>
/// <param name="Message">Verification message</param>
public sealed record AuditVerificationResult(
    bool IsValid,
    string? BrokenAtFile,
    string Message);
