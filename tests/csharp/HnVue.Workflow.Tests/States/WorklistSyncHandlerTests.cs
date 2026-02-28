using FluentAssertions;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for WorklistSyncHandler state transitions.
/// SPEC-WORKFLOW-001: DICOM worklist synchronization state management tests.
/// </summary>
public class WorklistSyncHandlerTests
{
    private readonly WorklistSyncHandler _sut;
    private readonly StudyContext _context;

    public WorklistSyncHandlerTests()
    {
        var logger = new NullLogger<WorklistSyncHandler>();
        _sut = new WorklistSyncHandler(logger);

        _context = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.WorklistSync
        };
    }

    [Fact]
    public async Task EnterAsync_LogsWorklistQuery_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_context, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.WorklistSync);
    }

    [Fact]
    public async Task ExitAsync_LogsStateExit_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.ExitAsync(_context, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(WorkflowState.PositionAndPreview, true)]
    [InlineData(WorkflowState.WorklistSync, false)]
    [InlineData(WorkflowState.ProtocolSelect, false)]
    [InlineData(WorkflowState.ExposureTrigger, false)]
    [InlineData(WorkflowState.Idle, false)]
    public async Task CanTransitionToAsync_ValidatesTransitions_Correctly(
        WorkflowState targetState,
        bool expected)
    {
        // Arrange & Act
        var result = await _sut.CanTransitionToAsync(targetState, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void State_ReturnsWorklistSync()
    {
        // Arrange & Act
        var state = _sut.State;

        // Assert
        state.Should().Be(WorkflowState.WorklistSync);
    }
}
