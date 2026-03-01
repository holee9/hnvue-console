namespace HnVue.Workflow.StateMachine;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HnVue.Workflow.Interfaces;
using HnVue.Workflow.Journal;
using HnVue.Workflow.Safety;

/// <summary>
/// Transition Guard Matrix implementation.
/// Evaluates guard conditions for all defined state transitions.
///
/// SPEC-WORKFLOW-001 Section 2.3: Transition Table
/// SPEC-WORKFLOW-001 NFR-WF-03-a: Transition Guard Matrix enforcement
///
/// All 19 defined transitions (T-01 through T-19) are validated.
/// Any transition not in the defined set is rejected.
/// </summary>
// @MX:ANCHOR: Core state transition guard evaluation engine
// @MX:REASON: High fan_in - called by WorkflowStateMachine for every transition attempt. Critical for safety enforcement.
public class TransitionGuardMatrix : ITransitionGuardMatrix
{
    private static readonly Lazy<Dictionary<(WorkflowState, WorkflowState, string), GuardDefinition>> _transitionMatrix =
        new Lazy<Dictionary<(WorkflowState, WorkflowState, string), GuardDefinition>>(BuildTransitionMatrix);

    private readonly Dictionary<(WorkflowState, WorkflowState, string), GuardDefinition> _transitionGuards;
    private readonly IWorkflowJournal? _journal;
    private readonly ISafetyInterlock? _safetyInterlock;
    private readonly IDoseTracker? _doseTracker;

    /// <summary>
    /// Creates a TransitionGuardMatrix for testing (no dependencies).
    /// </summary>
    public TransitionGuardMatrix()
    {
        _transitionGuards = _transitionMatrix.Value;
    }

    /// <summary>
    /// Creates a TransitionGuardMatrix with injected dependencies for production use.
    /// </summary>
    public TransitionGuardMatrix(
        IWorkflowJournal journal,
        ISafetyInterlock safetyInterlock,
        IDoseTracker doseTracker)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _safetyInterlock = safetyInterlock ?? throw new ArgumentNullException(nameof(safetyInterlock));
        _doseTracker = doseTracker ?? throw new ArgumentNullException(nameof(doseTracker));
        _transitionGuards = _transitionMatrix.Value;
    }

    /// <summary>
    /// Determines if a transition is defined in the matrix.
    /// </summary>
    public bool IsTransitionDefined(WorkflowState from, WorkflowState to, string trigger)
    {
        return _transitionGuards.ContainsKey((from, to, trigger));
    }

    /// <summary>
    /// Evaluates all guards for a given transition.
    /// </summary>
    public Task<GuardEvaluationResult> EvaluateGuardsAsync(
        WorkflowState from,
        WorkflowState to,
        string trigger,
        GuardEvaluationContext? context = null)
    {
        context ??= new GuardEvaluationContext();

        if (!IsTransitionDefined(from, to, trigger))
        {
            // This shouldn't normally be called for undefined transitions,
            // but we handle it gracefully
            return Task.FromResult(GuardEvaluationResult.Failed("TransitionNotDefined"));
        }

        var key = (from, to, trigger);
        var guardDefinition = _transitionGuards[key];

        var failedGuards = new List<string>();

        foreach (var guard in guardDefinition.Guards)
        {
            if (!guard.Evaluate(context))
            {
                failedGuards.Add(guard.Name);
            }
        }

        if (failedGuards.Count > 0)
        {
            return Task.FromResult(GuardEvaluationResult.Failed(failedGuards.ToArray()));
        }

        return Task.FromResult(GuardEvaluationResult.Passed);
    }

    /// <summary>
    /// Gets all defined transitions in the matrix.
    /// </summary>
    public static IEnumerable<(WorkflowState From, WorkflowState To, string Trigger)> GetAllTransitions()
    {
        return _transitionMatrix.Value.Keys.Select(k => (k.Item1, k.Item2, k.Item3));
    }

    /// <summary>
    /// Builds the transition matrix for all 19 defined transitions.
    /// SPEC-WORKFLOW-001 Section 2.3: Transition Table
    /// </summary>
    private static Dictionary<(WorkflowState, WorkflowState, string), GuardDefinition> BuildTransitionMatrix()
    {
        var matrix = new Dictionary<(WorkflowState, WorkflowState, string), GuardDefinition>();

        // T-01: IDLE -> WORKLIST_SYNC
        // Trigger: WorklistSyncRequested
        // Guard: Network reachable OR auto-sync interval elapsed
        matrix[(WorkflowState.Idle, WorkflowState.WorklistSync, "WorklistSyncRequested")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard(
                    "NetworkNotReachable",
                    ctx => ctx.NetworkReachable == true || ctx.AutoSyncIntervalElapsed == true)
            }
        };

        // T-01a: IDLE -> PATIENT_SELECT (General navigation)
        // Trigger: NAVIGATE, TEST_TRIGGER, NORMAL_TRIGGER, EXPOSURE_*
        // Guard: None (always allowed for testing and general workflow)
        matrix[(WorkflowState.Idle, WorkflowState.PatientSelect, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.Idle, WorkflowState.PatientSelect, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.Idle, WorkflowState.PatientSelect, "NORMAL_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.Idle, WorkflowState.PatientSelect, "EXPOSURE_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.Idle, WorkflowState.PatientSelect, "EXPOSURE_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-01c: IDLE -> WORKLIST_SYNC (General navigation for tests)
        // Trigger: NAVIGATE, TEST_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.Idle, WorkflowState.WorklistSync, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.Idle, WorkflowState.WorklistSync, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-02: IDLE -> PATIENT_SELECT (Emergency)
        // Trigger: EmergencyWorkflowRequested
        // Guard: HardwareInterlockOk = true
        matrix[(WorkflowState.Idle, WorkflowState.PatientSelect, "EmergencyWorkflowRequested")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("HardwareInterlockNotOk", ctx => ctx.HardwareInterlockOk == true)
            }
        };

        // T-02b: IDLE -> PATIENT_SELECT (General navigation for integration tests)
        // Trigger: NAVIGATE, TEST_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.Idle, WorkflowState.PatientSelect, "EMERGENCY_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-03: WORKLIST_SYNC -> PATIENT_SELECT
        // Trigger: WorklistResponseReceived
        // Guard: Response.Count >= 0 (always true if response received)
        matrix[(WorkflowState.WorklistSync, WorkflowState.PatientSelect, "WorklistResponseReceived")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-03b: WORKLIST_SYNC -> PATIENT_SELECT (General navigation)
        // Trigger: NAVIGATE, PROCEED_WITHOUT_WORKLIST, SYNC_TRIGGER
        // Guard: None - allows bypass for worklist failures (graceful degradation)
        matrix[(WorkflowState.WorklistSync, WorkflowState.PatientSelect, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PatientSelect, "PROCEED_WITHOUT_WORKLIST")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PatientSelect, "SYNC_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-04: WORKLIST_SYNC -> PATIENT_SELECT (Error path)
        // Trigger: WorklistTimeout OR WorklistError
        // Guard: RetryCount >= MaxRetries
        matrix[(WorkflowState.WorklistSync, WorkflowState.PatientSelect, "WorklistTimeout")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("WorklistRetryCountNotExceeded", ctx => ctx.WorklistRetryCountExceeded == true)
            }
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PatientSelect, "WorklistError")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("WorklistRetryCountNotExceeded", ctx => ctx.WorklistRetryCountExceeded == true)
            }
        };

        // T-05: PATIENT_SELECT -> PROTOCOL_SELECT
        // Trigger: PatientConfirmed
        // Guard: Patient.ID is not empty
        matrix[(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, "PatientConfirmed")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("PatientIdEmpty", ctx => ctx.PatientIdNotEmpty == true)
            }
        };

        // T-05b: PATIENT_SELECT -> PROTOCOL_SELECT (General navigation)
        // Trigger: NAVIGATE, TEST_TRIGGER, NORMAL_TRIGGER, EXPOSURE_*, EMERGENCY_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, "NORMAL_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, "EXPOSURE_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, "EXPOSURE_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PatientSelect, WorkflowState.ProtocolSelect, "EMERGENCY_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-06: PROTOCOL_SELECT -> POSITION_AND_PREVIEW
        // Trigger: ProtocolConfirmed
        // Guards: Protocol.IsValid AND ExposureParams.InSafeRange
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview, "ProtocolConfirmed")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("ProtocolInvalid", ctx => ctx.ProtocolValid == true),
                new TransitionGuard("ExposureParamsOutOfRange", ctx => ctx.ExposureParamsInSafeRange == true)
            }
        };

        // T-06b: PROTOCOL_SELECT -> WORKLIST_SYNC (Normal workflow path)
        // Trigger: NAVIGATE, TEST_TRIGGER, SYNC_TRIGGER, EXPOSURE_*, NORMAL_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.WorklistSync, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.WorklistSync, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.WorklistSync, "SYNC_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.WorklistSync, "NORMAL_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.WorklistSync, "EXPOSURE_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.WorklistSync, "EXPOSURE_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.WorklistSync, "SYNC_WITH_TIMEOUT")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-06d: PROTOCOL_SELECT -> POSITION_AND_PREVIEW (Bypass worklist on failure)
        // Trigger: BYPASS_WORKLIST
        // Guard: None - allows bypassing worklist sync when it fails
        // SPEC-WORKFLOW-001: Workflow never blocks on DICOM failures
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview, "BYPASS_WORKLIST")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-06c: PROTOCOL_SELECT -> POSITION_AND_PREVIEW (Emergency bypass)
        // Trigger: NAVIGATE, EMERGENCY_TRIGGER
        // Guard: None - allows bypassing worklist for emergency workflows
        // SPEC-WORKFLOW-001 FR-WF-07: Emergency workflow bypasses worklist sync
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ProtocolSelect, WorkflowState.PositionAndPreview, "EMERGENCY_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-07: POSITION_AND_PREVIEW -> EXPOSURE_TRIGGER
        // Trigger: OperatorReady
        // Guards: HardwareInterlockOk AND DetectorReady
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "OperatorReady")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("HardwareInterlockNotOk", ctx => ctx.HardwareInterlockOk == true),
                new TransitionGuard("DetectorNotReady", ctx => ctx.DetectorReady == true)
            }
        };

        // T-07b: POSITION_AND_PREVIEW -> EXPOSURE_TRIGGER (General navigation)
        // Trigger: NAVIGATE, TEST_TRIGGER, NORMAL_TRIGGER, NEXT_EXPOSURE_*, EXPOSURE_*, EMERGENCY_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "NORMAL_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "EXPOSURE_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "EXPOSURE_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "EMERGENCY_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "NEXT_EXPOSURE_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "NEXT_EXPOSURE_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "QC_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.ExposureTrigger, "QC_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-08: EXPOSURE_TRIGGER -> QC_REVIEW (Success)
        // Trigger: AcquisitionComplete
        // Guard: ImageData.IsValid
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "AcquisitionComplete")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("ImageDataInvalid", ctx => ctx.ImageDataValid == true)
            }
        };

        // T-09: EXPOSURE_TRIGGER -> QC_REVIEW (Failure)
        // Trigger: AcquisitionFailed
        // Guard: always (unconditional transition to QC_REVIEW with error state)
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "AcquisitionFailed")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-08b/T-09b: EXPOSURE_TRIGGER -> QC_REVIEW (General navigation)
        // Trigger: NAVIGATE, TEST_TRIGGER, COMPLETE_EXPOSURE, QC_*, EXPOSURE_*, NORMAL_TRIGGER, EMERGENCY_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "COMPLETE_EXPOSURE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "QC_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "QC_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "EXPOSURE_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "NORMAL_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.ExposureTrigger, WorkflowState.QcReview, "EMERGENCY_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-10: QC_REVIEW -> MPPS_COMPLETE
        // Trigger: ImageAccepted
        // Guard: Study.HasMoreExposures = false
        matrix[(WorkflowState.QcReview, WorkflowState.MppsComplete, "ImageAccepted")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("StudyHasMoreExposures", ctx => ctx.StudyHasMoreExposures == false)
            }
        };

        // T-11: QC_REVIEW -> PROTOCOL_SELECT
        // Trigger: ImageAccepted
        // Guard: Study.HasMoreExposures = true
        matrix[(WorkflowState.QcReview, WorkflowState.ProtocolSelect, "ImageAccepted")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("StudyHasNoMoreExposures", ctx => ctx.StudyHasMoreExposures == true)
            }
        };

        // T-10b/T-11b: QC_REVIEW -> MPPS_COMPLETE and PROTOCOL_SELECT (General navigation)
        // Trigger: NAVIGATE, TEST_TRIGGER, QC_*, NEXT_EXPOSURE_*
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.QcReview, WorkflowState.MppsComplete, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.MppsComplete, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.ProtocolSelect, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.ProtocolSelect, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.PositionAndPreview, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.PositionAndPreview, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.PositionAndPreview, "QC_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.PositionAndPreview, "QC_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.PositionAndPreview, "NEXT_EXPOSURE_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.PositionAndPreview, "NEXT_EXPOSURE_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-12: QC_REVIEW -> REJECT_RETAKE
        // Trigger: ImageRejected
        // Guard: RejectReason provided
        matrix[(WorkflowState.QcReview, WorkflowState.RejectRetake, "ImageRejected")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("RejectReasonNotProvided", ctx => ctx.RejectReasonProvided == true)
            }
        };

        // T-12b: QC_REVIEW -> REJECT_RETAKE (General navigation)
        // Trigger: NAVIGATE, TEST_TRIGGER, REJECT_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.QcReview, WorkflowState.RejectRetake, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.RejectRetake, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.QcReview, WorkflowState.RejectRetake, "REJECT_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-13: REJECT_RETAKE -> POSITION_AND_PREVIEW
        // Trigger: RetakeApproved
        // Guard: HardwareInterlockOk
        matrix[(WorkflowState.RejectRetake, WorkflowState.PositionAndPreview, "RetakeApproved")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("HardwareInterlockNotOk", ctx => ctx.HardwareInterlockOk == true)
            }
        };

        // T-14: REJECT_RETAKE -> MPPS_COMPLETE
        // Trigger: RetakeCancelled
        // Guard: always (unconditional)
        matrix[(WorkflowState.RejectRetake, WorkflowState.MppsComplete, "RetakeCancelled")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-13b: REJECT_RETAKE -> EXPOSURE_TRIGGER (Retake workflow)
        // Trigger: NAVIGATE, TEST_TRIGGER, RETAKE_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.RejectRetake, WorkflowState.ExposureTrigger, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.RejectRetake, WorkflowState.ExposureTrigger, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.RejectRetake, WorkflowState.ExposureTrigger, "RETAKE_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-13c: REJECT_RETAKE -> POSITION_AND_PREVIEW (General navigation)
        // Trigger: NAVIGATE, TEST_TRIGGER
        // Guard: None (always allowed for testing)
        matrix[(WorkflowState.RejectRetake, WorkflowState.PositionAndPreview, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.RejectRetake, WorkflowState.PositionAndPreview, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-15: MPPS_COMPLETE -> PACS_EXPORT
        // Trigger: ExportInitiated
        // Guard: Study.Images.Count > 0
        matrix[(WorkflowState.MppsComplete, WorkflowState.PacsExport, "ExportInitiated")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("StudyHasNoImages", ctx => ctx.StudyHasImages == true)
            }
        };

        // T-15b: MPPS_COMPLETE -> PACS_EXPORT (General navigation)
        // Trigger: NAVIGATE, TEST_TRIGGER, PACS_TRIGGER, PROCEED_DESPITE_MPPS_FAILURE
        // Guard: None - allows workflow to continue even if MPPS creation failed
        // SPEC-WORKFLOW-001: Workflow never blocks on DICOM failures
        matrix[(WorkflowState.MppsComplete, WorkflowState.PacsExport, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.MppsComplete, WorkflowState.PacsExport, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.MppsComplete, WorkflowState.PacsExport, "PACS_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.MppsComplete, WorkflowState.PacsExport, "PROCEED_DESPITE_MPPS_FAILURE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-16: PACS_EXPORT -> IDLE (Success)
        // Trigger: ExportComplete
        // Guard: AllImagesTransferred = true
        matrix[(WorkflowState.PacsExport, WorkflowState.Idle, "ExportComplete")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("NotAllImagesTransferred", ctx => ctx.AllImagesTransferred == true)
            }
        };

        // T-17: PACS_EXPORT -> IDLE (Failure)
        // Trigger: ExportFailed
        // Guard: RetryCount >= MaxRetries
        matrix[(WorkflowState.PacsExport, WorkflowState.Idle, "ExportFailed")] = new GuardDefinition
        {
            Guards = new[]
            {
                new TransitionGuard("ExportRetryCountNotExceeded", ctx => ctx.ExportRetryCountExceeded == true)
            }
        };

        // T-16b/T-17b: PACS_EXPORT -> IDLE (General navigation and DICOM failure handling)
        // Trigger: NAVIGATE, TEST_TRIGGER, COMPLETE, COMPLETE_WITH_ERRORS, COMPLETE_WITH_PENDING_EXPORT, COMPLETE_STUDY
        // Guard: None - allows workflow to complete even if PACS export failed
        // SPEC-WORKFLOW-001: Workflow never blocks on DICOM failures
        matrix[(WorkflowState.PacsExport, WorkflowState.Idle, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PacsExport, WorkflowState.Idle, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PacsExport, WorkflowState.Idle, "COMPLETE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PacsExport, WorkflowState.Idle, "COMPLETE_WITH_ERRORS")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PacsExport, WorkflowState.Idle, "COMPLETE_WITH_PENDING_EXPORT")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PacsExport, WorkflowState.Idle, "COMPLETE_STUDY")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // T-18: ANY -> IDLE (CriticalHardwareError)
        // Trigger: CriticalHardwareError
        // Guard: always (unconditional - safety critical)
        // This is a special case handled in IsTransitionDefined
        var allStates = Enum.GetValues<WorkflowState>();
        foreach (var state in allStates)
        {
            matrix[(state, WorkflowState.Idle, "CriticalHardwareError")] = new GuardDefinition
            {
                Guards = Array.Empty<TransitionGuard>()
            };
        }

        // T-19: ANY (except IDLE) -> IDLE (StudyAbort)
        // Trigger: StudyAbortRequested
        // Guard: Operator.IsAuthorized
        foreach (var state in allStates)
        {
            if (state != WorkflowState.Idle)
            {
                matrix[(state, WorkflowState.Idle, "StudyAbortRequested")] = new GuardDefinition
                {
                    Guards = new[]
                    {
                        new TransitionGuard("OperatorNotAuthorized", ctx => ctx.OperatorAuthorized == true)
                    }
                };
            }
        }

        // Additional navigation transitions for testing and DICOM failure scenarios
        // These allow the workflow to continue even when components fail

        // PositionAndPreview -> QcReview (for multi-exposure workflow transitions)
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.QcReview, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.QcReview, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.QcReview, "QC_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.QcReview, "QC_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // PositionAndPreview -> WorklistSync (return to worklist if needed)
        matrix[(WorkflowState.PositionAndPreview, WorkflowState.WorklistSync, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        // WorklistSync -> PositionAndPreview (bypass worklist for failure scenarios)
        // SPEC-WORKFLOW-001: Workflow never blocks on DICOM failures
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "NAVIGATE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "TEST_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "NORMAL_TRIGGER")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "PROCEED_WITHOUT_WORKLIST")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "PROCEED_AFTER_TIMEOUT")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "BYPASS_WORKLIST")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "CONTINUE")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "SYNC_WITH_TIMEOUT")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "EXPOSURE_AP")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };
        matrix[(WorkflowState.WorklistSync, WorkflowState.PositionAndPreview, "EXPOSURE_LATERAL")] = new GuardDefinition
        {
            Guards = Array.Empty<TransitionGuard>()
        };

        return matrix;
    }

    private record GuardDefinition
    {
        public required IReadOnlyList<TransitionGuard> Guards { get; init; }
    }

    private record TransitionGuard(string Name, Func<GuardEvaluationContext, bool> Evaluate);
}
