namespace HnVue.Workflow.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents a test scenario for HAL simulator orchestration.
/// </summary>
/// <remarks>
/// @MX:NOTE: Simulator scenario - defines test scenario steps
/// @MX:SPEC: SPEC-WORKFLOW-001 TASK-405
///
/// Scenarios allow predefined sequences of simulator state changes for testing.
/// Common scenarios include normal workflow, door opens during exposure,
/// emergency stop activation, temperature overheat, etc.
/// </remarks>
public class SimulatorScenario
{
    private readonly System.Collections.Generic.List<ScenarioStep> _steps = new();
    private int _currentStep;

    /// <summary>
    /// Initializes a new instance of the SimulatorScenario class.
    /// </summary>
    /// <param name="name">The name of the scenario.</param>
    public SimulatorScenario(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the name of the scenario.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the total number of steps in the scenario.
    /// </summary>
    public int StepCount => _steps.Count;

    /// <summary>
    /// Event raised when progress changes.
    /// </summary>
    public event EventHandler<int>? ProgressChanged;

    /// <summary>
    /// Adds a step to the scenario.
    /// </summary>
    /// <param name="action">The action to execute for this step.</param>
    /// <remarks>
    /// @MX:NOTE: AddStep - adds a scenario step
    /// </remarks>
    public void AddStep(Func<HalSimulatorOrchestrator, CancellationToken, Task> action)
    {
        _steps.Add(new ScenarioStep(action));
    }

    /// <summary>
    /// Executes all steps in the scenario.
    /// </summary>
    /// <param name="orchestrator">The orchestrator to run the scenario on.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// @MX:NOTE: ExecuteAsync - runs scenario steps
    /// </remarks>
    public async Task ExecuteAsync(HalSimulatorOrchestrator orchestrator, CancellationToken cancellationToken = default)
    {
        for (_currentStep = 0; _currentStep < _steps.Count; _currentStep++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _steps[_currentStep].ExecuteAsync(orchestrator, cancellationToken);

            // Report progress
            int progress = (int)((_currentStep + 1) * 100.0 / _steps.Count);
            OnProgressChanged(progress);
        }
    }

    /// <summary>
    /// Raises the ProgressChanged event.
    /// </summary>
    protected virtual void OnProgressChanged(int progress)
    {
        ProgressChanged?.Invoke(this, progress);
    }

    /// <summary>
    /// Creates a normal workflow scenario.
    /// </summary>
    /// <returns>A scenario representing normal workflow operation.</returns>
    /// <remarks>
    /// @MX:NOTE: CreateNormalWorkflow - creates standard workflow scenario
    /// </remarks>
    public static SimulatorScenario CreateNormalWorkflow()
    {
        var scenario = new SimulatorScenario("Normal Workflow");

        // Step 1: All interlocks are in safe state
        scenario.AddStep(async (orch, ct) =>
        {
            // Ensure all interlocks are safe (default state after reset)
            await Task.CompletedTask;
        });

        // Step 2: Detector acquisition cycle
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.Detector.StartAcquisitionAsync(ct);
            await Task.Delay(50, ct); // Simulate brief acquisition
            await orch.Detector.StopAcquisitionAsync(ct);
        });

        return scenario;
    }

    /// <summary>
    /// Creates a scenario where the door opens during exposure.
    /// </summary>
    /// <returns>A scenario simulating door opening during exposure.</returns>
    /// <remarks>
    /// @MX:NOTE: CreateDoorOpensDuringExposure - creates safety interlock test scenario
    /// </remarks>
    public static SimulatorScenario CreateDoorOpensDuringExposure()
    {
        var scenario = new SimulatorScenario("Door Opens During Exposure");

        // Step 1: Initial state - all safe
        scenario.AddStep(async (orch, ct) =>
        {
            await Task.CompletedTask;
        });

        // Step 2: Door opens (safety interlock triggered)
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.SafetyInterlock.SetInterlockStateAsync("door_closed", false, ct);
        });

        return scenario;
    }

    /// <summary>
    /// Creates a scenario where emergency stop is activated.
    /// </summary>
    /// <returns>A scenario simulating emergency stop activation.</returns>
    /// <remarks>
    /// @MX:NOTE: CreateEmergencyStopActivation - creates emergency stop scenario
    /// </remarks>
    public static SimulatorScenario CreateEmergencyStopActivation()
    {
        var scenario = new SimulatorScenario("Emergency Stop Activation");

        // Step 1: Initial state - all safe
        scenario.AddStep(async (orch, ct) =>
        {
            await Task.CompletedTask;
        });

        // Step 2: Emergency stop activated
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.SafetyInterlock.EmergencyStandbyAsync(ct);
        });

        return scenario;
    }

    /// <summary>
    /// Creates a scenario where temperature exceeds normal range.
    /// </summary>
    /// <returns>A scenario simulating temperature overheat.</returns>
    /// <remarks>
    /// @MX:NOTE: CreateTemperatureOverheat - creates thermal fault scenario
    /// </remarks>
    public static SimulatorScenario CreateTemperatureOverheat()
    {
        var scenario = new SimulatorScenario("Temperature Overheat");

        // Step 1: Initial state - all safe
        scenario.AddStep(async (orch, ct) =>
        {
            await Task.CompletedTask;
        });

        // Step 2: Temperature rises to unsafe level
        scenario.AddStep(async (orch, ct) =>
        {
            await orch.SafetyInterlock.SetInterlockStateAsync("thermal_normal", false, ct);
        });

        return scenario;
    }

    /// <summary>
    /// Represents a single step in a scenario.
    /// </summary>
    private readonly record struct ScenarioStep(Func<HalSimulatorOrchestrator, CancellationToken, Task> Action)
    {
        public Task ExecuteAsync(HalSimulatorOrchestrator orchestrator, CancellationToken cancellationToken) =>
            Action(orchestrator, cancellationToken);
    }
}
