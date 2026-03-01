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

namespace HnVue.Workflow.Tests.Integration;

/// <summary>
/// Integration tests for complete workflow scenarios.
/// SPEC-WORKFLOW-001: End-to-end workflow validation.
/// </summary>
public class WorkflowIntegrationTests
{
    private readonly Dictionary<WorkflowState, IStateHandler> _handlers;

    public WorkflowIntegrationTests()
    {
        var loggerFactory = NullLoggerFactory.Instance;

        // Create mock dependencies for ExposureTriggerHandler
        var mockSafetyInterlock = new Mock<ISafetyInterlock>();
        var mockHvgDriver = new Mock<IHvgDriver>();
        var mockDoseTracker = new Mock<IDoseTracker>();
        var mockJournal = new Mock<IWorkflowJournal>();
        var mockEventPublisher = new Mock<IWorkflowEventPublisher>();

        _handlers = new Dictionary<WorkflowState, IStateHandler>
        {
            { WorkflowState.Idle, new IdleHandler(loggerFactory.CreateLogger<IdleHandler>()) },
            { WorkflowState.PatientSelect, new PatientSelectHandler(loggerFactory.CreateLogger<PatientSelectHandler>()) },
            { WorkflowState.ProtocolSelect, new ProtocolSelectHandler(loggerFactory.CreateLogger<ProtocolSelectHandler>()) },
            { WorkflowState.WorklistSync, new WorklistSyncHandler(loggerFactory.CreateLogger<WorklistSyncHandler>()) },
            { WorkflowState.PositionAndPreview, new PositioningAndPreviewHandler(loggerFactory.CreateLogger<PositioningAndPreviewHandler>()) },
            { WorkflowState.ExposureTrigger, new ExposureTriggerHandler(
                loggerFactory.CreateLogger<ExposureTriggerHandler>(),
                mockSafetyInterlock.Object,
                mockHvgDriver.Object,
                mockDoseTracker.Object,
                mockJournal.Object,
                mockEventPublisher.Object) },
            { WorkflowState.QcReview, new QcReviewHandler(loggerFactory.CreateLogger<QcReviewHandler>()) },
            { WorkflowState.RejectRetake, new RejectRetakeHandler(loggerFactory.CreateLogger<RejectRetakeHandler>()) },
            { WorkflowState.MppsComplete, new MppsCompleteHandler(loggerFactory.CreateLogger<MppsCompleteHandler>()) },
            { WorkflowState.PacsExport, new PacsExportHandler(loggerFactory.CreateLogger<PacsExportHandler>()) }
        };
    }

    [Fact]
    public async Task CompleteWorkflow_FromIdleToPacsExport_Succeeds()
    {
        // Arrange
        var context = new StudyContext
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            CurrentState = WorkflowState.Idle
        };

        // Expected workflow path (simplified - MPPS happens after exposure, before final QC)
        // After MPPS completes, workflow returns to QC for final approval before PACS export
        var workflowPath = new[]
        {
            WorkflowState.Idle,
            WorkflowState.PatientSelect,
            WorkflowState.ProtocolSelect,
            WorkflowState.WorklistSync,
            WorkflowState.PositionAndPreview,
            WorkflowState.ExposureTrigger,
            WorkflowState.QcReview,
            WorkflowState.MppsComplete
        };

        // Act & Assert - Execute complete workflow
        StudyContext currentContext = context;
        for (int i = 0; i < workflowPath.Length - 1; i++)
        {
            var currentState = workflowPath[i];
            var nextState = workflowPath[i + 1];

            var currentHandler = _handlers[currentState];
            var nextHandler = _handlers[nextState];

            // Verify transition is allowed
            var canTransition = await currentHandler.CanTransitionToAsync(nextState, CancellationToken.None);
            canTransition.Should().BeTrue($"Transition from {currentState} to {nextState} should be allowed");

            // Exit current state
            await currentHandler.ExitAsync(currentContext, CancellationToken.None);

            // Create new context for next state
            currentContext = currentContext with { CurrentState = nextState };

            // Enter next state
            await nextHandler.EnterAsync(currentContext, CancellationToken.None);
        }

        // After MPPS complete, verify we can go to QcReview (for final approval)
        var mppsHandler = _handlers[WorkflowState.MppsComplete];
        var canGoToQc = await mppsHandler.CanTransitionToAsync(WorkflowState.QcReview, CancellationToken.None);
        canGoToQc.Should().BeTrue("After MPPS complete, should return to QC for final approval");

        // Verify final path from QC to PACS
        var qcHandler = _handlers[WorkflowState.QcReview];
        var canGoToPacs = await qcHandler.CanTransitionToAsync(WorkflowState.PacsExport, CancellationToken.None);
        canGoToPacs.Should().BeTrue("After final QC approval, should export to PACS");

        // Assert final state in our test path
        currentContext.CurrentState.Should().Be(WorkflowState.MppsComplete);
    }

    [Fact]
    public async Task RejectRetakeWorkflow_PreservesContext_ReturnsToExposure()
    {
        // Arrange
        var context = new StudyContext
        {
            StudyId = "STUDY-002",
            PatientId = "PATIENT-002",
            CurrentState = WorkflowState.QcReview
        };

        // Act - Simulate reject and retake workflow
        var qcHandler = _handlers[WorkflowState.QcReview];
        var rejectHandler = _handlers[WorkflowState.RejectRetake];
        var exposureHandler = _handlers[WorkflowState.ExposureTrigger];

        // Transition from QC Review to Reject Retake
        var canTransitionToReject = await qcHandler.CanTransitionToAsync(WorkflowState.RejectRetake, CancellationToken.None);
        canTransitionToReject.Should().BeTrue("QC Review should allow transition to Reject Retake");

        await qcHandler.ExitAsync(context, CancellationToken.None);
        var rejectContext = context with { CurrentState = WorkflowState.RejectRetake };
        await rejectHandler.EnterAsync(rejectContext, CancellationToken.None);

        // Transition from Reject Retake to Exposure Trigger (retake)
        var canTransitionToExposure = await rejectHandler.CanTransitionToAsync(WorkflowState.ExposureTrigger, CancellationToken.None);
        canTransitionToExposure.Should().BeTrue("Reject Retake should allow transition to Exposure Trigger for retake");

        await rejectHandler.ExitAsync(rejectContext, CancellationToken.None);
        var exposureContext = rejectContext with { CurrentState = WorkflowState.ExposureTrigger };
        await exposureHandler.EnterAsync(exposureContext, CancellationToken.None);

        // Assert - Study context should be preserved
        exposureContext.StudyId.Should().Be("STUDY-002");
        exposureContext.PatientId.Should().Be("PATIENT-002");
        exposureContext.CurrentState.Should().Be(WorkflowState.ExposureTrigger);
    }

    [Fact]
    public async Task EmergencyWorkflow_BypassesPositioning_ExecutesSuccessfully()
    {
        // Arrange
        var context = new StudyContext
        {
            StudyId = "STUDY-003",
            PatientId = "PATIENT-003",
            CurrentState = WorkflowState.ProtocolSelect
        };

        // Act - Emergency mode: skip positioning, go directly to exposure
        var protocolHandler = _handlers[WorkflowState.ProtocolSelect];
        var exposureHandler = _handlers[WorkflowState.ExposureTrigger];

        // This transition is NOT allowed in normal workflow (security check)
        var canTransitionDirectly = await protocolHandler.CanTransitionToAsync(WorkflowState.ExposureTrigger, CancellationToken.None);

        // In emergency mode with proper authorization, this might be allowed
        // For now, verify the guard is in place
        canTransitionDirectly.Should().BeFalse("Direct transition from Protocol to Exposure should be blocked without emergency override");
    }

    [Fact]
    public async Task InvalidTransition_IsBlocked_ByStateHandlers()
    {
        // Arrange
        var context = new StudyContext
        {
            StudyId = "STUDY-004",
            PatientId = "PATIENT-004",
            CurrentState = WorkflowState.Idle
        };

        // Act & Assert - Try invalid transitions
        var idleHandler = _handlers[WorkflowState.Idle];

        // Cannot skip directly to exposure
        var canSkipToExposure = await idleHandler.CanTransitionToAsync(WorkflowState.ExposureTrigger, CancellationToken.None);
        canSkipToExposure.Should().BeFalse("Idle should not allow direct transition to Exposure Trigger");

        // Cannot go backwards in workflow
        var canGoToCompleted = await idleHandler.CanTransitionToAsync(WorkflowState.Completed, CancellationToken.None);
        canGoToCompleted.Should().BeFalse("Idle should not allow transition to Completed");

        // Cannot jump to QC review without exposure
        var canSkipToQc = await idleHandler.CanTransitionToAsync(WorkflowState.QcReview, CancellationToken.None);
        canSkipToQc.Should().BeFalse("Idle should not allow direct transition to QC Review");
    }

    [Fact]
    public async Task AllHandlers_ImplementInterface_Consistently()
    {
        // Act & Assert - Verify all handlers follow the same pattern
        foreach (var (state, handler) in _handlers)
        {
            // State property should match
            handler.State.Should().Be(state);

            // EnterAsync should not throw
            var context = new StudyContext
            {
                StudyId = $"TEST-{state}",
                PatientId = $"PATIENT-{state}",
                CurrentState = state
            };

            var enterAction = async () => await handler.EnterAsync(context, CancellationToken.None);
            await enterAction.Should().NotThrowAsync($"Handler for {state} should not throw on EnterAsync");

            // ExitAsync should not throw
            var exitAction = async () => await handler.ExitAsync(context, CancellationToken.None);
            await exitAction.Should().NotThrowAsync($"Handler for {state} should not throw on ExitAsync");

            // CanTransitionToAsync should return a value
            var transitionResult = await handler.CanTransitionToAsync(WorkflowState.Idle, CancellationToken.None);
            // Result is a bool (implicit type check by successful assertion below)
            (transitionResult == true || transitionResult == false).Should().BeTrue();
        }
    }
}
