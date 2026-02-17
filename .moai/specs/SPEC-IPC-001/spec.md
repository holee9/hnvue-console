# SPEC-IPC-001: HnVue Inter-Process Communication

<!-- TAG: SPEC-IPC-001 -->
<!-- STATUS: Planned -->
<!-- CREATED: 2026-02-17 -->
<!-- PRIORITY: High -->

---

## Overview

| Field | Value |
|---|---|
| SPEC ID | SPEC-IPC-001 |
| Title | HnVue Inter-Process Communication |
| Product | HnVue - Diagnostic Medical Device X-ray GUI Console SW |
| Status | Planned |
| Priority | High |
| Lifecycle Level | spec-anchored |
| Regulatory Context | IEC 62304 Software Safety Classification |
| Related SPECs | SPEC-HAL-001, SPEC-IMAGING-001 |

---

## 1. Environment

### 1.1 System Context

HnVue is a diagnostic medical device X-ray GUI console software. The system is split into two isolated processes:

- **C++ Core Engine Process**: Hardware abstraction, X-ray generator control, image acquisition, dose calculation, real-time signal processing
- **C# WPF GUI Process**: User interface, workflow orchestration, image display, operator interaction

These processes communicate across a well-defined IPC boundary. This boundary is the subject of SPEC-IPC-001.

### 1.2 Technology Environment

| Component | Technology | Purpose |
|---|---|---|
| IPC Transport | gRPC (HTTP/2) | Primary communication channel |
| Serialization | Protocol Buffers v3 | Message schema definition and serialization |
| C++ Runtime | C++17 or later | Core Engine implementation language |
| C# Runtime | .NET 8 LTS | WPF GUI implementation language |
| C++ gRPC Library | grpc++ (v1.68 or later) | C++ gRPC server implementation |
| C# gRPC Library | Grpc.AspNetCore or Grpc.Core (v2.67 or later) | C# gRPC client implementation |
| Proto Compiler | protoc with grpc plugins | Code generation from .proto files |
| Image Pixel Format | 16-bit grayscale | X-ray image pixel depth |
| Maximum Detector Size | ~9MP (43x43cm at 150um pixel pitch, approx. 2867x2867 pixels) | Upper bound for image payload sizing |

### 1.3 Deployment Context

Both processes run on the same physical machine (Windows embedded system) to ensure deterministic low-latency communication. gRPC over localhost loopback is the transport. Shared memory is reserved as a future optional fallback for extreme throughput scenarios and is out of scope for this SPEC.

### 1.4 Regulatory Environment

The IPC subsystem operates within the IEC 62304 software safety framework. Process isolation is an explicit safety partitioning strategy: a GUI process failure must not compromise Core Engine operation or patient safety functions. The IPC layer must support auditability and deterministic error behavior consistent with medical device software requirements.

---

## 2. Assumptions

| ID | Assumption | Confidence | Risk if Wrong |
|---|---|---|---|
| ASM-IPC-01 | Both processes run on the same Windows machine and communicate via localhost loopback | High | Network latency would violate NFR-IPC-01 latency targets |
| ASM-IPC-02 | gRPC over HTTP/2 localhost provides sufficiently low latency (~50us) for 30fps image streaming | High | Shared memory fallback would need to be elevated to primary mechanism |
| ASM-IPC-03 | 16-bit grayscale image data up to ~9MP constitutes the maximum single-transfer payload | High | Streaming chunking strategy or payload limits would need revision |
| ASM-IPC-04 | The Core Engine acts as gRPC server; GUI acts as gRPC client for all channels | High | Role reversal would invalidate service definitions |
| ASM-IPC-05 | .proto files in the shared `proto/` directory are the single source of truth for the IPC interface contract | High | Interface drift would cause serialization failures |
| ASM-IPC-06 | protoc code generation is integrated into both build systems (CMake for C++, MSBuild for C#) | Medium | Manual code generation steps would introduce version mismatches |
| ASM-IPC-07 | The IEC 62304 safety partitioning requirement is satisfied by OS-level process isolation; the IPC layer does not implement additional safety logic | High | Additional safety mechanisms would be required in IPC layer |

---

## 3. Requirements

### 3.1 Functional Requirements

#### FR-IPC-01: Process-Isolated Communication Architecture

The system shall maintain communication between the C++ Core Engine process and the C# WPF GUI process exclusively through the defined IPC interface, with no shared memory regions or direct in-process calls.

EARS Pattern: Ubiquitous
> The IPC subsystem shall enforce strict process boundary separation such that all data exchange between Core Engine and GUI passes through the defined gRPC service interface.

#### FR-IPC-02: gRPC as Primary Transport

The system shall use gRPC over HTTP/2 on localhost as the exclusive primary transport mechanism for IPC.

EARS Pattern: Ubiquitous
> The IPC subsystem shall use gRPC as the primary transport layer for all inter-process communication.

#### FR-IPC-03: Protocol Buffer Serialization

The system shall use Protocol Buffer v3 (proto3) as the exclusive serialization format for all messages exchanged via IPC.

EARS Pattern: Ubiquitous
> The IPC subsystem shall serialize all messages using Protocol Buffer v3 format as defined in `.proto` files located in the shared `proto/` directory.

#### FR-IPC-04: Bidirectional Communication

**FR-IPC-04a: GUI-to-Core Command Channel**
The system shall provide a command channel through which the GUI process sends operator commands and configuration requests to the Core Engine.

EARS Pattern: Event-Driven
> WHEN the GUI operator initiates an action (e.g., exposure, collimator adjustment, configuration change), THEN the GUI process shall transmit the corresponding command to the Core Engine via the command gRPC service.

**FR-IPC-04b: Core-to-GUI Event and Data Channel**
The system shall provide a server-streaming channel through which the Core Engine pushes events, status updates, and image data to the GUI.

EARS Pattern: Event-Driven
> WHEN the Core Engine produces an event (e.g., acquisition complete, status change, fault), THEN the Core Engine shall push the event to the GUI process via the appropriate server-streaming gRPC service.

#### FR-IPC-05: Image Data Transfer Channel

The system shall provide a dedicated channel for transferring 16-bit grayscale image data from the Core Engine to the GUI, supporting both preview and full-resolution transfer modes.

EARS Pattern: Event-Driven
> WHEN the Core Engine completes image acquisition, THEN the system shall transfer the image data to the GUI process via the image streaming service, selecting preview or full-resolution mode as configured.

**FR-IPC-05a: Chunked Streaming**
The system shall transfer large image payloads (full 9MP image) as a sequence of chunks via gRPC server-streaming to avoid single-message payload limits and enable progressive rendering.

EARS Pattern: Ubiquitous
> The image transfer service shall split image data into fixed-size chunks and stream them sequentially in a server-streaming RPC to the GUI.

**FR-IPC-05b: Transfer Modes**
The system shall support two image transfer modes:
- `PREVIEW`: Downsampled image for rapid display during acquisition preview
- `FULL_QUALITY`: Full-resolution 16-bit image for diagnostic review

#### FR-IPC-06: System Status and Health Monitoring

The system shall provide a continuous health and status monitoring channel from the Core Engine to the GUI.

EARS Pattern: State-Driven
> WHILE the Core Engine is running, the system shall periodically publish system health metrics (process status, hardware status, error counts) to the GUI via a server-streaming health service.

**FR-IPC-06a: Heartbeat**
EARS Pattern: Event-Driven
> WHEN the Core Engine fails to send a heartbeat within the configured interval, THEN the GUI shall transition to a disconnected/degraded display state and initiate reconnection.

**FR-IPC-06b: Hardware Status**
EARS Pattern: Event-Driven
> WHEN the hardware status of any monitored component changes (e.g., generator ready, detector online), THEN the Core Engine shall push a HardwareStatusEvent to the GUI.

#### FR-IPC-07: Configuration Synchronization

The system shall provide a mechanism for the GUI to read and write configuration parameters managed by the Core Engine.

EARS Pattern: Event-Driven
> WHEN the GUI requests a configuration read or write, THEN the Core Engine shall process the request and return the result including the updated configuration state.

**FR-IPC-07a: Initial Sync**
EARS Pattern: Event-Driven
> WHEN the GUI connects to the Core Engine for the first time in a session, THEN the system shall perform a full configuration synchronization to populate the GUI state.

### 3.2 Non-Functional Requirements

#### NFR-IPC-01: Image Transfer Latency

The system shall transfer a complete full-resolution 16-bit image (up to 9MP) from Core Engine to GUI in under 50 milliseconds measured at the application layer (first chunk sent to last chunk received).

EARS Pattern: Ubiquitous
> The IPC image transfer service shall complete full-resolution image delivery within 50ms under normal operating conditions.

#### NFR-IPC-02: Command Round-Trip Latency

The system shall complete a command request-response cycle from GUI to Core Engine and back in under 10 milliseconds under normal operating conditions.

EARS Pattern: Ubiquitous
> The IPC command service shall deliver a response to the GUI within 10ms of the GUI sending a command request.

#### NFR-IPC-03: Crash Isolation

The system shall ensure that a crash or hang in the GUI process does not cause a crash, hang, or loss of data integrity in the Core Engine process.

EARS Pattern: If-Then (Unwanted Behavior)
> If the GUI process terminates unexpectedly, then the Core Engine shall detect the lost connection, log the event, and continue operating in standalone mode without entering a fault state.

#### NFR-IPC-04: Automatic Reconnection

The system shall automatically re-establish IPC connectivity when the connection is lost, without requiring operator intervention.

EARS Pattern: Event-Driven
> WHEN the IPC connection is lost, THEN the client (GUI) shall attempt automatic reconnection using an exponential backoff strategy (initial 500ms, max 30s, jitter applied) until the connection is restored or a maximum retry count is reached.

#### NFR-IPC-05: Concurrent Message Streams

The system shall support concurrent independent gRPC streams for commands, image data, health monitoring, and configuration synchronization without mutual blocking.

EARS Pattern: Ubiquitous
> The IPC subsystem shall support simultaneous active gRPC streams across all service channels (command, image, health, configuration) without head-of-line blocking.

#### NFR-IPC-06: IEC 62304 Auditability

The system shall log all IPC messages at a configurable verbosity level sufficient to support post-incident analysis, including message type, timestamp, and result code.

EARS Pattern: Ubiquitous
> The IPC subsystem shall record structured log entries for every command request, response, image transfer initiation, transfer completion, error, and reconnection event.

#### NFR-IPC-07: Proto Schema Versioning

The system shall maintain backward compatibility of the proto schema for minor version increments. Breaking changes shall require a major version bump and coordinated deployment.

EARS Pattern: Ubiquitous
> The IPC interface versioning shall follow semantic versioning: minor additions (new fields, new optional RPCs) shall be backward compatible; removal or type changes of existing fields shall require a major version increment.

---

## 4. Specifications

### 4.1 Package and Directory Structure

```
hnvue-console/
├── proto/                          # Shared: single source of truth
│   ├── hnvue_ipc.proto             # Root proto file
│   ├── hnvue_command.proto         # Command service definitions
│   ├── hnvue_image.proto           # Image streaming service definitions
│   ├── hnvue_health.proto          # Health monitoring service definitions
│   ├── hnvue_config.proto          # Configuration sync service definitions
│   └── hnvue_common.proto          # Shared message types (enums, errors)
│
├── libs/hnvue-ipc/                 # C++ IPC library (Core Engine side)
│   ├── CMakeLists.txt
│   ├── include/
│   │   └── hnvue/ipc/
│   │       ├── IpcServer.h         # gRPC server lifecycle management
│   │       ├── CommandServiceImpl.h
│   │       ├── ImageServiceImpl.h
│   │       ├── HealthServiceImpl.h
│   │       └── ConfigServiceImpl.h
│   └── src/
│       ├── IpcServer.cpp
│       ├── CommandServiceImpl.cpp
│       ├── ImageServiceImpl.cpp
│       ├── HealthServiceImpl.cpp
│       └── ConfigServiceImpl.cpp
│
└── src/HnVue.Ipc.Client/           # C# IPC client library (GUI side)
    ├── HnVue.Ipc.Client.csproj     # Packaged as NuGet
    ├── IpcClient.cs                # gRPC client lifecycle management
    ├── CommandChannel.cs
    ├── ImageChannel.cs
    ├── HealthChannel.cs
    └── ConfigChannel.cs
```

### 4.2 Protocol Buffer Service Definitions

The following proto definitions are normative. The `proto/` directory is the single source of truth. All C++ and C# code is generated from these definitions via protoc.

#### 4.2.1 hnvue_common.proto

```protobuf
syntax = "proto3";
package hnvue.ipc;
option csharp_namespace = "HnVue.Ipc";

// Semantic version of the IPC interface contract.
// Minor increments: backward compatible additions only.
// Major increments: breaking changes requiring coordinated deployment.
message InterfaceVersion {
  uint32 major = 1;
  uint32 minor = 2;
  uint32 patch = 3;
}

// Standardized error information included in all responses.
message IpcError {
  ErrorCode code = 1;
  string message = 2;  // Human-readable description, English only
  string detail = 3;   // Optional structured detail (e.g., JSON or key=value)
}

enum ErrorCode {
  ERROR_CODE_UNSPECIFIED = 0;
  ERROR_CODE_OK = 1;
  ERROR_CODE_INVALID_ARGUMENT = 2;
  ERROR_CODE_HARDWARE_NOT_READY = 3;
  ERROR_CODE_ACQUISITION_IN_PROGRESS = 4;
  ERROR_CODE_CONFIGURATION_REJECTED = 5;
  ERROR_CODE_INTERNAL = 6;
  ERROR_CODE_TIMEOUT = 7;
}

// Monotonic timestamp in microseconds since Core Engine start.
// Allows latency measurement independent of wall clock sync.
message Timestamp {
  uint64 microseconds_since_start = 1;
}
```

#### 4.2.2 hnvue_command.proto

```protobuf
syntax = "proto3";
package hnvue.ipc;
import "hnvue_common.proto";
option csharp_namespace = "HnVue.Ipc";

// Command service: GUI → Core Engine (unary request/response)
service CommandService {
  // Initiate an X-ray exposure sequence
  rpc StartExposure(StartExposureRequest) returns (StartExposureResponse);

  // Abort an in-progress exposure
  rpc AbortExposure(AbortExposureRequest) returns (AbortExposureResponse);

  // Move collimator blades to specified position
  rpc SetCollimator(SetCollimatorRequest) returns (SetCollimatorResponse);

  // Execute calibration routine
  rpc RunCalibration(RunCalibrationRequest) returns (RunCalibrationResponse);

  // Query current system state (synchronous snapshot)
  rpc GetSystemState(GetSystemStateRequest) returns (GetSystemStateResponse);
}

message StartExposureRequest {
  ExposureParameters parameters = 1;
  Timestamp request_timestamp = 2;
}

message ExposureParameters {
  float kv = 1;             // Kilovoltage (kV)
  float mas = 2;            // Milliampere-seconds (mAs)
  uint32 detector_id = 3;   // Target detector panel identifier
  ImageTransferMode transfer_mode = 4;
}

enum ImageTransferMode {
  IMAGE_TRANSFER_MODE_UNSPECIFIED = 0;
  IMAGE_TRANSFER_MODE_PREVIEW = 1;
  IMAGE_TRANSFER_MODE_FULL_QUALITY = 2;
}

message StartExposureResponse {
  bool success = 1;
  uint64 acquisition_id = 2;  // Unique ID for this acquisition
  IpcError error = 3;
  Timestamp response_timestamp = 4;
}

message AbortExposureRequest {
  uint64 acquisition_id = 1;
  Timestamp request_timestamp = 2;
}

message AbortExposureResponse {
  bool success = 1;
  IpcError error = 2;
  Timestamp response_timestamp = 3;
}

message SetCollimatorRequest {
  CollimatorPosition position = 1;
  Timestamp request_timestamp = 2;
}

message CollimatorPosition {
  float left_mm = 1;
  float right_mm = 2;
  float top_mm = 3;
  float bottom_mm = 4;
}

message SetCollimatorResponse {
  bool success = 1;
  CollimatorPosition actual_position = 2;  // Position after command applied
  IpcError error = 3;
  Timestamp response_timestamp = 4;
}

message RunCalibrationRequest {
  CalibrationMode mode = 1;
  Timestamp request_timestamp = 2;
}

enum CalibrationMode {
  CALIBRATION_MODE_UNSPECIFIED = 0;
  CALIBRATION_MODE_DARK_FIELD = 1;
  CALIBRATION_MODE_FLAT_FIELD = 2;
  CALIBRATION_MODE_GAIN = 3;
}

message RunCalibrationResponse {
  bool success = 1;
  IpcError error = 2;
  Timestamp response_timestamp = 3;
}

message GetSystemStateRequest {
  Timestamp request_timestamp = 1;
}

message GetSystemStateResponse {
  SystemState state = 1;
  Timestamp response_timestamp = 2;
}

enum SystemState {
  SYSTEM_STATE_UNSPECIFIED = 0;
  SYSTEM_STATE_INITIALIZING = 1;
  SYSTEM_STATE_READY = 2;
  SYSTEM_STATE_ACQUIRING = 3;
  SYSTEM_STATE_CALIBRATING = 4;
  SYSTEM_STATE_FAULT = 5;
  SYSTEM_STATE_SHUTTING_DOWN = 6;
}
```

#### 4.2.3 hnvue_image.proto

```protobuf
syntax = "proto3";
package hnvue.ipc;
import "hnvue_common.proto";
option csharp_namespace = "HnVue.Ipc";

// Image service: Core Engine → GUI (server-streaming)
service ImageService {
  // Stream image data after acquisition completes.
  // Core Engine initiates streaming; GUI subscribes.
  rpc SubscribeImageStream(ImageStreamRequest) returns (stream ImageChunk);
}

message ImageStreamRequest {
  // Subscription filter: zero means subscribe to all acquisitions
  uint64 acquisition_id_filter = 1;
  ImageTransferMode preferred_mode = 2;
}

message ImageChunk {
  uint64 acquisition_id = 1;
  ImageMetadata metadata = 2;    // Present only in first chunk (sequence_number == 0)
  uint32 sequence_number = 3;    // Zero-indexed chunk sequence
  uint32 total_chunks = 4;       // Total number of chunks for this image
  bytes pixel_data = 5;          // Raw 16-bit little-endian grayscale pixels, chunk slice
  bool is_last_chunk = 6;
  IpcError error = 7;            // Non-zero if transfer failed
  Timestamp chunk_timestamp = 8;
}

message ImageMetadata {
  uint32 width_pixels = 1;
  uint32 height_pixels = 2;
  uint32 bits_per_pixel = 3;     // Always 16 for diagnostic images
  float pixel_pitch_mm = 4;
  ImageTransferMode transfer_mode = 5;
  Timestamp acquisition_timestamp = 6;
  float kv_actual = 7;
  float mas_actual = 8;
  uint32 detector_id = 9;
}
```

#### 4.2.4 hnvue_health.proto

```protobuf
syntax = "proto3";
package hnvue.ipc;
import "hnvue_common.proto";
option csharp_namespace = "HnVue.Ipc";

// Health service: Core Engine → GUI (server-streaming)
service HealthService {
  // Subscribe to continuous health and status updates.
  // Core Engine streams at a configured interval (default: 1Hz).
  rpc SubscribeHealth(HealthSubscribeRequest) returns (stream HealthEvent);
}

message HealthSubscribeRequest {
  // Requested event types. Empty means subscribe to all.
  repeated HealthEventType event_type_filter = 1;
}

message HealthEvent {
  HealthEventType event_type = 1;
  Timestamp event_timestamp = 2;

  oneof payload {
    HeartbeatPayload heartbeat = 3;
    HardwareStatusPayload hardware_status = 4;
    FaultPayload fault = 5;
    SystemStateChangePayload state_change = 6;
  }
}

enum HealthEventType {
  HEALTH_EVENT_TYPE_UNSPECIFIED = 0;
  HEALTH_EVENT_TYPE_HEARTBEAT = 1;
  HEALTH_EVENT_TYPE_HARDWARE_STATUS = 2;
  HEALTH_EVENT_TYPE_FAULT = 3;
  HEALTH_EVENT_TYPE_STATE_CHANGE = 4;
}

message HeartbeatPayload {
  uint64 sequence_number = 1;
  float cpu_usage_percent = 2;
  float memory_usage_mb = 3;
}

message HardwareStatusPayload {
  uint32 component_id = 1;
  string component_name = 2;
  HardwareComponentStatus status = 3;
  string detail = 4;
}

enum HardwareComponentStatus {
  HARDWARE_STATUS_UNSPECIFIED = 0;
  HARDWARE_STATUS_ONLINE = 1;
  HARDWARE_STATUS_OFFLINE = 2;
  HARDWARE_STATUS_DEGRADED = 3;
  HARDWARE_STATUS_FAULT = 4;
}

message FaultPayload {
  uint32 fault_code = 1;
  string fault_description = 2;
  FaultSeverity severity = 3;
  bool requires_operator_action = 4;
}

enum FaultSeverity {
  FAULT_SEVERITY_UNSPECIFIED = 0;
  FAULT_SEVERITY_WARNING = 1;
  FAULT_SEVERITY_ERROR = 2;
  FAULT_SEVERITY_CRITICAL = 3;
}

message SystemStateChangePayload {
  SystemState previous_state = 1;
  SystemState new_state = 2;
  string reason = 3;
}

// Re-use SystemState from hnvue_command.proto
import "hnvue_command.proto";
```

#### 4.2.5 hnvue_config.proto

```protobuf
syntax = "proto3";
package hnvue.ipc;
import "hnvue_common.proto";
option csharp_namespace = "HnVue.Ipc";

// Configuration service: GUI ↔ Core Engine (bidirectional unary)
service ConfigService {
  // Read current configuration from Core Engine
  rpc GetConfiguration(GetConfigRequest) returns (GetConfigResponse);

  // Write (update) configuration on Core Engine
  rpc SetConfiguration(SetConfigRequest) returns (SetConfigResponse);

  // Subscribe to configuration change notifications (Core → GUI push)
  rpc SubscribeConfigChanges(ConfigChangeSubscribeRequest)
      returns (stream ConfigChangeEvent);
}

message GetConfigRequest {
  repeated string parameter_keys = 1;  // Empty means fetch all parameters
  Timestamp request_timestamp = 2;
}

message GetConfigResponse {
  map<string, ConfigValue> parameters = 1;
  Timestamp response_timestamp = 2;
  IpcError error = 3;
}

message SetConfigRequest {
  map<string, ConfigValue> parameters = 1;
  Timestamp request_timestamp = 2;
}

message SetConfigResponse {
  bool success = 1;
  map<string, ConfigValue> applied_parameters = 2;
  repeated string rejected_keys = 3;  // Keys that failed validation
  IpcError error = 4;
  Timestamp response_timestamp = 5;
}

message ConfigChangeSubscribeRequest {
  repeated string parameter_keys = 1;  // Empty means watch all parameters
}

message ConfigChangeEvent {
  string parameter_key = 1;
  ConfigValue old_value = 2;
  ConfigValue new_value = 3;
  ConfigChangeSource source = 4;
  Timestamp change_timestamp = 5;
}

enum ConfigChangeSource {
  CONFIG_CHANGE_SOURCE_UNSPECIFIED = 0;
  CONFIG_CHANGE_SOURCE_GUI = 1;       // Change initiated by GUI
  CONFIG_CHANGE_SOURCE_CORE = 2;      // Change initiated by Core Engine internally
  CONFIG_CHANGE_SOURCE_STARTUP = 3;   // Applied during initialization
}

message ConfigValue {
  oneof value {
    bool bool_value = 1;
    int64 int_value = 2;
    double double_value = 3;
    string string_value = 4;
    bytes bytes_value = 5;
  }
}
```

### 4.3 Connection Management and Reconnection Specification

#### 4.3.1 Server Startup (C++ Core Engine)

The C++ IpcServer shall:

1. Bind a gRPC server to `localhost:50051` (default port, configurable)
2. Register all four service implementations (Command, Image, Health, Config)
3. Start serving before signaling readiness to the host application
4. Log the bound address and InterfaceVersion at startup

The Core Engine shall expose an `InterfaceVersion` constant in a generated header so the C# client can perform version compatibility verification at connect time.

#### 4.3.2 Client Connection Lifecycle (C# GUI)

```
State Machine:
  DISCONNECTED --> (connect attempt) --> CONNECTING
  CONNECTING --> (handshake success) --> CONNECTED
  CONNECTING --> (timeout/error) --> BACKOFF
  BACKOFF --> (backoff elapsed) --> CONNECTING
  CONNECTED --> (connection lost) --> RECONNECTING
  RECONNECTING --> (success) --> CONNECTED
  RECONNECTING --> (max retries exceeded) --> FAULT
  FAULT --> (operator reset) --> DISCONNECTED
```

**Reconnection Policy:**

| Attempt | Wait Before Retry |
|---|---|
| 1 | 500ms |
| 2 | 1,000ms |
| 3 | 2,000ms |
| 4 | 4,000ms |
| 5+ | 30,000ms (max), with ±10% jitter |

Maximum consecutive failures before entering FAULT state: 10 (configurable).

#### 4.3.3 Heartbeat-Based Disconnect Detection

- Core Engine sends `HeartbeatPayload` every 1,000ms (configurable)
- GUI considers connection lost if no heartbeat received for 3,000ms (configurable, must be > 2x heartbeat interval)
- GUI initiates reconnection immediately upon detecting the timeout

#### 4.3.4 Graceful Shutdown

EARS Pattern: Event-Driven
> WHEN the Core Engine initiates a planned shutdown, THEN the Core Engine shall send a `SYSTEM_STATE_SHUTTING_DOWN` state change event before closing gRPC connections, allowing the GUI to update its display state cleanly.

EARS Pattern: If-Then
> If the GUI process terminates without sending a shutdown signal, THEN the Core Engine shall detect the lost RPC call and log the disconnection without entering a fault state.

### 4.4 Error Handling Specification

#### 4.4.1 Command Errors

- All command RPCs return an `IpcError` field in their response message
- The Core Engine shall never leave a command call unresponded; if processing cannot complete within a configurable timeout (default 5s), return `ERROR_CODE_TIMEOUT`
- The GUI shall not issue a new command of the same type if the previous call is still in-flight (idempotency guard is the GUI's responsibility)

#### 4.4.2 Image Transfer Errors

- If an error occurs mid-stream, the Core Engine shall emit a final `ImageChunk` with `is_last_chunk = true` and a non-zero `error` field
- The GUI shall discard partially received image data upon receiving an error chunk and display an appropriate error state
- The GUI may request a re-transfer by sending a new `StartExposure` command only if the acquisition is still retained in Core Engine memory (subject to Core Engine retention policy)

#### 4.4.3 gRPC Status Codes

The following gRPC status codes have defined handling behavior:

| gRPC Status | Meaning in HnVue IPC | GUI Action |
|---|---|---|
| OK | Request processed successfully | Update UI state |
| UNAVAILABLE | Server not reachable | Trigger reconnection |
| DEADLINE_EXCEEDED | Request took too long | Show timeout error, log |
| INVALID_ARGUMENT | Malformed request proto | Log, do not retry |
| INTERNAL | Core Engine internal error | Show fault, log |
| CANCELLED | Client cancelled the call | No action required |

### 4.5 Versioning and Schema Evolution

**Rule 1: Backward Compatible Changes (Minor Version Bump)**
- Adding new optional proto fields (proto3 fields are optional by default)
- Adding new enum values (with UNSPECIFIED = 0 default handling)
- Adding new RPC methods to a service

**Rule 2: Breaking Changes (Major Version Bump)**
- Removing or renaming existing fields or RPCs
- Changing field types
- Changing field numbers
- Removing enum values that existing code may receive

**Rule 3: Version Negotiation**
EARS Pattern: Event-Driven
> WHEN the GUI client establishes a connection, THEN the GUI shall call `GetSystemState` and include interface version verification. If the Core Engine major version does not match the GUI's compiled major version, THEN the GUI shall display a version mismatch error and refuse to operate.

### 4.6 Logging and Auditability Specification

All IPC events shall be logged using the application's structured logging facility. Required log fields per event:

| Field | Type | Description |
|---|---|---|
| `timestamp_utc` | ISO 8601 string | Wall clock time of event |
| `timestamp_core_us` | uint64 | Core Engine monotonic timestamp (from `Timestamp` message) |
| `event_type` | string | e.g., `command.start_exposure.request` |
| `acquisition_id` | uint64 | Present when applicable |
| `result_code` | string | `ErrorCode` enum name or gRPC status code name |
| `latency_us` | uint64 | Round-trip time for request/response pairs |
| `detail` | string | Optional human-readable detail |

Log verbosity levels:

| Level | Events Logged |
|---|---|
| ERROR | Faults, failed commands, transfer errors, reconnection failure |
| WARN | Reconnection attempts, version mismatches, unexpected state transitions |
| INFO | Connection established/lost, command request/response, image transfer start/complete |
| DEBUG | Heartbeat events, configuration changes, individual chunk transfers |

### 4.7 Build System Integration

#### 4.7.1 C++ (CMake)

The `libs/hnvue-ipc/CMakeLists.txt` shall:
- Locate the `proto/` directory relative to the repository root
- Run `protoc` with `--grpc_out` and `--cpp_out` as part of a pre-build custom target
- Link the generated sources into the `hnvue-ipc` static library
- Export `hnvue-ipc` as a CMake target for the Core Engine to consume

#### 4.7.2 C# (MSBuild / .csproj)

The `HnVue.Ipc.Client.csproj` shall:
- Reference the `Grpc.Tools` NuGet package
- Include all `.proto` files in the `proto/` directory as `<Protobuf>` items with `GrpcServices="Client"` attribute
- Generate C# client stubs during build
- Package as a NuGet package (`HnVue.Ipc.Client`) for consumption by the WPF GUI project

### 4.8 Traceability Matrix

| Requirement | Proto Definition | Implementation Location |
|---|---|---|
| FR-IPC-01 | Process boundary enforced by architecture | IpcServer.h, IpcClient.cs |
| FR-IPC-02 | All services use gRPC | All .proto files |
| FR-IPC-03 | proto3 syntax | All .proto files |
| FR-IPC-04a | CommandService | hnvue_command.proto, CommandServiceImpl.* |
| FR-IPC-04b | ImageService, HealthService | hnvue_image.proto, hnvue_health.proto |
| FR-IPC-05 | ImageService.SubscribeImageStream | hnvue_image.proto, ImageServiceImpl.* |
| FR-IPC-05a | ImageChunk streaming | hnvue_image.proto |
| FR-IPC-05b | ImageTransferMode enum | hnvue_command.proto, hnvue_image.proto |
| FR-IPC-06 | HealthService.SubscribeHealth | hnvue_health.proto, HealthServiceImpl.* |
| FR-IPC-06a | HeartbeatPayload | hnvue_health.proto |
| FR-IPC-06b | HardwareStatusPayload | hnvue_health.proto |
| FR-IPC-07 | ConfigService | hnvue_config.proto, ConfigServiceImpl.* |
| FR-IPC-07a | GetConfiguration on connect | IpcClient.cs connection lifecycle |
| NFR-IPC-01 | ImageChunk.chunk_timestamp | Performance measured at ImageChannel.cs |
| NFR-IPC-02 | Timestamp in all request/response | Measured at CommandChannel.cs |
| NFR-IPC-03 | Process isolation by design | OS-level, no IPC-layer code |
| NFR-IPC-04 | Client state machine | IpcClient.cs reconnection logic |
| NFR-IPC-05 | Independent gRPC channels per service | IpcClient.cs channel configuration |
| NFR-IPC-06 | Log fields specification | All service implementations |
| NFR-IPC-07 | InterfaceVersion message | hnvue_common.proto |

---

## 5. Out of Scope

The following are explicitly excluded from SPEC-IPC-001:

- Shared memory IPC (reserved for future SPEC if throughput requirements change)
- Network-remote IPC (between different physical machines)
- Authentication or encryption of the gRPC channel (localhost-only, deferred to security review)
- Core Engine internal implementation of image acquisition (covered by SPEC-HAL-001)
- Image processing algorithms applied after IPC delivery (covered by SPEC-IMAGING-001)
- GUI rendering and display logic (covered by SPEC-UI-001)
- Dose calculation logic (covered by SPEC-DOSE-001)

---

## 6. Open Questions

| ID | Question | Owner | Resolution Target |
|---|---|---|---|
| OQ-IPC-01 | Should gRPC TLS be enabled for localhost to meet any forthcoming cybersecurity requirements? | Security Lead | Before implementation |
| OQ-IPC-02 | What is the Core Engine image retention policy (how long is an acquired image held for potential re-request)? | Core Engine Team | Before acceptance testing |
| OQ-IPC-03 | Is the default gRPC port (50051) acceptable, or should it be dynamically allocated and communicated via a sidecar mechanism? | Infrastructure Team | Before implementation |
| OQ-IPC-04 | What is the maximum acceptable gRPC message size limit for a single chunk? Default is 4MB. Needs validation against 9MP image chunking strategy. | Core Engine Team | Before implementation |

---

*SPEC-IPC-001 v1.0 - HnVue Inter-Process Communication*
*TAG: SPEC-IPC-001 | Created: 2026-02-17 | Status: Planned*
