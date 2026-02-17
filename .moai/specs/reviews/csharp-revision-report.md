# C# Layer Revision Report

## Summary
- Issues addressed: 7/7 (all assigned issues)
- Additional improvements: 2 (WARNING-06, DCMTK cleanup in plan.md)
- Files modified: 8

## Revisions

### CRITICAL-01: Cross-SPEC Interface Name Mismatch (HAL vs WORKFLOW)
- **Files**: SPEC-WORKFLOW-001/spec.md, plan.md, acceptance.md
- **Action**: Renamed all HAL interface references to match HAL's actual names:
  - `IHalHvg` -> `IHvgDriver` (all occurrences across 3 files)
  - `IHalAcquisition` -> `IDetector` (all occurrences across 3 files)
  - `IHalInterlock` -> `ISafetyInterlock` (all occurrences across 3 files, aligned with C++ team adding ISafetyInterlock to HAL)
  - `IHalAec` -> `IAecController` (all occurrences across 3 files)
- **Status**: FIXED
- **Notes**: Updated in spec.md (Section 5.1 interlock table, Section 6.1 dependency table, Section 8.1 package structure), plan.md (Milestone 1.3, DI section), and acceptance.md (all Gherkin scenarios referencing HAL interfaces)

### CRITICAL-03: .NET Version Inconsistency
- **File**: SPEC-DOSE-001/spec.md
- **Action**: Changed ".NET 6.0 LTS or later" to ".NET 8 LTS or later" and "C# 10.0 or later" to "C# 12.0 or later" in Section 1.2 Deployment Environment
- **Status**: FIXED
- **Notes**: All other C# SPECs already correctly reference .NET 8 LTS

### WARNING-03: DCMTK Fallback Architecture Unclear
- **Files**: SPEC-DICOM-001/spec.md, plan.md
- **Action**: Removed DCMTK as a fallback from the C# DICOM layer. fo-dicom 5.x is now specified as the sole DICOM engine for the C# layer. Updated:
  - spec.md Metadata: "fo-dicom 5.x (sole DICOM engine)"
  - spec.md Section 1.6: Removed DCMTK row from technical dependencies table
  - spec.md Assumption A-04: Updated to reflect fo-dicom as complete solution
  - plan.md Metadata: Updated library field
  - plan.md Section 2.1: Replaced DCMTK fallback paragraph with note that DCMTK is C++ engine only
  - plan.md Risks: Updated JPEG 2000 risk mitigation to remove DCMTK reference
- **Status**: FIXED
- **Notes**: DCMTK remains available in the C++ core engine (SPEC-HAL-001) for emergency raw pixel data handling. The C# DICOM layer does not need DCMTK since fo-dicom 5.x provides all required codecs.

### WARNING-04: WPF/Avalonia Ambiguity
- **File**: SPEC-TEST-001/spec.md
- **Action**: Changed "C# / WPF or Avalonia" to "C# / WPF" in Section 1.2 Technology Stack table
- **Status**: FIXED
- **Notes**: The project exclusively uses WPF per SPEC-UI-001. No other Avalonia references found in SPEC-TEST-001.

### WARNING-07: Dose Audit Trail Integrity
- **File**: SPEC-DOSE-001/spec.md
- **Action**: Added two new requirements under NFR-DOSE-04:
  - R-NFR-04-D: SHA-256 hash chain mechanism where each audit record includes the hash of the previous record, with a well-known initialization vector for the first record
  - R-NFR-04-E: On-demand audit trail integrity verification utility that reports hash chain breaks
- **Status**: FIXED
- **Notes**: SHA-256 hash chain provides tamper-evidence suitable for IEC 62304 Class B without requiring cryptographic key management (no digital signatures needed at this safety level)

### WARNING-08: UI Latency Budget Allocation
- **File**: SPEC-UI-001/spec.md
- **Action**: Added NFR-UI-02a section with:
  - End-to-end latency budget allocation table breaking 200ms into 5 layers (UI Input 30ms, IPC Request 10ms, Engine Processing 120ms, IPC Response 10ms, UI Rendering 30ms)
  - NFR-UI-02a-02: Per-layer latency instrumentation requirement with 150% budget violation logging
- **Status**: FIXED
- **Notes**: Budget numbers are based on IPC NFR values (10ms command round-trip) and realistic WPF rendering performance. Engine processing gets the largest allocation (120ms) as it includes workflow logic, HAL commands, and image processing.

### WARNING-09: FSM Guard-Failure Recovery
- **File**: SPEC-WORKFLOW-001/spec.md
- **Action**: Added Section 5.4 "Guard-Failure Recovery Specifications" with:
  - Recovery table covering 5 critical transition scenarios:
    - T-07 interlock failure: remain in POSITION_AND_PREVIEW, display failed interlock
    - T-07 detector not ready: remain, poll detector readiness
    - T-06 parameter out of range: remain in PROTOCOL_SELECT, display valid bounds
    - T-08/T-09 mid-exposure interlock loss: abort exposure, record partial dose
    - T-18 critical hardware error: emergency standby all hardware
  - Safety-06: Guard failure audit entry requirements
  - Safety-07: Fresh interlock re-evaluation required after any guard failure
- **Status**: FIXED
- **Notes**: Recovery actions are conservative (fail-safe) - they always preserve the current state or move to a safe state, never attempt to force the original transition.

## Additional Improvements

### WARNING-06: Association Pool Size Limits (DICOM)
- **Files**: SPEC-DICOM-001/spec.md, plan.md
- **Action**:
  - spec.md: Added NFR-POOL-01 with max pool size (4) and acquisition timeout (30s)
  - plan.md: Added pool configuration parameters (max size, acquisition timeout, exhaustion behavior, idle eviction)
- **Status**: FIXED (additional improvement, not blocking)

### DCMTK Reference Cleanup in plan.md
- **File**: SPEC-DICOM-001/plan.md
- **Action**: Cleaned up all DCMTK fallback references to be consistent with the spec.md changes
- **Status**: FIXED (part of WARNING-03 resolution)

## Deferred Items

### SUGGESTION-05: TransmissionQueue State Diagram (DICOM)
- **Status**: DEFERRED
- **Reason**: Non-blocking suggestion. The queue states (PENDING, RETRYING, FAILED) are clearly described in text. A visual diagram can be added during implementation.

### SUGGESTION-06: Log Retention Policy Reference (TEST)
- **Status**: DEFERRED
- **Reason**: Non-blocking suggestion. Log retention is implicitly covered by SPEC-TEST-001 C-01 constraint (device lifetime + 2 years per ISO 13485). A cross-SPEC log retention policy can be established during SPEC-INFRA-001 plan phase.

## New Concerns Discovered

1. **SPEC-WORKFLOW-001/spec.md Section 6.1**: The dependency table now correctly references ISafetyInterlock with `CheckAllInterlocks()` method. This is a new method not in the original HAL SPEC - the C++ team should confirm this method is being added to the ISafetyInterlock interface.

2. **SPEC-DOSE-001/spec.md Section 4.5**: The audit trail schema does not include a `PreviousRecordHash` field needed for the SHA-256 hash chain (NFR-04-D). This field should be added to the schema during implementation planning (plan.md creation).

## Change Log

| File | Changes |
|------|---------|
| SPEC-DICOM-001/spec.md | Removed DCMTK from metadata, dependencies table, assumption A-04; added NFR-POOL-01 |
| SPEC-DICOM-001/plan.md | Removed DCMTK fallback references; updated library metadata; added association pool configuration; updated risk mitigation |
| SPEC-DOSE-001/spec.md | Fixed .NET 6.0 -> .NET 8 LTS, C# 10.0 -> C# 12.0; added NFR-04-D and NFR-04-E (hash chain audit integrity) |
| SPEC-WORKFLOW-001/spec.md | Renamed all IHal* interfaces to match HAL names (IHvgDriver, IDetector, ISafetyInterlock, IAecController); added Section 5.4 guard-failure recovery; added Safety-06, Safety-07 |
| SPEC-WORKFLOW-001/plan.md | Renamed all IHal* interfaces in milestones and DI section |
| SPEC-WORKFLOW-001/acceptance.md | Renamed all IHal* interfaces in Gherkin scenarios (IHalHvg->IHvgDriver, IHalAec->IAecController, IHalAcquisition->IDetector) |
| SPEC-UI-001/spec.md | Added NFR-UI-02a latency budget allocation table and instrumentation requirement |
| SPEC-TEST-001/spec.md | Removed Avalonia from technology stack (WPF only) |

---

*Report generated: 2026-02-17*
*Reviewer: frontend-dev (C# layer specialist)*
