# HnVue SPEC Requirements Gap Analysis

**Analysis Date**: 2026-02-27
**Analyst**: team-analyst
**Product**: HnVue - Diagnostic Medical Device X-ray GUI Console SW

---

## Executive Summary

This document analyzes the requirements gaps, dependencies, and integration risks across the 9 SPECs that define the HnVue project. The analysis identifies missing documentation, unclear assumptions, and validation needs.

**Key Findings:**
- 9 SPECs defined with clear implementation dependencies
- 4 SPECs completed (INFRA, IPC, HAL, IMAGING)
- 5 SPECs planned but not yet implemented (DICOM, DOSE, WORKFLOW, UI, TEST)
- Critical gaps in cross-SPEC integration interfaces

---

## 1. SPEC Dependency Graph

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         HnVue SPEC Dependency Map                          │
└─────────────────────────────────────────────────────────────────────────────┘

                              ┌──────────────────┐
                              │  SPEC-INFRA-001  │ ✅ Completed
                              │  (Build/CI/CD)    │
                              └────────┬─────────┘
                                       │ Prerequisite for all
                    ┌──────────────────┼──────────────────┬──────────────────┐
                    ▼                  ▼                  ▼                  ▼
            ┌───────────────┐  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐
            │ SPEC-IPC-001  │  │ SPEC-HAL-001  │  │ SPEC-TEST-001 │  │ SPEC-IMAGING  │
            │ (gRPC IPC)    │  │ (Hardware)    │  │ (Testing)     │  │ -001          │
            │ ✅ Completed  │  │ ✅ Completed  │  │ Planned       │  │ ✅ Completed  │
            └───────┬───────┘  └───────┬───────┘  └───────┬───────┘  └───────┬───────┘
                    │                  │                  │                  │
                    └──────────────────┼──────────────────┼──────────────────┘
                                       │                  │
                    ┌──────────────────┼──────────────────┼──────────────────┐
                    ▼                  ▼                  ▼                  ▼
            ┌───────────────┐  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐
            │ SPEC-DICOM    │  │ SPEC-DOSE     │  │ SPEC-WORKFLOW │  │   SPEC-UI     │
            │ -001          │  │ -001          │  │ -001          │  │ -001          │
            │ Planned       │  │ Planned       │  │ Planned       │  │ Planned       │
            └───────────────┘  └───────────────┘  └───────────────┘  └───────┬───────┘
                                                                        │
                                                                    Consumes
                                                                    All Above
```

### Dependency Levels

| Level | SPEC(s) | Description |
|-------|---------|-------------|
| **Level 1** | INFRA | Foundation - Build system, CI/CD, dependencies |
| **Level 2** | IPC, HAL, TEST, IMAGING | Core services - Communication, hardware, testing |
| **Level 3** | DICOM, DOSE, WORKFLOW | Business services - Clinical workflows |
| **Level 4** | UI | Presentation layer - User interface |

---

## 2. Cross-SPEC Interface Dependencies

### 2.1 Critical Integration Points

| Integration Point | SPEC A | SPEC B | Interface Status | Risk Level |
|-------------------|--------|--------|------------------|------------|
| gRPC Service Contracts | IPC-001 | UI-001 | Proto defined | Medium |
| HAL to Workflow | HAL-001 | WORKFLOW-001 | Interface defined | Low |
| DICOM RDSR Generation | DOSE-001 | DICOM-001 | Message format unclear | **HIGH** |
| Dose Display | DOSE-001 | UI-001 | Data contract defined | Low |
| Image Processing | IMAGING-001 | UI-001 | IPC streaming defined | Medium |
| Safety Interlocks | HAL-001 | WORKFLOW-001 | ISafetyInterlock defined | Low |

### 2.2 Missing Interface Definitions

**Gap #1: DICOM to DOSE RDSR Integration**
- **Issue**: SPEC-DOSE-001 defines RDSR generation, but SPEC-DICOM-001 doesn't reference the RDSR SOP class from DOSE
- **Impact**: Duplicate RDSR generation or missing dose data in DICOM exports
- **Recommendation**: Create joint interface specification defining how DOSE RDSR integrates with DICOM C-STORE

**Gap #2: Test Infrastructure Coverage**
- **Issue**: SPEC-TEST-001 references all other SPECs but lacks specific test case mappings for completed SPECs
- **Impact**: Unclear test coverage validation for INFRA, IPC, HAL, IMAGING
- **Recommendation**: Create traceability matrix mapping completed SPEC requirements to test cases

**Gap #3: IPC Message Versioning**
- **Issue**: SPEC-IPC-001 defines versioning but consuming SPECs don't specify version compatibility requirements
- **Impact**: Runtime incompatibility between Core Engine and GUI
- **Recommendation**: Add version compatibility constraints to all consuming SPECs

---

## 3. Requirements Completeness Analysis

### 3.1 Completed SPECs (4/9)

| SPEC | Status | Coverage | Gaps |
|------|--------|----------|------|
| **SPEC-INFRA-001** | ✅ Complete | Build system, CI/CD, vcpkg | None - implementation matches SPEC |
| **SPEC-IPC-001** | ✅ Complete | gRPC proto, services | Missing runtime error recovery scenarios |
| **SPEC-HAL-001** | ✅ Complete | Hardware interfaces | Missing simulator interface spec for testing |
| **SPEC-IMAGING-001** | ✅ Complete | Image pipeline | Missing integration test specification |

### 3.2 Planned SPECs (5/9)

| SPEC | Status | Blockers | Critical Gaps |
|------|--------|----------|--------------|
| **SPEC-DICOM-001** | Planned | None | Missing PACS configuration validation |
| **SPEC-DOSE-001** | Planned | DAP calibration coefficients | Missing DAP meter hardware spec |
| **SPEC-WORKFLOW-001** | Planned | HAL, IPC, DICOM, DOSE | Missing crash recovery edge cases |
| **SPEC-UI-001** | Planned | IPC, all services | Missing accessibility validation spec |
| **SPEC-TEST-001** | Planned | All SPECs | Missing hardware simulator validation |

---

## 4. Assumptions Requiring Validation

### 4.1 High-Risk Assumptions

| ASSUMPTION | Source SPEC | Risk | Validation Method |
|------------|-------------|------|-------------------|
| A-01 (IPC latency) | IPC-001 | gRPC localhost latency < 50us | Performance benchmark required |
| A-02 (16-bit pipeline) | IMAGING-001 | All stages preserve 16-bit precision | Validation test needed |
| A-03 (DAP meter optional) | DOSE-001 | System operates without DAP meter | Integration test required |
| A-04 (PACS availability) | DICOM-001 | Worklist available on network | Network failure simulation needed |
| A-05 (HAL response time) | WORKFLOW-001 | Interlock check < 10ms | Hardware simulator validation |

### 4.2 Technical Assumptions Requiring Clarification

**ASSUMPTION A-DOSE-01**: HVG parameters delivered to Dose subsystem within 200ms
- **Gap**: No specification of what happens if timeout occurs
- **Impact**: Dose records may be incomplete
- **Recommendation**: Define timeout handling behavior

**ASSUMPTION A-WORKFLOW-01**: HAL provides deterministic interlock response
- **Gap**: No specification for interlock signal failure
- **Impact**: Safety-critical exposure command may hang
- **Recommendation**: Define timeout and fail-safe behavior

**ASSUMPTION A-UI-01**: Core Engine exposes all business logic via gRPC
- **Gap**: No fallback if Core Engine is unavailable
- **Impact**: UI cannot function in degraded mode
- **Recommendation**: Define degraded mode operation

---

## 5. Safety-Critical Integration Risks

### 5.1 Radiation Safety Risks

| Risk ID | Description | Related SPECs | Severity | Mitigation Status |
|---------|-------------|---------------|----------|-------------------|
| **SAFETY-01** | Exposure command without interlock verification | HAL-001, WORKFLOW-001 | CRITICAL | ✅ Specified in WORKFLOW-001 |
| **SAFETY-02** | Dose accumulation tracking failure | DOSE-001, WORKFLOW-001 | CRITICAL | ⚠️ Partial - missing failure recovery |
| **SAFETY-03** | AEC override without operator confirmation | HAL-001, UI-001 | HIGH | ⚠️ UI confirmation not explicit |
| **SAFETY-04** | Emergency workflow bypasses safety checks | WORKFLOW-001, UI-001 | HIGH | ✅ Emergency path specified |

### 5.2 Data Integrity Risks

| Risk ID | Description | Related SPECs | Severity | Mitigation Status |
|---------|-------------|---------------|----------|-------------------|
| **INTEGRITY-01** | Patient-DICOM mismatch | DICOM-001, UI-001 | MEDIUM | ⚠️ Reconciliation not specified |
| **INTEGRITY-02** | RDSR dose data corruption | DOSE-001, DICOM-001 | HIGH | ⚠️ Validation not specified |
| **INTEGRITY-03** | Audit trail non-sequential | WORKFLOW-001 | MEDIUM | ✅ Journal specified |

---

## 6. Missing Documentation

### 6.1 User Stories Not Covered

**US-DICOM-01: PACS Failure Recovery**
- **Description**: Operator workflow when PACS is unavailable
- **Gap**: No SPEC defines degraded mode operation
- **Impact**: Clinical workflow interruption
- **Recommendation**: Add to WORKFLOW-001 or create SPEC-RESILIENCE-001

**US-DOSE-01: Dose Report Generation**
- **Description**: Operator requests printable dose report
- **Gap**: Mentioned in DOSE-001 but not detailed
- **Impact**: Regulatory compliance gap
- **Recommendation**: Expand DOSE-001 section FR-DOSE-08

**US-UI-01: Multi-Monitor Configuration**
- **Description**: Configure which screens appear on which monitors
- **Gap**: Specified as optional in UI-001 but not detailed
- **Impact**: Deployment configuration complexity
- **Recommendation**: Create dedicated configuration guide

### 6.2 Edge Cases Not Specified

**Edge Case #1: Core Engine Crash During Exposure**
- **SPECs Affected**: WORKFLOW-001, IPC-001, DOSE-001
- **Current State**: Partially addressed in WORKFLOW-001 crash recovery
- **Gap**: No specification for dose recording after crash
- **Recommendation**: Define crash-state dose reconstruction

**Edge Case #2: Network Partition During DICOM Export**
- **SPECs Affected**: DICOM-001, WORKFLOW-001
- **Current State**: Retry queue specified in DICOM-001
- **Gap**: No operator notification workflow
- **Recommendation**: Add operator alert to WORKFLOW-001

**Edge Case #3: Detector Disconnect During Acquisition**
- **SPECs Affected**: HAL-001, WORKFLOW-001, UI-001
- **Current State**: Hardware status specified in HAL-001
- **Gap**: No workflow state machine transition
- **Recommendation**: Add transition to WORKFLOW-001 T-18 recovery

---

## 7. Implementation Risks by SPEC

### 7.1 SPEC-DICOM-001 Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| fo-dicom codec gap | Medium | High | Create codec validation plan |
| PACS conformance drift | Medium | Medium | Maintain conformance test suite |
| UID collision | Low | Critical | Document UID root allocation |
| Worklist timeout handling | High | Medium | Define emergency workflow fallback |

### 7.2 SPEC-DOSE-001 Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| DAP calibration coefficient drift | High | High | Define calibration validation schedule |
| RDSR validation failure | Medium | High | Integrate DVTK validation in CI |
| Missing patient context | Medium | Medium | Define holding buffer behavior |
| Audit trail corruption | Low | Critical | Implement hash-chain verification |

### 7.3 SPEC-WORKFLOW-001 Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| State machine invalid transition | Low | Critical | Implement guard matrix enforcement |
| Interlock timeout | Medium | Critical | Define fail-safe abort behavior |
| Journal corruption | Low | High | Implement journal recovery procedures |
| Multi-exposure context loss | Medium | High | Validate state persistence |

### 7.4 SPEC-UI-001 Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| MVVM architecture violation | Medium | Medium | Code review checklist |
| 16-bit rendering performance | Medium | Medium | GPU acceleration validation |
| Localization gaps | High | Low | Korean clinical review required |
| Accessibility non-compliance | Medium | High | IEC 62366 validation required |

### 7.5 SPEC-TEST-001 Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| HW simulator insufficient fidelity | High | High | Validation against physical hardware |
| DVTK license issue | Medium | Medium | Confirm commercial use license |
| Test coverage inflation | Medium | Medium | Exclude generated code from coverage |

---

## 8. Priority Recommendations

### 8.1 Immediate Actions (Before Implementation)

1. **Create Cross-SPEC Interface Specification**
   - Document all IPC message contracts between C++ and C#
   - Define RDSR integration between DOSE and DICOM
   - Specify error handling across process boundaries

2. **Validate Safety-Critical Assumptions**
   - Benchmark IPC latency under load
   - Validate interlock response time with hardware simulator
   - Confirm DAP calculation accuracy tolerance

3. **Close Missing Documentation Gaps**
   - Define PACS failure recovery workflow
   - Specify crash recovery dose reconstruction
   - Document multi-monitor configuration

### 8.2 Implementation Phase Actions

1. **Establish SPEC Integration Testing**
   - Create integration tests for each cross-SPEC interface
   - Validate message contracts between services
   - Test error propagation across boundaries

2. **Create Traceability Matrix**
   - Map all requirements to test cases
   - Map all test cases to requirements
   - Generate automated RTM reports

3. **Implement Safety Validation**
   - Create fault injection test suite
   - Validate all safety interlock chains
   - Test emergency workflows

### 8.3 Documentation Actions

1. **Create Architecture Decision Records**
   - Document gRPC service contract decisions
   - Record RDSR integration approach
   - Archive safety-critical design rationale

2. **Generate API Documentation**
   - Publish IPC service contracts
   - Document HAL interface specifications
   - Create integration guides for each service boundary

---

## 9. Traceability Gaps

### 9.1 Requirements to Implementation Traceability

**Status**: ⚠️ PARTIAL
- Completed SPECs have implementation references
- Planned SPECs lack traceability to implementation

**Gaps**:
- No unified traceability matrix across all SPECs
- Test cases not mapped to requirements in TEST-001
- Safety requirements not explicitly traced to implementation

### 9.2 Risk Control Traceability

**Status**: ⚠️ PARTIAL
- Safety interlocks specified in HAL-001 and WORKFLOW-001
- Risk control measures identified but not mapped to test validation

**Gaps**:
- No verification that all ISO 14971 risks have controls
- Missing validation evidence for risk control effectiveness
- No failure mode coverage analysis

---

## 10. Conclusion

The HnVue SPEC suite is well-structured with clear dependencies between 9 SPECs. The 4 completed SPECs (INFRA, IPC, HAL, IMAGING) provide a solid foundation. The 5 planned SPECs (DICOM, DOSE, WORKFLOW, UI, TEST) have comprehensive requirements but require:

1. **Interface Clarification**: Cross-SPEC message contracts need explicit documentation
2. **Safety Validation**: Safety-critical assumptions require verification
3. **Edge Case Coverage**: Missing edge cases need specification
4. **Traceability**: Unified requirements-to-test traceability matrix needed

**Overall Risk Assessment**: MEDIUM
- Safety-critical functions are well-specified
- Cross-SPEC integration is the primary risk area
- Testing infrastructure needs early validation

---

*Analysis completed: 2026-02-27*
*Next review: After DICOM, DOSE, WORKFLOW implementation*
