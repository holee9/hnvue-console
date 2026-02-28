using FluentAssertions;
using Xunit;

namespace HnVue.Workflow.Tests.StateMachine;

/// <summary>
/// Tests for workflow transition definitions.
/// SPEC-WORKFLOW-001 Section 2.3: Transition Table (19 transitions)
/// </summary>
public class WorkflowTransitionTests
{
    [Fact]
    public void TransitionTable_ShouldDefineAll19Transitions()
    {
        // Arrange & Act
        var transitions = TransitionGuardMatrix.GetAllTransitions();

        // Assert
        // The SPEC defines 19 logical transitions:
        // - T-01 through T-17: 17 basic transitions (T-04 has 2 trigger variants, counted as 1 transition)
        // - T-18: CriticalHardwareError (applies to all states, counted as 1 transition)
        // - T-19: StudyAbortRequested (applies to non-Idle states, counted as 1 transition)
        // Total: 17 + 1 + 1 = 19 logical transitions

        // The implementation has more physical entries because:
        // - T-04 is defined twice (WorklistTimeout, WorklistError)
        // - T-18 is defined for ALL 10 states
        // - T-19 is defined for 9 non-Idle states
        // Total entries: 17 + 1 (duplicate T-04) + 10 (T-18) + 9 (T-19) = 37

        transitions.Should().HaveCount(37, "19 logical transitions implemented as 37 physical entries due to T-04 duplication and global handlers");

        // Verify we have the 17 basic transitions (excluding T-18 and T-19)
        // Note: This will be 18 because T-04 has 2 entries
        var basicTransitions = transitions.Where(t =>
            t.Trigger != "CriticalHardwareError" &&
            t.Trigger != "StudyAbortRequested").ToList();

        basicTransitions.Should().HaveCount(18, "T-04 has 2 trigger variants, making 18 physical entries for 17 logical transitions");
    }

    [Theory]
    [InlineData("T-01", WorkflowState.Idle, WorkflowState.WorklistSync, "WorklistSyncRequested")]
    [InlineData("T-02", WorkflowState.Idle, WorkflowState.PatientSelect, "EmergencyWorkflowRequested")]
    [InlineData("T-03", WorkflowState.WorklistSync, WorkflowState.PatientSelect, "WorklistResponseReceived")]
    [InlineData("T-04a", WorkflowState.WorklistSync, WorkflowState.PatientSelect, "WorklistTimeout")]
    [InlineData("T-04b", WorkflowState.WorklistSync, WorkflowState.PatientSelect, "WorklistError")]
    [InlineData("T-05", WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, "PatientConfirmed")]
    [InlineData("T-06", WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview, "ProtocolConfirmed")]
    [InlineData("T-07", WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "OperatorReady")]
    [InlineData("T-08", WorkflowState.ExposureTrigger, WorkflowState.QcReview, "AcquisitionComplete")]
    [InlineData("T-09", WorkflowState.ExposureTrigger, WorkflowState.QcReview, "AcquisitionFailed")]
    // T-10 and T-11 both use "ImageAccepted" trigger with different guards
    [InlineData("T-10", WorkflowState.QcReview, WorkflowState.MppsComplete, "ImageAccepted")]
    [InlineData("T-11", WorkflowState.QcReview, WorkflowState.ProtocolSelect, "ImageAccepted")]
    [InlineData("T-12", WorkflowState.QcReview, WorkflowState.RejectRetake, "ImageRejected")]
    [InlineData("T-13", WorkflowState.RejectRetake, WorkflowState.PositionAndPreview, "RetakeApproved")]
    [InlineData("T-14", WorkflowState.RejectRetake, WorkflowState.MppsComplete, "RetakeCancelled")]
    [InlineData("T-15", WorkflowState.MppsComplete, WorkflowState.PacsExport, "ExportInitiated")]
    [InlineData("T-16", WorkflowState.PacsExport, WorkflowState.Idle, "ExportComplete")]
    [InlineData("T-17", WorkflowState.PacsExport, WorkflowState.Idle, "ExportFailed")]
    // T-18: ANY -> IDLE (CriticalHardwareError) - tested separately for multiple states
    [InlineData("T-18a", WorkflowState.ExposureTrigger, WorkflowState.Idle, "CriticalHardwareError")]
    [InlineData("T-18b", WorkflowState.WorklistSync, WorkflowState.Idle, "CriticalHardwareError")]
    // T-19: ANY (except IDLE) -> IDLE (StudyAbortRequested) - tested separately
    [InlineData("T-19a", WorkflowState.ExposureTrigger, WorkflowState.Idle, "StudyAbortRequested")]
    [InlineData("T-19b", WorkflowState.QcReview, WorkflowState.Idle, "StudyAbortRequested")]
    public void TransitionTable_ShouldContainAllSpecifiedTransitions(
        string transitionId,
        WorkflowState fromState,
        WorkflowState toState,
        string trigger)
    {
        // Arrange
        var matrix = new TransitionGuardMatrix();

        // Act
        var isDefined = matrix.IsTransitionDefined(fromState, toState, trigger);

        // Assert
        isDefined.Should().BeTrue($"transition {transitionId} ({fromState} -> {toState} via {trigger}) should be defined");
    }

    [Fact]
    public void TransitionResult_Success_ShouldContainNewState()
    {
        // Arrange
        var fromState = WorkflowState.Idle;
        var toState = WorkflowState.WorklistSync;

        // Act
        var result = TransitionResult.Success(fromState, toState, "WorklistSyncRequested");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OldState.Should().Be(fromState);
        result.NewState.Should().Be(toState);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void TransitionResult_Failure_ShouldContainErrorMessage()
    {
        // Arrange
        var fromState = WorkflowState.Idle;
        var reason = "Invalid transition: not defined in transition table";

        // Act
        var result = TransitionResult.Failure(fromState, reason, Array.Empty<string>());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.OldState.Should().Be(fromState);
        result.NewState.Should().Be(fromState); // State should not change
        result.ErrorMessage.Should().Be(reason);
    }
}
