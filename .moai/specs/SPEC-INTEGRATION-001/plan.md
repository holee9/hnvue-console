# SPEC-INTEGRATION-001: Integration Test Plan

## Metadata

| Field          | Value                                              |
|----------------|----------------------------------------------------|
| SPEC ID        | SPEC-INTEGRATION-001                                |
| Title          | HnVue Integration Test Plan                         |
| Product        | HnVue - Diagnostic Medical Device X-ray GUI Console SW |
| Status         | Draft                                              |
| Priority       | High                                               |
| Safety Class   | IEC 62304 Class B/C                                |
| Created        | 2026-03-13                                         |
| Version        | 1.0.0                                              |

---

## 1. Executive Summary

### 1.1 Purpose

This document defines a comprehensive integration test strategy for validating end-to-end workflows across the 10 completed SPEC components of HnVue Console. The integration tests verify that independently developed components function correctly when integrated into the complete system.

### 1.2 Scope

**In Scope:**
- Cross-component workflow validation
- End-to-end clinical scenarios
- System integration points
- Data flow across SPEC boundaries
- Error handling and recovery

**Out of Scope:**
- Unit tests (covered by individual SPECs)
- Performance/load testing (separate SPEC)
- Security penetration testing (covered by SPEC-SECURITY-001)

---

## 2. SPEC Interdependency Analysis

### 2.1 Component Interaction Matrix

| Component | Workflow | IPC | DICOM | Dose | UI | Security |
|-----------|----------|-----|-------|------|-----|----------|
| **SPEC-WORKFLOW-001** | Core Orchestrator | ★★★ | ★★ | ★★★ | ★★★ | ★★ |
| **SPEC-IPC-001** | Communication Layer | — | ★ | — | ★★ | ★ |
| **SPEC-DICOM-001** | External Systems | — | — | ★★ | ★ | ★ |
| **SPEC-DOSE-001** | Dose Tracking | ★★★ | ★★ | — | ★★★ | ★ |
| **SPEC-UI-001** | User Interface | ★★★ | ★ | ★★ | — | ★★★ |
| **SPEC-SECURITY-001** | Cross-cutting | ★★★ | ★ | ★★★ | ★★★ | — |
| **SPEC-HAL-001** | Hardware Abstraction | ★ | — | ★ | — | — |
| **SPEC-IMAGING-001** | Image Processing | ★ | — | — | ★★ | — |

**Legend:**
- ★★★: Critical dependency (primary integration point)
- ★★: Important dependency (secondary integration point)
- ★: Optional dependency (tertiary integration point)
- —: No direct dependency

### 2.2 Critical Integration Points

#### 2.2.1 Workflow Engine + Dose Management (P0)
**Scenario:** Clinical workflows with dose tracking
- **Components:** SPEC-WORKFLOW-001 + SPEC-DOSE-001
- **Integration Point:** Workflow orchestrates exposure → Dose records parameters → Dose calculates DAP → Workflow updates cumulative dose
- **Risk:** Cumulative dose miscalculation, missing dose records, race conditions

#### 2.2.2 Workflow Engine + DICOM (P0)
**Scenario:** MPPS and Worklist integration
- **Components:** SPEC-WORKFLOW-001 + SPEC-DICOM-001
- **Integration Point:** Workflow fetches worklist → Workflow creates MPPS on exposure start → Workflow updates MPPS on completion
- **Risk:** MPPS state desynchronization, worklist data mismatch, DICOM association failures

#### 2.2.3 UI + Workflow + gRPC IPC (P0)
**Scenario:** User actions triggering backend operations
- **Components:** SPEC-UI-001 + SPEC-WORKFLOW-001 + SPEC-IPC-001
- **Integration Point:** UI command → ViewModel calls Service → Service uses gRPC client → Core Engine processes via IPC → Response propagates back
- **Risk:** Command timeout, UI freeze, data serialization loss, connection failure

#### 2.2.4 Security + All Components (P0)
**Scenario:** Authentication/authorization for all operations
- **Components:** SPEC-SECURITY-001 + All SPECs
- **Integration Point:** UserService validates session → Service layer checks permissions → AuditLogService records events
- **Risk:** Unauthorized access, privilege escalation, audit log gaps

---

## 3. Integration Test Scenarios

### 3.1 Priority Classification

| Priority | Description | Success Criteria |
|----------|-------------|-------------------|
| **P0 (Critical)** | Patient safety workflows, mandatory regulatory compliance | 100% pass rate required for release |
| **P1 (High)** | Core clinical workflows, data integrity | 95%+ pass rate |
| **P2 (Medium)** | Edge cases, error recovery, non-critical features | 90%+ pass rate |
| **P3 (Low)** | Nice-to-have features, optimizations | 80%+ pass rate |

### 3.2 Clinical Workflow Scenarios (P0)

#### Scenario INT-001: Complete Patient Examination Workflow

**Description:** End-to-end patient examination from worklist selection to PACS archival

**Prerequisites:**
- Orthanc DICOM simulator running (worklist + PACS)
- User authenticated as TECHNOLOGIST
- Core Engine process running
- Test patient registered in worklist

**Test Steps:**
1. **Login & Authentication (SPEC-SECURITY-001)**
   - User logs in with valid credentials
   - Verify session creation and RBAC permissions
   - Confirm audit log entry: `USER_LOGIN`

2. **Worklist Query (SPEC-DICOM-001 + SPEC-UI-001)**
   - UI fetches worklist via WorklistService
   - WorklistService calls DICOM C-FIND
   - Verify worklist items displayed in WorklistView
   - Confirm audit log entry: `WORKLIST_QUERY`

3. **Patient Selection (SPEC-WORKFLOW-001 + SPEC-UI-001)**
   - User selects worklist item
   - Workflow creates session context
   - Verify patient data populated in UI

4. **Protocol Selection (SPEC-WORKFLOW-001 + SPEC-UI-001)**
   - User selects examination protocol (body part, projection)
   - Workflow validates protocol against user permissions
   - Verify exposure parameters loaded

5. **Exposure Preparation (SPEC-WORKFLOW-001 + SPEC-IPC-001)**
   - UI sends StartExposure command via gRPC
   - Verify command round-trip <10ms (NFR-IPC-02)
   - Core Engine acknowledges readiness

6. **Exposure Execution (SPEC-DOSE-001 + SPEC-WORKFLOW-001)**
   - Core Engine triggers exposure
   - Dose service records parameters (kVp, mAs, filtration)
   - Dose service calculates DAP within 1 second (NFR-DOSE-01)
   - Workflow creates MPPS IN_PROGRESS (SPEC-DICOM-001)

7. **Image Acquisition (SPEC-IMAGING-001 + SPEC-IPC-001)**
   - Core Engine streams image via gRPC ImageService
   - Verify image transfer <50ms (NFR-IPC-01)
   - UI receives and displays image

8. **Dose Display (SPEC-DOSE-001 + SPEC-UI-001)**
   - DoseDisplayNotifier publishes dose update
   - UI displays current DAP and cumulative study DAP
   - Verify display updates within 1 second (FR-DOSE-04)

9. **MPPS Completion (SPEC-DICOM-001 + SPEC-WORKFLOW-001)**
   - Workflow sends MPPS N-SET (COMPLETED)
   - Verify MPPS includes series/image references
   - Confirm audit log entry: `MPPS_COMPLETED`

10. **DICOM Storage (SPEC-DICOM-001 + SPEC-IMAGING-001)**
    - ImageService C-STOREs image to PACS
    - Verify storage commitment success
    - Confirm audit log entry: `IMAGE_STORED`

11. **RDSR Generation (SPEC-DOSE-001 + SPEC-DICOM-001)**
    - Dose service compiles RDSR document
    - RDSR C-STOREd to PACS
    - Verify RDSR contains all mandatory TID 10001/10003 items

**Success Criteria:**
- All 11 steps complete without errors
- Dose calculation accuracy within ±5% (NFR-DOSE-03)
- Image data integrity verified (pixel-perfect)
- Audit trail complete with hash chain integrity (NFR-DOSE-04-D)
- No race conditions or deadlocks detected
- Total workflow time <60 seconds (excluding operator actions)

**Test Data:**
- Test patient: `PATIENT-TEST-001`
- Accession number: `ACC-TEST-20260313-001`
- Protocol: `CHEST_PA` (Posteroanterior chest)
- Exposure parameters: kVp=120, mAs=5, SID=180cm

**Automation Strategy:** Fully automated E2E test using xUnit with Testcontainers for Orthanc

---

#### Scenario INT-002: DRL Alerting Workflow

**Description:** Verify dose reference level comparison and alerting

**Prerequisites:**
- DRL configured for CHEST_PA protocol: 50 mGy·cm²
- User authenticated as RADIOLOGIST
- Active study with cumulative dose approaching DRL

**Test Steps:**
1. **Initial Exposure (Below DRL)**
   - Execute exposure with DAP=30 mGy·cm²
   - Verify no alert triggered
   - Confirm cumulative dose displayed correctly

2. **Subsequent Exposure (Exceeds DRL)**
   - Execute exposure with DAP=25 mGy·cm²
   - Cumulative dose: 55 mGy·cm² (exceeds DRL of 50)
   - Verify DRL alert triggered in UI
   - Confirm audit log entry: `DRL_EXCEEDED`

3. **Alert Persistence**
   - Verify alert visible in UI until study closed
   - Confirm alert not cleared on new exposure

4. **Alert Acknowledgment**
   - User acknowledges alert
   - Verify alert dismissed but logged

**Success Criteria:**
- Alert triggers only when cumulative dose exceeds DRL (not per-exposure)
- Alert does not block exposure workflow (FR-DOSE-05-D)
- Audit trail records DRL exceedance with timestamp
- Multiple alerts do not duplicate for same study

**Test Data:**
- Protocol: `CHEST_PA`
- DRL threshold: 50 mGy·cm²
- Exposure 1: 30 mGy·cm²
- Exposure 2: 25 mGy·cm²
- Cumulative: 55 mGy·cm²

**Automation Strategy:** Automated test with mocked dose calculator returning specific DAP values

---

### 3.3 Security Integration Scenarios (P0)

#### Scenario INT-003: Role-Based Access Control (RBAC) Enforcement

**Description:** Verify that user roles correctly restrict access to functionality

**Prerequisites:**
- Users created for each role (ADMINISTRATOR, RADIOLOGIST, TECHNOLOGIST, PHYSICIST, OPERATOR, VIEWER, SERVICE)
- Authentication service operational

**Test Steps:**
1. **ADMINISTRATOR Role**
   - Login as ADMINISTRATOR
   - Verify access to ConfigurationView (user management, system settings)
   - Verify access to AuditLogView
   - Verify can execute calibration commands

2. **RADIOLOGIST Role**
   - Login as RADIOLOGIST
   - Verify access to PatientView, WorklistView, ImageReviewView
   - Verify CAN sign reports (if implemented)
   - Verify DENIED access to ConfigurationView
   - Confirm audit log entry: `ACCESS_DENIED`

3. **TECHNOLOGIST Role**
   - Login as TECHNOLOGIST
   - Verify access to AcquisitionView (exposure control)
   - Verify access to PatientView, WorklistView
   - Verify DENIED access to system configuration
   - Verify DENIED access to report signing

4. **PHYSICIST Role**
   - Login as PHYSICIST
   - Verify access to QC functions
   - Verify access to exposure parameter configuration
   - Verify DENIED access to user management

5. **VIEWER Role**
   - Login as VIEWER
   - Verify READ-ONLY access to ImageReviewView
   - Verify DENIED access to AcquisitionView
   - Verify DENIED access to configuration

6. **Unauthorized Command Attempt**
   - As TECHNOLOGIST, attempt to execute calibration command (requires PHYSICIST/ADMIN)
   - Verify command rejected at Service layer
   - Verify audit log entry: `ACCESS_DENIED`

**Success Criteria:**
- All role permissions correctly enforced across UI and Service layers
- No privilege escalation possible
- All denied accesses logged in audit trail
- Session management enforces 30-minute timeout (FR-SEC-02)

**Test Data:**
- Test users per role with known credentials
- Role permissions matrix from SPEC-SECURITY-001

**Automation Strategy:** Parameterized test iterating over all role-action combinations

---

#### Scenario INT-004: Audit Trail Integrity

**Description:** Verify audit trail hash chain and tamper evidence

**Prerequisites:**
- AuditLogService operational
- Audit trail storage configured (file or database)

**Test Steps:**
1. **Generate Audit Events**
   - Execute 100 diverse actions (login, exposure, config change, access denied)
   - Verify each event generates audit log entry

2. **Verify Hash Chain**
   - Call AuditTrailWriter.VerifyIntegrity()
   - Confirm all hashes form valid chain
   - Verify first record uses well-known initialization vector

3. **Tamper Detection**
   - Manually corrupt a single audit record (modify timestamp)
   - Call AuditTrailWriter.VerifyIntegrity()
   - Verify corruption detected at broken link
   - Verify first corrupted record identified

4. **Audit Event Coverage**
   - Verify all event types from FR-SEC-09 are logged
   - Verify PHI masking for Patient ID and Patient Name (FR-SEC-10)
   - Verify UTC timestamps with millisecond precision

**Success Criteria:**
- Hash chain integrity verification passes for unmodified audit trail
- Any corruption detected with exact location of broken link
- All mandatory event types logged
- PHI correctly masked in non-DEBUG logs
- 6-year retention policy enforced (FR-SEC-07)

**Test Data:**
- 100 test events covering all event types
- Known corrupted audit trail for tamper detection test

**Automation Strategy:** Automated test with in-memory audit storage for speed

---

### 3.4 Error Handling and Recovery Scenarios (P1)

#### Scenario INT-005: PACS Communication Failure

**Description:** Verify graceful handling of DICOM network failures

**Prerequisites:**
- PACS simulator configured
- Network disruption capability (firewall or simulator control)

**Test Steps:**
1. **Normal Operation**
   - Execute exposure and C-STORE to PACS
   - Verify success and storage commitment

2. **Simulate PACS Unreachable**
   - Block network to PACS (firewall or stop simulator)
   - Execute exposure
   - Verify C-STORE fails gracefully
   - Verify image enqueued in retry queue (FR-DICOM-08)

3. **Retry Behavior**
   - Verify exponential back-off retry: 30s, 60s, 120s, 240s, 600s
   - Confirm max 5 retry attempts (default)

4. **PACS Recovery**
   - Restore network connectivity
   - Verify retry queue processes and succeeds
   - Verify storage commitment completed

5. **Max Retries Exceeded**
   - With PACS unreachable, wait for max retries
   - Verify item transitions to FAILED terminal state
   - Verify operator notified
   - Verify item preserved for manual recovery

**Success Criteria:**
- No data loss on network failure
- Retry queue persists across application restarts
- Operator notified of terminal failures
- No indefinite blocking of UI

**Test Data:**
- PACS endpoint: `orthanc:11112`
- Retry configuration: max=5, initial=30s, multiplier=2.0, max_interval=3600s

**Automation Strategy:** Integration test with Docker-controlled Orthanc lifecycle

---

#### Scenario INT-006: gRPC IPC Connection Failure

**Description:** Verify reconnection behavior when Core Engine disconnects

**Prerequisites:**
- Core Engine process running
- gRPC client operational

**Test Steps:**
1. **Normal Connection**
   - Verify GUI connected to Core Engine
   - Execute command and verify response

2. **Simulate Core Engine Crash**
   - Kill Core Engine process (simulate crash)
   - Verify GUI detects lost connection (heartbeat timeout >3s)
   - Verify GUI transitions to DISCONNECTED state

3. **Automatic Reconnection**
   - Restart Core Engine process
   - Verify GUI initiates reconnection
   - Verify exponential back-off: 500ms, 1s, 2s, 4s, 30s (max)
   - Confirm connection restored

4. **State Restoration**
   - After reconnection, verify configuration sync (FR-IPC-07a)
   - Verify health monitoring resumes
   - Verify no data loss during disconnection

**Success Criteria:**
- GUI remains responsive during Core Engine disconnection
- Reconnection automatic without operator intervention
- No GUI crash or hang
- Graceful degradation (show "disconnected" status)

**Test Data:**
- gRPC endpoint: `localhost:50051`
- Heartbeat interval: 1000ms
- Heartbeat timeout: 3000ms

**Automation Strategy:** Integration test with process lifecycle management

---

### 3.5 Data Flow Scenarios (P1)

#### Scenario INT-007: DICOM Worklist to MPPS Data Consistency

**Description:** Verify data consistency from worklist selection through MPPS reporting

**Prerequisites:**
- Worklist SCP with scheduled procedures
- MPPS SCP operational

**Test Steps:**
1. **Fetch Worklist**
   - Query worklist for scheduled date
   - Retrieve worklist item with:
     - Patient ID, Patient Name, Accession Number
     - Scheduled Procedure Step ID
     - Requested Procedure ID
     - Study Instance UID

2. **Select Worklist Item**
   - User selects procedure from worklist
   - Workflow creates session context with worklist data

3. **Create MPPS IN_PROGRESS**
   - On exposure start, send MPPS N-CREATE
   - Verify MPPS contains:
     - Matching Patient ID, Accession Number
     - Scheduled Step Attributes Sequence from worklist
     - Performed Procedure Step Start Date/Time

4. **Complete MPPS**
   - On exposure completion, send MPPS N-SET (COMPLETED)
   - Verify MPPS contains:
     - Series and Image references
     - Radiation Dose (if available from SPEC-DOSE-001)

5. **Verify Consistency**
   - Compare MPPS data with original worklist data
   - Verify no data corruption or truncation
   - Verify UID consistency throughout

**Success Criteria:**
- All worklist data correctly propagated to MPPS
- UID consistency (Study Instance UID preserved)
- No data loss in Scheduled Step Attributes Sequence
- MPPS state transitions correct: IN_PROGRESS → COMPLETED

**Test Data:**
- Worklist entry with complete demographics
- Known UIDs for verification

**Automation Strategy:** Integration test with DICOM simulators

---

#### Scenario INT-008: Image Data Integrity Through Pipeline

**Description:** Verify image data integrity from acquisition to display to storage

**Prerequisites:**
- Core Engine capable of generating test image
- PACS simulator for storage

**Test Steps:**
1. **Generate Test Image**
   - Core Engine generates 16-bit grayscale test pattern
   - Known dimensions and pixel values for verification

2. **Transfer via gRPC IPC**
   - Stream image chunks from Core Engine to GUI
   - Verify transfer time <50ms (NFR-IPC-01)
   - Verify all chunks received (sequence number validation)

3. **Reconstruct Image in GUI**
   - Assemble chunks into complete image
   - Verify pixel-perfect match with source (checksum verification)

4. **Display in UI**
   - Render image in ImageReviewView
   - Verify visual rendering correct (no artifacts)

5. **Encode DICOM**
   - Encode image as DX IOD DICOM object
   - Verify transfer syntax negotiation (JPEG 2000 Lossless preferred)

6. **C-STORE to PACS**
   - Transmit DICOM object to PACS
   - Verify storage commitment success

7. **Retrieve and Verify**
   - C-MOVE image back from PACS
   - Decode and verify pixel-perfect match with original

**Success Criteria:**
- Zero pixel data corruption throughout pipeline
- Checksum match at each stage
- Transfer latency targets met
- DICOM encoding/decoding lossless

**Test Data:**
- Test image: 2048x2048 16-bit grayscale
- Known checksum: SHA-256 hash of pixel data

**Automation Strategy:** Fully automated pipeline test

---

### 3.6 Concurrency and Race Condition Scenarios (P2)

#### Scenario INT-009: Concurrent Exposure and Dose Recording

**Description:** Verify thread safety when multiple exposures occur in rapid succession

**Prerequisites:**
- System configured for rapid sequential exposures

**Test Steps:**
1. **Rapid Sequential Exposures**
   - Execute 10 exposures with 100ms interval
   - Verify dose recording thread-safe
   - Verify no dose records lost

2. **Cumulative Dose Accuracy**
   - Verify cumulative dose updated correctly
   - Verify no race conditions in accumulator

3. **RDSR Generation**
   - Generate RDSR after rapid exposures
   - Verify all 10 exposures included
   - Verify correct order (by timestamp)

**Success Criteria:**
- All exposures recorded
- No lost dose records
- Cumulative dose mathematically correct
- RDSR contains all irradiation events

**Test Data:**
- 10 exposures with identical parameters
- Known expected cumulative dose

**Automation Strategy:** Automated test with threading simulation

---

#### Scenario INT-010: Concurrent User Sessions

**Description:** Verify system behavior with multiple simultaneous users

**Prerequisites:**
- Multi-user authentication configured
- Test environment supports concurrent sessions

**Test Steps:**
1. **Create Multiple Sessions**
   - Login 5 users simultaneously
   - Verify session isolation

2. **Concurrent Operations**
   - Each user executes different operations:
     - User 1: Patient registration
     - User 2: Worklist query
     - User 3: Exposure execution
     - User 4: Image review
     - User 5: Configuration change

3. **Verify Isolation**
   - Verify no cross-session data leakage
   - Verify audit trails correctly tagged by user
   - Verify no deadlocks

**Success Criteria:**
- All sessions operate independently
- No performance degradation beyond acceptable limits
- Audit trails correctly attributed
- No session corruption

**Test Data:**
- 5 distinct user accounts with different roles

**Automation Strategy:** Multi-threaded integration test

---

## 4. Test Data Management

### 4.1 Test Data Strategy

**Principles:**
- Isolated test data per test run (no shared state)
- Deterministic test data (reproducible results)
- Realistic data volume (production-like)
- Privacy-compliant (synthetic PHI only)

**Test Data Categories:**

| Category | Source | Refresh Rate |
|----------|--------|--------------|
| Patient Demographics | Synthetic generator | Per test run |
| Worklist Entries | DICOM MWL simulator | Per test run |
| Images | Synthetic test patterns | Static |
| Configuration | Test configuration files | Per test suite |
| User Accounts | Seed script | Daily |

### 4.2 Test Fixtures

**DICOM Test Data:**
- Worklist entries with varying modalities, body regions
- Prior studies for Query/Retrieve testing
- MPPS SCP with N-CREATE/N-SET support

**PACS Test Data:**
- Empty repository at test start
- Configured storage commitment support
- Configured Query/Retrieve (C-FIND/C-MOVE)

**Dose Test Data:**
- Known exposure parameters with expected DAP values
- DRL thresholds per protocol
- Calibration coefficients for dose calculation

### 4.3 Mock Data Generation

**Patient Generator:**
```csharp
// Synthetic patient generation
string patientId = $"PAT-TEST-{Guid.NewGuid():N}";
string patientName = $"TEST^PATIENT^{Random.Next(1000, 9999)}";
DateTime dob = DateTime.Now.AddYears(-Random.Next(18, 90));
```

**Worklist Entry Generator:**
```csharp
// Synthetic worklist generation
var worklistItem = new WorklistEntry
{
    PatientId = patientId,
    AccessionNumber = $"ACC-TEST-{DateTime.Now:yyyyMMdd}-{Random.Next(1000, 9999)}",
    ScheduledProcedureStepId = Guid.NewGuid().ToString(),
    StudyInstanceUid = GenerateDicomUid(),
    // ... other required attributes
};
```

---

## 5. Test Framework Selection

### 5.1 Recommended Framework: xUnit + Testcontainers + Playwright

**Rationale:**

| Layer | Framework | Justification |
|-------|-----------|---------------|
| Test Execution | xUnit 2.6+ | Already used in project, excellent async support, parallel test execution |
| DICOM/PACS Simulation | Testcontainers (Docker Orthanc) | Isolated test environment, reproducible, easy setup/teardown |
| E2E UI Testing | Playwright | Cross-browser support, auto-waiting, excellent debugging |
| gRPC Mocking | gRPC Server In-Process | Fast, no external dependencies, full control |
| Test Data | Bogus (F# Data Generator) | Realistic fake data, fluent API |

**Alternative Considered:** SpecFlow (BDD)
- **Pros:** Human-readable scenarios, business stakeholder involvement
- **Cons:** Additional abstraction layer, slower execution, learning curve
- **Verdict:** Not recommended for HnVue at this stage (team familiar with xUnit)

### 5.2 Test Project Structure

```
tests/integration/
├── HnVue.Integration.Tests/
│   ├── ClinicalWorkflows/
│   │   ├── CompleteExaminationWorkflowTests.cs      # INT-001
│   │   ├── DrlAlertingWorkflowTests.cs               # INT-002
│   │   └── WorklistToMppsConsistencyTests.cs         # INT-007
│   ├── Security/
│   │   ├── RbacEnforcementTests.cs                   # INT-003
│   │   └── AuditTrailIntegrityTests.cs               # INT-004
│   ├── ErrorHandling/
│   │   ├── PacsFailureTests.cs                       # INT-005
│   │   └── IpcFailureTests.cs                        # INT-006
│   ├── DataFlow/
│   │   ├── ImageIntegrityTests.cs                    # INT-008
│   │   └── DoseRecordingTests.cs                    # INT-009
│   ├── Concurrency/
│   │   ├── ConcurrentExposureTests.cs                # INT-009
│   │   └── ConcurrentSessionTests.cs                 # INT-010
│   ├── Fixtures/
│   │   ├── OrthancFixture.cs                         # DICOM simulator lifecycle
│   │   ├── CoreEngineFixture.cs                      # gRPC server lifecycle
│   │   ├── AuthenticationFixture.cs                  # User session management
│   │   └── PatientDataFixture.cs                    # Synthetic patient generator
│   ├── Helpers/
│   │   ├── DicomAssert.cs                            # DICOM-specific assertions
│   │   ├── DoseAssert.cs                             # Dose calculation assertions
│   │   └── AuditTrailVerifier.cs                     # Hash chain verification
│   └── TestData/
│       ├── WorklistSamples.json
│       ├── DrlConfiguration.json
│       └── CalibrationData.json
```

### 5.3 Test Configuration

**appsettings.IntegrationTests.json:**
```json
{
  "Dicom": {
    "WorklistScp": {
      "AeTitle": "ORTHANC-WL",
      "Host": "localhost",
      "Port": 11114
    },
    "PacsScp": {
      "AeTitle": "ORTHANC-PACS",
      "Host": "localhost",
      "Port": 11112
    },
    "MppsScp": {
      "AeTitle": "ORTHANC-MPPS",
      "Host": "localhost",
      "Port": 11116
    }
  },
  "Ipc": {
    "CoreEngineHost": "localhost",
    "CoreEnginePort": 50051
  },
  "Test": {
    "ParallelExecution": true,
    "MaxDegreeOfParallelism": 4,
    "TestTimeoutSeconds": 300
  }
}
```

---

## 6. Test Isolation Strategy

### 6.1 Isolation Levels

| Level | Scope | Isolation Mechanism |
|-------|-------|-------------------|
| **Test Method** | Individual test | Fresh fixtures per test, deterministic data |
| **Test Class** | Related tests | Shared fixture setup/teardown |
| **Test Suite** | Entire integration suite | Separate Docker network per suite run |

### 6.2 Fixture Lifecycle

**OrthancFixture (DICOM Simulator):**
- **Setup:** Start Docker container, load test data, verify C-ECHO
- **Teardown:** Stop container, remove volumes
- **Scope:** Per test class (shared across tests in class)

**CoreEngineFixture (gRPC Server):**
- **Setup:** Launch in-process gRPC server with mock implementations
- **Teardown:** Graceful shutdown
- **Scope:** Per test method (complete isolation)

**AuthenticationFixture:**
- **Setup:** Create test users, reset audit log
- **Teardown:** Cleanup test users
- **Scope:** Per test class

### 6.3 Database Isolation (if applicable)

- **Strategy:** Transaction rollback after each test
- **Alternative:** Separate database per test run (Docker)
- **Audit Log:** In-memory implementation for test speed

---

## 7. Automation Strategy

### 7.1 Automation Priority Matrix

| Scenario ID | Scenario Name | Automation Priority | Automation Effort | Automation Timeline |
|-------------|---------------|---------------------|------------------|---------------------|
| INT-001 | Complete Examination Workflow | P0 | High (80 hours) | Phase 1 (Week 1-2) |
| INT-002 | DRL Alerting Workflow | P0 | Medium (40 hours) | Phase 1 (Week 1-2) |
| INT-003 | RBAC Enforcement | P0 | Medium (40 hours) | Phase 1 (Week 2) |
| INT-004 | Audit Trail Integrity | P0 | Low (20 hours) | Phase 1 (Week 2) |
| INT-005 | PACS Communication Failure | P1 | Medium (40 hours) | Phase 2 (Week 3) |
| INT-006 | gRPC IPC Connection Failure | P1 | Medium (40 hours) | Phase 2 (Week 3) |
| INT-007 | Worklist to MPPS Consistency | P1 | High (60 hours) | Phase 2 (Week 3-4) |
| INT-008 | Image Data Integrity | P1 | High (60 hours) | Phase 2 (Week 4) |
| INT-009 | Concurrent Exposure | P2 | Medium (40 hours) | Phase 3 (Week 5) |
| INT-010 | Concurrent User Sessions | P2 | Medium (40 hours) | Phase 3 (Week 5) |

**Total Automation Effort:** ~460 hours (12 weeks at 40 hours/week)

### 7.2 Manual vs. Automated Tests

| Test Type | Automation Approach | Rationale |
|-----------|---------------------|-----------|
| **Clinical Workflows (P0)** | Fully automated | Regression protection, CI/CD integration |
| **Security Tests (P0)** | Fully automated | Must run on every build |
| **Error Recovery (P1)** | Fully automated | Deterministic failure simulation |
| **Data Flow (P1)** | Fully automated | Data integrity verification |
| **Concurrency (P2)** | Semi-automated | Tool-assisted manual testing for race conditions |
| **UI Usability** | Manual | Subjective assessment, requires human judgment |

### 7.3 CI/CD Integration

**Pipeline Stages:**

```yaml
# GitHub Actions workflow example
stages:
  - name: Unit Tests
    run: dotnet test tests/csharp/* --filter "Category=Unit"

  - name: Integration Tests
    run: |
      docker-compose -f docker-compose.test.yml up -d
      dotnet test tests/integration/HnVue.Integration.Tests
      docker-compose -f docker-compose.test.yml down
    if: github.event_name == 'push' || github.event_name == 'pull_request'

  - name: E2E Tests
    run: |
      docker-compose -f docker-compose.e2e.yml up -d
      dotnet test tests/e2e/HnVue.Console.E2E.Tests
      docker-compose -f docker-compose.e2e.yml down
    if: github.ref == 'refs/heads/main'
```

**Quality Gates:**
- Unit tests: 100% pass rate
- Integration tests: 100% P0, 95%+ P1
- E2E tests: 100% P0
- Code coverage: 85%+ (unit), 70%+ (integration)

---

## 8. Success Criteria and Acceptance

### 8.1 Test Completion Criteria

| Priority | Pass Rate Requirement | Blocking Issues |
|----------|----------------------|-----------------|
| **P0** | 100% (all tests must pass) | Zero tolerance |
| **P1** | 95%+ | Documented exceptions required for <100% |
| **P2** | 90%+ | Documented exceptions required for <95% |
| **P3** | 80%+ | Nice-to-have, not blocking |

### 8.2 Definition of Done

A test scenario is considered "Done" when:
1. **Test Implemented:** Code written and passing locally
2. **Test Automated:** Integrated into CI/CD pipeline
3. **Test Documented:** Test purpose, steps, expected outcomes documented
4. **Test Reviewed:** Peer review completed
5. **Test Stable:** No flaky behavior across 10 consecutive runs
6. **Maintainable:** Clear test data strategy, fixture lifecycle documented

### 8.3 Exit Criteria

Integration testing phase is complete when:
- All P0 scenarios implemented and passing (100%)
- All P1 scenarios implemented and passing (95%+)
- Integration test suite executes in <30 minutes
- Test stability achieved (<1% flaky test rate)
- CI/CD integration verified
- Test documentation complete
- Known issues documented with severity and workarounds

---

## 9. Risk Assessment

### 9.1 Technical Risks

| Risk ID | Risk Description | Probability | Impact | Mitigation Strategy |
|---------|-----------------|-------------|--------|-------------------|
| **R-01** | Orthanc DICOM simulator behavior differs from production PACS | Medium | High | Validate against real PACS in staging environment; implement compatibility tests |
| **R-02** | gRPC in-process mock does not replicate real Core Engine behavior | Medium | High | Implement contract tests; validate against real Core Engine in staging |
| **R-03** | Test data not realistic enough to catch edge cases | Low | Medium | Use production anonymized data (with privacy review) |
| **R-04** | Concurrent test execution causes race conditions in tests | Medium | Medium | Implement test isolation; use deterministic test data |
| **R-05** | Integration tests too slow for CI/CD feedback loop | High | High | Parallelize tests; use selective test execution based on code changes |

### 9.2 Operational Risks

| Risk ID | Risk Description | Probability | Impact | Mitigation Strategy |
|---------|-----------------|-------------|--------|-------------------|
| **R-06** | Test environment not equivalent to production | Medium | High | Infrastructure as Code (IaC) for environment parity |
| **R-07** | Test data management becomes bottleneck | Medium | Medium | Automate test data generation and cleanup |
| **R-08** | Team lacks integration testing expertise | Low | High | Training on xUnit, Testcontainers, Docker; pair programming |
| **R-09** | Insufficient time allocation for automation | High | High | Phased approach; prioritize P0 scenarios; extend timeline if needed |

---

## 10. Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
**Objective:** Establish test infrastructure and automate P0 clinical workflows

**Deliverables:**
- [x] Integration test project structure created
- [x] Testcontainers configuration for Orthanc
- [x] Base fixture classes implemented
- [x] Test data generators implemented
- [x] INT-001 automated (Complete Examination Workflow)
- [x] INT-002 automated (DRL Alerting Workflow)
- [x] INT-003 automated (RBAC Enforcement)
- [x] INT-004 automated (Audit Trail Integrity)

**Success Criteria:**
- All P0 clinical workflow tests passing
- Test execution time <15 minutes
- CI/CD integration verified

### Phase 2: Error Handling & Data Flow (Week 3-4)
**Objective:** Automate P1 scenarios covering error recovery and data integrity

**Deliverables:**
- [x] INT-005 automated (PACS Communication Failure)
- [x] INT-006 automated (gRPC IPC Connection Failure)
- [x] INT-007 automated (Worklist to MPPS Consistency)
- [x] INT-008 automated (Image Data Integrity)

**Success Criteria:**
- All P1 tests passing (95%+)
- Error recovery scenarios validated
- Data integrity verified end-to-end

### Phase 3: Concurrency & Optimization (Week 5)
**Objective:** Automate P2 scenarios and optimize test suite performance

**Deliverables:**
- [x] INT-009 automated (Concurrent Exposure)
- [x] INT-010 automated (Concurrent User Sessions)
- [x] Test execution optimized to <30 minutes
- [x] Flaky test remediation
- [x] Test documentation complete

**Success Criteria:**
- All P2 tests passing (90%+)
- Test stability achieved (<1% flaky rate)
- Documentation complete

---

## 11. Maintenance Strategy

### 11.1 Test Maintenance

**Ongoing Activities:**
- **Test Review:** Quarterly review of test relevance and coverage
- **Test Data Refresh:** Monthly update of test data generators
- **Fixture Updates:** As external dependencies evolve (Orthanc versions, .NET updates)
- **Flaky Test Tracking:** Immediate remediation of flaky tests

**Test Health Metrics:**
- Pass rate trend (target: >98%)
- Execution time trend (target: <30 minutes)
- Flaky test rate (target: <1%)
- Test coverage trend (target: 70%+ integration coverage)

### 11.2 Regression Prevention

**Trigger Conditions for New Integration Tests:**
- Bug found in production → Create regression test
- New SPEC component integrated → Add cross-component scenarios
- Critical incident resolved → Add prevention test
- Performance degradation detected → Add performance regression test

---

## 12. Traceability Matrix

### 12.1 Requirements Traceability

| Scenario ID | SPEC-WORKFLOW | SPEC-IPC | SPEC-DICOM | SPEC-DOSE | SPEC-UI | SPEC-SECURITY |
|-------------|---------------|---------|------------|-----------|---------|---------------|
| **INT-001** | ★★★ | ★★ | ★★ | ★★★ | ★★★ | ★★ |
| **INT-002** | ★★ | — | — | ★★★ | ★★ | — |
| **INT-003** | ★★ | — | — | — | ★★★ | ★★★ |
| **INT-004** | ★ | — | — | — | — | ★★★ |
| **INT-005** | ★ | — | ★★★ | — | ★ | — |
| **INT-006** | ★ | ★★★ | — | — | ★★ | — |
| **INT-007** | ★★★ | — | ★★★ | — | ★ | — |
| **INT-008** | ★ | ★★ | ★★★ | — | ★★ | — |
| **INT-009** | ★★★ | — | — | ★★★ | — | — |
| **INT-010** | ★ | — | — | — | ★★★ | ★★★ |

### 12.2 Regulatory Traceability

| Scenario ID | IEC 62304 | IEC 60601-1-3 | IEC 60601-2-54 | FDA 21 CFR Part 11 | IHE REM |
|-------------|-----------|---------------|----------------|-------------------|---------|
| **INT-001** | ✓ | ✓ | ✓ | ✓ | ✓ |
| **INT-002** | ✓ | ✓ | ✓ | — | ✓ |
| **INT-003** | ✓ | — | — | ✓ | — |
| **INT-004** | ✓ | — | — | ✓ | — |
| **INT-005** | ✓ | — | — | — | ✓ |
| **INT-006** | ✓ | — | — | — | — |
| **INT-007** | ✓ | — | — | ✓ | ✓ |
| **INT-008** | ✓ | — | — | ✓ | — |
| **INT-009** | ✓ | ✓ | — | — | ✓ |
| **INT-010** | ✓ | — | — | ✓ | — |

---

## 13. Summary

### 13.1 Key Integration Points Identified

1. **Workflow + Dose (P0):** Clinical workflows with dose tracking
2. **Workflow + DICOM (P0):** MPPS and Worklist integration
3. **UI + Workflow + gRPC IPC (P0):** User actions triggering backend operations
4. **Security + All Components (P0):** Authentication/authorization for all operations

### 13.2 Priority Test Scenarios

**P0 (Critical - 100% required):**
- INT-001: Complete Patient Examination Workflow
- INT-002: DRL Alerting Workflow
- INT-003: RBAC Enforcement
- INT-004: Audit Trail Integrity

**P1 (High - 95%+ required):**
- INT-005: PACS Communication Failure
- INT-006: gRPC IPC Connection Failure
- INT-007: Worklist to MPPS Data Consistency
- INT-008: Image Data Integrity Through Pipeline

**P2 (Medium - 90%+ required):**
- INT-009: Concurrent Exposure and Dose Recording
- INT-010: Concurrent User Sessions

### 13.3 Recommended Test Framework

**Stack:**
- **Test Execution:** xUnit 2.6+ (already in project)
- **DICOM Simulation:** Testcontainers with Docker Orthanc
- **UI Testing:** Playwright (for E2E UI scenarios)
- **gRPC Mocking:** In-process gRPC server
- **Test Data:** Bogus (synthetic data generation)

**Rationale:**
- Team familiarity with xUnit
- Docker ensures isolated, reproducible test environments
- Orthanc provides full DICOM SCP functionality
- Playwright offers superior debugging and auto-waiting

### 13.4 Estimated Implementation Effort

**Total:** ~460 hours (12 weeks at 40 hours/week)

**Breakdown:**
- **Phase 1 (Foundation + P0):** 160 hours (4 weeks)
- **Phase 2 (P1 Error Handling + Data Flow):** 200 hours (5 weeks)
- **Phase 3 (P2 Concurrency + Optimization):** 100 hours (3 weeks)

**Resource Requirements:**
- 1 Senior Test Automation Engineer (full-time)
- 1 DICOM Specialist (part-time, 20%)
- 1 DevOps Engineer (CI/CD integration, part-time, 10%)

---

## Appendix A: Test Scenario Templates

### Template A.1: Clinical Workflow Test

```csharp
[Theory]
[InlineData("CHEST_PA", 120, 5, 30.5)]
[Trait("Category", "Integration")]
[Trait("Priority", "P0")]
public async Task CompleteExaminationWorkflow_ShouldSucceed(
    string protocol, float kvp, float mas, float expectedDapGyCm2)
{
    // Arrange
    await _orthanceFixture.LoadWorklistAsync(protocol);
    await _authFixture.LoginAsync("TECHNOLOGIST", "password");

    // Act
    var worklistItems = await _workflowService.FetchWorklistAsync();
    var selectedProcedure = worklistItems.First();
    await _workflowService.StartProcedureAsync(selectedProcedure);

    var exposureResult = await _exposureService.ExecuteExposureAsync(
        new ExposureParameters { Kv = kvp, Mas = mas });

    // Assert
    Assert.True(exposureResult.Success);
    Assert.Equal(expectedDapGyCm2, exposureResult.CalculatedDapGyCm2, 0.1);

    await _auditFixture.VerifyAuditEntryAsync("EXPOSURE_COMPLETED");
    await _dicomFixture.VerifyMppsCompletedAsync(selectedProcedure.MppsUid);
    await _dicomFixture.VerifyImageStoredAsync(exposureResult.ImageSopUid);
}
```

---

## Appendix B: Docker Compose Test Configuration

```yaml
version: '3.8'
services:
  orthanc-wl:
    image: jodogne/orthanc-plugins:latest
    ports:
      - "11114:4242"
    volumes:
      - ./test-data/orthanc-wl:/var/lib/orthanc/db:z
    environment:
      - ORTHANC_NAME=orthanc-wl
      - ORTHANC_MODALITIES=*

  orthanc-pacs:
    image: jodogne/orthanc-plugins:latest
    ports:
      - "11112:4242"
    volumes:
      - ./test-data/orthanc-pacs:/var/lib/orthanc/db:z
    environment:
      - ORTHANC_NAME=orthanc-pacs

  orthanc-mpps:
    image: jodogne/orthanc-plugins:latest
    ports:
      - "11116:4242"
    volumes:
      - ./test-data/orthanc-mpps:/var/lib/orthanc/db:z
    environment:
      - ORTHANC_NAME=orthanc-mpps

networks:
  test-network:
    driver: bridge
```

---

**Document Version:** 1.0.0
**Last Updated:** 2026-03-13
**Status:** Draft - Pending Review
**Next Review:** After Phase 1 completion (2026-03-27)
