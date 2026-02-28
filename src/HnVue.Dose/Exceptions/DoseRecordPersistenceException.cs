namespace HnVue.Dose.Exceptions;

/// <summary>
/// Exception thrown when dose record persistence fails.
/// </summary>
/// <remarks>
/// @MX:NOTE: Domain exception for persistence failures
/// @MX:SPEC: SPEC-DOSE-001 NFR-DOSE-02
///
/// Thrown by IDoseRecordRepository.PersistAsync when atomic write fails.
/// Should NOT be swallowed - caller must handle and log to audit trail.
/// </remarks>
public sealed class DoseRecordPersistenceException : Exception
{
    /// <summary>
    /// Gets the exposure event ID that failed to persist.
    /// </summary>
    public Guid? ExposureEventId { get; }

    /// <summary>
    /// Initializes a new instance of the DoseRecordPersistenceException class.
    /// </summary>
    public DoseRecordPersistenceException()
        : base("Dose record persistence failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance with a specified error message.
    /// </summary>
    public DoseRecordPersistenceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a specified error message and inner exception.
    /// </summary>
    public DoseRecordPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance with exposure event ID and error message.
    /// </summary>
    public DoseRecordPersistenceException(Guid exposureEventId, string message)
        : base($"Dose record persistence failed for exposure {exposureEventId}: {message}")
    {
        ExposureEventId = exposureEventId;
    }

    /// <summary>
    /// Initializes a new instance with exposure event ID, error message, and inner exception.
    /// </summary>
    public DoseRecordPersistenceException(Guid exposureEventId, string message, Exception innerException)
        : base($"Dose record persistence failed for exposure {exposureEventId}: {message}", innerException)
    {
        ExposureEventId = exposureEventId;
    }
}
