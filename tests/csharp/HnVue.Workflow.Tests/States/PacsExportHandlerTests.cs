using FluentAssertions;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for PacsExportHandler state transitions.
/// SPEC-WORKFLOW-001: PACS export state management tests.
/// </summary>
public class PacsExportHandlerTests
{
    private readonly PacsExportHandler _sut;
    private readonly StudyContext _context;

    public PacsExportHandlerTests()
    {
        var logger = new NullLogger<PacsExportHandler>();
        _sut = new PacsExportHandler(logger);

        _context = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.PacsExport
        };
    }

    [Fact]
    public async Task EnterAsync_LogsStateEntry_Succeeds()
    {
        // Arrange & Act
        var act = async () => await _sut.EnterAsync(_context, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.State.Should().Be(WorkflowState.PacsExport);
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
    [InlineData(WorkflowState.Completed, true)]
    [InlineData(WorkflowState.PacsExport, false)]
    [InlineData(WorkflowState.QcReview, false)]
    [InlineData(WorkflowState.Idle, false)]
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
    public void State_ReturnsPacsExport()
    {
        // Arrange & Act
        var state = _sut.State;

        // Assert
        state.Should().Be(WorkflowState.PacsExport);
    }
}
