# SPEC Review Report - HnVue Console

## Metadata

| Field | Value |
|-------|-------|
| Reviewer | team-quality |
| Date | 2026-02-17 |
| Scope | All 9 SPECs (15 files) |
| Verdict | Conditional Pass - Revisions Required |

---

## 1. Executive Summary

All 15 SPEC documents across 9 modules have been reviewed. The overall quality is strong: requirements use EARS format consistently, safety classifications align with IEC 62304 guidance, and the layered architecture is well-defined. However, several cross-SPEC inconsistencies, missing documents, and technical issues require revision before implementation can begin.

**Critical issues**: 4
**Warnings**: 8
**Suggestions**: 6

---

## 2. Document Completeness

### 2.1 File Coverage

| SPEC | spec.md | plan.md | acceptance.md | Status |
|------|---------|---------|---------------|--------|
| SPEC-INFRA-001 | Yes | No | No | Incomplete |
| SPEC-IPC-001 | Yes | No | No | Incomplete |
| SPEC-HAL-001 | Yes | Yes | Yes | Complete |
| SPEC-IMAGING-001 | Yes | No | No | Incomplete |
| SPEC-DICOM-001 | Yes | Yes | Yes | Complete |
| SPEC-DOSE-001 | Yes | No | No | Incomplete |
| SPEC-WORKFLOW-001 | Yes | Yes | Yes | Complete |
| SPEC-UI-001 | Yes | No | No | Incomplete |
| SPEC-TEST-001 | Yes | No | No | Incomplete |

**Finding [WARNING-01]**: 6 of 9 SPECs lack plan.md and acceptance.md. While the team lead indicated these can be added later, implementation should not start on a module without at least acceptance criteria defined.

### 2.2 EARS Format Compliance

All 9 spec.md files use EARS format for functional requirements. Compliance is consistent across documents. Requirements are categorized as Event-Driven, State-Driven, Ubiquitous, Unwanted, and Optional where appropriate.

**Verdict**: Pass

---

## 3. Critical Issues

### CRITICAL-01: Cross-SPEC Interface Name Mismatch (HAL vs WORKFLOW)

**Files**: `SPEC-HAL-001/spec.md`, `SPEC-WORKFLOW-001/spec.md`

SPEC-WORKFLOW-001 references HAL interfaces as:
- `IHalHvg` (WORKFLOW) vs `IHvgDriver` (HAL)
- `IHalInterlock` (WORKFLOW) vs no dedicated interlock interface (HAL)
- `IHalDetector` (WORKFLOW) vs `IDetector` (HAL)

These names must be reconciled. Either WORKFLOW should adopt HAL's naming or HAL should expose facade interfaces matching WORKFLOW's expectations.

**Impact**: Implementation will fail at integration if interfaces don't match.
**Recommendation**: Standardize on HAL's interface names (`IDetector`, `IGenerator`, `ICollimator`, `IHvgDriver`) and update WORKFLOW references accordingly. Create `IInterlock` interface in HAL if safety interlocks need a dedicated abstraction.

### CRITICAL-02: Phantom SPEC References in SPEC-IMAGING-001

**File**: `SPEC-IMAGING-001/spec.md`

References non-existent SPECs:
- `SPEC-ACQ-xxx` (acquisition SPEC - does not exist)
- `SPEC-VIEWER-001` (viewer SPEC - does not exist)
- `SPEC-CALIBRATION-001` (calibration SPEC - does not exist)

The project has exactly 9 SPECs. These references are dangling.

**Impact**: Creates confusion about scope boundaries and missing functionality.
**Recommendation**: Remove phantom references. Map the referenced functionality to actual SPECs (acquisition -> WORKFLOW, viewer -> UI, calibration -> IMAGING internal).

### CRITICAL-03: .NET Version Inconsistency in SPEC-DOSE-001

**File**: `SPEC-DOSE-001/spec.md`

References ".NET 6.0 LTS" in the deployment/runtime section. The project target is .NET 8 LTS as stated in the tech stack and all other C# SPECs.

**Impact**: Incorrect target framework will cause build configuration errors.
**Recommendation**: Change all .NET 6.0 references to .NET 8 LTS.

### CRITICAL-04: gRPC Version Inconsistency (INFRA vs IPC)

**Files**: `SPEC-INFRA-001/spec.md`, `SPEC-IPC-001/spec.md`

- SPEC-INFRA-001 lists gRPC 1.60.x in the SOUP table
- SPEC-IPC-001 references grpc++ v1.68

These must agree on a single gRPC version.

**Impact**: Version conflicts at build time; potential ABI incompatibility.
**Recommendation**: Align to a single version. Prefer 1.68.x if it's the latest stable at implementation time, and update INFRA's SOUP table accordingly.

---

## 4. Warnings

### WARNING-02: Protobuf Enum Zero Values (HAL)

**File**: `SPEC-HAL-001/spec.md`

Proto3 enums use meaningful values at index 0 (e.g., `AEC_MANUAL=0`, `GEN_IDLE=0`). Proto3 best practice requires index 0 to be an `UNSPECIFIED` sentinel value to distinguish "not set" from a valid default.

**Recommendation**: Add `*_UNSPECIFIED = 0` to all enums and shift existing values to start at 1.

### WARNING-03: DCMTK Fallback Architecture Unclear (DICOM)

**File**: `SPEC-DICOM-001/spec.md`

DICOM module is C# with fo-dicom 5.x. DCMTK is C++ library listed as fallback. The spec mentions "process interop" for DCMTK but doesn't specify the mechanism (CLI invocation? Named pipe? gRPC?).

**Recommendation**: Either remove DCMTK fallback from the DICOM spec (since fo-dicom handles all required codecs) or specify the exact interop mechanism in plan.md.

### WARNING-04: WPF/Avalonia Ambiguity (TEST)

**File**: `SPEC-TEST-001/spec.md`

References "WPF or Avalonia" for UI testing. The project uses WPF exclusively per SPEC-UI-001.

**Recommendation**: Remove Avalonia references. Standardize on WPF-specific testing approaches.

### WARNING-05: Missing Safety Interlock Interface (HAL)

**File**: `SPEC-HAL-001/spec.md`

SPEC-WORKFLOW-001 defines 6 safety interlocks (IL-01 through IL-06) that must be checked before every exposure. HAL's spec does not define a dedicated `IInterlock` or `ISafetyInterlock` interface to surface these hardware signals.

The interlock signals are scattered across `IGenerator` (ready status), `ICollimator` (position), `IDetector` (ready), and `IPatientTable` (locked). There's no unified interlock query mechanism.

**Recommendation**: Add an `ISafetyInterlock` composite interface in HAL that aggregates all 6 interlock checks into a single atomic query, or document exactly which existing interface method maps to each interlock.

### WARNING-06: Association Pooling Thread Safety (DICOM)

**File**: `SPEC-DICOM-001/plan.md`

Association pooling uses `SemaphoreSlim` per pool slot. The plan mentions keying by (CalledAETitle, Host, Port, TransferSyntax) but doesn't address pool size limits or connection exhaustion scenarios.

**Recommendation**: Define maximum pool size, connection timeout, and exhaustion behavior in spec.md NFRs.

### WARNING-07: Dose Calculation Audit Trail Integrity (DOSE)

**File**: `SPEC-DOSE-001/spec.md`

Dose values are used for patient safety reporting. The spec mentions "immutable audit records" but doesn't specify the integrity mechanism (hash chain, digital signature, write-once storage).

**Recommendation**: Specify the tamper-evidence mechanism for dose audit records, given IEC 62304 Class B classification.

### WARNING-08: UI Response Time vs IPC Latency Budget (UI + IPC)

**Files**: `SPEC-UI-001/spec.md`, `SPEC-IPC-001/spec.md`

- UI NFR: 200ms response time for user actions
- IPC NFR: 50ms image transfer, 10ms command round-trip

The latency budget for a full user action (UI -> IPC -> HAL -> IPC -> UI) isn't explicitly allocated. The 200ms UI budget must account for 2x IPC round-trips plus processing.

**Recommendation**: Add end-to-end latency budget allocation table showing how 200ms is distributed across layers.

### WARNING-09: No Error Recovery for Workflow FSM Transitions

**File**: `SPEC-WORKFLOW-001/spec.md`

The 10-state FSM defines transitions and guards but doesn't specify behavior when a transition guard fails. For example, if `EXPOSURE_TRIGGER` guard (all interlocks pass) fails mid-transition, the recovery path is not defined.

**Recommendation**: Add guard-failure recovery specifications for each safety-critical transition.

---

## 5. Suggestions

### SUGGESTION-01: Standardize Performance Constraint Format

Performance requirements use different formats across SPECs:
- HAL: "100ms frame DMA", "50ms HVG command"
- DICOM: "50 MB C-STORE within 10 s", "Worklist within 3 s"
- UI: "200ms response time"
- IMAGING: "<2s full pipeline"

**Recommendation**: Adopt a consistent format: `<metric>: <threshold> (<measurement_method>)`.

### SUGGESTION-02: Add Deployment Diagram

No SPEC includes a deployment diagram showing process boundaries (C++ engine process, C# GUI process, gRPC channel, PACS network). A single deployment diagram in SPEC-INFRA-001 would clarify the runtime architecture.

### SUGGESTION-03: Define SOUP Version Pinning Strategy

SPEC-INFRA-001 lists SOUP dependencies with version ranges (e.g., "OpenCV 4.9.x"). For IEC 62304 compliance, exact version pinning is required for released software. The strategy for when to pin vs. use ranges should be documented.

### SUGGESTION-04: Clarify Calibration Data Lifecycle (IMAGING)

SPEC-IMAGING-001 mentions calibration data hot-reload but doesn't specify:
- Who generates calibration data (factory? field service?)
- Storage format and location
- Validation on load (checksum, schema version)

### SUGGESTION-05: Add State Diagram for TransmissionQueue (DICOM)

The retry queue has states (PENDING, RETRYING, FAILED) but no visual state diagram. Adding one to plan.md would clarify the lifecycle.

### SUGGESTION-06: Define Log Rotation and Retention Policy

Multiple SPECs mention structured logging but none specify log rotation, maximum log size, or retention period. For a medical device, log retention requirements may be regulatory.

---

## 6. Regulatory Compliance Assessment

### IEC 62304

| Requirement | Status | Notes |
|-------------|--------|-------|
| Safety classification | Pass | Class C for WORKFLOW/Generator, Class B for others |
| Software development plan | Partial | Only 3 of 9 SPECs have plan.md |
| Requirements traceability | Pass | FR/NFR IDs enable traceability |
| Risk management integration | Pass | Risk tables present in plan.md files |
| SOUP management | Pass | SPEC-INFRA-001 has SOUP table with versions |
| V&V strategy | Pass | SPEC-TEST-001 defines comprehensive V&V matrix |

### ISO 14971

| Requirement | Status | Notes |
|-------------|--------|-------|
| Hazard identification | Pass | Safety interlocks in WORKFLOW, dose limits in DOSE |
| Risk estimation | Partial | Risk tables in plans but no formal severity/probability matrix |
| Risk control measures | Pass | Safety interlocks, parameter validation, audit trails |

### ISO 13485

| Requirement | Status | Notes |
|-------------|--------|-------|
| Design input | Pass | EARS requirements serve as design inputs |
| Design output | Partial | Acceptance criteria defined for 3 SPECs only |
| Design review | This report | Current document serves as design review |
| Design verification | Planned | SPEC-TEST-001 defines verification strategy |

---

## 7. Cross-SPEC Dependency Analysis

### Dependency Chain Validation

Expected: INFRA -> IPC -> HAL/IMAGING -> DICOM -> DOSE -> WORKFLOW -> UI

| Dependency | Declared | Consistent | Notes |
|------------|----------|------------|-------|
| IPC -> INFRA | Yes | Yes | Build system dependency |
| HAL -> IPC | Yes | Yes | gRPC server in C++ engine |
| HAL -> INFRA | Yes | Yes | Build tools |
| IMAGING -> HAL | Yes | Yes | Raw image data from detector |
| IMAGING -> INFRA | Yes | Yes | Build tools |
| DICOM -> IPC | Implicit | Warning | DICOM is C# but needs IPC to receive images from C++ engine |
| DOSE -> DICOM | Yes | Yes | RDSR generation |
| DOSE -> WORKFLOW | Implicit | Warning | Dose calculation triggered by workflow |
| WORKFLOW -> HAL | Yes | Mismatch | Interface names don't match (CRITICAL-01) |
| WORKFLOW -> DICOM | Yes | Yes | MWL, MPPS, Storage |
| WORKFLOW -> IMAGING | Yes | Yes | Image processing pipeline |
| WORKFLOW -> DOSE | Yes | Yes | Dose tracking |
| UI -> WORKFLOW | Yes | Yes | MVVM binds to workflow state |
| UI -> IPC | Yes | Yes | gRPC client in C# GUI |

### Circular Dependency Check

No circular dependencies detected. The layered architecture is clean.

---

## 8. Acceptance Criteria Quality (3 Complete SPECs)

### SPEC-HAL-001/acceptance.md
- Format: Gherkin (Given/When/Then) - Correct
- Coverage: All FR-HAL and NFR-HAL requirements covered
- Quality gates: Build, tests, coverage >=85%, TSan, ASan, clang-tidy
- **Verdict**: Pass

### SPEC-DICOM-001/acceptance.md
- Format: Structured acceptance criteria (AC-01 through AC-11)
- Coverage: All FR-DICOM requirements covered
- Quality gates: DVTK validation, Orthanc integration
- **Verdict**: Pass

### SPEC-WORKFLOW-001/acceptance.md
- Format: Gherkin for functional, table for safety
- Coverage: All FR-WF and NFR-WF requirements covered
- Safety: 100% branch coverage for Safety/ namespace specified
- **Verdict**: Pass

---

## 9. Plan Quality (3 Complete SPECs)

### SPEC-HAL-001/plan.md
- Milestones: 5 clear milestones with dependency order
- Risks: 6 risks with mitigations
- IEC 62304 artifacts: Listed
- **Verdict**: Pass

### SPEC-DICOM-001/plan.md
- Milestones: 4 goals (Primary, Secondary, Final, Optional)
- Technical approach: Detailed (fo-dicom, DI, retry queue, TLS, UID generation)
- Risks: 6 risks with mitigations
- **Verdict**: Pass

### SPEC-WORKFLOW-001/plan.md
- Milestones: 3 goals with 9 sub-milestones
- Technical approach: Channel-based executor, safety annotations
- Risks: Implicit in milestone descriptions but no formal risk table
- **Verdict**: Pass with note - add formal risk table

---

## 10. Revision Requirements Summary

### For C++ Layer SPECs (INFRA, IPC, HAL, IMAGING)

| ID | SPEC | Type | Description |
|----|------|------|-------------|
| CRITICAL-02 | IMAGING | Fix | Remove phantom SPEC references (SPEC-ACQ-xxx, SPEC-VIEWER-001, SPEC-CALIBRATION-001) |
| CRITICAL-04 | INFRA/IPC | Fix | Align gRPC version (1.60.x vs 1.68) |
| WARNING-02 | HAL | Fix | Add UNSPECIFIED=0 to all proto3 enums |
| WARNING-05 | HAL | Add | Define ISafetyInterlock interface or document interlock-to-interface mapping |

### For C# Layer SPECs (DICOM, DOSE, WORKFLOW, UI, TEST)

| ID | SPEC | Type | Description |
|----|------|------|-------------|
| CRITICAL-01 | WORKFLOW | Fix | Reconcile HAL interface names (IHalHvg -> IHvgDriver, etc.) |
| CRITICAL-03 | DOSE | Fix | Change .NET 6.0 LTS to .NET 8 LTS |
| WARNING-03 | DICOM | Clarify | Specify DCMTK fallback mechanism or remove |
| WARNING-04 | TEST | Fix | Remove Avalonia references, use WPF only |
| WARNING-07 | DOSE | Add | Specify audit trail integrity mechanism |
| WARNING-08 | UI/IPC | Add | End-to-end latency budget allocation |
| WARNING-09 | WORKFLOW | Add | Guard-failure recovery specifications |

---

## 11. Verdict

**Conditional Pass**: The SPEC documents demonstrate strong overall quality with comprehensive requirements coverage, proper EARS formatting, and clear architectural boundaries. However, the 4 critical issues (interface name mismatches, phantom references, version inconsistencies) must be resolved before implementation begins.

**Required before implementation**:
1. Fix all 4 CRITICAL issues
2. Address WARNING-02 (proto3 enum zero values) and WARNING-05 (safety interlock interface)
3. Generate plan.md and acceptance.md for remaining 6 SPECs (can be done incrementally per implementation order)

**Recommended but not blocking**:
- Address remaining warnings and suggestions
- Add deployment diagram
- Standardize performance constraint format

---

*End of Review Report*
