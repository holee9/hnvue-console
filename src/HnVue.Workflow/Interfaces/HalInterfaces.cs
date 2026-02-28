namespace HnVue.Workflow.Interfaces;

using System.Threading;
using System.Threading.Tasks;

#region High-Voltage Generator

/// <summary>
/// Defines the contract for controlling the High-Voltage Generator (HVG).
/// </summary>
/// <remarks>
/// @MX:ANCHOR: HVG control interface - X-ray generation control
/// @MX:WARN: Safety-critical - controls high-voltage X-ray emission
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-06
///
/// This interface provides methods for triggering X-ray exposure with
/// specified parameters (kV, mA, ms) and monitoring exposure status.
/// </remarks>
public interface IHvgDriver
{
    /// <summary>
    /// Triggers an X-ray exposure with the specified parameters.
    /// </summary>
    /// <param name="parameters">The exposure parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if exposure was triggered successfully; false if aborted by safety interlock.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Exposure trigger - final X-ray emission control
    /// @MX:WARN: Safety-critical - triggers ionizing radiation
    /// </remarks>
    Task<bool> TriggerExposureAsync(ExposureParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts the current exposure.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Exposure abort - emergency termination of X-ray emission
    /// @MX:WARN: Safety-critical - immediately stops radiation
    /// </remarks>
    Task AbortExposureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the high-voltage generator.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The current generator status.</returns>
    Task<HvgStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents X-ray exposure parameters.
/// </summary>
/// <remarks>
/// @MX:NOTE: Exposure parameters - kV, mA, exposure time
/// </remarks>
public readonly record struct ExposureParameters
{
    /// <summary>Tube peak voltage in kilovolts (kV).</summary>
    public required int Kv { get; init; }

    /// <summary>Tube current in milliamperes (mA).</summary>
    public required int Ma { get; init; }

    /// <summary>Exposure time in milliseconds (ms).</summary>
    public required int Ms { get; init; }
}

/// <summary>
/// Represents the current status of the high-voltage generator.
/// </summary>
/// <remarks>
/// @MX:NOTE: HVG status structure - generator state information
/// </remarks>
public readonly record struct HvgStatus
{
    /// <summary>The current state of the generator.</summary>
    public required HvgState State { get; init; }

    /// <summary>True if the generator is ready for exposure.</summary>
    public required bool IsReady { get; init; }

    /// <summary>Any active fault code, or null if no fault.</summary>
    public string? FaultCode { get; init; }
}

/// <summary>
/// Represents the possible states of the high-voltage generator.
/// </summary>
/// <remarks>
/// @MX:NOTE: HVG state enumeration - generator operational states
/// </remarks>
public enum HvgState
{
    /// <summary>Generator is initializing.</summary>
    Initializing,

    /// <summary>Generator is ready for exposure.</summary>
    Ready,

    /// <summary>Exposure is in progress.</summary>
    Exposing,

    /// <summary>Generator has a fault condition.</summary>
    Fault,

    /// <summary>Generator is in standby mode.</summary>
    Standby
}

#endregion

#region Detector

/// <summary>
/// Defines the contract for controlling the X-ray detector.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Detector interface - image acquisition control
/// @MX:SPEC: SPEC-WORKFLOW-001 FR-WORKFLOW-05
///
/// This interface provides methods for managing detector acquisition,
/// retrieving detector information, and monitoring detector status.
/// </remarks>
public interface IDetector
{
    /// <summary>
    /// Starts a new image acquisition.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAcquisitionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the current acquisition.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAcquisitionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the detector.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The current detector status.</returns>
    Task<DetectorStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets static detector information.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Detector information structure.</returns>
    Task<DetectorInfo> GetInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the current status of the detector.
/// </summary>
/// <remarks>
/// @MX:NOTE: Detector status structure - detector state information
/// </remarks>
public readonly record struct DetectorStatus
{
    /// <summary>The current state of the detector.</summary>
    public required DetectorState State { get; init; }

    /// <summary>True if the detector is ready for acquisition.</summary>
    public required bool IsReady { get; init; }

    /// <summary>Any active error message, or null if no error.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents the possible states of the detector.
/// </summary>
/// <remarks>
/// @MX:NOTE: Detector state enumeration - detector operational states
/// </remarks>
public enum DetectorState
{
    /// <summary>Detector is initializing.</summary>
    Initializing,

    /// <summary>Detector is ready for acquisition.</summary>
    Ready,

    /// <summary>Acquisition is in progress.</summary>
    Acquiring,

    /// <summary>Detector has an error condition.</summary>
    Error,

    /// <summary>Detector is in standby mode.</summary>
    Standby
}

/// <summary>
/// Represents static detector information.
/// </summary>
/// <remarks>
/// @MX:NOTE: Detector info structure - detector metadata
/// </remarks>
public readonly record struct DetectorInfo
{
    /// <summary>Detector manufacturer name.</summary>
    public required string Manufacturer { get; init; }

    /// <summary>Detector model name.</summary>
    public required string Model { get; init; }

    /// <summary>Detector serial number.</summary>
    public required string SerialNumber { get; init; }

    /// <summary>Pixel width in micrometers.</summary>
    public required int PixelWidth { get; init; }

    /// <summary>Pixel height in micrometers.</summary>
    public required int PixelHeight { get; init; }

    /// <summary>Number of columns in the detector array.</summary>
    public required int Columns { get; init; }

    /// <summary>Number of rows in the detector array.</summary>
    public required int Rows { get; init; }
}

#endregion

#region Dose Tracking

/// <summary>
/// Defines the contract for tracking radiation dose accumulation.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Dose tracker interface - radiation dose monitoring
/// @MX:SPEC: SPEC-WORKFLOW-001 NFR-SAFETY-02
///
/// This interface provides methods for tracking cumulative radiation dose
/// for the current study and enforcing dose limit compliance.
/// </remarks>
public interface IDoseTracker
{
    /// <summary>
    /// Records a dose entry from an exposure.
    /// </summary>
    /// <param name="doseEntry">The dose entry to record.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Dose recording - tracks radiation exposure
    /// @MX:WARN: Safety-critical - dose accumulation monitoring
    /// </remarks>
    Task RecordDoseAsync(DoseEntry doseEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cumulative dose for the current study.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The cumulative dose information.</returns>
    Task<CumulativeDose> GetCumulativeDoseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the proposed exposure would exceed dose limits.
    /// </summary>
    /// <param name="proposedDose">The proposed dose entry.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the proposed dose is within limits; false if it would exceed limits.</returns>
    /// <remarks>
    /// @MX:ANCHOR: Dose limit check - prevents excessive radiation exposure
    /// @MX:WARN: Safety-critical - dose limit enforcement
    /// </remarks>
    Task<bool> IsWithinDoseLimitsAsync(DoseEntry proposedDose, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single dose entry from an exposure.
/// </summary>
/// <remarks>
/// @MX:NOTE: Dose entry structure - single exposure dose information
/// </remarks>
public readonly record struct DoseEntry
{
    /// <summary>The study identifier.</summary>
    public required string StudyId { get; init; }

    /// <summary>The patient identifier.</summary>
    public required string PatientId { get; init; }

    /// <summary>Dose-Area Product in µGy·m².</summary>
    public required double Dap { get; init; }

    /// <summary>Entrance Skin Dose in mGy.</summary>
    public required double Esd { get; init; }

    /// <summary>Exposure timestamp (UTC).</summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Represents cumulative dose information for a study.
/// </summary>
/// <remarks>
/// @MX:NOTE: Cumulative dose structure - total dose accumulation
/// </remarks>
public readonly record struct CumulativeDose
{
    /// <summary>The study identifier.</summary>
    public required string StudyId { get; init; }

    /// <summary>Total Dose-Area Product in µGy·m².</summary>
    public required double TotalDap { get; init; }

    /// <summary>Total number of exposures in the study.</summary>
    public required int ExposureCount { get; init; }

    /// <summary>True if cumulative dose is within configured limits.</summary>
    public required bool IsWithinLimits { get; init; }

    /// <summary>Configured dose limit in µGy·m², or null if no limit is set.</summary>
    public double? DoseLimit { get; init; }
}

#endregion
