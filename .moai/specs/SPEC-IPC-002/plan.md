# SPEC-IPC-002: Implementation Plan

<!-- TAG: SPEC-IPC-002 -->

---

## 1. Implementation Strategy

### 1.1 Methodology

- **Development Mode**: TDD (RED-GREEN-REFACTOR)
- **Language**: C# 12 / .NET 8
- **Testing**: xUnit + Moq
- **Reference Pattern**: PatientServiceAdapter, WorklistServiceAdapter (SPEC-IPC-001 완성된 어댑터)

### 1.2 Implementation Order

Proto 변경 -> DoseServiceAdapter (P1, Safety Critical) -> ImageServiceAdapter (P2) -> GrpcAdapterBase Deadline (P3) -> AuditLog Injection (P4)

Safety-critical DoseServiceAdapter를 먼저 구현하여 IEC 62304 Class C 요구사항을 조기에 검증한다.

---

## 2. Milestones

### Milestone 1: Proto File Changes (Priority: High - Foundation)

**목표**: 새로운 RPC 정의 추가

**작업 항목**:
1. `proto/hnvue_image.proto`에 `GetImage` RPC 추가
   - `GetImageRequest` / `GetImageResponse` 메시지 정의
   - protoc로 C# stub 생성 확인
2. `proto/hnvue_dose.proto`에 `ResetStudyDose` RPC 추가
   - `ResetStudyDoseRequest` / `ResetStudyDoseResponse` 메시지 정의
   - protoc로 C# stub 생성 확인

**완료 기준**: Proto 컴파일 성공, 생성된 C# 코드 빌드 통과

### Milestone 2: DoseServiceAdapter Implementation (Priority: High - Safety Critical)

**목표**: IEC 62304 Class C 준수 DoseServiceAdapter 완전 구현

**작업 항목**:
1. **RED**: `GetCurrentDoseDisplayAsync` 테스트 작성
   - gRPC mock으로 `GetDoseSummary` 응답 설정
   - `DoseDisplay` 매핑 검증
   - 실패 시 예외 전파 검증 (0 반환 금지)
2. **GREEN**: `GetCurrentDoseDisplayAsync` 구현
   - `DoseService.DoseServiceClient.GetDoseSummaryAsync()` 호출
   - Proto 응답 -> `DoseDisplay` 매핑
3. **RED**: `GetAlertThresholdAsync` 테스트 작성
   - `ConfigService.GetConfiguration` mock
   - 정상 응답 / 실패 시 보수적 기본값(50/100 mGy) 검증
4. **GREEN**: `GetAlertThresholdAsync` 구현
   - ConfigService 통해 threshold 조회
   - Fail-safe 기본값 적용
5. **RED**: `SetAlertThresholdAsync` 테스트 작성
   - `ConfigService.SetConfiguration` + `IAuditLogService.LogAsync` 호출 검증
6. **GREEN**: `SetAlertThresholdAsync` 구현
   - Configuration 저장 + 감사 로그 기록
7. **RED**: `SubscribeDoseUpdatesAsync` 테스트 작성
   - Server-streaming mock으로 `IAsyncEnumerable<DoseUpdate>` 변환 검증
8. **GREEN**: `SubscribeDoseUpdatesAsync` 구현
   - `SubscribeDoseAlerts` 스트림 -> `DoseUpdate` 매핑
9. **RED**: `ResetCumulativeDoseAsync` 테스트 작성
   - `ResetStudyDose` RPC 호출 + 감사 로그 검증
10. **GREEN**: `ResetCumulativeDoseAsync` 구현
11. **REFACTOR**: 공통 매핑 로직 추출, 에러 핸들링 패턴 정리

**완료 기준**: 모든 DoseServiceAdapter 테스트 통과, 안전 fail-safe 시나리오 검증

### Milestone 3: ImageServiceAdapter Implementation (Priority: High)

**목표**: ImageServiceAdapter gRPC 통합 + 렌더링 위임 구현

**작업 항목**:
1. **RED**: `GetImageAsync` 테스트 작성
   - `ImageService.GetImage` mock으로 `ImageData` 반환 검증
   - 실패 시 null 반환 검증
2. **GREEN**: `GetImageAsync` 구현
   - `GetImage` RPC 호출 -> `ImageData` 매핑
3. **RED**: `GetCurrentImageAsync` 테스트 작성
   - `SubscribeImageStream` mock으로 청크 수집/재조립 검증
4. **GREEN**: `GetCurrentImageAsync` 구현
   - Server-streaming 구독 -> 청크 재조립 -> `ImageData?` 반환
5. **RED**: GUI 표시 메서드(WindowLevel, ZoomPan, Orientation, Transform, Reset) 위임 테스트
   - 렌더링 파이프라인 서비스 호출 검증
6. **GREEN**: GUI 표시 메서드 구현
   - 각 메서드에서 해당 렌더링 서비스 호출
7. **REFACTOR**: 이미지 청크 재조립 로직 정리

**완료 기준**: 모든 ImageServiceAdapter 테스트 통과, gRPC 실패 시 명시적 null/예외 반환

### Milestone 4: GrpcAdapterBase Deadline Enhancement (Priority: Medium)

**목표**: 모든 gRPC 호출에 deadline 정책 적용

**작업 항목**:
1. **RED**: `CreateCallOptions(TimeSpan)` 테스트 작성
   - Deadline이 설정된 `CallOptions` 반환 검증
2. **GREEN**: `GrpcAdapterBase`에 `CreateCallOptions` 메서드 추가
   - `DateTime.UtcNow.Add(deadline)` 기반 CallOptions 생성
3. **RED**: `CreateStreamingCallOptions()` 테스트 작성
   - Deadline 없는 `CallOptions` 반환 검증 (long-lived stream용)
4. **GREEN**: `CreateStreamingCallOptions` 구현
5. 기존 어댑터(Patient, Worklist, Exposure, Network)에 deadline 적용
6. 새 어댑터(Image, Dose)에 deadline 적용
7. **REFACTOR**: 중복 CallOptions 생성 코드 제거

**완료 기준**: 모든 gRPC 호출에 적절한 deadline 설정, 기존 테스트 통과

### Milestone 5: IEC 62304 Audit Log Injection (Priority: Medium)

**목표**: 안전 중요 작업에 감사 로그 추가

**작업 항목**:
1. **RED**: `AECServiceAdapter`에 `IAuditLogService` injection 테스트
   - AEC enable/disable 시 감사 로그 호출 검증
2. **GREEN**: `AECServiceAdapter` 생성자에 `IAuditLogService` 추가
   - Enable/disable 메서드에 `LogAsync` 호출 추가
3. **RED**: 감사 로그 실패가 원래 작업을 중단시키지 않는지 테스트
4. **GREEN**: try-catch로 감사 로그 실패 격리
5. **REFACTOR**: 감사 로그 패턴을 helper 메서드로 추출

**완료 기준**: AEC, Dose 어댑터의 안전 중요 작업에 감사 로그 기록, 감사 실패 격리 확인

---

## 3. Technical Approach

### 3.1 Proto 변경 전략

Proto 파일 변경은 클라이언트(C#) 측만 수행한다. C++ 서버 측 구현은 별도 작업이므로 단위 테스트에서 gRPC mock을 사용하여 서버 의존성 없이 검증한다.

### 3.2 DoseServiceAdapter 안전 설계

```
GetCurrentDoseDisplayAsync:
  try: GetDoseSummary RPC -> Map to DoseDisplay -> return
  catch RpcException: throw (propagate, do NOT return zero)

GetAlertThresholdAsync:
  try: GetConfiguration RPC -> Parse thresholds -> return
  catch: return DoseAlertThreshold(warning: 50, error: 100)  // Conservative defaults

SetAlertThresholdAsync:
  SetConfiguration RPC -> AuditLog.LogAsync(threshold change) -> return
  catch audit failure: log warning, continue
```

### 3.3 ImageServiceAdapter Chunk Reassembly

`GetCurrentImageAsync`는 server-streaming RPC인 `SubscribeImageStream`을 사용한다. CancellationToken과 timeout을 결합하여 무한 대기를 방지한다.

### 3.4 Deadline 적용 패턴

`GrpcAdapterBase`에 deadline 생성 메서드를 추가하고, 기존/신규 모든 어댑터에서 활용한다.

---

## 4. Risks and Mitigation

| Risk | Impact | Mitigation |
|---|---|---|
| C++ 서버 GetImage RPC 미구현 | 통합 테스트 불가 | 단위 테스트 mock으로 검증, 통합 테스트는 C++ 완성 후 |
| Dose 0 반환 버그 (Class C) | 환자 안전 위험 | 명시적 예외 전파, fail-safe 기본값, 전용 테스트 시나리오 |
| gRPC Deadline으로 기존 기능 회귀 | 느린 네트워크에서 timeout | 충분한 deadline 값 (5s/30s), 기존 테스트 전체 재실행 |
| 감사 로그 성능 영향 | 응답 지연 | Fire-and-forget 패턴 또는 비동기 큐 사용 |
| Proto 변경 시 빌드 호환성 | 빌드 실패 | Proto 변경 후 즉시 빌드 검증 |

---

## 5. File Change Map

### New Files
- (없음 - 기존 stub 파일 수정)

### Modified Files
| File | Change Type | Module |
|---|---|---|
| `proto/hnvue_image.proto` | Add GetImage RPC | M1 |
| `proto/hnvue_dose.proto` | Add ResetStudyDose RPC | M1 |
| `src/HnVue.Console/Services/Adapters/DoseServiceAdapter.cs` | Full implementation | M2 |
| `src/HnVue.Console/Services/Adapters/ImageServiceAdapter.cs` | Full implementation | M3 |
| `src/HnVue.Console/Services/Adapters/GrpcAdapterBase.cs` | Add deadline methods | M4 |
| `src/HnVue.Console/Services/Adapters/AECServiceAdapter.cs` | Add audit log injection | M5 |
| `src/HnVue.Console/Services/Adapters/PatientServiceAdapter.cs` | Apply deadline | M4 |
| `src/HnVue.Console/Services/Adapters/WorklistServiceAdapter.cs` | Apply deadline | M4 |
| `src/HnVue.Console/Services/Adapters/ExposureServiceAdapter.cs` | Apply deadline | M4 |
| `src/HnVue.Console/Services/Adapters/NetworkServiceAdapter.cs` | Apply deadline | M4 |

### New Test Files
| File | Purpose |
|---|---|
| `tests/HnVue.Console.Tests/Services/Adapters/DoseServiceAdapterTests.cs` | DoseServiceAdapter TDD |
| `tests/HnVue.Console.Tests/Services/Adapters/ImageServiceAdapterTests.cs` | ImageServiceAdapter TDD |
| `tests/HnVue.Console.Tests/Services/Adapters/GrpcAdapterBaseDeadlineTests.cs` | Deadline policy TDD |
| `tests/HnVue.Console.Tests/Services/Adapters/AECServiceAdapterAuditTests.cs` | AEC audit log TDD |
