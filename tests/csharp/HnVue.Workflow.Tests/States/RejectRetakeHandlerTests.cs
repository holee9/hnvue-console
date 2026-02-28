using FluentAssertions;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for RejectRetakeHandler state transitions.
/// SPEC-WORKFLOW-001: Workflow state management tests.
/// </summary>
public class RejectRetakeHandlerTests
{
    private readonly RejectRetakeHandler _sut;
    private readonly StudyContext _context;

    public RejectRetakeHandlerTests()
    {
        var logger = new NullLogger<RejectRetakeHandler>();
        _sut = new RejectRetakeHandler(logger);

        _context = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.RejectRetake
        };
    }

    [Fact]
    public async Task EnterAsync_LogsStateEntry_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_context, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.RejectRetake);
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
    [InlineData(WorkflowState.ExposureTrigger, true)]
    [InlineData(WorkflowState.MppsComplete, true)]
    [InlineData(WorkflowState.RejectRetake, false)]
    [InlineData(WorkflowState.QcReview, false)]
    [InlineData(WorkflowState.PatientSelect, false)]
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
    public void State_ReturnsRejectRetake()
    {
        // Arrange & Act
        var state = _sut.State;

        // Assert
        state.Should().Be(WorkflowState.RejectRetake);
    }
}
