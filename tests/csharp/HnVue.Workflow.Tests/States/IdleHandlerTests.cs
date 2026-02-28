using FluentAssertions;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for IdleHandler state transitions.
/// SPEC-WORKFLOW-001: Workflow state management tests.
/// </summary>
public class IdleHandlerTests
{
    private readonly IdleHandler _sut;
    private readonly StudyContext _context;

    public IdleHandlerTests()
    {
        var logger = new NullLogger<IdleHandler>();
        _sut = new IdleHandler(logger);

        _context = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.Idle
        };
    }

    [Fact]
    public async Task EnterAsync_LogsStateEntry_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_context, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.Idle);
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
    [InlineData(WorkflowState.PatientSelect, true)]
    [InlineData(WorkflowState.Idle, false)]
    [InlineData(WorkflowState.ProtocolSelect, false)]
    [InlineData(WorkflowState.ExposureTrigger, false)]
    [InlineData(WorkflowState.Completed, false)]
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
    public void State_ReturnsIdle()
    {
        // Arrange & Act
        var state = _sut.State;

        // Assert
        state.Should().Be(WorkflowState.Idle);
    }
}
