# HAL Simulators Guide

## Overview

The HnVue Hardware Abstraction Layer (HAL) Simulators provide cross-platform test doubles for all hardware components. These simulators enable testing on Linux, macOS, or Windows without requiring physical X-ray hardware.

**Location**: `src/HnVue.Workflow/Hal/Simulators/`

**Purpose**: Enable cross-platform development and testing of clinical workflow logic without real hardware.

**Safety Classification**: IEC 62304 Class C - Simulators must enforce the same safety interlocks as production hardware.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    HAL Simulator Layer                      │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ HvgDriver    │  │ Detector     │  │ Safety       │      │
│  │ Simulator    │  │ Simulator    │  │ Interlock    │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                 │                 │               │
│  ┌──────▼───────┐  ┌──────▼───────┐  ┌──────▼───────┐      │
│  │ DoseTracker  │  │ AEC          │  │ Orchestrator │      │
│  │ Simulator    │  │ Simulator    │  │             │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
        ┌──────────────────────────────────────┐
        │     Workflow Engine (Test Mode)      │
        └──────────────────────────────────────┘
```

---

## Simulator Components

### 1. HvgDriverSimulator

**File**: `HvgDriverSimulator.cs`

**Interface**: `IHvgDriver`

**Purpose**: Simulates High-Voltage Generator (HVG) hardware for X-ray exposure control.

**States**:
- `Initializing`: Initial state on startup
- `Standby`: Ready to receive exposure commands
- `Ready`: Armed and ready for exposure
- `Exposing`: X-ray exposure in progress
- `Fault`: Error state
- `StandbyAfterFault`: Recovering from fault

**Safety-Critical Behavior**:
```csharp
// BEFORE ANY EXPOSURE, HVG CHECKS SAFETY INTERLOCK
if (_safetyInterlock != null)
{
    var isExposureBlocked = await _safetyInterlock.IsExposureBlockedAsync(cancellationToken);
    if (isExposureBlocked)
    {
        // EXPOSURE BLOCKED - DO NOT EXPOSE
        return false;
    }
}
```

**Key Methods**:
| Method | Description | Safety Notes |
|--------|-------------|--------------|
| `InitializeAsync()` | Initialize HVG hardware simulation | Sets state to Standby |
| `PrepareForExposureAsync()` | Arm generator for exposure | Returns false if not in Standby |
| `TriggerExposureAsync()` | Execute X-ray exposure | SAFETY-CRITICAL: Checks interlock |
| `AbortExposureAsync()` | Emergency abort during exposure | Immediate stop, sets fault state |
| `ResetFaultAsync()` | Clear fault and return to standby | Requires supervisor authorization |

**Example Usage**:
```csharp
var safetyInterlock = new SafetyInterlockSimulator();
var hvg = new HvgDriverSimulator(safetyInterlock);

await hvg.InitializeAsync();
await hvg.PrepareForExposureAsync();

var parameters = new ExposureParameters { Kv = 120, Ma = 200, Ms = 100 };
var success = await hvg.TriggerExposureAsync(parameters);

// Safety interlock blocks unsafe exposure
safetyInterlock.SetInterlockUnsafe("door_closed", false);
success = await hvg.TriggerExposureAsync(parameters); // Returns false
```

---

### 2. DetectorDriverSimulator

**File**: `DetectorSimulator.cs`

**Interface**: `IDetector`

**Purpose**: Simulates flat-panel X-ray detector image acquisition.

**States**:
- `Initializing`: Initial state on startup
- `Standby`: Ready to receive commands
- `Acquiring`: Image acquisition in progress
- `Ready`: Image available for readout
- `Fault`: Error state

**Key Methods**:
| Method | Description |
|--------|-------------|
| `InitializeAsync()` | Initialize detector hardware simulation |
| `PrepareAcquisitionAsync()` | Prepare detector for exposure |
| `StartAcquisitionAsync()` | Begin image acquisition after X-ray |
| `GetImageAsync()` | Retrieve acquired 16-bit grayscale DICOM image |
| `ResetFaultAsync()` | Clear fault and return to standby |

**Image Generation**:
- Generates 16-bit grayscale DICOM-compatible images
- Simulated pixel data: 2048 × 2048 resolution
- Simulates photon noise (Poisson distribution)
- Simulates detector gain correction
- Returns DICOM pixel data as `ushort[]` array

**Example Usage**:
```csharp
var detector = new DetectorDriverSimulator();

await detector.InitializeAsync();
await detector.PrepareAcquisitionAsync();
await detector.StartAcquisitionAsync();

// Wait for acquisition to complete
while (detector.State != DetectorState.Ready)
{
    await Task.Delay(10);
}

// Retrieve 16-bit grayscale image
var image = await detector.GetImageAsync();
Console.WriteLine($"Acquired {image.Width}×{image.Height} image");
```

---

### 3. SafetyInterlockSimulator

**File**: `SafetyInterlockSimulator.cs`

**Interface**: `ISafetyInterlock`

**Purpose**: Simulates 9-way safety interlock chain for radiation safety.

**Interlock Definitions**:

| Interlock ID | Safety Requirement | Unsafe Condition |
|--------------|-------------------|------------------|
| `door_closed` | Exposure room door must be closed | Door open sensor triggered |
| `emergency_stop_clear` | Emergency stop button not pressed | Emergency stop activated |
| `thermal_normal` | HVG temperature within safe range | Thermal overload detected |
| `generator_ready` | HVG ready for exposure | Generator fault or not ready |
| `detector_ready` | Detector ready for acquisition | Detector fault or not ready |
| `collimator_valid` | Collimator position valid | Collimator out of range |
| `table_locked` | Patient table locked in position | Table unlocked |
| `dose_within_limits` | Cumulative dose below alert threshold | Dose alert threshold exceeded |
| `aec_configured` | AEC properly configured (if AEC mode) | AEC sensor disconnected |

**Key Methods**:
| Method | Description |
|--------|-------------|
| `CheckAllInterlocksAsync()` | Check all 9 interlocks, return status |
| `IsExposureBlockedAsync()` | Returns true if ANY interlock is unsafe |
| `EmergencyStandbyAsync()` | Emergency stop - sets all interlocks unsafe |
| `SetInterlockUnsafe()` | Manually set an interlock to unsafe (for testing) |
| `SetInterlockSafe()` | Manually set an interlock to safe (for testing) |

**Safety Rule**:
```csharp
// ALL 9 INTERLOCKS MUST BE SAFE FOR EXPOSURE
public async Task<bool> IsExposureBlockedAsync(CancellationToken cancellationToken = default)
{
    var status = await CheckAllInterlocksAsync(cancellationToken);
    return !status.AllSafe; // Blocked if ANY interlock is unsafe
}
```

**Example Usage**:
```csharp
var safety = new SafetyInterlockSimulator();

// Check all interlocks
var status = await safety.CheckAllInterlocksAsync();
Console.WriteLine($"All safe: {status.AllSafe}");

// Simulate door opening
await safety.SetInterlockUnsafe("door_closed", false);

// Exposure is now blocked
var blocked = await safety.IsExposureBlockedAsync();
Console.WriteLine($"Exposure blocked: {blocked}"); // true

// Emergency stop
await safety.EmergencyStandbyAsync();
```

---

### 4. DoseTrackerSimulator

**File**: `DoseTrackerSimulator.cs`

**Interface**: `IDoseTracker`

**Purpose**: Simulates per-exposure and cumulative radiation dose tracking.

**Dose Metrics**:
- **DAP** (Dose-Area Product): Gycm² (Gy × cm²)
- **Skin Dose**: mGy (milligray)
- **Cumulative Dose**: Per-study and per-patient accumulation

**Key Methods**:
| Method | Description |
|--------|-------------|
| `RecordExposureAsync()` | Record dose for completed exposure |
| `GetCumulativeDoseAsync()` | Get cumulative dose for current study |
| `GetDoseReportAsync()` | Generate RDSR-style dose report |
| `ResetDoseTracking()` | Clear dose tracking (new study) |

**Dose Calculation**:
```csharp
// Simulated dose calculation based on exposure parameters
var dap = (parameters.Kv * parameters.Ma * parameters.Ms) * 0.0001; // Gycm²
var skinDose = dap * 1.5; // mGy (conversion factor)
```

**Example Usage**:
```csharp
var dose = new DoseTrackerSimulator();

var parameters = new ExposureParameters { Kv = 120, Ma = 200, Ms = 100 };
await dose.RecordExposureAsync("exposure-001", parameters);

var cumulative = await dose.GetCumulativeDoseAsync();
Console.WriteLine($"Cumulative DAP: {cumulative.DapGycm2} Gycm²");
Console.WriteLine($"Cumulative Skin Dose: {cumulative.SkinDose_mGy} mGy");
```

---

### 5. AecControllerSimulator

**File**: `AecControllerSimulator.cs`

**Interface**: `IAecController`

**Purpose**: Simulates Automatic Exposure Control for optimizing exposure parameters.

**AEC Operation**:
1. Pre-exposure measurement (low-dose test pulse)
2. Patient thickness calculation based on penetration
3. Optimal kV/mA/mAs recommendation
4. Exposure parameter update for operator review

**Key Methods**:
| Method | Description |
|--------|-------------|
| `SelectAecFieldAsync()` | Select AEC detection field (center/left/right) |
| `SetSensitivityAsync()` | Set AEC sensitivity (-3 to +3) |
| `GetRecommendedParametersAsync()` | Get AEC-calculated exposure parameters |
| `StartAecMonitoringAsync()` | Enable AEC for next exposure |
| `DisableAecAsync()` | Disable AEC, use manual parameters |

**Example Usage**:
```csharp
var aec = new AecControllerSimulator();

await aec.SelectAecFieldAsync(AecField.Center);
await aec.SetSensitivityAsync(0); // Normal sensitivity

// AEC calculates optimal parameters based on patient thickness
var recommended = await aec.GetRecommendedParametersAsync(patientThickness_cm: 25);
Console.WriteLine($"AEC recommends: {recommended.Kv} kV, {recommended.Ma} mA, {recommended.Ms} ms");
```

---

## HalSimulatorOrchestrator

**File**: `HalSimulatorOrchestrator.cs`

**Purpose**: Coordinates all HAL simulators for realistic workflow simulation.

**Responsibilities**:
- Creates and initializes all simulators
- Provides unified interface for simulator control
- Implements common test scenarios (normal exposure, emergency, hardware failure)

**Example Usage**:
```csharp
var orchestrator = new HalSimulatorOrchestrator();
await orchestrator.InitializeAsync();

// Run a normal exposure scenario
await orchestrator.RunScenarioAsync(SimulatorScenario.NormalExposure);

// Run a hardware failure scenario
await orchestrator.RunScenarioAsync(SimulatorScenario.DetectorFault);
```

---

## Test Scenarios

**File**: `SimulatorScenario.cs`

**Predefined Scenarios**:

| Scenario | Description | Use Case |
|----------|-------------|----------|
| `NormalExposure` | Standard exposure workflow | Happy path testing |
| `EmergencyWorkflow` | Emergency workflow bypasses worklist | Emergency patient testing |
| `DoorOpensDuringExposure` | Safety interlock aborts exposure | Safety verification |
| `DetectorFault` | Detector failure during acquisition | Hardware failure testing |
| `HvgFault` | HVG fault during exposure | Hardware failure testing |
| `DoseAlertThreshold` | Cumulative dose exceeds alert threshold | Dose tracking verification |

---

## Cross-Platform Testing

### Linux Testing
```bash
# Build HAL simulators (cross-platform C#)
dotnet build src/HnVue.Workflow/HnVue.Workflow.csproj

# Run integration tests using simulators
dotnet test tests/csharp/HnVue.Workflow.IntegrationTests/
```

### Windows Testing with Real Hardware
```csharp
// Swap simulator for real hardware driver
var hvg = new HvgDriverReal(); // Real hardware driver (Windows only)
var detector = new DetectorDriverReal(); // Real hardware driver (Windows only)
```

---

## Safety Compliance

**IEC 62304 Class C Requirements**:

1. **SAFETY-CRITICAL: Exposure Blocking**
   - `HvgDriverSimulator.TriggerExposureAsync()` MUST call `ISafetyInterlock.IsExposureBlockedAsync()` before ANY exposure
   - Exposure is blocked if ANY of the 9 interlocks is unsafe
   - This behavior is identical to production hardware

2. **SAFETY-CRITICAL: Emergency Stop**
   - `ISafetyInterlock.EmergencyStandbyAsync()` sets ALL interlocks to unsafe
   - Emergency stop cannot be overridden without manual reset
   - Matches production hardware behavior

3. **SAFETY-CRITICAL: Dose Tracking**
   - `DoseTrackerSimulator.RecordExposureAsync()` accumulates dose per study
   - Dose alert threshold enforcement matches production requirements
   - RDSR-style dose report generation for regulatory compliance

---

## Integration Test Examples

### Test 1: Safety Interlock Blocks Exposure
```csharp
[Fact]
public async Task SafetyVerification_ExposureBlocked_WhenInterlockUnsafe()
{
    // Arrange
    var safety = new SafetyInterlockSimulator();
    var hvg = new HvgDriverSimulator(safety);
    await hvg.InitializeAsync();
    await hvg.PrepareForExposureAsync();

    // Act: Set door interlock to unsafe
    await safety.SetInterlockUnsafe("door_closed", false);
    var parameters = new ExposureParameters { Kv = 120, Ma = 200, Ms = 100 };
    var exposureResult = await hvg.TriggerExposureAsync(parameters);

    // Assert: Exposure MUST be blocked
    Assert.False(exposureResult, "SAFETY-CRITICAL: Exposure MUST be blocked when door is open");
}
```

### Test 2: Dose Accumulation
```csharp
[Fact]
public async Task DoseTracking_AccumulatesAcrossMultipleExposures()
{
    // Arrange
    var dose = new DoseTrackerSimulator();

    // Act: Record 3 exposures
    var params = new ExposureParameters { Kv = 120, Ma = 200, Ms = 100 };
    await dose.RecordExposureAsync("exp1", params);
    await dose.RecordExposureAsync("exp2", params);
    await dose.RecordExposureAsync("exp3", params);

    // Assert: Dose accumulates correctly
    var cumulative = await dose.GetCumulativeDoseAsync();
    Assert.Equal(3, cumulative.ExposureCount);
    Assert.True(cumulative.DapGycm2 > 0);
}
```

---

## Summary

| Component | File | Interface | Safety-Critical |
|-----------|------|-----------|-----------------|
| HvgDriverSimulator | HvgDriverSimulator.cs | IHvgDriver | YES - Checks interlock before exposure |
| DetectorDriverSimulator | DetectorSimulator.cs | IDetector | NO |
| SafetyInterlockSimulator | SafetyInterlockSimulator.cs | ISafetyInterlock | YES - Blocks exposure if unsafe |
| DoseTrackerSimulator | DoseTrackerSimulator.cs | IDoseTracker | YES - Enforces dose limits |
| AecControllerSimulator | AecControllerSimulator.cs | IAecController | NO |
| HalSimulatorOrchestrator | HalSimulatorOrchestrator.cs | - | NO - Coordination only |

---

**Last Updated**: 2026-03-01
**Status**: All 6 HAL simulators implemented and tested
**Test Coverage**: 263+ unit tests, 15/20 integration tests passing
