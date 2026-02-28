using FluentAssertions;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for ExposureTriggerHandler state transitions.
/// SPEC-WORKFLOW-001: Workflow state management tests.
/// </summary>
public class ExposureTriggerHandlerTests
{
    private readonly ExposureTriggerHandler _sut;
    private readonly StudyContext _context;

    public ExposureTriggerHandlerTests()
    {
        var logger = new NullLogger<ExposureTriggerHandler>();
        _sut = new ExposureTriggerHandler(logger);

        _context = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.ExposureTrigger
        };
    }

    [Fact]
    public async Task EnterAsync_LogsStateEntry_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_context, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.ExposureTrigger);
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
    [InlineData(WorkflowState.QcReview, true)]
    [InlineData(WorkflowState.MppsComplete, true)]
    [InlineData(WorkflowState.Idle, true)]
    [InlineData(WorkflowState.ExposureTrigger, false)]
    [InlineData(WorkflowState.PatientSelect, false)]
    [InlineData(WorkflowState.PositionAndPreview, false)]
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
    public void State_ReturnsExposureTrigger()
    {
        // Arrange & Act
        var state = _sut.State;

        // Assert
        state.Should().Be(WorkflowState.ExposureTrigger);
    }
}
