namespace HnVue.Workflow.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using HnVue.Workflow.Interfaces;

/// <summary>
/// Simulator for the High-Voltage Generator (HVG) driver.
/// </summary>
/// <remarks>
/// @MX:NOTE: HVG driver simulator - test double for IHvgDriver
/// @MX:SPEC: SPEC-WORKFLOW-001 TASK-401
///
/// This simulator provides realistic HVG behavior for testing:
/// - Async state transitions (Idle → Preparing → Ready → Exposing → Idle)
/// - Fault injection for HVG communication failures
/// - Exposure timing simulation
/// - Parameter validation
/// </remarks>
public sealed class HvgDriverSimulator : IHvgDriver
{
    private readonly object _lock = new();
    private HvgState _state = HvgState.Initializing;
    private bool _isReady;
    private string? _faultCode;
    private bool _faultModeEnabled;
    private ExposureParameters _lastExposureParameters;
    private int _exposureCount;
    private Task? _currentExposureTask;
    private CancellationTokenSource? _exposureCts;

    /// <summary>
    /// Initializes a new instance of the HvgDriverSimulator class.
    /// </summary>
    public HvgDriverSimulator()
    {
        _lastExposureParameters = new ExposureParameters { Kv = 0, Ma = 0, Ms = 0 };
    }

    /// <summary>
    /// Initializes the simulator asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Initialize - sets up simulator in Idle state
    /// </remarks>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken); // Simulate initialization delay

        lock (_lock)
        {
            _state = HvgState.Idle;
            _isReady = false;
            _faultCode = null;
            _exposureCount = 0;
        }
    }

    /// <summary>
    /// Prepares the generator for exposure.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Prepare - transitions to Ready state after preparation
    /// @MX:WARN: State transition - affects readiness for exposure
    /// </remarks>
    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_faultModeEnabled)
            {
                _state = HvgState.Fault;
                _faultCode = "ERR_PREP_FAULT";
                _isReady = false;
                return;
            }

            _state = HvgState.Preparing;
            _isReady = false;
        }

        // Simulate preparation time
        await Task.Delay(50, cancellationToken);

        lock (_lock)
        {
            _state = HvgState.Ready;
            _isReady = true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: TriggerExposureAsync - core X-ray exposure control
    /// @MX:WARN: Safety-critical - controls ionizing radiation emission
    /// Validates parameters, checks state readiness, simulates exposure timing
    /// </remarks>
    public async Task<bool> TriggerExposureAsync(
        ExposureParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // Validate parameters
        if (parameters.Kv <= 0 || parameters.Ma <= 0 || parameters.Ms <= 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            // Check if ready (must be in Ready state with IsReady=true)
            if (_state != HvgState.Ready || !_isReady)
            {
                return false;
            }

            // Check fault mode
            if (_faultModeEnabled)
            {
                _state = HvgState.Fault;
                _faultCode = "ERR_EXPOSURE_FAULT";
                _isReady = false;
                return false;
            }

            // Start exposure
            _state = HvgState.Exposing;
            _isReady = false;
            _lastExposureParameters = parameters;
            _exposureCount++;
            _exposureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        // Simulate exposure and await completion
        _currentExposureTask = SimulateExposureAsync(parameters.Ms, _exposureCts.Token);

        try
        {
            await _currentExposureTask;
            return true;
        }
        catch (OperationCanceledException)
        {
            // Exposure was cancelled
            return false;
        }
    }

    /// <inheritdoc/>
    public Task AbortExposureAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_state != HvgState.Exposing)
            {
                return Task.CompletedTask;
            }

            // Cancel the exposure task
            _exposureCts?.Cancel();
            _state = HvgState.Idle;
            _isReady = false;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<HvgStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var status = new HvgStatus
            {
                State = _state,
                IsReady = _isReady,
                FaultCode = _faultCode
            };

            return Task.FromResult(status);
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
            if (enabled && _state != HvgState.Fault)
            {
                _faultCode = "ERR_FAULT_MODE";
            }
        }
    }

    /// <summary>
    /// Clears the current fault condition.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ClearFaultAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _faultCode = null;
            _faultModeEnabled = false;
            _state = HvgState.Idle;
            _isReady = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the last exposure parameters.
    /// </summary>
    /// <returns>The last exposure parameters used.</returns>
    public ExposureParameters GetLastExposureParameters()
    {
        lock (_lock)
        {
            return _lastExposureParameters;
        }
    }

    /// <summary>
    /// Gets the total number of exposures performed.
    /// </summary>
    /// <returns>The exposure count.</returns>
    public int GetExposureCount()
    {
        lock (_lock)
        {
            return _exposureCount;
        }
    }

    /// <summary>
    /// Resets the simulator to initial state.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        // Cancel any ongoing exposure
        _exposureCts?.Cancel();

        await Task.Delay(10, cancellationToken);

        lock (_lock)
        {
            _state = HvgState.Initializing;
            _isReady = false;
            _faultCode = null;
            _faultModeEnabled = false;
            _exposureCount = 0;
            _lastExposureParameters = new ExposureParameters { Kv = 0, Ma = 0, Ms = 0 };
            _currentExposureTask = null;
            _exposureCts?.Dispose();
            _exposureCts = null;
        }
    }

    /// <summary>
    /// Simulates the exposure process for the specified duration.
    /// </summary>
    /// <param name="exposureTimeMs">Exposure time in milliseconds.</param>
    /// <param name="cancellationToken">Token to cancel the exposure.</param>
    /// <returns>A task representing the exposure simulation.</returns>
    /// <remarks>
    /// @MX:NOTE: Exposure simulation - simulates timing and completion
    /// </remarks>
    private async Task SimulateExposureAsync(int exposureTimeMs, CancellationToken cancellationToken)
    {
        try
        {
            // Simulate exposure time
            await Task.Delay(exposureTimeMs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Exposure was aborted
            return;
        }
        finally
        {
            lock (_lock)
            {
                _state = HvgState.Idle;
                _isReady = false;
            }
        }
    }
}
