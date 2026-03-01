namespace HnVue.Workflow.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using HnVue.Workflow.Interfaces;

/// <summary>
/// Simulator for the Automatic Exposure Controller (AEC).
/// </summary>
/// <remarks>
/// @MX:NOTE: AEC controller simulator - test double for IAecController
/// @MX:SPEC: SPEC-WORKFLOW-001 TASK-404
///
/// This simulator provides realistic AEC behavior for testing:
/// - Readiness states (NotConfigured → Ready)
/// - Parameter recommendation based on body part thickness
/// - AEC chamber selection (1-3 chambers)
/// - Density index validation (0-3)
/// - Body part thickness validation (1-500mm)
/// </remarks>
public sealed class AecControllerSimulator : IAecController
{
    private readonly object _lock = new();
    private AecState _state = AecState.Initializing;
    private bool _isReady;
    private string? _errorMessage;
    private AecParameters _lastConfiguredParameters;

    /// <summary>
    /// Initializes a new instance of the AecControllerSimulator class.
    /// </summary>
    public AecControllerSimulator()
    {
        _lastConfiguredParameters = new AecParameters
        {
            AecEnabled = false,
            Chamber = 1,
            DensityIndex = 0,
            BodyPartThickness = 200,
            KvPriority = false
        };
    }

    /// <summary>
    /// Initializes the simulator asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: Initialize - sets simulator to NotConfigured state
    /// </remarks>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);

        lock (_lock)
        {
            _state = AecState.NotConfigured;
            _isReady = false;
            _errorMessage = null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: SetAecParametersAsync - configures AEC for exposure
    /// @MX:WARN: Parameter validation - invalid values throw exceptions
    /// </remarks>
    public Task SetAecParametersAsync(AecParameters parameters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            // Validate chamber selection (1-3)
            if (parameters.Chamber < 1 || parameters.Chamber > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters.Chamber),
                    "Chamber must be between 1 and 3");
            }

            // Validate density index (0-3)
            if (parameters.DensityIndex < 0 || parameters.DensityIndex > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters.DensityIndex),
                    "DensityIndex must be between 0 and 3");
            }

            // Validate body part thickness (1-500mm)
            if (parameters.BodyPartThickness < 1 || parameters.BodyPartThickness > 500)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters.BodyPartThickness),
                    "BodyPartThickness must be between 1 and 500 mm");
            }

            // Store the parameters
            _lastConfiguredParameters = parameters;

            // Transition to Ready state
            _state = AecState.Ready;
            _isReady = true;
            _errorMessage = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:ANCHOR: GetAecReadinessAsync - checks AEC readiness status
    /// @MX:WARN: Safety-critical - AEC must be ready when mode is enabled
    /// </remarks>
    public Task<AecStatus> GetAecReadinessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var status = new AecStatus
            {
                State = _state,
                IsReady = _isReady,
                ErrorMessage = _errorMessage
            };

            return Task.FromResult(status);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// @MX:NOTE: GetRecommendedParamsAsync - suggests optimal exposure parameters
    /// </remarks>
    public Task<ExposureParameters> GetRecommendedParamsAsync(int bodyPartThickness, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            // Algorithm: Thicker body parts require higher mAs
            // Base kV: 80 for thin, 120 for thick
            // Base mA: 100-320 based on thickness
            // Exposure time: 100ms fixed for AEC

            double normalizedThickness = bodyPartThickness / 200.0; // Normalize to 200mm baseline

            // kV increases with thickness (80-120 kV range)
            int kv = (int)(80 + (normalizedThickness - 1.0) * 20);
            kv = Math.Max(40, Math.Min(150, kv));

            // mA increases significantly with thickness (100-500 mA range)
            int ma = (int)(200 * normalizedThickness);
            ma = Math.Max(10, Math.Min(500, ma));

            // Fixed exposure time for AEC mode
            int ms = 100;

            var parameters = new ExposureParameters
            {
                Kv = kv,
                Ma = ma,
                Ms = ms
            };

            return Task.FromResult(parameters);
        }
    }

    /// <summary>
    /// Gets the last configured AEC parameters.
    /// </summary>
    /// <returns>The last configured parameters.</returns>
    /// <remarks>
    /// @MX:NOTE: GetLastConfiguredParameters - retrieves configured values
    /// </remarks>
    public AecParameters GetLastConfiguredParameters()
    {
        lock (_lock)
        {
            return _lastConfiguredParameters;
        }
    }

    /// <summary>
    /// Resets the simulator to initial state.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: ResetAsync - restores simulator to NotConfigured state
    /// </remarks>
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _state = AecState.NotConfigured;
            _isReady = false;
            _errorMessage = null;

            _lastConfiguredParameters = new AecParameters
            {
                AecEnabled = false,
                Chamber = 1,
                DensityIndex = 0,
                BodyPartThickness = 200,
                KvPriority = false
            };
        }

        return Task.CompletedTask;
    }
}
