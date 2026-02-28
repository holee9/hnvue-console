using FluentAssertions;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for PatientSelectHandler state transitions.
/// SPEC-WORKFLOW-001: Patient selection validation state management tests.
/// </summary>
public class PatientSelectHandlerTests
{
    private readonly PatientSelectHandler _sut;
    private readonly StudyContext _validContext;
    private readonly StudyContext _invalidContext;

    public PatientSelectHandlerTests()
    {
        var logger = new NullLogger<PatientSelectHandler>();
        _sut = new PatientSelectHandler(logger);

        _validContext = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.PatientSelect
        };

        _invalidContext = new StudyContext
        {
            StudyId = "STUDY-002",
            PatientId = "AB",  // Too short
            CurrentState = WorkflowState.PatientSelect
        };
    }

    [Fact]
    public async Task EnterAsync_WithValidPatientId_LogsSuccess_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_validContext, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.PatientSelect);
    }

    [Fact]
    public async Task EnterAsync_WithInvalidPatientId_LogsWarning_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_invalidContext, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.PatientSelect);
    }

    [Fact]
    public async Task ExitAsync_LogsStateExit_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.ExitAsync(_validContext, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(WorkflowState.ProtocolSelect, true)]
    [InlineData(WorkflowState.PatientSelect, false)]
    [InlineData(WorkflowState.WorklistSync, false)]
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
    public void State_ReturnsPatientSelect()
    {
        // Arrange & Act
        var state = _sut.State;

        // Assert
        state.Should().Be(WorkflowState.PatientSelect);
    }
}
