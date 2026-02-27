# HnVue IPC Integration Tests

Cross-language integration tests for SPEC-IPC-001: Inter-Process Communication between C++ gRPC server and C# gRPC client.

## Test Scope

These integration tests verify the complete IPC communication path:

1. **Connection Lifecycle** (`ConnectionLifecycleTests.cs`)
   - Server startup → Client connection
   - Graceful disconnect
   - State transitions

2. **Command Round-Trip** (`CommandRoundTripTests.cs`)
   - GetSystemState (echo test)
   - StartExposure with validation
   - SetCollimator with position feedback
   - AbortExposure cancellation
   - RunCalibration modes

## Prerequisites

### 1. Build C++ IPC Server

The integration tests require the C++ gRPC server executable. Build it using CMake:

```bash
# From repository root
mkdir -p build
cd build
cmake .. -DCMAKE_BUILD_TYPE=Debug
cmake --build . --target hnvue-ipc-server

# Or for Release
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --target hnvue-ipc-server
```

The server executable will be located at:
- `build/Debug/hnvue-ipc-server.exe` (Windows Debug)
- `build/Release/hnvue-ipc-server.exe` (Windows Release)
- `build/hnvue-ipc-server` (Linux)

### 2. Manual Server Startup (For Development)

If automatic process management is not working, start the server manually:

```bash
# From build directory
./hnvue-ipc-server --port=50051 --verbose
```

Or on Windows:
```cmd
hnvue-ipc-server.exe --port=50051 --verbose
```

## Running Tests

### Visual Studio

1. Open Test Explorer (Test > Test Explorer)
2. Build the solution
3. Run all tests in `HnVue.Integration.Tests`

### Command Line

```bash
# From repository root
dotnet test tests/integration/HnVue.Integration.Tests/HnVue.Integration.Tests.csproj
```

With verbose output:
```bash
dotnet test tests/integration/HnVue.Integration.Tests/HnVue.Integration.Tests.csproj --logger "console;verbosity=detailed"
```

### VS Code

1. Install .NET Core Test Explorer extension
2. Click on test beakers in the editor margin
3. Select "Run Test"

## Test Categories

### Connection Lifecycle Tests

- `Client_ShouldConnectToServer_Successfully` - Verifies basic connectivity
- `Client_ShouldDisconnectGracefully_StateChangesToDisconnected` - Verifies graceful shutdown
- `Client_ConnectWhenAlreadyConnected_ShouldNotThrow` - Verifies idempotent connection
- `Client_StateChangedEvent_ShouldFireOnStateTransitions` - Verifies event firing
- `Client_InitialState_ShouldBeDisconnected` - Verifies initial state

### Command Round-Trip Tests

- `GetSystemState_ShouldReturnValidResponse_EchoTest` - Basic echo test
- `GetSystemState_RoundTripLatency_ShouldBeUnder10ms` - NFR-IPC-02 latency validation
- `StartExposure_WithValidParameters_ShouldReturnAcquisitionId` - Exposure command
- `SetCollimator_WithValidPosition_ShouldReturnActualPosition` - Collimator control
- `RunCalibration_WithValidMode_ShouldCompleteSuccessfully` - Calibration modes
- `AbortExposure_WithValidAcquisitionId_ShouldCompleteSuccessfully` - Exposure cancellation

## NFR Validation

### NFR-IPC-02: Command Round-Trip Latency

The `GetSystemState_RoundTripLatency_ShouldBeUnder10ms` test validates the 10ms latency requirement. Note that this test may be flaky on CI/CD due to environment variability.

## Troubleshooting

### Server Not Found

If tests fail with "Server not running" message:

1. Verify C++ server build completed successfully
2. Check server executable exists in expected location
3. Start server manually and verify it listens on `localhost:50051`
4. Check firewall settings (localhost communication should be allowed)

### Connection Timeout

If client connection times out:

1. Verify server is running: `netstat -an | grep 50051` (Linux) or `netstat -an | findstr 50051` (Windows)
2. Check server logs for errors
3. Verify port 50051 is not used by another process

### Performance Test Failures

If latency tests fail intermittently:

1. Run tests on a quiet machine (close other applications)
2. Consider increasing tolerance in test assertions
3. Check for background processes consuming CPU

## Future Tests

Additional test scenarios to implement:

1. **Image Streaming Tests**
   - Preview mode subscription
   - Full-quality image transfer
   - Chunk reassembly
   - Latency measurement (< 50ms target)
   - Transfer error handling

2. **Health Monitoring Tests**
   - Heartbeat reception
   - Heartbeat timeout detection
   - Hardware status change events
   - Fault notification

3. **Configuration Sync Tests**
   - Initial configuration sync
   - Get/Set configuration
   - Configuration change subscription
   - Validation rejection handling

4. **Error Scenario Tests**
   - Server crash simulation
   - Network interruption
   - Invalid command parameters
   - Version mismatch detection

## References

- SPEC-IPC-001: HnVue Inter-Process Communication
- SPEC-IPC-001 Section 4.2.2: CommandService RPC definitions
- SPEC-IPC-001 Section 4.3.2: Client Connection Lifecycle
- SPEC-IPC-001 NFR-IPC-02: Command round-trip latency < 10ms
