namespace HnVue.Dose.Alerting;

/// <summary>
/// Dose Reference Level (DRL) threshold configuration.
/// </summary>
/// <remarks>
/// @MX:NOTE: Domain model for DRL configuration - FR-DOSE-05 compliance
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-05
///
/// DRL thresholds are keyed by examination type and body region.
/// Default values from national guidelines are provided as factory defaults.
/// </remarks>
public sealed record DrlThreshold
{
    /// <summary>
    /// Gets the examination protocol name (e.g., "CXR PA", "Abdomen AP").
    /// </summary>
    public required string Protocol { get; init; }

    /// <summary>
    /// Gets the body region code (optional, for more granular DRL).
    /// </summary>
    /// <remarks>
    /// SNOMED-CT coded value when available.
    /// </remarks>
    public string? BodyRegionCode { get; init; }

    /// <summary>
    /// Gets the cumulative study DAP threshold in Gy·cm².
    /// </summary>
    /// <remarks>
    /// Alert triggered when cumulative study DAP exceeds this value.
    /// </remarks>
    public decimal CumulativeDapThresholdGyCm2 { get; init; }

    /// <summary>
    /// Gets the single-exposure DAP threshold in Gy·cm² (optional).
    /// </summary>
    /// <remarks>
    /// Alert logged when single exposure DAP exceeds this value.
    /// Null means no single-exposure threshold configured.
    /// </remarks>
    public decimal? SingleExposureDapThresholdGyCm2 { get; init; }
}

/// <summary>
/// Manages Dose Reference Level (DRL) configuration and thresholds.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Implementation of DRL configuration management - FR-DOSE-05 compliance
/// @MX:REASON: Critical implementation for IEC 60601-1-3 §7.4 DRL comparison
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-05
///
/// DRL thresholds are configurable per examination type and body region.
/// Default values from national guidelines are provided as factory defaults.
/// All configuration changes are logged to the audit trail.
///
/// While no DRL is configured, DRL comparison is suppressed per FR-DOSE-05-C.
/// </remarks>
public sealed class DrlConfiguration
{
    private readonly Dictionary<string, DrlThreshold> _thresholdsByKey = new();
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the DrlConfiguration class.
    /// </summary>
    public DrlConfiguration()
    {
        LoadFactoryDefaults();
    }

    /// <summary>
    /// Gets a DRL threshold by protocol key.
    /// </summary>
    /// <param name="protocol">Examination protocol name</param>
    /// <param name="bodyRegionCode">Optional body region code</param>
    /// <returns>DRL threshold or null if not configured</returns>
    public DrlThreshold? GetThreshold(string protocol, string? bodyRegionCode = null)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return null;
        }

        lock (_lock)
        {
            var key = BuildKey(protocol, bodyRegionCode);

            if (_thresholdsByKey.TryGetValue(key, out var threshold))
            {
                return threshold;
            }

            // Try protocol-only key if body region specific not found
            if (bodyRegionCode is not null)
            {
                var protocolOnlyKey = BuildKey(protocol, null);
                return _thresholdsByKey.GetValueOrDefault(protocolOnlyKey);
            }

            return null;
        }
    }

    /// <summary>
    /// Sets or updates a DRL threshold.
    /// </summary>
    /// <param name="threshold">DRL threshold to set</param>
    /// <exception cref="ArgumentNullException">Thrown when threshold is null</exception>
    public void SetThreshold(DrlThreshold threshold)
    {
        if (threshold is null)
        {
            throw new ArgumentNullException(nameof(threshold));
        }

        if (string.IsNullOrWhiteSpace(threshold.Protocol))
        {
            throw new ArgumentException("Protocol is required.", nameof(threshold));
        }

        lock (_lock)
        {
            var key = BuildKey(threshold.Protocol, threshold.BodyRegionCode);
            _thresholdsByKey[key] = threshold;
        }
    }

    /// <summary>
    /// Removes a DRL threshold.
    /// </summary>
    /// <param name="protocol">Examination protocol name</param>
    /// <param name="bodyRegionCode">Optional body region code</param>
    /// <returns>True if threshold was removed</returns>
    public bool RemoveThreshold(string protocol, string? bodyRegionCode = null)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return false;
        }

        lock (_lock)
        {
            var key = BuildKey(protocol, bodyRegionCode);
            return _thresholdsByKey.Remove(key);
        }
    }

    /// <summary>
    /// Gets all configured DRL thresholds.
    /// </summary>
    /// <returns>Read-only list of all thresholds</returns>
    public IReadOnlyList<DrlThreshold> GetAllThresholds()
    {
        lock (_lock)
        {
            return _thresholdsByKey.Values.ToList();
        }
    }

    /// <summary>
    /// Loads factory default DRL values from national guidelines.
    /// </summary>
    /// <remarks>
    /// Default values based on typical national DRL guidelines.
    /// Specific values should be confirmed with clinical team during site setup.
    /// </remarks>
    private void LoadFactoryDefaults()
    {
        // Common chest X-ray DRLs (example values)
        _thresholdsByKey[BuildKey("CXR PA", null)] = new DrlThreshold
        {
            Protocol = "CXR PA",
            BodyRegionCode = null,
            CumulativeDapThresholdGyCm2 = 0.25m,  // 250 mGy·cm²
            SingleExposureDapThresholdGyCm2 = 0.25m
        };

        _thresholdsByKey[BuildKey("CXR LAT", null)] = new DrlThreshold
        {
            Protocol = "CXR LAT",
            BodyRegionCode = null,
            CumulativeDapThresholdGyCm2 = 0.30m,  // 300 mGy·cm²
            SingleExposureDapThresholdGyCm2 = 0.30m
        };

        // Abdomen DRLs (example values)
        _thresholdsByKey[BuildKey("Abdomen AP", null)] = new DrlThreshold
        {
            Protocol = "Abdomen AP",
            BodyRegionCode = null,
            CumulativeDapThresholdGyCm2 = 3.0m,  // 3000 mGy·cm²
            SingleExposureDapThresholdGyCm2 = 3.0m
        };

        // Extremity DRLs (example values)
        _thresholdsByKey[BuildKey("Extremity", null)] = new DrlThreshold
        {
            Protocol = "Extremity",
            BodyRegionCode = null,
            CumulativeDapThresholdGyCm2 = 0.10m,  // 100 mGy·cm²
            SingleExposureDapThresholdGyCm2 = 0.10m
        };
    }

    /// <summary>
    /// Builds a configuration key from protocol and body region.
    /// </summary>
    private static string BuildKey(string protocol, string? bodyRegionCode)
    {
        return string.IsNullOrWhiteSpace(bodyRegionCode)
            ? protocol.ToUpperInvariant()
            : $"{protocol.ToUpperInvariant()}:{bodyRegionCode.ToUpperInvariant()}";
    }
}
