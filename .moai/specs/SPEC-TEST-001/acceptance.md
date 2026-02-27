# SPEC-TEST-001: Acceptance Criteria

## Metadata

| Field    | Value                                          |
|----------|------------------------------------------------|
| SPEC ID  | SPEC-TEST-001                                  |
| Title    | HnVue Testing Framework and V&V Strategy       |
| Format   | Given-When-Then (Gherkin-style)                |

---

## Definition of Done

A requirement is considered complete when:
1. All acceptance scenarios for that requirement pass in the automated test suite or are verified by document review
2. The implementation is traceable to this SPEC in the RTM
3. Coverage thresholds are met for the affected safety class (Class C: 100% decision, Class B: 85–90%, Class A: 85%)
4. No test case exists without a `requirement` metadata marker referencing at least one SPEC ID
5. JUnit XML and Cobertura XML artifacts are generated and retained for at least 90 days in the CI pipeline

---

## AC-01: Unit Testing Framework (FR-TEST-01)

### Scenario 1.1 - GTest Enforced for All New C++ Source Files

```
Given a developer creates a new C++ source file in the core engine
When a pull request is submitted without a corresponding GTest test file
Then the CI pipeline rejects the pull request
  And the rejection message identifies the missing test file path
  And no manual override is permitted for Class C components
```

### Scenario 1.2 - xUnit Enforced for All New C# Source Files

```
Given a developer creates a new C# source file in the GUI layer
When a pull request is submitted without a corresponding xUnit test file
Then the CI pipeline rejects the pull request
  And the rejection message identifies the missing test file path
```

### Scenario 1.3 - Unit Tests Execute Without Hardware

```
Given a CI build host with no physical detector or generator attached
When the complete unit test suite is executed (GTest + xUnit)
Then all tests pass without requiring any physical hardware connection
  And no test attempts to open a USB, PCIe, or serial port to a physical device
  And all HAL calls are intercepted by mock objects
```

### Scenario 1.4 - Class C Failure Blocks Pipeline

```
Given a GTest test case covering a Class C component (Generator Control or AEC)
When that test case fails
Then the CI pipeline is blocked at the Unit stage
  And no subsequent stage (Integration, DICOM, System) executes
  And a manual review is required before the pipeline can proceed
```

### Scenario 1.5 - Unit Test Suite Completes Within 5 Minutes

```
Given the complete unit test suite (GTest C++ + xUnit C#)
  And the CI build host meeting the specified hardware requirements (OQ-02)
When all unit tests are executed in sequence
Then the total elapsed time is under 300 seconds
  And each individual test case completes within 30 seconds
```

### Scenario 1.6 - Unit Tests Organized in Canonical Directory

```
Given a C++ test file located outside of tests/unit/cpp/
When the CI RTM Gate stage executes
Then the test file is flagged as incorrectly placed
  And the gate fails with a directory structure violation message
```

---

## AC-02: Integration Testing Framework (FR-TEST-02)

### Scenario 2.1 - Simulators Auto-Start Before Integration Tests

```
Given the pytest integration test suite is invoked
When test session setup begins
Then the detector simulator and generator simulator are launched automatically
  And the pytest conftest.py waits until both simulators report READY state
  And integration test cases only execute after both simulators are confirmed running
```

### Scenario 2.2 - Simulators Auto-Terminate After Integration Tests

```
Given all integration tests have completed (pass or fail)
When the pytest session teardown executes
Then both simulators are terminated
  And the simulator final state (IDLE for detector, STANDBY for generator) is recorded in the test log
  And no simulator process remains running after teardown
```

### Scenario 2.3 - HAL Calls Routed to Python Mock During Integration Test

```
Given an integration test exercising the image acquisition pipeline
While the HW simulator testbench is active
Then all HAL function calls (detector read, register write, DMA transfer) are intercepted
  And routed to the Python detector simulator via the defined interface
  And the test receives synthetic image data from the simulator, not from physical hardware
```

### Scenario 2.4 - Integration Tests Do Not Modify Production Config

```
Given an integration test that exercises configuration-dependent behavior
When the integration test executes
Then no production configuration file is modified
  And no calibration data store is written to
  And all configuration changes are applied to isolated test-scope fixtures only
```

---

## AC-03: System Testing Framework (FR-TEST-03)

### Scenario 3.1 - System Test Launches Full Application Stack

```
Given a system test for the patient-to-PACS clinical workflow
When the system test is initiated
Then the full HnVue application stack launches with:
    The detector simulator running
    The generator simulator running
    An Orthanc PACS Docker container running and healthy
  And all components are confirmed ready before the first test step executes
```

### Scenario 3.2 - Inter-Component Messages Captured and Verified

```
Given a running system test executing the image acquisition workflow
While the system test workflow is executing
Then all inter-component messages (IPC commands, DICOM C-STORE, MPPS N-CREATE) are captured
  And each message is verified against the protocol specification
  And any protocol violation causes the test case to fail immediately
```

### Scenario 3.3 - Partial Startup State Prohibited

```
Given a system test that is invoked before the application stack is fully initialized
When the system test detects that any required component is not in READY state
Then the system test aborts with a precondition failure error
  And no test case steps execute against the partially initialized stack
  And the failure message identifies which component failed to reach READY state
```

---

## AC-04: DICOM Conformance Testing (FR-TEST-04)

### Scenario 4.1 - C-STORE Conformance Validated by DVTK

```
Given a DX image produced by the HnVue DICOM service and stored in Orthanc
When DVTK DicomValidator validates the stored DICOM object
Then the validation result contains zero Critical violations
  And the validation result contains zero Error violations
  And all Type 1 (mandatory) attributes are present and non-zero length
```

### Scenario 4.2 - C-FIND MWL Conformance Validated

```
Given an Orthanc instance with the worklist plugin configured and three scheduled procedures loaded
When HnVue issues a C-FIND Modality Worklist request
Then the C-FIND request dataset is validated by DVTK against the MWL SOP class definition
  And the response datasets pass DVTK mandatory attribute checks
  And the query completes with Success status (0x0000)
```

### Scenario 4.3 - Mandatory DICOM Attribute Absence Fails Test

```
Given a DICOM object produced by HnVue with a mandatory attribute intentionally absent (fault injection)
When the DICOM conformance test validates the object
Then the test fails and is not reported as passing
  And the failure message identifies the absent attribute by tag (xxxx,xxxx) and name
  And the conformance test result is not accepted as evidence for the affected DVTK check
```

### Scenario 4.4 - Pixel Data Encoding and Transfer Syntax Verified

```
Given a DX image transmitted by HnVue with JPEG 2000 Lossless transfer syntax
When the DICOM conformance test validates the transmitted object
Then the pixel data encoding is confirmed as JPEG 2000 Lossless (1.2.840.10008.1.2.4.90)
  And the Photometric Interpretation matches the declared Conformance Statement
  And DVTK reports zero violations for pixel data encoding
```

---

## AC-05: Usability Testing Framework (FR-TEST-05)

### Scenario 5.1 - Usability Test Plan Exists and Is Complete

```
Given the tests/usability/plans/ directory
When a reviewer checks for the IEC 62366-1 Usability Test Plan
Then a plan document exists that maps each critical task to a test scenario
  And each scenario has defined success criteria (task completion rate, error rate, time limit)
  And the critical tasks covered include:
      Patient registration
      Image acquisition initiation
      Image window/level adjustment
      DICOM storage
      Dose report access
```

### Scenario 5.2 - Usability Test Session Records Required Metrics

```
Given a usability test session is conducted with a representative user
When the session executor records the results
Then task completion time is recorded for each critical task
  And error count is recorded for each critical task
  And subjective difficulty rating is recorded for each critical task
  And all records are saved in tests/usability/results/
```

### Scenario 5.3 - Summative Evaluation Acceptance Criterion

```
Given the summative usability evaluation results for all critical tasks
When the results are reviewed against the acceptance criteria
Then the task success rate is at or above 95% across all critical tasks
  And the critical error rate is 0% (zero critical errors observed)
  And the results are documented in tests/usability/results/ for regulatory submission
```

---

## AC-06: HW Simulator Testbench (FR-TEST-06)

### Scenario 6.1 - Detector Simulator Responds to Acquisition Trigger

```
Given the detector simulator is running in READY state
When an acquisition trigger command is sent via the emulated USB 3.x interface
Then the simulator transitions through ACQUIRING and TRANSFERRING states
  And returns a configurable synthetic X-ray image within the protocol-defined response time
  And the returned image conforms to the configured parameters (width, height, bit depth)
  And the simulator returns to IDLE state after transfer completes
```

### Scenario 6.2 - Generator Simulator Executes Full Exposure Sequence

```
Given the generator simulator is in STANDBY state
When an Arm command followed by an Expose command is sent via the emulated serial interface
Then the simulator transitions: STANDBY -> READY -> PREPARING -> EXPOSING -> COMPLETE
  And each state transition is reported via the protocol status response
  And the COMPLETE state is reached without error
  And the simulator returns to STANDBY after the exposure cycle
```

### Scenario 6.3 - Generator Simulator Fault Injection Produces Expected Errors

```
Given the generator simulator is configured in fault injection mode with hardware interlock fault
While operating in fault injection mode
Then when an Arm command is received, the simulator returns an interlock fault status code
  And the fault code matches the physical protocol specification for interlock errors
  And the simulator state machine does not advance beyond READY
```

### Scenario 6.4 - Simulators Run Without Elevated Privileges

```
Given a CI build host running as a standard non-administrator user
When the detector simulator and generator simulator are started
Then both simulators start successfully without requiring elevation
  And no UAC prompt (Windows) or sudo invocation (Linux) is required
  And all inter-process communication uses unprivileged mechanisms (named pipes, TCP loopback)
```

---

## AC-07: Interoperability Testing (FR-TEST-07)

### Scenario 7.1 - Three Simulated PACS Configurations Tested

```
Given three Orthanc instances configured to simulate different PACS vendor profiles
  (pacs_vendor_a: permissive transfer syntax, pacs_vendor_b: strict Explicit VR LE only, pacs_vendor_c: MWL + Storage Commitment)
When an interoperability test executes a C-STORE to each PACS configuration
Then the C-STORE succeeds against all three configurations
  And for each stored object, the PACS viewer can retrieve and display it correctly
  And the interop test reports pass for each vendor configuration
```

### Scenario 7.2 - Stored Images Retrievable by Simulated PACS

```
Given HnVue has stored a DX image via C-STORE to a simulated PACS (pacs_vendor_a)
When the simulated PACS viewer issues a C-FIND followed by a C-MOVE to retrieve the image
Then the image is retrieved successfully
  And the retrieved image pixel data is byte-identical to the transmitted image
  And the retrieval completes within the configured timeout
```

---

## AC-08: CI Reporting (FR-TEST-08)

### Scenario 8.1 - JUnit XML Generated for All Test Suites

```
Given a complete CI pipeline execution (Unit + Integration + DICOM + System stages)
When all stages complete
Then a separate JUnit XML file is produced for each stage:
    junit-unit.xml
    junit-integration.xml
    junit-dicom.xml
    junit-system.xml
  And each file conforms to the JUnit XML schema (testsuites/testsuite/testcase)
```

### Scenario 8.2 - JUnit XML Entries Include Mandatory Fields

```
Given a JUnit XML test report produced by any test stage
When the report is parsed
Then each testcase element contains:
    classname in the format {Component}.{SafetyClass}.{TestSuite}
    name in the format {RequirementID}_{ScenarioDescription}
    time as a float value in seconds
    status as pass, fail, or skip
  And failed testcases include a failure element with message and stack trace
```

### Scenario 8.3 - Cobertura XML Coverage Report Generated

```
Given a test execution for any component (C++, C#, or Python)
When the test run completes
Then a Cobertura XML coverage report is generated alongside the JUnit XML report
  And the report includes per-file and per-class coverage metrics
  And the report is retained as a CI artifact for at least 90 days
```

### Scenario 8.4 - Test Reports Retained 90 Days

```
Given a CI pipeline execution that produced JUnit XML and Cobertura XML artifacts
When 90 days have elapsed since the pipeline ran
Then the artifacts are still accessible in the CI artifact store
  And the artifacts have not been automatically purged before the 90-day retention period
```

---

## AC-09: Requirements Traceability Matrix (FR-TEST-09)

### Scenario 9.1 - RTM Bidirectional Traceability

```
Given the RTM CSV file at tests/traceability/rtm.csv
When the RTM is parsed and analyzed
Then every SPEC requirement ID has at least one linked test case ID (forward traceability)
  And every test case ID in the RTM references at least one valid SPEC requirement ID (backward traceability)
  And the RTM contains no orphaned requirements (requirements with zero test cases)
  And the RTM contains no orphaned test cases (test cases with no requirement reference)
```

### Scenario 9.2 - New Test Case Without Requirement Reference Fails Gate

```
Given a new pytest test function is created without a @pytest.mark.requirement marker
When the RTM Gate CI stage executes
Then the gate fails
  And the failure message identifies the test function name and file path
  And the pipeline blocks the merge
```

### Scenario 9.3 - New Requirement Without Test Case Flags RTM Incomplete

```
Given a new SPEC requirement FR-TEST-XX.Y is added to spec.md
When the RTM Gate CI stage executes
Then the gate detects that FR-TEST-XX.Y has no linked test case in rtm.csv
  And the gate fails with a message identifying FR-TEST-XX.Y as untested
  And the pipeline blocks the merge until at least one test case references FR-TEST-XX.Y
```

### Scenario 9.4 - RTM HTML Report Generated and Readable

```
Given the RTM CSV file at tests/traceability/rtm.csv is up to date
When the RTM report generation script executes
Then tests/traceability/rtm_report.html is created
  And the HTML report displays each requirement with its linked test cases
  And the HTML report displays each test case with its linked requirement IDs
  And the report is suitable for inclusion in design verification documentation
```

---

## AC-10: Synthetic Test Data Generation (FR-TEST-10)

### Scenario 10.1 - Synthetic DX DICOM File Generated

```
Given the synthetic data generator script in tests/data/generators/
When the generator is executed with DX modality parameters
Then a DICOM file is produced in tests/data/fixtures/
  And the file passes DVTK validation with zero Critical and zero Error violations
  And all mandatory DICOM attributes for the DX SOP class are present
```

### Scenario 10.2 - No Real Patient Data in Generated Files

```
Given a DICOM file produced by the synthetic data generator
When the file is parsed and all patient attribute tags are extracted
Then tag (0010,0010) Patient Name does not contain a real person's name
  And tag (0010,0020) Patient ID is an anonymized synthetic identifier
  And tag (0010,0030) Patient Birth Date does not correspond to a real patient record
  And tag (0010,0040) Patient Sex is populated with a synthetic value (M, F, or O)
```

### Scenario 10.3 - Generator Assigns Valid Anonymized Patient IDs

```
Given the synthetic data generator configured to produce 10 DICOM test files
When the generator executes
Then each file has a unique anonymized Patient ID assigned
  And no two files share the same Patient ID
  And each Patient ID conforms to a defined anonymization pattern (e.g., TEST-XXXX)
```

---

## AC-11: Coverage Thresholds (NFR-TEST-01, NFR-TEST-01.1)

### Scenario 11.1 - Class C Decision Coverage Enforced at 100%

```
Given a C++ source file belonging to a Class C component (Generator Control or AEC)
When the GTest suite executes and lcov measures decision coverage
Then the decision coverage for that file is 100%
  And if coverage is below 100%, the Coverage Gate CI stage fails
  And the failure message identifies the file, uncovered branches, and line numbers
```

### Scenario 11.2 - Class B Statement Coverage Enforced at 85%+

```
Given a C++ or C# source file belonging to a Class B component (Image Processing, DICOM, Dose, UI)
When the test suite executes and coverage is measured
Then the statement coverage for that file meets or exceeds the class-specific threshold:
    Image Processing Pipeline: >= 85%
    DICOM Communication: >= 85%
    Dose Management: >= 90%
    UI/Viewer Layer: >= 80%
  And if any threshold is not met, the Coverage Gate CI stage fails
```

### Scenario 11.3 - Class C Merge Gate Blocks Coverage Regression

```
Given a pull request that modifies a Class C component file
When the CI pipeline evaluates the Coverage Gate
Then if the change would reduce decision coverage below 100% for any Class C file
  The merge is blocked
  And the gate failure message identifies the coverage regression delta
```

---

## AC-12: Hardware Independence (NFR-TEST-03)

### Scenario 12.1 - Full Test Suite on Headless CI Host

```
Given a CI build host with no physical X-ray detector, no physical HVG generator, and no external PACS
When the complete regression test suite (Unit + Integration + DICOM + System) is executed
Then all test stages complete without any connection attempt to physical hardware
  And all test results are deterministic and repeatable
  And the test suite produces the same results on any conforming CI host
```

---

## Quality Gates

| Gate                    | Criterion                                                              | Blocking |
|-------------------------|------------------------------------------------------------------------|----------|
| Unit Test Pass          | All GTest and xUnit tests pass                                         | Yes      |
| Class C Coverage        | 100% decision coverage for Generator Control and AEC components        | Yes      |
| Class B Coverage        | Per-component statement coverage thresholds met (85–90%)              | Yes      |
| Unit Suite Speed        | Total unit test execution time under 5 minutes                         | Yes      |
| DVTK Conformance        | Zero Critical, zero Error violations for all DICOM objects under test  | Yes      |
| Integration Pass        | All pytest integration tests pass with healthy simulators              | Yes      |
| System Workflow Pass    | All E2E clinical workflow tests complete successfully                  | Yes      |
| No Physical Hardware    | Zero connections to physical devices in any automated test stage       | Yes      |
| RTM Bidirectional       | All requirements traced to tests; all tests traced to requirements     | Yes      |
| RTM Requirement Marker  | All test cases carry a valid requirement marker                        | Yes      |
| Synthetic Data PHI-Free | No real patient identifiers in any generated test fixture              | Yes      |
| Artifact Retention      | JUnit XML and Cobertura XML retained >= 90 days in CI artifact store   | Yes      |
| Test Repeatability      | No timing-dependent or order-dependent tests (deterministic results)   | Yes      |
| Simulator No-Privilege  | Simulators start without elevated OS privileges                        | Yes      |
| IEC 62304 Traceability  | All requirements traceable in RTM; evidence documents authored         | Yes      |
