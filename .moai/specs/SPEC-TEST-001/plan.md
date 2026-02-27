# SPEC-TEST-001: Implementation Plan

## Metadata

| Field    | Value                                                           |
|----------|-----------------------------------------------------------------|
| SPEC ID  | SPEC-TEST-001                                                   |
| Title    | HnVue Testing Framework and V&V Strategy                        |
| Package  | `tests/` (entire test tree)                                     |
| Language | C++ 17 (GTest), C# 12 / .NET 8 (xUnit), Python 3.10+ (pytest)  |
| Library  | Google Test, xUnit, pytest, DVTK, Orthanc Docker                |

---

## 1. Milestones

### Primary Goal: Test Infrastructure Foundation

Establish the directory structure, CI pipeline integration, Docker Orthanc configuration, and HW simulator testbench as the foundation required by all subsequent test suites.

Components in scope:
- `tests/` directory reorganization per SPEC-TEST-001 §4.1 (resolve duplicate path issue)
- Docker Orthanc configuration with DICOM plugin support (conformance, interop, system instances)
- Python detector simulator (`tests/simulators/detector/`) — USB 3.x / PCIe protocol emulation
- Python generator simulator (`tests/simulators/generator/`) — serial protocol emulation with fault injection
- Synthetic DICOM test data generator (`tests/data/generators/`) — DX modality, anonymized Patient IDs
- CI pipeline stage definitions: Unit → Integration → DICOM → System → Coverage Gate → RTM Gate
- Pytest conftest.py with `@pytest.mark.requirement` and `@pytest.mark.safety_class` marker infrastructure

Acceptance Gate: Simulators start and respond to commands without errors; Orthanc container starts and accepts a test C-STORE; directory structure matches SPEC §4.1; CI pipeline runs all stages without configuration errors.

### Secondary Goal: Unit and Integration Test Completion

Migrate and complete all unit tests to the canonical `tests/unit/` directory structure, and implement the pytest integration test suite against the HW simulator testbench.

Components in scope:
- Migrate existing C++ tests (`tests/cpp/`) to `tests/unit/cpp/` with SPEC §4.1 subdirectory layout
- Migrate existing C# DICOM tests (`tests/csharp/HnVue.Dicom.Tests/`) to `tests/unit/csharp/`
- Resolve duplicate empty directory `tests/HnVue.Dicom.Tests/` (delete or repurpose)
- Implement pytest integration tests (`tests/integration/ipc/`, `tests/integration/hal_simulator/`, `tests/integration/pipeline/`)
- Coverage instrumentation: gcov/lcov for C++, Coverlet for C#, pytest-cov for Python
- JUnit XML output configuration for all test runners (CTest, dotnet test, pytest)
- Cobertura XML coverage report generation per component

Acceptance Gate: All existing tests pass in new directory structure; integration tests exercise IPC, HAL simulator, and image pipeline; coverage reports generated in Cobertura XML format.

### Final Goal: DICOM Conformance, V&V Documentation, and RTM

Implement DVTK-based DICOM conformance tests, system end-to-end tests, interoperability tests, and produce all regulatory documentation artifacts.

Components in scope:
- DVTK integration for C-STORE, C-FIND, C-MOVE, MWL conformance validation (`tests/dicom/conformance/`)
- System end-to-end tests covering complete clinical workflow (`tests/system/workflows/`)
- Multi-vendor PACS interoperability tests using three Orthanc configurations (`tests/interop/`)
- Requirements Traceability Matrix in CSV and HTML formats (`tests/traceability/`)
- IEC 62304 §5.5–§5.8 verification evidence document generation
- Usability test plan and protocol documents per IEC 62366-1 (`tests/usability/`)

Acceptance Gate: All FR-TEST-01 through FR-TEST-10 and NFR requirements verified. RTM achieves bidirectional traceability with zero untraceable requirements. DVTK validation produces zero Critical and zero Error violations for all IODs under test.

### Optional Goal: Extended Regulatory Documentation

Address open questions and extend documentation for regulatory submission:
- DICOM Conformance Statement final draft (OQ-01, OQ-03 dependent)
- Summative usability evaluation protocol for clinical radiographers (OQ-05 dependent)
- CI host specification documentation for the 5-minute unit test gate (OQ-02 dependent)

---

## 2. Technical Approach

### 2.1 Test Pyramid

The HnVue test strategy follows a standard test pyramid with regulatory compliance overlaid:

```
              [System Tests]
           Clinical workflow E2E
            pytest + simulators
          ─────────────────────────
         [Integration Tests]
       IPC + HAL simulator + pipeline
            pytest + conftest
       ───────────────────────────────
      [Unit Tests]
   C++: GTest | C#: xUnit | Python: pytest
  GTest (Class C: 100% decision, Class B: 85-90%)
  xUnit (Class B: 80-85%, Class A: 85%)
 ─────────────────────────────────────────────────
```

Unit tests form the largest layer, providing fast feedback (< 5 minutes total). Integration tests verify cross-layer IPC and HAL-to-simulator interfaces. System tests exercise complete clinical workflows with Docker Orthanc and all simulators active.

The DICOM conformance layer (DVTK + Orthanc) runs alongside the integration/system layers and gates on protocol compliance independently of functional test results.

### 2.2 HW Simulator Architecture

Both simulators are Python 3.10+ programs with JSON configuration interfaces, state machine implementations, and a fault injection API:

**Detector Simulator** (`tests/simulators/detector/`):
- State machine: IDLE → ARMING → READY → ACQUIRING → TRANSFERRING → IDLE
- Protocol: Named pipe or socket interface emulating USB 3.x bulk transfer
- Image generation: Flat field, grid pattern, and configurable pixel parameters via JSON
- Fault injection: USB disconnect, acquisition timeout, partial DMA, register read failure

**Generator Simulator** (`tests/simulators/generator/`):
- State machine: STANDBY → READY → PREPARING → EXPOSING → COMPLETE → STANDBY
- Protocol: Virtual COM port or socket emulating serial HVG protocol
- Command set: Set kV, Set mA, Set time, Arm, Expose, Abort, Status query
- Fault injection: Communication timeout, interlock fault, kV overshoot, mid-exposure abort

Both simulators log all command-response pairs with timestamps and do not require elevated OS privileges (NFR-TEST-03, FR-TEST-06.8).

### 2.3 DVTK Integration

DVTK (DICOM Validation Toolkit) is integrated as the primary DICOM conformance validator:
- Python bindings used via pytest for automated test execution
- Validates all produced IODs against DICOM PS 3.x definitions
- Results reported as pytest test outcomes, exported to JUnit XML
- DVTK license must be confirmed per OQ-01 (A-05) before any DVTK-dependent test is merged

### 2.4 Orthanc Docker Configuration

Three separate Orthanc instances serve distinct test purposes:
- **conformance**: Permissive peer acceptance, non-persistent, for DVTK conformance testing
- **interop**: Configured with three different AE Title and transfer syntax profiles to simulate vendor diversity
- **system**: Full plugin set (C-STORE SCP, C-FIND SCP, Storage Commitment), non-persistent, for E2E workflow tests

All instances use the `jodogne/orthanc-plugins:latest` Docker image and are orchestrated via Docker Compose with health checks gating test execution.

### 2.5 Requirements Traceability Matrix (RTM)

The RTM is machine-readable (CSV) and human-readable (HTML):
- CSV schema: `requirement_id`, `test_case_id`, `test_suite`, `safety_class`, `status`
- HTML report generated from CSV via a Python script in `tests/traceability/`
- RTM completeness is a blocking CI gate: all requirements must have at least one linked test case
- Every test case must reference at least one SPEC requirement ID (enforced by pytest marker validation)

### 2.6 Hybrid Development Methodology Application

Per `quality.yaml` (development_mode: hybrid):
- New test infrastructure files (simulators, conftest, generators): TDD — RED-GREEN-REFACTOR
- Existing test files being migrated (C++ GTest, C# xUnit): DDD — ANALYZE-PRESERVE-IMPROVE
- Characterization tests are written first for existing tests during migration to capture current passing behavior

---

## 3. Risks and Mitigations

| Risk                                                     | Likelihood | Impact | Mitigation                                                                          |
|----------------------------------------------------------|------------|--------|-------------------------------------------------------------------------------------|
| DVTK license incompatible with commercial medical device | Medium     | High   | Confirm license with Legal/QA (OQ-01) before implementing DVTK-dependent tests      |
| Orthanc Docker unavailable in CI network                 | Medium     | High   | Verify CI network access (OQ-06); fall back to embedded fo-dicom SCP for unit tests |
| Python HW simulators fail to emulate protocol edge cases | Medium     | Medium | Start with happy-path protocol; iteratively add edge cases as WORKFLOW tests reveal gaps |
| Duplicate directory path `tests/HnVue.Dicom.Tests/`     | High       | Medium | Delete empty directory and update all project references in first sprint             |
| CI unit test suite exceeds 5-minute gate                 | Low        | Medium | Separate slow tests with `@pytest.mark.slow`; enforce parallelism in CTest/dotnet   |
| RTM maintenance falls behind requirement changes         | Medium     | High   | Automate RTM generation from test metadata; enforce via CI gate (C-07)              |
| Class C component tests missing 100% decision coverage   | Low        | High   | Enforce with gcov/lcov threshold gate in CI; block merge on any Class C regression  |
| IPC protocol not finalized (OQ-04)                       | Medium     | Medium | Abstract IPC interface in integration test fixtures; switch implementation when decided |

---

## 4. Architecture Design Direction

### 4.1 Test Directory Structure

The canonical structure per SPEC §4.1 is enforced from the first milestone. Migration of existing tests happens in the Secondary Goal milestone:

```
tests/
├── unit/
│   ├── cpp/                        # GTest suites (migrated from tests/cpp/)
│   │   ├── hal/
│   │   ├── generator/              # Class C: 100% decision coverage
│   │   ├── aec/                    # Class C: 100% decision coverage
│   │   ├── imaging/
│   │   ├── dicom/
│   │   └── dose/
│   └── csharp/                     # xUnit suites (migrated from tests/csharp/)
│       ├── ui/
│       └── patient/
├── integration/
│   ├── ipc/
│   ├── hal_simulator/
│   └── pipeline/
├── system/
│   ├── workflows/
│   └── regression/
├── dicom/
│   ├── conformance/
│   ├── scu/
│   └── scp/
├── usability/
│   ├── plans/
│   ├── protocols/
│   └── results/
├── simulators/
│   ├── detector/
│   ├── generator/
│   └── pacs/
├── interop/
│   ├── pacs_vendor_a/
│   ├── pacs_vendor_b/
│   └── pacs_vendor_c/
├── data/
│   ├── generators/
│   └── fixtures/
└── traceability/
    ├── rtm.csv
    └── rtm_report.html
```

### 4.2 Test Metadata Enforcement

A shared pytest conftest.py in `tests/` enforces:
- `@pytest.mark.requirement("FR-TEST-xx.x")` — mandatory on all pytest test functions
- `@pytest.mark.safety_class("B")` or `("C")` — mandatory for all HW-adjacent tests
- CI gate: any test missing `requirement` marker fails the RTM Gate stage

For GTest, test suite names encode the safety class: `GeneratorControl_ClassC_`. For xUnit, `[Trait("Requirement", "FR-XX-01")]` attributes are mandatory.

### 4.3 CI Pipeline Stage Definitions

| Stage         | Tests Executed                          | Gate Condition                                    | Artifact                          |
|---------------|-----------------------------------------|---------------------------------------------------|-----------------------------------|
| Unit          | GTest (C++), xUnit (C#)                 | All pass; coverage >= class threshold             | junit-unit.xml, coverage.xml      |
| Integration   | pytest integration suite                | All pass; simulators healthy                      | junit-integration.xml             |
| DICOM         | DVTK + Orthanc conformance              | Zero Critical, zero Error violations              | junit-dicom.xml, dvtk-report      |
| System        | pytest end-to-end workflows             | All workflows complete                            | junit-system.xml                  |
| Regression    | Full suite replay                       | No new failures vs. baseline                      | regression-delta.xml              |
| Coverage Gate | Coverage aggregation                    | Per-component thresholds met (SPEC §1.3)         | coverage-summary.html             |
| RTM Gate      | RTM completeness check                  | All requirements traced; all tests traced         | rtm-report.html                   |

---

## 5. Dependency Order

1. Resolve duplicate directory: delete `tests/HnVue.Dicom.Tests/` (empty), keep `tests/csharp/HnVue.Dicom.Tests/`
2. Create canonical `tests/` directory tree per §4.1
3. Implement Docker Orthanc configuration (three instances) and verify CI network access (OQ-06)
4. Implement Python detector simulator (happy path, then fault injection)
5. Implement Python generator simulator (happy path, then fault injection)
6. Implement synthetic DICOM test data generator (`tests/data/generators/`)
7. Configure CI pipeline stages and JUnit XML output for all runners
8. Migrate existing C++ GTest tests to `tests/unit/cpp/` with ANALYZE-PRESERVE-IMPROVE
9. Migrate existing C# xUnit tests to `tests/unit/csharp/` with ANALYZE-PRESERVE-IMPROVE
10. Implement pytest integration tests against HW simulator testbench
11. Implement DVTK conformance tests (`tests/dicom/conformance/`)
12. Implement system end-to-end workflow tests (`tests/system/workflows/`)
13. Implement interoperability tests (`tests/interop/`)
14. Generate RTM CSV and HTML; enforce RTM Gate in CI
15. Author IEC 62304 §5.5–§5.8 verification evidence documents
16. Author usability test plans and protocols (`tests/usability/`)
