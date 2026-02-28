using HnVue.Dicom.Rdsr;
using Microsoft.Extensions.Logging;

namespace HnVue.Dose.Recording;

/// <summary>
/// Accumulates cumulative dose data per patient study.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of cumulative dose tracking - SPEC-DOSE-001 FR-DOSE-03
/// @MX:REASON: Core component for patient study dose aggregation
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-03
///
/// Maintains cumulative DAP and exposure count for each active study.
/// Thread-safe: Uses lock for concurrent exposure event processing.
///
/// State-Driven Behavior (FR-DOSE-03-C, FR-DOSE-03-D):
/// - Active study: Events associated with current study
/// - No active study: Events retained in holding buffer
/// </remarks>
public sealed class StudyDoseAccumulator
{
    private readonly ILogger<StudyDoseAccumulator> _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, StudyAccumulationState> _activeStudies = new();
    private readonly List<DoseRecord> _holdingBuffer = new();

    /// <summary>
    /// Initializes a new instance of the StudyDoseAccumulator class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public StudyDoseAccumulator(ILogger<StudyDoseAccumulator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current active study UID, if any.
    /// </summary>
    public string? ActiveStudyUid { get; private set; }

    /// <summary>
    /// Gets whether an active study session is open.
    /// </summary>
    public bool HasActiveStudy => !string.IsNullOrEmpty(ActiveStudyUid);

    /// <summary>
    /// Opens a new study session for dose accumulation.
    /// </summary>
    /// <param name="studyInstanceUid">DICOM Study Instance UID</param>
    /// <param name="patientId">Patient ID</param>
    /// <exception cref="ArgumentNullException">Thrown when studyInstanceUid or patientId is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when a study is already active</exception>
    /// <remarks>
    /// Associates subsequent exposure events with this study until closed.
    /// </remarks>
    public void OpenStudy(string studyInstanceUid, string patientId)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            throw new ArgumentException("Study Instance UID is required.", nameof(studyInstanceUid));
        }

        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient ID is required.", nameof(patientId));
        }

        lock (_lock)
        {
            if (HasActiveStudy)
            {
                throw new InvalidOperationException($"Cannot open study {studyInstanceUid}: Study {ActiveStudyUid} is already active.");
            }

            ActiveStudyUid = studyInstanceUid;
            _activeStudies[studyInstanceUid] = new StudyAccumulationState
            {
                StudyInstanceUid = studyInstanceUid,
                PatientId = patientId,
                CumulativeDapGyCm2 = 0m,
                ExposureCount = 0,
                StartedAtUtc = DateTime.UtcNow
            };

            _logger.LogInformation(
                "Study session opened: Study={StudyUid}, Patient={PatientId}",
                studyInstanceUid, patientId);
        }
    }

    /// <summary>
    /// Closes the current active study session.
    /// </summary>
    /// <returns>Final cumulative dose summary for the closed study</returns>
    /// <exception cref="InvalidOperationException">Thrown when no study is active</exception>
    /// <remarks>
    /// Finalizes cumulative dose tracking and returns summary for RDSR generation.
    /// </remarks>
    public StudyDoseAccumulation CloseStudy()
    {
        lock (_lock)
        {
            if (!HasActiveStudy || ActiveStudyUid is null)
            {
                throw new InvalidOperationException("No active study to close.");
            }

            var studyUid = ActiveStudyUid;
            var state = _activeStudies[studyUid];

            // Remove from active studies but keep state for return
            _activeStudies.Remove(studyUid);
            ActiveStudyUid = null;

            _logger.LogInformation(
                "Study session closed: Study={StudyUid}, Patient={PatientId}, CumulativeDAP={CumulativeDap}Gy·cm², Exposures={ExposureCount}",
                studyUid, state.PatientId, state.CumulativeDapGyCm2, state.ExposureCount);

            return new StudyDoseAccumulation(
                state.StudyInstanceUid,
                state.PatientId,
                state.CumulativeDapGyCm2,
                state.ExposureCount,
                state.StartedAtUtc,
                DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Adds an exposure dose record to the current study accumulation.
    /// </summary>
    /// <param name="record">Dose record from exposure event</param>
    /// <returns>Updated cumulative dose summary</returns>
    /// <exception cref="ArgumentNullException">Thrown when record is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when no study is active</exception>
    /// <remarks>
    /// Updates cumulative DAP and increments exposure count.
    /// Thread-safe: Can be called from multiple threads concurrently.
    /// </remarks>
    public StudyDoseAccumulation AddExposure(DoseRecord record)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        lock (_lock)
        {
            if (!HasActiveStudy)
            {
                // State-Driven: Add to holding buffer when no active study
                _holdingBuffer.Add(record);
                _logger.LogWarning(
                    "No active study. Exposure record {ExposureId} added to holding buffer.",
                    record.ExposureEventId);

                return new StudyDoseAccumulation(
                    record.StudyInstanceUid,
                    record.PatientId,
                    record.CalculatedDapGyCm2,
                    1,
                    record.TimestampUtc,
                    DateTime.UtcNow);
            }

            if (record.StudyInstanceUid != ActiveStudyUid)
            {
                throw new InvalidOperationException(
                    $"Exposure study {record.StudyInstanceUid} does not match active study {ActiveStudyUid}.");
            }

            var state = _activeStudies[ActiveStudyUid];
            state.CumulativeDapGyCm2 += record.CalculatedDapGyCm2;
            state.ExposureCount++;

            _logger.LogDebug(
                "Exposure added to study {StudyUid}: DAP={Dap}Gy·cm², Cumulative={Cumulative}Gy·cm², Count={Count}",
                ActiveStudyUid, record.CalculatedDapGyCm2, state.CumulativeDapGyCm2, state.ExposureCount);

            return new StudyDoseAccumulation(
                state.StudyInstanceUid,
                state.PatientId,
                state.CumulativeDapGyCm2,
                state.ExposureCount,
                state.StartedAtUtc,
                DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Gets the current accumulation state for the active study.
    /// </summary>
    /// <returns>Current accumulation summary or null if no active study</returns>
    public StudyDoseAccumulation? GetCurrentAccumulation()
    {
        lock (_lock)
        {
            if (!HasActiveStudy || ActiveStudyUid is null)
            {
                return null;
            }

            var state = _activeStudies[ActiveStudyUid];
            return new StudyDoseAccumulation(
                state.StudyInstanceUid,
                state.PatientId,
                state.CumulativeDapGyCm2,
                state.ExposureCount,
                state.StartedAtUtc,
                DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Associates records in the holding buffer with a newly opened study.
    /// </summary>
    /// <param name="studyInstanceUid">Study to associate buffered records with</param>
    /// <returns>Number of records associated</returns>
    /// <remarks>
    /// Called after opening a study to process any records accumulated while no study was active.
    /// </remarks>
    public int AssociateHoldingBuffer(string studyInstanceUid)
    {
        lock (_lock)
        {
            var count = 0;
            var toAssociate = _holdingBuffer.Where(r => r.StudyInstanceUid == studyInstanceUid).ToList();

            foreach (var record in toAssociate)
            {
                _holdingBuffer.Remove(record);
                AddExposure(record);
                count++;
            }

            if (count > 0)
            {
                _logger.LogInformation(
                    "Associated {Count} records from holding buffer with study {StudyUid}",
                    count, studyInstanceUid);
            }

            return count;
        }
    }

    /// <summary>
    /// Internal state for study accumulation.
    /// </summary>
    private sealed class StudyAccumulationState
    {
        public required string StudyInstanceUid { get; init; }
        public required string PatientId { get; init; }
        public decimal CumulativeDapGyCm2 { get; set; }
        public int ExposureCount { get; set; }
        public required DateTime StartedAtUtc { get; init; }
    }
}

/// <summary>
/// Summary of dose accumulation for a study.
/// </summary>
/// <param name="StudyInstanceUid">DICOM Study Instance UID</param>
/// <param name="PatientId">Patient ID</param>
/// <param name="CumulativeDapGyCm2">Cumulative DAP in Gy·cm²</param>
/// <param name="ExposureCount">Total number of exposures</param>
/// <param name="StudyStartedAtUtc">Study start timestamp</param>
/// <param name="LastUpdatedUtc">Last update timestamp</param>
public sealed record StudyDoseAccumulation(
    string StudyInstanceUid,
    string PatientId,
    decimal CumulativeDapGyCm2,
    int ExposureCount,
    DateTime StudyStartedAtUtc,
    DateTime LastUpdatedUtc);
