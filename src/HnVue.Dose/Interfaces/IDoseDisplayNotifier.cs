using HnVue.Dose.Display;

namespace HnVue.Dose.Interfaces;

/// <summary>
/// Publishes dose display updates to GUI layer.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Public API for dose display notifications - real-time GUI updates
/// @MX:REASON: Critical interface for IEC 60601-2-54 dose display requirements
/// @MX:SPEC: SPEC-DOSE-001 FR-DOSE-04
///
/// GUI layer subscribes to this IObservable to receive real-time dose updates.
/// Each emission contains current exposure DAP and cumulative study DAP.
///
/// Display requirements per IEC 60601-2-54:
/// - Update within 1 second of exposure completion
/// - SI units (Gy·cm² or mGy·cm²)
/// - At least 2 decimal places precision
/// </remarks>
public interface IDoseDisplayNotifier
{
    /// <summary>
    /// Observable stream of dose display updates.
    /// </summary>
    /// <remarks>
    /// GUI layer subscribes to this IObservable to receive real-time updates.
    /// Each emission contains current exposure DAP and cumulative study DAP.
    /// </remarks>
    IObservable<DoseDisplayUpdate> DoseUpdates { get; }

    /// <summary>
    /// Publishes a dose display update to all subscribers.
    /// </summary>
    /// <param name="update">Dose display update containing current and cumulative DAP values</param>
    /// <exception cref="ArgumentNullException">Thrown when update is null</exception>
    void Publish(DoseDisplayUpdate update);
}
