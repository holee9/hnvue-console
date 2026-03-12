# HnVue Console

**HnVue - Diagnostic Medical Device X-ray GUI Console Software**

의료용 X선 장비의 GUI 콘솔 소프트웨어입니다. IEC 62304 Class B/C 표준을 따르며 하이브리드 아키텍처(C++ Core Engine + C# WPF GUI)로 설계되었습니다.

---

## 1. 개요 (Overview)

### 제품 정의
- **목적**: 의료용 X선 장비의 진단 콘솔 소프트웨어
- **안전 등급**: IEC 62304 Class B/C
- **아키텍처**: 하이브리드 (C++ Core + C# WPF GUI)
- **통신**: gRPC IPC, DICOM 표준

### 현재 상태 (2026-03-12)
- **SPEC 완료**: 10/10 (100%) ✅
- **테스트 통과**: 1,048개 ✅
  - HnVue.Console.Tests: 219 pass
  - HnVue.Dose.Tests: 222 pass
  - HnVue.Workflow.Tests: 351 pass
  - HnVue.Dicom.Tests: 256 pass
- **빌드 상태**: 0 errors, acceptable warnings ✅

### SPEC 완료 목록
| SPEC | 설명 | 안전 등급 |
|------|------|----------|
| SPEC-INFRA-001 | Build/CI/CD 인프라 | A |
| SPEC-IPC-001 | gRPC IPC | C |
| SPEC-HAL-001 | Hardware Abstraction Layer | C |
| SPEC-IMAGING-001 | Image Processing Pipeline | A |
| SPEC-DICOM-001 | DICOM Services | B |
| SPEC-DOSE-001 | Radiation Dose Management | B |
| SPEC-WORKFLOW-001 | Clinical Workflow Engine | C |
| SPEC-UI-001 | WPF Console UI (MVVM) | B |
| SPEC-UI-002 | AsyncRelayCommand Improvements | B |
| SPEC-TEST-001 | Test Infrastructure | B |

---

## 2. 아키텍처 (Architecture)

### 시스템 계층 구조

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation Layer (WPF GUI)                                │
│  - MVVM ViewModels (16개)                                   │
│  - XAML Views                                                │
├─────────────────────────────────────────────────────────────┤
│  Application Layer (C# .NET 8)                              │
│  - Workflow Engine (10-state state machine)                 │
│  - Dose Management (DAP, RDSR)                              │
│  - DICOM Services (Worklist, MPPS, Storage)                │
├─────────────────────────────────────────────────────────────┤
│  Integration Layer (gRPC IPC)                               │
│  - Protocol Buffer Definitions                              │
│  - Inter-Process Communication                             │
├─────────────────────────────────────────────────────────────┤
│  Core Engine Layer (C++)                                    │
│  - Image Processing (OpenCV, FFTW)                          │
│  - HAL (Hardware Abstraction Layer)                        │
│  - Device Drivers (HVG, Detector)                          │
└─────────────────────────────────────────────────────────────┘
```

**자세한 아키텍처 문서**: [docs/architecture.md](docs/architecture.md)

### 핵심 컴포넌트
- **Workflow Engine**: 10상태 임상 워크플로우 상태 머신
- **Dose Management**: 방사선량 추적, DAP 계산, RDSR 생성
- **DICOM Services**: C-FIND, N-CREATE, C-STORE 지원
- **HAL**: HVG, Detector, Safety Interlocks 추상화
- **MVVM UI**: 16개 ViewModels, WPF 데이터 바인딩

---

## 3. 빠른 시작 (Quick Start)

### 사전 요구 사항
- **C++**: CMake 3.25+, MSVC (Windows) or GCC (Linux)
- **C#**: .NET 8 SDK
- **라이브러리**: vcpkg, OpenCV 4.x, FFTW 3.x
- **운영체제**: Windows 10/11 (WPF GUI), Linux (비즈니스 로직 개발)

### 빌드

```bash
# C++ Core Engine (이미지 처리, HAL)
cd libs/hnvue-imaging
cmake -B build -S .
cmake --build build

# C# Application (전체 솔루션)
dotnet build src/HnVue.Console/HnVue.Console.sln
```

### 테스트 실행

```bash
# 전체 테스트 (1,048개)
dotnet test

# 개별 테스트 스위트
dotnet test tests/csharp/HnVue.Workflow.Tests/
dotnet test tests/csharp/HnVue.Dose.Tests/
dotnet test tests/csharp/HnVue.Dicom.Tests/
dotnet test tests/csharp/HnVue.Console.Tests/

# ViewModel 테스트만 필터링
dotnet test --filter "FullyQualifiedName~ViewModels"
```

**테스트 리포트**: [docs/test-reports/](docs/test-reports/)

### 애플리케이션 실행

```bash
# WPF GUI (Windows 전용)
dotnet run --project src/HnVue.Console/HnVue.Console.csproj
```

---

## 4. 프로젝트 범위 및 저장소 분리

### 이 저장소 (hnvue-console)
의료용 X-ray GUI 콘솔 소프트웨어 핵심 구현

- **WPF MVVM 애플리케이션** (C#/.NET 8)
  - 16개 ViewModels, 10+ Views
  - gRPC 어댑터 13개
  - 의존성 주입 및 서비스 계층

- **Workflow 엔진** (C#/.NET 8)
  - 10상태 임상 워크플로우 상태 머신
  - 프로토콜 관리 및 검증
  - 안전 인터락 확인 (9개 인터락)

- **DICOM 서비스** (C#/.NET 8)
  - Worklist (C-FIND), MPPS (N-CREATE/N-SET), Storage (C-STORE)
  - 연결 풀링 및 재시시 큐

- **방사선량 관리** (C#/.NET 8)
  - DAP 계산, 누적 선량 추적
  - RDSR 생성, 감사 추적 (SHA-256)

- **gRPC IPC 인프라** (C# + C++)
  - 프로토콜 버퍼 정의
  - 클라이언트/서버 구현

- **HAL 인터페이스** (C# + C++)
  - HVG, Detector, Safety Interlocks 추상화
  - 테스트용 시뮬레이터 포함

### 별도 하드웨어 저장소
하드웨어 관련 구현은 별도 저장소에서 관리합니다

- **hnvue-simulators**: Python gRPC 기반 HAL 시뮬레이터 서버
  - 포괄적인 하드웨어 시뮬레이션
  - 통합 테스트 지원

- **fpga-imx8mp**: FPGA 기반 Detector 하드웨어
  - C++/Verilog 구현
  - FPGA 툴체인

- **fpga-work**: FPGA 워크플로우 및 장치 제어
  - 하드웨어 특화 프로토콜
  - 물리적 장치 인터페이스

---

## 5. 규제 준용 (Regulatory Compliance)

### IEC 62304 Safety Classification
| SPEC | 설명 | Safety Class |
|------|------|--------------|
| SPEC-WORKFLOW-001 | X-ray exposure control | **Class C** |
| SPEC-HAL-001 | Hardware abstraction | **Class C** |
| SPEC-IPC-001 | IPC for exposure control | **Class C** |
| SPEC-DOSE-001 | Dose monitoring/display | **Class B** |
| SPEC-DICOM-001 | DICOM communication | **Class B** |
| SPEC-UI-001 | User interface | **Class B** |

### 적용 표준
- **IEC 62304**: Medical device software lifecycle
- **IEC 60601-1**: Medical electrical equipment safety
- **IEC 60601-2-54**: Dose display requirements
- **DICOM PS 3.x**: Imaging interoperability
- **IHE REM Profile**: RDSR generation
- **FDA 21 CFR Part 11**: Audit trail with tamper evidence

### 요구사항 추적성
전체 요구사항 추적성 매트릭스: [docs/rtm.md](docs/rtm.md)

- **RTM (Main)**: 전체 요구사항 추적성 매트릭스
- **MRD**: 시장 요구사항 문서
- **PRD**: 제품 요구사항 문서

---

## 6. 개발 방법론 (Development Methodology)

### Simulator-First Development
모든 HAL 컴포넌트는 시뮬레이터를 통해 먼저 개발됩니다

- **이점**: 물리적 하드웨어 없이 전체 기능 개발 가능
- **테스트**: 통합 테스트는 HAL 시뮬레이터로 실행
- **리눅스 개발**: 비즈니스 로직은 크로스 플랫폼 지원

### CI/CD with Simulators
- **Gitea Actions**: 자화된 빌드 및 테스트
- **Python 시뮬레이터**: gRPC 기반 HAL 시뮬레이션
- **테스트 커버리지**: 85%+ 목표

### 플랫폼 지원
**✅ Linux 호환 (비즈니스 로직 개발)**
- Workflow Engine (351 tests)
- Dose Management (222 tests)
- DICOM Services (256 tests)
- ViewModels (219 tests)

**❌ Windows 전용 (WPF GUI)**
- XAML Views 및 디자인 타임 렌더링
- 하드웨어 드라이버 통합

---

## 7. 문서 (Documentation)

### 테스트 리포트
- **통합 테스트**: [docs/test-reports/integration-tests.md](docs/test-reports/integration-tests.md) (20/20 passing)
- **단위 테스트**: [docs/test-reports/unit-tests.md](docs/test-reports/unit-tests.md) (1,048 tests)

### 아키텍처
- **시스템 아키텍처**: [docs/architecture.md](docs/architecture.md)
  - 계층 구조
  - 컴포넌트 책임
  - 데이터 흐름
  - 인터페이스 정의

### Phase별 진행 계획
- **Phase 1 (완료)**: 기반 구축 (2026-03-12)
  - SPEC 10개 완료 (100%)
  - 테스트 1,048개 통과
- **Phase 2 (진행 중)**: MFDS 인허가 준비
  - [개발 로드맵](docs/development-roadmap-phase2.md)
  - [보안 규정 준용 계획](docs/cybersecurity-compliance-plan.md)

### 규정 준용 문서
- **RTM**: [docs/rtm.md](docs/rtm.md) - 요구사항 추적성 매트릭스
- **MRD**: [docs/mrd.md](docs/mrd.md) - 시장 요구사항
- **PRD**: [docs/prd.md](docs/prd.md) - 제품 요구사항
- **어댑터 감사**: [docs/adapter-audit.md](docs/adapter-audit.md)

### SPEC 문서
- [SPEC-UI-001](.moai/specs/SPEC-UI-001/spec.md): GUI Console User Interface
- [SPEC-DOSE-001](.moai/specs/SPEC-DOSE-001/spec.md): Radiation Dose Management
- [SPEC-WORKFLOW-001](.moai/specs/SPEC-WORKFLOW-001/spec.md): Clinical Workflow Engine
- [SPEC-IPC-001](.moai/specs/SPEC-IPC-001/spec.md): Inter-Process Communication
- [SPEC-DICOM-001](.moai/specs/SPEC-DICOM-001/spec.md): DICOM Communication Services

### 참고 문서
- [연구 보고서](docs/xray-console-sw-research.md)
- [CHANGELOG](CHANGELOG.md) - 전체 변경 이력

---

## 8. 최신 업데이트 (Recent Updates)

### 2026-03-12: 프로젝트 계획 대비 구현 교차검증 완료 ✅
- **P0 Critical 이슈 수정**: DoseService 오프라인 경고, 서비스 상태 인디케이터
- **어댑터 감사**: 13개 어댑터 중 16% 구현률 확인
- **TRUST 5 점수**: 3.8/5.0 산출
- **테스트**: 1,048개 전체 통과

### 2026-03-11: gRPC Service Adapters 완료 ✅
- **9개 gRPC 어댑터**: Patient, Worklist, User, Dose, AEC, Protocol, AuditLog, QC, Image
- **Python 시뮬레이터 구조**: HAL 시뮬레이션용 gRPC 서버 프레임워크

### 2026-03-02: Windows Environment Finalization ✅
- **Build System**: MSBuild CS2001, CS0579 오류 수정
- **gRPC Adapters**: 14개 어댑터 구현
- **ViewModels**: 9개 ViewModels API 수정
- **테스트**: 96/96 tests passing

**전체 변경 이력**: [CHANGELOG.md](CHANGELOG.md)

---

## 9. 라이선스 및 연락처

### 라이선스
Copyright © 2025 abyz-lab. All rights reserved.

### 기여
이 프로젝트는 의료용 소프트웨어로 IEC 62304 표준을 따릅니다. 기여 방법은 별도 문서를 참고하십시오.

### 연락처
- **Repository**: https://github.com/holee9/hnvue-console
- **Issues**: GitHub Issues

---

**문서 최종 업데이트**: 2026-03-12
