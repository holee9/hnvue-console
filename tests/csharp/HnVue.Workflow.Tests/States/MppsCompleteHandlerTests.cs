using FluentAssertions;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for MppsCompleteHandler state transitions.
/// SPEC-WORKFLOW-001: MPPS completion state management tests.
/// </summary>
public class MppsCompleteHandlerTests
{
    private readonly MppsCompleteHandler _sut;
    private readonly StudyContext _context;

    public MppsCompleteHandlerTests()
    {
        var logger = new NullLogger<MppsCompleteHandler>();
        _sut = new MppsCompleteHandler(logger);

        _context = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.MppsComplete
        };
    }

    [Fact]
    public async Task EnterAsync_LogsStateEntry_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_context, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.MppsComplete);
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
    [InlineData(WorkflowState.MppsComplete, false)]
    [InlineData(WorkflowState.PatientSelect, false)]
    [InlineData(WorkflowState.PacsExport, false)]
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
    public void State_ReturnsMppsComplete()
    {
        // Arrange & Act
        var state = _sut.State;

        // Assert
        state.Should().Be(WorkflowState.MppsComplete);
    }
}
