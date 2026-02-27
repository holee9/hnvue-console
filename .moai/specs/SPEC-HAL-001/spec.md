# SPEC-HAL-001: HnVue Hardware Abstraction Layer

**SPEC ID**: SPEC-HAL-001
**Title**: HnVue Hardware Abstraction Layer (hnvue-hal)
**Status**: Completed
**Priority**: High
**Safety Classification**: IEC 62304 Class C (generator control), IEC 62304 Class B (detector interface)
**Created**: 2026-02-17
**Related SPECs**: SPEC-INFRA-001 (prerequisite), SPEC-IMAGING-001 (consumer of IDetector), SPEC-WORKFLOW-001 (consumer of all HAL interfaces)

---

## 1. Environment

### 1.1 Product Context

HnVue is a diagnostic medical device X-ray GUI Console software. The Hardware Abstraction Layer (hnvue-hal) provides the unified hardware interface layer that decouples the application software stack from physical X-ray device hardware and vendor-specific SDKs.

### 1.2 Deployment Context

- **Host Platform**: Windows 10/11 (64-bit), embedded PC in X-ray console cabinet
- **Runtime**: C++17 shared library (`hnvue-hal.dll`)
- **Package Location**: `libs/hnvue-hal/`
- **Public Headers**: `include/hnvue/hal/`
- **Plugin DLLs**: Vendor adapter plugins loaded at runtime from configurable plugin directory
- **Build System**: CMake 3.25+, vcpkg dependency management
- **Regulatory Context**: IEC 60601-2-54 (X-ray performance/safety), IEC 62304 (software lifecycle)

### 1.3 Scope Boundaries

**IN SCOPE:**

- Public C++ interface definitions for all hardware subsystems
- HVG (High Voltage Generator) communication implementations (RS-232/RS-485, Ethernet)
- Plugin architecture for dynamic loading of vendor detector adapter DLLs
- Command queuing, timeout, and retry management for HVG
- Real-time status callback and alarm event subsystem
- AEC (Automatic Exposure Control) interface and mode switching
- Table and collimator position feedback interfaces
- DMA ring buffer management for detector frame acquisition
- HVG simulator and detector simulator for development and testing
- Standard interface definitions (protobuf) for HVG and Detector integration

**OUT OF SCOPE:**

- FPGA implementation (separate hardware project)
- Vendor detector SDK implementation (separate project; this SPEC defines integration interfaces only)
- Image processing pipeline (SPEC-IMAGING-001)
- DICOM services (SPEC-DICOM-001)
- Clinical workflow orchestration (SPEC-WORKFLOW-001)
- GUI/IPC layer (SPEC-IPC-001, SPEC-UI-001)

### 1.4 Dependencies

| Dependency | Type | Purpose |
|---|---|---|
| C++17 standard library | SOUP | Core language runtime |
| Boost.Asio or WinAPI | SOUP | Serial and TCP/IP communication |
| protobuf 3.x | SOUP | Standard interface message definitions |
| Google Test | SOUP (test only) | Unit and integration testing |
| vcpkg | Tool | Dependency management |

---

## 2. Assumptions

### 2.1 Technical Assumptions

- A2-01: Vendor detector SDK projects implement the `IDetector` interface defined in this SPEC and expose it via the plugin ABI contract.
- A2-02: HVG devices communicate via RS-232 (up to 115200 baud), RS-485, or Ethernet (TCP/IP) with vendor-specific ASCII or binary command protocols wrapped behind `IGenerator`.
- A2-03: The host OS provides POSIX-compatible serial port APIs or Windows COMx device access without requiring kernel drivers.
- A2-04: DMA ring buffers are managed in user space; kernel-level DMA drivers are provided by the detector vendor adapter DLL.
- A2-05: Physical AEC hardware signals are routed through the detector or a dedicated AEC controller; the HAL layer provides software-level interface only.

### 2.2 Regulatory Assumptions

- A2-06: Generator control functions (IGenerator, IAEC) are classified IEC 62304 Class C because software failure can cause serious injury via radiation overdose.
- A2-07: Detector acquisition interface (IDetector, DMA buffer) is classified IEC 62304 Class B because software failure may cause loss of diagnostic data but not direct physical harm.
- A2-08: All public interfaces must be fully mockable to support SOUP-isolated unit testing per IEC 62304 §5.5.

### 2.3 Architectural Assumptions

- A2-09: `hnvue-hal` has no dependencies on other `hnvue-*` libraries (pure interface layer; unidirectional dependency rule).
- A2-10: Plugin DLLs are signed and validated before loading to prevent unauthorized hardware access.
- A2-11: The HAL layer does not perform clinical decision making; it only forwards commands and reports state.

---

## 3. Requirements

### 3.1 Functional Requirements

#### FR-HAL-01: Vendor Detector SDK Integration Interface

The system shall provide a pure C++ abstract interface `IDetector` that defines the complete contract for vendor detector adapter plugins.

The system shall define a plugin ABI contract using an extern "C" factory function (`CreateDetector`) to enable binary-compatible loading of vendor DLLs compiled with different toolchains.

When a detector plugin DLL is loaded, the system shall validate that the plugin exports the required factory symbol and reports a compatible interface version before completing initialization.

#### FR-HAL-02: Vendor HVG SDK Standard Interface

The system shall define a standard protobuf service definition (`HvgControl`) that specifies all HVG command, status, and alarm message contracts for inter-process and cross-language interoperability.

The system shall provide a pure C++ abstract interface `IGenerator` that mirrors the `HvgControl` protobuf service semantics for in-process use.

The system shall implement `IGenerator` for RS-232/RS-485 serial HVG communication (`GeneratorRS232Impl`) and for Ethernet TCP/IP HVG communication (`GeneratorEthernetImpl`).

The system shall provide a `GeneratorSimulator` implementation of `IGenerator` that emulates HVG responses for development and testing without physical hardware.

#### FR-HAL-03: Plugin Architecture for Device Drivers

The system shall implement a `DevicePluginLoader` that discovers, loads, and manages the lifecycle of device driver plugins from a configurable plugin directory at runtime.

The system shall support loading multiple plugins simultaneously to manage heterogeneous device configurations (e.g., one detector plugin and multiple peripheral plugins).

When a plugin fails to load or initialize, the system shall report a structured error identifying the plugin path, failure reason, and whether the failure is recoverable.

The system shall support unloading and reloading a plugin without restarting the host application, subject to no active acquisition being in progress.

#### FR-HAL-04: HVG Communication Protocols

When configured for serial communication, the system shall establish and maintain RS-232 or RS-485 serial communication with the HVG at configurable baud rates (9600 to 115200 bps), parity, stop bits, and flow control settings.

When configured for Ethernet communication, the system shall establish and maintain a TCP connection to the HVG at the configured IP address and port, with configurable connection timeout and keep-alive parameters.

The system shall implement the HVG vendor communication protocol as a stateless command-response transaction over the established physical channel.

#### FR-HAL-05: Command Queuing with Timeout and Retry

The system shall maintain an ordered command queue for HVG commands with configurable maximum queue depth.

When a command is enqueued and the channel is idle, the system shall dispatch the command immediately and await a response within the configured timeout period.

If a command response is not received within the timeout period, the system shall retry the command up to the configured maximum retry count before failing the command with a timeout error.

The system shall support priority promotion for abort commands, placing them at the front of the queue ahead of all pending non-abort commands.

If a command response is not received within the timeout period and the system shall mark the command as failed and invoke the registered error callback with the command identity and timeout reason.

#### FR-HAL-06: Real-Time Status Callback and Alarm Events

The system shall provide a `StreamStatus` mechanism that delivers `HvgStatus` messages (kVp actual, mA actual, exposure state, interlock state) to registered callbacks at a rate not less than 10 Hz during an active exposure.

The system shall provide an alarm event stream that delivers `HvgAlarm` messages to registered callbacks within 50 ms of the alarm condition being detected on the physical device.

When a registered alarm callback is not set and an alarm condition is detected, the system shall log the alarm with severity and timestamp and place the generator in a safe state.

The system shall support registering multiple callbacks per event stream and guarantee that all registered callbacks are invoked for each event, even if one callback throws an exception.

#### FR-HAL-07: AEC Interface

The system shall provide an `IAEC` interface that supports switching between automatic exposure control (AEC) mode and manual exposure mode.

When AEC mode is active, the system shall accept the AEC termination signal from the AEC hardware input and invoke the HVG abort sequence within 5 ms of signal assertion.

When manual mode is active, the system shall use the configured exposure time parameter and terminate exposure at the scheduled time regardless of AEC input.

The system shall report the current AEC mode and AEC threshold configuration through the status polling interface.

#### FR-HAL-08: Table and Collimator Position Feedback

The system shall provide an `IPatientTable` interface that delivers table position (longitudinal, lateral, height) in millimeters via polling and change-notification callbacks.

The system shall provide an `ICollimator` interface that delivers collimator blade position (left, right, top, bottom field size) in millimeters and supports commanding collimator movement where hardware supports motorized control.

Where a hardware subsystem does not support motorized control, the system shall report position as read-only and reject command requests with an appropriate error code.

#### FR-HAL-09: DMA Ring Buffer Management

The system shall provide a `DmaRingBuffer` utility class that manages a pre-allocated circular buffer in user-space memory for high-throughput detector frame data transfer.

The system shall support configuring buffer depth (number of frames), frame size, and overwrite policy (overwrite oldest or block producer) at construction time.

When a detector plugin writes a frame to the ring buffer, the system shall invoke the registered frame-available callback with a non-owning view of the frame data and a monotonically increasing frame sequence number.

The system shall provide thread-safe producer-consumer semantics for the ring buffer, guaranteeing no data race between the detector plugin write thread and the consumer callback thread.

### 3.2 Non-Functional Requirements

#### NFR-HAL-01: Data Transfer Latency

The system shall deliver detector frame data from DMA ring buffer write completion to the consumer callback invocation within 100 ms under normal operating conditions.

#### NFR-HAL-02: HVG Command Response Latency

The system shall complete an HVG command-response round trip (command enqueue to response callback invocation) within 50 ms for the physical RS-232/Ethernet channel under normal operating conditions.

#### NFR-HAL-03: Mockability

All seven primary interfaces (`IDetector`, `IGenerator`, `ICollimator`, `IPatientTable`, `IAEC`, `IDoseMonitor`, `ISafetyInterlock`) shall be pure abstract C++ classes with no non-virtual member functions in the public interface, enabling full Google Mock substitution in tests without linking to hardware drivers.

#### NFR-HAL-04: IEC 62304 Traceability

The system shall maintain bidirectional traceability between each requirement in this SPEC, the implementing source file, and the verification test case in `libs/hnvue-hal/tests/`.

#### NFR-HAL-05: Thread Safety

The system shall be safe to call from multiple threads simultaneously for read-only status and callback registration operations. Write operations (command enqueue, mode change) shall be internally synchronized.

#### NFR-HAL-06: Error Isolation

If a vendor plugin crashes or throws an unhandled exception, the system shall catch the exception at the plugin boundary, log the failure, and transition the affected device to an error state without propagating the exception to the calling application.

---

## 4. Specifications

### 4.1 Directory Structure

```
libs/hnvue-hal/
├── CMakeLists.txt
├── include/
│   └── hnvue/
│       └── hal/
│           ├── IDetector.h         # Detector plugin interface
│           ├── IGenerator.h        # HVG interface
│           ├── ICollimator.h       # Collimator control interface
│           ├── IPatientTable.h     # Patient table control interface
│           ├── IAEC.h              # AEC interface
│           ├── IDoseMonitor.h      # Dose monitor interface
│           ├── ISafetyInterlock.h  # Composite safety interlock interface
│           ├── DeviceManager.h     # Device lifecycle manager
│           ├── DmaRingBuffer.h     # DMA ring buffer utility
│           ├── HalTypes.h          # Shared data types and enumerations
│           └── PluginAbi.h         # C-linkage ABI contract for plugins
├── src/
│   ├── generator/
│   │   ├── GeneratorRS232Impl.h
│   │   ├── GeneratorRS232Impl.cpp
│   │   ├── GeneratorEthernetImpl.h
│   │   ├── GeneratorEthernetImpl.cpp
│   │   ├── GeneratorSimulator.h
│   │   └── GeneratorSimulator.cpp
│   ├── plugin/
│   │   ├── DetectorPluginLoader.h
│   │   └── DetectorPluginLoader.cpp
│   ├── aec/
│   │   ├── AecController.h
│   │   └── AecController.cpp
│   ├── buffer/
│   │   └── DmaRingBuffer.cpp
│   └── DeviceManager.cpp
├── proto/
│   ├── hvg_control.proto           # HVG standard interface (protobuf)
│   └── detector_acquisition.proto  # Detector integration interface (protobuf)
└── tests/
    ├── CMakeLists.txt
    ├── test_generator_command_queue.cpp
    ├── test_generator_simulator.cpp
    ├── test_detector_plugin_loader.cpp
    ├── test_dma_ring_buffer.cpp
    ├── test_aec_controller.cpp
    └── mock/
        ├── MockDetector.h
        ├── MockGenerator.h
        ├── MockCollimator.h
        ├── MockPatientTable.h
        ├── MockAec.h
        ├── MockDoseMonitor.h
        └── MockSafetyInterlock.h
```

### 4.2 Standard Interface Definitions

#### 4.2.1 HVG Standard Interface (protobuf)

The following protobuf service definition is the canonical standard interface for HVG integration. All vendor HVG SDK adapters and the internal `IGenerator` implementation must conform to these message contracts.

```protobuf
// File: libs/hnvue-hal/proto/hvg_control.proto
// IEC 62304 Class C - Generator control: serious injury risk
syntax = "proto3";
package hnvue.hal.hvg;

service HvgControl {
  // Set exposure parameters before arming
  rpc SetExposureParams(ExposureParams) returns (HvgResponse);

  // Arm and initiate X-ray exposure
  rpc StartExposure(ExposureRequest) returns (ExposureResult);

  // Abort an in-progress exposure immediately
  rpc AbortExposure(AbortRequest) returns (HvgResponse);

  // Server-streaming: real-time HVG status at >= 10 Hz
  rpc StreamStatus(StatusRequest) returns (stream HvgStatus);

  // Server-streaming: alarm events with < 50 ms delivery latency
  rpc StreamAlarms(AlarmRequest) returns (stream HvgAlarm);

  // Query static HVG device capabilities
  rpc GetCapabilities(Empty) returns (HvgCapabilities);
}

message ExposureParams {
  float kvp        = 1;  // kV: range 40.0–150.0
  float ma         = 2;  // mA: range 0.1–1000.0
  float ms         = 3;  // Exposure time in milliseconds: 1–10000
  float mas        = 4;  // mAs (alternative to ma+ms)
  AecMode aec_mode = 5;
  string focus     = 6;  // "large" | "small"
}

enum AecMode {
  AEC_MODE_UNSPECIFIED = 0;
  AEC_MANUAL  = 1;
  AEC_AUTO    = 2;
}

message HvgResponse {
  bool   success   = 1;
  string error_msg = 2;
  int32  error_code = 3;
}

message ExposureRequest {
  string request_id = 1;
}

message ExposureResult {
  bool   success        = 1;
  float  actual_kvp     = 2;
  float  actual_ma      = 3;
  float  actual_ms      = 4;
  float  actual_mas     = 5;
  string error_msg      = 6;
}

message AbortRequest {
  string reason = 1;
}

message StatusRequest {}

message HvgStatus {
  float       actual_kvp     = 1;
  float       actual_ma      = 2;
  GeneratorState state       = 3;
  bool        interlock_ok   = 4;
  int64       timestamp_us   = 5;  // microseconds since epoch
}

enum GeneratorState {
  GEN_STATE_UNSPECIFIED = 0;
  GEN_IDLE      = 1;
  GEN_READY     = 2;
  GEN_ARMED     = 3;
  GEN_EXPOSING  = 4;
  GEN_ERROR     = 5;
}

message AlarmRequest {}

message HvgAlarm {
  int32  alarm_code  = 1;
  string description = 2;
  AlarmSeverity severity = 3;
  int64  timestamp_us    = 4;
}

enum AlarmSeverity {
  ALARM_SEVERITY_UNSPECIFIED = 0;
  ALARM_INFO     = 1;
  ALARM_WARNING  = 2;
  ALARM_ERROR    = 3;
  ALARM_CRITICAL = 4;
}

message HvgCapabilities {
  float min_kvp  = 1;
  float max_kvp  = 2;
  float min_ma   = 3;
  float max_ma   = 4;
  float min_ms   = 5;
  float max_ms   = 6;
  bool  has_aec  = 7;
  bool  has_dual_focus = 8;
  string vendor_name = 9;
  string model_name  = 10;
  string firmware_version = 11;
}

message Empty {}
```

#### 4.2.2 Detector Integration Interface (protobuf)

The following protobuf service definition is the canonical integration interface for vendor detector SDK adapter projects. Vendor adapter projects implement this service; this SPEC only defines the contract.

```protobuf
// File: libs/hnvue-hal/proto/detector_acquisition.proto
// IEC 62304 Class B - Detector acquisition
syntax = "proto3";
package hnvue.hal.detector;

service DetectorAcquisition {
  // Server-streaming: deliver raw frames to consumer
  rpc StreamFrames(AcquisitionConfig) returns (stream RawFrame);

  // Begin acquisition session
  rpc StartAcquisition(AcquisitionConfig) returns (DetectorResponse);

  // Stop acquisition session
  rpc StopAcquisition(StopRequest) returns (DetectorResponse);

  // Run flat-field or dark-field calibration
  rpc RunCalibration(CalibrationType) returns (CalibrationResult);

  // Query static detector properties
  rpc GetDetectorInfo(Empty) returns (DetectorInfo);
}

message AcquisitionConfig {
  AcquisitionMode mode        = 1;
  int32           num_frames  = 2;   // 0 = continuous
  float           frame_rate  = 3;   // frames per second
  int32           binning     = 4;   // 1 | 2 | 4
  string          session_id  = 5;
}

enum AcquisitionMode {
  ACQUISITION_MODE_UNSPECIFIED = 0;
  MODE_STATIC     = 1;
  MODE_CONTINUOUS = 2;
  MODE_TRIGGERED  = 3;
}

message RawFrame {
  int64  sequence_number = 1;
  int64  timestamp_us    = 2;
  int32  width           = 3;
  int32  height          = 4;
  int32  bit_depth       = 5;
  bytes  pixel_data      = 6;  // row-major, native byte order
  string session_id      = 7;
}

message DetectorResponse {
  bool   success   = 1;
  string error_msg = 2;
  int32  error_code = 3;
}

message StopRequest {
  string session_id = 1;
  string reason     = 2;
}

message CalibrationType {
  CalibType type = 1;
  int32 num_frames = 2;
}

enum CalibType {
  CALIB_TYPE_UNSPECIFIED = 0;
  CALIB_DARK_FIELD  = 1;
  CALIB_FLAT_FIELD  = 2;
  CALIB_DEFECT_MAP  = 3;
}

message CalibrationResult {
  bool   success      = 1;
  string output_path  = 2;
  string error_msg    = 3;
}

message DetectorInfo {
  string vendor          = 1;
  string model           = 2;
  string serial_number   = 3;
  int32  pixel_width     = 4;
  int32  pixel_height    = 5;
  float  pixel_pitch_um  = 6;
  int32  max_bit_depth   = 7;
  float  max_frame_rate  = 8;
  string firmware_version = 9;
}

message Empty {}
```

### 4.3 C++ Driver Interface Specifications

#### 4.3.1 IGenerator Interface

```
// File: include/hnvue/hal/IGenerator.h
// IEC 62304 Class C - SAFETY CRITICAL
// All implementations must be reviewed and tested under IEC 62304 §5.5–5.7

namespace hnvue::hal {

class IHvgDriver {
public:
    virtual ~IHvgDriver() = default;

    // Query current generator status (thread-safe, non-blocking)
    virtual HvgStatus GetStatus() = 0;

    // Configure exposure parameters (must be called before StartExposure)
    virtual bool SetExposureParams(const ExposureParams& params) = 0;

    // Initiate exposure; blocks until exposure begins or fails
    virtual ExposureResult StartExposure() = 0;

    // Abort in-progress exposure; must return within 10 ms
    virtual void AbortExposure() = 0;

    // Register callback for asynchronous alarm delivery (< 50 ms latency)
    virtual void RegisterAlarmCallback(
        std::function<void(HvgAlarm)> callback) = 0;

    // Plugin factory: load implementation from vendor_plugin_path
    // Returns nullptr on failure; sets error via GetLastError()
    static std::unique_ptr<IHvgDriver> Create(
        const std::string& vendor_plugin_path);
};

} // namespace hnvue::hal
```

#### 4.3.2 IDetector Interface

```
// File: include/hnvue/hal/IDetector.h
// IEC 62304 Class B

namespace hnvue::hal {

class IDetector {
public:
    virtual ~IDetector() = default;

    virtual DetectorInfo    GetDetectorInfo() = 0;
    virtual bool            StartAcquisition(const AcquisitionConfig& cfg) = 0;
    virtual bool            StopAcquisition() = 0;
    virtual CalibrationResult RunCalibration(CalibType type, int num_frames) = 0;
    virtual void            RegisterFrameCallback(
                                std::function<void(const RawFrame&)> cb) = 0;
    virtual DetectorStatus  GetStatus() = 0;
};

} // namespace hnvue::hal
```

#### 4.3.3 Plugin ABI Contract

```
// File: include/hnvue/hal/PluginAbi.h
// C-linkage factory for binary-compatible plugin loading

extern "C" {
    // Detector plugin entry point (required export)
    hnvue::hal::IDetector* CreateDetector(
        const hnvue::hal::PluginConfig* config);

    void DestroyDetector(hnvue::hal::IDetector* detector);

    // Plugin manifest (required export)
    const hnvue::hal::PluginManifest* GetPluginManifest();
}
```

#### 4.3.4 Additional Interface Sketches

```
// ICollimator - Field of view control
namespace hnvue::hal {
class ICollimator {
public:
    virtual ~ICollimator() = default;
    virtual CollimatorPosition GetPosition() = 0;
    virtual bool SetPosition(const CollimatorPosition& pos) = 0;
    virtual bool IsMotorized() const = 0;
    virtual void RegisterPositionCallback(
        std::function<void(CollimatorPosition)> cb) = 0;
};
} // namespace hnvue::hal

// IPatientTable - Table position feedback
namespace hnvue::hal {
class IPatientTable {
public:
    virtual ~IPatientTable() = default;
    virtual TablePosition GetPosition() = 0;
    virtual bool MoveTo(const TablePosition& pos) = 0;
    virtual bool IsMotorized() const = 0;
    virtual void RegisterPositionCallback(
        std::function<void(TablePosition)> cb) = 0;
};
} // namespace hnvue::hal

// IAEC - Automatic Exposure Control
namespace hnvue::hal {
class IAEC {
public:
    virtual ~IAEC() = default;
    virtual bool SetMode(AecMode mode) = 0;
    virtual AecMode GetMode() const = 0;
    virtual bool SetThreshold(float threshold_pct) = 0;
    virtual void RegisterTerminationCallback(
        std::function<void(AecTerminationEvent)> cb) = 0;
};
} // namespace hnvue::hal

// IDoseMonitor - Radiation dose monitoring
namespace hnvue::hal {
class IDoseMonitor {
public:
    virtual ~IDoseMonitor() = default;
    virtual DoseReading GetCurrentDose() = 0;
    virtual float GetDap() = 0;   // Dose Area Product (uGy*cm2)
    virtual void Reset() = 0;
    virtual void RegisterDoseCallback(
        std::function<void(DoseReading)> cb) = 0;
};
} // namespace hnvue::hal

// ISafetyInterlock - Composite safety interlock interface
// Aggregates all hardware interlock signals required by SPEC-WORKFLOW-001
// Covers both physical safety interlocks and device readiness checks
// Maps to WORKFLOW interlocks IL-01 through IL-09
namespace hnvue::hal {

struct InterlockStatus {
    // Physical Safety Interlocks
    bool door_closed;          // IL-01: X-ray room door closed (hardware sensor)
    bool emergency_stop_clear; // IL-02: Emergency stop not activated (hardware sensor)
    bool thermal_normal;       // IL-03: No overtemperature condition (IHvgDriver thermal sensor)

    // Device Readiness Interlocks
    bool generator_ready;      // IL-04: Generator in ready state, no fault (IGenerator)
    bool detector_ready;       // IL-05: Detector acquisition ready (IDetector)
    bool collimator_valid;     // IL-06: Collimator position within valid range (ICollimator)
    bool table_locked;         // IL-07: Patient table locked/stable (IPatientTable)
    bool dose_within_limits;   // IL-08: Dose accumulation within configured limits (IDoseMonitor)
    bool aec_configured;       // IL-09: AEC mode properly configured for protocol (IAEC)

    // Aggregate
    bool all_passed;           // Convenience: true only if all above are true
    uint64_t timestamp_us;     // Timestamp of interlock check (microseconds since epoch)
};

class ISafetyInterlock {
public:
    virtual ~ISafetyInterlock() = default;

    // Perform atomic check of all 9 interlocks and return aggregate status.
    // This is the single entry point for WORKFLOW pre-exposure safety verification.
    // Must complete within 10 ms to avoid exposure delay.
    virtual InterlockStatus CheckAllInterlocks() = 0;

    // Check a single interlock by index (0-8, corresponding to IL-01 through IL-09).
    // Returns true if the specified interlock passes.
    virtual bool CheckInterlock(int interlock_index) = 0;

    // Named accessors for safety-critical interlocks (used by WORKFLOW interlock chain)
    virtual bool GetDoorStatus() = 0;          // IL-01: true if door closed
    virtual bool GetEStopStatus() = 0;         // IL-02: true if e-stop clear
    virtual bool GetThermalStatus() = 0;       // IL-03: true if thermal normal

    // Emergency standby: place all hardware in safe state.
    // Called by WORKFLOW on critical hardware error (T-18 recovery).
    // Disarms generator, stops detector acquisition, logs safety event.
    // Must complete within 100 ms.
    virtual void EmergencyStandby() = 0;

    // Register callback for interlock state changes (any interlock transitions to failed).
    // Callback is invoked within 50 ms of the state change detection.
    virtual void RegisterInterlockCallback(
        std::function<void(InterlockStatus)> cb) = 0;
};
} // namespace hnvue::hal
```

### 4.4 DeviceManager Lifecycle

The `DeviceManager` is the single entry point for the application to access all hardware interfaces. It is responsible for:

1. Reading device configuration from a JSON configuration file
2. Loading and initializing registered plugins via `DevicePluginLoader`
3. Instantiating built-in implementations (`GeneratorRS232Impl`, `GeneratorEthernetImpl`, `GeneratorSimulator`)
4. Providing typed accessors (`GetGenerator()`, `GetDetector()`, `GetCollimator()`, etc.)
5. Managing graceful shutdown of all devices in reverse initialization order
6. Propagating critical hardware errors to a registered application-level error handler

### 4.5 Command Queue Specification

The HVG command queue shall implement the following behavior:

| Property | Value |
|---|---|
| Queue type | FIFO with priority override for abort commands |
| Maximum depth | Configurable, default 16 |
| Default command timeout | 500 ms (configurable) |
| Maximum retry count | 3 (configurable) |
| Abort command priority | Highest (pre-empts queue) |
| Thread safety | Lock-free SPSC for single producer; mutex-protected for MPSC |

### 4.6 DMA Ring Buffer Specification

| Property | Value |
|---|---|
| Buffer type | Circular (ring), user-space |
| Configurable parameters | Depth (frames), frame size (bytes), overwrite policy |
| Overwrite policy options | DROP_OLDEST, BLOCK_PRODUCER |
| Thread model | Single producer (detector plugin), single consumer (callback thread) |
| Frame sequence | Monotonically increasing uint64, wraps at UINT64_MAX |
| Memory allocation | Pre-allocated at construction; no heap allocation during operation |

### 4.7 Performance Constraints Summary

| Metric | Threshold | Measurement Point |
|---|---|---|
| Frame DMA to callback | <= 100 ms | DMA write complete to callback entry |
| HVG command round-trip | <= 50 ms | Enqueue to response callback |
| Alarm delivery | <= 50 ms | Hardware alarm to application callback |
| AEC signal to abort | <= 5 ms | AEC signal assertion to AbortExposure initiation |
| Status stream rate | >= 10 Hz | During active exposure |

### 4.8 Error Codes

All error conditions exposed through the HAL use the `HalError` enumeration:

| Code | Name | Description |
|---|---|---|
| 0 | HAL_OK | Success |
| 1 | HAL_ERR_TIMEOUT | Command response timeout |
| 2 | HAL_ERR_COMM | Communication channel failure |
| 3 | HAL_ERR_PLUGIN | Plugin load or ABI error |
| 4 | HAL_ERR_PARAM | Invalid parameter value |
| 5 | HAL_ERR_STATE | Command invalid in current device state |
| 6 | HAL_ERR_HARDWARE | Physical hardware fault |
| 7 | HAL_ERR_ABORT | Operation aborted by request |
| 8 | HAL_ERR_NOT_SUPPORTED | Operation not supported by hardware |

### 4.9 Packaging and Distribution

- **Primary library**: `hnvue-hal.dll` (Windows) / `libhnvue-hal.so` (Linux)
- **Plugin naming convention**: `hnvue-hal-detector-{vendor}.dll`
- **Headers**: Installed to `include/hnvue/hal/` for consumer projects
- **CMake target**: `hnvue::hal` — imported by `find_package(hnvue-hal)`
- **Symbol export**: Only public interface symbols exported; implementation symbols hidden

---

## 5. Traceability

| Requirement | Implements | Test Case |
|---|---|---|
| FR-HAL-01 | `IDetector.h`, `PluginAbi.h`, `DetectorPluginLoader.cpp` | `test_detector_plugin_loader.cpp` |
| FR-HAL-02 | `IGenerator.h`, `hvg_control.proto`, `GeneratorRS232Impl.cpp`, `GeneratorEthernetImpl.cpp`, `GeneratorSimulator.cpp` | `test_generator_simulator.cpp` |
| FR-HAL-03 | `DetectorPluginLoader.cpp`, `DeviceManager.cpp` | `test_detector_plugin_loader.cpp` |
| FR-HAL-04 | `GeneratorRS232Impl.cpp`, `GeneratorEthernetImpl.cpp` | `test_generator_command_queue.cpp` |
| FR-HAL-05 | Command queue in `GeneratorRS232Impl.cpp` | `test_generator_command_queue.cpp` |
| FR-HAL-06 | Callback registration in `IHvgDriver`, `GeneratorSimulator.cpp` | `test_generator_simulator.cpp` |
| FR-HAL-07 | `IAEC.h`, `AecController.cpp` | `test_aec_controller.cpp` |
| FR-HAL-08 | `ICollimator.h`, `IPatientTable.h` | Mock-based unit tests |
| FR-HAL-09 | `DmaRingBuffer.h`, `DmaRingBuffer.cpp` | `test_dma_ring_buffer.cpp` |
| NFR-HAL-01 | `DmaRingBuffer.cpp` | `test_dma_ring_buffer.cpp` (timing assertions) |
| NFR-HAL-02 | Command queue, `GeneratorRS232Impl.cpp` | `test_generator_command_queue.cpp` (timing) |
| NFR-HAL-03 | All `I*.h` headers | All mock headers in `tests/mock/` |
| NFR-HAL-04 | This traceability table | CI traceability report |
| NFR-HAL-05 | Thread-safety implementations | Concurrent access tests |
| NFR-HAL-06 | `DetectorPluginLoader.cpp` exception boundary | `test_detector_plugin_loader.cpp` |

---

*SPEC-HAL-001 v1.0 | HnVue Diagnostic X-ray Console | IEC 62304 Class B/C | 2026-02-17*
