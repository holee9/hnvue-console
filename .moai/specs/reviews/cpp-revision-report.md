# C++ Layer Revision Report

## Summary
- Issues addressed: 4/4 required + 1 partial suggestion
- Files modified: SPEC-INFRA-001/spec.md, SPEC-IPC-001/spec.md (no changes needed), SPEC-HAL-001/spec.md, SPEC-HAL-001/acceptance.md, SPEC-IMAGING-001/spec.md

## Revisions

### CRITICAL-02: Phantom SPEC References in SPEC-IMAGING-001
- **File**: `.moai/specs/SPEC-IMAGING-001/spec.md`
- **Action**: Removed all references to non-existent SPECs (SPEC-ACQ-xxx, SPEC-VIEWER-001, SPEC-CALIBRATION-001). Remapped as follows:
  - `SPEC-ACQ-xxx` in system context diagram -> `SPEC-WORKFLOW-001` (acquisition is triggered by workflow)
  - `SPEC-ACQ-001 (TBD)` in Related Documents -> `SPEC-WORKFLOW-001` (acquisition workflow)
  - `SPEC-VIEWER-001 (TBD)` in Related Documents -> `SPEC-UI-001` (WPF GUI diagnostic viewer)
  - `SPEC-CALIBRATION-001 (TBD)` in Related Documents -> `SPEC-IMAGING-001 (this document)` with note that calibration data lifecycle is managed internally by CalibrationManager (Section 4.4)
- **Status**: FIXED
- **Notes**: The system context diagram arrow label was updated from `[SPEC-ACQ-xxx]` to `[SPEC-WORKFLOW-001]` to accurately reflect that acquisition is orchestrated by the workflow module.

### CRITICAL-04: gRPC Version Mismatch (INFRA vs IPC)
- **File**: `.moai/specs/SPEC-INFRA-001/spec.md`
- **Action**: Updated SOUP table gRPC version from `1.60.x` to `1.68.x` to align with SPEC-IPC-001 which already specifies `grpc++ v1.68`.
- **Status**: FIXED
- **Notes**: SPEC-IPC-001 already had the correct version (1.68) and required no changes. INFRA was the only file needing update.

### WARNING-02: Proto3 Enum Zero Values
- **File**: `.moai/specs/SPEC-HAL-001/spec.md`
- **Action**: Added `*_UNSPECIFIED = 0` sentinel values to all 5 proto3 enum definitions and shifted existing meaningful values to start at 1:
  - `AecMode`: Added `AEC_MODE_UNSPECIFIED = 0`, shifted AEC_MANUAL to 1, AEC_AUTO to 2
  - `GeneratorState`: Added `GEN_STATE_UNSPECIFIED = 0`, shifted GEN_IDLE to 1 through GEN_ERROR to 5
  - `AlarmSeverity`: Added `ALARM_SEVERITY_UNSPECIFIED = 0`, shifted ALARM_INFO to 1 through ALARM_CRITICAL to 4
  - `AcquisitionMode`: Added `ACQUISITION_MODE_UNSPECIFIED = 0`, shifted MODE_STATIC to 1 through MODE_TRIGGERED to 3
  - `CalibType`: Added `CALIB_TYPE_UNSPECIFIED = 0`, shifted CALIB_DARK_FIELD to 1 through CALIB_DEFECT_MAP to 3
- **Status**: FIXED
- **Notes**: Acceptance criteria in acceptance.md reference enum value names (e.g., `GEN_IDLE`, `AEC_MANUAL`) not numeric indices, so they remain valid without modification.

### WARNING-05: Missing Safety Interlock Interface
- **File**: `.moai/specs/SPEC-HAL-001/spec.md`, `.moai/specs/SPEC-HAL-001/acceptance.md`
- **Action**: Added `ISafetyInterlock` composite interface in Section 4.3.4 with:
  - `InterlockStatus` struct aggregating all 6 interlock checks (IL-01 through IL-06) with explicit mapping to existing HAL interfaces
  - `CheckAllInterlocks()` method returning aggregate status (single atomic query)
  - `CheckInterlock(int)` method for individual interlock checks
  - `RegisterInterlockCallback()` for state change notifications
  - Updated NFR-HAL-03 to reference 7 interfaces (was 6)
  - Added `ISafetyInterlock.h` to directory structure
  - Added `MockSafetyInterlock.h` to test mock directory
  - Updated acceptance.md AC-HAL-NFR-03-01 to reference 7 interfaces
- **Status**: FIXED
- **Notes**: The interface documents the mapping between each interlock (IL-01 through IL-06) and the corresponding HAL interface method. The `CheckAllInterlocks()` method provides the single atomic query recommended by the reviewer. 10 ms completion requirement specified to avoid exposure delay.

## Suggestions Addressed

### SUGGESTION-01: Standardize Performance Constraint Format
- **Status**: DEFERRED
- **Notes**: HAL spec already uses a consistent table format in Section 4.7 with Metric/Threshold/Measurement Point columns. Cross-SPEC standardization should be coordinated across all 9 SPECs in a separate pass.

### SUGGESTION-02: Deployment Diagram in SPEC-INFRA-001
- **Status**: DEFERRED
- **Notes**: SPEC-INFRA-001 already has a system architecture diagram in Section 1.1 showing process boundaries and IPC channel. A full deployment diagram would be valuable but is not blocking for implementation.

### SUGGESTION-03: SOUP Version Pinning Strategy
- **Status**: DEFERRED
- **Notes**: SPEC-INFRA-001 NFR-INFRA-03 already addresses SOUP version pinning requirements. The strategy for ranges vs. exact pins is implicitly covered by the vcpkg baseline pinning mechanism. A more explicit policy could be added in plan.md when it is created.

### SUGGESTION-04: Calibration Data Lifecycle
- **Status**: DEFERRED
- **Notes**: SPEC-IMAGING-001 Section 4.4 covers calibration data format, validation rules, and hot-reload. The question of who generates calibration data (factory vs. field service) is a system-level decision outside this SPEC's scope. Recommend adding to SPEC-WORKFLOW-001 as a calibration workflow requirement.

## New Concerns
- The ISafetyInterlock interface depends on all 6 device interfaces being initialized. The DeviceManager should ensure ISafetyInterlock is constructed after all device interfaces are available, and should handle partial initialization gracefully (e.g., if a non-critical device like patient table is unavailable, the interlock for that device should report its specific failure rather than blocking all interlocks).
- The proto3 enum value renumbering is a breaking change if any code was already generated against the old numbering. Since implementation has not started, this is safe to apply now but must be treated as a major version bump if applied after implementation begins.

## Change Log

| File | Changes |
|------|---------|
| `SPEC-INFRA-001/spec.md` | Updated gRPC SOUP version from 1.60.x to 1.68.x (line 102) |
| `SPEC-IPC-001/spec.md` | No changes needed (already had correct gRPC version 1.68) |
| `SPEC-HAL-001/spec.md` | Added UNSPECIFIED=0 sentinels to 5 proto3 enums; added ISafetyInterlock interface with InterlockStatus struct; updated NFR-HAL-03 to 7 interfaces; added ISafetyInterlock.h and MockSafetyInterlock.h to directory structure |
| `SPEC-HAL-001/acceptance.md` | Updated AC-HAL-NFR-03-01 from 6 to 7 mockable interfaces |
| `SPEC-HAL-001/plan.md` | No changes needed |
| `SPEC-IMAGING-001/spec.md` | Replaced phantom SPEC references (SPEC-ACQ-xxx, SPEC-VIEWER-001, SPEC-CALIBRATION-001) with actual SPECs (SPEC-WORKFLOW-001, SPEC-UI-001, SPEC-IMAGING-001 internal) |

---

*C++ Layer Revision Report v1.0 | 2026-02-17 | Reviewer: abyz-lab*
