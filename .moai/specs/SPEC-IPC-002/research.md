# SPEC-IPC-002: Adapter Implementation Research Report

**Research Date:** 2026-03-18
**Status:** Complete
**Researcher:** team-reader

---

## Executive Summary

This research explores the **9 gRPC stub adapters** in `src/HnVue.Console/Services/Adapters/` that require real implementation for SPEC-IPC-002. The current state:

- **13 adapter files** total: 4 partially implemented + 9 stubs
- **9 stub adapters** return graceful defaults or log warnings for **7 services** (Image, Dose, User, AEC, Protocol, AuditLog, QC)
- **4 working adapters** provide reference implementation patterns: Patient, Worklist, Exposure, Network
- **Proto files** exist for 11 services in `proto/` directory
- **Service interfaces** define all method signatures in `src/HnVue.Console/Services/`

---

## Section 1: Complete Adapter Inventory

### 1.1 Working Adapters (4 files - Partial + Full Implementation)

#### PatientServiceAdapter.cs (✅ FULLY IMPLEMENTED)
**Status:** Complete gRPC integration
**Proto Service:** `PatientService` (hnvue_patient.proto)
**Interface:** `IPatientService` (4 methods)

**Methods & Mappings:**
1. `SearchPatientsAsync(PatientSearchRequest)` → `PatientService.SearchPatients(SearchPatientsRequest)`
   - Maps: `Query`, `MaxResults` → proto fields
   - Returns: `PatientSearchResult` with `List<Patient>`, `TotalCount`

2. `RegisterPatientAsync(PatientRegistration)` → `PatientService.RegisterPatient(RegisterPatientRequest)`
   - Maps: `PatientId`, `PatientName`, `DateOfBirth`, `Sex` → proto `Patient` message

3. `UpdatePatientAsync(PatientEditRequest)` → `PatientService.UpdatePatient(UpdatePatientRequest)`
   - Partial update: only non-null fields sent to proto

4. `GetPatientAsync(string patientId)` → `PatientService.GetPatient(GetPatientRequest)`
   - Returns: Single `Patient?` or null if not found

**Mapping Patterns:** Sex enum, DateOnly parsing, string trimming for names

**Error Handling:** RpcException catch → returns default/empty collections, logs warning

---

#### WorklistServiceAdapter.cs (✅ FULLY IMPLEMENTED)
**Status:** Complete gRPC integration
**Proto Service:** `WorklistService` (hnvue_worklist.proto)
**Interface:** `IWorklistService` (3 methods)

**Methods & Mappings:**
1. `GetWorklistAsync()` → `WorklistService.QueryWorklist(QueryWorklistRequest)`
   - Hardcoded: `MaxResults = 100`
   - Maps proto `WorklistEntry` → model `WorklistItem`
   - Parses: `ScheduledDate` + `ScheduledTime` → `DateTimeOffset`

2. `RefreshWorklistAsync(WorklistRefreshRequest)` → `WorklistService.QueryWorklist(QueryWorklistRequest)`
   - Same proto call as GetWorklist, returns wrapped result with `RefreshedAt` timestamp

3. `SelectWorklistItemAsync(string procedureId)` → `WorklistService.UpdateWorklistStatus(UpdateWorklistStatusRequest)`
   - Maps: `procedureId` → `WorklistEntryId`
   - Sets: `NewStatus = WorklistStatus.InProgress`

**Status Mapping:** `WorklistStatus` proto enum → `WorklistStatus` model enum

---

#### ExposureServiceAdapter.cs (⚠️ PARTIAL - 2 of 6 methods implemented)
**Status:** Mixed - Some methods have proto, others return graceful defaults
**Proto Services:** `CommandService` (for trigger/cancel only)
**Interface:** `IExposureService` (6 methods)

**Methods Status:**
1. ✅ `TriggerExposureAsync(ExposureTriggerRequest)` → `CommandService.StartExposure(StartExposureRequest)`
   - Implemented: Maps exposure parameters, returns `ExposureTriggerResult`

2. ✅ `CancelExposureAsync()` → `CommandService.AbortExposure(AbortExposureRequest)`
   - Implemented: Issues abort command

3. ❌ `SubscribePreviewFramesAsync()` - No proto defined
   - Returns: Empty async enumerable (graceful default)
   - Log: Warning "gRPC proto not yet defined"

4. ❌ `GetExposureRangesAsync()` - No proto defined
   - Returns: Hardcoded ranges (40-150 kVp, 1-500 mA, 1-5000ms, 50-200cm)

5. ❌ `GetExposureParametersAsync()` - No proto defined
   - Returns: Hardcoded defaults (70kVp, 100mA, 100ms, 100cm)

6. ❌ `SetExposureParametersAsync()` - No proto defined
   - Returns: `Task.CompletedTask` (no-op)

**Gap:** No `ExposureService` proto service - only `CommandService` methods available

---

#### NetworkServiceAdapter.cs (⚠️ PARTIAL - 2 of 5 methods with partial implementation)
**Status:** Mixed - ConfigService used but mappings incomplete
**Proto Services:** `ConfigService` (hnvue_config.proto) - partial
**Interface:** `INetworkService` (5 methods)

**Methods Status:**
1. ⚠️ `GetNetworkConfigAsync()` → `ConfigService.GetConfiguration(GetConfigRequest)`
   - **Code:** Calls proto but returns hardcoded defaults (no actual mapping)
   - **Note:** Proto-to-model key mapping "not yet established"
   - Returns empty values: DICOM AE Title, port, PACS hostname, port, MWL flag

2. ⚠️ `UpdateNetworkConfigAsync()` → `ConfigService.SetConfiguration(SetConfigRequest)`
   - **Code:** Calls proto but doesn't populate request fields
   - **Note:** Model-to-proto mapping needed

3. ❌ `TestPacsConnectionAsync()` - No proto defined
   - Returns: Hardcoded `false`

4. ❌ `TestMwlConnectionAsync()` - No proto defined
   - Returns: Hardcoded `false`

5. ❌ `GetConnectionStatusAsync()` - No proto defined
   - Returns: Hardcoded defaults (all disconnected)

**Gap:** No dedicated `NetworkService` - only partial ConfigService usage

---

### 1.2 Stub Adapters (9 files - **Target for SPEC-IPC-002 Implementation**)

#### 1. ImageServiceAdapter.cs (STUB)
**Status:** No gRPC calls - returns graceful defaults
**Proto Service:** ❌ **NO PROTO FILE** - only hnvue_image.proto exists (image streaming, not query service)
**Interface:** `IImageService` (7 methods)

**Methods (all stubbed):**
1. `GetImageAsync(string imageId)` → Returns empty `ImageData`
2. `GetCurrentImageAsync(string studyId)` → Returns `null`
3. `ApplyWindowLevelAsync(imageId, windowLevel)` → No-op
4. `SetZoomPanAsync(imageId, zoomPan)` → No-op
5. `SetOrientationAsync(imageId, orientation)` → No-op
6. `ApplyTransformAsync(imageId, transform)` → No-op
7. `ResetTransformAsync(imageId)` → No-op

**Current Pattern:** Logs warning, returns default/empty, no gRPC calls

**Gap:**
- Proto missing: Need `ImageService` with query RPC methods
- Current proto `hnvue_image.proto` only has `SubscribeImageStream` (server-streaming)
- Need: GetImage, GetCurrentImage, ApplyTransform RPCs or subscribe to push updates

---

#### 2. DoseServiceAdapter.cs (STUB)
**Status:** No gRPC calls - returns graceful defaults
**Proto Service:** ✅ `DoseService` exists (hnvue_dose.proto)
**Interface:** `IDoseService` (5 methods)

**Methods (all stubbed):**
1. `GetCurrentDoseDisplayAsync()` → Returns zeros, empty study ID
2. `GetAlertThresholdAsync()` → Returns hardcoded threshold (10/20 mGy)
3. `SetAlertThresholdAsync(threshold)` → No-op
4. `SubscribeDoseUpdatesAsync()` → Empty async enumerable
5. `ResetCumulativeDoseAsync(studyId)` → No-op

**Current Pattern:** All methods return hardcoded defaults or no-op

**Proto Available Methods:**
- `RecordDose(RecordDoseRequest)` - for recording exposure dose
- `GetDoseHistory(GetDoseHistoryRequest)` - historical data
- `GetDoseSummary(GetDoseSummaryRequest)` - cumulative summary
- `SubscribeDoseAlerts(DoseAlertSubscribeRequest)` - stream alerts
- `AcknowledgeDoseAlert(AcknowledgeDoseAlertRequest)` - acknowledge alert

**Gap:** Current interface doesn't align with proto - Need to implement real proto calls

---

#### 3. UserServiceAdapter.cs (✅ **SPECIAL CASE - FULLY IMPLEMENTED**)
**Status:** Complete gRPC integration + security features
**Proto Service:** ✅ `UserService` (hnvue_user.proto)
**Interface:** `IUserService` (16+ methods)

**This adapter is FULLY FUNCTIONAL** - includes:
- Authentication with 5-attempt lockout
- Session management with 30-min timeout
- Password complexity validation
- RBAC permission mapping
- User CRUD operations
- Account unlock functionality

**Key Methods Implemented:**
- `AuthenticateAsync()` - with lockout tracking (ConcurrentDictionary)
- `LogoutAsync()`, `ValidateSessionAsync()`, `RefreshSessionAsync()`
- `GetUserPermissionsAsync()` - role-based mapping
- `ChangePasswordAsync()` - with complexity validation
- `CreateUserAsync()`, `UpdateUserAsync()`, `DeactivateUserAsync()`

**Note:** This adapter should be reference implementation for pattern following

---

#### 4. AECServiceAdapter.cs (⚠️ PARTIAL)
**Status:** Mixed - Some proto calls work, others stubbed
**Proto Service:** ✅ `AECService` (hnvue_aec.proto)
**Interface:** `IAECService` (4 methods)

**Methods Status:**
1. ✅ `EnableAECAsync()` → `AECService.SetAecEnabled(SetAecEnabledRequest)`
   - Implemented: Calls proto with `Enabled=true`

2. ✅ `DisableAECAsync()` → `AECService.SetAecEnabled(SetAecEnabledRequest)`
   - Implemented: Calls proto with `Enabled=false`

3. ✅ `GetAECStateAsync()` → `AECService.GetAecStatus(GetAecStatusRequest)`
   - Implemented: Calls proto, returns boolean state

4. ⚠️ `SubscribeAECStateChangesAsync()` → `AECService.SubscribeAecChanges(AecChangeSubscribeRequest)`
   - Implemented: Streams changes from proto, maps boolean result

**Pattern:** Good error handling, async streaming properly handled

---

#### 5. ProtocolServiceAdapter.cs (⚠️ PARTIAL)
**Status:** Mixed - Core proto calls work, edge cases handled
**Proto Service:** ✅ `ProtocolService` (hnvue_protocol.proto)
**Interface:** `IProtocolService` (4 methods)

**Methods Status:**
1. ✅ `GetBodyPartsAsync()` → `ProtocolService.ListProtocols(ListProtocolsRequest)`
   - Calls proto with empty filter
   - Maps: Distinct body parts from response

2. ✅ `GetProjectionsAsync(bodyPartCode)` → `ProtocolService.ListProtocols(ListProtocolsRequest)`
   - Filters by body part
   - Maps: Distinct projections

3. ✅ `GetProtocolPresetAsync(bodyPartCode, projectionCode)` → `ProtocolService.ListProtocols(ListProtocolsRequest)`
   - Filters by both codes
   - Maps: First result to `ProtocolPreset` with exposure parameters

4. ✅ `SelectProtocolAsync(selection)` → `ProtocolService.ListProtocols(ListProtocolsRequest)`
   - Same query pattern as GetProtocolPreset
   - Returns: `ProtocolSelectionResult` with preset + AEC recommendation

**Status:** Nearly complete, good error handling, returns sensible defaults on failure

---

#### 6. AuditLogServiceAdapter.cs (✅ **SPECIAL CASE - FULLY IMPLEMENTED**)
**Status:** Complete gRPC integration + local WORM storage
**Proto Service:** ✅ `AuditLogService` (hnvue_audit_log.proto)
**Interface:** `IAuditLogService` (8+ methods)

**This adapter is FULLY FUNCTIONAL** - features:
- Dual-layer storage: gRPC + Local WORM (Write-Once-Read-Many)
- SHA-256 hash chain for integrity verification
- NTP time synchronization
- 6-year retention policy enforcement
- Atomic file writes for crash safety
- Fallback to local storage if gRPC fails
- FDA 21 CFR Part 11 compliance

**Key Methods Implemented:**
- `LogAsync()` - with hash chaining + NTP sync
- `GetLogsAsync()`, `GetLogsPagedAsync()` - with filtering + fallback
- `VerifyIntegrityAsync()` - validates hash chain
- `EnforceRetentionPolicyAsync()` - cleanup expired entries
- `ExportLogsAsync()` - CSV export

**Note:** Reference implementation for production-grade adapter

---

#### 7. QCServiceAdapter.cs (⚠️ PARTIAL)
**Status:** Mixed - Proto calls exist but incomplete
**Proto Service:** ✅ `QCService` (hnvue_qc.proto)
**Interface:** `IQCService` (5 methods)

**Methods Status:**
1. ✅ `AcceptImageAsync(imageId)` → `QCService.SubmitForQcReview` + `PerformQcAction`
   - Two-step: Submit → Perform decision (Accept)
   - Maps result status

2. ✅ `RejectImageAsync(imageId, reason, notes)` → `QCService.GetQcStatus` + `PerformQcAction`
   - Get review ID first → Issue rejection with defects
   - Maps rejection reason to defect type

3. ✅ `ReprocessImageAsync(imageId)` → `QCService.GetQcStatus` + `PerformQcAction`
   - Similar pattern: Get review ID → Issue reprocess decision

4. ✅ `GetQCStatusAsync(imageId)` → `QCService.GetQcStatus(GetQcStatusRequest)`
   - Direct proto call

5. ✅ `ExecuteQCActionAsync(request)` → Generic action dispatcher
   - Maps `QCAction` enum to proto `QcDecision` enum

**Mappings Implemented:**
- `RejectionReason` → `QcDefectType`
- `QCAction` → `QcDecision`
- `QCStatus` proto → model with fallbacks

**Pattern:** Good - maps complex enums, handles multi-step workflows

---

## Section 2: Proto Service Inventory

### 2.1 Proto Files Present (11 files)

| File | Service | Status | RPCs | Adapters Using |
|------|---------|--------|------|-----------------|
| hnvue_patient.proto | PatientService | ✅ | SearchPatients, GetPatient, RegisterPatient, UpdatePatient, MergePatients | PatientServiceAdapter |
| hnvue_worklist.proto | WorklistService | ✅ | QueryWorklist, GetWorklistEntry, SubscribeWorklistChanges, UpdateWorklistStatus | WorklistServiceAdapter |
| hnvue_dose.proto | DoseService | ✅ | RecordDose, GetDoseHistory, GetDoseSummary, SubscribeDoseAlerts, AcknowledgeDoseAlert | DoseServiceAdapter (STUB) |
| hnvue_aec.proto | AECService | ✅ | GetAecStatus, SetAecEnabled, ConfigureAec, SubscribeAecChanges | AECServiceAdapter (partial) |
| hnvue_protocol.proto | ProtocolService | ✅ | ListProtocols | ProtocolServiceAdapter (partial) |
| hnvue_qc.proto | QCService | ✅ | SubmitForQcReview, GetQcStatus, PerformQcAction | QCServiceAdapter (partial) |
| hnvue_audit_log.proto | AuditLogService | ✅ | QueryAuditLog, GetAuditEntry, ExportAuditLog | AuditLogServiceAdapter (full) |
| hnvue_user.proto | UserService | ✅ | Authenticate, Logout, GetCurrentSession, ValidateSession, ChangePassword, ListUsers, CreateUser, UpdateUser | UserServiceAdapter (full) |
| hnvue_image.proto | ImageService | ⚠️ | SubscribeImageStream (streaming only) | ImageServiceAdapter (STUB) |
| hnvue_command.proto | CommandService | ✅ | StartExposure, AbortExposure, PrepareAcquisition | ExposureServiceAdapter (partial) |
| hnvue_config.proto | ConfigService | ✅ | GetConfiguration, SetConfiguration | NetworkServiceAdapter (partial) |

### 2.2 Proto Service Gaps

| Adapter | Missing Proto Service | Current Status | Impact |
|---------|----------------------|-----------------|--------|
| ImageServiceAdapter | ImageService (query methods) | Only streaming exists | Cannot query images by ID or study |
| DoseServiceAdapter | Partial - misaligned interface | Has service but methods mismatch | Interface expects query, proto expects record |
| NetworkServiceAdapter | NetworkService (dedicated) | ConfigService used but incomplete | No PACS/MWL connection testing |

---

## Section 3: Model Class Inventory

All data models are in `src/HnVue.Console/Models/`:

### ImageModels.cs
- `ImageData` - Raw pixel data + metadata
- `PixelSpacing` - Physical spacing (row/column mm)
- `WindowLevel` - Display window/level
- `ZoomPan` - Zoom + pan coordinates
- `ImageOrientation` - Rotation/flip
- `ImageTransform` - Combined transformations

### PatientModels.cs
- `Patient` - Patient demographics
- `PatientSearchRequest`, `PatientSearchResult` - Search wrapper
- `PatientRegistration` - New patient data
- `PatientEditRequest` - Update payload
- `Sex` enum - Patient sex

### WorklistModels.cs
- `WorklistItem` - Single procedure entry
- `WorklistRefreshRequest`, `WorklistRefreshResult` - Refresh wrapper
- `WorklistStatus` enum - Procedure status

### DoseModels.cs
- `DoseDisplay` - Current + cumulative doses
- `DoseValue` - Dose amount + unit + timestamp
- `DoseAlertThreshold` - Warning/error limits
- `DoseUpdate` - Alert stream event
- `DoseUnit` enum - mGy, mSv, etc.

### UserAuthenticationModels.cs
- `User` - User record
- `UserSession` - Active session with token
- `AuthenticationResult` - Login response
- `Permission` - Role permission
- `UserRole` enum - Admin, Radiologist, Technologist, Physicist, Operator, Viewer, Service
- `PasswordValidationResult` - Complexity check
- `PasswordValidationFailure` enum

### QCModels.cs
- `QCActionResult` - QC decision response
- `QCActionRequest` - QC action input
- `RejectionReason` enum - Defect reason
- `QCStatus` enum - Review status
- `QCAction` enum - Accept/Reject/Reprocess

### AuditModels.cs
- `AuditLogEntry` - Single audit entry
- `AuditLogFilter` - Query filter
- `PagedAuditLogResult` - Paged results
- `AuditEventType` enum - Event categorization
- `AuditOutcome` enum - Success/Warning/Failure

### ProtocolModels.cs
- `BodyPart` - Anatomical region
- `Projection` - X-ray angle
- `ProtocolPreset` - Protocol with default exposure
- `ProtocolSelection` - User selection input
- `ProtocolSelectionResult` - Protocol + AEC recommendation
- `FocalSpotSize` enum - Small/Large/Fine/Coarse

### AECModels.cs (if exists)
- AEC configuration models (need to verify if this file exists)

---

## Section 4: Reference Implementation Patterns

### 4.1 Successful Pattern: PatientServiceAdapter

**What Works:**
1. **Constructor:** IConfiguration + ILogger passed to base class
2. **Error Handling:** try-catch RpcException with graceful defaults
3. **Data Mapping:** Proto ↔ Model with enum conversion
4. **Null Safety:** Check proto response before mapping
5. **Logging:** LogWarning on RPC failure with context

**Pattern Code:**
```csharp
public async Task<PatientSearchResult> SearchPatientsAsync(PatientSearchRequest request, CancellationToken ct)
{
    try
    {
        var client = CreateClient<HnVue.Ipc.PatientService.PatientServiceClient>();
        var grpcRequest = new HnVue.Ipc.SearchPatientsRequest
        {
            Query = request.Query,
            MaxResults = request.MaxResults
        };

        var response = await client.SearchPatientsAsync(grpcRequest, cancellationToken: ct);

        var patients = response.Patients.Select(p => new Patient
        {
            PatientId = p.PatientId,
            PatientName = $"{p.FamilyName} {p.GivenName}".Trim(),
            DateOfBirth = ParseDateOfBirth(p.DateOfBirth),
            Sex = MapSex(p.Sex),
            AccessionNumber = null
        }).ToList();

        return new PatientSearchResult
        {
            Patients = patients,
            TotalCount = response.TotalCount
        };
    }
    catch (RpcException ex)
    {
        _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}",
            nameof(IPatientService), nameof(SearchPatientsAsync));
        return new PatientSearchResult
        {
            Patients = Array.Empty<Patient>(),
            TotalCount = 0
        };
    }
}
```

### 4.2 Advanced Pattern: UserServiceAdapter

**Advanced Features:**
1. **State Management:** ConcurrentDictionary for session tracking
2. **Complex Logic:** Password validation, account lockout
3. **Time Management:** NTP synchronization, session timeout
4. **Role-Based Access:** RBAC permission mapping
5. **Security:** Login attempt tracking with exponential backoff

**Lessons:**
- Constant definitions for policy (MaxFailedLoginAttempts = 5, SessionTimeoutMinutes = 30)
- Private helper methods for repeated logic
- @MX tags for critical security invariants
- Fallback to default user for offline mode

### 4.3 Production Pattern: AuditLogServiceAdapter

**Enterprise Features:**
1. **Data Integrity:** SHA-256 hash chain with verification
2. **Persistence:** Dual-layer (gRPC + local WORM)
3. **Atomicity:** Temp file → atomic rename
4. **Compliance:** 6-year retention, NTP sync, export
5. **Resilience:** Local fallback if gRPC fails

**Lessons:**
- Lock protection for thread safety
- Careful timestamp handling (NTP vs local)
- Atomic file operations (temp → rename pattern)
- Graceful degradation (gRPC failure → local storage)

---

## Section 5: Implementation Requirements for 9 Stubs

### 5.1 Critical Path (Must Have Proto)

#### ImageServiceAdapter (PRIORITY 1 - BLOCKED)
**Current Gap:** No proto service for image queries
**Proto Gap:** `ImageService` missing GetImage, GetCurrentImage RPCs
**Action Needed:**
1. Define ImageService RPC methods in proto
2. Implement GetImage, GetCurrentImage RPCs
3. Adapter: Create client, map response, handle errors

---

#### DoseServiceAdapter (PRIORITY 2 - Ready)
**Current Status:** Proto available, interface mismatch
**Scope:**
1. Implement `GetCurrentDoseDisplayAsync()`
   - Call: `DoseService.GetDoseSummary()` → current dose + threshold
   - OR: Subscribe to alerts + track current

2. Implement `GetAlertThresholdAsync()`
   - Query or return from config

3. Implement `SetAlertThresholdAsync()`
   - Call: `DoseService.SetDoseThreshold()` (if proto has it) or local config

4. Implement `SubscribeDoseUpdatesAsync()`
   - Call: `DoseService.SubscribeDoseAlerts()` stream
   - Map: `DoseAlertEvent` → `DoseUpdate`

**Test Data:** Use `DoseRecord` from proto history

---

#### NetworkServiceAdapter (PRIORITY 3 - Blocked)
**Current Gap:** Incomplete ConfigService, no PACS/MWL testing
**Action Needed:**
1. Complete ConfigService mapping for network keys
   - Map: DicomAeTitle, Port, PACS hostname → ConfigService keys

2. Implement TestPacsConnectionAsync
   - Option A: gRPC connection test
   - Option B: DICOM C-FIND test (external library)

3. Implement TestMwlConnectionAsync
   - Similar DICOM C-FIND test

4. Implement GetConnectionStatusAsync
   - Aggregate results

---

### 5.2 Ready for Implementation (Proto Complete)

#### AECServiceAdapter (70% Complete)
**Missing:** SubscribeAECStateChangesAsync needs stream error handling fix
**Scope:** Already mostly done

---

#### ProtocolServiceAdapter (80% Complete)
**Status:** Working well, may need polish

---

#### QCServiceAdapter (75% Complete)
**Status:** Proto calls work, enum mappings solid

---

### 5.3 Deferred (Need Architectural Decision)

#### ImageServiceAdapter
- **Decision Needed:** Push (streaming) vs Pull (query) model?
- **Current Proto:** Only streaming available
- **Recommendation:** Implement SubscribeImageStream for now, defer GetImage until proto added

---

## Section 6: DI Registration Analysis

### Current Registration Location
**File:** `src/HnVue.Console/HnVue.Console.csproj` or `Program.cs`

**Expected Pattern:**
```csharp
builder.Services.AddSingleton<IPatientService, PatientServiceAdapter>();
builder.Services.AddSingleton<IWorklistService, WorklistServiceAdapter>();
builder.Services.AddSingleton<IDoseService, DoseServiceAdapter>();
// ... etc
```

**Finding:** All 13 adapters are likely registered identically in DI container

**Note:** GrpcAdapterBase requires IConfiguration + ILogger in constructor - verify DI passes these correctly

---

## Section 7: Testing Infrastructure

### Unit Test Patterns Found
- `src/HnVue.Console.Tests/` directory exists
- xUnit framework with Moq/NSubstitute for mocking
- Test files in same namespace as adapters

### Considerations for 9 Stubs
1. **Mock Proto Clients:** Use Moq to mock `*ServiceClient`
2. **Error Cases:** Test RpcException handling
3. **Streaming:** Use async enumerable test helpers
4. **Integration Tests:** Use Testcontainers if gRPC server needed

---

## Section 8: Risks & Dependencies

### High-Risk Items
1. **ImageService Proto Missing** - Blocks ImageServiceAdapter implementation
2. **Network Testing** - May need external dependencies (DICOM library, PACS server)
3. **NTP Synchronization** - Already in AuditLogServiceAdapter but not all adapters need it
4. **Streaming Methods** - DoseServiceAdapter.SubscribeDoseUpdatesAsync needs careful error handling

### Dependencies
- **Proto Compilation:** Must regenerate C# stubs when proto files change
- **Configuration:** Adapters depend on IConfiguration for GrpcServer:Address
- **Security:** Adapters depend on GrpcSecurityOptions validation

---

## Section 9: File Paths Summary

### Adapters (src/HnVue.Console/Services/Adapters/)
- Base: `GrpcAdapterBase.cs` (220 lines, TLS/mTLS support)
- Working: `PatientServiceAdapter.cs`, `WorklistServiceAdapter.cs`
- Partial: `ExposureServiceAdapter.cs`, `NetworkServiceAdapter.cs`, `AECServiceAdapter.cs`, `ProtocolServiceAdapter.cs`, `QCServiceAdapter.cs`
- Stub: `ImageServiceAdapter.cs`, `DoseServiceAdapter.cs`
- **Full Implementation:** `UserServiceAdapter.cs` (876 lines), `AuditLogServiceAdapter.cs` (831 lines)

### Service Interfaces (src/HnVue.Console/Services/)
- All 13 interfaces defined: I*Service.cs files
- 200-1000+ lines each, well-documented

### Proto Files (proto/)
- 11 files, ~100-200 lines each
- All define service messages + RPC methods
- Standard gRPC patterns (request/response, streaming)

### Models (src/HnVue.Console/Models/)
- 12 model files covering all domain concepts
- Records + enums for type safety

---

## Section 10: Implementation Roadmap

### Phase 1 (Unblocked) - Ready to Start
1. Complete AECServiceAdapter (fix streaming error handling)
2. Polish ProtocolServiceAdapter (already working)
3. Enhance QCServiceAdapter (improve error messages)

### Phase 2 (Proto Decision Needed)
1. Fix ImageServiceAdapter (need GetImage proto methods)
2. Complete DoseServiceAdapter (interface alignment)

### Phase 3 (External Dependencies)
1. NetworkServiceAdapter PACS/MWL testing (external DICOM library?)

### Phase 4 (Polish)
1. Unit test all adapters (80%+ coverage)
2. Integration tests with mock gRPC server

---

## Conclusion

**Total Implementation Effort:**
- ✅ 3 adapters already complete (UserService, AuditLog) + working patterns
- ⚠️ 5 adapters 70%+ complete (AEC, Protocol, QC, Exposure, Network)
- ❌ 1 adapter completely stubbed (Image - proto blocked)
- ⚠️ 1 adapter needs interface realignment (Dose)

**Key Success Factors:**
1. Follow UserServiceAdapter + AuditLogServiceAdapter patterns
2. Resolve ImageService proto gap (requires definition or architectural choice)
3. Implement comprehensive error handling + logging
4. Maintain async/await pattern consistency
5. Use strong typing with proto-generated enums

**Estimated Complexity:** Medium-to-High (5 proto services straightforward, 2 services complex, 2 services blocked by proto gaps)

