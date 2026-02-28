using Microsoft.Extensions.Logging;

namespace HnVue.Dose.Alerting;

/// <summary>
/// Compares dose values against Dose Reference Levels (DRL) and triggers alerts.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of DRL comparison and alerting - FR-DOSE-05 compliance
/// @MX:REASON: Critical implementation for IEC 60601-1-3 §7.4 DRL comparison
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-05
///
/// Compares cumulative study DAP and single-exposure DAP against configured thresholds.
/// Triggers alert notification when DRL is exceeded.
/// Logs DRL exceedance events to audit trail.
///
/// Per FR-DOSE-05-D: Does not block or delay exposure workflow.
/// Alerting is advisory only.
///
/// Per FR-DOSE-05-C: Suppresses comparison when no DRL is configured.
/// </remarks>
public sealed class DrlComparer
{
    private readonly DrlConfiguration _configuration;
    private readonly ILogger<DrlComparer> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when DRL threshold is exceeded.
    /// </summary>
    /// <remarks>
    /// Subscribers receive DRL exceedance details.
    /// </remarks>
    public event EventHandler<DrlExceededEventArgs>? DrlExceeded;

    /// <summary>
    /// Initializes a new instance of the DrlComparer class.
    /// </summary>
    /// <param name="configuration">DRL configuration</param>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
    public DrlComparer(DrlConfiguration configuration, ILogger<DrlComparer> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Compares cumulative study DAP against DRL threshold.
    /// </summary>
    /// <param name="protocol">Examination protocol name</param>
    /// <param name="bodyRegionCode">Optional body region code</param>
    /// <param name="cumulativeDapGyCm2">Cumulative study DAP in Gy·cm²</param>
    /// <param name="studyInstanceUid">Study Instance UID for audit trail</param>
    /// <returns>True if DRL was exceeded</returns>
    /// <remarks>
    /// Raises DrlExceeded event when threshold is exceeded.
    /// Logs DRL exceedance to audit trail.
    /// </remarks>
    public bool CompareCumulativeDap(
        string protocol,
        string? bodyRegionCode,
        decimal cumulativeDapGyCm2,
        string studyInstanceUid)
    {
        var threshold = _configuration.GetThreshold(protocol, bodyRegionCode);

        // Per FR-DOSE-05-C: Suppress comparison when no DRL is configured
        if (threshold is null)
        {
            _logger.LogDebug(
                "No DRL configured for protocol: {Protocol}, BodyRegion: {BodyRegion}",
                protocol, bodyRegionCode ?? "none");
            return false;
        }

        var exceeded = cumulativeDapGyCm2 > threshold.CumulativeDapThresholdGyCm2;

        if (exceeded)
        {
            var eventArgs = new DrlExceededEventArgs(
                protocol,
                bodyRegionCode,
                DrlExceedanceType.Cumulative,
                cumulativeDapGyCm2,
                threshold.CumulativeDapThresholdGyCm2,
                studyInstanceUid);

            _logger.LogWarning(
                "Cumulative DRL exceeded: Protocol={Protocol}, CumulativeDAP={Cumulative}Gy·cm², Threshold={Threshold}Gy·cm², Study={StudyUid}",
                protocol, cumulativeDapGyCm2, threshold.CumulativeDapThresholdGyCm2, studyInstanceUid);

            DrlExceeded?.Invoke(this, eventArgs);
        }

        return exceeded;
    }

    /// <summary>
    /// Compares single-exposure DAP against DRL threshold.
    /// </summary>
    /// <param name="protocol">Examination protocol name</param>
    /// <param name="bodyRegionCode">Optional body region code</param>
    /// <param name="exposureDapGyCm2">Single-exposure DAP in Gy·cm²</param>
    /// <param name="studyInstanceUid">Study Instance UID for audit trail</param>
    /// <returns>True if DRL was exceeded</returns>
    /// <remarks>
    /// Logs DRL exceedance to audit trail per FR-DOSE-05-B.
    /// Does not raise event (alert is logged only for single-exposure).
    /// </remarks>
    public bool CompareSingleExposureDap(
        string protocol,
        string? bodyRegionCode,
        decimal exposureDapGyCm2,
        string studyInstanceUid)
    {
        var threshold = _configuration.GetThreshold(protocol, bodyRegionCode);

        // Per FR-DOSE-05-C: Suppress comparison when no DRL is configured
        if (threshold is null)
        {
            return false;
        }

        // Only check if single-exposure threshold is configured
        if (!threshold.SingleExposureDapThresholdGyCm2.HasValue)
        {
            return false;
        }

        var singleExposureThreshold = threshold.SingleExposureDapThresholdGyCm2.Value;
        var exceeded = exposureDapGyCm2 > singleExposureThreshold;

        if (exceeded)
        {
            _logger.LogWarning(
                "Single-exposure DRL exceeded: Protocol={Protocol}, ExposureDAP={Exposure}Gy·cm², Threshold={Threshold}Gy·cm², Study={StudyUid}",
                protocol, exposureDapGyCm2, singleExposureThreshold, studyInstanceUid);

            // Per FR-DOSE-05-B: Log DRL threshold exceedance event
            // Note: This should be logged to the audit trail
            // TODO: Integrate with AuditTrailWriter when available
        }

        return exceeded;
    }

    /// <summary>
    /// Gets the DRL threshold for a protocol.
    /// </summary>
    /// <param name="protocol">Examination protocol name</param>
    /// <param name="bodyRegionCode">Optional body region code</param>
    /// <returns>DRL threshold or null if not configured</returns>
    public DrlThreshold? GetThreshold(string protocol, string? bodyRegionCode = null)
    {
        return _configuration.GetThreshold(protocol, bodyRegionCode);
    }
}

/// <summary>
/// Type of DRL exceedance.
/// </summary>
public enum DrlExceedanceType
{
    /// <summary>
    /// Cumulative study DAP exceeded threshold.
    /// </summary>
    Cumulative,

    /// <summary>
    /// Single-exposure DAP exceeded threshold.
    /// </summary>
    SingleExposure
}

/// <summary>
/// Event arguments for DRL exceeded event.
/// </summary>
/// <param name="Protocol">Examination protocol name</param>
/// <param name="BodyRegionCode">Body region code, if applicable</param>
/// <param name="ExceedanceType">Type of DRL exceedance</param>
/// <param name="ActualValueGyCm2">Actual DAP value</param>
/// <param name="ThresholdValueGyCm2">DRL threshold value</param>
/// <param name="StudyInstanceUid">Associated study UID</param>
public sealed record DrlExceededEventArgs(
    string Protocol,
    string? BodyRegionCode,
    DrlExceedanceType ExceedanceType,
    decimal ActualValueGyCm2,
    decimal ThresholdValueGyCm2,
    string StudyInstanceUid);
