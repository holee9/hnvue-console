# SPEC-WORKFLOW-001: Implementation Plan

## TAG: SPEC-WORKFLOW-001

## Reference
- Specification: `spec.md`
- Acceptance Criteria: `acceptance.md`
- IEC 62304 Class C — all implementation artifacts require documented verification

---

## Primary Goal: Core State Machine and Safety Foundation

### Milestone 1.1 — State Machine Skeleton
- Define `WorkflowState` enum (S-00 through S-09 plus UNKNOWN)
- Implement `TransitionGuardMatrix` with compile-time-validated transition table
- Implement `WorkflowStateMachine` with exclusive-lock serialized executor
- Implement `TransitionResult` (Success / Rejected / Error) discriminated union
- Unit tests: all valid transitions accepted; all invalid transitions rejected

### Milestone 1.2 — Workflow Journal
- Design SQLite journal schema (WorkflowJournalEntry, see spec.md §7.4)
- Implement `SqliteWorkflowJournal` with write-ahead behavior
- Implement journal write-before-notify enforcement at FSM level
- Unit tests: journal durability under simulated I/O failure

### Milestone 1.3 — Safety Interlock Chain
- Implement `InterlockChecker` querying all nine interlocks via `ISafetyInterlock.CheckAllInterlocks()` (IL-01 through IL-09)
- Implement interlock timeout (10 ms) treated as OPEN
- Implement `ParameterSafetyValidator` for kVp, mA, ExposureTime, mAs, DAP
- Unit tests: each interlock failure individually blocks exposure; combined pass permits exposure

---

## Secondary Goal: Clinical Workflow Paths

### Milestone 2.1 — Normal Workflow Path (S-00 → S-08)
- Implement all 10 state handler classes (see spec.md §8.1 package structure)
- Implement `StudyContext` and `ExposureRecord` data models
- Implement `StudyContextManager` with PHI lifecycle management
- Implement T-01 through T-16 transitions with all guards
- Integration tests: end-to-end normal workflow with mock HAL and DICOM

### Milestone 2.2 — DICOM Integration Points
- Implement WORKLIST_SYNC state handler with C-FIND SCU (via SPEC-DICOM-001 interfaces)
- Implement MPPS N-CREATE at study start (PROTOCOL_SELECT entry)
- Implement MPPS N-SET COMPLETED at MPPS_COMPLETE
- Implement MPPS N-SET DISCONTINUED on study abort (T-19)
- Implement PACS_EXPORT state handler with C-STORE (via SPEC-DICOM-001)

### Milestone 2.3 — Protocol Management
- Implement `IProtocolRepository` with SQLite backing
- Implement CRUD operations for Protocol records
- Implement `ProcedureCodeMapper` for N-to-1 worklist code → protocol mapping
- Implement protocol validation against `DeviceSafetyLimits`
- Capacity test: load 500+ protocols, verify lookup latency <= 50 ms

### Milestone 2.4 — Generator and AEC Orchestration
- Implement `ExposureTriggerStateHandler` with HAL HVG preparation sequence
- Implement acquisition watchdog timer
- Implement AEC integration: parameter transmission, readiness confirmation, recommended parameter retrieval
- Latency tests: measure and assert exposure trigger latency <= 200 ms

---

## Final Goal: Resilience and Ancillary Workflows

### Milestone 3.1 — Crash Recovery
- Implement `CrashRecoveryService` with journal replay on startup
- Implement `RecoveryOptions` operator confirmation dialog integration via IPC
- Requirement: recovery state restoration within 5 seconds of process start
- Test: simulate crash at each state, verify correct recovery state and no automatic hardware commands

### Milestone 3.2 — Reject/Retake Workflow
- Implement `RejectRetakeStateHandler` with structured reason collection
- Implement dose recording for rejected exposures via `IDoseTracker`
- Implement retake approval (T-13) and retake cancellation (T-14) transitions

### Milestone 3.3 — Emergency Workflow
- Implement emergency workflow entry (T-02) from IDLE
- Implement IHE Unscheduled Workflow DICOM tagging
- Implement worklist reconciliation operation for emergency studies

### Milestone 3.4 — Multi-Exposure Study Support
- Implement ordered exposure series list management
- Implement PROTOCOL_SELECT re-entry after accepted exposure (T-11) when more exposures remain
- Implement automatic MPPS_COMPLETE trigger (T-10) when all exposures complete

### Milestone 3.5 — Dose Integration and Study Completion
- Implement `DoseTrackingCoordinator` integrating with `IDoseTracker` at each exposure lifecycle event
- Implement study completion event (`StudyCompletedEvent`) with full dose summary
- Implement PHI clearing at study completion

---

## Technical Approach

### State Machine Pattern
The FSM uses the State pattern with a dedicated serialized channel (e.g., `Channel<TransitionRequest>`) rather than locks, to avoid deadlock risk in async environments. Each `IStateHandler` is responsible for entry actions and transition guard participation. The `WorkflowStateMachine` orchestrates handler selection and journal writing.

### Safety-Critical Code Conventions
All methods in `Safety/` namespace are annotated with `[SafetyCritical]` (custom attribute) for traceability during code review. These methods have 100% branch coverage requirement with no coverage exemptions. All safety checks use explicit null checks and return-value validation rather than relying on exception paths.

### Dependency Injection
All external interfaces (`IHvgDriver`, `IDetector`, `ISafetyInterlock`, `IAecController`, `IDoseTracker`, `IImagingPipeline`, DICOM service interfaces) are registered via .NET `IServiceCollection`. No static or singleton anti-patterns in safety-critical paths.

### Persistence
- Journal: SQLite via `Microsoft.Data.Sqlite`, WAL mode enabled
- Protocol database: SQLite, separate database file, read-optimized indexes on (BodyPart, Projection, DeviceModel)
- No cloud or network persistence for safety-critical state

---

## Risks and Mitigations

| Risk                                             | Likelihood | Impact   | Mitigation                                                       |
|--------------------------------------------------|------------|----------|------------------------------------------------------------------|
| HAL latency exceeds 10 ms interlock query budget  | Low        | Critical | Validate HAL response times in HAL integration tests; add timeout |
| Exposure trigger latency regression              | Medium     | Critical | Continuous latency measurement with CI alert on threshold breach  |
| SQLite journal write blocking main executor      | Medium     | High     | Use async I/O with WAL mode; benchmark under load                |
| Protocol mapping unmapped codes in production    | Medium     | Medium   | Log all unmapped codes; provide configuration UI review dashboard |
| Crash recovery leaving hardware in armed state   | Low        | Critical | Recovery service always sends hardware-standby before presenting options |

---

## Dependencies on Other SPECs

| SPEC            | Dependency Type     | Blocking Milestone |
|-----------------|---------------------|--------------------|
| SPEC-HAL-001    | Interface contract  | Milestone 1.3, 2.4 |
| SPEC-DICOM-001  | Interface contract  | Milestone 2.2      |
| SPEC-DOSE-001   | Interface contract  | Milestone 3.2, 3.5 |
| SPEC-IMAGING-001| Interface contract  | Milestone 2.1      |
| SPEC-IPC-001    | Interface contract  | Milestone 2.1, 3.5 |
