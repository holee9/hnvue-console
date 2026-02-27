# Integration Test Execution Guide

## Quick Start

### 1. Build C++ Server

```bash
# From repository root
mkdir -p build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --target hnvue-ipc-server

# Verify executable exists
ls -la hnvue-ipc-server*  # Linux/Mac
dir hnvue-ipc-server.exe  # Windows
```

### 2. Run Tests

```bash
# From repository root
dotnet test tests/integration/HnVue.Integration.Tests/HnVue.Integration.Tests.csproj --logger "console;verbosity=detailed"
```

## Expected Output

Successful test run:

```
Total tests: 8
     Passed: 8
     Failed: 0
 Skipped: 0
```

## Test Details

### ConnectionLifecycleTests (5 tests)

| Test | Description | Expected Result |
|------|-------------|-----------------|
| Client_ShouldConnectToServer_Successfully | Basic connectivity | Connection succeeds |
| Client_ShouldDisconnectGracefully_StateChangesToDisconnected | Graceful shutdown | State = Disconnected |
| Client_ConnectWhenAlreadyConnected_ShouldNotThrow | Idempotent connection | No exception thrown |
| Client_StateChangedEvent_ShouldFireOnStateTransitions | Event firing | Events fire correctly |
| Client_InitialState_ShouldBeDisconnected | Initial state | State = Disconnected |

### CommandRoundTripTests (3+ tests)

| Test | Description | NFR Target |
|------|-------------|------------|
| GetSystemState_ShouldReturnValidResponse_EchoTest | Echo test | - |
| GetSystemState_RoundTripLatency_ShouldBeUnder10ms | Latency | < 10ms |
| StartExposure_WithValidParameters_ShouldReturnAcquisitionId | Exposure | - |
| SetCollimator_WithValidPosition_ShouldReturnActualPosition | Collimator | - |
| RunCalibration_WithValidMode_ShouldCompleteSuccessfully | Calibration | - |
| AbortExposure_WithValidAcquisitionId_ShouldCompleteSuccessfully | Abort | - |

## CI/CD Integration

### GitHub Actions Example

```yaml
name: IPC Integration Tests

on: [push, pull_request]

jobs:
  integration-tests:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Setup CMake
      uses: jwlawson/actions-setup-cmake@v1

    - name: Build C++ Server
      run: |
        mkdir build
        cd build
        cmake .. -DCMAKE_BUILD_TYPE=Release
        cmake --build . --target hnvue-ipc-server

    - name: Run Integration Tests
      run: |
        dotnet test tests/integration/HnVue.Integration.Tests/HnVue.Integration.Tests.csproj --logger "trx;LogFileName=test-results.trx"

    - name: Upload Test Results
      uses: actions/upload-artifact@v3
      with:
        name: test-results
        path: '**/test-results.trx'
```

### Gitea Actions Example

```yaml
name: IPC Integration Tests

on: [push, pull_request]

jobs:
  integration-tests:
    runs-on: windows-latest

    steps:
    - name: Checkout
      uses: actions/checkout@v3

    - name: Setup .NET SDK
      uses: https://github.com/actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Build C++ Server
      run: |
        mkdir build
        cd build
        cmake .. -DCMAKE_BUILD_TYPE=Release
        cmake --build . --target hnvue-ipc-server

    - name: Run Tests
      run: dotnet test tests/integration/HnVue.Integration.Tests/HnVue.Integration.Tests.csproj
```

## Performance Baseline

Record performance baselines for regression detection:

| Test | Target | Baseline | Threshold |
|------|--------|----------|-----------|
| GetSystemState latency | < 10ms | ~2ms | 15ms |
| StartExposure latency | < 10ms | ~3ms | 15ms |
| SetCollimator latency | < 10ms | ~3ms | 15ms |

## Known Issues

1. **Server Process Management**: Automatic process spawning may not work in all environments. Manual server startup is recommended for development.

2. **Latency Test Flakiness**: Performance tests may fail on busy systems. Consider running these tests in isolation on dedicated hardware.

3. **Port Conflicts**: Ensure port 50051 is not used by other applications. Use `--port` flag to specify alternative port if needed.

## Next Steps

1. Implement Image Streaming Tests
2. Implement Health Monitoring Tests
3. Implement Configuration Sync Tests
4. Add automated test execution to CI/CD pipeline
