namespace HnVue.Workflow.StateMachine;

/// <summary>
/// Primary workflow states for the HnVue Clinical Workflow Engine.
///
/// SPEC-WORKFLOW-001 Section 2.2: State Definitions
/// IEC 62304 Class C - X-ray exposure control state machine.
/// </summary>
public enum WorkflowState
{
    /// <summary>
    /// S-00: No active study. System ready, hardware on standby.
    /// </summary>
    Idle,

    /// <summary>
    /// S-01: Querying DICOM Worklist server for pending orders.
    /// </summary>
    WorklistSync,

    /// <summary>
    /// S-02: Operator selects a patient from worklist or enters emergency patient data.
    /// </summary>
    PatientSelect,

    /// <summary>
    /// S-03: Operator selects or confirms body part/projection protocol.
    /// </summary>
    ProtocolSelect,

    /// <summary>
    /// S-04: Live detector preview active; operator positions patient.
    /// </summary>
    PositionAndPreview,

    /// <summary>
    /// S-05: Exposure command issued; generator armed and firing.
    /// </summary>
    ExposureTrigger,

    /// <summary>
    /// S-06: Acquired image displayed for operator quality check.
    /// </summary>
    QcReview,

    /// <summary>
    /// S-07: DICOM MPPS N-SET COMPLETED sent; study record closed.
    /// </summary>
    MppsComplete,

    /// <summary>
    /// S-08: DICOM C-STORE transfer in progress to configured PACS node.
    /// </summary>
    PacsExport,

    /// <summary>
    /// S-09: Image rejected; system preparing for retake exposure.
    /// </summary>
    RejectRetake
}
