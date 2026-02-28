using HnVue.Dose.Calculation;
using Microsoft.Extensions.Logging;

namespace HnVue.Dose.Acquisition;

/// <summary>
/// Receives exposure parameters from the HVG subsystem.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of HVG parameter acquisition - SPEC-DOSE-001 FR-DOSE-01
/// @MX:REASON: Critical implementation for exposure data collection
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-01, FR-DOSE-06
///
/// Acquires kVp, mAs, filtration, and detector geometry synchronously
/// within 200ms of exposure completion per SPEC-DOSE-001 NFR-DOSE-01.
///
/// Thread-safe: Uses lock for concurrent access protection.
/// </remarks>
public sealed class ExposureParameterReceiver
{
    private readonly ILogger<ExposureParameterReceiver> _logger;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, PendingExposure> _pendingExposures = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ExposureParameterReceiver class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
    public ExposureParameterReceiver(ILogger<ExposureParameterReceiver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("ExposureParameterReceiver initialized");
    }

    /// <summary>
    /// Event raised when exposure parameters are received.
    /// </summary>
    /// <remarks>
    /// Subscribers receive ExposureParameters for dose calculation.
    /// </remarks>
    public event EventHandler<ExposureParametersReceivedEventArgs>? ParametersReceived;

    /// <summary>
    /// Starts receiving parameters for a new exposure event.
    /// </summary>
    /// <param name="exposureId">Unique exposure event identifier</param>
    /// <param name="studyInstanceUid">Associated study UID</param>
    /// <param name="patientId">Associated patient ID</param>
    /// <param name="acquisitionProtocol">Optional examination protocol name</param>
    /// <param name="bodyRegionCode">Optional target body region code</param>
    /// <returns>Context for parameter collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when required IDs are null</exception>
    /// <remarks>
    /// Called by host application when exposure begins.
    /// Creates a pending exposure context for parameter accumulation.
    /// </remarks>
    public ExposureContext StartExposure(
        Guid exposureId,
        string studyInstanceUid,
        string patientId,
        string? acquisitionProtocol = null,
        string? bodyRegionCode = null)
    {
        VerifyNotDisposed();

        if (exposureId == Guid.Empty)
        {
            throw new ArgumentException("Exposure ID cannot be empty.", nameof(exposureId));
        }

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
            var context = new ExposureContext(exposureId, studyInstanceUid, patientId);
            var pending = new PendingExposure
            {
                Context = context,
                AcquisitionProtocol = acquisitionProtocol,
                BodyRegionCode = bodyRegionCode,
                StartedAtUtc = DateTime.UtcNow
            };

            _pendingExposures[exposureId] = pending;

            _logger.LogDebug(
                "Exposure started: ExposureId={ExposureId}, Study={StudyUid}, Patient={PatientId}",
                exposureId, studyInstanceUid, patientId);

            return context;
        }
    }

    /// <summary>
    /// Receives HVG parameters (kVp, mAs, filtration).
    /// </summary>
    /// <param name="exposureId">Exposure event identifier</param>
    /// <param name="kvpValue">Peak kilovoltage</param>
    /// <param name="masValue">Tube current-exposure time product</param>
    /// <param name="filterMaterial">Beam filtration material code</param>
    /// <param name="filterThicknessMm">Filtration thickness in mm</param>
    /// <exception cref="InvalidOperationException">Thrown when exposure is not found</exception>
    /// <remarks>
    /// Called by HVG subsystem when exposure parameters are available.
    /// Must be received within 200ms of exposure completion per NFR-DOSE-01.
    /// </remarks>
    public void ReceiveHvgParameters(
        Guid exposureId,
        decimal kvpValue,
        decimal masValue,
        string filterMaterial,
        decimal filterThicknessMm)
    {
        VerifyNotDisposed();

        lock (_lock)
        {
            if (!_pendingExposures.TryGetValue(exposureId, out var pending))
            {
                _logger.LogWarning(
                    "HVG parameters received for unknown exposure: ExposureId={ExposureId}",
                    exposureId);
                return;
            }

            pending.KvpValue = kvpValue;
            pending.MasValue = masValue;
            pending.FilterMaterial = filterMaterial;
            pending.FilterThicknessMm = filterThicknessMm;
            pending.HvgReceivedAtUtc = DateTime.UtcNow;

            _logger.LogDebug(
                "HVG parameters received: ExposureId={ExposureId}, kVp={Kvp}, mAs={Mas}, Filter={Filter}",
                exposureId, kvpValue, masValue, filterMaterial);

            TryCompleteExposure(exposureId, pending);
        }
    }

    /// <summary>
    /// Receives detector geometry (field area, SID).
    /// </summary>
    /// <param name="exposureId">Exposure event identifier</param>
    /// <param name="fieldWidthMm">Collimated field width at detector in mm</param>
    /// <param name="fieldHeightMm">Collimated field height at detector in mm</param>
    /// <param name="sidMm">Source-to-Image Distance in mm</param>
    /// <exception cref="InvalidOperationException">Thrown when exposure is not found</exception>
    /// <remarks>
    /// Called by detector subsystem when geometry data is available.
    /// </remarks>
    public void ReceiveDetectorGeometry(
        Guid exposureId,
        decimal fieldWidthMm,
        decimal fieldHeightMm,
        decimal sidMm)
    {
        VerifyNotDisposed();

        lock (_lock)
        {
            if (!_pendingExposures.TryGetValue(exposureId, out var pending))
            {
                _logger.LogWarning(
                    "Detector geometry received for unknown exposure: ExposureId={ExposureId}",
                    exposureId);
                return;
            }

            pending.FieldWidthMm = fieldWidthMm;
            pending.FieldHeightMm = fieldHeightMm;
            pending.SidMm = sidMm;
            pending.GeometryReceivedAtUtc = DateTime.UtcNow;

            _logger.LogDebug(
                "Detector geometry received: ExposureId={ExposureId}, Field={Field}x{Height}mm, SID={Sid}mm",
                exposureId, fieldWidthMm, fieldHeightMm, sidMm);

            TryCompleteExposure(exposureId, pending);
        }
    }

    /// <summary>
    /// Attempts to complete exposure when all parameters are received.
    /// </summary>
    private void TryCompleteExposure(Guid exposureId, PendingExposure pending)
    {
        // Check if all required parameters are received
        if (!HasAllParameters(pending))
        {
            return;
        }

        // Remove from pending
        _pendingExposures.Remove(exposureId);

        // Build exposure parameters
        var parameters = new ExposureParameters
        {
            KvpValue = pending.KvpValue!.Value,
            MasValue = pending.MasValue!.Value,
            FilterMaterial = pending.FilterMaterial!,
            FilterThicknessMm = pending.FilterThicknessMm!.Value,
            SidMm = pending.SidMm!.Value,
            FieldWidthMm = pending.FieldWidthMm!.Value,
            FieldHeightMm = pending.FieldHeightMm!.Value,
            TimestampUtc = DateTime.UtcNow,
            AcquisitionProtocol = pending.AcquisitionProtocol,
            BodyRegionCode = pending.BodyRegionCode
        };

        // Validate parameters
        if (!parameters.IsValid())
        {
            _logger.LogWarning(
                "Invalid exposure parameters received: ExposureId={ExposureId}",
                exposureId);
            return;
        }

        // Raise event
        ParametersReceived?.Invoke(this, new ExposureParametersReceivedEventArgs(
            pending.Context,
            parameters));

        _logger.LogInformation(
            "Exposure parameters complete: ExposureId={ExposureId}, kVp={Kvp}, mAs={Mas}",
            exposureId, parameters.KvpValue, parameters.MasValue);
    }

    /// <summary>
    /// Checks if all required parameters have been received.
    /// </summary>
    private static bool HasAllParameters(PendingExposure pending)
    {
        return pending.KvpValue.HasValue
            && pending.MasValue.HasValue
            && !string.IsNullOrWhiteSpace(pending.FilterMaterial)
            && pending.FilterThicknessMm.HasValue
            && pending.SidMm.HasValue
            && pending.FieldWidthMm.HasValue
            && pending.FieldHeightMm.HasValue;
    }

    /// <summary>
    /// Cancels a pending exposure.
    /// </summary>
    /// <param name="exposureId">Exposure event identifier</param>
    /// <returns>True if exposure was cancelled</returns>
    public bool CancelExposure(Guid exposureId)
    {
        VerifyNotDisposed();

        lock (_lock)
        {
            if (_pendingExposures.Remove(exposureId))
            {
                _logger.LogDebug("Exposure cancelled: ExposureId={ExposureId}", exposureId);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets the count of pending exposures.
    /// </summary>
    public int PendingExposureCount
    {
        get
        {
            lock (_lock)
            {
                return _pendingExposures.Count;
            }
        }
    }

    /// <summary>
    /// Verifies the receiver has not been disposed.
    /// </summary>
    private void VerifyNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExposureParameterReceiver));
        }
    }

    /// <summary>
    /// Disposes the receiver.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogDebug("ExposureParameterReceiver disposing");

            _pendingExposures.Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// Internal state for a pending exposure.
    /// </summary>
    private sealed class PendingExposure
    {
        public required ExposureContext Context { get; init; }
        public decimal? KvpValue { get; set; }
        public decimal? MasValue { get; set; }
        public string? FilterMaterial { get; set; }
        public decimal? FilterThicknessMm { get; set; }
        public decimal? SidMm { get; set; }
        public decimal? FieldWidthMm { get; set; }
        public decimal? FieldHeightMm { get; set; }
        public string? AcquisitionProtocol { get; set; }
        public string? BodyRegionCode { get; set; }
        public DateTime StartedAtUtc { get; init; }
        public DateTime? HvgReceivedAtUtc { get; set; }
        public DateTime? GeometryReceivedAtUtc { get; set; }
    }
}

/// <summary>
/// Context for an exposure event being collected.
/// </summary>
/// <param name="ExposureId">Unique exposure event identifier</param>
/// <param name="StudyInstanceUid">Associated study UID</param>
/// <param name="PatientId">Associated patient ID</param>
public sealed record ExposureContext(
    Guid ExposureId,
    string StudyInstanceUid,
    string PatientId);

/// <summary>
/// Event arguments for exposure parameters received.
/// </summary>
/// <param name="Context">Exposure context</param>
/// <param name="Parameters">Received exposure parameters</param>
public sealed record ExposureParametersReceivedEventArgs(
    ExposureContext Context,
    ExposureParameters Parameters);
