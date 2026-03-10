# gRPC Adapter Audit Report (2C-1)

## Overview

All gRPC service adapters inherit from `GrpcAdapterBase`, which manages `GrpcChannel` lifecycle and typed client creation. Adapters are located in `src/HnVue.Console/Services/Adapters/`.

## Adapter Status Summary

| # | Adapter | Interface | Status | Real gRPC Methods | Stub Methods | Total |
|---|---------|-----------|--------|-------------------|--------------|-------|
| 1 | SystemStatusServiceAdapter | ISystemStatusService | **Partial** | 3 | 1 | 4 |
| 2 | ExposureServiceAdapter | IExposureService | **Partial** | 2 | 5 | 7 |
| 3 | SystemConfigServiceAdapter | ISystemConfigService | **Partial** | 4 | 2 | 6 |
| 4 | NetworkServiceAdapter | INetworkService | **Partial** | 2 | 3 | 5 |
| 5 | ImageServiceAdapter | IImageService | Stub | 0 | 7 | 7 |
| 6 | PatientServiceAdapter | IPatientService | Stub | 0 | 4 | 4 |
| 7 | WorklistServiceAdapter | IWorklistService | Stub | 0 | 3 | 3 |
| 8 | UserServiceAdapter | IUserService | Stub | 0 | 8 | 8 |
| 9 | DoseServiceAdapter | IDoseService | Stub | 0 | 5 | 5 |
| 10 | AECServiceAdapter | IAECService | Stub | 0 | 4 | 4 |
| 11 | ProtocolServiceAdapter | IProtocolService | Stub | 0 | 4 | 4 |
| 12 | AuditLogServiceAdapter | IAuditLogService | Stub | 0 | 5 | 5 |
| 13 | QCServiceAdapter | IQCService | Stub | 0 | 5 | 5 |

**Totals: 4 partial, 9 stub, 0 complete.**

## Detailed Analysis

### Adapters with Real gRPC Calls (Partial)

#### 1. SystemStatusServiceAdapter
- **GetOverallStatusAsync** - Uses `CommandService.GetSystemState`, maps `SystemState` enum to `ComponentHealth`
- **CanInitiateExposureAsync** - Uses `CommandService.GetSystemState`, checks for `Ready` state
- **SubscribeStatusUpdatesAsync** - Uses `HealthService.SubscribeHealth` streaming call, maps `HardwareStatus` and `Fault` events
- *GetComponentStatusAsync* - Stub (returns null)

#### 2. ExposureServiceAdapter
- **TriggerExposureAsync** - Uses `CommandService.StartExposure`, maps exposure parameters and returns acquisition ID
- **CancelExposureAsync** - Uses `CommandService.AbortExposure`
- *SubscribePreviewFramesAsync* - Stub (yields nothing)
- *GetExposureRangesAsync* - Stub (returns hardcoded ranges)
- *GetExposureParametersAsync* - Stub (returns defaults)
- *SetExposureParametersAsync* - Stub (no-op)

#### 3. SystemConfigServiceAdapter
- **GetConfigAsync** - Uses `ConfigService.GetConfiguration` (but returns empty config; proto-to-model mapping incomplete)
- **GetConfigSectionAsync** - Uses `ConfigService.GetConfiguration` with section key (returns empty object)
- **UpdateConfigAsync** - Uses `ConfigService.SetConfiguration` (sends empty request)
- **StartCalibrationAsync** - Uses `CommandService.RunCalibration`
- *GetCalibrationStatusAsync* - Stub (returns defaults)
- *ValidateNetworkConfigAsync* - Stub (returns true)

#### 4. NetworkServiceAdapter
- **GetNetworkConfigAsync** - Uses `ConfigService.GetConfiguration` with "network" key (returns defaults; mapping incomplete)
- **UpdateNetworkConfigAsync** - Uses `ConfigService.SetConfiguration` (sends empty request)
- *TestPacsConnectionAsync* - Stub (returns false)
- *TestMwlConnectionAsync* - Stub (returns false)
- *GetConnectionStatusAsync* - Stub (returns all-disconnected)

### Fully Stub Adapters

All methods log a warning `"gRPC proto not yet defined for {Service}.{Method}"` and return graceful defaults (empty collections, null, false, or zero values).

- **ImageServiceAdapter** (7 methods) - No proto for image retrieval/manipulation
- **PatientServiceAdapter** (4 methods) - No proto for patient CRUD
- **WorklistServiceAdapter** (3 methods) - No proto for worklist operations
- **UserServiceAdapter** (8 methods) - No proto for user management/auth
- **DoseServiceAdapter** (5 methods) - No proto for dose tracking
- **AECServiceAdapter** (4 methods) - No proto for AEC control
- **ProtocolServiceAdapter** (4 methods) - No proto for protocol selection
- **AuditLogServiceAdapter** (5 methods) - No proto for audit logging
- **QCServiceAdapter** (5 methods) - No proto for QC workflow

## Proto Services Used

The following gRPC services from `HnVue.Ipc` namespace are currently referenced:

| Proto Service | Used By | Methods Called |
|---------------|---------|----------------|
| `CommandService` | SystemStatus, Exposure, SystemConfig | `GetSystemState`, `StartExposure`, `AbortExposure`, `RunCalibration` |
| `HealthService` | SystemStatus | `SubscribeHealth` (server streaming) |
| `ConfigService` | SystemConfig, Network | `GetConfiguration`, `SetConfiguration` |

## Recommended Next Steps for 2C Implementation

### Priority 1 - Complete Partial Adapters
1. **SystemConfigServiceAdapter / NetworkServiceAdapter** - Define proto-to-model key mapping for `ConfigService` responses. Currently the gRPC calls are made but responses are discarded because the key naming convention is not established.
2. **ExposureServiceAdapter** - Implement `SubscribePreviewFramesAsync` streaming, `GetExposureParametersAsync`, and `SetExposureParametersAsync` using `CommandService` or a new proto service.

### Priority 2 - Define New Proto Services
These adapters require new `.proto` definitions on the backend server:
1. **ImageService proto** - Critical for image viewer; needs `GetImage`, `GetCurrentImage`
2. **PatientService proto** - Needed for patient registration and search
3. **WorklistService proto** - Needed for DICOM MWL integration
4. **DoseService proto** - Needed for dose tracking and alerts (IEC 62304 safety)

### Priority 3 - Remaining Stubs
5. **UserService proto** - User management and RBAC
6. **AECService proto** - AEC on/off control and state streaming
7. **ProtocolService proto** - Body part/projection protocol selection
8. **AuditLogService proto** - Audit trail for regulatory compliance
9. **QCService proto** - Image QC accept/reject/reprocess workflow

### Architecture Notes
- All adapters follow a consistent pattern: constructor DI, `CreateClient<T>()` for gRPC client, `RpcException` catch for graceful degradation
- Stub methods return safe defaults so the UI layer can operate in "offline/demo" mode
- The `GrpcAdapterBase` handles channel lifecycle and disposal correctly
- Consider adding health check/connectivity status to `GrpcAdapterBase` for improved error UX
