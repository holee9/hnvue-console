# HnVue 아키텍처 및 구조 (Architecture and Structure)

## OVERALL ARCHITECTURE (전체 아키텍처)

### Architectural Pattern (아키텍처 패턴)

**하이브리드 레이어드 아키텍처 (Hybrid Layered Architecture)**

HnVue는 C++ Core Engine과 C# WPF GUI가 결합된 하이브리드 아키텍처를 채택합니다. 두 계층은 gRPC 기반 IPC(Inter-Process Communication)로 통신하며, 각 계층 내에서는 레이어드 패턴을 따릅니다.

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│                   (C# WPF - User Interface)                  │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐            │
│  │   Views     │ │ ViewModels  │ │  Commands   │            │
│  └─────────────┘ └─────────────┘ └─────────────┘            │
├─────────────────────────────────────────────────────────────┤
│                   Application Layer                          │
│                 (C# - Business Logic)                        │
│  ┌─────────────────────────────────────────────────────┐    │
│  │        Workflow Engine (Class C Safety)              │    │
│  │  - Interlock System (9 interlocks)                   │    │
│  │  - Exposure Control                                  │    │
│  │  - State Machine                                     │    │
│  └─────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────┤
│                     Service Layer                            │
│              (C# - Domain Services)                         │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │   DICOM Service  │  │    Dose Service  │                │
│  │  - Import/Export │  │  - Dose Tracking │                │
│  │  - Metadata      │  │  - ALARA Principle│                │
│  └──────────────────┘  └──────────────────┘                │
├─────────────────────────────────────────────────────────────┤
│                      IPC Layer                               │
│            (gRPC + Protobuf Communication)                  │
│  ┌──────────────────┐              ┌──────────────────┐    │
│  │  C# gRPC Client  │◄────────────►│  C++ gRPC Server  │    │
│  └──────────────────┘              └──────────────────┘    │
├─────────────────────────────────────────────────────────────┤
│                      Core Layer                              │
│                (C++ - High Performance)                      │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │  HAL (Hardware)  │  │ Imaging Engine   │                │
│  │  - Detector      │  │  - Processing    │                │
│  │  - HV Generator  │  │  - Plugins       │                │
│  │  - Collimator    │  │  - OpenCV, FFTW  │                │
│  └──────────────────┘  └──────────────────┘                │
├─────────────────────────────────────────────────────────────┤
│                  Infrastructure Layer                        │
│              (Build, CI/CD, Testing)                         │
│  CMake 3.25+ | vcpkg | .NET 8 | Gitea Actions               │
└─────────────────────────────────────────────────────────────┘
```

### Architecture Principles (아키텍처 원칙)

**1. 계층 분리 (Layer Separation)**
- 상위 계층은 하위 계층에만 의존
- 동일 계층 내 모듈 간 순환 의존 금지
- 명확한 인터페이스 경계

**2. 플러그인 아키텍처 (Plugin Architecture)**
- HAL: 하드웨어 드라이버 플러그인 DLL
- Imaging: 이미지 처리 엔진 플러그인 (IImageProcessingEngine 인터페이스)
- 런타임 교체 및 확장 가능

**3. 프로세스 격리 (Process Isolation)**
- C++ Core Engine과 C# GUI는 별도 프로세스
- IPC를 통한 안전한 통신
- 장애 격리 및 안정성 확보

**4. 모듈 독립성 (Module Independence)**
- 각 SPEC은 독립적으로 개발 및 테스트 가능
- 최소한의 의존성
- 명확한 공개 인터페이스

## DIRECTORY STRUCTURE (디렉토리 구조)

### Root Structure (루트 구조)

```
hnvue-console/
├── .claude/                 # Claude Code 설정
│   ├── agents/             # MoAI 서브에이전트 정의
│   ├── commands/           # MoAI 슬래시 명령
│   ├── rules/              # 프로젝트 규칙
│   ├── skills/             # MoAI 스킬
│   └── ...
├── .gitea/                 # Gitea CI/CD 설정
├── .moai/                  # MoAI-ADK 프로젝트 관리
│   ├── config/             # MoAI 설정 (YAML)
│   │   └── sections/       # 설정 섹션
│   │       ├── quality.yaml       # 개발 방법론 (hybrid)
│   │       ├── language.yaml      # 언어 설정 (ko)
│   │       ├── project.yaml       # 프로젝트 메타데이터
│   │       ├── workflow.yaml      # 워크플로우 설정
│   │       └── ...
│   ├── project/            # 프로젝트 문서화 (이 문서)
│   │   ├── product.md      # 제품 개요
│   │   ├── structure.md    # 아키텍처 문서
│   │   └── tech.md         # 기술 스택
│   ├── specs/              # SPEC 문서 (9개)
│   │   ├── SPEC-INFRA-001/ # 빌드 시스템, CI/CD
│   │   ├── SPEC-IPC-001/   # gRPC IPC
│   │   ├── SPEC-HAL-001/   # 하드웨어 추상화
│   │   ├── SPEC-IMAGING-001/ # 이미지 처리
│   │   ├── SPEC-DICOM-001/ # DICOM 서비스
│   │   ├── SPEC-DOSE-001/  # 방사선량 관리
│   │   ├── SPEC-WORKFLOW-001/ # 워크플로우 엔진
│   │   ├── SPEC-UI-001/    # WPF GUI
│   │   └── SPEC-TEST-001/  # 테스트
│   ├── backups/            # 백업
│   └── reports/            # 리포트
├── docs/                   # 프로젝트 문서
│   ├── xray-console-sw-research.md
│   └── 리서치보고서-챗지피티.md
├── libs/                   # C++ 라이브러리 (4개)
│   ├── hnvue-infra/        # INFRA (공유 라이브러리)
│   ├── hnvue-ipc/          # IPC (gRPC 서버)
│   ├── hnvue-hal/          # HAL (하드웨어 추상화)
│   └── hnvue-imaging/      # Imaging (이미지 처리)
├── proto/                  # Protobuf 정의
│   └── *.proto             # gRPC 메시지 정의
├── scripts/                # 빌드 및 배포 스크립트
├── src/                    # C# 소스 (5개 프로젝트)
│   ├── HnVue.Console/      # WPF GUI (UI)
│   ├── HnVue.Workflow/     # 워크플로우 엔진 (Application)
│   ├── HnVue.Dicom/        # DICOM 서비스 (Service)
│   ├── HnVue.Dose/         # 방사선량 서비스 (Service)
│   └── HnVue.Ipc.Client/   # gRPC 클라이언트 (IPC)
├── tests/                  # 테스트 (C++, C#, Integration)
│   ├── cpp/                # C++ 테스트
│   │   └── hnvue-infra.Tests/
│   ├── csharp/             # C# 단위 테스트
│   │   ├── HnVue.Workflow.Tests/
│   │   ├── HnVue.Dicom.Tests/
│   │   ├── HnVue.Dose.Tests/
│   │   └── HnVue.Ipc.Client.Tests/
│   └── integration/        # 통합 테스트
│       └── HnVue.Integration.Tests/
├── CMakeLists.txt          # C++ 루트 CMake
├── CMakePresets.json       # CMake 프리셋
├── Directory.Build.props   # C# 공통 빌드 설정
├── Directory.Packages.props # C# NuGet 패키지
├── HnVue.sln               # C# 솔루션
└── vcpkg.json              # C++ 의존성
```

### C++ Libraries Structure (C++ 라이브러리 구조)

```
libs/
├── hnvue-infra/            # 공유 인프라 (SPEC-INFRA-001)
│   ├── include/
│   │   └── hnvue/
│   │       └── infra/
│   │           ├── logging.hpp       # 로깅 (spdlog)
│   │           ├── config.hpp        # 설정 관리
│   │           └── thread_pool.hpp   # 스레드 풀
│   ├── src/
│   ├── tests/
│   └── CMakeLists.txt
│
├── hnvue-ipc/              # gRPC IPC 서버 (SPEC-IPC-001)
│   ├── include/
│   │   └── hnvue/
│   │       └── ipc/
│   │           ├── grpc_server.hpp    # gRPC 서버
│   │           └── service_impl.hpp   # 서비스 구현
│   ├── proto/               # 생성된 Protobuf 코드
│   ├── src/
│   ├── tests/
│   └── CMakeLists.txt
│
├── hnvue-hal/              # 하드웨어 추상화 (SPEC-HAL-001)
│   ├── include/
│   │   └── hnvue/
│   │       └── hal/
│   │           ├── i_detector.hpp     # 검출기 인터페이스
│   │           ├── i_hv_generator.hpp # 고전압 발생기 인터페이스
│   │           ├── i_collimator.hpp   # 콜리메이터 인터페이스
│   │           └── plugin_manager.hpp # 플러그인 관리자
│   ├── src/
│   ├── plugins/             # 하드웨어 드라이버 플러그인 DLL
│   │   ├── vendor_detector.dll
│   │   └── mock_hardware.dll
│   ├── tests/
│   └── CMakeLists.txt
│
└── hnvue-imaging/          # 이미지 처리 엔진 (SPEC-IMAGING-001)
    ├── include/
    │   └── hnvue/
    │       └── imaging/
    │           ├── i_image_processing_engine.hpp # 엔진 인터페이스
    │           ├── image_buffer.hpp              # 이미지 버퍼
    │           └── processing_pipeline.hpp       # 파이프라인
    ├── src/
    │   ├── engines/           # 이미지 처리 엔진 플러그인
    │   │   ├── opencv_engine.dll
    │   │   └── fftw_engine.dll
    │   └── algorithms/
    ├── tests/
    └── CMakeLists.txt
```

### C# Projects Structure (C# 프로젝트 구조)

```
src/
├── HnVue.Ipc.Client/       # IPC 클라이언트 (SPEC-IPC-001)
│   ├── Services/
│   │   ├── GrpcChannelService.cs      # gRPC 채널 관리
│   │   └── ProtobufMapper.cs          # Protobuf 매핑
│   ├── Generated/            # 생성된 gRPC 코드
│   └── HnVue.Ipc.Client.csproj
│
├── HnVue.Dicom/            # DICOM 서비스 (SPEC-DICOM-001)
│   ├── Services/
│   │   ├── IDicomService.cs           # DICOM 서비스 인터페이스
│   │   ├── DicomImportService.cs      # DICOM Import
│   │   ├── DicomExportService.cs      # DICOM Export
│   │   └── DicomMetadataService.cs    # 메타데이터 관리
│   ├── Models/
│   │   └── DicomDataset.cs            # DICOM 데이터셋
│   └── HnVue.Dicom.csproj
│
├── HnVue.Dose/             # 방사선량 서비스 (SPEC-DOSE-001)
│   ├── Services/
│   │   ├── IDoseService.cs            # 방사선량 서비스 인터페이스
│   │   ├── DoseTrackingService.cs     # 누적 선량 추적
│   │   └── DoseAlertService.cs        # 선량 경고 (ALARA)
│   ├── Models/
│   │   └── DoseRecord.cs              # 선량 기록
│   └── HnVue.Dose.csproj
│
├── HnVue.Workflow/         # 워크플로우 엔진 (SPEC-WORKFLOW-001)
│   ├── Core/
│   │   ├── WorkflowEngine.cs          # 워크플로우 엔진
│   │   ├── StateMachine.cs            # 상태 머신
│   │   └── InterlockChecker.cs        # 인터락 검사기 (Class C)
│   ├── Safety/
│   │   ├── Interlocks/                # 9개 인터락
│   │   │   ├── IInterlock.cs          # 인터락 인터페이스
│   │   │   ├── DetectorReadyInterlock.cs
│   │   │   ├── HvGeneratorReadyInterlock.cs
│   │   │   ├── CollimatorPositionInterlock.cs
│   │   │   ├── PatientPositionInterlock.cs
│   │   │   ├── DoseLimitInterlock.cs
│   │   │   ├── DoorClosedInterlock.cs
│   │   │   ├── EmergencyStopInterlock.cs
│   │   │   ├── SystemErrorInterlock.cs
│   │   │   └── PreviousExposureInterlock.cs
│   │   └── ExposureController.cs      # 노출 제어
│   └── HnVue.Workflow.csproj
│
└── HnVue.Console/          # WPF GUI (SPEC-UI-001)
    ├── Views/               # XAML 뷰
    │   ├── MainWindow.xaml
    │   ├── PatientInfoView.xaml
    │   ├── ExposureControlView.xaml
    │   └── ImageDisplayView.xaml
    ├── ViewModels/          # ViewModels
    │   ├── MainWindowViewModel.cs
    │   ├── PatientInfoViewModel.cs
    │   ├── ExposureControlViewModel.cs
    │   └── ImageDisplayViewModel.cs
    ├── Services/            # GUI 서비스
    │   └── NavigationService.cs
    └── HnVue.Console.csproj
```

## MODULE RELATIONSHIPS (모듈 관계)

### Dependency Graph (의존성 그래프)

**의존성 방향 (하위 → 상위)**
```
INFRA (공통)
    ↓
IPC ←── HAL
    ↓      ↓
IMAGING ←┘
    ↓
DICOM, DOSE (서비스)
    ↓
WORKFLOW (애플리케이션)
    ↓
UI (프레젠테이션)
```

**C++ 모듈 간 의존성**
- `hnvue-infra`: 모든 C++ 라이브러리의 기반
- `hnvue-ipc`: `hnvue-infra` 의존, gRPC 서버 제공
- `hnvue-hal`: `hnvue-infra`, `hnvue-ipc` 의존
- `hnvue-imaging`: `hnvue-infra`, `hnvue-ipc` 의존

**C# 모듈 간 의존성**
- `HnVue.Ipc.Client`: 모든 C# 프로젝트의 기반
- `HnVue.Dicom`, `HnVue.Dose`: `HnVue.Ipc.Client` 의존
- `HnVue.Workflow`: `HnVue.Ipc.Client`, `HnVue.Dicom`, `HnVue.Dose` 의존
- `HnVue.Console`: 모든 C# 프로젝트 의존

### Cross-Language Communication (교차 언어 통신)

**gRPC IPC 계층**
```
┌─────────────────────────────────────────────────────────┐
│ C# WPF GUI Process                                      │
│  ┌──────────────┐         ┌─────────────────────────┐   │
│  │ UI ViewModel │────────►│ HnVue.Ipc.Client       │   │
│  └──────────────┘         │  (gRPC Client)          │   │
│                           └─────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
                            │ gRPC (Protobuf)
                            ▼
┌─────────────────────────────────────────────────────────┐
│ C++ Core Engine Process                                 │
│  ┌─────────────────────────┐         ┌──────────────┐   │
│  │ HnVue.Ipc.Server        │────────►│ HAL, Imaging │   │
│  │  (gRPC Server)          │         │  Services    │   │
│  └─────────────────────────┘         └──────────────┘   │
└─────────────────────────────────────────────────────────┘
```

**Protobuf 메시지 흐름**
- 이미지 데이터: 바이너리 스트림 + 메타데이터
- 장치 상태: protobuf 메시지 (감시, 제어)
- 제어 명령: protobuf 요청/응답

## DATA FLOW (데이터 흐름)

### Typical Exposure Workflow (일반적인 촬영 워크플로우)

```
1. 사용자 입력 (UI)
   PatientInfoView → PatientInfoViewModel
   ↓
2. 워크플로우 엔진 (C#)
   WorkflowEngine.PrepareExposure()
   ↓
3. 인터락 검사 (Class C Safety)
   InterlockChecker.CheckAllInterlocks()
   - 9개 인터락 모두 통과해야 함
   ↓
4. IPC 통신 (gRPC)
   GrpcChannelService.SendExposureCommand()
   ↓
5. C++ 코어 엔진
   HnVue.Ipc.Server.ReceiveCommand()
   ↓
   HAL.ExecuteExposure()
   - Detector.Acquire()
   - HVGenerator.Enable()
   ↓
   Imaging.ProcessImage()
   - OpenCV/FFTW 처리
   ↓
6. 결과 반환 (gRPC + Protobuf)
   HnVue.Ipc.Server.SendImageResponse()
   ↓
7. DICOM 변환 (C#)
   DicomExportService.ExportToDicom()
   ↓
8. 방사선량 기록 (C#)
   DoseTrackingService.RecordDose()
   ↓
9. UI 업데이트 (WPF)
   ImageDisplayViewModel.UpdateImage()
   ImageDisplayView.Render()
```

### Image Processing Pipeline (이미지 처리 파이프라인)

```
Detector Raw Data (16-bit RAW)
    ↓
DMA Transfer (Ring Buffer)
    ↓
Preprocessing (C++)
  - Dark current correction
  - Gain correction
  - Bad pixel correction
    ↓
Image Processing Engine (Plugin)
  - OpenCV Engine / FFTW Engine
  - Noise reduction
  - Edge enhancement
  - Contrast adjustment
    ↓
Postprocessing (C++)
  - Log compression
  - Window/Level adjustment
    ↓
gRPC Transfer (Protobuf)
    ↓
DICOM Encapsulation (C#)
  - fo-dicom 5.x
  - Metadata insertion
  - File format conversion
    ↓
Display (WPF)
  - 16-bit grayscale rendering
  - Zoom/Pan/Window/Level
```

## INTEGRATION POINTS (통합 지점)

### External System Integrations (외부 시스템 통합)

**DICOM Network (의료 영상 네트워크)**
- DICOM SCU (Service Class User): C-STORE, C-FIND, C-MOVE
- DICOM SCP (Service Class Provider): Storage, Query/Retrieve
- PACS (Picture Archiving and Communication System) 연동
- Hospital Information System (HIS) 통합 (HL7 연동 예정)

**Hardware Devices (하드웨어 장치)**
- Detector (평판 검출기): FPGA SDK 래핑 (별도 프로젝트)
- HV Generator (고전압 발생기): 직렬 통신 또는 Ethernet
- Collimator (콜리메이터): 모터 제어
- Emergency Stop (비상 정지): GPIO 인터럽트

**Vendor SDK Integration**
- HVG (High Voltage Generator) SDK: 표준 인터페이스 (protobuf 정의)
- Detector SDK: 벤더별 래퍼 (플러그인 DLL)

### Internal APIs (내부 API)

**C++ Core Engine API (HAL)**
```cpp
namespace hnvue::hal {
    class IDetector {
        virtual bool Initialize() = 0;
        virtual bool Acquire(ImageBuffer& image) = 0;
        virtual void SetExposureParams(ExposureParams params) = 0;
    };

    class IHVGenerator {
        virtual bool Enable() = 0;
        virtual void Disable() = 0;
        virtual void SetKV(float kv) = 0;
        virtual void SetMAS(float mas) = 0;
    };
}
```

**C# Workflow API**
```csharp
namespace HnVue.Workflow.Core {
    public class WorkflowEngine {
        public async Task<ExposureResult> PrepareExposureAsync(ExposureRequest request);
        public void CancelExposure();
        public SystemState GetSystemState();
    }

    public interface IInterlock {
        string Name { get; }
        InterlockStatus Check();
        string Description { get; }
    }
}
```

## ARCHITECTURAL DECISIONS (아키텍처 결정)

### Decision 1: Hybrid Architecture (하이브리드 아키텍처)

**Context**
- 고성능 이미지 처리 필요 (C++)
- 직관적인 GUI 개발 필요 (C#)
- 의료용 소프트웨어 규제 준수 요구

**Decision**
- C++ Core Engine (이미지 처리, 하드웨어 제어)
- C# WPF GUI (사용자 인터페이스)
- gRPC IPC (프로세스 간 통신)

**Rationale**
- C++: 고성능, 하드웨어 근접성, 안전성 검증 용이
- C#: 빠른 UI 개발, .NET 생태계, WPF 데이터 바인딩
- IPC: 장애 격리, 독립적 배포, 규제 대응

**Trade-offs**
- 장점: 최적의 성능과 생산성, 장애 격리
- 단점: IPC 오버헤드, 복잡한 디버깅

### Decision 2: Plugin Architecture (플러그인 아키텍처)

**Context**
- 벤더 하드웨어 SDK 변경 가능성
- 이미지 처리 알고리즘 진화 필요성
- 모듈 간 결합도 최소화

**Decision**
- HAL: 하드웨어 드라이버 플러그인 (DLL)
- Imaging: 이미지 처리 엔진 플러그인 (IImageProcessingEngine)

**Rationale**
- 런타임 교체 가능성
- 독립적 개발 및 테스트
- 벤더 의존성 분리

**Trade-offs**
- 장점: 유연성, 확장성, 테스트 용이성
- 단점: 인터페이스 안정성 요구, 버전 관리 복잡

### Decision 3: SPEC-First Development (SPEC-First 개발)

**Context**
- 규제 대상 소프트웨어 (IEC 62304)
- 요구사항 추적 가능성 필수
- 복잡한 모듈 간 의존성

**Decision**
- 모든 기능은 SPEC 문서(EARS 형식)부터 시작
- 9개 독립적인 SPEC
- 명확한 인터페이스 정의

**Rationale**
- 요구사항-구현 추적 가능성
- 병렬 개발 가능성
- 규제 준비 문서화

**Trade-offs**
- 장점: 품질, 추적성, 병렬성
- 단점: 초기 문서화 비용

---

**문서 버전:** 1.0.0
**최종 업데이트:** 2026-02-18
**작성자:** abyz-lab
**언어:** Korean (ko)
