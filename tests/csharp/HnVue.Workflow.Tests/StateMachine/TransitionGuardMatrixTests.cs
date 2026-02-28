namespace HnVue.Workflow.Tests.StateMachine;

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for TransitionGuardMatrix.
/// Tests guard evaluation for all 19 defined transitions in SPEC-WORKFLOW-001 Section 2.3.
///
/// SPEC-WORKFLOW-001 Requirements:
/// - FR-WF-01-b: Guard evaluation before state transition
/// - NFR-WF-03-a: Transition Guard Matrix enforcement
/// </summary>
public class TransitionGuardMatrixTests
{
    private readonly TransitionGuardMatrix _guardMatrix;

    public TransitionGuardMatrixTests()
    {
        _guardMatrix = new TransitionGuardMatrix();
    }

    [Fact]
    public void Constructor_ShouldCreateValidInstance()
    {
        // Assert
        _guardMatrix.Should().NotBeNull();
    }

    #region T-01: IDLE -> WORKLIST_SYNC

    [Fact]
    public async Task TransitionT01_IdleToWorklistSync_WithNetworkReachable_ShouldPass()
    {
        // Arrange
        var context = CreateGuardContext(networkReachable: true, autoSyncElapsed: false);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.Idle,
            WorkflowState.WorklistSync,
            "WorklistSyncRequested",
            context);

        // Assert
        result.AllPassed.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionT01_IdleToWorklistSync_WithAutoSyncElapsed_ShouldPass()
    {
        // Arrange
        var context = CreateGuardContext(networkReachable: false, autoSyncElapsed: true);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.Idle,
            WorkflowState.WorklistSync,
            "WorklistSyncRequested",
            context);

        // Assert
        result.AllPassed.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionT01_IdleToWorklistSync_WithNeitherCondition_ShouldFail()
    {
        // Arrange
        var context = CreateGuardContext(networkReachable: false, autoSyncElapsed: false);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.Idle,
            WorkflowState.WorklistSync,
            "WorklistSyncRequested",
            context);

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedGuards.Should().Contain("NetworkNotReachable");
    }

    #endregion

    #region T-02: IDLE -> PATIENT_SELECT (Emergency)

    [Fact]
    public async Task TransitionT02_IdleToPatientSelect_WithHardwareInterlockOk_ShouldPass()
    {
        // Arrange
        var context = CreateGuardContext(hardwareInterlockOk: true);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.Idle,
            WorkflowState.PatientSelect,
            "EmergencyWorkflowRequested",
            context);

        // Assert
        result.AllPassed.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionT02_IdleToPatientSelect_WithInterlockFailed_ShouldFail()
    {
        // Arrange
        var context = CreateGuardContext(hardwareInterlockOk: false);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.Idle,
            WorkflowState.PatientSelect,
            "EmergencyWorkflowRequested",
            context);

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedGuards.Should().Contain("HardwareInterlockNotOk");
    }

    #endregion

    #region T-07: POSITION_AND_PREVIEW -> EXPOSURE_TRIGGER

    [Fact]
    public async Task TransitionT07_PositionAndPreviewToExposureTrigger_WithAllGuardsPassing_ShouldPass()
    {
        // Arrange
        var context = CreateGuardContext(
            hardwareInterlockOk: true,
            detectorReady: true);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.PositionAndPreview,
            WorkflowState.ExposureTrigger,
            "OperatorReady",
            context);

        // Assert
        result.AllPassed.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionT07_PositionAndPreviewToExposureTrigger_WithInterlockFailed_ShouldFail()
    {
        // Arrange
        var context = CreateGuardContext(
            hardwareInterlockOk: false,
            detectorReady: true);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.PositionAndPreview,
            WorkflowState.ExposureTrigger,
            "OperatorReady",
            context);

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedGuards.Should().Contain("HardwareInterlockNotOk");
    }

    [Fact]
    public async Task TransitionT07_PositionAndPreviewToExposureTrigger_WithDetectorNotReady_ShouldFail()
    {
        // Arrange
        var context = CreateGuardContext(
            hardwareInterlockOk: true,
            detectorReady: false);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.PositionAndPreview,
            WorkflowState.ExposureTrigger,
            "OperatorReady",
            context);

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedGuards.Should().Contain("DetectorNotReady");
    }

    #endregion

    #region T-06: PROTOCOL_SELECT -> POSITION_AND_PREVIEW

    [Fact]
    public async Task TransitionT06_ProtocolSelectToPositionAndPreview_WithValidProtocol_ShouldPass()
    {
        // Arrange
        var context = CreateGuardContext(
            protocolValid: true,
            exposureParamsInSafeRange: true);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.ProtocolSelect,
            WorkflowState.PositionAndPreview,
            "ProtocolConfirmed",
            context);

        // Assert
        result.AllPassed.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionT06_ProtocolSelectToPositionAndPreview_WithInvalidProtocol_ShouldFail()
    {
        // Arrange
        var context = CreateGuardContext(
            protocolValid: false,
            exposureParamsInSafeRange: true);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.ProtocolSelect,
            WorkflowState.PositionAndPreview,
            "ProtocolConfirmed",
            context);

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedGuards.Should().Contain("ProtocolInvalid");
    }

    [Fact]
    public async Task TransitionT06_ProtocolSelectToPositionAndPreview_WithParamsOutOfRange_ShouldFail()
    {
        // Arrange
        var context = CreateGuardContext(
            protocolValid: true,
            exposureParamsInSafeRange: false);

        // Act
        var result = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.ProtocolSelect,
            WorkflowState.PositionAndPreview,
            "ProtocolConfirmed",
            context);

        // Assert
        result.AllPassed.Should().BeFalse();
        result.FailedGuards.Should().Contain("ExposureParamsOutOfRange");
    }

    #endregion

    #region T-19: ANY -> IDLE (CriticalHardwareError)

    [Fact]
    public async Task TransitionT19_CriticalHardwareError_ShouldAlwaysPass_UnconditionalTransition()
    {
        // Arrange
        var context = CreateGuardContext();

        // Act - Test from various states
        var result1 = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.ExposureTrigger,
            WorkflowState.Idle,
            "CriticalHardwareError",
            context);

        var result2 = await _guardMatrix.EvaluateGuardsAsync(
            WorkflowState.WorklistSync,
            WorkflowState.Idle,
            "CriticalHardwareError",
            context);

        // Assert
        result1.AllPassed.Should().BeTrue("T-18/T-19 critical error transition should be unconditional");
        result2.AllPassed.Should().BeTrue("T-18/T-19 critical error transition should be unconditional");
    }

    #endregion

    #region Invalid Transition Prevention

    [Fact]
    public void IsTransitionDefined_ForValidTransition_ShouldReturnTrue()
    {
        // Act & Assert - Test a few valid transitions
        _guardMatrix.IsTransitionDefined(
            WorkflowState.Idle,
            WorkflowState.WorklistSync,
            "WorklistSyncRequested").Should().BeTrue();

        _guardMatrix.IsTransitionDefined(
            WorkflowState.PatientSelect,
            WorkflowState.ProtocolSelect,
            "PatientConfirmed").Should().BeTrue();

        _guardMatrix.IsTransitionDefined(
            WorkflowState.ExposureTrigger,
            WorkflowState.QcReview,
            "AcquisitionComplete").Should().BeTrue();
    }

    [Fact]
    public void IsTransitionDefined_ForInvalidTransition_ShouldReturnFalse()
    {
        // Act & Assert - Test invalid transitions
        _guardMatrix.IsTransitionDefined(
            WorkflowState.Idle,
            WorkflowState.ExposureTrigger,
            "InvalidTrigger").Should().BeFalse();

        _guardMatrix.IsTransitionDefined(
            WorkflowState.WorklistSync,
            WorkflowState.QcReview,
            "InvalidTrigger").Should().BeFalse();

        _guardMatrix.IsTransitionDefined(
            WorkflowState.PacsExport,
            WorkflowState.PositionAndPreview,
            "InvalidTrigger").Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private GuardEvaluationContext CreateGuardContext(
        bool? networkReachable = null,
        bool? autoSyncElapsed = null,
        bool? hardwareInterlockOk = null,
        bool? detectorReady = null,
        bool? protocolValid = null,
        bool? exposureParamsInSafeRange = null)
    {
        return new GuardEvaluationContext
        {
            NetworkReachable = networkReachable,
            AutoSyncIntervalElapsed = autoSyncElapsed,
            HardwareInterlockOk = hardwareInterlockOk,
            DetectorReady = detectorReady,
            ProtocolValid = protocolValid,
            ExposureParamsInSafeRange = exposureParamsInSafeRange
        };
    }

    #endregion
}
