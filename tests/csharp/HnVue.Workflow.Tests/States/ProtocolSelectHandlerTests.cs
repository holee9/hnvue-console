using FluentAssertions;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for ProtocolSelectHandler state transitions.
/// SPEC-WORKFLOW-001: Protocol selection and mapping state management tests.
/// </summary>
public class ProtocolSelectHandlerTests
{
    private readonly ProtocolSelectHandler _sut;
    private readonly StudyContext _contextWithProtocol;
    private readonly StudyContext _contextWithoutProtocol;

    public ProtocolSelectHandlerTests()
    {
        var logger = new NullLogger<ProtocolSelectHandler>();
        _sut = new ProtocolSelectHandler(logger);

        _contextWithProtocol = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.ProtocolSelect,
            Metadata = new Dictionary<string, object>
            {
                { "protocol", "CHEST_PA" }
            }
        };

        _contextWithoutProtocol = new StudyContext
        {
            StudyId = "STUDY-002",
            PatientId = "PATIENT-002",
            CurrentState = WorkflowState.ProtocolSelect,
            Metadata = new Dictionary<string, object>()
        };
    }

    [Fact]
    public async Task EnterAsync_WithProtocol_LogsProtocol_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_contextWithProtocol, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.ProtocolSelect);
    }

    [Fact]
    public async Task EnterAsync_WithoutProtocol_LogsWarning_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_contextWithoutProtocol, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.ProtocolSelect);
    }

    [Fact]
    public async Task ExitAsync_LogsStateExit_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.ExitAsync(_contextWithProtocol, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(WorkflowState.WorklistSync, true)]
    [InlineData(WorkflowState.ProtocolSelect, false)]
    [InlineData(WorkflowState.PatientSelect, false)]
    [InlineData(WorkflowState.PositionAndPreview, false)]
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
    public void State_ReturnsProtocolSelect()
    {
        // Arrange & Act
        var state = _sut.State;

        // Assert
        state.Should().Be(WorkflowState.ProtocolSelect);
    }
}
