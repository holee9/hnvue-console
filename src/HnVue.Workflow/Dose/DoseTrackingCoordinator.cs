namespace HnVue.Workflow.Dose;

using HnVue.Workflow.Study;

/// <summary>
/// Coordinates radiation dose tracking across exposures and studies.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Dose tracking coordinator - central radiation dose management
/// @MX:REASON: Safety-critical - enforces dose limits across all exposures
/// @MX:SPEC: SPEC-WORKFLOW-001 NFR-SAFETY-02
///
/// This coordinator provides:
/// - Per-study cumulative dose tracking
/// - Per-patient dose limit enforcement
/// - Dose alert threshold monitoring
/// - RDSR (Radiation Dose Structured Report) data aggregation
/// </remarks>
public sealed class DoseTrackingCoordinator
{
    private readonly Dictionary<string, StudyDoseTracker> _studyTrackers = new();
    private readonly Dictionary<string, PatientDoseSummary> _patientRecords = new();
    private readonly object _lock = new();
    private readonly DoseLimitConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoseTrackingCoordinator"/> class.
    /// </summary>
    /// <param name="configuration">The dose limit configuration.</param>
    public DoseTrackingCoordinator(DoseLimitConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Records a dose entry from an exposure.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="patientId">The patient identifier.</param>
    /// <param name="exposure">The exposure record with dose data.</param>
    /// <returns>The cumulative dose summary after recording.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Dose recording - tracks each radiation exposure
    /// @MX:WARN: Safety-critical - updates cumulative radiation dose
    /// </remarks>
    public CumulativeDoseSummary RecordDose(string studyId, string patientId, ExposureRecord exposure)
    {
        lock (_lock)
        {
            // Get or create study tracker
            if (!_studyTrackers.TryGetValue(studyId, out var tracker))
            {
                tracker = new StudyDoseTracker(studyId, patientId, _configuration);
                _studyTrackers[studyId] = tracker;
            }

            // Record the exposure dose
            var summary = tracker.RecordExposure(exposure);

            // Update patient record
            UpdatePatientRecord(patientId, exposure);

            return summary;
        }
    }

    /// <summary>
    /// Gets the cumulative dose for the specified study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <returns>The cumulative dose summary, or null if the study doesn't exist.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Cumulative dose retrieval - gets total study dose
    /// @MX:WARN: Safety-critical - dose monitoring
    /// </remarks>
    public CumulativeDoseSummary? GetCumulativeDose(string studyId)
    {
        lock (_lock)
        {
            return _studyTrackers.TryGetValue(studyId, out var tracker)
                ? tracker.GetCumulativeDose()
                : null;
        }
    }

    /// <summary>
    /// Checks whether a proposed exposure would exceed dose limits.
    /// </summary>
    /// <param name="patientId">The patient identifier.</param>
    /// <param name="proposedDap">The proposed DAP in cGycm².</param>
    /// <returns>The dose limit check result.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Dose limit check - prevents excessive radiation
    /// @MX:WARN: Safety-critical - dose limit enforcement
    /// </remarks>
    public DoseLimitCheckResult CheckDoseLimits(string patientId, decimal proposedDap)
    {
        lock (_lock)
        {
            var patientRecord = _patientRecords.GetValueOrDefault(patientId);
            var currentDose = patientRecord?.TotalCumulativeDap ?? 0;

            var projectedDose = currentDose + proposedDap;
            var studyLimit = _configuration.StudyDoseLimit;
            var dailyLimit = _configuration.DailyDoseLimit;
            var warningThreshold = _configuration.WarningThresholdPercent;

            var withinStudy = !studyLimit.HasValue || projectedDose <= studyLimit.Value;
            var withinDaily = !dailyLimit.HasValue || projectedDose <= dailyLimit.Value;

            // Calculate warning threshold - warn when projected dose exceeds threshold percentage of limit
            var studyWarningThreshold = studyLimit.HasValue ? studyLimit.Value * warningThreshold : (decimal?)null;
            var dailyWarningThreshold = dailyLimit.HasValue ? dailyLimit.Value * warningThreshold : (decimal?)null;

            var shouldWarnStudy = studyWarningThreshold.HasValue && projectedDose > studyWarningThreshold.Value;
            var shouldWarnDaily = dailyWarningThreshold.HasValue && projectedDose > dailyWarningThreshold.Value;

            var result = new DoseLimitCheckResult
            {
                CurrentCumulativeDose = currentDose,
                ProposedDose = proposedDap,
                ProjectedCumulativeDose = projectedDose,
                WithinStudyLimit = withinStudy,
                WithinDailyLimit = withinDaily,
                IsWithinLimits = withinStudy && withinDaily,
                ShouldWarn = shouldWarnStudy || shouldWarnDaily
            };

            return result;
        }
    }

    /// <summary>
    /// Gets the patient's total cumulative dose across all studies.
    /// </summary>
    /// <param name="patientId">The patient identifier.</param>
    /// <returns>The patient's cumulative dose, or null if no records exist.</returns>
    public PatientDoseSummary? GetPatientCumulativeDose(string patientId)
    {
        lock (_lock)
        {
            return _patientRecords.GetValueOrDefault(patientId);
        }
    }

    /// <summary>
    /// Clears the dose tracking data for a completed study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    public void ClearStudy(string studyId)
    {
        lock (_lock)
        {
            _studyTrackers.Remove(studyId);
        }
    }

    private void UpdatePatientRecord(string patientId, ExposureRecord exposure)
    {
        if (!_patientRecords.TryGetValue(patientId, out var record))
        {
            record = new PatientDoseSummary
            {
                PatientId = patientId,
                TotalCumulativeDap = 0,
                TotalExposures = 0,
                FirstExposureDate = exposure.AcquiredAt ?? DateTimeOffset.Now,
                LastExposureDate = exposure.AcquiredAt ?? DateTimeOffset.Now
            };
            _patientRecords[patientId] = record;
        }

        if (exposure.AdministeredDap.HasValue)
        {
            record.TotalCumulativeDap += exposure.AdministeredDap.Value;
        }
        record.TotalExposures++;
        record.LastExposureDate = exposure.AcquiredAt ?? DateTimeOffset.Now;
    }
}

/// <summary>
/// Tracks dose accumulation for a single study.
/// </summary>
/// <remarks>
/// @MX:NOTE: Study dose tracker - per-study dose accumulation
/// </remarks>
internal sealed class StudyDoseTracker
{
    private readonly List<ExposureRecord> _exposures = new();
    private readonly string _studyId;
    private readonly string _patientId;
    private readonly DoseLimitConfiguration _configuration;

    public StudyDoseTracker(string studyId, string patientId, DoseLimitConfiguration configuration)
    {
        _studyId = studyId;
        _patientId = patientId;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Records an exposure and returns the updated cumulative dose.
    /// </summary>
    public CumulativeDoseSummary RecordExposure(ExposureRecord exposure)
    {
        _exposures.Add(exposure);
        return GetCumulativeDose();
    }

    /// <summary>
    /// Gets the current cumulative dose.
    /// </summary>
    /// <remarks>
    /// @MX:ANCHOR: Cumulative dose calculation - checks dose limits
    /// @MX:WARN: Safety-critical - enforces radiation dose limits
    /// </remarks>
    public CumulativeDoseSummary GetCumulativeDose()
    {
        decimal totalDap = 0;
        int acceptedCount = 0;

        foreach (var exp in _exposures)
        {
            if (exp.AdministeredDap.HasValue)
            {
                totalDap += exp.AdministeredDap.Value;
            }
            if (exp.Status == ExposureStatus.Accepted)
            {
                acceptedCount++;
            }
        }

        // Check dose limits - StudyDoseLimit is the primary limit for a single study
        var doseLimit = _configuration.StudyDoseLimit;
        var isWithinLimits = !doseLimit.HasValue || totalDap <= doseLimit.Value;

        return new CumulativeDoseSummary
        {
            StudyId = _studyId,
            PatientId = _patientId,
            TotalDap = totalDap,
            ExposureCount = _exposures.Count,
            AcceptedCount = acceptedCount,
            IsWithinLimits = isWithinLimits,
            DoseLimit = doseLimit
        };
    }
}

/// <summary>
/// Represents dose limit configuration.
/// </summary>
/// <remarks>
/// @MX:NOTE: Dose limit configuration - configurable safety thresholds
/// </remarks>
public sealed class DoseLimitConfiguration
{
    /// <summary>
    /// Gets or sets the maximum allowed dose per study in cGycm².
    /// </summary>
    public decimal? StudyDoseLimit { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed dose per day in cGycm².
    /// </summary>
    public decimal? DailyDoseLimit { get; set; }

    /// <summary>
    /// Gets or sets the warning threshold as a percentage of the limit.
    /// </summary>
    public decimal WarningThresholdPercent { get; set; } = 0.8m; // 80% by default
}

/// <summary>
/// Represents the result of a dose limit check.
/// </summary>
/// <remarks>
/// @MX:NOTE: Dose limit check result - dose validation outcome
/// </remarks>
public sealed class DoseLimitCheckResult
{
    /// <summary>
    /// Gets or sets the current cumulative dose before the proposed exposure.
    /// </summary>
    public required decimal CurrentCumulativeDose { get; init; }

    /// <summary>
    /// Gets or sets the proposed dose for the new exposure.
    /// </summary>
    public required decimal ProposedDose { get; init; }

    /// <summary>
    /// Gets or sets the projected cumulative dose if the exposure proceeds.
    /// </summary>
    public required decimal ProjectedCumulativeDose { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the dose is within study limits.
    /// </summary>
    public required bool WithinStudyLimit { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the dose is within daily limits.
    /// </summary>
    public required bool WithinDailyLimit { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether all limits are satisfied.
    /// </summary>
    public required bool IsWithinLimits { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether a warning should be issued.
    /// </summary>
    public bool ShouldWarn { get; set; }
}

/// <summary>
/// Represents a patient's cumulative dose summary across all studies.
/// </summary>
/// <remarks>
/// @MX:NOTE: Patient dose summary - cross-study dose aggregation
/// </remarks>
public sealed class PatientDoseSummary
{
    /// <summary>
    /// Gets or sets the patient identifier.
    /// </summary>
    public required string PatientId { get; set; }

    /// <summary>
    /// Gets or sets the total cumulative dose in cGycm².
    /// </summary>
    public required decimal TotalCumulativeDap { get; set; }

    /// <summary>
    /// Gets or sets the total number of exposures recorded.
    /// </summary>
    public required int TotalExposures { get; set; }

    /// <summary>
    /// Gets or sets the date of the first recorded exposure.
    /// </summary>
    public required DateTimeOffset FirstExposureDate { get; set; }

    /// <summary>
    /// Gets or sets the date of the most recent exposure.
    /// </summary>
    public required DateTimeOffset LastExposureDate { get; set; }
}
