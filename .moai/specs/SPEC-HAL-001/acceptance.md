# SPEC-HAL-001: Acceptance Criteria

**SPEC ID**: SPEC-HAL-001
**Module**: `hnvue-hal` — Hardware Abstraction Layer
**Format**: Given-When-Then (Gherkin-style)
**Safety Classification**: IEC 62304 Class C (generator), Class B (detector)

---

## Quality Gate Criteria

Before any acceptance scenario is considered, the following gates must be green:

- Build: `cmake --build build/hnvue-hal --config Release` exits 0
- Tests: `ctest -R hnvue-hal --output-on-failure` exits 0
- Code coverage: `>= 85%` on non-hardware implementation files
- Thread-sanitizer: Zero data race reports
- Address-sanitizer: Zero memory error reports
- Static analysis: Zero `clang-tidy` warnings on public headers
- Export check: `dumpbin /EXPORTS hnvue-hal.dll` contains only declared public symbols

---

## FR-HAL-01: Vendor Detector SDK Integration Interface

### AC-HAL-01-01: IDetector Interface Mockability

```gherkin
Feature: IDetector interface is fully mockable

  Scenario: Google Mock substitutes IDetector in unit test
    Given a unit test that includes MockDetector.h
    And MockDetector inherits from hnvue::hal::IDetector
    When the test instantiates MockDetector and sets an expectation on StartAcquisition
    Then the test compiles and links without referencing any detector hardware library
    And the mock expectation is verified by Google Mock at test teardown
```

### AC-HAL-01-02: Plugin ABI Version Validation

```gherkin
Feature: Plugin loader validates ABI version on load

  Scenario: Compatible plugin loads successfully
    Given a stub detector plugin DLL that exports GetPluginManifest
    And the manifest reports a compatible interface version
    When DevicePluginLoader::Load is called with the plugin path
    Then Load returns HAL_OK
    And GetDetector() returns a non-null IDetector pointer

  Scenario: Incompatible plugin is rejected
    Given a stub detector plugin DLL that reports an incompatible interface version
    When DevicePluginLoader::Load is called with the plugin path
    Then Load returns HAL_ERR_PLUGIN
    And GetDetector() returns nullptr
    And the error log contains the plugin path and version mismatch detail
```

### AC-HAL-01-03: Missing Export Rejected

```gherkin
Feature: Plugin without required exports is rejected

  Scenario: DLL missing CreateDetector symbol is rejected
    Given a DLL that does not export the CreateDetector symbol
    When DevicePluginLoader::Load is called with that DLL path
    Then Load returns HAL_ERR_PLUGIN
    And the error message identifies the missing export name
```

---

## FR-HAL-02: HVG Standard Interface

### AC-HAL-02-01: SetExposureParams Round-Trip via Simulator

```gherkin
Feature: SetExposureParams is accepted by GeneratorSimulator

  Scenario: Valid exposure parameters accepted
    Given a GeneratorSimulator instance in GEN_IDLE state
    And ExposureParams with kVp=80.0, mA=200.0, ms=100.0, AecMode=AEC_MANUAL
    When SetExposureParams is called
    Then the return value is true
    And GetStatus().state remains GEN_IDLE
    And GetStatus().actual_kvp is 0.0 (not yet exposing)

  Scenario: Invalid kVp value rejected
    Given a GeneratorSimulator instance
    And ExposureParams with kVp=200.0 (above 150.0 maximum)
    When SetExposureParams is called
    Then the return value is false
    And the last error code is HAL_ERR_PARAM
```

### AC-HAL-02-02: StartExposure State Transition via Simulator

```gherkin
Feature: StartExposure triggers correct state transitions in simulator

  Scenario: Successful exposure sequence
    Given a GeneratorSimulator with valid ExposureParams already set
    When StartExposure is called
    Then the simulator transitions to GEN_EXPOSING within 10 ms
    And a status callback delivers HvgStatus with state=GEN_EXPOSING within 100 ms
    And after the configured exposure duration the state transitions to GEN_IDLE
    And ExposureResult.success is true
    And ExposureResult.actual_kvp is within 2% of the requested kVp
```

### AC-HAL-02-03: AbortExposure Cancels Active Exposure

```gherkin
Feature: AbortExposure stops an in-progress exposure

  Scenario: Abort during active exposure
    Given a GeneratorSimulator actively exposing (state=GEN_EXPOSING)
    When AbortExposure is called
    Then the simulator transitions to GEN_IDLE within 10 ms
    And no further status callbacks report GEN_EXPOSING
    And the alarm callback is NOT invoked (abort is not an alarm)
```

### AC-HAL-02-04: Protobuf Interface Compiles and Links

```gherkin
Feature: hvg_control.proto generates valid C++ code

  Scenario: Generated protobuf code compiles
    Given hvg_control.proto processed by protoc
    When the generated .pb.h and .pb.cc files are included in a test target
    Then the test target compiles and links without errors
    And an ExposureParams message can be serialized and deserialized round-trip with no data loss
```

---

## FR-HAL-03: Plugin Architecture

### AC-HAL-03-01: Runtime Plugin Load Without App Restart

```gherkin
Feature: Plugins can be loaded and unloaded at runtime

  Scenario: Load, use, and unload a plugin without restart
    Given the application is running with no detector plugin loaded
    When DevicePluginLoader::Load is called with the stub plugin path
    Then Load returns HAL_OK
    And StartAcquisition succeeds on the loaded detector
    When DevicePluginLoader::Unload is called
    Then Unload returns HAL_OK
    And GetDetector() returns nullptr after unload
    And no memory leak is reported by AddressSanitizer after unload
```

### AC-HAL-03-02: Plugin Exception Isolation

```gherkin
Feature: Plugin exceptions do not propagate to the application

  Scenario: Plugin StartAcquisition throws an unexpected exception
    Given a deliberately broken test plugin that throws std::runtime_error from StartAcquisition
    When the application calls StartAcquisition through the plugin loader wrapper
    Then the exception is caught at the plugin boundary
    And StartAcquisition returns HAL_ERR_PLUGIN to the caller
    And the application continues running normally
    And the error log records the exception message and plugin identity
```

---

## FR-HAL-04: HVG Communication Protocols

### AC-HAL-04-01: Serial Port Configuration

```gherkin
Feature: GeneratorRS232Impl opens serial port with correct settings

  Scenario: Serial port opens with configured parameters
    Given GeneratorRS232Impl configured with port="COM3", baud=115200, parity=NONE, stopbits=1
    When Connect() is called (hardware available)
    Then the serial port opens successfully
    And the port is configured to the specified baud rate and framing
    [Hardware Required - label: HARDWARE_REQUIRED]

  Scenario: Serial port open fails gracefully when port unavailable
    Given GeneratorRS232Impl configured with port="COM99" (non-existent)
    When Connect() is called
    Then Connect returns HAL_ERR_COMM
    And the error message identifies the port name and OS error code
```

### AC-HAL-04-02: Ethernet Connection

```gherkin
Feature: GeneratorEthernetImpl connects to HVG over TCP

  Scenario: TCP connection established successfully
    Given GeneratorEthernetImpl configured with host="192.168.1.100", port=8080
    And a listening TCP server is available at that address
    When Connect() is called
    Then the TCP connection is established within the configured timeout
    And GetStatus() returns without error
    [Hardware Required - label: HARDWARE_REQUIRED]

  Scenario: TCP connection timeout handled gracefully
    Given GeneratorEthernetImpl configured with host="10.255.255.1" (unreachable), timeout_ms=1000
    When Connect() is called
    Then Connect returns HAL_ERR_COMM after approximately 1000 ms
    And no thread is left blocked after Connect returns
```

---

## FR-HAL-05: Command Queuing with Timeout and Retry

### AC-HAL-05-01: Command FIFO Ordering

```gherkin
Feature: Commands are processed in FIFO order

  Scenario: Three commands queued are dispatched in order
    Given a GeneratorSimulator with command recording enabled
    When SetExposureParams (cmd A), GetCapabilities (cmd B), GetCapabilities (cmd C) are enqueued
    Then the simulator receives commands in order: A, B, C
    And no command is skipped or duplicated
```

### AC-HAL-05-02: Abort Command Priority

```gherkin
Feature: AbortExposure pre-empts queued commands

  Scenario: Abort is processed before pending non-abort commands
    Given a GeneratorSimulator processing a long command (100 ms delay)
    And two GetCapabilities commands are queued behind it
    When AbortExposure is enqueued while the long command is processing
    Then AbortExposure is dispatched immediately after the current command completes
    And AbortExposure is processed before the two queued GetCapabilities commands
```

### AC-HAL-05-03: Command Timeout and Retry

```gherkin
Feature: Commands timeout and retry as configured

  Scenario: Command times out and retries to maximum count
    Given a GeneratorSimulator configured to not respond to SetExposureParams
    And the command queue configured with timeout_ms=100, max_retries=3
    When SetExposureParams is enqueued
    Then the command is attempted 4 times total (1 initial + 3 retries)
    And each attempt waits approximately 100 ms before retrying
    And after the final retry the error callback is invoked with HAL_ERR_TIMEOUT
    And the command identity in the error callback matches the original command
```

### AC-HAL-05-04: Queue Depth Enforcement

```gherkin
Feature: Queue rejects commands beyond maximum depth

  Scenario: Commands beyond max depth are rejected
    Given a command queue with max_depth=4
    And 4 commands are already queued and the dispatch thread is blocked
    When a 5th command is enqueued
    Then the enqueue call returns HAL_ERR_STATE immediately
    And the queue length remains 4
```

---

## FR-HAL-06: Real-Time Status Callback and Alarm Events

### AC-HAL-06-01: Status Callback Rate During Exposure

```gherkin
Feature: Status callbacks are delivered at >= 10 Hz during exposure

  Scenario: Status stream rate during simulated exposure
    Given a GeneratorSimulator with a registered status callback
    When StartExposure is called with ms=500
    Then the status callback is invoked at least 5 times within 500 ms
    And each callback delivers HvgStatus with state=GEN_EXPOSING
    And consecutive callback timestamps differ by no more than 200 ms
```

### AC-HAL-06-02: Alarm Callback Latency

```gherkin
Feature: Alarm callbacks are delivered within 50 ms

  Scenario: Alarm delivery latency
    Given a GeneratorSimulator with a registered alarm callback
    And a callback timestamp recorder that captures the simulation alarm injection time
    When the simulator injects an ALARM_ERROR alarm
    Then the alarm callback is invoked within 50 ms of injection
    And HvgAlarm.severity is ALARM_ERROR
    And HvgAlarm.alarm_code matches the injected code
```

### AC-HAL-06-03: Multiple Callbacks All Invoked

```gherkin
Feature: All registered callbacks receive each event

  Scenario: Three alarm callbacks all receive the same alarm
    Given three separate alarm callback functions registered on the same IHvgDriver
    When an alarm is generated by the simulator
    Then all three callbacks are invoked for that alarm
    And no callback invocation raises an uncaught exception
    And if one callback throws, the other two are still invoked
```

---

## FR-HAL-07: AEC Interface

### AC-HAL-07-01: AEC Mode Switching

```gherkin
Feature: AEC mode switches between AUTO and MANUAL

  Scenario: Switch to AEC_AUTO mode
    Given an IAEC instance in AEC_MANUAL mode
    When SetMode(AEC_AUTO) is called
    Then SetMode returns true
    And GetMode() returns AEC_AUTO

  Scenario: Switch from AEC_AUTO to AEC_MANUAL mode
    Given an IAEC instance in AEC_AUTO mode
    When SetMode(AEC_MANUAL) is called
    Then SetMode returns true
    And GetMode() returns AEC_MANUAL
```

### AC-HAL-07-02: AEC Termination Signal Response Time

```gherkin
Feature: AEC termination signal initiates abort within 5 ms

  Scenario: Software-simulated AEC termination during exposure
    Given a GeneratorSimulator in GEN_EXPOSING state
    And IAEC in AEC_AUTO mode with a registered termination callback
    And a timestamp recorder on the AEC signal injection
    When the test injects a synthetic AEC termination signal
    Then AbortExposure is invoked within 5 ms of signal injection
    And the generator transitions to GEN_IDLE within 10 ms total
```

---

## FR-HAL-08: Table and Collimator Position Feedback

### AC-HAL-08-01: ICollimator Read-Only Mode Rejection

```gherkin
Feature: Non-motorized collimator rejects movement commands

  Scenario: SetPosition rejected when not motorized
    Given an ICollimator mock configured as non-motorized (IsMotorized() = false)
    When SetPosition is called with any CollimatorPosition value
    Then SetPosition returns false
    And the last error is HAL_ERR_NOT_SUPPORTED
    And no movement is attempted
```

### AC-HAL-08-02: Position Change Notification

```gherkin
Feature: Position callbacks fire on position change

  Scenario: Table position callback on simulated move
    Given an IPatientTable mock with a registered position callback
    When the mock simulates a table movement from 0 mm to 100 mm longitudinal
    Then the position callback is invoked at least once with the new position
    And the callback value matches the simulated final position within 1 mm tolerance
```

---

## FR-HAL-09: DMA Ring Buffer Management

### AC-HAL-09-01: Frame-to-Callback Latency

```gherkin
Feature: Frame data available to callback within 100 ms

  Scenario: DMA ring buffer frame delivery latency
    Given a DmaRingBuffer with depth=8, frame_size=2MB
    And a frame-available callback that records the arrival timestamp
    And a producer thread that records the write-completion timestamp
    When the producer writes a frame to the ring buffer
    Then the callback is invoked within 100 ms of write completion
    And the frame sequence number in the callback is monotonically increasing
    And the frame data in the callback is byte-for-byte identical to what was written
```

### AC-HAL-09-02: DROP_OLDEST Overwrite Policy

```gherkin
Feature: DMA buffer drops oldest frame when full with DROP_OLDEST policy

  Scenario: Buffer overflows with DROP_OLDEST configured
    Given a DmaRingBuffer with depth=3 and DROP_OLDEST overwrite policy
    And no consumer is reading frames
    When 4 frames are written by the producer in rapid succession
    Then frames 1 and 2 are written successfully without blocking
    And on the 4th write, frame 1 (oldest) is overwritten
    And the producer write call returns without blocking
    And the frame sequence numbers reported to the callback are 2, 3, 4 (skipping 1)
```

### AC-HAL-09-03: BLOCK_PRODUCER Overwrite Policy

```gherkin
Feature: DMA buffer blocks producer when full with BLOCK_PRODUCER policy

  Scenario: Buffer overflows with BLOCK_PRODUCER configured
    Given a DmaRingBuffer with depth=2 and BLOCK_PRODUCER overwrite policy
    And no consumer is reading frames
    When 3 frames are written by the producer
    Then frames 1 and 2 are written successfully
    And the 3rd write blocks until the consumer reads at least one frame
    And after the consumer reads a frame the 3rd write completes
    And no frames are dropped
```

### AC-HAL-09-04: Thread Safety — No Data Race

```gherkin
Feature: DMA ring buffer has no data race under concurrent access

  Scenario: Thread sanitizer reports clean under concurrent write and read
    Given a DmaRingBuffer configured for SPSC use
    And a producer thread writing 1000 frames at maximum rate
    And a consumer callback thread reading frames
    When the test runs under thread sanitizer
    Then thread sanitizer reports zero data races
    And all 1000 frames are delivered to the callback in sequence
    And no frame data is corrupted
```

---

## NFR-HAL-03: Full Mockability Verification

### AC-HAL-NFR-03-01: All Seven Interfaces Mockable Without Hardware

```gherkin
Feature: All primary HAL interfaces are mockable without hardware

  Scenario: Complete mock build with no hardware dependency
    Given a test target that includes all seven mock headers (IDetector, IGenerator, ICollimator, IPatientTable, IAEC, IDoseMonitor, ISafetyInterlock)
    And no hardware driver libraries are linked
    When the test target is built and run on a CI machine with no X-ray hardware
    Then all seven mocks compile and link successfully
    And each mock can be instantiated and exercised with Google Mock expectations
    And the test target passes with all expectations verified
```

---

## NFR-HAL-05: Thread Safety

### AC-HAL-NFR-05-01: Concurrent Status Reads and Command Enqueue

```gherkin
Feature: Status reads and command enqueue are thread-safe

  Scenario: Concurrent access from two threads
    Given a GeneratorSimulator running with a status callback active
    And thread A repeatedly calling GetStatus at 100 Hz
    And thread B enqueuing SetExposureParams commands at 10 Hz
    When both threads run concurrently for 5 seconds under thread sanitizer
    Then thread sanitizer reports zero data races
    And GetStatus never returns a partially-written HvgStatus struct
    And all enqueued commands complete without corruption
```

---

## NFR-HAL-06: Error Isolation

### AC-HAL-NFR-06-01: Crashed Plugin Does Not Crash Host

```gherkin
Feature: Plugin crash does not propagate to host application

  Scenario: Deliberately crashing plugin exception is isolated
    Given a test plugin that throws std::terminate-inducing code in its factory
    When DevicePluginLoader::Load is called with the crashing plugin
    Then the host application does not terminate or throw
    And Load returns HAL_ERR_PLUGIN
    And the application continues running and can load a valid plugin subsequently
```

---

## Verification Methods

| Test Category | Tool | CI Label |
|---|---|---|
| Unit tests (no hardware) | Google Test + ctest | `hnvue-hal-unit` |
| Thread-safety tests | Google Test + TSan | `hnvue-hal-tsan` |
| Memory safety tests | Google Test + ASan | `hnvue-hal-asan` |
| Latency benchmarks | Google Test + timing assertions | `hnvue-hal-bench` |
| Hardware integration | Google Test + physical HVG | `HARDWARE_REQUIRED` |
| Static analysis | clang-tidy | CI lint step |
| Coverage report | gcovr / lcov | `hnvue-hal-coverage` |

---

## Test Execution Commands

```
# Run all non-hardware unit tests
ctest --test-dir build -R hnvue-hal-unit --output-on-failure

# Run thread-sanitizer tests
cmake -DCMAKE_BUILD_TYPE=TSan -S . -B build-tsan
cmake --build build-tsan --target hnvue-hal-tests
ctest --test-dir build-tsan -R hnvue-hal-tsan --output-on-failure

# Run address-sanitizer tests
cmake -DCMAKE_BUILD_TYPE=ASan -S . -B build-asan
cmake --build build-asan --target hnvue-hal-tests
ctest --test-dir build-asan -R hnvue-hal-asan --output-on-failure

# Generate coverage report
cmake -DCMAKE_BUILD_TYPE=Coverage -S . -B build-cov
cmake --build build-cov --target hnvue-hal-tests
ctest --test-dir build-cov -R hnvue-hal-unit
gcovr --html-details coverage/hnvue-hal.html --root libs/hnvue-hal
```

---

*SPEC-HAL-001 Acceptance Criteria v1.0 | HnVue Diagnostic X-ray Console | IEC 62304 Class B/C | 2026-02-17*
