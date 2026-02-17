# 진단 의료용 X-ray Console SW 개발 리서치

> **작성일**: 2026-02-17  
> **목적**: FPGA 기반 X-ray 검출기와 연동하는 GUI Console SW 개발을 위한 기술 리서치  
> **대상 독자**: FPGA/HW 개발자 관점의 SW 아키텍처 설계

---

## 1. X-ray Console SW 개요 및 핵심 기능

### 1.1 Console SW의 역할

진단 의료용 X-ray Console SW는 X-ray 시스템의 **통합 제어 및 영상 획득/처리/관리**를 담당하는 핵심 소프트웨어다. 상용 제품 사례(dicomPACS DX-R, ExamVueDR, Carestream Eclipse 등)를 분석하면, 공통적으로 다음 기능 블록을 포함한다:

| 기능 블록 | 설명 |
|-----------|------|
| **Image Acquisition** | Detector로부터 Raw 데이터 수신, 실시간 프리뷰, 촬영 트리거 제어 |
| **Image Processing** | Gain/Offset 보정, Noise reduction, Window/Level 조정, Virtual Grid (GLI) |
| **Generator Control** | X-ray Generator의 kV, mA, 노출시간 제어 및 상태 모니터링 |
| **Patient Management** | 환자 정보 입력/관리, Worklist 연동 (DICOM MWL) |
| **DICOM Communication** | PACS 저장(C-STORE), 조회(C-FIND), 이동(C-MOVE), Worklist(C-FIND) |
| **Viewer/Diagnostic** | 영상 뷰어, 측정 도구(거리, 각도, Cobb angle), Annotation |
| **Dose Management** | 방사선량 기록, DAP(Dose Area Product) 모니터링, RDSR 생성 |
| **System Configuration** | Calibration, AEC 설정, 촬영 프로토콜 관리 |

### 1.2 시스템 아키텍처 (전형적 구성)

```
┌─────────────────────────────────────────────────────────┐
│                    Console SW (Host PC)                  │
│  ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐  │
│  │ GUI Layer │ │ Image    │ │ DICOM    │ │ Device    │  │
│  │ (Qt/WPF)  │ │ Pipeline │ │ Service  │ │ Interface │  │
│  └─────┬─────┘ └────┬─────┘ └────┬─────┘ └─────┬─────┘  │
│        └──────┬──────┘            │             │        │
│          App Logic Layer          │             │        │
└───────────────┬───────────────────┬─────────────┬────────┘
                │                   │             │
           ┌────▼────┐       ┌─────▼─────┐  ┌───▼────┐
           │ PACS/   │       │ HIS/RIS   │  │ HW     │
           │ Archive │       │ Worklist  │  │ Layer  │
           └─────────┘       └───────────┘  └───┬────┘
                                                │
                          ┌─────────────────────┼──────────┐
                          │    USB 3.x / PCIe / GigE       │
                          ├────────────┬───────────────────┤
                          │   FPGA     │     MCU           │
                          │ (Detector  │  (Generator       │
                          │  Readout)  │   Interface)      │
                          └────────────┴───────────────────┘
```

---

## 2. GUI 프레임워크 선택

### 2.1 주요 후보 비교

| 항목 | **Qt (C++)** | **WPF (C#/.NET)** | **Avalonia (C#/.NET)** |
|------|-------------|-------------------|----------------------|
| **언어** | C++ (+ QML for UI) | C# / XAML | C# / AXAML |
| **크로스 플랫폼** | Windows/Linux/macOS/Embedded | Windows only | Windows/Linux/macOS |
| **의료기기 실적** | 매우 높음 (Dräger, Oncosoft, Neocis, Fresenius Kabi) | 높음 (다수 상용 PACS Viewer) | 아직 제한적 |
| **DICOM 라이브러리** | DCMTK (C++), GDCM (C++) 직접 통합 | fo-dicom (.NET), LEADTOOLS | fo-dicom (.NET) |
| **IEC 62304** | Qt Safe Renderer 인증 보유 | 별도 인증 필요 | 별도 인증 필요 |
| **이미지 처리** | VTK, ITK 네이티브 통합 | VTK.NET wrapper | VTK.NET wrapper |
| **라이선스** | 상용 라이선스 또는 LGPL/GPL | .NET 무료 | MIT (무료) |
| **FPGA 연동** | USB/PCIe 드라이버 직접 작성 용이 | P/Invoke 또는 C++/CLI wrapper | P/Invoke |
| **Testability** | Qt Test, Google Test | xUnit, NUnit, MSTest | xUnit, NUnit |

### 2.2 권장 아키텍처: 하이브리드 접근

FPGA 기반 detector 개발자 관점에서, 아래와 같은 하이브리드 구조를 권장한다:

```
┌────────────────────────────────────────────┐
│          GUI Layer (C# / WPF or Avalonia)   │
│  - 환자 관리 UI, Workflow, DICOM Viewer    │
│  - MVVM 패턴, Unit Test 용이              │
└──────────────────┬─────────────────────────┘
                   │ (Interop: gRPC / Named Pipe / Shared Memory)
┌──────────────────▼─────────────────────────┐
│        Core Engine (C++ / Qt optional)      │
│  - Image Acquisition & Processing Pipeline │
│  - Detector Driver (USB 3.x / CSI-2 MIPI) │
│  - Real-time 성능 critical 모듈           │
│  - DCMTK for DICOM                        │
└──────────────────┬─────────────────────────┘
                   │
┌──────────────────▼─────────────────────────┐
│        Hardware Abstraction Layer           │
│  - FPGA Register R/W (libusb / WinUSB)    │
│  - MCU Serial Protocol (Generator 제어)    │
│  - DMA Transfer Management                │
└────────────────────────────────────────────┘
```

**이유**:
- C++로 Image Pipeline과 HW 인터페이스를 구현하여 실시간 성능 확보
- C#으로 GUI를 구현하여 빠른 UI 개발과 높은 Testability 확보
- gRPC 또는 Named Pipe로 프로세스 간 통신 (프로세스 격리 = 안정성)
- FPGA 개발자로서 C++이 HW 인터페이스에 자연스러움

### 2.3 단일 스택으로 갈 경우

**Qt (C++) 단일 스택** — 가장 검증된 의료기기 SW 스택이다. Oncosoft, MITK(Medical Imaging Interaction Toolkit) 등이 Qt + VTK + ITK + DCMTK 조합으로 구축되어 있다. FPGA 드라이버부터 GUI까지 동일 언어로 작성할 수 있다.

**WPF (C#) 단일 스택** — fo-dicom + WPF 조합이 성숙해 있으며, MVVM 패턴으로 Unit Test가 용이하다. 단, HW 인터페이스 레이어에서 C++/CLI wrapper 또는 P/Invoke 가 필요하다.

---

## 3. 핵심 라이브러리 및 도구

### 3.1 DICOM 라이브러리

| 라이브러리 | 언어 | 라이선스 | 특징 |
|-----------|------|---------|------|
| **DCMTK** | C++ | BSD | 가장 성숙한 DICOM 구현, OFFIS 유지보수, 의료기기 표준 |
| **GDCM** | C++ | BSD | Python/C#/Java 바인딩, JPEG2000 지원 우수 |
| **fo-dicom** | C# (.NET) | MS-PL | .NET 네이티브, async/await 지원, 활발한 커뮤니티 |
| **PyDICOM** | Python | MIT | Python 분석/프로토타이핑, 프로덕션보다는 도구용 |
| **LEADTOOLS** | C++/C#/Java | 상용 | PACS Workstation Framework 포함, 가격 높음 |
| **DCF (DICOM Connectivity Framework)** | C++/C#/Java | 상용 | 일관된 API, NDT/DICONDE 지원 |

**권장**: 프로덕션에는 **DCMTK (C++)** 또는 **fo-dicom (C#)** 중 메인 언어에 맞춰 선택. 검증 스크립트에는 **PyDICOM** 활용.

### 3.2 이미지 처리

| 라이브러리 | 용도 |
|-----------|------|
| **VTK** (Visualization Toolkit) | 2D/3D 렌더링, MPR, Volume Rendering |
| **ITK** (Insight Toolkit) | 영상 분할, 필터링, Registration |
| **OpenCV** | 범용 이미지 처리, 실시간 처리에 강점 |
| **FFTW** | FFT 기반 이미지 필터링 |

### 3.3 HW 인터페이스 (FPGA ↔ Host)

| 인터페이스 | 라이브러리/드라이버 | 비고 |
|-----------|-------------------|------|
| **USB 3.x** | libusb, WinUSB, FX3 SDK (Cypress/Infineon) | Bulk Transfer, 5Gbps+ |
| **PCIe** | Custom driver, Xilinx XDMA | 최고 대역폭, 드라이버 개발 복잡 |
| **GigE Vision** | Aravis (Linux), GenICam SDK | 카메라 표준 프로토콜 |
| **Camera Link** | 전용 Frame Grabber SDK | Legacy 시스템 |

### 3.4 개발/테스트 도구

| 도구 | 용도 |
|------|------|
| **DVTK** (DICOM Validation Toolkit) | DICOM 적합성 검증 |
| **Conquest DICOM Server** | 오픈소스 PACS, 개발/테스트용 |
| **Orthanc** | 경량 DICOM 서버, REST API, 테스트 환경 구축 |
| **dcmdump / dcmsend** (DCMTK) | DICOM 파일 분석/전송 CLI |

---

## 4. 의료기기 인허가 및 규격

### 4.1 국제 규격 (필수)

| 규격 | 내용 | 중요도 |
|------|------|--------|
| **IEC 62304** | 의료기기 SW 수명주기 프로세스 | ★★★ 최우선 |
| **ISO 14971** | 의료기기 위험 관리 | ★★★ 필수 |
| **ISO 13485** | 의료기기 품질경영시스템 (QMS) | ★★★ 필수 |
| **IEC 62366** | 의료기기 사용적합성 (Usability Engineering) | ★★☆ |
| **IEC 60601-1** | 의료전기기기 기본 안전 | ★★★ HW 포함 시 |
| **IEC 60601-2-54** | X-ray 촬영 장비 특수 요구사항 | ★★★ |
| **IEC 60601-1-3** | 방사선 방호 | ★★☆ |
| **DICOM PS 3.x** | DICOM 표준 (통신, 데이터 구조) | ★★★ |
| **HL7 FHIR / v2** | 의료정보 교환 표준 | ★★☆ |

### 4.2 IEC 62304 소프트웨어 안전성 등급

X-ray Console SW는 일반적으로 **Class B** 또는 **Class C** 에 해당한다:

| 등급 | 정의 | 적용 |
|------|------|------|
| **Class A** | 부상 또는 건강 피해 불가능 | 단순 표시/로그 |
| **Class B** | 경상 가능 | 영상 표시, 측정 도구 |
| **Class C** | 사망 또는 중상 가능 | Generator 제어, AEC, 노출 제어 |

**SOUP (Software of Unknown Pedigree) 관리 필수**: Qt, DCMTK, fo-dicom 등 오픈소스 라이브러리 사용 시 IEC 62304에 따른 SOUP 리스크 분석이 요구된다. 버전 고정, 변경 추적, 결함 목록 관리가 필요하다.

### 4.3 한국 MFDS (식약처) 요구사항

2025년 「디지털의료제품법」 시행에 따라 SW 관련 요구사항이 강화되었다:

- **내장형 디지털의료기기소프트웨어**: HW에 설치/연결되어 제어·구동·데이터 처리를 수행하는 SW → X-ray Console SW가 이에 해당
- **제출 서류**: 디지털의료기기소프트웨어 적합성 확인보고서, SW 검증 및 유효성확인 자료
- **안전성 등급**: IEC 62304 기반 등급 판정 후 등급에 따른 문서화 수준 결정
- **사이버보안**: 의료기기 사이버보안 가이드라인 준수 필요

### 4.4 FDA 510(k) (미국 수출 시)

- 21 CFR Part 820 (Quality System Regulation) 또는 QMSR (ISO 13485 기반)
- 21 CFR 1020 (방사선 제품 성능 기준)
- FDA Guidance: "Content of Premarket Submissions for Device Software Functions" (2023)
- IEC 62304 준수를 통해 FDA 제출 문서화를 간소화할 수 있음
- DICOM 적합성 시험 결과 포함 필요

---

## 5. SW 아키텍처 설계 가이드

### 5.1 모듈 구조

```
xray-console/
├── docs/                         # 문서 (MD 형식)
│   ├── SRS/                      # Software Requirements Specification
│   ├── SAD/                      # Software Architecture Document
│   ├── SDS/                      # Software Detailed Design
│   └── test-plans/               # Test Plans (V&V)
├── src/
│   ├── core/                     # C++ Core Engine
│   │   ├── acquisition/          # Image Acquisition Module
│   │   ├── processing/           # Image Processing Pipeline
│   │   ├── dicom/               # DICOM Service (DCMTK wrapper)
│   │   ├── hw_interface/        # FPGA/MCU Driver Layer
│   │   └── protocol/            # IPC Protocol (gRPC/protobuf)
│   ├── gui/                      # C# GUI Application
│   │   ├── ViewModels/          # MVVM ViewModels
│   │   ├── Views/               # XAML Views
│   │   ├── Services/            # Business Logic Services
│   │   └── Models/              # Data Models
│   ├── firmware/                 # FPGA/MCU 관련 (참조)
│   │   ├── vhdl/                # FPGA RTL (VHDL)
│   │   └── mcu/                 # MCU Firmware (C)
│   └── tools/                    # 유틸리티/스크립트
│       ├── dicom_validator/     # DICOM 적합성 검증 (Python)
│       └── test_data_gen/       # 테스트 데이터 생성기
├── tests/
│   ├── unit/                    # Unit Tests
│   │   ├── core_tests/          # C++ (Google Test)
│   │   └── gui_tests/           # C# (xUnit)
│   ├── integration/             # Integration Tests
│   ├── system/                  # System-level Tests
│   └── testbench/               # HW Simulation Testbench
│       ├── tb_acquisition/      # Acquisition 시뮬레이션
│       └── tb_protocol/         # 통신 프로토콜 검증
├── scripts/
│   ├── ci/                      # CI/CD Pipeline
│   └── deploy/                  # Deployment Scripts
├── .gitea/                      # Gitea 설정
│   └── workflows/               # CI Workflow
├── CMakeLists.txt               # C++ Build System
├── Directory.Build.props        # C# Build Properties
└── README.md
```

### 5.2 Image Acquisition Pipeline

```
Detector (FPGA)
    │
    │  USB 3.x Bulk Transfer (Raw 16-bit pixels)
    ▼
[DMA Ring Buffer] ──── HAL Layer (C++)
    │
    ▼
[Offset Correction] ──── Dark Frame 감산
    │
    ▼
[Gain Correction] ──── Gain Map 적용
    │
    ▼
[Defect Pixel Map] ──── Dead/Hot Pixel 보간
    │
    ▼
[Scatter Correction] ──── Virtual Grid (선택적)
    │
    ▼
[Window/Level] ──── 표시용 LUT 적용
    │
    ▼
[GUI Display] ──── Preview & Review
    │
    ▼
[DICOM Export] ──── Patient Info + Image → PACS
```

### 5.3 검증 전략 (V&V)

IEC 62304에 따라, 안전성 등급에 맞는 검증 수준이 필요하다:

| 검증 레벨 | 도구 | 대상 |
|-----------|------|------|
| **Unit Test** | Google Test (C++), xUnit (C#), pytest (Python) | 개별 모듈/함수 |
| **Module Test** | 통합 테스트 프레임워크 | 모듈 간 인터페이스 |
| **Integration Test** | HW Simulator + SW | FPGA↔SW 데이터 경로 |
| **System Test** | 실제 HW 또는 시뮬레이터 | End-to-end Workflow |
| **DICOM Conformance** | DVTK, Orthanc | DICOM 프로토콜 적합성 |
| **Usability Test** | IEC 62366 기반 | 사용적합성 평가 |

**Testbench 구조 (FPGA-SW 통합)**:

```
┌───────────────────────────────────────┐
│         Test Harness (Python)         │
│  - 테스트 시나리오 정의               │
│  - Expected vs Actual 비교            │
│  - 결과 리포팅 (JUnit XML)           │
├───────────────┬───────────────────────┤
│  SW Under     │  HW Simulator        │
│  Test (C++)   │  (SystemVerilog TB    │
│               │   or Python Mock)     │
├───────────────┼───────────────────────┤
│         Shared Memory / FIFO          │
│  (Test Data Injection & Capture)      │
└───────────────────────────────────────┘
```

---

## 6. DICOM 통합 요점

### 6.1 필수 DICOM Service Classes

| SOP Class | 용도 |
|-----------|------|
| **Storage SCU** (C-STORE) | 촬영 영상을 PACS로 전송 |
| **Modality Worklist SCU** (C-FIND) | HIS/RIS에서 환자/검사 정보 조회 |
| **MPPS SCU** (N-CREATE/N-SET) | Modality Performed Procedure Step 보고 |
| **Storage Commitment SCU** (N-ACTION) | PACS 저장 확인 |
| **Print SCU** (N-CREATE) | DICOM 필름 프린트 (선택) |
| **Query/Retrieve SCU** (C-FIND/C-MOVE) | 이전 영상 조회 (선택) |

### 6.2 X-ray 관련 IOD (Information Object Definition)

- **Digital X-Ray Image (DX)** — 일반 디지털 X-ray
- **Computed Radiography (CR)** — CR 시스템 호환
- **X-Ray Radiation Dose SR** — 방사선량 구조화 리포트
- **Grayscale Softcopy Presentation State (GSPS)** — Window/Level, 표시 상태 저장

### 6.3 DICOM Conformance Statement

인허가 제출 시 **DICOM Conformance Statement** 작성이 필수이며, 아래 항목을 포함해야 한다:
- 지원하는 SOP Classes 목록
- Transfer Syntax 지원 현황 (Implicit/Explicit VR, JPEG 2000 등)
- 네트워크 및 미디어 프로파일
- 보안 프로파일 (TLS 지원 여부)

---

## 7. 개발 환경 및 CI/CD

### 7.1 권장 개발 환경

```
[개발 PC]
├── IDE: Visual Studio 2022 (C# GUI) + Qt Creator / VS Code (C++ Core)
├── Build: CMake (C++), MSBuild (C#)
├── VCS: Gitea (Self-hosted, Docker)
├── CI: Gitea Actions 또는 Jenkins
└── Package: vcpkg (C++ dependencies), NuGet (C# dependencies)

[테스트 환경]
├── Orthanc DICOM Server (Docker)
├── DVTK Validator
├── HW Simulator (Python Mock / FPGA Dev Board)
└── Test Automation: pytest + Google Test + xUnit
```

### 7.2 Gitea 기반 워크플로우

```yaml
# .gitea/workflows/ci.yml (예시)
name: Console SW CI
on: [push, pull_request]

jobs:
  build-cpp:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build Core Engine
        run: |
          mkdir build && cd build
          cmake .. -DCMAKE_BUILD_TYPE=Release
          cmake --build . --parallel
      - name: Run Unit Tests
        run: cd build && ctest --output-on-failure

  build-csharp:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build GUI
        run: dotnet build src/gui/XRayConsole.sln
      - name: Run Tests
        run: dotnet test tests/gui_tests/ --logger "trx"

  dicom-conformance:
    runs-on: ubuntu-latest
    needs: [build-cpp]
    steps:
      - name: Start Orthanc
        run: docker run -d -p 4242:4242 -p 8042:8042 orthancteam/orthanc
      - name: Run DICOM Tests
        run: pytest tests/integration/test_dicom.py -v
```

---

## 8. 오픈소스 참고 프로젝트

| 프로젝트 | 설명 | 기술 스택 |
|---------|------|----------|
| **MITK** | Medical Imaging Interaction Toolkit | C++, Qt, VTK, ITK, DCMTK |
| **3D Slicer** | 의료영상 분석 플랫폼 | C++, Qt, VTK, ITK |
| **OsiriX Lite** | macOS DICOM Viewer | Objective-C, VTK |
| **Orthanc** | 경량 DICOM 서버 | C++, REST API |
| **Horos** | macOS DICOM Viewer (OsiriX fork) | Objective-C |
| **Cornerstone.js** | 웹 기반 DICOM Viewer | JavaScript |
| **gVirtualXRay** | X-ray 시뮬레이터 (교육/검증) | C++, Python, OpenGL |
| **QtDcm** | Qt 기반 DICOM PACS 위젯 | C++, Qt, DCMTK |

---

## 9. 리스크 및 고려사항

### 9.1 기술적 리스크

| 리스크 | 영향 | 완화 방안 |
|--------|------|----------|
| USB 3.x 대역폭 부족 | 대형 패널 실시간 전송 실패 | DMA 최적화, 압축 전송, PCIe 대안 검토 |
| SOUP 라이브러리 결함 | 인허가 지연, 안전 이슈 | 버전 고정, SOUP 리스크 분석, 대안 라이브러리 확보 |
| DICOM 호환성 | 타사 PACS 연동 실패 | DVTK 적합성 테스트, 다중 PACS 벤더 테스트 |
| GUI 응답성 | 대용량 이미지 로딩 시 UI 프리징 | 비동기 처리, 백그라운드 스레딩, 프로그레시브 로딩 |

### 9.2 인허가 리스크

| 리스크 | 영향 | 완화 방안 |
|--------|------|----------|
| IEC 62304 문서화 부족 | 인허가 반려 | 초기부터 문서 템플릿 구축, V&V 추적성 확보 |
| 사이버보안 미준수 | 추가 자료 요구 | 설계 초기부터 보안 요구사항 반영 (TLS, 인증, 감사 로그) |
| 임상 검증 데이터 부족 | 출시 지연 | 팬텀 이미지 테스트 + 임상 협력기관 확보 |

---

## 10. 다음 단계 (Action Items)

1. **GUI 프레임워크 확정**: Qt (C++) vs WPF (C#) vs 하이브리드 — PoC 진행 권장
2. **DICOM 라이브러리 선정**: DCMTK vs fo-dicom — 메인 언어에 맞춰 결정
3. **IEC 62304 프로세스 수립**: 안전성 등급 결정 → 문서 템플릿 작성 → Gitea 워크플로우 연동
4. **FPGA↔Host 프로토콜 정의**: USB 3.x 전송 프로토콜, 커맨드 체계 상세 설계
5. **테스트 인프라 구축**: Orthanc Docker + DVTK + HW Mock 환경 셋업
6. **Prototype 개발**: 최소 Image Acquisition → Display → DICOM Export 파이프라인

---

## 참고 자료

- IEC 62304:2006+AMD1:2015 Medical device software — Software life cycle processes
- ISO 14971:2019 Medical devices — Application of risk management to medical devices
- ISO 13485:2016 Medical devices — Quality management systems
- DICOM Standard (https://www.dicomstandard.org/)
- MFDS 디지털의료기기소프트웨어 허가·심사 가이드라인 (2025)
- FDA Guidance: Premarket Submissions for Device Software Functions (2023)
- Qt for Medical Devices (https://www.qt.io/development/qt-in-medical)
- DCMTK Documentation (https://dicom.offis.de/dcmtk)
- MITK Documentation (https://docs.mitk.org/)
