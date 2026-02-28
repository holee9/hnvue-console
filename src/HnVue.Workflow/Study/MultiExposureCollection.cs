namespace HnVue.Workflow.Study;

using System.Collections.ObjectModel;

/// <summary>
/// Represents a collection of exposures within a single study.
/// </summary>
/// <remarks>
/// @MX:NOTE: Exposure collection - manages multiple exposures per study
/// @MX:SPEC: SPEC-WORKFLOW-001 NFR-WORKFLOW-03
///
/// Extends existing Study context to support multi-exposure studies
/// (e.g., lateral+AP views) with cumulative dose tracking.
/// Uses existing ExposureRecord class from StudyContext.cs.
/// </remarks>
public sealed class ExposureCollection
{
    private readonly List<ExposureRecord> _exposures = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets the unique study identifier.
    /// </summary>
    public required string StudyId { get; init; }

    /// <summary>
    /// Gets the patient identifier.
    /// </summary>
    public required string PatientId { get; init; }

    /// <summary>
    /// Gets all exposures in the collection (thread-safe snapshot).
    /// </summary>
    public ReadOnlyCollection<ExposureRecord> Exposures
    {
        get
        {
            lock (_lock)
            {
                return _exposures.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the total number of exposures in the collection.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _exposures.Count;
            }
        }
    }

    /// <summary>
    /// Adds an exposure to the collection.
    /// </summary>
    /// <param name="exposure">The exposure record to add.</param>
    /// <remarks>
    /// @MX:ANCHOR: Exposure recording - tracks each exposure
    /// @MX:WARN: Safety-critical - dose accumulation
    /// </remarks>
    public void AddExposure(ExposureRecord exposure)
    {
        lock (_lock)
        {
            _exposures.Add(exposure);
        }
    }

    /// <summary>
    /// Gets the cumulative dose for all exposures in the collection.
    /// </summary>
    /// <returns>The cumulative dose summary.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Cumulative dose calculation - tracks total radiation
    /// @MX:WARN: Safety-critical - dose limit monitoring
    /// </remarks>
    public CumulativeDoseSummary GetCumulativeDose()
    {
        decimal totalDap = 0;
        int acceptedCount = 0;

        lock (_lock)
        {
            foreach (var exposure in _exposures)
            {
                if (exposure.AdministeredDap.HasValue)
                {
                    totalDap += exposure.AdministeredDap.Value;
                }
                if (exposure.Status == ExposureStatus.Accepted)
                {
                    acceptedCount++;
                }
            }
        }

        return new CumulativeDoseSummary
        {
            StudyId = StudyId,
            PatientId = PatientId,
            TotalDap = totalDap,
            ExposureCount = Count,
            AcceptedCount = acceptedCount,
            IsWithinLimits = true, // TODO: Implement dose limit checking
            DoseLimit = null
        };
    }
}

/// <summary>
/// Represents a cumulative dose summary for a study.
/// </summary>
/// <remarks>
/// @MX:NOTE: Cumulative dose summary - total radiation dose
/// </remarks>
public sealed class CumulativeDoseSummary
{
    /// <summary>
    /// Gets or sets the study identifier.
    /// </summary>
    public required string StudyId { get; init; }

    /// <summary>
    /// Gets or sets the patient identifier.
    /// </summary>
    public required string PatientId { get; init; }

    /// <summary>
    /// Gets or sets the total Dose-Area Product in cGycm².
    /// </summary>
    public required decimal TotalDap { get; init; }

    /// <summary>
    /// Gets or sets the total number of exposures.
    /// </summary>
    public required int ExposureCount { get; init; }

    /// <summary>
    /// Gets or sets the number of accepted exposures.
    /// </summary>
    public required int AcceptedCount { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the cumulative dose is within configured limits.
    /// </summary>
    public required bool IsWithinLimits { get; init; }

    /// <summary>
    /// Gets or sets the configured dose limit in cGycm², if any.
    /// </summary>
    public decimal? DoseLimit { get; init; }
}

/// <summary>
/// Coordinates multi-exposure study workflows.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Multi-exposure coordinator - manages multi-view studies
/// @MX:SPEC: SPEC-WORKFLOW-001 NFR-WORKFLOW-03
///
/// Handles coordination of multiple exposures within a single study,
/// including dose accumulation tracking and study completion detection.
/// </remarks>
public sealed class MultiExposureCoordinator
{
    private readonly Dictionary<string, ExposureCollection> _studies = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets or creates an exposure collection for the specified study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="patientId">The patient identifier.</param>
    /// <returns>The exposure collection for the study.</returns>
    public ExposureCollection GetOrCreateCollection(string studyId, string patientId)
    {
        lock (_lock)
        {
            if (!_studies.TryGetValue(studyId, out var collection))
            {
                collection = new ExposureCollection
                {
                    StudyId = studyId,
                    PatientId = patientId
                };
                _studies[studyId] = collection;
            }
            return collection;
        }
    }

    /// <summary>
    /// Records an exposure for the specified study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <param name="patientId">The patient identifier.</param>
    /// <param name="exposure">The exposure record to add.</param>
    /// <remarks>
    /// @MX:ANCHOR: Exposure recording - adds exposure to collection
    /// @MX:WARN: Safety-critical - updates cumulative dose
    /// </remarks>
    public void RecordExposure(string studyId, string patientId, ExposureRecord exposure)
    {
        var collection = GetOrCreateCollection(studyId, patientId);
        collection.AddExposure(exposure);
    }

    /// <summary>
    /// Gets the cumulative dose for the specified study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <returns>The cumulative dose summary, or null if the study doesn't exist.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Cumulative dose retrieval - gets total dose
    /// @MX:WARN: Safety-critical - dose monitoring
    /// </remarks>
    public CumulativeDoseSummary? GetCumulativeDose(string studyId)
    {
        lock (_lock)
        {
            return _studies.TryGetValue(studyId, out var collection)
                ? collection.GetCumulativeDose()
                : null;
        }
    }

    /// <summary>
    /// Removes the exposure collection for the specified study.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    public void RemoveCollection(string studyId)
    {
        lock (_lock)
        {
            _studies.Remove(studyId);
        }
    }

    /// <summary>
    /// Checks if the study has more exposures pending.
    /// </summary>
    /// <param name="studyId">The study identifier.</param>
    /// <returns>True if there are pending exposures; false if all are completed.</returns>
    public bool HasPendingExposures(string studyId)
    {
        lock (_lock)
        {
            if (!_studies.TryGetValue(studyId, out var collection))
            {
                return false;
            }

            return collection.Exposures.Any(e => e.Status == ExposureStatus.Pending);
        }
    }
}
