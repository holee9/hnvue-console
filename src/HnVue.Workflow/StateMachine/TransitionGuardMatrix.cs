namespace HnVue.Workflow.StateMachine;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
public class TransitionGuardMatrix
{
    private static readonly Lazy<Dictionary<(WorkflowState, WorkflowState, string), GuardDefinition>> _transitionMatrix =
        new Lazy<Dictionary<(WorkflowState, WorkflowState, string), GuardDefinition>>(BuildTransitionMatrix);

    private readonly Dictionary<(WorkflowState, WorkflowState, string), GuardDefinition> _transitionGuards;

    public TransitionGuardMatrix()
    {
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

        // T-03: WORKLIST_SYNC -> PATIENT_SELECT
        // Trigger: WorklistResponseReceived
        // Guard: Response.Count >= 0 (always true if response received)
        matrix[(WorkflowState.WorklistSync, WorkflowState.PatientSelect, "WorklistResponseReceived")] = new GuardDefinition
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

        return matrix;
    }

    private record GuardDefinition
    {
        public required IReadOnlyList<TransitionGuard> Guards { get; init; }
    }

    private record TransitionGuard(string Name, Func<GuardEvaluationContext, bool> Evaluate);
}
