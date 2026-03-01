# HnVue Console

**HnVue - Diagnostic Medical Device X-ray GUI Console Software**

의료용 X선 장비의 GUI 콘솔 소프트웨어입니다. IEC 62304 Class B/C 표준을 따르며 하이브리드 아키텍처(C++ Core Engine + C# WPF GUI)로 설계되었습니다.

---

## 아키텍처

### 하이브리드 구조
- **C++ Core Engine**: 고성능 이미지 처리, 장치 추상화 계층
- **C# WPF GUI**: 사용자 인터페이스, 진단 뷰어
- **gRPC IPC**: 프로세스 간 통신

### 의존성 흐름
```
INFRA → IPC → HAL/IMAGING → DICOM → DOSE → WORKFLOW → UI
```

---

## 구현 현황

| SPEC | 설명 | 상태 | 진행률 |
|------|------|------|--------|
| SPEC-INFRA-001 | Build/CI/CD 인프라 | ✅ 완료 | 100% |
| SPEC-IPC-001 | gRPC IPC (C++ Server + C# Client) | ✅ 완료 | 100% |
| SPEC-HAL-001 | Hardware Abstraction Layer | ✅ 완료 | 100% |
| SPEC-IMAGING-001 | Image Processing Pipeline | ✅ 완료 | 100% |
| SPEC-DICOM-001 | DICOM Communication Services (Storage/Worklist/MPPS/Commitment/QR) | ✅ 완료 | 100% |
| SPEC-DOSE-001 | Radiation Dose Management (DAP, Cumulative Tracking, RDSR, Audit Trail) | ✅ 완료 | 100% |
| SPEC-WORKFLOW-001 | Workflow Engine (Phase 1-3: State Machine, Protocol, Dose) | ✅ 완료 | 100% |
| SPEC-UI-001 | WPF Console UI (Phase 1: MVVM Architecture Complete) | 🔄 Phase 1 완료 | 60% |
| SPEC-TEST-001 | Test Infrastructure | 🔄 진행중 | 30% |

**전체 진행률: 7/9 SPEC (78%), WORKFLOW Phase 1-3 완료로 안전 임계 경로 구현**

---

## 최근 업데이트

### 2026-03-01: SPEC-WORKFLOW-001 Phase 1-3 완료 - 상태 머신, 프로토콜, 방사선량 ✅

#### 임상 워크플로우 상태 머신 구현 (FR-WF-01 ~ FR-WF-07)

**WorkflowStateMachine 핵심 기능**
- 10개 상태 기반 임상 워크플로우: IDLE → WORKLIST_SYNC → PATIENT_SELECT → PROTOCOL_SELECT → POSITION_AND_PREVIEW → EXPOSURE_TRIGGER → QC_REVIEW → REJECT_RETAKE → MPPS_COMPLETE → PACS_EXPORT
- 비동기 상태 전환: TryTransitionAsync with guard clause evaluation
- 상태 컨텍스트 바인딩: StudyContext를 통한 환자, 프로토콜, 노출 정보 추적
- 이벤트 발행: 상태 변경 이벤트 for UI integration

**안전 임계 가드 (Safety-Critical Guards)**
- InterlockChecker: 9가지 하드웨어 인터락 사전 검증
  - IL-01: KVP_READY, IL-02: MA_READY, IL-03: COLLIMATOR_READY
  - IL-04: DETECTOR_READY, IL-05: TABLE_POSITION_SAFE
  - IL-06: DOOR_CLOSED, IL-07: EMERGENCY_STOP_RELEASED
  - IL-08: AEC_READY, IL-09: PROTOCOL_VALID
- DoseLimitGuard: 누적 방사선량 한도 사전 검증
- ProtocolSafetyGuard: 프로토콜 안전 파라미터 검증

**TransitionResult 상태 타입**
- Success: 상태 전환 성공
- InvalidState: 현재 상태에서 전환 불가
- GuardFailed: 가드 조건 불충족
- Error: 시스템 오류 발생

**상태별 상세 기능**
| 상태 | 기능 | 가드 조건 |
|------|------|-----------|
| IDLE | 시스템 대기 상태 | - |
| WORKLIST_SYNC | MWL 동기화 | DICOM 연결 |
| PATIENT_SELECT | 환자 선택/등록 | 환자 ID 유효 |
| PROTOCOL_SELECT | 촬영 프로토콜 선택 | 프로토콜 안전성 |
| POSITION_AND_PREVIEW | 위치 정렬/프리뷰 | 장비 준비 완료 |
| EXPOSURE_TRIGGER | 방사선 조사 | 모든 인터락 통과 |
| QC_REVIEW | 이미지 품질 검토 | 이미지 존재 |
| REJECT_RETAKE | 재촬영 처리 | 재촬영 횟수 < 3 |
| MPPS_COMPLETE | MPPS 전송 완료 | DICOM 연결 |
| PACS_EXPORT | PACS 송신 | DICOM 연결 |

---

#### 프로토콜 저장소 구현 (FR-WF-08, FR-WF-09)

**Protocol 엔티티 구조**
- ProtocolId: 고유 식별자 (GUID)
- BodyPart: 촬영 부위 (CHEST, ABDOMEN, PELVIS 등)
- Projection: 투영 방식 (PA, AP, LATERAL, OBLIQUE 등)
- Kv: 관 전압 (40-150 kVp)
- Ma: 관 전류 (1-500 mA)
- ExposureTimeMs: 노출 시간 (1-3000 ms)
- CalculatedMas: 계산된 mAs = Kv × Ma × ExposureTime / 1000
- AecMode: AEC 모드 (Disabled, Enabled, Override)
- DeviceModel: 장치 모델 (HVG-3000 등)
- CompositeKey: BodyPart|Projection|DeviceModel 고유 키

**DeviceSafetyLimits 안전 검증**
| 파라미터 | 최소값 | 최대값 | 단위 |
|---------|--------|--------|------|
| MinKvp | 40 | - | kVp |
| MaxKvp | - | 150 | kVp |
| MinMa | 1 | - | mA |
| MaxMa | - | 500 | mA |
| MaxExposureTimeMs | - | 3000 | ms |
| MaxMas | - | 2000 | mAs |

**ProtocolRepository 기능**
- CreateAsync: 프로토콜 생성 + 안전 검증
- UpdateAsync: 프로토콜 수정 + 안전 검증
- DeleteAsync: 소프트 삭제 (is_active = 0)
- GetByCompositeKeyAsync: 복합 키 조회 (< 50ms)
- GetProtocolsByBodyPartAsync: 부위별 조회
- GetByProcedureCodeAsync: 진단 코드별 조회
- GetAllAsync: 전체 활성 프로토콜 조회

**SQLite 스키마**
```sql
CREATE UNIQUE INDEX idx_protocols_composite_unique
  ON protocols(body_part COLLATE NOCASE, projection COLLATE NOCASE, device_model COLLATE NOCASE);

CREATE INDEX idx_protocols_body_part ON protocols(body_part);
CREATE INDEX idx_protocols_active ON protocols(is_active);
```

**성능 최적화**
- WAL 모드: 동시 읽기/쓰기 지원
- 인덱싱: 복합 키 고유 인덱스, 부위 검색 인덱스
- 대소문자 무시: COLLATE NOCASE로 검색
- 조회 성능: 500개 프로토콜 기준 < 50ms

---

#### 방사선량 한도 통합 (FR-WF-04, FR-WF-05)

**MultiExposureCoordinator 다중 노출 추적**
- Study별 누적 방사선량 추적
- 다중 뷰 촬영 지원 (PA, LATERAL, OBLIQUE 등)
- 노출 기록 관리 (ExposureRecord)

**StudyDoseTracker 임상 용량 추적**
- StudyDoseLimit: 연구당 방사선량 한도 (기본 1000 mAs)
- DailyDoseLimit: 일일 방사선량 한도 (기본 5000 mAs)
- WarningThresholdPercent: 경고 임계값 (기본 80%)
- IsWithinLimits: 한도 내 여부 판단
- TotalDap: 총 Dose-Area Product

**DoseLimitConfiguration 설정**
```csharp
public class DoseLimitConfiguration
{
    public decimal StudyDoseLimit { get; set; } = 1000m;      // mAs
    public decimal DailyDoseLimit { get; set; } = 5000m;      // mAs
    public decimal WarningThresholdPercent { get; set; } = 0.8m; // 80%
}
```

**실시간 검증 흐름**
1. 노출 전: CheckDoseLimits(projectedDose) 호출
2. 예상 용량 계산: TotalDap + ProjectedDap
3. 한도 검증: StudyDoseLimit, DailyDoseLimit 비교
4. 경고 발생: 80% 도달 시 경고
5. 거부: 한도 초과 시 노출 거부

---

#### WorkflowEngine 메인 오케스트레이터

**IWorkflowEngine 인터페이스 구현**
- TryTransitionAsync: 비동기 상태 전환
- GetCurrentStateAsync: 현재 상태 조회
- GetStudyContextAsync: 연구 컨텍스트 조회
- SubscribeToEvents: 상태 변경 이벤트 구독

**WorkflowEngine 교체**
- WorkflowEngineStub 삭제
- WorkflowStateMachine 기반 실제 구현으로 대체
- 10개 상태 핸들러 통합
- Guard clause 평가 통합

---

#### 테스트 (170개, 전체 통과)

**프로토콜 테스트 (50개)**
| 테스트 파일 | 테스트 수 | 설명 |
|-------------|----------|------|
| DeviceSafetyLimitsTests | 12 | 안전 한도 검증 |
| ProtocolRepositoryTests | 28 | CRUD, 복합 키 조회 |
| ProtocolTests | 10 | 엔티티 검증 |

**방사선량 테스트 (37개)**
| 테스트 파일 | 테스트 수 | 설명 |
|-------------|----------|------|
| DoseTrackingCoordinatorTests | 15 | 용량 추적, 한도 검증 |
| MultiExposureCoordinatorTests | 12 | 다중 노출 추적 |
| ExposureCollectionTests | 10 | 노출 기록 관리 |

**상태 머신 테스트 (83개)**
| 테스트 파일 | 테스트 수 | 설명 |
|-------------|----------|------|
| WorkflowStateMachineTests | 35 | 상태 전환, 가드 평가 |
| TransitionResultTests | 18 | 전환 결과 검증 |
| GuardEvaluationTypesTests | 15 | 가드 타입 검증 |
| StudyContextTests | 15 | 컨텍스트 관리 |

**성능 테스트**
- CompositeKeyLookupPerformance: 500개 프로토콜 < 50ms 조회
- MultiExposureDoseTracking: 다중 노출 누적 추적

---

#### 기술 사양

**플랫폼**
- .NET 8 LTS
- C# 12
- Windows 10/11

**데이터베이스**
- SQLite 3.x
- WAL 모드 (Write-Ahead Logging)
- COLLATE NOCASE (대소문자 무시)

**안전 분류**
- IEC 62304 Class C (방사선 조사 제어)

**파일 구성**
- Protocol: 6 files (Protocol, ProtocolRepository, DeviceSafetyLimits, IProtocolRepository, ProtocolStub, enums)
- Dose: 4 files (DoseTrackingCoordinator, MultiExposureCollection, DoseLimitConfiguration, ExposureRecord)
- StateMachine: 3 files (WorkflowStateMachine, TransitionResult, GuardEvaluationTypes)
- Engine: 1 file (WorkflowEngine)

**테스트 커버리지**
- 총 170개 테스트 통과
- 프로토콜: 50개
- 방사선량: 37개
- 상태 머신: 83개

---

### 2026-03-01: SPEC-UI-001 Phase 1 완료 - MVVM 아키텍처 구현 ✅

#### UI Layer Foundation Complete
- **MVVM Architecture**: 순수 .NET 8 ViewModel (WPF 의존 없음)
- **16 ViewModels**: Patient, Worklist, Acquisition, ImageReview, SystemStatus, Configuration, AuditLog 등
- **10+ Views**: WPF XAML 기반 프레젠테이션 계층
- **3 Dialog Pairs**: PatientRegistration, PatientEdit, Confirmation, Error

#### 구현 상세

**ViewModels (16 files, ~110KB)**
| ViewModel | 기능 | SPEC 요구사항 |
|-----------|------|---------------|
| `PatientViewModel` | 환자 검색, 등록, 수정 | FR-UI-01 |
| `WorklistViewModel` | MWL 표시 및 선택 | FR-UI-02 |
| `AcquisitionViewModel` | 실시간 촬영 프리뷰 | FR-UI-09 |
| `ImageReviewViewModel` | 이미지 뷰어 (W/L, Zoom, Pan) | FR-UI-03 |
| `ExposureParameterViewModel` | kVp, mA, time, SID, FSS | FR-UI-07 |
| `ProtocolViewModel` | Body part, projection 선택 | FR-UI-06 |
| `DoseViewModel` | 현재/누적 방사선량 표시 | FR-UI-10 |
| `AECViewModel` | AEC 모드 토글 | FR-UI-11 |
| `SystemStatusViewModel` | 시스템 상태 대시보드 | FR-UI-12 |
| `ConfigurationViewModel` | 시스템 설정 관리 | FR-UI-08 |
| `AuditLogViewModel` | 감사 로그 뷰어 | FR-UI-13 |

**Infrastructure Components**
- **Commands**: `RelayCommand`, `AsyncRelayCommand` (ICommand 구현)
- **Converters**: 7개 WPF 값 변환기 (StatusToBrush, BoolToVisibility 등)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection 기반 서비스 등록
- **Models**: 9개 데이터 모델 (Patient, Protocol, Dose, Image 등)
- **Rendering**: `GrayscaleRenderer`, `WindowLevelTransform` (16-bit grayscale 지원)
- **Localization**: ko-KR, en-US 리소스 파일
- **Services**: 13개 인터페이스 + 12개 Mock 구현

**Views (10+ files)**
- `PatientView.xaml` - 환자 관리 화면
- `WorklistView.xaml` - MWL 표시
- `AcquisitionView.xaml` - 촬영 인터페이스
- `ImageReviewView.xaml` - 이미지 리뷰
- `SystemStatusView.xaml` - 시스템 상태
- `ConfigurationView.xaml` - 설정 관리
- `AuditLogView.xaml` - 감사 로그
- `Views/Panels/` - 7개 하위 패널 (AEC, Dose, Protocol, Exposure, ImageViewer, MeasurementTool, QCAction)

**Tests (13 test files)**
- MVVM 준수 테스트 (`MvvmComplianceTests`)
- ViewModel 단위 테스트 (Patient, Worklist, Protocol, SystemStatus 등)
- 테스트 헬퍼 (`ViewModelTestBase`, `MvvmComplianceChecker`)

#### SPEC 요구사항 커버리지

| 카테고리 | 완료 상태 |
|----------|-----------|
| FR-UI-01 ~ FR-UI-13 | ✅ 완료 (ViewModel + View 구현) |
| FR-UI-14 (Multi-Monitor) | ⏳ Phase 4 대기 |
| NFR-UI-01 (MVVM Architecture) | ✅ 완료 (WPF 의존 없음) |
| NFR-UI-02 (Response Time) | ⏳ gRPC 연결 후 측정 |
| NFR-UI-06 (Localization) | ✅ 완료 (ko-KR, en-US) |
| NFR-UI-07 (Testability) | ✅ 완료 (Constructor injection) |

#### 빌드 상태
- **HnVue.Console.dll** (390 KB) 성공적으로 생성
- **TRUST 5 Score**: 84/100 (GOOD)
- **Known Warnings**: 20× CS0579 (WPF 디자인 타임 빌드)

#### Phase 4 남은 작업
1. **gRPC 클라이언트 연동**: Mock*Service → 실제 gRPC 호출
2. **이미지 파이프라인 연동**: 16-bit grayscale 렌더링
3. **측정 도구 구현**: Distance, Angle, Cobb angle overlays
4. **지연 시간 계측**: NFR-UI-02a 준수 확인
5. **하드웨어 연동**: SystemStatusViewModel 실시간 연결

---

### 2026-02-28: SPEC-DOSE-001 & SPEC-WORKFLOW-001 Phase 1-3 완료

#### SPEC-DOSE-001: Radiation Dose Management ✅
- **DAP Calculation**: HVG 파라미터 기반 Dose-Area Product 계산
- **Cumulative Tracking**: Study-level 누적 방사선량 추적
- **Real-time Display**: IObservable 기반 실시간 업데이트
- **DRL Comparison**: Dose Reference Level 비교 및 알림
- **RDSR Integration**: HnVue.Dicom.Rdsr 데이터 제공자
- **Audit Trail**: SHA-256 해시 체인 (FDA 21 CFR Part 11 준수)

**구현 파일**: 20개 source, 12개 test (~5,000 LOC)
**테스트**: 222개 통과

#### SPEC-WORKFLOW-001 Phase 1-3: Clinical Workflow Engine ✅
- **Phase 1: Core State Machine**
  - 10-state WorkflowStateMachine with transition guards
  - TransitionGuardMatrix for state validation
  - SQLite WorkflowJournal with crash recovery
  - 9 hardware interlocks validation (InterlockChecker)

- **Phase 2/3: State Handlers & Infrastructure**
  - 10 State Handlers (Idle, PatientSelect, ProtocolSelect, WorklistSync, PositionAndPreview, ExposureTrigger, QcReview, RejectRetake, MppsComplete, PacsExport)
  - HAL Integration: IHvgDriver, IDetector, IDoseTracker, ISafetyInterlock
  - Multi-Exposure Support: MultiExposureCoordinator for multi-view studies
  - IPC Events: IWorkflowEventPublisher for async event streaming
  - Dose Coordinator: DoseTrackingCoordinator for cumulative dose tracking
  - Protocol Enhancements: ProtocolValidator with exposure parameter validation
  - Reject/Retake: RejectRetakeCoordinator with limit enforcement

**구현 파일**: 79개 source, 44개 test (~13,672 LOC)
**테스트**: 311개 통과 (222 Dose + 89 Workflow)
**MX 태그**: 48개 (@MX:ANCHOR 12, @MX:WARN 6, @MX:NOTE 30+)

#### Phase 4 (향후 계획)
- Hardware Integration: 실제 HAL 드라이버 구현
- DICOM Integration: C-FIND, MPPS, C-STORE 실제 구현
- GUI Integration: WPF/WinUI 이벤트 구독

---

## 기술 스택

### C++ (Core Engine)
- **언어**: C++17, C++20 지원
- **빌드**: CMake 3.25+, vcpkg
- **이미지 처리**: OpenCV 4.x
- **FFT**: FFTW 3.x
- **테스트**: Google Test

### C# (GUI & Services)
- **언어**: C# 12
- **프레임워크**: .NET 8 LTS
- **UI**: WPF
- **DICOM**: fo-dicom 5.x
- **테스트**: xUnit

### IPC
- **프로토콜**: gRPC 1.68.x
- **직렬화**: Protocol Buffers

### CI/CD
- **시스템**: Gitea Actions (self-hosted)

---

## 프로젝트 구조

```
hnvue-console/
├── libs/                    # C++ libraries
│   ├── hnvue-infra/         # ✅ Build infrastructure
│   ├── hnvue-ipc/           # ✅ gRPC IPC library
│   ├── hnvue-hal/           # ✅ Hardware Abstraction Layer
│   └── hnvue-imaging/       # ✅ Image Processing Pipeline
├── src/                     # C# applications
│   ├── HnVue.Ipc.Client/    # ✅ gRPC Client
│   ├── HnVue.Dicom/         # ✅ DICOM Service
│   │   └── Rdsr/            # ✅ RDSR Document Generation
│   ├── HnVue.Dose/          # ✅ Radiation Dose Management
│   │   ├── Calculation/     # ✅ DAP Calculator, Calibration
│   │   ├── Recording/       # ✅ Dose Record Repository, Audit Trail
│   │   ├── Display/         # ✅ Dose Display Notifier
│   │   ├── Alerting/        # ✅ DRL Comparison
│   │   └── RDSR/            # ✅ RDSR Data Provider
│   ├── HnVue.Workflow/      # ✅ Workflow Engine (Phase 1-3 Complete)
│   │   ├── StateMachine/    # ✅ WorkflowStateMachine, TransitionResult, GuardEvaluation
│   │   ├── States/          # ✅ StudyContext (Patient, Protocol, Exposure tracking)
│   │   ├── Safety/          # ✅ InterlockChecker (9 interlocks)
│   │   ├── Study/           # ✅ MultiExposureCollection (Dose tracking)
│   │   ├── Protocol/        # ✅ Protocol, ProtocolRepository, DeviceSafetyLimits
│   │   ├── Dose/            # ✅ DoseTrackingCoordinator, DoseLimitConfiguration
│   │   └── WorkflowEngine.cs # ✅ Main orchestrator (replaces stub)
│   └── HnVue.Console/       # 🔄 WPF GUI (Phase 1 Complete)
│       ├── ViewModels/      # ✅ 16 ViewModels (Patient, Worklist, Acquisition, etc.)
│       ├── Views/           # ✅ 10+ Views (Patient, Worklist, Acquisition, ImageReview, etc.)
│       │   └── Panels/       # ✅ 7 Panels (AEC, Dose, Protocol, Exposure, ImageViewer, MeasurementTool, QCAction)
│       ├── Dialogs/         # ✅ 3 Dialog Pairs (PatientRegistration, PatientEdit, Confirmation, Error)
│       ├── Commands/        # ✅ RelayCommand, AsyncRelayCommand
│       ├── Converters/      # ✅ 7 Value Converters
│       ├── DependencyInjection/ # ✅ Service Registration
│       ├── Models/          # ✅ 9 Data Models (Patient, Protocol, Dose, Image, etc.)
│       ├── Rendering/       # ✅ GrayscaleRenderer, WindowLevelTransform
│       ├── Resources/       # ✅ Localization (ko-KR, en-US) + Styles
│       ├── Services/        # ✅ 13 Interfaces + 12 Mock Implementations
│       └── Shell/           # ✅ MainWindow (Shell Window)
├── tests/                   # Test suites
│   ├── cpp/                 # C++ tests (Google Test)
│   ├── csharp/              # C# tests (xUnit)
│   │   ├── HnVue.Dose.Tests/        # ✅ 222 tests
│   │   ├── HnVue.Workflow.Tests/    # ✅ 170 tests (37 Dose + 133 Protocol/StateMachine)
│   │   └── HnVue.Console.Tests/     # ✅ 13 ViewModel tests + MVVM compliance tests
│   └── integration/         # Integration tests
└── .moai/                   # MoAI-ADK configuration
    └── specs/               # SPEC documents
        ├── SPEC-DOSE-001/   # ✅ Complete
        └── SPEC-WORKFLOW-001/ # ✅ Phase 1-3 Complete
```

---

## 규제 준수

### IEC 62304 Safety Classification
- **SPEC-WORKFLOW-001**: Class C (X-ray exposure control)
- **SPEC-DOSE-001**: Class B (Dose monitoring and display)

### 적용 표준
- IEC 62304: Medical device software lifecycle
- IEC 60601-1: Medical electrical equipment safety
- IEC 60601-2-54: Dose display requirements
- DICOM PS 3.x: Imaging interoperability
- IHE REM Profile: RDSR generation
- FDA 21 CFR Part 11: Audit trail with tamper evidence

---

## 빌드

### 사전 요구 사항
- CMake 3.25+
- C++17 컴파일러 (MSVC on Windows)
- .NET 8 SDK
- vcpkg
- OpenCV 4.x
- FFTW 3.x

### C++ 빌드
```bash
cd libs/hnvue-imaging
cmake -B build -S .
cmake --build build
```

### C# 빌드
```bash
dotnet build src/HnVue.Console/HnVue.Console.sln
```

### 테스트 실행
```bash
# Dose Management Tests
dotnet test tests/csharp/HnVue.Dose.Tests/HnVue.Dose.Tests.csproj

# Workflow Engine Tests
dotnet test tests/csharp/HnVue.Workflow.Tests/HnVue.Workflow.Tests.csproj

# Console UI Tests (ViewModels)
dotnet test tests/csharp/HnVue.Console.Tests/HnVue.Console.Tests.csproj
```

---

## 문서

- [SPEC 문서](.moai/specs/)
  - [SPEC-UI-001: GUI Console User Interface](.moai/specs/SPEC-UI-001/spec.md) - Phase 1 완료
  - [SPEC-DOSE-001: Radiation Dose Management](.moai/specs/SPEC-DOSE-001/spec.md)
  - [SPEC-WORKFLOW-001: Clinical Workflow Engine](.moai/specs/SPEC-WORKFLOW-001/spec.md)
  - [SPEC-IPC-001: Inter-Process Communication](.moai/specs/SPEC-IPC-001/spec.md)
  - [SPEC-IMAGING-001: Image Processing Pipeline](.moai/specs/SPEC-IMAGING-001/spec.md)
  - [SPEC-DICOM-001: DICOM Communication Services](.moai/specs/SPEC-DICOM-001/spec.md)
  - [SPEC-INFRA-001: Project Infrastructure](.moai/specs/SPEC-INFRA-001/spec.md)
- [아키텍처](docs/)
- [연구 보고서](docs/xray-console-sw-research.md)
- [CHANGELOG](CHANGELOG.md)

---

## 라이선스

Copyright © 2025 abyz-lab. All rights reserved.

---

## 기여

이 프로젝트는 의료용 소프트웨어로 IEC 62304 표준을 따릅니다. 기여 방법은 별도 문서를 참고하십시오.
