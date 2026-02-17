# SPEC-WORKFLOW-001: Acceptance Criteria

## TAG: SPEC-WORKFLOW-001

## Reference
- Specification: `spec.md`
- Implementation Plan: `plan.md`
- IEC 62304 Class C — test evidence must be archived and traceable to requirements

---

## AC-WF-01: State Machine Completeness (FR-WF-01)

### AC-WF-01-01: Valid Transition Acceptance
```
Given the Workflow Engine is in a defined source state
When a valid trigger event is received with all guard conditions satisfied
Then the engine transitions to the defined target state
And a WorkflowStateChangedEvent is published within 50 ms
And a journal entry is recorded before the event is published
```

### AC-WF-01-02: Invalid Transition Rejection
```
Given the Workflow Engine is in any state
When a trigger event is received that has no valid transition defined for the current state
Then the engine remains in the current state (no state change)
And a TransitionRejectedResult is returned with AttemptedFromState, AttemptedToState, Trigger, and FailedGuards populated
And the journal records the rejected attempt with result = REJECTED
```

### AC-WF-01-03: Guard Condition Failure
```
Given the Workflow Engine is in POSITION_AND_PREVIEW state
When the operator triggers an exposure
And the hardware interlock IL-01 (room door) reports OPEN
Then the engine remains in POSITION_AND_PREVIEW state
And the TransitionRejectedResult contains FailedGuards = ["HardwareInterlockOk"]
And the operator is notified of the specific failed interlock
```

### AC-WF-01-04: Audit Log Completeness
```
Given any state transition is attempted (successful or rejected)
When the transition processing completes
Then the journal contains an entry with all required fields:
  TransitionId (non-empty GUID),
  Timestamp (UTC, millisecond precision),
  FromState, ToState, Trigger,
  GuardResults (array with at least one entry per evaluated guard),
  OperatorId (non-empty),
  Category (WORKFLOW or SAFETY)
```

### AC-WF-01-05: Concurrent Event Safety
```
Given the Workflow Engine is processing a state transition
When a second transition trigger event arrives concurrently
Then the second event is queued and processed sequentially after the first completes
And no state corruption or duplicate journal entries occur
```

---

## AC-WF-02: Protocol Management (FR-WF-02)

### AC-WF-02-01: Protocol CRUD
```
Given the system is in an appropriate configuration state
When a new Protocol record is created with valid BodyPart, Projection, kVp, mA, ExposureTimeMs, AECMode, FocusSize, GridUsed
Then the protocol is persisted to the database with a unique ProtocolID
And the protocol is retrievable by ProtocolID and by (BodyPart, Projection) key
```

### AC-WF-02-02: Safety Limit Enforcement on Save
```
Given a Protocol record is being saved
When any of kVp, mA, or ExposureTimeMs exceeds the DeviceSafetyLimits bounds
Then the save operation is rejected with a structured error response
And no partial record is written to the database
```

### AC-WF-02-03: Protocol Capacity
```
Given the protocol database contains 500 distinct protocol records indexed by (BodyPart, Projection, DeviceModel)
When a protocol lookup by (BodyPart, Projection) key is executed
Then the result is returned within 50 ms
And the correct protocol record is returned
```

### AC-WF-02-04: Protocol Presentation in PROTOCOL_SELECT
```
Given the Workflow Engine is in PROTOCOL_SELECT state
And the active worklist item has procedure code "XR-CHEST-PA"
When the operator opens the protocol list
Then only protocols mapped to "XR-CHEST-PA" are presented as the primary list
And the highest-confidence-score protocol is pre-selected
```

---

## AC-WF-03: AEC Control Integration (FR-WF-03)

### AC-WF-03-01: AEC Parameters Transmitted Before Arming
```
Given the selected Protocol has AecMode = Enabled
And the Workflow Engine enters POSITION_AND_PREVIEW state
When the state entry action executes
Then IAecController.SetAecParameters() is called with the protocol's AecChambers and sensitivity settings
Before IHvgDriver.ArmGenerator() is called
```

### AC-WF-03-02: AEC Readiness Gate on Exposure Trigger
```
Given AEC mode is Enabled
And IAecController.GetAecReadiness() returns NOT_READY
When the operator triggers exposure in POSITION_AND_PREVIEW state
Then the transition to EXPOSURE_TRIGGER is blocked
And the operator receives notification: "AEC not ready"
```

### AC-WF-03-03: AEC Recommended Parameters Applied
```
Given AEC mode is Enabled
And IAecController.GetAecReadiness() returns READY
And IAecController.GetRecommendedParams() returns kVp=80, mA=200, ExposureTimeMs=25
When the recommended parameters are retrieved
Then the ExposureRecord for this exposure is updated with the AEC-recommended values
And the updated values are displayed to the operator before exposure confirmation
```

---

## AC-WF-04: Generator Control Orchestration (FR-WF-04)

### AC-WF-04-01: Generator Preparation Before Exposure
```
Given the Workflow Engine transitions to EXPOSURE_TRIGGER state
When the ExposureTriggerStateHandler entry action executes
Then IHvgDriver.SetExposureParameters(kVp, mA, ExposureTimeMs, FocusSize) is called first
Then IHvgDriver.ArmGenerator() is called
And the exposure command is issued only after HAL returns READY acknowledgment
```

### AC-WF-04-02: Interlock Check Immediately Before Trigger
```
Given the generator has been armed
When the exposure command is about to be issued
Then all nine interlocks (IL-01 through IL-09) are queried via ISafetyInterlock.CheckAllInterlocks()
And the exposure command is issued only if all nine return their required status simultaneously
```

### AC-WF-04-03: Exposure Trigger Latency
```
Given the Workflow Engine receives a valid exposure trigger event
When measured from event receipt to IHvgDriver.TriggerExposure() call
Then the elapsed time is at most 200 ms
```

### AC-WF-04-04: Acquisition Watchdog
```
Given the exposure command has been issued
When detector image data is not received within AcquisitionTimeout
Then the engine transitions to QC_REVIEW with status AcquisitionFailed
And a log entry with category HARDWARE is recorded
And the operator is notified of the acquisition timeout
```

### AC-WF-04-05: Generator Fault Blocks Exposure
```
Given ISafetyInterlock.CheckAllInterlocks() returns generator_ready = NOT_READY (fault condition)
When the exposure trigger is evaluated
Then the transition to EXPOSURE_TRIGGER is blocked
And the specific failed interlock (IL-04 Generator Ready) is communicated to the operator
And a journal entry with category SAFETY is recorded
```

---

## AC-WF-05: Multi-Exposure Study Support (FR-WF-05)

### AC-WF-05-01: Sequential Exposure Management
```
Given a study is defined with three planned exposures (e.g., Chest PA, Chest Lateral, Spine AP)
When the first image is accepted by the operator
Then the ExposureRecord for index 1 is marked Accepted
And the engine transitions to PROTOCOL_SELECT
And the patient and study context are preserved
And the operator is shown exposure 2 of 3 context
```

### AC-WF-05-02: Automatic MPPS_COMPLETE on Last Exposure
```
Given a study with three planned exposures
When the third (final) image is accepted by the operator
Then the engine transitions directly to MPPS_COMPLETE (not back to PROTOCOL_SELECT)
```

### AC-WF-05-03: Exposure Series Status Tracking
```
Given a study has progressed through multiple exposures with mixed outcomes
When the study is queried for its exposure series
Then each ExposureRecord correctly reflects its status: Accepted, Rejected, or Incomplete
```

---

## AC-WF-06: Reject/Retake Workflow (FR-WF-06)

### AC-WF-06-01: Reject Reason Required
```
Given the Workflow Engine is in QC_REVIEW state
When the operator initiates image rejection without providing a reject reason
Then the rejection is not processed
And the operator is prompted to select a structured reject reason
```

### AC-WF-06-02: Rejection Audit Record
```
Given the operator rejects an image with reason "Patient Motion"
When the rejection is confirmed
Then the WorkflowJournal contains an entry with:
  Category = WORKFLOW,
  Trigger = ImageRejected,
  Metadata.RejectReason = "PatientMotion",
  Metadata.ExposureIndex = (current index),
  Metadata.OperatorId = (current operator),
  Metadata.AdministeredDap = (dose value from DoseTracker)
```

### AC-WF-06-03: Retake Approval Path
```
Given the engine is in REJECT_RETAKE state
When the operator approves a retake
And hardware interlock check passes
Then the engine transitions to POSITION_AND_PREVIEW
And the generator is placed in standby ready mode via HAL
```

### AC-WF-06-04: Retake Cancellation Path
```
Given the engine is in REJECT_RETAKE state
When the operator cancels the retake
Then the engine transitions to MPPS_COMPLETE
And the ExposureRecord for the rejected view is marked Incomplete
```

### AC-WF-06-05: Rejected Dose Recorded
```
Given an exposure is rejected
When IDoseTracker.RecordRejected() is called
Then the dose for the rejected exposure is included in the cumulative study dose
And the final study dose report includes the rejected-exposure dose labeled separately
```

---

## AC-WF-07: Emergency Workflow (FR-WF-07)

### AC-WF-07-01: Emergency Entry Bypasses Worklist
```
Given the Workflow Engine is in IDLE state
When the operator activates the emergency workflow
Then the engine transitions directly to PATIENT_SELECT
Without performing any DICOM C-FIND worklist query
```

### AC-WF-07-02: Emergency Minimum Data Enforcement
```
Given the engine is in PATIENT_SELECT via emergency workflow
When the operator attempts to confirm a patient with missing PatientName or PatientID or BodyPart
Then the confirmation is rejected
And the operator is prompted to complete the minimum required fields
```

### AC-WF-07-03: IHE Unscheduled Workflow Tagging
```
Given a study was initiated via emergency workflow
When DICOM objects are generated (MPPS, images)
Then all objects contain the IHE Unscheduled Workflow flag
And the AccessionNumber is a locally generated placeholder value (not from a worklist)
And StudyContext.IsEmergency = true in the journal
```

---

## AC-WF-08: Study Completion and Cleanup (FR-WF-08)

### AC-WF-08-01: Hardware Released on Completion
```
Given the Workflow Engine transitions to IDLE after study completion
When the IDLE state entry action executes
Then IHvgDriver ArmGenerator is not active (generator standby confirmed)
And IDetector confirms detector is in standby mode
```

### AC-WF-08-02: PHI Cleared from Memory
```
Given a study has just completed and the engine is in IDLE
When the in-memory StudyContext is inspected
Then PatientID, PatientName, PatientBirthDate, and PatientSex fields are null or empty
And no PHI fields exist in any active in-memory data structure
```

### AC-WF-08-03: Study Completed Event Content
```
Given a study has completed with two accepted exposures, one rejected, total DAP = 15.3 cGy·cm²
When StudyCompletedEvent is published to the IPC bus
Then the event contains:
  StudyInstanceUID (non-empty),
  ExposureCount = 3,
  AcceptedCount = 2,
  RejectedCount = 1,
  TotalDap = 15.3 cGy·cm²,
  CompletedAt (UTC timestamp)
```

---

## AC-WF-09: Procedure Code Mapping (FR-WF-09)

### AC-WF-09-01: Procedure Code to Protocol Mapping
```
Given the mapping table contains: code "XR-CHEST-PA" → ProtocolID P-001 (Chest PA, confidence 1.0)
When a worklist item with RequestedProcedureCode "XR-CHEST-PA" is confirmed
Then ProcedureCodeMapper returns P-001
And PROTOCOL_SELECT pre-selects protocol P-001
```

### AC-WF-09-02: Unmapped Code Handling
```
Given the mapping table has no entry for procedure code "XR-KNEE-MERCHANT"
When a worklist item with that code is confirmed
Then PROTOCOL_SELECT presents the full unfiltered protocol list
And a log entry with category WORKFLOW records the unmapped code for configuration review
```

### AC-WF-09-03: N-to-1 Mapping
```
Given codes "XR-CHEST-AP" and "XR-CHEST-PORTABLE" both map to protocol P-002
When either code is received from the worklist
Then ProcedureCodeMapper returns P-002 for both
```

---

## AC-NFR-01: Atomic Logged Transitions (NFR-WF-01)

### AC-NFR-01-01: Journal Written Before Event Published
```
Given a state transition is applied
When measured by transaction ordering
Then the SQLite journal commit completes before WorkflowStateChangedEvent is dispatched to subscribers
Verifiable via: mock journal that blocks and observes event dispatch order
```

### AC-NFR-01-02: Journal Durability Under Fault
```
Given the journal write operation is in progress
When a simulated process crash occurs immediately after the journal write
Then on next startup, the journal entry is present and correct
And CrashRecoveryService correctly reads the last state from the journal
```

---

## AC-NFR-02: Crash Recovery (NFR-WF-02)

### AC-NFR-02-01: Recovery Detection on Startup
```
Given the journal contains an in-progress study state from a previous session
When the Workflow Engine process starts
Then CrashRecoveryService detects the incomplete session within 5 seconds
And presents the operator with a recovery options dialog before accepting new commands
```

### AC-NFR-02-02: No Automatic Hardware Commands During Recovery
```
Given a crash occurred during EXPOSURE_TRIGGER state
When CrashRecoveryService restores the session
Then no IHvgDriver, IDetector, or IAecController methods are called automatically
Until the operator explicitly approves recovery via the recovery dialog
```

### AC-NFR-02-03: Clean Start on Recovery Decline
```
Given the operator declines recovery
When the operator selects "Start New Session"
Then StudyContext is cleared
And MPPS N-SET DISCONTINUED is sent for the interrupted study
And the engine initializes to IDLE state cleanly
```

---

## AC-NFR-03: Invalid Transition Prevention (NFR-WF-03)

### AC-NFR-03-01: Exhaustive Invalid Transition Coverage
```
Given the Workflow Engine is in each of the 10 defined states
When all events not defined as valid triggers for that state are delivered
Then each results in a TransitionRejectedResult
And no state change occurs
And the engine processes subsequent valid events correctly
```

### AC-NFR-03-02: No Undefined States
```
Given any combination of valid and invalid events delivered in any order
When the engine state is inspected
Then it always holds a value from the WorkflowState enum (never null or undefined)
```

---

## AC-SAFETY: Hardware Interlock and Parameter Safety

### AC-SAFETY-01: All Nine Interlocks Required
```
Given interlocks IL-01 through IL-09 all report their required status via ISafetyInterlock.CheckAllInterlocks()
When the exposure trigger guard is evaluated
Then HardwareInterlockOk = true
And the transition to EXPOSURE_TRIGGER is permitted (subject to other guards)

For each interlock IL-01 through IL-09 individually:
Given that specific interlock reports a non-required status
And all other eight report required status
When the exposure trigger guard is evaluated
Then HardwareInterlockOk = false
And the transition is blocked with a message identifying the failed interlock
```

### AC-SAFETY-02: Parameter Limit Enforcement
```
Given DeviceSafetyLimits specifies MaxKvp = 150
When a protocol with kVp = 151 is saved
Then the save is rejected with error: "kVp 151 exceeds MaxKvp 150"

Given DeviceSafetyLimits specifies MaxKvp = 150
When a protocol with kVp = 150 is saved
Then the save is accepted (boundary value: at limit is valid)
```

### AC-SAFETY-03: Interlock Loss During EXPOSURE_TRIGGER
```
Given the Workflow Engine is in EXPOSURE_TRIGGER state with exposure in progress
When IL-01 (room door) transitions from CLOSED to OPEN
Then IHvgDriver.AbortExposure() is called immediately
And the engine transitions to QC_REVIEW with AcquisitionFailed status
And a journal entry with category SAFETY is recorded
```

### AC-SAFETY-04: Safety Log Category Traceability
```
Given any safety-related event occurs (interlock check, parameter validation, exposure command)
When the journal is queried for entries with Category = SAFETY
Then every interlock check result is present
And every parameter validation result is present
And every exposure command issuance is present
```

---

## Definition of Done

A requirement is considered complete when:

1. Implementation code exists for all behaviors described in the SPEC section.
2. All acceptance criteria for the requirement have passing automated test cases.
3. Safety-critical paths (`Safety/` namespace) have 100% branch coverage with no exemptions.
4. All other paths meet 85% minimum branch coverage.
5. No LSP errors or warnings in the `src/HnVue.Workflow/` package.
6. All journal and audit log fields are populated and validated in integration tests.
7. Peer code review completed with safety annotation checklist verified.
8. Test evidence (test run report) is archived and linked to this SPEC for IEC 62304 traceability.
