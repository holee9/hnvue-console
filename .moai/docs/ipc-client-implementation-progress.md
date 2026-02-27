# C# gRPC Client Implementation Progress

## Status: In Progress (TDD Methodology)

### Overview
Implementing C# WPF GUI client for SPEC-IPC-001 using TDD (RED-GREEN-REFACTOR) methodology.

### Development Mode
- `.moai/config/sections/quality.yaml`: `development_mode: "hybrid"`
- `hybrid_settings.new_features: "tdd"` - Use TDD for NEW code
- For replacing existing placeholders: TDD approach (RED-GREEN-REFACTOR)

### Project Structure
```
src/HnVue.Ipc.Client/
├── IpcClient.cs              ✓ Client lifecycle management
├── IpcClientOptions.cs        ✓ Configuration with validation
├── ConnectionState.cs         ✓ State enum
├── ReconnectionPolicy.cs      ✓ Exponential backoff logic
├── CommandChannel.cs          ⚠ Has placeholders - needs gRPC integration
├── ImageChannel.cs            ⚠ Has placeholders - needs gRPC integration
├── HealthChannel.cs           ⚠ Has placeholders - needs gRPC integration
└── ConfigChannel.cs           ⚠ Has placeholders - needs gRPC integration

tests/HnVue.Ipc.Client.Tests/
├── IpcClientTests.cs          ✓ Connection state tests
├── CommandChannelTests.cs     ✓ Placeholder tests exist
├── ImageChannelTests.cs       ✓ Placeholder tests exist
├── HealthChannelTests.cs      ✓ Placeholder tests exist
├── ConfigChannelTests.cs      ✓ Placeholder tests exist
├── ReconnectionPolicyTests.cs ✓ Reconnection logic tests
└── ServerConnectionTests.cs   ✓ NEW: TDD tests for server connection
```

### Proto Files (Shared with C++)
```
proto/
├── hnvue_common.proto         ✓ Common types (IpcError, Timestamp, InterfaceVersion)
├── hnvue_command.proto        ✓ CommandService RPCs
├── hnvue_image.proto          ✓ ImageService streaming
├── hnvue_health.proto         ✓ HealthService monitoring
└── hnvue_config.proto         ✓ ConfigService sync
```

### Implementation Progress

#### ✅ Phase 1: Core Infrastructure (COMPLETE)
- [x] IpcClient lifecycle management (connect/disconnect/reconnect)
- [x] ConnectionState enum with state machine
- [x] ReconnectionPolicy with exponential backoff
- [x] IpcClientOptions with validation
- [x] Basic test structure with xUnit + Moq + FluentAssertions

#### 🔄 Phase 2: gRPC Integration (IN PROGRESS)
- [ ] CommandChannel with actual gRPC client calls
  - [ ] StartExposureAsync - convert between C# types and proto types
  - [ ] AbortExposureAsync
  - [ ] SetCollimatorAsync
  - [ ] RunCalibrationAsync
  - [ ] GetSystemStateAsync (with version negotiation)
- [ ] ImageChannel with streaming support
  - [ ] SubscribeImageStreamAsync - server streaming
  - [ ] Chunk reassembly logic
  - [ ] Preview vs FullQuality mode
- [ ] HealthChannel with monitoring
  - [ ] SubscribeHealthAsync - server streaming
  - [ ] Heartbeat detection (3s timeout)
  - [ ] HardwareStatus change handling
  - [ ] Fault notification handling
- [ ] ConfigChannel with sync
  - [ ] GetConfigurationAsync
  - [ ] SetConfigurationAsync
  - [ ] SubscribeConfigChangesAsync - server streaming

#### ⏳ Phase 3: Integration Testing (PENDING)
- [ ] Mock gRPC server setup for integration tests
- [ ] End-to-end connection testing
- [ ] Version compatibility testing
- [ ] Reconnection scenario testing
- [ ] Performance testing (latency targets)

### Key Requirements to Verify

#### FR-IPC-04a: Command Channel (GUI → Core)
- StartExposure with ExposureParameters
- AbortExposure with acquisition_id
- SetCollimator with position
- RunCalibration with mode
- GetSystemState (version negotiation)

#### FR-IPC-05: Image Streaming (Core → GUI)
- Server streaming for ImageChunk
- Chunked transfer for 9MP images
- Preview vs FullQuality mode
- Transfer completion detection

#### FR-IPC-06: Health Monitoring (Core → GUI)
- HeartbeatPayload every 1s
- 3s timeout detection
- HardwareStatus change events
- Fault notification events

#### NFR-IPC-01: Image Transfer Latency < 50ms
- Measure from first chunk to last chunk

#### NFR-IPC-02: Command Round-Trip < 10ms
- Measure from request to response

#### NFR-IPC-04: Automatic Reconnection
- Exponential backoff: 500ms → 30s max
- Jitter: ±10%
- Max 10 retries before FAULT state

### Next Steps

1. **Immediate**: Implement CommandChannel with actual gRPC calls
   - Replace placeholder `Task.Delay` with actual RPC calls
   - Convert between domain types and proto-generated types
   - Implement proper error handling and logging

2. **Follow-up**: Implement ImageChannel streaming
   - Handle server-streaming response
   - Reassemble chunks into complete image
   - Implement progress reporting

3. **Then**: Implement HealthChannel monitoring
   - Subscribe to health stream
   - Implement heartbeat timeout detection
   - Raise events for status changes

4. **Finally**: Implement ConfigChannel sync
   - Get/Set configuration RPCs
   - Subscribe to config changes
   - Initial sync on connection

### Dependencies
- Grpc.Net.Client - gRPC client for .NET ✓
- Google.Protobuf - Protobuf serialization ✓
- Microsoft.Extensions.Logging - Structured logging ✓
- xUnit - Testing framework ✓
- Moq - Mocking ✓
- FluentAssertions - Assert syntax ✓

### TDD Progress
- ✅ RED: ServerConnectionTests written (failing tests for connection)
- ⏳ GREEN: Implementation in progress
- ⏳ REFACTOR: Pending implementation completion

### Test Coverage Target
- **Target**: 85%+ (per hybrid mode requirements)
- **Current**: ~40% (infrastructure tests, need gRPC integration tests)

---
*Last Updated: 2026-02-27*
*SPEC: SPEC-IPC-001*
