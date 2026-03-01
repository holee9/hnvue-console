namespace HnVue.Workflow.StateMachine;

using System;
using System.Collections.Generic;

/// <summary>
/// Context for evaluating transition guards.
/// Contains all relevant state information needed for guard evaluation.
/// </summary>
public class GuardEvaluationContext
{
    /// <summary>
    /// Whether the network is reachable for worklist sync.
    /// Used by: T-01 (IDLE -> WORKLIST_SYNC)
    /// </summary>
    public bool? NetworkReachable { get; init; }

    /// <summary>
    /// Whether the auto-sync interval has elapsed.
    /// Used by: T-01 (IDLE -> WORKLIST_SYNC)
    /// </summary>
    public bool? AutoSyncIntervalElapsed { get; init; }

    /// <summary>
    /// Whether all hardware interlocks are in required state.
    /// Used by: T-02, T-07, T-13
    /// </summary>
    public bool? HardwareInterlockOk { get; init; }

    /// <summary>
    /// Whether the detector is ready for acquisition.
    /// Used by: T-07 (POSITION_AND_PREVIEW -> EXPOSURE_TRIGGER)
    /// </summary>
    public bool? DetectorReady { get; init; }

    /// <summary>
    /// Whether the selected protocol is valid.
    /// Used by: T-06 (PROTOCOL_SELECT -> POSITION_AND_PREVIEW)
    /// </summary>
    public bool? ProtocolValid { get; init; }

    /// <summary>
    /// Whether exposure parameters are within safe limits.
    /// Used by: T-06 (PROTOCOL_SELECT -> POSITION_AND_PREVIEW)
    /// </summary>
    public bool? ExposureParamsInSafeRange { get; init; }

    /// <summary>
    /// Whether the worklist response was received with valid count.
    /// Used by: T-03 (WORKLIST_SYNC -> PATIENT_SELECT)
    /// </summary>
    public bool? WorklistResponseValid { get; init; }

    /// <summary>
    /// Whether worklist retry count has reached max retries.
    /// Used by: T-04 (WORKLIST_SYNC -> PATIENT_SELECT on timeout/error)
    /// </summary>
    public bool? WorklistRetryCountExceeded { get; init; }

    /// <summary>
    /// Whether patient ID is not empty.
    /// Used by: T-05 (PATIENT_SELECT -> PROTOCOL_SELECT)
    /// </summary>
    public bool? PatientIdNotEmpty { get; init; }

    /// <summary>
    /// Whether image data is valid after acquisition.
    /// Used by: T-08, T-09 (EXPOSURE_TRIGGER -> QC_REVIEW)
    /// </summary>
    public bool? ImageDataValid { get; init; }

    /// <summary>
    /// Whether the study has more exposures planned.
    /// Used by: T-10, T-11 (QC_REVIEW -> MPPS_COMPLETE or PROTOCOL_SELECT)
    /// </summary>
    public bool? StudyHasMoreExposures { get; init; }

    /// <summary>
    /// Whether a reject reason was provided.
    /// Used by: T-12 (QC_REVIEW -> REJECT_RETAKE)
    /// </summary>
    public bool? RejectReasonProvided { get; init; }

    /// <summary>
    /// Whether all images were transferred.
    /// Used by: T-16 (PACS_EXPORT -> IDLE)
    /// </summary>
    public bool? AllImagesTransferred { get; init; }

    /// <summary>
    /// Whether export retry count has reached max retries.
    /// Used by: T-17 (PACS_EXPORT -> IDLE on export failed)
    /// </summary>
    public bool? ExportRetryCountExceeded { get; init; }

    /// <summary>
    /// Whether the study has images count > 0.
    /// Used by: T-15 (MPPS_COMPLETE -> PACS_EXPORT)
    /// </summary>
    public bool? StudyHasImages { get; init; }

    /// <summary>
    /// Whether the operator is authorized for the action.
    /// Used by: T-19 (ANY -> IDLE on study abort)
    /// </summary>
    public bool? OperatorAuthorized { get; init; }

    /// <summary>
    /// Whether this is an emergency/unscheduled workflow.
    /// Used by: T-02 (IDLE -> PATIENT_SELECT emergency path)
    /// SPEC-WORKFLOW-001: FR-WF-07 Emergency workflow bypass
    /// </summary>
    public bool? IsEmergencyWorkflow { get; init; }

    /// <summary>
    /// Additional metadata for guard evaluation.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; } = new();
}

/// <summary>
/// Result of guard evaluation for a transition.
/// </summary>
public class GuardEvaluationResult
{
    /// <summary>
    /// Whether all guards passed.
    /// </summary>
    public bool AllPassed { get; init; }

    /// <summary>
    /// List of guard names that failed.
    /// </summary>
    public string[] FailedGuards { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful guard evaluation result.
    /// </summary>
    public static GuardEvaluationResult Passed => new() { AllPassed = true };

    /// <summary>
    /// Creates a failed guard evaluation result.
    /// </summary>
    public static GuardEvaluationResult Failed(params string[] failedGuards) =>
        new() { AllPassed = false, FailedGuards = failedGuards };
}
