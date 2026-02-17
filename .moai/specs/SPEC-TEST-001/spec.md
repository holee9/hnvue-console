# SPEC-TEST-001: HnVue Testing Framework and V&V Strategy

## Metadata

| Field        | Value                                         |
|--------------|-----------------------------------------------|
| SPEC ID      | SPEC-TEST-001                                 |
| Title        | HnVue Testing Framework and V&V Strategy      |
| Product      | HnVue - Diagnostic Medical Device X-ray GUI Console SW |
| Status       | Planned                                       |
| Priority     | High                                          |
| Created      | 2026-02-17                                    |
| Lifecycle    | spec-anchored                                 |
| Assigned     | manager-ddd                                   |
| Regulatory   | IEC 62304, ISO 14971, ISO 13485, IEC 62366, MFDS/FDA |

---

## 1. Environment

### 1.1 System Context

HnVue is a diagnostic medical device X-ray GUI Console Software operating on a host PC. It interfaces with FPGA-based detectors via USB 3.x/PCIe, MCU-based generator controllers via serial protocol, and external systems via DICOM networking. The software is subject to regulatory requirements for medical device software under IEC 62304 Class B and Class C safety classifications.

The testing framework must operate without physical hardware, relying on software simulators and test doubles to maintain development velocity and enable continuous integration.

### 1.2 Technology Stack

| Layer          | Technology                        | Purpose                         |
|----------------|-----------------------------------|---------------------------------|
| Core Engine    | C++ (17 or later)                 | Image pipeline, HAL, real-time  |
| GUI Layer      | C# / WPF                          | Patient management, UI, viewer  |
| Unit Testing   | Google Test (C++), xUnit (C#)     | Component-level verification    |
| Integration    | Python pytest + HW simulator      | Cross-layer integration testing |
| DICOM Testing  | DVTK + Orthanc (Docker)           | DICOM conformance verification  |
| CI Reporting   | JUnit XML format                  | Pipeline artifact integration   |
| Build/CI       | CTest (C++ runner)                | CMake-native test orchestration |
| Containerization | Docker                          | Orthanc PACS, test isolation    |

### 1.3 Component Safety Classification

| Component              | IEC 62304 Class | Coverage Requirement     |
|------------------------|-----------------|--------------------------|
| Generator Control      | C               | 100% decision coverage   |
| AEC (Auto Exposure Control) | C          | 100% decision coverage   |
| Image Processing Pipeline | B            | 85% statement coverage   |
| DICOM Communication    | B               | 85% statement coverage   |
| Dose Management        | B               | 90% statement coverage   |
| UI / Viewer Layer      | B               | 80% statement coverage   |
| Patient Management     | A               | 85% statement coverage   |

### 1.4 Development Methodology

Per `.moai/config/sections/quality.yaml`: Hybrid mode.

- New code (new modules, new functions): TDD - RED-GREEN-REFACTOR cycle
- Legacy code modifications and refactoring: DDD - ANALYZE-PRESERVE-IMPROVE cycle
- All test frameworks apply to both new and existing code

### 1.5 Assumptions

| ID   | Assumption                                                                 | Confidence | Risk if Wrong                              |
|------|----------------------------------------------------------------------------|------------|--------------------------------------------|
| A-01 | Physical hardware (detector, generator) is NOT available during CI/CD      | High       | Test gaps if simulators are insufficient   |
| A-02 | Orthanc PACS Docker image is accessible in CI network environment          | High       | DICOM conformance tests cannot execute     |
| A-03 | Google Test and xUnit versions are compatible with the project build system | High       | Build system rework required               |
| A-04 | Python 3.10+ is available in CI for pytest and HW simulator scripts        | High       | Simulator testbench cannot execute         |
| A-05 | DVTK library license permits use in commercial medical device development  | Medium     | DICOM validation tool must be replaced     |
| A-06 | IEC 62304 Class C components are fully enumerated (Generator Control, AEC) | High       | Coverage gaps in safety-critical code      |
| A-07 | Regulatory submission requires JUnit XML as machine-readable evidence      | Medium     | Report format may require conversion       |

---

## 2. Requirements

### 2.1 Functional Requirements - Unit Testing Framework (FR-TEST-01)

**FR-TEST-01.1** The system shall provide Google Test (GTest) as the unit testing framework for all C++ components in the HnVue core engine.

**FR-TEST-01.2** The system shall provide xUnit as the unit testing framework for all C# components in the HnVue GUI layer.

**FR-TEST-01.3** The system shall provide pytest as the unit testing framework for all Python-based test utilities, simulator scripts, and data generation tools.

**FR-TEST-01.4 (EARS - Ubiquitous)** The system shall enforce that all new C++ source files have a corresponding GTest test file before the implementation file is merged.

**FR-TEST-01.5 (EARS - Ubiquitous)** The system shall enforce that all new C# source files have a corresponding xUnit test file before the implementation file is merged.

**FR-TEST-01.6 (EARS - Ubiquitous)** The system shall organize unit tests within the `tests/unit/` directory, mirroring the source directory structure.

**FR-TEST-01.7 (EARS - Event-Driven)** When a unit test for a Class C component fails, the system shall block the CI pipeline and require manual review before proceeding.

**FR-TEST-01.8 (EARS - Unwanted)** The system shall not permit unit tests to establish connections to physical hardware devices or external network services.

### 2.2 Functional Requirements - Integration Testing Framework (FR-TEST-02)

**FR-TEST-02.1 (EARS - Ubiquitous)** The system shall provide an integration testing framework that exercises the communication interfaces between the C++ core engine and the C# GUI layer via the defined IPC protocol (gRPC/Named Pipe/Shared Memory).

**FR-TEST-02.2 (EARS - Event-Driven)** When an integration test is initiated, the system shall automatically start all required software simulators (detector simulator, generator simulator) before executing test cases.

**FR-TEST-02.3 (EARS - Event-Driven)** When all integration tests complete, the system shall terminate all software simulators and report their final state in the test log.

**FR-TEST-02.4 (EARS - State-Driven)** While the HW simulator testbench is active, the system shall intercept all HAL (Hardware Abstraction Layer) function calls and route them to the Python-based mock objects.

**FR-TEST-02.5 (EARS - Ubiquitous)** The system shall organize integration tests within the `tests/integration/` directory.

**FR-TEST-02.6 (EARS - Unwanted)** The system shall not allow integration tests to modify production configuration files or calibration data stores.

### 2.3 Functional Requirements - System Testing Framework (FR-TEST-03)

**FR-TEST-03.1 (EARS - Ubiquitous)** The system shall provide an end-to-end system test framework that exercises complete clinical workflows (patient registration, image acquisition, image processing, DICOM storage, dose recording).

**FR-TEST-03.2 (EARS - Event-Driven)** When a system test is executed, the system shall launch the full HnVue application stack with all simulators and a local Orthanc PACS instance.

**FR-TEST-03.3 (EARS - State-Driven)** While a system test workflow is executing, the system shall capture and verify all inter-component messages to confirm protocol compliance.

**FR-TEST-03.4 (EARS - Ubiquitous)** The system shall organize system tests within the `tests/system/` directory.

**FR-TEST-03.5 (EARS - Unwanted)** The system shall not execute system tests in less than a fully initialized application state; partial startup states are prohibited.

### 2.4 Functional Requirements - DICOM Conformance Testing (FR-TEST-04)

**FR-TEST-04.1 (EARS - Ubiquitous)** The system shall provide DICOM conformance testing using DVTK (DICOM Validation Toolkit) to validate all DICOM service class implementations.

**FR-TEST-04.2 (EARS - Ubiquitous)** The system shall maintain an Orthanc PACS Docker instance as the reference DICOM server for conformance testing.

**FR-TEST-04.3 (EARS - Event-Driven)** When a DICOM conformance test is executed, the system shall validate the conformance of C-STORE, C-FIND, C-MOVE, and DICOM Modality Worklist (C-FIND MWL) service classes.

**FR-TEST-04.4 (EARS - Ubiquitous)** The system shall validate DICOM tags for mandatory attributes as defined in the DICOM Conformance Statement for each SOP class used by HnVue.

**FR-TEST-04.5 (EARS - Event-Driven)** When a DICOM object is sent by HnVue, the system shall verify that the pixel data encoding, photometric interpretation, and transfer syntax are compliant with the declared Conformance Statement.

**FR-TEST-04.6 (EARS - Ubiquitous)** The system shall organize DICOM conformance tests within the `tests/dicom/` directory.

**FR-TEST-04.7 (EARS - Unwanted)** The system shall not accept a DICOM conformance test result as passing if any mandatory DICOM attribute is absent or incorrectly encoded.

### 2.5 Functional Requirements - Usability Testing Framework (FR-TEST-05)

**FR-TEST-05.1 (EARS - Ubiquitous)** The system shall provide a usability test framework aligned with IEC 62366-1 requirements, supporting summative and formative usability evaluation.

**FR-TEST-05.2 (EARS - Ubiquitous)** The system shall maintain a Usability Test Plan document that maps each critical task to a test scenario with defined success criteria.

**FR-TEST-05.3 (EARS - Ubiquitous)** The system shall organize usability test artifacts within the `tests/usability/` directory, including test plans, protocols, and results records.

**FR-TEST-05.4 (EARS - Event-Driven)** When a usability test session is conducted, the system shall record task completion times, error rates, and subjective difficulty ratings for each critical task.

**FR-TEST-05.5 (EARS - Ubiquitous)** The system shall define critical tasks to include: patient registration, image acquisition initiation, image window/level adjustment, DICOM storage, and dose report access.

### 2.6 Functional Requirements - HW Simulator Testbench (FR-TEST-06)

**FR-TEST-06.1 (EARS - Ubiquitous)** The system shall provide a Python-based HW simulator testbench that emulates the FPGA-based X-ray detector and the MCU-based high-voltage generator (HVG).

**FR-TEST-06.2 (EARS - Ubiquitous)** The detector simulator shall implement the same USB 3.x / PCIe communication protocol as the physical detector, including register read/write and DMA image data transfer.

**FR-TEST-06.3 (EARS - Ubiquitous)** The generator simulator shall implement the same serial communication protocol as the physical HVG, including kV, mA, and exposure time commands and status responses.

**FR-TEST-06.4 (EARS - Event-Driven)** When the detector simulator receives an acquisition trigger command, it shall return a configurable synthetic X-ray image within the protocol-defined response time.

**FR-TEST-06.5 (EARS - Event-Driven)** When the generator simulator receives an exposure command, it shall transition through the correct state sequence (Ready, Preparing, Exposing, Complete) and report each state via the protocol.

**FR-TEST-06.6 (EARS - State-Driven)** While operating in fault injection mode, the generator simulator shall simulate protocol errors, timeout conditions, and hardware fault codes as configured by the test script.

**FR-TEST-06.7 (EARS - Ubiquitous)** The system shall organize the HW simulator testbench within the `tests/simulators/` directory.

**FR-TEST-06.8 (EARS - Unwanted)** The HW simulator testbench shall not require elevated OS privileges (administrator/root) for standard test execution.

### 2.7 Functional Requirements - Interoperability Testing (FR-TEST-07)

**FR-TEST-07.1 (EARS - Ubiquitous)** The system shall provide an interoperability test suite that validates HnVue DICOM communication with simulated multi-vendor PACS and HIS/RIS endpoints.

**FR-TEST-07.2 (EARS - Ubiquitous)** The interoperability test suite shall test against at least three simulated PACS configurations representing different vendor implementations of DICOM storage and query/retrieve.

**FR-TEST-07.3 (EARS - Event-Driven)** When an interoperability test is executed against a simulated PACS, the system shall validate that images stored by HnVue can be retrieved and displayed correctly by the simulated PACS viewer.

**FR-TEST-07.4 (EARS - Ubiquitous)** The system shall organize interoperability tests within the `tests/interop/` directory.

### 2.8 Functional Requirements - CI Reporting (FR-TEST-08)

**FR-TEST-08.1 (EARS - Ubiquitous)** The system shall generate JUnit XML format test reports for all automated test suites (unit, integration, system, DICOM).

**FR-TEST-08.2 (EARS - Event-Driven)** When a CI pipeline executes, the system shall produce a consolidated JUnit XML report aggregating results from all test suites.

**FR-TEST-08.3 (EARS - Ubiquitous)** The system shall include test suite name, test case name, execution time, result (pass/fail/skip), and failure message in each JUnit XML test entry.

**FR-TEST-08.4 (EARS - Ubiquitous)** The system shall generate a code coverage report in Cobertura XML format alongside each test execution report.

**FR-TEST-08.5 (EARS - Ubiquitous)** The system shall retain all test reports as CI pipeline artifacts for a minimum of 90 days for regulatory audit purposes.

### 2.9 Functional Requirements - Requirements Traceability Matrix (FR-TEST-09)

**FR-TEST-09.1 (EARS - Ubiquitous)** The system shall maintain a Requirements Traceability Matrix (RTM) that links each software requirement to one or more test cases.

**FR-TEST-09.2 (EARS - Ubiquitous)** The RTM shall cover bidirectional traceability: from requirement to test case, and from test case to requirement.

**FR-TEST-09.3 (EARS - Event-Driven)** When a new test case is created, the system shall enforce that it references at least one SPEC requirement identifier in its test metadata.

**FR-TEST-09.4 (EARS - Event-Driven)** When a new software requirement is added, the system shall flag the RTM as incomplete until at least one test case is linked to that requirement.

**FR-TEST-09.5 (EARS - Ubiquitous)** The RTM shall be maintained as a machine-readable artifact (CSV or JSON) within the `tests/traceability/` directory.

**FR-TEST-09.6 (EARS - Ubiquitous)** The system shall generate a human-readable RTM report in HTML format for inclusion in design verification documentation.

### 2.10 Functional Requirements - Synthetic Test Data Generation (FR-TEST-10)

**FR-TEST-10.1 (EARS - Ubiquitous)** The system shall provide a synthetic test data generation tool that creates DICOM-compliant test images without exposing real patient data.

**FR-TEST-10.2 (EARS - Ubiquitous)** The synthetic data generator shall produce DICOM files for all modality types supported by HnVue (DX - Digital Radiography).

**FR-TEST-10.3 (EARS - Event-Driven)** When generating synthetic DICOM data, the system shall populate all mandatory DICOM attributes with valid synthetic values and assign anonymized Patient IDs.

**FR-TEST-10.4 (EARS - Unwanted)** The synthetic data generator shall not embed real patient names, birth dates, or healthcare identifiers in generated test data.

**FR-TEST-10.5 (EARS - Ubiquitous)** The system shall organize synthetic test data and generation scripts within the `tests/data/` directory.

---

## 3. Non-Functional Requirements

**NFR-TEST-01 (Coverage - Minimum)** The system shall achieve a minimum of 85% statement coverage per module, measured per the safety classification table in Section 1.3.

**NFR-TEST-01.1 (Coverage - Class C)** The system shall achieve 100% decision coverage for all IEC 62304 Class C components (Generator Control, AEC) with no exceptions.

**NFR-TEST-02 (Performance - Unit Tests)** The complete unit test suite shall execute in under 5 minutes on the CI build host to maintain developer feedback velocity.

**NFR-TEST-03 (Hardware Independence)** The system shall execute all automated tests (unit, integration, system, DICOM) without requiring physical hardware; all hardware interfaces shall be replaced by software simulators.

**NFR-TEST-04 (Coverage - Class C Strict)** The system shall enforce that any merge request modifying a Class C component file does not reduce decision coverage below 100%.

**NFR-TEST-05 (Automation)** The system shall execute the complete regression test suite automatically on every CI pipeline trigger, with no manual intervention required.

**NFR-TEST-06 (Isolation)** Each test suite (unit, integration, system, DICOM, interop) shall execute in an isolated environment; test suite failures shall not affect other suites.

**NFR-TEST-07 (Repeatability)** The system shall produce deterministic, repeatable test results; non-deterministic tests (timing-dependent, order-dependent) are prohibited.

**NFR-TEST-08 (Traceability)** Every automated test case shall be traceable to at least one software requirement; untraceable tests are flagged as a quality gate violation.

---

## 4. Specifications

### 4.1 Directory Structure

```
tests/
├── unit/                       # FR-TEST-01: Unit tests
│   ├── cpp/                    # Google Test suites for C++ core engine
│   │   ├── hal/                # HAL unit tests (Class C: 100% decision)
│   │   ├── generator/          # Generator Control tests (Class C: 100% decision)
│   │   ├── aec/                # AEC tests (Class C: 100% decision)
│   │   ├── imaging/            # Image Processing tests (Class B: 85%)
│   │   ├── dicom/              # DICOM module tests (Class B: 85%)
│   │   └── dose/               # Dose Management tests (Class B: 90%)
│   └── csharp/                 # xUnit suites for C# GUI layer
│       ├── ui/                 # UI component tests (Class B: 80%)
│       └── patient/            # Patient Management tests (Class A: 85%)
├── integration/                # FR-TEST-02: Integration tests
│   ├── ipc/                    # IPC protocol integration tests
│   ├── hal_simulator/          # HAL-to-simulator integration tests
│   └── pipeline/               # Image pipeline integration tests
├── system/                     # FR-TEST-03: System (end-to-end) tests
│   ├── workflows/              # Clinical workflow tests
│   └── regression/             # Regression test suites
├── dicom/                      # FR-TEST-04: DICOM conformance tests
│   ├── conformance/            # DVTK-based conformance validation
│   ├── scu/                    # Service Class User tests
│   └── scp/                    # Service Class Provider tests
├── usability/                  # FR-TEST-05: Usability test artifacts
│   ├── plans/                  # IEC 62366 usability test plans
│   ├── protocols/              # Test protocols and instructions
│   └── results/                # Completed test result records
├── simulators/                 # FR-TEST-06: HW simulator testbench
│   ├── detector/               # FPGA detector mock (Python)
│   ├── generator/              # MCU/HVG mock (Python)
│   └── pacs/                   # PACS/HIS mock configuration
├── interop/                    # FR-TEST-07: Interoperability tests
│   ├── pacs_vendor_a/          # Simulated vendor A PACS
│   ├── pacs_vendor_b/          # Simulated vendor B PACS
│   └── pacs_vendor_c/          # Simulated vendor C PACS
├── data/                       # FR-TEST-10: Synthetic test data
│   ├── generators/             # Python data generation scripts
│   └── fixtures/               # Pre-generated DICOM test files
└── traceability/               # FR-TEST-09: Requirements traceability
    ├── rtm.csv                 # Machine-readable RTM
    └── rtm_report.html         # Human-readable RTM report
```

### 4.2 V&V Matrix

#### 4.2.1 Verification and Validation Coverage Matrix

| SPEC ID       | Software Requirement                          | Unit Test | Integration Test | System Test | DICOM Test | Regulatory Reference         |
|---------------|-----------------------------------------------|-----------|------------------|-------------|------------|------------------------------|
| SPEC-HAL-001  | Hardware Abstraction Layer interface          | GTest     | pytest           | -           | -          | IEC 62304 §5.5, §5.6         |
| SPEC-IMAGING-001 | Image Processing Pipeline                 | GTest     | pytest           | System      | -          | IEC 62304 §5.5, ISO 14971    |
| SPEC-DICOM-001 | DICOM Communication Services              | GTest     | pytest           | System      | DVTK       | IEC 62304 §5.5, DICOM PS 3.x |
| SPEC-DOSE-001 | Dose Management and RDSR                     | GTest     | pytest           | System      | DVTK       | IEC 62304 §5.5, IEC 60601-2-44 |
| SPEC-UI-001   | GUI and Viewer Layer                          | xUnit     | pytest           | System      | -          | IEC 62304 §5.5, IEC 62366    |
| SPEC-WORKFLOW-001 | Clinical Workflow Engine                 | GTest/xUnit | pytest         | System      | DVTK       | IEC 62304 §5.6, ISO 13485    |
| SPEC-IPC-001  | IPC Communication Layer                      | GTest/xUnit | pytest         | -           | -          | IEC 62304 §5.5               |
| SPEC-INFRA-001 | CI/CD and Build Infrastructure             | -         | pytest           | System      | -          | ISO 13485 §7.5               |

#### 4.2.2 IEC 62304 Compliance Mapping

| IEC 62304 Clause | Activity                             | Test Type           | Evidence Artifact                   |
|------------------|--------------------------------------|---------------------|-------------------------------------|
| §5.1 Software Development Planning | V&V plan definition | N/A        | This SPEC document                  |
| §5.5 Software Unit Testing        | Unit verification    | GTest / xUnit       | JUnit XML reports, coverage reports |
| §5.6 Software Integration Testing | Integration V&V      | pytest + simulators | JUnit XML reports, IPC logs         |
| §5.7 Software System Testing      | System V&V           | pytest end-to-end   | System test reports                 |
| §5.8 Release                      | Regression gate      | All suites          | CI pipeline artifacts               |
| §8.2 Problem Resolution           | Defect regression    | All suites          | Bug-linked test cases               |
| §9.4 Change Requests              | Change regression    | Targeted suites     | RTM delta reports                   |

#### 4.2.3 ISO 14971 Risk Control Verification Matrix

| Risk Control ID | Description                                | Verification Method    | Test Suite Location               |
|-----------------|--------------------------------------------|------------------------|-----------------------------------|
| RC-GEN-01       | Prevent over-exposure (kV limit enforcement) | Unit + Integration   | `tests/unit/cpp/generator/`       |
| RC-GEN-02       | Emergency stop command response time         | Integration + System | `tests/integration/hal_simulator/`|
| RC-AEC-01       | AEC dose limit enforcement                   | Unit + Integration   | `tests/unit/cpp/aec/`             |
| RC-IMG-01       | Image identification (Patient-Image link)    | Unit + System        | `tests/unit/cpp/imaging/`         |
| RC-DOSE-01      | Dose log completeness                        | Unit + System        | `tests/unit/cpp/dose/`            |
| RC-DICOM-01     | DICOM Patient ID integrity                   | Unit + DICOM         | `tests/dicom/conformance/`        |

### 4.3 HW Simulator Architecture

#### 4.3.1 Detector Simulator

The Python-based detector simulator (`tests/simulators/detector/`) shall implement the following behavioral model:

**State Machine:** IDLE -> ARMING -> READY -> ACQUIRING -> TRANSFERRING -> IDLE

**Protocol Simulation:**
- USB 3.x bulk transfer emulation via named pipe or socket interface
- Register map emulation: All detector registers accessible via read/write API
- DMA transfer simulation: Synthetic image data pushed on acquisition trigger
- Configurable synthetic image generation: Flat field, grid pattern, clinical phantom

**Fault Injection Interface:**
- Simulate USB disconnect/reconnect events
- Simulate acquisition timeout (no data returned)
- Simulate partial DMA transfer (corrupted frame)
- Simulate register read failure (return error code)

**Configuration Interface:**
- JSON-based configuration for image parameters (width, height, bit depth, pixel spacing)
- Configurable response latency to simulate timing variations
- Logging of all received commands and sent responses

#### 4.3.2 Generator (HVG) Simulator

The Python-based generator simulator (`tests/simulators/generator/`) shall implement the following behavioral model:

**State Machine:** STANDBY -> READY -> PREPARING -> EXPOSING -> COMPLETE -> STANDBY

**Protocol Simulation:**
- Serial port emulation via virtual COM port or socket
- Command set: Set kV, Set mA, Set time, Arm, Expose, Abort, Status query
- Status response encoding matching physical protocol specification
- Configurable kV/mA/time range enforcement

**Fault Injection Interface:**
- Simulate communication timeout
- Simulate hardware interlock fault
- Simulate kV overshoot / undershoot
- Simulate exposure abort mid-exposure

**Configuration Interface:**
- JSON-based configuration for generator model parameters
- Configurable dose output model for dose tracking tests
- Logging of all command-response pairs with timestamps

#### 4.3.3 PACS Simulator (Orthanc Docker)

The Orthanc PACS instance for DICOM testing shall be configured as follows:

- Docker image: `jodogne/orthanc-plugins:latest` with DICOM C-STORE, C-FIND, C-MOVE support
- Configuration: Permissive DICOM peer acceptance for conformance testing
- Separate instances for: conformance testing, interoperability testing, system testing
- Data persistence: Non-persistent (ephemeral) per test run to ensure test isolation

### 4.4 Test Metadata and Traceability Tags

Every test case in all test suites shall include metadata in the following format:

**For Google Test (C++):**
- Test suite name encodes component and safety class: `GeneratorControl_ClassC_`
- Test case names follow the pattern: `{Requirement_ID}_{Scenario}`
- Coverage markers in comments: `// COVERS: FR-TEST-02.4, RC-GEN-01`

**For xUnit (C#):**
- Test category attribute encoding requirement ID: `[Trait("Requirement", "FR-UI-01")]`
- Test display name includes scenario description
- Coverage markers in XML documentation comments

**For pytest:**
- Test marked with `@pytest.mark.requirement("FR-TEST-02.1")`
- Test marked with `@pytest.mark.safety_class("B")` or `@pytest.mark.safety_class("C")`
- Test file docstring contains list of covered requirements

### 4.5 CI Pipeline Integration

#### 4.5.1 Pipeline Stages

| Stage          | Tests Executed                           | Gate Condition                              | Artifact                        |
|----------------|------------------------------------------|---------------------------------------------|---------------------------------|
| Unit           | GTest (C++), xUnit (C#)                 | All pass, coverage >= class threshold       | junit-unit.xml, coverage.xml    |
| Integration    | pytest integration suite                 | All pass, simulators healthy                | junit-integration.xml           |
| DICOM          | DVTK + Orthanc conformance              | All conformance checks pass                 | junit-dicom.xml, dvtk-report    |
| System         | pytest end-to-end workflows              | All workflows complete                       | junit-system.xml                |
| Regression     | Full suite replay                        | No new failures vs. baseline                | regression-delta.xml            |
| Coverage Gate  | Coverage aggregation                     | Per-component thresholds met (Section 1.3)  | coverage-summary.html           |
| RTM Gate       | RTM completeness check                   | All requirements traced, all tests traced   | rtm-report.html                 |

#### 4.5.2 JUnit XML Report Structure

Each test report shall conform to the JUnit XML schema with the following fields per test case:

- `classname`: `{Component}.{SafetyClass}.{TestSuite}`
- `name`: `{RequirementID}_{ScenarioDescription}`
- `time`: execution time in seconds (float)
- `status`: `pass` | `fail` | `skip`
- `failure`: failure message with stack trace on failure
- `properties`: requirement IDs, safety class, component name

### 4.6 IEC 62304 Compliance Architecture

#### 4.6.1 Software Verification Plan

The HnVue testing framework implements the Software Verification Plan required by IEC 62304 §5.1.11 through the following structure:

**Unit Verification (§5.5):**
- All Class C units: 100% decision coverage verified by GTest with gcov/lcov measurement
- All Class B units: 85-90% statement coverage verified by GTest/xUnit
- All Class A units: 85% statement coverage verified by xUnit
- Evidence: JUnit XML + Cobertura coverage XML per CI run

**Integration Verification (§5.6):**
- All IPC interfaces tested with both valid and invalid message scenarios
- All HAL interfaces tested via simulator testbench
- Evidence: pytest integration JUnit XML, IPC message logs

**System Testing (§5.7):**
- End-to-end clinical workflow tests covering all documented use cases
- Regression baseline established after each release
- Evidence: pytest system JUnit XML, workflow trace logs

#### 4.6.2 Validation Plan (IEC 62366 Usability)

Usability validation for HnVue follows IEC 62366-1 and is documented in `tests/usability/`:

- Formative evaluations: During design iteration (not regulatory evidence)
- Summative evaluation: Final clinical user testing with representative users
- Critical tasks evaluated: Patient registration, image acquisition, dose review
- Acceptance criteria: Task success rate >= 95%, critical error rate = 0%

### 4.7 Regulatory Documentation Requirements

#### 4.7.1 Required Software V&V Documents

| Document                              | Source                          | Regulatory Reference           |
|---------------------------------------|---------------------------------|--------------------------------|
| Software Verification Plan            | This SPEC + test plans          | IEC 62304 §5.1.11              |
| Software Unit Test Records            | GTest/xUnit JUnit XML           | IEC 62304 §5.5.5               |
| Software Integration Test Records     | pytest JUnit XML                | IEC 62304 §5.6.7               |
| Software System Test Records          | pytest JUnit XML                | IEC 62304 §5.7.4               |
| DICOM Conformance Statement           | DVTK validation + documentation | DICOM PS 3.2                   |
| Requirements Traceability Matrix      | RTM CSV/HTML                    | IEC 62304 §5.1.1, ISO 13485 §7 |
| Risk Control Verification Evidence   | Linked test records             | ISO 14971 §8                   |
| Usability Validation Report           | Summative test results          | IEC 62366-1 §5.9               |
| Software Release Record               | CI pipeline final report        | IEC 62304 §5.8.8               |

---

## 5. Constraints

**C-01 (Regulatory):** All test records must be retained in non-modifiable form for the device lifetime plus 2 years as required by ISO 13485 §4.2.5 and applicable MFDS/FDA guidance.

**C-02 (Hardware Independence):** No automated test (unit, integration, system, DICOM) may require physical detector or generator hardware to execute.

**C-03 (Coverage Enforcement):** The CI pipeline shall block merge of any commit that reduces coverage below the defined threshold for the modified component's safety class.

**C-04 (Patient Data):** No real patient data (name, date of birth, ID, images) may be used in any test fixture or synthetic data set.

**C-05 (Tool Qualification):** Test tools used for safety-critical verification (GTest, xUnit, DVTK) shall be documented under IEC 62304 §9.8 SOUP management with version, known anomaly, and risk assessment records.

**C-06 (Reproducibility):** All automated tests must be deterministic; any test dependent on system clock, random seed, or external network state must use controlled mocking.

**C-07 (Traceability):** The RTM must be updated within one sprint of adding, modifying, or deleting any software requirement or test case.

---

## 6. Traceability

| SPEC ID         | Requirement             | Source Requirement | Test Location                              |
|-----------------|-------------------------|--------------------|--------------------------------------------|
| FR-TEST-01      | Unit test frameworks    | IEC 62304 §5.5     | `tests/unit/`                              |
| FR-TEST-02      | Integration framework   | IEC 62304 §5.6     | `tests/integration/`                       |
| FR-TEST-03      | System test framework   | IEC 62304 §5.7     | `tests/system/`                            |
| FR-TEST-04      | DICOM conformance       | DICOM PS 3.x       | `tests/dicom/`                             |
| FR-TEST-05      | Usability framework     | IEC 62366-1        | `tests/usability/`                         |
| FR-TEST-06      | HW simulator testbench  | NFR-TEST-03        | `tests/simulators/`                        |
| FR-TEST-07      | Interoperability tests  | ISO 13485          | `tests/interop/`                           |
| FR-TEST-08      | JUnit XML reporting     | MFDS/FDA SW V&V    | CI pipeline artifacts                      |
| FR-TEST-09      | Traceability matrix     | IEC 62304 §5.1.1   | `tests/traceability/`                      |
| FR-TEST-10      | Synthetic test data     | C-04               | `tests/data/`                              |
| NFR-TEST-01     | 85% coverage minimum    | IEC 62304 §5.5     | Coverage gate in CI                        |
| NFR-TEST-01.1   | 100% Class C decision   | IEC 62304 §5.5     | GTest + lcov enforcement                   |
| NFR-TEST-02     | Unit tests < 5 min      | Developer velocity | CI pipeline time gate                      |
| NFR-TEST-03     | No physical HW          | C-02               | Simulator testbench (FR-TEST-06)           |
| NFR-TEST-04     | Class C merge gate      | IEC 62304 §5.5     | CI pipeline merge protection               |
| NFR-TEST-05     | Automated regression    | ISO 13485          | CI pipeline (all stages)                   |

---

## 7. Open Questions

| ID   | Question                                                                                     | Owner       | Priority |
|------|----------------------------------------------------------------------------------------------|-------------|----------|
| OQ-01 | Has DVTK license been verified for commercial medical device use?                           | Legal/QA    | High     |
| OQ-02 | What is the target CI host specification (CPU, RAM) for the unit test 5-minute gate?        | DevOps      | Medium   |
| OQ-03 | Are MFDS submission requirements for SW V&V documentation identical to FDA 510(k) format?   | Regulatory  | High     |
| OQ-04 | Which IPC protocol (gRPC, Named Pipe, Shared Memory) has been finalized for C++/C# bridge?  | Architect   | High     |
| OQ-05 | Will the summative usability evaluation use actual clinical radiographers or simulation users? | Clinical  | Medium   |
| OQ-06 | Is the Orthanc Docker image permitted in the target production/CI network environment?       | DevOps      | Medium   |

---

*SPEC-TEST-001 created 2026-02-17. Regulatory references: IEC 62304:2006+AMD1:2015, ISO 14971:2019, ISO 13485:2016, IEC 62366-1:2015+AMD1:2020.*
