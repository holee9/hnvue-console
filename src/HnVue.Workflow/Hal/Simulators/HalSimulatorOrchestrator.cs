namespace HnVue.Workflow.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Orchestrates all HAL simulators for unified testing.
/// </summary>
/// <remarks>
/// @MX:NOTE: HAL simulator orchestrator - unified coordination for all simulators
/// @MX:SPEC: SPEC-WORKFLOW-001 TASK-405
///
/// This orchestrator provides:
/// - Unified coordination for all HAL simulators
/// - Scenario playback (normal workflow, door opens during exposure, etc.)
/// - Simulator reset capabilities
/// - Single point of access for all simulators in testing
/// </remarks>
public sealed class HalSimulatorOrchestrator
{
    private readonly object _lock = new();
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the HalSimulatorOrchestrator class.
    /// </summary>
    public HalSimulatorOrchestrator()
    {
        // Create all simulator instances
        HvgDriver = new HvgDriverSimulator();
        Detector = new DetectorSimulator();
        SafetyInterlock = new SafetyInterlockSimulator();
        AecController = new AecControllerSimulator();
        DoseTracker = new DoseTrackerSimulator();
    }

    /// <summary>
    /// Gets the HVG driver simulator.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: HvgDriver - high-voltage generator simulator
    /// </remarks>
    public HvgDriverSimulator HvgDriver { get; }

    /// <summary>
    /// Gets the detector simulator.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Detector - flat-panel detector simulator
    /// </remarks>
    public DetectorSimulator Detector { get; }

    /// <summary>
    /// Gets the safety interlock simulator.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: SafetyInterlock - safety interlock simulator
    /// </remarks>
    public SafetyInterlockSimulator SafetyInterlock { get; }

    /// <summary>
    /// Gets the AEC controller simulator.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: AecController - automatic exposure controller simulator
    /// </remarks>
    public AecControllerSimulator AecController { get; }

    /// <summary>
    /// Gets the dose tracker simulator.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: DoseTracker - radiation dose tracking simulator
    /// </remarks>
    public DoseTrackerSimulator DoseTracker { get; }

    /// <summary>
    /// Initializes all simulators asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: InitializeAsync - initializes all simulators
    /// </remarks>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                return;
            }
        }

        // Initialize all simulators in parallel
        var tasks = new[]
        {
            HvgDriver.InitializeAsync(cancellationToken),
            Detector.InitializeAsync(cancellationToken),
            SafetyInterlock.InitializeAsync(cancellationToken),
            AecController.InitializeAsync(cancellationToken),
            DoseTracker.InitializeAsync(cancellationToken)
        };

        await Task.WhenAll(tasks);

        lock (_lock)
        {
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Plays a scenario across all simulators.
    /// </summary>
    /// <param name="scenario">The scenario to play.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:ANCHOR: PlayScenarioAsync - executes test scenarios
    /// @MX:WARN: Scenario execution - modifies simulator states
    /// </remarks>
    public async Task PlayScenarioAsync(SimulatorScenario scenario, CancellationToken cancellationToken = default)
    {
        if (scenario == null)
        {
            throw new ArgumentNullException(nameof(scenario));
        }

        // Ensure initialized before playing scenario
        await InitializeAsync(cancellationToken);

        // Execute the scenario
        await scenario.ExecuteAsync(this, cancellationToken);
    }

    /// <summary>
    /// Resets all simulators to their initial state.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: ResetAsync - resets all simulators to initial state
    /// </remarks>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        // Reset all simulators in parallel
        var tasks = new[]
        {
            HvgDriver.ResetAsync(cancellationToken),
            Detector.ResetAsync(cancellationToken),
            SafetyInterlock.ResetAsync(cancellationToken),
            AecController.ResetAsync(cancellationToken),
            DoseTracker.ResetAsync(cancellationToken)
        };

        await Task.WhenAll(tasks);

        lock (_lock)
        {
            _isInitialized = false;
        }
    }
}
