using FluentAssertions;
using HnVue.Workflow.Events;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.Journal;
using HnVue.Workflow.Safety;
using HnVue.Workflow.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HnVue.Workflow.Tests.States;

/// <summary>
/// Unit tests for ExposureTriggerHandler state transitions.
/// SPEC-WORKFLOW-001: Workflow state management tests.
/// </summary>
public class ExposureTriggerHandlerTests
{
    private readonly Mock<ILogger<ExposureTriggerHandler>> _loggerMock;
    private readonly Mock<ISafetyInterlock> _safetyInterlockMock;
    private readonly Mock<IHvgDriver> _hvgDriverMock;
    private readonly Mock<IDoseTracker> _doseTrackerMock;
    private readonly Mock<IWorkflowJournal> _journalMock;
    private readonly Mock<IWorkflowEventPublisher> _eventPublisherMock;
    private readonly ExposureTriggerHandler _sut;
    private readonly StudyContext _context;

    public ExposureTriggerHandlerTests()
    {
        _loggerMock = new Mock<ILogger<ExposureTriggerHandler>>();
        _safetyInterlockMock = new Mock<ISafetyInterlock>();
        _hvgDriverMock = new Mock<IHvgDriver>();
        _doseTrackerMock = new Mock<IDoseTracker>();
        _journalMock = new Mock<IWorkflowJournal>();
        _eventPublisherMock = new Mock<IWorkflowEventPublisher>();

        _sut = new ExposureTriggerHandler(
            _loggerMock.Object,
            _safetyInterlockMock.Object,
            _hvgDriverMock.Object,
            _doseTrackerMock.Object,
            _journalMock.Object,
            _eventPublisherMock.Object);

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

    #region Mid-Exposure Interlock Monitoring Tests (T-08/T-09)

    [Fact]
    public void IsExposureActive_Initially_ReturnsFalse()
    {
        // Arrange & Act
        var isActive = _sut.IsExposureActive;

        // Assert
        isActive.Should().BeFalse();
    }

    #endregion
}
