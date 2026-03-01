namespace HnVue.Workflow.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.Safety;

/// <summary>
/// Simulator for the X-ray detector driver.
/// </summary>
/// <remarks>
/// @MX:NOTE: Detector simulator - test double for IDetector
/// @MX:SPEC: SPEC-WORKFLOW-001 TASK-402
///
/// This simulator provides realistic detector behavior for testing:
/// - Async state transitions (Ready → Acquiring → Ready)
/// - Fault injection for detector communication failures
/// - Acquisition timing simulation
/// - Customizable detector information
/// - Safety interlock integration (detector_ready interlock)
/// </remarks>
public sealed class DetectorSimulator : IDetector
{
    private readonly object _lock = new();
    private readonly ISafetyInterlock? _safetyInterlock;
    private DetectorState _state = DetectorState.Initializing;
    private bool _isReady;
    private string? _errorMessage;
    private bool _faultModeEnabled;
    private int _acquisitionCount;
    private TimeSpan _acquisitionTime = TimeSpan.FromMilliseconds(100);
    private DetectorInfo _detectorInfo;

    /// <summary>
    /// Initializes a new instance of the DetectorSimulator class.
    /// </summary>
    /// <param name="safetyInterlock">Optional safety interlock for integration testing.
    /// When provided, detector_ready interlock is updated when detector enters/exits error state.</param>
    public DetectorSimulator(ISafetyInterlock? safetyInterlock = null)
    {
        _safetyInterlock = safetyInterlock;
        _detectorInfo = new DetectorInfo
        {
            Manufacturer = "Simulated Detector Corp",
            Model = "SD-2000",
            SerialNumber = "SIM-DET-001",
            PixelWidth = 200,
            PixelHeight = 200,
            Columns = 2048,
            Rows = 2048
        };
    }

    /// <summary>
    /// Initializes the simulator asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Initialize - sets up simulator in Ready state
    /// </remarks>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken); // Simulate initialization delay

        lock (_lock)
        {
            _state = DetectorState.Ready;
            _isReady = true;
            _errorMessage = null;
            _acquisitionCount = 0;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: StartAcquisitionAsync - begins image acquisition
    /// @MX:WARN: State transition - affects detector readiness and safety interlock
    /// </remarks>
    public Task StartAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_faultModeEnabled)
            {
                _state = DetectorState.Error;
                _errorMessage = "ERR_ACQUISITION_FAULT";
                _isReady = false;

                // SAFETY: Update safety interlock to reflect detector error state
                // This ensures exposure is blocked when detector is in error
                _ = Task.Run(async () =>
                {
                    if (_safetyInterlock != null)
                    {
                        await _safetyInterlock.SetInterlockStateAsync("detector_ready", false);
                    }
                }, cancellationToken);

                return Task.CompletedTask;
            }

            // Check if already acquiring
            if (_state == DetectorState.Acquiring)
            {
                // Already acquiring, ignore gracefully
                return Task.CompletedTask;
            }

            // Start acquisition
            _state = DetectorState.Acquiring;
            _isReady = false;
            _acquisitionCount++;
        }

        // Simulate acquisition in background
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_acquisitionTime, cancellationToken);
                lock (_lock)
                {
                    if (_state == DetectorState.Acquiring)
                    {
                        _state = DetectorState.Ready;
                        _isReady = true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: StopAcquisitionAsync - stops image acquisition
    /// @MX:WARN: State transition - may interrupt ongoing acquisition
    /// </remarks>
    public Task StopAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_state == DetectorState.Acquiring)
            {
                _state = DetectorState.Ready;
                _isReady = true;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<DetectorStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var status = new DetectorStatus
            {
                State = _state,
                IsReady = _isReady,
                ErrorMessage = _errorMessage
            };

            return Task.FromResult(status);
        }
    }

    /// <inheritdoc/>
    public Task<DetectorInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_detectorInfo);
        }
    }

    /// <summary>
    /// Enables or disables fault injection mode.
    /// </summary>
    /// <param name="enabled">True to enable fault injection, false to disable.</param>
    /// <remarks>
    /// @MX:ANCHOR: SetFaultMode - controls fault injection behavior
    /// @MX:WARN: Fault injection - affects all operations when enabled
    /// Use only for testing fault scenarios
    /// </remarks>
    public void SetFaultMode(bool enabled)
    {
        lock (_lock)
        {
            _faultModeEnabled = enabled;
            if (enabled && _state != DetectorState.Error)
            {
                _errorMessage = "ERR_FAULT_MODE";
            }
        }
    }

    /// <summary>
    /// Clears the current fault condition.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: ClearFaultAsync - restores detector to Ready state and updates safety interlock
    /// </remarks>
    public async Task ClearFaultAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _errorMessage = null;
            _faultModeEnabled = false;
            _state = DetectorState.Ready;
            _isReady = true;
        }

        // SAFETY: Restore safety interlock to safe state when fault is cleared
        // This allows exposure to proceed after detector fault is resolved
        if (_safetyInterlock != null)
        {
            await _safetyInterlock.SetInterlockStateAsync("detector_ready", true, cancellationToken);
        }
    }

    /// <summary>
    /// Sets the detector information.
    /// </summary>
    /// <param name="info">The detector information to set.</param>
    /// <remarks>
    /// @MX:NOTE: SetDetectorInfo - customizes detector metadata
    /// </remarks>
    public void SetDetectorInfo(DetectorInfo info)
    {
        lock (_lock)
        {
            _detectorInfo = info;
        }
    }

    /// <summary>
    /// Sets the simulated acquisition time.
    /// </summary>
    /// <param name="acquisitionTime">The acquisition time to simulate.</param>
    /// <remarks>
    /// @MX:NOTE: SetAcquisitionTime - controls acquisition timing simulation
    /// </remarks>
    public void SetAcquisitionTime(TimeSpan acquisitionTime)
    {
        _acquisitionTime = acquisitionTime;
    }

    /// <summary>
    /// Gets the total number of acquisitions performed.
    /// </summary>
    /// <returns>The acquisition count.</returns>
    public int GetAcquisitionCount()
    {
        lock (_lock)
        {
            return _acquisitionCount;
        }
    }

    /// <summary>
    /// Resets the simulator to initial state.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        lock (_lock)
        {
            _state = DetectorState.Initializing;
            _isReady = false;
            _errorMessage = null;
            _faultModeEnabled = false;
            _acquisitionCount = 0;
            _acquisitionTime = TimeSpan.FromMilliseconds(100);
        }
    }
}
