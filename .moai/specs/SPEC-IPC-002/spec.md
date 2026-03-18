---
id: SPEC-IPC-002
version: 1.0.0
status: completed
created: 2026-03-18
updated: 2026-03-18
author: drake
priority: high
issue_number: 0
---

## HISTORY

| Version | Date | Author | Description |
|---------|------|--------|-------------|
| 1.0.0 | 2026-03-18 | drake | Initial SPEC creation |

---

## Overview

| Field | Value |
|---|---|
| SPEC ID | SPEC-IPC-002 |
| Title | gRPC Adapter Implementation (ImageService + DoseService + Common Technical Debt) |
| Product | HnVue - Diagnostic Medical Device X-ray GUI Console SW |
| Status | Draft |
| Priority | High |
| Lifecycle Level | spec-anchored |
| Regulatory Context | IEC 62304 Class B (Image), Class C (Dose - Safety Critical) |
| Related SPECs | SPEC-IPC-001 (Completed), SPEC-SECURITY-001 (Completed) |

---

## 1. Environment

### 1.1 System Context

SPEC-IPC-001에서 정의한 gRPC IPC 아키텍처 위에 구축한다. C++ Core Engine Process와 C# WPF GUI Process 간의 gRPC 통신 채널이 이미 구축되어 있으며, 13개 어댑터 중 4개(Patient, Worklist, Exposure, Network)가 완전 구현되어 참조 패턴을 제공한다.

본 SPEC은 나머지 stub 어댑터 중 **ImageServiceAdapter**와 **DoseServiceAdapter** 2개를 실제 구현하고, 모든 어댑터에 적용되는 공통 기술 부채(gRPC Deadline, 감사 로그)를 해결한다.

### 1.2 Technology Environment

| Component | Technology | Purpose |
|---|---|---|
| Runtime | .NET 8 LTS, C# 12 | WPF GUI Process |
| gRPC Client | Grpc.Net.Client | gRPC 클라이언트 라이브러리 |
| Serialization | Protocol Buffers v3 | 메시지 직렬화 |
| Proto Files | `proto/hnvue_image.proto`, `proto/hnvue_dose.proto`, `proto/hnvue_config.proto` | 서비스 정의 |
| Testing | xUnit, Moq | 단위 테스트 |
| Base Class | GrpcAdapterBase | 공통 gRPC 어댑터 패턴 |

### 1.3 Regulatory Context

| Domain | IEC 62304 Class | Rationale |
|---|---|---|
| Image Display | Class B | 진단 영상 표시 정확성 필수 |
| Dose Management | **Class C** | 환자 안전 - 방사선량 오표시는 직접적 위험 |
| Audit Logging | Class C | 안전 중요 작업의 추적성 필수 |

---

## 2. Assumptions

### 2.1 기술 가정

- [A-1] C++ Core Engine의 gRPC 서버가 proto 파일에 정의된 모든 RPC를 정상 구현하고 있다
- [A-2] `GrpcAdapterBase`의 기존 패턴(채널 관리, 에러 핸들링)이 새 어댑터에도 적용 가능하다
- [A-3] `hnvue_image.proto`에 `GetImage` RPC 추가가 C++ 서버 측 구현 없이도 클라이언트 stub 생성이 가능하다 (서버 구현은 별도 작업)
- [A-4] `hnvue_dose.proto`에 `ResetStudyDose` RPC 추가가 필요하다 (서버 구현은 별도 작업)
- [A-5] `ConfigService.GetConfiguration` / `SetConfiguration` RPC가 이미 서버에 구현되어 있다

### 2.2 안전 가정

- [A-6] Dose 표시 실패 시 0을 반환하면 안 된다 (무방사선으로 오해할 수 있음)
- [A-7] Alert threshold 조회 실패 시 보수적(낮은) 값을 기본값으로 사용해야 한다
- [A-8] 안전 중요 작업(AEC enable/disable, dose threshold 변경)은 반드시 감사 로그에 기록해야 한다

### 2.3 범위 가정

- [A-9] GUI 표시 작업(WindowLevel, ZoomPan, Orientation, Transform)은 gRPC 호출이 아닌 기존 렌더링 파이프라인에 위임한다
- [A-10] 나머지 7개 stub 어댑터(User, AEC, Protocol, AuditLog, QC, Patient extension, Worklist extension)는 본 SPEC 범위 밖이다

---

## 3. Requirements

### Module 1: ImageService Adapter Implementation

<!-- TAG: SPEC-IPC-002-IMG -->

**REQ-IMG-001** [Ubiquitous]
시스템은 **항상** `ImageServiceAdapter`가 `IImageService` 인터페이스의 모든 메서드를 실제 구현으로 제공해야 한다.

**REQ-IMG-002** [Event-Driven]
**WHEN** `GetImageAsync(imageId)`가 호출되면 **THEN** `ImageService.GetImage` RPC를 통해 이미지 데이터를 조회하여 `ImageData` 객체로 반환해야 한다.

**REQ-IMG-003** [Event-Driven]
**WHEN** `GetCurrentImageAsync(studyId)`가 호출되면 **THEN** `ImageService.SubscribeImageStream`을 구독하여 최신 이미지 청크를 수집, 재조립하여 `ImageData?`로 반환해야 한다.

**REQ-IMG-004** [Event-Driven]
**WHEN** `ApplyWindowLevelAsync`, `SetZoomPanAsync`, `SetOrientationAsync`, `ApplyTransformAsync`, `ResetTransformAsync`가 호출되면 **THEN** 기존 렌더링 파이프라인 서비스(GrayscaleRenderer/WindowLevelTransform)에 위임해야 한다.

**REQ-IMG-005** [Unwanted]
시스템은 gRPC 통신 실패 시 빈 `ImageData`를 자동 반환**하지 않아야 한다**. 명시적 예외 또는 null을 반환해야 한다.

### Module 2: DoseService Adapter Implementation (Class C - Safety Critical)

<!-- TAG: SPEC-IPC-002-DOSE -->

**REQ-DOSE-001** [Ubiquitous]
시스템은 **항상** `DoseServiceAdapter`가 `IDoseService` 인터페이스의 모든 메서드를 안전 요구사항을 준수하며 구현해야 한다.

**REQ-DOSE-002** [Event-Driven]
**WHEN** `GetCurrentDoseDisplayAsync()`가 호출되면 **THEN** `DoseService.GetDoseSummary` RPC를 호출하여 최근 30일 dose summary를 `DoseDisplay`로 매핑하여 반환해야 한다.

**REQ-DOSE-003** [Event-Driven]
**WHEN** `GetAlertThresholdAsync()`가 호출되면 **THEN** `ConfigService.GetConfiguration(["dose.warning_threshold_mgy", "dose.error_threshold_mgy"])` RPC를 호출하여 `DoseAlertThreshold`로 반환해야 한다.

**REQ-DOSE-004** [Event-Driven]
**WHEN** `SetAlertThresholdAsync(threshold)`가 호출되면 **THEN** `ConfigService.SetConfiguration()`으로 값을 저장하고 `IAuditLogService`를 통해 감사 로그를 기록해야 한다.

**REQ-DOSE-005** [Event-Driven]
**WHEN** `SubscribeDoseUpdatesAsync()`가 호출되면 **THEN** `DoseService.SubscribeDoseAlerts` 스트리밍 RPC를 구독하여 `IAsyncEnumerable<DoseUpdate>`로 변환하여 반환해야 한다.

**REQ-DOSE-006** [Event-Driven]
**WHEN** `ResetCumulativeDoseAsync(studyId)`가 호출되면 **THEN** `DoseService.ResetStudyDose` RPC를 호출하고 감사 로그를 기록해야 한다.

**REQ-DOSE-007** [Unwanted]
시스템은 `GetCurrentDoseDisplayAsync()` 실패 시 **0을 반환하지 않아야 한다**. 예외를 전파하거나 마지막 유효값을 반환해야 한다.

**REQ-DOSE-008** [State-Driven]
**IF** `GetAlertThresholdAsync()` RPC 호출이 실패하면 **THEN** 보수적 기본값(warning: 50 mGy, error: 100 mGy)을 반환해야 한다.

### Module 3: gRPC Common Infrastructure Improvement

<!-- TAG: SPEC-IPC-002-INFRA -->

**REQ-INFRA-001** [Ubiquitous]
시스템은 **항상** 모든 gRPC 호출에 deadline(timeout)을 설정해야 한다.

**REQ-INFRA-002** [State-Driven]
**IF** 일반 command RPC 호출이면 **THEN** 5초 deadline을 적용해야 한다. **IF** 이미지 전송 RPC 호출이면 **THEN** 30초 deadline을 적용해야 한다.

**REQ-INFRA-003** [Event-Driven]
**WHEN** gRPC 호출이 deadline을 초과하면 **THEN** `RpcException(StatusCode.DeadlineExceeded)`를 발생시키고 경고 로그를 기록해야 한다.

**REQ-INFRA-004** [Unwanted]
시스템은 deadline 없이 gRPC 호출을 수행**하지 않아야 한다** (무한 대기 방지).

### Module 4: IEC 62304 Audit Trail Compliance

<!-- TAG: SPEC-IPC-002-AUDIT -->

**REQ-AUDIT-001** [Ubiquitous]
시스템은 **항상** 안전 중요 작업(dose threshold 변경, AEC enable/disable)을 `IAuditLogService`를 통해 감사 로그로 기록해야 한다.

**REQ-AUDIT-002** [Event-Driven]
**WHEN** `DoseServiceAdapter.SetAlertThresholdAsync()`가 호출되면 **THEN** 변경 전/후 값, 요청자, 타임스탬프를 감사 로그에 기록해야 한다.

**REQ-AUDIT-003** [Event-Driven]
**WHEN** `AECServiceAdapter`에서 AEC enable/disable 작업이 수행되면 **THEN** 작업 유형, 요청자, 타임스탬프를 감사 로그에 기록해야 한다.

**REQ-AUDIT-004** [Unwanted]
시스템은 감사 로그 기록 실패가 원래 작업을 중단시키**지 않아야 한다**. 감사 실패는 별도 경고 로그로 기록한다.

---

## 4. Technical Specifications

### 4.1 Proto File Changes

#### hnvue_image.proto 추가

```protobuf
// New RPC
rpc GetImage(GetImageRequest) returns (GetImageResponse);

message GetImageRequest {
  string image_id = 1;
}

message GetImageResponse {
  bytes pixel_data = 1;
  int32 width = 2;
  int32 height = 3;
  int32 bits_per_pixel = 4;
  string image_id = 5;
}
```

#### hnvue_dose.proto 추가

```protobuf
// New RPC
rpc ResetStudyDose(ResetStudyDoseRequest) returns (ResetStudyDoseResponse);

message ResetStudyDoseRequest {
  string study_id = 1;
  string reason = 2;
}

message ResetStudyDoseResponse {
  bool success = 1;
  string message = 2;
}
```

### 4.2 gRPC Deadline Policy

| RPC Category | Deadline | Examples |
|---|---|---|
| Command/Query | 5s | GetDoseSummary, GetConfiguration, GetImage |
| Image Streaming | 30s | SubscribeImageStream |
| Alert Streaming | No deadline (long-lived) | SubscribeDoseAlerts |
| Configuration Write | 5s | SetConfiguration, ResetStudyDose |

### 4.3 Safety Fail-Safe Values

| Field | Fail-Safe Value | Rationale |
|---|---|---|
| DoseDisplay on error | Exception propagation | 0 반환 시 무방사선 오해 위험 |
| Warning Threshold (default) | 50 mGy | 보수적(낮은) 값으로 조기 경고 |
| Error Threshold (default) | 100 mGy | 보수적(낮은) 값으로 조기 차단 |
| ImageData on error | null 반환 | 빈 이미지 표시 방지 |

### 4.4 Adapter Architecture

```
ImageServiceAdapter
  ├── gRPC calls: GetImage, SubscribeImageStream
  ├── Local delegation: WindowLevel, ZoomPan, Orientation, Transform → Rendering pipeline
  └── Dependencies: ImageService.ImageServiceClient, ILogger

DoseServiceAdapter (Class C)
  ├── gRPC calls: GetDoseSummary, GetConfiguration, SetConfiguration, SubscribeDoseAlerts, ResetStudyDose
  ├── Safety: Fail-safe defaults, exception propagation (no silent zeros)
  └── Dependencies: DoseService.DoseServiceClient, ConfigService.ConfigServiceClient, IAuditLogService, ILogger

GrpcAdapterBase (Enhanced)
  ├── New: CreateCallOptions(TimeSpan deadline) → CallOptions with deadline
  └── New: CreateStreamingCallOptions() → CallOptions without deadline (long-lived streams)
```

---

## 5. Out of Scope

- 나머지 7개 stub 어댑터(User, AEC body, Protocol, AuditLog, QC) 구현 (AEC 감사 로그 injection만 포함)
- C++ Core Engine 측 proto RPC 서버 구현
- gRPC retry policy / circuit breaker 패턴 (향후 SPEC)
- Image streaming 성능 최적화 (메모리 풀링, zero-copy)
- DoseService의 historical dose trend 분석 기능

---

## 6. Dependencies

| Dependency | Status | Impact |
|---|---|---|
| SPEC-IPC-001 | Completed | gRPC IPC 아키텍처, GrpcAdapterBase, 참조 어댑터 패턴 |
| SPEC-SECURITY-001 | Completed | IAuditLogService, SecurityAuditLogger 인터페이스 |
| C++ GetImage RPC 구현 | Pending | ImageServiceAdapter 통합 테스트 시 필요 (단위 테스트는 mock 가능) |
| C++ ResetStudyDose RPC 구현 | Pending | DoseServiceAdapter 통합 테스트 시 필요 (단위 테스트는 mock 가능) |
