# SPEC-INTEGRATION-001: Integration Testing Framework

## Metadata

| Field | Value |
|-------|-------|
| SPEC ID | SPEC-INTEGRATION-001 |
| Title | HnVue Integration Test Plan |
| Product | HnVue - Diagnostic Medical Device X-ray GUI Console SW |
| Status | Active |
| Priority | High |
| Safety Class | IEC 62304 Class B/C |
| Created | 2026-03-13 |
| Updated | 2026-03-18 |
| Version | 1.1.0 |

---

## 1. Requirements (EARS Format)

### 1.1 Test Framework Requirements

**REQ-INT-001**: WHERE integration tests are executed, WHEN the test suite runs, THE system SHALL validate cross-component clinical workflows end-to-end without requiring Docker or external DICOM servers.

**REQ-INT-002**: WHERE mock services are used in integration tests, WHEN services interact across component boundaries, THE system SHALL use the same interface contracts (IUserService, IDoseService, IWorklistService, etc.) as production implementations.

**REQ-INT-003**: WHEN running integration tests on a CI/CD environment without display, THE test framework SHALL compile and execute under net8.0-windows using `dotnet build` and `dotnet test` commands.

**REQ-INT-004**: WHERE Docker-based integration tests are required, WHEN Testcontainers is available, THE test suite SHALL support Orthanc DICOM simulator containers for DICOM workflow validation (INT-005, INT-007, INT-008).

### 1.2 Clinical Workflow Requirements (INT-001: Complete Patient Examination)

**REQ-INT-010**: WHERE a TECHNOLOGIST user is authenticated, WHEN the complete examination workflow is executed, THE system SHALL complete all workflow phases (authentication, worklist query, patient selection, protocol selection, exposure preparation, dose recording, dose threshold check, audit trail) without errors.

**REQ-INT-011**: WHEN a user authenticates with valid credentials via IUserService, THE system SHALL create a valid UserSession with non-null SessionId, AccessToken, and an ExpiresAt approximately 30 minutes in the future.

**REQ-INT-012**: WHERE a user session is active, WHEN IWorklistService.GetWorklistAsync is called, THE system SHALL return a non-empty list of WorklistItems and IAuditLogService SHALL record an WORKLIST_QUERY event.

**REQ-INT-013**: WHEN a WorklistItem is selected for an examination, THE system SHALL retrieve the selected item's procedure details (ProcedureId, PatientId, PatientName, AccessionNumber).

**REQ-INT-014**: WHERE a body part and projection are selected, WHEN IProtocolService.GetProtocolPresetAsync is called with CHEST and PA parameters, THE system SHALL return a ProtocolPreset with valid default ExposureParameters (KVp, MA, ExposureTimeMs).

**REQ-INT-015**: WHERE exposure parameters are configured, WHEN IExposureService.TriggerExposureAsync is called, THE system SHALL return ExposureTriggerResult with Success=true and a non-null ImageId.

**REQ-INT-016**: WHERE an exposure is completed, WHEN IDoseService.GetCurrentDoseDisplayAsync is called, THE system SHALL return DoseDisplay with non-null CurrentDose and CumulativeDose values.

**REQ-INT-017**: WHERE dose has been accumulated during a study, WHEN IDoseService.GetAlertThresholdAsync is called, THE system SHALL return DoseAlertThreshold with WarningThreshold and ErrorThreshold that can be compared against cumulative dose for safety evaluation.

**REQ-INT-018**: WHEN any clinical workflow step is executed, THE IAuditLogService SHALL record appropriate audit events (UserLogin, WorklistQuery, ExposureInitiated, ExposureCompleted) that form a complete, verifiable audit trail.

### 1.3 DRL Alerting Requirements (INT-002 - Already Implemented)

**REQ-INT-020**: WHERE cumulative dose is below the DRL warning threshold, WHEN dose evaluation is performed, THE system SHALL NOT trigger any DRL alert.

**REQ-INT-021**: WHERE cumulative dose equals or exceeds the DRL warning threshold, WHEN dose evaluation is performed, THE system SHALL indicate warning alert state.

**REQ-INT-022**: WHERE cumulative dose equals or exceeds the DRL error threshold, WHEN dose evaluation is performed, THE system SHALL indicate error alert state.

### 1.4 Security Requirements (INT-003, INT-004 - Already Implemented)

**REQ-INT-030**: WHERE a user has the Technologist role, WHEN system.admin permission is checked via HasPermissionAsync, THE system SHALL return false.

**REQ-INT-031**: WHERE a user has the Viewer role, WHEN all granted permissions are enumerated, THE AllowedActions SHALL contain only "Read" actions.

**REQ-INT-032**: WHEN audit log entries are created via LogAsync, THE system SHALL maintain a SHA-256 hash chain where each entry references the previous entry's hash.

**REQ-INT-033**: WHERE any audit log entry is tampered with, WHEN VerifyIntegrityAsync is executed, THE system SHALL detect the corruption and identify the entry where the chain breaks.

---

## 2. Acceptance Criteria

### 2.1 INT-001: Complete Patient Examination Workflow

| ID | Criterion | Measurement |
|----|-----------|-------------|
| AC-INT-001-1 | User authentication succeeds with valid credentials | AuthenticationResult.Success == true |
| AC-INT-001-2 | Authenticated session has valid SessionId and AccessToken | Both non-null and non-empty |
| AC-INT-001-3 | Session expiry is approximately 30 minutes from creation | ExpiresAt within 1 minute of (UtcNow + 30 minutes) |
| AC-INT-001-4 | Worklist query returns at least one scheduled item | WorklistItems.Count >= 1 |
| AC-INT-001-5 | Selected worklist item has complete patient information | PatientId, PatientName, AccessionNumber all non-empty |
| AC-INT-001-6 | Protocol preset returns valid exposure parameters | ProtocolPreset.DefaultExposure != null, KVp > 0 |
| AC-INT-001-7 | Exposure trigger succeeds and returns ImageId | ExposureTriggerResult.Success == true, ImageId != null |
| AC-INT-001-8 | Dose display contains current and cumulative dose values | Both non-null, CumulativeDose.Value >= 0 |
| AC-INT-001-9 | Dose threshold check evaluates correctly against thresholds | Alert flags correspond correctly to dose vs threshold comparison |
| AC-INT-001-10 | Audit log contains USER_LOGIN event | At least one AuditLogEntry with EventType == UserLogin |
| AC-INT-001-11 | Audit log contains EXPOSURE events | At least one entry with ExposureInitiated or ExposureCompleted |
| AC-INT-001-12 | Complete workflow executes without unhandled exceptions | No exceptions thrown during any step |

### 2.2 INT-002: DRL Alerting (Already Implemented)

| ID | Criterion | Measurement |
|----|-----------|-------------|
| AC-INT-002-1 | No alert below warning threshold | isAlertTriggered == false when dose < warningThreshold |
| AC-INT-002-2 | Warning alert above warning threshold | isWarningTriggered == true when dose >= warningThreshold |
| AC-INT-002-3 | Error alert above error threshold | isErrorTriggered == true when dose >= errorThreshold |
| AC-INT-002-4 | Threshold update persisted | GetAlertThresholdAsync returns updated values |
| AC-INT-002-5 | Dose reset clears cumulative dose | CumulativeDose.Value == 0 after reset |

### 2.3 INT-003/INT-004: Security (Already Implemented)

| ID | Criterion | Measurement |
|----|-----------|-------------|
| AC-INT-003-1 | Admin has system.admin permission | HasPermissionAsync("admin", "system.admin") == true |
| AC-INT-003-2 | Technologist denied system.admin | HasPermissionAsync("tech01", "system.admin") == false |
| AC-INT-003-3 | Viewer has read-only actions | All AllowedActions == "Read" |
| AC-INT-004-1 | Hash chain valid for unmodified log | VerifyIntegrityAsync().IsValid == true |
| AC-INT-004-2 | Corruption detected in tampered log | VerifyIntegrityAsync().IsValid == false, BrokenAtEntryId != null |

---

## 3. Technical Approach

### 3.1 Integration Strategy

The integration test suite uses a layered approach:

**Layer 1 - Mock-based Integration (Current Phase)**
- Uses production Mock* implementations (MockUserService, MockDoseService, etc.)
- Tests cross-service interaction via shared models and service interfaces
- No Docker or external services required
- Validates service coordination and data flow

**Layer 2 - Testcontainers-based Integration (Future Phase)**
- Uses Docker Orthanc container for DICOM workflow validation
- Tests INT-005, INT-007, INT-008 with real DICOM protocol
- Requires `Testcontainers` NuGet package (v3.7.0, already in Directory.Packages.props)

### 3.2 Test Structure

```
tests/integration/HnVue.Integration.Tests/
├── ClinicalWorkflows/
│   ├── CompleteExaminationWorkflowTests.cs   # INT-001 (this sprint)
│   └── DrlAlertingWorkflowTests.cs           # INT-002 (complete)
├── Security/
│   ├── RbacEnforcementTests.cs               # INT-003 (complete)
│   └── AuditTrailIntegrityTests.cs           # INT-004 (complete)
└── HnVue.Integration.Tests.csproj
```

### 3.3 Mock Service Usage for INT-001

INT-001 uses the following production Mock services directly:
- `MockUserService` - Authentication, session management, RBAC
- `MockWorklistService` - DICOM worklist items
- `MockDoseService` - Dose recording, thresholds, display
- `MockAuditLogService` - Audit event recording and integrity
- `MockExposureService` - Exposure parameter management and triggering
- `MockProtocolService` - Body part/projection protocol management

### 3.4 IEC 62304 Compliance

All integration tests include regulatory traceability:
- `[Trait("SPEC", "SPEC-INTEGRATION-001")]` - SPEC traceability
- `[Trait("Priority", "P0")]` - Safety criticality
- `[Trait("Category", "Integration")]` - Test classification
- Audit trail verification validates IEC 62304 Class C requirement for traceable clinical events

---

## 4. Test Coverage Matrix

| Test ID | Description | Priority | Status | Services Exercised |
|---------|-------------|----------|--------|--------------------|
| INT-001 | Complete Patient Examination Workflow | P0 | Active | IUserService, IWorklistService, IProtocolService, IExposureService, IDoseService, IAuditLogService |
| INT-002 | DRL Alerting Workflow | P0 | Complete | IDoseService |
| INT-003 | RBAC Enforcement | P0 | Complete | IUserService |
| INT-004 | Audit Trail Integrity | P0 | Complete | IAuditLogService |
| INT-005 | PACS Communication Failure | P1 | Active (Mock-based) | IImageService, IAuditLogService |
| INT-006 | gRPC IPC Connection Failure | P1 | Active (Mock-based) | ISystemStatusService, IAuditLogService |
| INT-007 | Worklist to MPPS Data Consistency | P1 | Planned | IWorklistService, IMppsService (requires Docker) |
| INT-008 | Image Data Integrity Through Pipeline | P1 | Planned | IImageService, IDicomService (requires Docker) |
| INT-009 | Concurrent Exposure and Dose Recording | P2 | Planned | IDoseService, IExposureService |
| INT-010 | Concurrent User Sessions | P2 | Planned | IUserService |

### 4.1 SPEC Interdependency Coverage

| Covered SPEC | Integration Points Tested |
|--------------|--------------------------|
| SPEC-SECURITY-001 | Authentication, Session, RBAC, Audit Trail (INT-001, INT-003, INT-004) |
| SPEC-DOSE-001 | Dose display, thresholds, alerts, reset (INT-001, INT-002) |
| SPEC-WORKFLOW-001 | Worklist-to-exposure workflow orchestration (INT-001) |
| SPEC-IPC-001 | Exposure triggering via service layer (INT-001) |
| SPEC-DICOM-001 | Worklist query, MPPS (INT-001 partial, INT-007 planned) |

---

## 5. Regulatory Compliance Mapping

| Scenario | IEC 62304 | IEC 60601-1-3 | FDA 21 CFR Part 11 | IHE REM |
|----------|-----------|---------------|-------------------|---------|
| INT-001 | 5.7 (System Integration Testing) | 3.4 (Dose Display) | 11.10 (Audit Trail) | REM Profile |
| INT-002 | 5.7 | 3.4 (DRL Alerting) | — | REM Profile |
| INT-003 | 5.7 | — | 11.300 (Access Control) | — |
| INT-004 | 5.7 | — | 11.10 (Audit Trail) | — |

---

## 6. Definition of Done

An integration test scenario is complete when:
1. Test code is written following the established pattern (Arrange-Act-Assert, FluentAssertions)
2. All acceptance criteria are verified by passing assertions
3. Test compiles successfully with `dotnet build`
4. Test passes locally with `dotnet test --filter "Category=Integration"`
5. Test uses `[Trait]` attributes for SPEC, Priority, and Category
6. Code comments are in English
7. No test relies on hardcoded timing (use deterministic mock behavior)
8. All public interfaces exercised through interface types (not concrete mock types)

---

**Document Version:** 1.1.0
**Last Updated:** 2026-03-18
**Status:** Active
**Based On:** plan.md v1.0.0 (2026-03-13)
