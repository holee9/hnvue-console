# HnVue Console - 진단 의료기기 X-ray GUI Console SW 구현 계획

## Context

**제품명**: HnVue Console
**유형**: 현대적 진단 의료용 X-ray GUI Console Software
**아키텍처 원칙**:
1. 기능 모듈 = **독립 라이브러리** (종속성 없음, 독립 빌드/테스트/배포)
2. 영상처리 엔진 = **플러그인 아키텍처** (내장 엔진 또는 외부 엔진 교체/병행)
3. Detector vendor SDK wrapping = **별도 프로젝트** (표준 인터페이스로 연동)
4. HVG (Generator) = **표준 인터페이스 + 프로토콜 구현** 포함

---

## 모듈형 라이브러리 아키텍처

```
HnVue Console Application (hnvue-console)
│
│  ← 각 모듈은 독립 라이브러리, 단방향 의존만 허용
│
├─ hnvue-hal          [독립 라이브러리] Hardware Abstraction
│   ├─ IDetector      인터페이스 (Detector vendor plugin 로딩)
│   ├─ IGenerator     인터페이스 + RS-232/Ethernet 구현
│   ├─ ICollimator    인터페이스
│   ├─ ITable         인터페이스
│   ├─ IAEC           인터페이스
│   └─ IDoseMonitor   인터페이스
│
├─ hnvue-imaging      [독립 라이브러리] Image Processing Engine Layer
│   ├─ IImagingEngine 플러그인 인터페이스
│   ├─ BuiltinEngine  내장 처리 엔진 (기본 구현)
│   └─ EngineProxy    외부 엔진 교체/병행 실행 관리자
│
├─ hnvue-dicom        [독립 라이브러리] DICOM Service
│   ├─ StorageSCU, WorklistSCU, MPPSSCU
│   ├─ StorageCommitSCU, QueryRetrieveSCU
│   └─ DoseStructuredReport, DicomBuilder
│
├─ hnvue-workflow     [독립 라이브러리] Clinical Workflow Engine
│   ├─ WorkflowStateMachine
│   ├─ ProtocolManager
│   └─ ExposureController (HAL + Imaging 조율)
│
├─ hnvue-dose         [독립 라이브러리] Dose Management
│   ├─ DoseCalculator (DAP, DRL)
│   ├─ DoseDatabase (SQLite)
│   └─ RDSRBuilder
│
├─ hnvue-ipc          [독립 라이브러리] IPC / API Layer
│   ├─ gRPC 서버 (Core → GUI 통신)
│   └─ protobuf 메시지 정의
│
└─ hnvue-gui          [C# WPF 애플리케이션] GUI Console
    ├─ hnvue-ipc gRPC 클라이언트
    └─ MVVM Views/ViewModels
```

### 의존성 규칙 (단방향)

```
hnvue-hal       → (외부 의존 없음, 순수 인터페이스)
hnvue-imaging   → hnvue-hal (IDetector만)
hnvue-dicom     → (외부 의존 없음, DCMTK만)
hnvue-workflow  → hnvue-hal, hnvue-imaging, hnvue-dicom
hnvue-dose      → hnvue-dicom (RDSR용)
hnvue-ipc       → hnvue-workflow, hnvue-dicom, hnvue-dose
hnvue-gui       → hnvue-ipc (gRPC 클라이언트만)
hnvue-console   → 모든 라이브러리 (조립만)
```

---

## 영상처리 엔진 플러그인 아키텍처

HnVue의 핵심 설계 결정: 영상처리 엔진은 **완전 교체 및 병행 실행** 가능

```cpp
// hnvue-imaging 공개 인터페이스
namespace hnvue::imaging {

// 모든 엔진이 구현해야 하는 순수 인터페이스
class IImagingEngine {
public:
    virtual ~IImagingEngine() = default;

    // 전처리 보정 체인
    virtual ImageResult applyOffsetCorrection(const RawFrame&, const OffsetMap&) = 0;
    virtual ImageResult applyGainCorrection(const RawFrame&, const GainMap&) = 0;
    virtual ImageResult applyDefectCorrection(const RawFrame&, const DefectMap&) = 0;
    virtual ImageResult applyScatterCorrection(const RawFrame&, const ScatterParams&) = 0;
    virtual ImageResult applyWindowLevel(const RawFrame&, float center, float width) = 0;

    // 엔진 메타정보
    virtual std::string engineId() const = 0;
    virtual std::string engineVersion() const = 0;
    virtual EngineCapabilities capabilities() const = 0;  // GPU/CPU/FPGA
};

// 엔진 관리자: 교체 및 병행 실행
class ImagingEngineProxy {
public:
    // 내장 엔진 또는 외부 엔진(.dll/.so) 등록
    void registerEngine(std::shared_ptr<IImagingEngine> engine, Priority p);

    // 단일 엔진 처리
    ImageResult process(const RawFrame&, const ProcessingParams&);

    // 병행 실행: 여러 엔진 동시 실행 후 결과 선택
    std::vector<ImageResult> processParallel(const RawFrame&, const ProcessingParams&);

    // 런타임 엔진 교체
    void switchEngine(const std::string& engineId);
};

} // namespace hnvue::imaging
```

**지원 시나리오**:
| 시나리오 | 동작 |
|---------|------|
| 내장 엔진만 사용 | `BuiltinEngine` 기본 등록, 단일 처리 |
| 외부 엔진으로 교체 | `registerEngine(externalEngine)` + `switchEngine()` |
| 두 엔진 병행 실행 | `processParallel()` - A/B 비교 또는 이중화 |
| GPU 가속 엔진 추가 | 외부 CUDA/OpenCL 엔진 플러그인 로드 |
| 외부 AI 처리 엔진 | REST/gRPC 기반 외부 엔진 어댑터 |

---

## SPEC 목록 (8개 독립 SPEC)

### SPEC-INFRA-001: 프로젝트 인프라 및 모노레포 구조

**목적**: 독립 라이브러리 기반 모노레포 구축

**핵심 작업**:
- CMake 모노레포 구조 (각 라이브러리 독립 `CMakeLists.txt`)
- vcpkg 의존성 관리 (DCMTK, gRPC, protobuf, OpenCV, VTK, SQLite3)
- C# .NET 8 WPF 프로젝트 구조 (NuGet: fo-dicom, CommunityToolkit.Mvvm)
- 라이브러리별 공개 헤더 설계 (`include/hnvue/*/`)
- Gitea Actions CI/CD (각 라이브러리 독립 빌드/테스트 잡)
- 플러그인 로딩 공통 유틸리티 (`.dll`/`.so` 동적 로딩)
- 코딩 컨벤션, IEC 62304 프로세스 템플릿

**디렉토리 구조**:
```
hnvue-console/                    (루트 모노레포)
├── CMakeLists.txt                (루트 - 하위 라이브러리 포함)
├── vcpkg.json
├── libs/
│   ├── hnvue-hal/
│   │   ├── CMakeLists.txt        (독립 빌드 가능)
│   │   ├── include/hnvue/hal/   (공개 헤더)
│   │   ├── src/
│   │   └── tests/
│   ├── hnvue-imaging/
│   ├── hnvue-dicom/
│   ├── hnvue-workflow/
│   ├── hnvue-dose/
│   └── hnvue-ipc/
├── apps/
│   └── hnvue-gui/               (C# WPF 애플리케이션)
│       ├── HnVueConsole.sln
│       └── src/
├── tests/
│   └── integration/
├── docs/
│   ├── SRS/, SAD/, SDS/
│   └── test-plans/
├── config/
│   └── protocols/               (촬영 프로토콜 JSON)
└── .gitea/workflows/
```

---

### SPEC-HAL-001: Hardware Abstraction Layer 라이브러리

**목적**: `hnvue-hal` - 모든 X-ray 디바이스 표준 인터페이스 + HVG 구현

**공개 인터페이스** (`include/hnvue/hal/`):
```cpp
IDetector.h      // Detector 플러그인 인터페이스 (별도 프로젝트 구현)
IGenerator.h     // HVG 인터페이스 (이 라이브러리에서 구현)
ICollimator.h    // Collimator 제어 인터페이스
IPatientTable.h  // 환자 테이블 제어 인터페이스
IAEC.h           // AEC 인터페이스
IDoseMonitor.h   // 선량 모니터 인터페이스
DeviceManager.h  // 전체 디바이스 생명주기
```

**구현체** (`src/`):
```
generator/
  GeneratorRS232Impl.cpp    # RS-232/RS-485 시리얼 HVG
  GeneratorEthernetImpl.cpp # TCP/IP 기반 HVG
  GeneratorSimulator.cpp    # 개발/테스트용 시뮬레이터
plugin/
  DetectorPluginLoader.cpp  # .dll/.so 동적 로딩
```

**IGenerator 핵심 설계**:
- 명령 큐잉 및 타이밍 제어
- Timeout/Retry 정책
- 비동기 상태 콜백 (kV/mA 실시간)
- 알람/인터락 이벤트
- kVp: 40~150kV / mA: 0.1~1000mA / 노출시간: 1ms~10s

---

### SPEC-IMAGING-001: 영상처리 엔진 라이브러리 (플러그인 아키텍처)

**목적**: `hnvue-imaging` - 교체/병행 가능한 영상처리 엔진 레이어

**공개 인터페이스**:
```
include/hnvue/imaging/
  IImagingEngine.h       # 플러그인 인터페이스
  ImagingEngineProxy.h   # 엔진 등록/교체/병행 관리자
  ProcessingParams.h     # 처리 파라미터 구조체
  ImageResult.h          # 처리 결과 구조체
  EngineCapabilities.h   # 엔진 능력 (CPU/GPU/etc)
```

**내장 엔진** (`src/builtin/`):
- `OffsetCorrectionPass.cpp` - Dark Frame 감산
- `GainCorrectionPass.cpp` - Gain Map 적용
- `DefectPixelPass.cpp` - Dead/Hot Pixel 보간 (Bilinear/Bicubic)
- `ScatterCorrectionPass.cpp` - Virtual Grid (GLI)
- `NoiseReductionPass.cpp` - 노이즈 필터
- `WindowLevelPass.cpp` - LUT 기반 표시 변환
- `ProcessingPipeline.cpp` - Pass 체인 조합

**엔진 관리자**:
- `ImagingEngineProxy.cpp` - 단일/병행 처리, 런타임 교체
- `ExternalEngineAdapter.cpp` - 외부 엔진(.dll/.so) 어댑터

**Calibration 지원**:
- Dark/Flat Field 수집 워크플로우
- 보정 데이터 저장/로드 (Binary + XML 메타)

---

### SPEC-DICOM-001: DICOM 서비스 라이브러리

**목적**: `hnvue-dicom` - DCMTK 기반 완전한 DICOM 서비스

**공개 인터페이스**:
```
include/hnvue/dicom/
  IDicomService.h          # 최상위 DICOM 서비스 인터페이스
  StorageSCU.h
  WorklistSCU.h
  MPPSSCU.h
  StorageCommitSCU.h
  QueryRetrieveSCU.h
  DicomImageBuilder.h      # DX/CR IOD 생성
  DoseStructuredReport.h   # RDSR 생성
  DicomConfig.h            # AE Title, IP, Port 설정
```

**지원 DICOM Service Classes**:
| SOP Class | 용도 |
|-----------|------|
| Storage SCU (C-STORE) | PACS 전송 |
| Modality Worklist SCU (C-FIND) | HIS/RIS 환자 조회 |
| MPPS SCU (N-CREATE/N-SET) | 검사 진행 보고 |
| Storage Commitment SCU | PACS 저장 확인 |
| Query/Retrieve SCU (C-FIND/C-MOVE) | 이전 영상 조회 |

**IOD 지원**: DX, CR, GSPS, X-Ray Radiation Dose SR
**보안**: TLS 1.2+ (DICOM over TLS)
**네트워크**: 멀티 PACS 지원, 연결 풀링

---

### SPEC-WORKFLOW-001: 임상 워크플로우 엔진 라이브러리

**목적**: `hnvue-workflow` - X-ray 촬영 전체 워크플로우 상태 머신

**임상 상태 머신**:
```
IDLE
  → WORKLIST_SYNC           # MWL C-FIND
    → PATIENT_SELECTED
      → PROTOCOL_SELECTED   # Bodypart/Projection/kVp/mAs
        → DEVICE_READY      # Detector + Generator 준비 확인
          → POSITIONING     # Collimator/Table 조정
            → PREVIEW       # 실시간 프리뷰 (형광투시)
              → ARMED       # Generator Arm
                → EXPOSURE  # X-ray 조사 (AEC 연동)
                  → ACQ     # 영상 수신 (IImagingEngine)
                    → PROCESSING
                      → QC_REVIEW     # Accept/Reject/Repeat
                        → STORE       # MPPS + C-STORE + RDSR
                          → IDLE
```

**프로토콜 관리**:
- Bodypart/Projection preset DB (JSON, 확장 가능)
- kVp/mAs/SID/FSS/AEC 기본값 및 허용 범위
- 체형 자동 보정 (BMI 기반)
- 프로토콜 CRUD (임상 관리자용)

**공개 인터페이스**:
```
include/hnvue/workflow/
  IWorkflowEngine.h      # 워크플로우 제어 인터페이스
  WorkflowState.h        # 상태 열거형 및 전이 이벤트
  IProtocolManager.h     # 프로토콜 CRUD
  ExposureParams.h       # 촬영 파라미터
```

---

### SPEC-DOSE-001: 선량 관리 라이브러리

**목적**: `hnvue-dose` - 방사선 선량 모니터링, 기록, 보고

**핵심 기능**:
- DAP (Dose Area Product) 실시간 계산 (`IDoseMonitor` 연동)
- 검사별/환자별 누적 선량
- DRL (Diagnostic Reference Level) 비교 및 경고
- RDSR (X-Ray Radiation Dose Structured Report) 생성 → `hnvue-dicom`
- SQLite 로컬 선량 DB
- 보고서 내보내기 (PDF/CSV)

**공개 인터페이스**:
```
include/hnvue/dose/
  IDoseManager.h         # 선량 관리 인터페이스
  DoseRecord.h           # 선량 기록 데이터 모델
  DRLDatabase.h          # DRL 기준값 DB
  DoseReportExporter.h   # PDF/CSV 내보내기
```

---

### SPEC-IPC-001: IPC / API 라이브러리

**목적**: `hnvue-ipc` - Core Engine ↔ GUI 통신 레이어

**설계**:
- gRPC 서버 (C++ Core 측, Named Pipe on Windows)
- protobuf 메시지 정의 (`hnvue.proto`)
- 스트리밍 지원 (실시간 이미지 프리뷰 스트림)
- 이벤트 발행 (디바이스 상태, 워크플로우 전이, 알람)

**공개 인터페이스**:
```
include/hnvue/ipc/
  HnVueServer.h          # gRPC 서버 (Core 측)
  HnVueServiceImpl.h     # 서비스 메서드 구현
proto/
  hnvue.proto            # 공유 메시지 정의
```

---

### SPEC-UI-001: HnVue Console GUI 애플리케이션

**목적**: `hnvue-gui` - C# WPF MVVM 전체 콘솔 UI

**화면 구성**:

| 화면 | 주요 기능 |
|------|---------|
| **WorklistView** | MWL 환자 목록, 검색/필터, 응급 등록, 수기 등록 |
| **PatientView** | 환자 정보, 검사 이력, 이전 영상 조회 (Q/R) |
| **ProtocolView** | Bodypart/Projection, kVp/mAs/SID, AEC 모드 |
| **AcquisitionView** | 실시간 프리뷰, Collimator/Table 패널, 촬영 트리거 |
| **QCView** | Accept/Reject/Repeat, 처리 옵션 선택 |
| **ViewerView** | Win/Level, Zoom/Pan, 측정도구(거리/각도/Cobb), Annotation, GSPS |
| **DoseView** | DAP 실시간, 누적 선량, DRL 비교, 히스토리 차트 |
| **SystemConfig** | 디바이스 설정, DICOM 네트워크, Calibration, 사용자 관리 |
| **StatusBar** | Detector/Generator/PACS 연결 상태, 알람, 로그 |

**기술**:
- MVVM (CommunityToolkit.Mvvm)
- gRPC Client (hnvue-ipc 클라이언트)
- fo-dicom (C# 뷰어용 DICOM 파일 처리)
- LiveCharts2 (선량 차트)
- 다국어 지원 (RESX 리소스)
- IEC 62366 Usability Engineering 준수

---

### SPEC-TEST-001: V&V 테스트 인프라 및 IEC 62304 문서화

**목적**: 의료기기 규제 준수 검증 프레임워크

**라이브러리별 독립 테스트**:
```
libs/hnvue-hal/tests/           # Google Test - HAL + 시뮬레이터
libs/hnvue-imaging/tests/       # Google Test - 처리 알고리즘 수치 검증
libs/hnvue-dicom/tests/         # Google Test + pytest (Orthanc)
libs/hnvue-workflow/tests/      # Google Test - 상태 머신
libs/hnvue-dose/tests/          # Google Test + SQLite 검증
apps/hnvue-gui/tests/           # xUnit - ViewModel 단위 테스트
tests/integration/              # pytest - E2E 워크플로우
tests/system/                   # 실 HW 또는 시뮬레이터
```

**IEC 62304 산출물**:
- 안전성 등급 결정서 (Class B/C)
- SRS / SAD / SDS
- V&V 추적성 매트릭스
- SOUP 목록 (DCMTK, OpenCV, gRPC 등 버전 고정)
- 변경 이력 관리 프로세스

---

## 기술 스택

| 계층 | 기술 | 버전 |
|------|------|------|
| Core 라이브러리 | C++17 | - |
| 이미지 처리 | OpenCV, VTK, ITK | 4.x / 9.x / 5.x |
| DICOM | DCMTK | 3.6.8 |
| IPC | gRPC + protobuf | 1.60+ |
| 선량 DB | SQLite3 | 3.x |
| GUI | C# / WPF (.NET 8) | .NET 8 LTS |
| GUI MVVM | CommunityToolkit.Mvvm | 8.x |
| GUI DICOM | fo-dicom | 5.x |
| 빌드 (C++) | CMake 3.25+ | - |
| 패키지 (C++) | vcpkg | - |
| 패키지 (C#) | NuGet | - |
| CI/CD | Gitea Actions | - |
| DICOM 테스트 | Orthanc + DVTK | Docker |

---

## SPEC 실행 순서

```
SPEC-INFRA-001  (선행 필수 - 모노레포 기반)
      │
      ├─ SPEC-HAL-001      ─────────────────────────────┐
      │                                                  │ 병렬
      ├─ SPEC-DICOM-001    (HAL 의존 없음, 독립 개발)   │
      │                                                  │
      └─ SPEC-IMAGING-001  (IImagingEngine 플러그인)    ┘
              │                     │
         SPEC-WORKFLOW-001    SPEC-DOSE-001    (HAL + Imaging + DICOM 완료 후)
              │                     │
         SPEC-IPC-001         (Workflow + Dose 완료 후)
              │
         SPEC-UI-001          (IPC 완료 후)
              │
         SPEC-TEST-001        (전 단계 병행)
```

---

## 의료기기 인허가

| 규격 | 적용 |
|------|------|
| **IEC 62304** | SW 수명주기 - 라이브러리별 안전성 등급 결정 |
| **ISO 14971** | 위험 관리 - SOUP 리스크 분석 포함 |
| **ISO 13485** | 품질경영시스템 |
| **IEC 62366-1** | 사용적합성 공학 (GUI) |
| **IEC 60601-2-54** | X-ray 성능/안전 (Generator 제어 포함) |
| **DICOM PS3.x** | DICOM Conformance Statement 작성 필수 |
| **IHE RAD TF** | IHE Radiology 워크플로우 |
| **MFDS** | 디지털의료기기소프트웨어 적합성 확인보고서 |

---

## 검증 방법

```bash
# 각 라이브러리 독립 빌드/테스트
cmake --build build/hnvue-hal && ctest -R hnvue-hal
cmake --build build/hnvue-dicom && ctest -R hnvue-dicom

# 전체 빌드
cmake --build build --config Release && ctest --output-on-failure

# GUI 테스트
dotnet test apps/hnvue-gui/tests/ --logger "trx"

# DICOM 적합성 (Orthanc Docker)
docker compose up -d && pytest tests/integration/test_dicom.py -v

# 이미지 엔진 교체 검증
pytest tests/integration/test_engine_switch.py -v

# E2E 워크플로우
pytest tests/system/test_e2e_workflow.py -v
```

---

## 다음 단계 (승인 후)

```
Step 1:  /moai plan "SPEC-INFRA-001 HnVue 모노레포 인프라 구축"
Step 2:  /moai run SPEC-INFRA-001

Step 3a: /moai plan "SPEC-HAL-001 X-ray 시스템 HAL 및 HVG 인터페이스"
Step 3b: /moai plan "SPEC-DICOM-001 DICOM 서비스 라이브러리"          ← 병렬
Step 3c: /moai plan "SPEC-IMAGING-001 영상처리 엔진 플러그인 아키텍처" ← 병렬

Step 4:  /moai run SPEC-HAL-001, SPEC-DICOM-001, SPEC-IMAGING-001  (병렬)

Step 5:  /moai plan + run SPEC-WORKFLOW-001, SPEC-DOSE-001
Step 6:  /moai plan + run SPEC-IPC-001
Step 7:  /moai plan + run SPEC-UI-001
Step 8+: SPEC-TEST-001 (전 단계 병행)
```
