namespace HnVue.Workflow.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using HnVue.Workflow.Safety;

/// <summary>
/// Simulator for the Safety Interlock hardware.
/// </summary>
/// <remarks>
/// @MX:NOTE: Safety interlock simulator - test double for ISafetyInterlock
/// @MX:SPEC: SPEC-WORKFLOW-001 TASK-403
///
/// This simulator provides realistic safety interlock behavior for testing:
/// - All 9 interlocks (door, handswitch, collimation, temperature, cooling, grid, generator, detector, emergency)
/// - Individual enable/disable per interlock
/// - Safety-critical: exposure blocked when any interlock is active
/// - Atomic interlock checking within 10ms (SPEC requirement)
/// </remarks>
public sealed class SafetyInterlockSimulator : ISafetyInterlock
{
    private readonly object _lock = new();
    private InterlockStatus _interlockStatus = new();

    /// <summary>
    /// Initializes a new instance of the SafetyInterlockSimulator class.
    /// </summary>
    public SafetyInterlockSimulator()
    {
        // Initialize all interlocks to safe state (all true)
        ResetInterlockStatus();
    }

    /// <summary>
    /// Initializes the simulator asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Initialize - sets all interlocks to safe state
    /// </remarks>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Minimal delay

        lock (_lock)
        {
            ResetInterlockStatus();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: CheckAllInterlocksAsync - atomic interlock verification
    /// @MX:WARN: Safety-critical - must complete within 10ms per SPEC
    /// </remarks>
    public Task<InterlockStatus> CheckAllInterlocksAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            // Return a copy of the current status to prevent external modification
            var statusCopy = new InterlockStatus
            {
                door_closed = _interlockStatus.door_closed,
                emergency_stop_clear = _interlockStatus.emergency_stop_clear,
                thermal_normal = _interlockStatus.thermal_normal,
                generator_ready = _interlockStatus.generator_ready,
                detector_ready = _interlockStatus.detector_ready,
                collimator_valid = _interlockStatus.collimator_valid,
                table_locked = _interlockStatus.table_locked,
                dose_within_limits = _interlockStatus.dose_within_limits,
                aec_configured = _interlockStatus.aec_configured
            };

            return Task.FromResult(statusCopy);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: EmergencyStandbyAsync - emergency stop activation
    /// @MX:WARN: Safety-critical - immediately disables exposure capability
    /// </remarks>
    public Task EmergencyStandbyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            // Emergency standby activates emergency stop
            _interlockStatus.emergency_stop_clear = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the state of an individual interlock.
    /// </summary>
    /// <param name="interlockName">The name of the interlock to set.</param>
    /// <param name="enabled">True to enable (safe), false to disable (unsafe).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:ANCHOR: SetInterlockStateAsync - controls individual interlock state
    /// @MX:WARN: Safety-critical - directly affects exposure blocking
    /// </remarks>
    public Task SetInterlockStateAsync(string interlockName, bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            switch (interlockName)
            {
                case "door_closed":
                    _interlockStatus.door_closed = enabled;
                    break;
                case "emergency_stop_clear":
                    _interlockStatus.emergency_stop_clear = enabled;
                    break;
                case "thermal_normal":
                    _interlockStatus.thermal_normal = enabled;
                    break;
                case "generator_ready":
                    _interlockStatus.generator_ready = enabled;
                    break;
                case "detector_ready":
                    _interlockStatus.detector_ready = enabled;
                    break;
                case "collimator_valid":
                    _interlockStatus.collimator_valid = enabled;
                    break;
                case "table_locked":
                    _interlockStatus.table_locked = enabled;
                    break;
                case "dose_within_limits":
                    _interlockStatus.dose_within_limits = enabled;
                    break;
                case "aec_configured":
                    _interlockStatus.aec_configured = enabled;
                    break;
                default:
                    throw new ArgumentException($"Unknown interlock: {interlockName}", nameof(interlockName));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the state of an individual interlock.
    /// </summary>
    /// <param name="interlockName">The name of the interlock to query.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The current state of the interlock.</returns>
    /// <remarks>
    /// @MX:NOTE: GetInterlockStateAsync - queries individual interlock state
    /// </remarks>
    public Task<bool> GetInterlockStateAsync(string interlockName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            bool state = interlockName switch
            {
                "door_closed" => _interlockStatus.door_closed,
                "emergency_stop_clear" => _interlockStatus.emergency_stop_clear,
                "thermal_normal" => _interlockStatus.thermal_normal,
                "generator_ready" => _interlockStatus.generator_ready,
                "detector_ready" => _interlockStatus.detector_ready,
                "collimator_valid" => _interlockStatus.collimator_valid,
                "table_locked" => _interlockStatus.table_locked,
                "dose_within_limits" => _interlockStatus.dose_within_limits,
                "aec_configured" => _interlockStatus.aec_configured,
                _ => throw new ArgumentException($"Unknown interlock: {interlockName}", nameof(interlockName))
            };

            return Task.FromResult(state);
        }
    }

    /// <summary>
    /// Checks whether exposure is currently blocked by any interlock.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if exposure is blocked (any interlock failed), false if all pass.</returns>
    /// <remarks>
    /// @MX:ANCHOR: IsExposureBlockedAsync - safety gate for exposure control
    /// @MX:WARN: Safety-critical - returns true when any interlock is unsafe
    /// </remarks>
    public Task<bool> IsExposureBlockedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            // Exposure is blocked if ANY interlock is in unsafe state (false)
            bool isBlocked = !(
                _interlockStatus.door_closed &&
                _interlockStatus.emergency_stop_clear &&
                _interlockStatus.thermal_normal &&
                _interlockStatus.generator_ready &&
                _interlockStatus.detector_ready &&
                _interlockStatus.collimator_valid &&
                _interlockStatus.table_locked &&
                _interlockStatus.dose_within_limits &&
                _interlockStatus.aec_configured
            );

            return Task.FromResult(isBlocked);
        }
    }

    /// <summary>
    /// Resets the simulator to initial safe state.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: ResetAsync - restores all interlocks to safe state
    /// </remarks>
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            ResetInterlockStatus();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resets all interlocks to safe state.
    /// </summary>
    private void ResetInterlockStatus()
    {
        _interlockStatus = new InterlockStatus
        {
            door_closed = true,
            emergency_stop_clear = true,
            thermal_normal = true,
            generator_ready = true,
            detector_ready = true,
            collimator_valid = true,
            table_locked = true,
            dose_within_limits = true,
            aec_configured = true
        };
    }
}
