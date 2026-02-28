namespace HnVue.Dose.Display;

/// <summary>
/// Dose display update for GUI notification.
/// </summary>
/// <remarks>
/// @MX:NOTE: Domain model for dose display update - GUI notification payload
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-04
///
/// Published by IDoseDisplayNotifier to subscribed GUI components.
/// Contains current exposure DAP and cumulative study DAP.
/// </remarks>
public sealed record DoseDisplayUpdate
{
    /// <summary>
    /// Gets the unique identifier for the exposure event.
    /// </summary>
    /// <remarks>
    /// GUID from DoseRecord.ExposureEventId.
    /// Used for GUI correlation with dose records.
    /// </remarks>
    public required Guid ExposureEventId { get; init; }

    /// <summary>
    /// Gets the DAP value for the current exposure in Gy·cm².
    /// </summary>
    /// <remarks>
    /// Displayed as "Exposure DAP" in the dose panel.
    /// Converted to configured display units (Gy·cm² or mGy·cm²).
    /// </remarks>
    public required decimal ExposureDapGyCm2 { get; init; }

    /// <summary>
    /// Gets the cumulative DAP total for the current study in Gy·cm².
    /// </summary>
    /// <remarks>
    /// Displayed as "Study Total DAP" in the dose panel.
    /// Updated after each exposure event.
    /// </remarks>
    public required decimal StudyCumulativeDapGyCm2 { get; init; }

    /// <summary>
    /// Gets the total number of exposure events in the current study.
    /// </summary>
    /// <remarks>
    /// Displayed as "Exposure Count" in the dose panel.
    /// Incremented after each exposure event.
    /// </remarks>
    public required int StudyExposureCount { get; init; }

    /// <summary>
    /// Gets the Study Instance UID for the current study.
    /// </summary>
    /// <remarks>
    /// Used for GUI to associate dose display with patient context.
    /// Null when no active study is open.
    /// </remarks>
    public string? StudyInstanceUid { get; init; }

    /// <summary>
    /// Gets the Patient ID for the current study.
    /// </summary>
    /// <remarks>
    /// Used for GUI to display patient context in dose panel.
    /// Null when no active study is open.
    /// </remarks>
    public string? PatientId { get; init; }

    /// <summary>
    /// Gets the timestamp of the exposure event in UTC.
    /// </summary>
    /// <remarks>
    /// Displayed as "Last Exposure" time in the dose panel.
    /// </remarks>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>
    /// Gets whether the DRL threshold was exceeded for this exposure.
    /// </summary>
    /// <remarks>
    /// When true, GUI should highlight/alert the dose display.
    /// Indicates single-exposure or cumulative DRL exceedance.
    /// </remarks>
    public required bool DrlExceeded { get; init; }

    /// <summary>
    /// Gets the configured DRL threshold for the examination type, if available.
    /// </summary>
    /// <remarks>
    /// Displayed as "DRL Reference" in the dose panel.
    /// Null when no DRL is configured for the current protocol.
    /// </remarks>
    public decimal? DrlThresholdGyCm2 { get; init; }

    /// <summary>
    /// Gets the dose source indicator.
    /// </summary>
    /// <remarks>
    /// Indicates whether displayed DAP is calculated or measured.
    /// Displayed as "Source: Calculated" or "Source: Measured".
    /// </remarks>
    public required HnVue.Dicom.Rdsr.DoseSource DoseSource { get; init; }

    /// <summary>
    /// Gets whether an active study session is open.
    /// </summary>
    /// <remarks>
    /// When false, GUI should display cleared dose panel.
    /// See SPEC-DOSE-001 FR-DOSE-04-D.
    /// </remarks>
    public required bool HasActiveStudy { get; init; }
}
