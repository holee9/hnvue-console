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
| SPEC-WORKFLOW-001 | Workflow Engine (Phase 1-3: State Machine, Handlers, Integration) | 🔄 진행중 | 70% |
| SPEC-UI-001 | WPF Console UI | ❌ 미완료 | 0% |
| SPEC-TEST-001 | Test Infrastructure | 🔄 진행중 | 30% |

**전체 진행률: 6.5/9 SPEC (72%)**

---

## 최근 업데이트

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
│   ├── HnVue.Workflow/      # 🔄 Workflow Engine (Phase 1-3 Complete)
│   │   ├── StateMachine/    # ✅ State Machine, Transition Guards
│   │   ├── States/          # ✅ 10 State Handlers
│   │   ├── Safety/          # ✅ Interlock Checker
│   │   ├── Journal/         # ✅ SQLite Workflow Journal
│   │   ├── Study/           # ✅ Study Context, Multi-Exposure
│   │   ├── Protocol/        # ✅ Protocol Validator
│   │   ├── Dose/            # ✅ Dose Tracking Coordinator
│   │   ├── RejectRetake/    # ✅ Reject/Retake Coordinator
│   │   ├── Events/          # ✅ IPC Event Publisher
│   │   ├── Recovery/        # ✅ Crash Recovery Service
│   │   └── Interfaces/      # ✅ HAL Interfaces
│   └── HnVue.Console/       # ❌ WPF GUI (Pending)
├── tests/                   # Test suites
│   ├── cpp/                 # C++ tests (Google Test)
│   ├── csharp/              # C# tests (xUnit)
│   │   ├── HnVue.Dose.Tests/        # ✅ 222 tests
│   │   └── HnVue.Workflow.Tests/    # ✅ 89 tests
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
```

---

## 문서

- [SPEC 문서](.moai/specs/)
  - [SPEC-DOSE-001: Radiation Dose Management](.moai/specs/SPEC-DOSE-001/spec.md)
  - [SPEC-WORKFLOW-001: Clinical Workflow Engine](.moai/specs/SPEC-WORKFLOW-001/spec.md)
- [아키텍처](docs/)
- [연구 보고서](docs/xray-console-sw-research.md)
- [CHANGELOG](CHANGELOG.md)

---

## 라이선스

Copyright © 2025 abyz-lab. All rights reserved.

---

## 기여

이 프로젝트는 의료용 소프트웨어로 IEC 62304 표준을 따릅니다. 기여 방법은 별도 문서를 참고하십시오.
