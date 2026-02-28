namespace HnVue.Workflow.Study;

using System;
using System.Collections.Generic;
using System.Linq;
using HnVue.Workflow.Protocol;

/// <summary>
/// Patient information model.
/// Used for patient selection and emergency workflow.
/// </summary>
public class PatientInfo
{
    public string PatientID { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public DateOnly? PatientBirthDate { get; set; }
    public char? PatientSex { get; set; }
    public bool IsEmergency { get; set; }
    public string? AccessionNumber { get; set; }
    public string? WorklistItemUID { get; set; }
}

/// <summary>
/// Image data model.
/// Used for exposure results and QC review.
/// </summary>
public class ImageData
{
    public string ImageInstanceUID { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitsPerPixel { get; set; }
    public byte[] PixelData { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Study context model.
///
/// SPEC-WORKFLOW-001 Section 7.1: StudyContext
/// Contains patient and study metadata for the current clinical workflow.
/// </summary>
// @MX:ANCHOR: Study context manages patient data and exposure tracking
// @MX:REASON: High fan_in - accessed by state handlers, dose tracking, DICOM export. Critical for PHI handling.
public class StudyContext
{
    /// <summary>
    /// DICOM-compliant UID generated at study start.
    /// </summary>
    public string StudyInstanceUID { get; }

    /// <summary>
    /// From worklist or locally generated (emergency).
    /// </summary>
    public string AccessionNumber { get; }

    /// <summary>
    /// DICOM Patient ID.
    /// </summary>
    public string PatientID { get; }

    /// <summary>
    /// DICOM format: Family^Given^Middle^Prefix^Suffix.
    /// </summary>
    public string PatientName { get; }

    /// <summary>
    /// Optional, from worklist.
    /// </summary>
    public DateOnly? PatientBirthDate { get; set; }

    /// <summary>
    /// M/F/O, from worklist.
    /// </summary>
    public char? PatientSex { get; set; }

    /// <summary>
    /// True if emergency workflow was used.
    /// </summary>
    public bool IsEmergency { get; }

    /// <summary>
    /// DICOM Scheduled Procedure Step UID, null for emergency.
    /// </summary>
    public string? WorklistItemUID { get; }

    /// <summary>
    /// Ordered list of all exposures in the study.
    /// SPEC-WORKFLOW-001 FR-WF-05-d: Ordered exposure series list
    /// </summary>
    public List<ExposureRecord> ExposureSeries { get; }

    /// <summary>
    /// Study context creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Creates a new study context.
    /// </summary>
    public StudyContext(
        string studyInstanceUID,
        string accessionNumber,
        string patientID,
        string patientName,
        bool isEmergency,
        string? worklistItemUID = null,
        DateOnly? patientBirthDate = null,
        char? patientSex = null)
    {
        StudyInstanceUID = studyInstanceUID ?? throw new ArgumentNullException(nameof(studyInstanceUID));
        AccessionNumber = accessionNumber ?? throw new ArgumentNullException(nameof(accessionNumber));
        PatientID = patientID ?? throw new ArgumentNullException(nameof(patientID));
        PatientName = patientName ?? throw new ArgumentNullException(nameof(patientName));
        IsEmergency = isEmergency;
        WorklistItemUID = worklistItemUID;
        PatientBirthDate = patientBirthDate;
        PatientSex = patientSex;
        ExposureSeries = new List<ExposureRecord>();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a new exposure to the study.
    /// SPEC-WORKFLOW-001 FR-WF-05-a: Support for one or more exposures
    /// </summary>
    public ExposureRecord AddExposure(Protocol protocol, string operatorId)
    {
        var exposure = new ExposureRecord
        {
            ExposureIndex = ExposureSeries.Count + 1,
            Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol)),
            Status = ExposureStatus.Pending,
            OperatorId = operatorId ?? throw new ArgumentNullException(nameof(operatorId))
        };

        ExposureSeries.Add(exposure);
        return exposure;
    }

    /// <summary>
    /// Gets whether the study has more pending exposures.
    /// SPEC-WORKFLOW-001 FR-WF-05-b: Return to PROTOCOL_SELECT if more exposures remain
    /// </summary>
    public bool HasMoreExposures =>
        ExposureSeries.Any(e => e.Status == ExposureStatus.Pending);

    /// <summary>
    /// Gets the count of pending exposures.
    /// </summary>
    public int GetPendingExposureCount() =>
        ExposureSeries.Count(e => e.Status == ExposureStatus.Pending);

    /// <summary>
    /// Gets the count of accepted exposures.
    /// </summary>
    public int GetAcceptedExposureCount() =>
        ExposureSeries.Count(e => e.Status == ExposureStatus.Accepted);

    /// <summary>
    /// Gets the count of rejected exposures.
    /// </summary>
    public int GetRejectedExposureCount() =>
        ExposureSeries.Count(e => e.Status == ExposureStatus.Rejected);

    /// <summary>
    /// Gets the total exposure count (accepted + rejected).
    /// </summary>
    public int GetTotalExposureCount() =>
        GetAcceptedExposureCount() + GetRejectedExposureCount();

    /// <summary>
    /// Updates the status of an exposure by index.
    /// </summary>
    public void UpdateExposureStatus(int exposureIndex, ExposureStatus newStatus)
    {
        var exposure = GetExposureByIndex(exposureIndex);
        exposure.Status = newStatus;
    }

    /// <summary>
    /// Records acquisition data for an exposure.
    /// SPEC-WORKFLOW-001 FR-WF-06-b: Record rejection event with dose
    /// </summary>
    public void RecordAcquisition(int exposureIndex, string imageInstanceUID, decimal administeredDap)
    {
        var exposure = GetExposureByIndex(exposureIndex);
        exposure.ImageInstanceUID = imageInstanceUID ?? throw new ArgumentNullException(nameof(imageInstanceUID));
        exposure.AdministeredDap = administeredDap;
        exposure.Status = ExposureStatus.Acquired;
        exposure.AcquiredAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records rejection for an exposure.
    /// SPEC-WORKFLOW-001 FR-WF-06-a: Require structured reject reason
    /// </summary>
    public void RecordRejection(int exposureIndex, RejectReason rejectReason, string operatorId)
    {
        var exposure = GetExposureByIndex(exposureIndex);
        exposure.Status = ExposureStatus.Rejected;
        exposure.RejectReason = rejectReason;
        exposure.RejectedBy = operatorId ?? throw new ArgumentNullException(nameof(operatorId));
        exposure.RejectedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the total dose for the study (including rejected exposures).
    /// SPEC-WORKFLOW-001 FR-WF-06-d: Rejected exposure dose included in cumulative study dose
    /// </summary>
    public decimal GetTotalDose() =>
        ExposureSeries
            .Where(e => e.AdministeredDap.HasValue)
            .Sum(e => e.AdministeredDap!.Value);

    /// <summary>
    /// Gets the next pending exposure, or null if none exist.
    /// </summary>
    public ExposureRecord? GetNextPendingExposure() =>
        ExposureSeries.FirstOrDefault(e => e.Status == ExposureStatus.Pending);

    private ExposureRecord GetExposureByIndex(int exposureIndex)
    {
        if (exposureIndex < 1 || exposureIndex > ExposureSeries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(exposureIndex),
                $"Exposure index {exposureIndex} is out of range (1-{ExposureSeries.Count})");
        }

        return ExposureSeries[exposureIndex - 1];
    }
}

/// <summary>
/// Exposure record model.
///
/// SPEC-WORKFLOW-001 Section 7.2: ExposureRecord
/// Tracks individual exposure within a study.
/// </summary>
public class ExposureRecord
{
    /// <summary>
    /// Sequence number within study (1-based).
    /// SPEC-WORKFLOW-001 FR-WF-05-d: Ordered exposure series list
    /// </summary>
    public int ExposureIndex { get; init; }

    /// <summary>
    /// Protocol snapshot at time of acquisition.
    /// SPEC-WORKFLOW-001 Section 8.2: Immutable Protocol Snapshot
    /// </summary>
    public Protocol Protocol { get; init; } = null!;

    /// <summary>
    /// Current status of the exposure.
    /// </summary>
    public ExposureStatus Status { get; set; }

    /// <summary>
    /// Set when Status = Rejected.
    /// SPEC-WORKFLOW-001 FR-WF-06-a: Structured reject reason
    /// </summary>
    public RejectReason? RejectReason { get; set; }

    /// <summary>
    /// DICOM SOP Instance UID, set after acquisition.
    /// </summary>
    public string? ImageInstanceUID { get; set; }

    /// <summary>
    /// DAP in cGycm, set after exposure.
    /// </summary>
    public decimal? AdministeredDap { get; set; }

    /// <summary>
    /// Timestamp of successful acquisition.
    /// </summary>
    public DateTime? AcquiredAt { get; set; }

    /// <summary>
    /// Operator who performed the exposure.
    /// </summary>
    public string OperatorId { get; init; } = string.Empty;

    /// <summary>
    /// Operator who rejected the exposure (if rejected).
    /// </summary>
    public string? RejectedBy { get; set; }

    /// <summary>
    /// Timestamp when exposure was rejected.
    /// </summary>
    public DateTime? RejectedAt { get; set; }
}

/// <summary>
/// Exposure status enum.
/// SPEC-WORKFLOW-001 FR-WF-05-d: Tracking status for each exposure index
/// </summary>
public enum ExposureStatus
{
    /// <summary>
    /// Exposure planned but not yet acquired.
    /// </summary>
    Pending,

    /// <summary>
    /// Image acquired but not yet reviewed.
    /// </summary>
    Acquired,

    /// <summary>
    /// Image accepted by operator.
    /// </summary>
    Accepted,

    /// <summary>
    /// Image rejected by operator.
    /// </summary>
    Rejected,

    /// <summary>
    /// Exposure could not be completed (e.g., retake cancelled).
    /// </summary>
    Incomplete
}

/// <summary>
/// Structured reject reason.
/// SPEC-WORKFLOW-001 FR-WF-06-a: Motion, Positioning, ExposureError, EquipmentArtifact, Other
/// </summary>
public enum RejectReason
{
    /// <summary>
    /// Patient motion during exposure.
    /// </summary>
    Motion,

    /// <summary>
    /// Incorrect patient positioning.
    /// </summary>
    Positioning,

    /// <summary>
    /// Exposure parameters incorrect.
    /// </summary>
    ExposureError,

    /// <summary>
    /// Equipment artifact in image.
    /// </summary>
    EquipmentArtifact,

    /// <summary>
    /// Other reason (requires comment).
    /// </summary>
    Other
}
