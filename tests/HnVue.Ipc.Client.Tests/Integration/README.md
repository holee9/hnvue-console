# SPEC-IPC-001 Integration Tests

## Overview

Integration tests validate end-to-end communication between the C++ gRPC server and C# WPF GUI client through real gRPC RPC calls (no mocks).

## Test Architecture

```
C++ Integration Tests (tests/cpp/hnvue-ipc.Tests/integration/)
├── test_integration.cpp       # Complete integration test suite
└── README.md                   # This file

C# Integration Tests (tests/HnVue.Ipc.Client.Tests/Integration/)
├── IpcIntegrationTests.cs     # Complete integration test suite
└── README.md                   # This file (symbolic link)
```

## Prerequisites

### Building C++ Server

```bash
# Build C++ gRPC server
cd build
cmake --build . --target hnvue-ipc-server

# Server binary location
./libs/hnvue-ipc/hnvue-ipc-server
```

### Building C# Client

```bash
# Build C# gRPC client
dotnet build src/HnVue.Ipc.Client/HnVue.Ipc.Client.csproj

# Client library location
src/HnVue.Ipc.Client/bin/Debug/net8.0/HnVue.Ipc.Client.dll
```

## Running Integration Tests

### C++ Integration Tests

```bash
# Build integration tests
cd build
cmake --build . --target hnvue-ipc.IntegrationTests

# Run all integration tests
ctest -R hnvue-ipc.IntegrationTests -V

# Run specific test category
./tests/cpp/hnvue-ipc.Tests/hnvue-ipc.IntegrationTests --gtest_filter="*ServerLifecycle*"

# Run with labels (requires CTest)
ctest -L INTEGRATION -V
```

### C# Integration Tests

```bash
# Build integration tests
dotnet build tests/HnVue.Ipc.Client.Tests/HnVue.Ipc.Client.Tests.csproj

# Start C++ server in separate terminal
./libs/hnvue-ipc/hnvue-ipc-server --port 50051

# Run all integration tests (in another terminal)
dotnet test tests/HnVue.Ipc.Client.Tests/HnVue.Ipc.Client.Tests.csproj --filter "FullyQualifiedName~Integration"

# Run specific test category
dotnet test tests/HnVue.Ipc.Client.Tests/HnVue.Ipc.Client.Tests.csproj --filter "FullyQualifiedName~Connection"
```

## Test Categories

### Category 1: Server Lifecycle Tests

Validate server startup, shutdown, and port binding.

- `ServerStart_BindsToConfiguredPort` - Server starts on configured port
- `ServerStop_ShutdownGracefully` - Server stops gracefully
- `ServerStart_PortInUse_Fails` - Server refuses to start if port occupied

### Category 2: Connection Flow Tests

Validate client connection lifecycle and state transitions.

- `ClientConnection_ConnectsSuccessfully` - Client connects to server
- `ConnectionState_TransitionsCorrectly` - Connection states transition correctly
- `Connection_ServerNotAvailable_Fails` - Connection fails when server unavailable

### Category 3: RPC Call Tests (Unary)

Validate unary RPC calls to all services.

- `CommandService_GetSystemState_ReturnsValidState` - GetSystemState RPC
- `CommandService_StartExposure_ProcessesRequest` - StartExposure RPC
- `ConfigService_GetConfig_ReturnsConfiguration` - GetConfig RPC
- `Command_InvalidParameters_ReturnsError` - Parameter validation

### Category 4: Streaming RPC Tests

Validate server streaming and bidirectional streaming RPCs.

- `HealthService_SubscribeHealth_ReceivesHeartbeats` - Health heartbeat stream
- `ImageService_StreamImage_DeliversChunks` - Image chunk streaming
- `ConfigService_WatchConfig_SendsChanges` - Config change notifications

### Category 5: Error Handling Tests

Validate error handling and recovery scenarios.

- `RpcCall_InvalidParameters_ReturnsInvalidArgument` - Invalid parameter handling
- `RpcCall_Timeout_ReturnsDeadlineExceeded` - RPC timeout handling
- `Connection_Lost_DetectsAndRecovers` - Connection loss detection
- `Version_Mismatch_ConnectionRejected` - Version compatibility check

### Category 6: Concurrent Operations Tests

Validate multiple simultaneous RPC calls and client connections.

- `ConcurrentRpcCalls_AllCompleteSuccessfully` - Concurrent RPC handling
- `MultipleClients_AllConnectSuccessfully` - Multiple client connections
- `Concurrent_MultipleChannels_AllWorkSimultaneously` - Multi-channel operations

### Category 7: Version Negotiation Tests

Validate client-server version compatibility checking.

- `GetSystemState_ReturnsVersionInfo` - Version information retrieval
- `Version_Mismatch_ConnectionRejected` - Version mismatch rejection

### Category 8: Initial Config Sync Tests

Validate automatic configuration synchronization after connection.

- `InitialConfigSync_AfterConnection_InitializesConfig` - Config sync on connect

### Category 9: Heartbeat Disconnect Detection Tests

Validate health monitoring and disconnect detection.

- `Health_SubscribeHealth_ReceivesHeartbeats` - Heartbeat subscription
- `Heartbeat_Stopped_DetectsConnectionLoss` - Disconnect detection
- `HeartbeatTimeout_Exceeded_DetectsDisconnect` - Heartbeat timeout

## Test Scenarios

### Scenario 1: Normal Connection Flow

1. Server starts on localhost:50051
2. Client connects successfully
3. Version check passes (client compatible with server)
4. Initial config sync completes
5. Health subscription begins
6. Client can execute RPC calls

**Expected Result**: All steps complete successfully, client enters CONNECTED state.

### Scenario 2: Command Flow

1. Client connected to server
2. Client sends StartExposure request with parameters (kV=120, mAs=10, DetectorId=1)
3. Server processes request and returns acquisition_id
4. Server queues image for streaming
5. Client receives image stream
6. Client reconstructs complete image

**Expected Result**: Exposure started successfully, image streamed and reconstructed.

### Scenario 3: Streaming Flow

1. Client connected to server
2. Client subscribes to health stream
3. Server sends heartbeat every 1 second
4. Client monitors heartbeat timestamps
5. If heartbeat missing >3 seconds, client detects disconnect
6. Client initiates reconnection if auto-reconnect enabled

**Expected Result**: Heartbeats received regularly, disconnect detected if missing.

### Scenario 4: Error Handling

1. Connection loss detected (heartbeat timeout or RPC failure)
2. Client transitions to RECONNECTING state
3. Client attempts reconnection with exponential backoff
4. After max retries, client transitions to FAULT state
5. Client raises StateChanged event for UI notification
6. User can manually trigger reconnection attempt

**Expected Result**: Automatic reconnection up to max retries, fault state on failure.

## CI/CD Integration

### GitHub Actions

```yaml
name: Integration Tests

on: [push, pull_request]

jobs:
  cpp-integration:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Build C++ server
        run: |
          cmake -B build -DCMAKE_BUILD_TYPE=Debug
          cmake --build build --target hnvue-ipc-server
      - name: Run C++ integration tests
        run: |
          ctest -R hnvue-ipc.IntegrationTests -V

  csharp-integration:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Start C++ server
        run: |
          ./libs/hnvue-ipc/hnvue-ipc-server --port 50051
      - name: Run C# integration tests
        run: |
          dotnet test tests/HnVue.Ipc.Client.Tests --filter "FullyQualifiedName~Integration"
```

### Test Reporting

Integration tests generate JUnit XML format reports for CI/CD integration:

- C++: GTest XML output (`--gtest_output=xml:integration-results.xml`)
- C#: xUnit XML output (`--logger "trx;LogFileName=integration-results.trx"`)

## Known Limitations

1. **Server Availability**: Tests require C++ server to be built and running
2. **Port Conflicts**: Tests use unique port offsets to avoid conflicts in parallel execution
3. **Service Implementation**: Some tests marked as UNIMPLEMENTED until services are complete
4. **Timing Sensitivity**: Streaming tests may be sensitive to system load and timing
5. **External Coordination**: Connection loss tests require external test harness for server control

## Future Enhancements

1. **Mock Server**: Implement in-memory C++ gRPC server for reliable testing
2. **Test Containers**: Docker containers for isolated test environments
3. **Performance Tests**: Add performance benchmarks for RPC latency and throughput
4. **Chaos Testing**: Random failure injection for robustness validation
5. **Contract Testing**: Automated protobuf contract validation

## References

- SPEC-IPC-001: Inter-Process Communication Specification
- gRPC C++ Documentation: https://grpc.io/docs/languages/cpp/
- gRPC C# Documentation: https://grpc.io/docs/languages/csharp/
- Google Test Documentation: https://google.github.io/googletest/
- xUnit Documentation: https://xunit.net/

## Contact

For questions or issues with integration tests, please contact:
- abyz-lab <hnabyz2023@gmail.com>
