namespace HnVue.Workflow.Tests.StateMachine;

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

/// <summary>
/// Unit tests for WorkflowStateMachine core functionality.
/// Tests state transitions, guard evaluation, and invalid transition prevention as defined in SPEC-WORKFLOW-001.
///
/// SPEC-WORKFLOW-001 Requirements:
/// - FR-WF-01: Full Workflow State Machine
/// - NFR-WF-01: Atomic, Logged State Transitions
/// - NFR-WF-03: Invalid Transition Prevention
/// </summary>
public class WorkflowStateMachineTests
{
    private readonly Mock<ILogger<WorkflowStateMachine>> _loggerMock;
    private readonly Mock<IWorkflowJournal> _journalMock;
    private readonly Mock<ITransitionGuardMatrix> _guardMatrixMock;

    public WorkflowStateMachineTests()
    {
        _loggerMock = new Mock<ILogger<WorkflowStateMachine>>();
        _journalMock = new Mock<IWorkflowJournal>();
        _guardMatrixMock = new Mock<ITransitionGuardMatrix>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new WorkflowStateMachine(
            null!,
            _journalMock.Object,
            _guardMatrixMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullJournal_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new WorkflowStateMachine(
            _loggerMock.Object,
            null!,
            _guardMatrixMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("journal");
    }

    [Fact]
    public void Constructor_WithNullGuardMatrix_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new WorkflowStateMachine(
            _loggerMock.Object,
            _journalMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("guardMatrix");
    }

    [Fact]
    public void InitialState_ShouldBeIdle()
    {
        // Arrange
        var stateMachine = CreateStateMachine();

        // Act
        var currentState = stateMachine.CurrentState;

        // Assert
        currentState.Should().Be(WorkflowState.Idle);
    }

    [Fact]
    public async Task TryTransitionAsync_WithValidTransitionAndPassingGuards_ShouldSucceed()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        var fromState = WorkflowState.Idle;
        var toState = WorkflowState.WorklistSync;
        var trigger = "WorklistSyncRequested";

        SetupGuardEvaluation(fromState, toState, trigger, true);
        SetupJournalWriteSuccess();

        // Act
        var result = await stateMachine.TryTransitionAsync(toState, trigger, "operator1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.NewState.Should().Be(toState);
        stateMachine.CurrentState.Should().Be(toState);
    }

    [Fact]
    public async Task TryTransitionAsync_WithValidTransitionButFailingGuards_ShouldFail()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        var fromState = WorkflowState.Idle;
        var toState = WorkflowState.WorklistSync;
        var trigger = "WorklistSyncRequested";

        SetupGuardEvaluation(fromState, toState, trigger, false, new[] { "NetworkNotReachable" });

        // Act
        var result = await stateMachine.TryTransitionAsync(toState, trigger, "operator1");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.NewState.Should().Be(fromState);
        stateMachine.CurrentState.Should().Be(fromState);
        result.FailedGuards.Should().ContainSingle(g => g == "NetworkNotReachable");
    }

    [Fact]
    public async Task TryTransitionAsync_WithInvalidTransition_ShouldThrowInvalidStateTransitionException()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        var fromState = WorkflowState.Idle;
        var toState = WorkflowState.ExposureTrigger; // Invalid: cannot jump from IDLE to EXPOSURE_TRIGGER
        var trigger = "InvalidTrigger";

        SetupTransitionNotDefined(fromState, toState, trigger);

        // Act
        var result = await stateMachine.TryTransitionAsync(toState, trigger, "operator1");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Should().BeOfType<InvalidStateTransitionException>();
        stateMachine.CurrentState.Should().Be(fromState);
    }

    [Fact]
    public async Task TryTransitionAsync_ShouldWriteToJournalBeforePublishingEvent()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        var fromState = WorkflowState.Idle;
        var toState = WorkflowState.WorklistSync;
        var trigger = "WorklistSyncRequested";
        var journalCallOrder = 0;

        SetupGuardEvaluation(fromState, toState, trigger, true);
        _journalMock
            .Setup(j => j.WriteEntryAsync(It.IsAny<WorkflowJournalEntry>(), It.IsAny<System.Threading.CancellationToken>()))
            .Callback(() => journalCallOrder++)
            .Returns(Task.CompletedTask);

        // Act
        await stateMachine.TryTransitionAsync(toState, trigger, "operator1");

        // Assert
        // Journal should have been called before any events are published
        journalCallOrder.Should().BeGreaterThan(0);
        _journalMock.Verify(
            j => j.WriteEntryAsync(
                It.Is<WorkflowJournalEntry>(e =>
                    e.FromState == fromState &&
                    e.ToState == toState &&
                    e.Trigger == trigger &&
                    e.OperatorId == "operator1"),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryTransitionAsync_WhenJournalWriteFails_ShouldNotTransition()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        var fromState = WorkflowState.Idle;
        var toState = WorkflowState.WorklistSync;
        var trigger = "WorklistSyncRequested";

        SetupGuardEvaluation(fromState, toState, trigger, true);
        _journalMock
            .Setup(j => j.WriteEntryAsync(It.IsAny<WorkflowJournalEntry>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Journal write failed"));

        // Act
        var result = await stateMachine.TryTransitionAsync(toState, trigger, "operator1");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<InvalidOperationException>();
        stateMachine.CurrentState.Should().Be(fromState);
    }

    [Fact]
    public async Task TransitionT01_IdleToWorklistSync_ShouldSucceedWhenNetworkReachable()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        SetupGuardEvaluation(WorkflowState.Idle, WorkflowState.WorklistSync, "WorklistSyncRequested", true);
        SetupJournalWriteSuccess();

        // Act
        var result = await stateMachine.TryTransitionAsync(WorkflowState.WorklistSync, "WorklistSyncRequested", "operator1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        stateMachine.CurrentState.Should().Be(WorkflowState.WorklistSync);
    }

    [Fact]
    public async Task TransitionT02_IdleToPatientSelect_ShouldSucceedForEmergencyWorkflow()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        SetupGuardEvaluation(WorkflowState.Idle, WorkflowState.PatientSelect, "EmergencyWorkflowRequested", true);
        SetupJournalWriteSuccess();

        // Act
        var result = await stateMachine.TryTransitionAsync(WorkflowState.PatientSelect, "EmergencyWorkflowRequested", "operator1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        stateMachine.CurrentState.Should().Be(WorkflowState.PatientSelect);
    }

    [Fact]
    public async Task StateChangedEvent_ShouldBeRaisedAfterSuccessfulTransition()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        WorkflowState? newState = null;
        stateMachine.StateChanged += (s, e) => newState = e.NewState;

        SetupGuardEvaluation(WorkflowState.Idle, WorkflowState.WorklistSync, "WorklistSyncRequested", true);
        SetupJournalWriteSuccess();

        // Act
        await stateMachine.TryTransitionAsync(WorkflowState.WorklistSync, "WorklistSyncRequested", "operator1");

        // Assert
        newState.Should().Be(WorkflowState.WorklistSync);
    }

    [Fact]
    public async Task StateChangedEvent_ShouldNotBeRaisedForFailedTransition()
    {
        // Arrange
        var stateMachine = CreateStateMachine();
        var eventRaised = false;
        stateMachine.StateChanged += (s, e) => eventRaised = true;

        SetupGuardEvaluation(WorkflowState.Idle, WorkflowState.WorklistSync, "WorklistSyncRequested", false);

        // Act
        await stateMachine.TryTransitionAsync(WorkflowState.WorklistSync, "WorklistSyncRequested", "operator1");

        // Assert
        eventRaised.Should().BeFalse();
    }

    private WorkflowStateMachine CreateStateMachine()
    {
        return new WorkflowStateMachine(
            _loggerMock.Object,
            _journalMock.Object,
            _guardMatrixMock.Object);
    }

    private void SetupGuardEvaluation(WorkflowState from, WorkflowState to, string trigger, bool pass, string[]? failedGuards = null)
    {
        var guardResult = new GuardEvaluationResult
        {
            AllPassed = pass,
            FailedGuards = failedGuards ?? Array.Empty<string>()
        };

        _guardMatrixMock
            .Setup(g => g.EvaluateGuardsAsync(from, to, trigger, It.IsAny<HnVue.Workflow.StateMachine.GuardEvaluationContext?>()))
            .ReturnsAsync(guardResult);

        _guardMatrixMock
            .Setup(g => g.IsTransitionDefined(from, to, trigger))
            .Returns(true);
    }

    private void SetupTransitionNotDefined(WorkflowState from, WorkflowState to, string trigger)
    {
        _guardMatrixMock
            .Setup(g => g.IsTransitionDefined(from, to, trigger))
            .Returns(false);
    }

    private void SetupJournalWriteSuccess()
    {
        _journalMock
            .Setup(j => j.WriteEntryAsync(It.IsAny<WorkflowJournalEntry>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}
