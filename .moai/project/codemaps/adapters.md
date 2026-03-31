# gRPC Adapter Audit

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

**Totals: 4 partial (31%), 9 stub (69%), 0 complete.**

## Partial Adapters (Real gRPC Calls)

### 1. SystemStatusServiceAdapter
- **GetOverallStatusAsync** - Uses `CommandService.GetSystemState`
- **CanInitiateExposureAsync** - Uses `CommandService.GetSystemState`
- **SubscribeStatusUpdatesAsync** - Uses `HealthService.SubscribeHealth` streaming
- *GetComponentStatusAsync* - Stub (returns null)

### 2. ExposureServiceAdapter
- **TriggerExposureAsync** - Uses `CommandService.StartExposure`
- **CancelExposureAsync** - Uses `CommandService.AbortExposure`
- *SubscribePreviewFramesAsync* - Stub
- *GetExposureRangesAsync* - Stub (hardcoded)
- *GetExposureParametersAsync* - Stub
- *SetExposureParametersAsync* - Stub

### 3. SystemConfigServiceAdapter
- **GetConfigAsync** - Uses `ConfigService.GetConfiguration`
- **GetConfigSectionAsync** - Uses `ConfigService.GetConfiguration`
- **UpdateConfigAsync** - Uses `ConfigService.SetConfiguration`
- **StartCalibrationAsync** - Uses `CommandService.RunCalibration`
- *GetCalibrationStatusAsync* - Stub
- *ValidateNetworkConfigAsync* - Stub

### 4. NetworkServiceAdapter
- **GetNetworkConfigAsync** - Uses `ConfigService.GetConfiguration`
- **UpdateNetworkConfigAsync** - Uses `ConfigService.SetConfiguration`
- *TestPacsConnectionAsync* - Stub
- *TestMwlConnectionAsync* - Stub
- *GetConnectionStatusAsync* - Stub

## Stub Adapters (No gRPC Implementation)

All methods log `"gRPC proto not yet defined for {Service}.{Method}"` and return safe defaults.

- **ImageServiceAdapter** (7 methods) - No proto for image retrieval/manipulation
- **PatientServiceAdapter** (4 methods) - No proto for patient CRUD
- **WorklistServiceAdapter** (3 methods) - No proto for worklist operations
- **UserServiceAdapter** (8 methods) - No proto for user management/auth
- **DoseServiceAdapter** (5 methods) - No proto for dose tracking
- **AECServiceAdapter** (4 methods) - No proto for AEC control
- **ProtocolServiceAdapter** (4 methods) - No proto for protocol selection
- **AuditLogServiceAdapter** (5 methods) - No proto for audit logging
- **QCServiceAdapter** (5 methods) - No proto for QC workflow

## Proto Services Currently Used

| Proto Service | Used By | Methods Called |
|---------------|---------|----------------|
| `CommandService` | SystemStatus, Exposure, SystemConfig | GetSystemState, StartExposure, AbortExposure, RunCalibration |
| `HealthService` | SystemStatus | SubscribeHealth (server streaming) |
| `ConfigService` | SystemConfig, Network | GetConfiguration, SetConfiguration |

## Recommended Implementation Priority

### Priority 1 (Critical - Patient Care)
1. **ImageServiceAdapter** - Image viewer functionality
2. **PatientServiceAdapter** - Patient registration and search
3. **WorklistServiceAdapter** - DICOM MWL integration
4. **DoseServiceAdapter** - IEC 62304 safety requirement

### Priority 2 (High - Clinical Workflow)
5. **ProtocolServiceAdapter** - Protocol selection
6. **AECServiceAdapter** - AEC control
7. **ExposureServiceAdapter** - Complete remaining stubs

### Priority 3 (Medium - System Management)
8. **UserServiceAdapter** - User management and RBAC
9. **SystemConfigServiceAdapter** - Complete remaining stubs
10. **NetworkServiceAdapter** - Complete remaining stubs

### Priority 4 (Low - Support)
11. **QCServiceAdapter** - QC workflow
12. **AuditLogServiceAdapter** - Regulatory compliance
13. **SystemStatusServiceAdapter** - Complete remaining stub

## Architecture Notes

- All adapters follow consistent pattern: constructor DI, `CreateClient<T>()`, `RpcException` handling
- Stub methods return safe defaults for "offline/demo" mode
- `GrpcAdapterBase` handles channel lifecycle and disposal correctly
- Consider adding health check/connectivity status to `GrpcAdapterBase`
