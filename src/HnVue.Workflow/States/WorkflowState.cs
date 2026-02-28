namespace HnVue.Workflow.States;

/// <summary>
/// Represents the possible states in the radiography workflow.
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Workflow state enumeration - core state machine definition
/// @MX:REASON: Centralized state definition ensures type-safe state transitions
/// @MX:SPEC: SPEC-WORKFLOW-001
///
/// States follow the clinical workflow from patient selection through image acquisition,
/// quality review, and PACS export. Each state has specific entry/exit behaviors and
/// transition guards.
/// </remarks>
public enum WorkflowState
{
    /// <summary>
    /// Initial idle state, ready to start a new study.
    /// </summary>
    Idle,

    /// <summary>
    /// Patient selection and validation in progress.
    /// </summary>
    PatientSelect,

    /// <summary>
    /// Protocol selection and mapping in progress.
    /// </summary>
    ProtocolSelect,

    /// <summary>
    /// DICOM worklist synchronization in progress.
    /// </summary>
    WorklistSync,

    /// <summary>
    /// Patient positioning and image preview in progress.
    /// </summary>
    PositionAndPreview,

    /// <summary>
    /// Exposure trigger waiting or in progress.
    /// </summary>
    ExposureTrigger,

    /// <summary>
    /// MPPS (Modality Performed Procedure Step) completion in progress.
    /// </summary>
    MppsComplete,

    /// <summary>
    /// Quality control review in progress.
    /// </summary>
    QcReview,

    /// <summary>
    /// Image reject/retake workflow in progress.
    /// </summary>
    RejectRetake,

    /// <summary>
    /// PACS (Picture Archiving and Communication System) export in progress.
    /// </summary>
    PacsExport,

    /// <summary>
    /// Terminal state - workflow completed successfully.
    /// </summary>
    Completed
}
