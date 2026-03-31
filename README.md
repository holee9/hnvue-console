# HnVue Console

**의료용 X선 장비의 진단 GUI 콘솔 소프트웨어** (IEC 62304 Class B/C)

하이브리드 아키텍처(C++ Core Engine + C# WPF GUI)로 설계된 의료기기 소프트웨어입니다. gRPC IPC 통신과 DICOM 표준을 지원하며, 방사선량 관리 및 임상 워크플로우를 포함합니다.

---

## 1. 개요

| 항목 | 내용 |
|------|------|
| **목적** | 의료용 X선 장비의 진단 콘솔 소프트웨어 |
| **안전 등급** | IEC 62304 Class B/C |
| **기술 스택** | C# 12 / .NET 8 (WPF MVVM) + C++ (HAL, Imaging) |
| **통신** | gRPC IPC (13개 어댑터), DICOM (Worklist, MPPS, Storage) |
| **운영체제** | Windows 10/11 (64-bit) 전용 |

### 현재 상태

| 지표 | 현황 |
|------|------|
| SPEC 완료 | 13/13 (100%) |
| C# 테스트 | 1,451 pass |
| Python 테스트 | 206 pass |
| E2E UI 테스트 | 62/62 pass |
| gRPC 어댑터 | 13개 전체 구현 완료 |
| 빌드 상태 | 0 errors |

---

## 2. 아키텍처

```
+---------------------------------------------------------+
|  Presentation Layer (WPF GUI)                           |
|  - MVVM ViewModels (16개), XAML Views, 다국어 지원       |
+---------------------------------------------------------+
|  Application Layer (C# .NET 8)                          |
|  - Workflow Engine (10-state FSM)                       |
|  - Dose Management (DAP, RDSR)                          |
|  - DICOM Services (Worklist, MPPS, Storage)             |
+---------------------------------------------------------+
|  Integration Layer (gRPC IPC)                           |
|  - Protocol Buffer 정의 (15개 .proto)                    |
|  - 13개 Service Adapter                                  |
+---------------------------------------------------------+
|  Core Engine Layer (C++)                                |
|  - Image Processing (OpenCV, FFTW)                      |
|  - HAL (HVG, Detector, Safety Interlocks)               |
+---------------------------------------------------------+
```

**상세 아키텍처**: [docs/architecture.md](docs/architecture.md)

### 핵심 컴포넌트

- **Workflow Engine**: 10상태 임상 워크플로우 상태 머신 (안전 인터락 9개)
- **Dose Management**: DAP 계산, 누적 선량 추적, RDSR 생성, 감사 추적 (SHA-256)
- **DICOM Services**: C-FIND, N-CREATE/N-SET, C-STORE, Storage Commitment
- **HAL**: HVG, Detector, Collimator, AEC, Patient Table 추상화
- **Security**: 인증 Rate Limiting, WORM 저장소, 보안 감사 로그 (OWASP)
- **MVVM UI**: 16개 ViewModels, 9개 뷰, WPF 데이터 바인딩

---

## 3. 디렉토리 구조

```
hnvue-console/
│
├── src/                              # C# 소스 코드
│   ├── HnVue.Console/                #   WPF MVVM 애플리케이션
│   │   ├── ViewModels/               #     ViewModel 16개 (MVVM 패턴)
│   │   ├── Views/                    #     XAML 뷰 9개 + Panels/ 7개
│   │   ├── Services/                 #     서비스 인터페이스 + Adapters/ (gRPC 13개)
│   │   ├── Security/                 #     인증, 감사 로그, WORM 저장소, 입력 검증
│   │   ├── Converters/               #     XAML 값 변환기
│   │   ├── Dialogs/                  #     모달 다이얼로그 (환자 등록/편집)
│   │   ├── Commands/                 #     AsyncRelayCommand, RelayCommand
│   │   ├── Models/                   #     뷰 데이터 모델
│   │   ├── Rendering/               #     DICOM Window/Level 변환
│   │   └── Resources/               #     테마, 스타일, 다국어 리소스
│   │
│   ├── HnVue.Workflow/               #   임상 워크플로우 엔진
│   │   ├── StateMachine/             #     10-state 상태 머신 핵심
│   │   ├── States/                   #     개별 상태 구현 (Idle, Exposure 등)
│   │   ├── Safety/                   #     안전 인터락 (9개)
│   │   ├── Hal/                      #     HAL 연동 + 시뮬레이터
│   │   ├── Dose/                     #     워크플로우 내 선량 관리
│   │   ├── Recovery/                 #     크래시 복구 메커니즘
│   │   └── Emergency/               #     긴급 절차
│   │
│   ├── HnVue.Dose/                   #   방사선량 관리 (IEC 62304 Class B)
│   │   ├── Calculation/              #     DAP 계산, 교정 관리
│   │   ├── Recording/               #     감사 추적 (SHA-256 위변조 감지)
│   │   ├── RDSR/                     #     Radiation Dose Structured Report
│   │   └── Alerting/                #     선량 임계값 알림
│   │
│   ├── HnVue.Dicom/                  #   DICOM 통신 서비스 (IEC 62304 Class B)
│   │   ├── Worklist/                 #     C-FIND Worklist 조회
│   │   ├── Mpps/                     #     MPPS (N-CREATE/N-SET)
│   │   ├── Storage/                  #     C-STORE 전송
│   │   ├── StorageCommit/            #     Storage Commitment
│   │   ├── Rdsr/                     #     RDSR 빌더
│   │   ├── Iod/                      #     CR/DX Image Object Definition
│   │   ├── Tls/                      #     DICOM TLS 보안 전송
│   │   ├── Queue/                    #     전송 큐 + 재시도 로직
│   │   └── Facade/                   #     통합 DICOM 서비스 API
│   │
│   └── HnVue.Ipc.Client/            #   gRPC IPC 클라이언트 (Proto 스텁 생성)
│
├── libs/                             # C++ 네이티브 라이브러리
│   ├── hnvue-hal/                    #   HAL (HVG, Detector, Safety Interlocks)
│   ├── hnvue-imaging/                #   이미지 처리 엔진 (OpenCV, FFTW)
│   ├── hnvue-ipc/                    #   gRPC IPC 서버 (C++)
│   └── hnvue-infra/                  #   C++ 인프라 유틸리티
│
├── proto/                            # Protocol Buffer 정의 (15개 .proto)
│   ├── hnvue_image.proto             #   이미지 서비스 (GetImage, SubscribeImageStream)
│   ├── hnvue_dose.proto              #   선량 서비스 (GetDoseSummary, ResetStudyDose)
│   ├── hnvue_patient.proto           #   환자 관리
│   ├── hnvue_worklist.proto          #   워크리스트
│   └── ...                           #   command, config, health, aec, qc, audit 등
│
├── tests/                            # 전체 테스트
│   ├── csharp/                       #   C# 단위/통합 테스트 (1,451개)
│   │   ├── HnVue.Console.Tests/      #     GUI/ViewModel/Security 테스트 (622)
│   │   ├── HnVue.Workflow.Tests/     #     워크플로우 엔진 테스트 (351)
│   │   ├── HnVue.Dicom.Tests/        #     DICOM 프로토콜 테스트 (256)
│   │   ├── HnVue.Dose.Tests/         #     선량 관리 테스트 (222)
│   │   ├── HnVue.Dicom.IntegrationTests/  #  Docker Orthanc 통합 테스트
│   │   ├── HnVue.Dicom.PerformanceTests/  #  성능 벤치마크
│   │   └── HnVue.Workflow.IntegrationTests/ # 워크플로우 통합 테스트
│   │
│   ├── e2e/                          #   E2E UI 자동화 테스트 (62개, FlaUI/UIA3)
│   ├── integration/                  #   시스템 통합 테스트
│   │   └── HnVue.Integration.Tests/  #     ClinicalWorkflows, Concurrency, DataFlow, Security
│   ├── python/                       #   Python 테스트 (시뮬레이터)
│   ├── cpp/                          #   C++ GTest 테스트
│   ├── traceability/                 #   RTM 추적성 검증 (rtm.csv, rtm_report.html)
│   ├── coverage_gates/               #   커버리지 게이트 (85%+ 강제)
│   ├── scripts/                      #   테스트 자동화 스크립트
│   └── docker/                       #   Docker 테스트 환경 (Orthanc DICOM 서버)
│
├── docs/                             # 제품 문서
│   ├── architecture.md               #   시스템 아키텍처
│   ├── prd.md                        #   제품 요구사항 문서 (PRD)
│   ├── mrd.md                        #   시장 요구사항 문서 (MRD)
│   ├── rtm.md                        #   요구사항 추적성 매트릭스 (RTM)
│   ├── security/                     #   보안 분석 (위협 모델, 침투 테스트)
│   ├── templates/                    #   IEC 62304 규제 문서 템플릿
│   ├── test-reports/                 #   테스트 리포트
│   ├── archive/                      #   초기 기획/리뷰 문서 (참고용)
│   ├── ref/                          #   참고 바이너리 (gitignored, 별도 다운로드)
│   └── INDEX.md                      #   전체 문서 인덱스
│
├── scripts/                          # 빌드/테스트 자동화 스크립트
│   ├── build-all.ps1                 #   전체 빌드
│   ├── run-tests.ps1                 #   전체 테스트 실행
│   ├── e2e-verify.ps1                #   E2E 실증 동작검증
│   ├── generate-proto.sh             #   Proto 코드 생성
│   └── certs/                        #   개발용 TLS 인증서 생성
│
├── .sbom/                            # SBOM 생성/검증 도구
│   ├── generate-sbom.ps1             #   CycloneDX 1.5 SBOM 생성
│   └── validate-ntia.ps1             #   NTIA 최소 요소 검증
│
├── .github/workflows/                # GitHub Actions CI/CD
│   ├── ci.yml                        #   통합 CI 파이프라인 (빌드/테스트)
│   ├── sast.yml                      #   CodeQL 정적 분석
│   ├── dast.yml                      #   동적 보안 테스트
│   ├── sbom.yml                      #   SBOM 생성 + OSV 취약점 스캔
│   └── dependency-scan.yml           #   NuGet 의존성 취약점 스캔
│
├── .moai/                            # MoAI 오케스트레이션 (Claude Code)
│   ├── specs/                        #   SPEC 문서 13개 (요구사항/계획/인수 기준)
│   ├── project/                      #   프로젝트 메타 (product, structure, tech, codemaps)
│   ├── config/                       #   MoAI 설정
│   ├── docs/                         #   개발 가이드 (HAL 시뮬레이터, IPC 구현 등)
│   └── templates/                    #   보안 문서 템플릿
│
├── HnVue.sln                         # .NET 솔루션 파일
├── CMakeLists.txt                    # C++ 빌드 최상위 설정
├── Directory.Build.props             # MSBuild 공통 속성
├── Directory.Packages.props          # NuGet 중앙 패키지 관리
├── vcpkg.json                        # C++ 패키지 의존성 (OpenCV, FFTW)
├── CHANGELOG.md                      # 전체 변경 이력
└── CLAUDE.md                         # Claude Code 설정
```

---

## 4. 빠른 시작

### 사전 요구 사항

- **C++**: CMake 3.25+, MSVC 2022 (Windows)
- **C#**: .NET 8 SDK
- **라이브러리**: vcpkg, OpenCV 4.x, FFTW 3.x
- **운영체제**: Windows 10/11 (64-bit)
- **Docker**: Docker Desktop for Windows (통합 테스트용)

### 빌드

```powershell
# C++ Core Engine (이미지 처리, HAL)
cd libs/hnvue-imaging
cmake -B build -S .
cmake --build build

# C# Application (전체 솔루션)
dotnet build
```

### 테스트 실행

```powershell
# C# 전체 테스트 (1,451개)
dotnet test

# 개별 테스트 스위트
dotnet test tests/csharp/HnVue.Console.Tests/     # 622 tests
dotnet test tests/csharp/HnVue.Workflow.Tests/     # 351 tests
dotnet test tests/csharp/HnVue.Dose.Tests/         # 222 tests
dotnet test tests/csharp/HnVue.Dicom.Tests/        # 256 tests

# ViewModel 테스트만 필터링
dotnet test --filter "FullyQualifiedName~ViewModels"

# Python 테스트 (206개)
python -m pytest tests/ --ignore=tests/csharp --ignore=tests/e2e -q
```

### E2E 실증 동작검증

WPF 앱을 실제 실행하여 UI 자동 클릭으로 기능 구현을 검증합니다. gRPC 서버 없이도 동작합니다 (Mock 서비스 자동 주입). 인터랙티브 Windows 세션이 필요합니다.

```powershell
# 빌드 + 전체 62개 E2E 테스트
.\scripts\e2e-verify.ps1 -Build

# 빠른 재검증 (이미 빌드된 경우)
.\scripts\e2e-verify.ps1

# 특정 뷰만 검증
.\scripts\e2e-verify.ps1 -Filter "ImageReview"
```

### 애플리케이션 실행

```powershell
dotnet run --project src/HnVue.Console/HnVue.Console.csproj
```

---

## 5. 테스트 전략

### 테스트 피라미드

| 계층 | 기술 | 테스트 수 | 설명 |
|------|------|----------|------|
| **단위 테스트** | xUnit, Moq | 1,451 | C# 비즈니스 로직, ViewModel, 서비스 |
| **통합 테스트** | Docker Orthanc | 20+ | DICOM 서버 연동, gRPC 어댑터 |
| **E2E 테스트** | FlaUI/UIA3 | 62 | WPF UI 자동화 (9개 뷰 전체 커버) |
| **Python 테스트** | pytest | 206 | 시뮬레이터, RTM 추적성, 커버리지 게이트 |

### E2E 검증 커버리지 (62개)

| 뷰 | 테스트 수 | 핵심 검증 항목 |
|----|---------|--------------|
| Main Window | 6 | 앱 실행, 네비게이션 바, 상태 바 |
| Navigation | 6 | 뷰 전환, 순차 이동 |
| Patient | 5 | 검색, DataGrid, 응급 환자 |
| Worklist | 4 | 새로고침, DataGrid, 상태 |
| Image Review | 10 | 측정 도구, QC 패널, 수용/거부 |
| Acquisition | 7 | AEC, 프로토콜, 촬영 트리거 |
| System Status | 5 | 상태 인디케이터, 컴포넌트 |
| Configuration | 5 | 역할, 탭, 저장/새로고침 |
| Audit Log | 6 | 로그 조회, 필터, 내보내기 |
| Locale | 4 | 한국어/영어 전환 |

- **커버리지 목표**: 85%+ (coverage gate 자동 강제)
- **테스트 리포트**: [docs/test-reports/](docs/test-reports/)

---

## 6. 규제 준용

### IEC 62304 Safety Classification

| SPEC | 설명 | Safety Class |
|------|------|-------------|
| SPEC-WORKFLOW-001 | X-ray exposure control | **Class C** |
| SPEC-HAL-001 | Hardware abstraction | **Class C** |
| SPEC-IPC-001 | IPC for exposure control | **Class C** |
| SPEC-SECURITY-001 | 보안 인증 & 감사 로그 | **Class C** |
| SPEC-IPC-002 | gRPC Adapter (Dose) | **Class B/C** |
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

### SBOM (Software Bill of Materials)

CycloneDX 1.5 형식으로 NTIA 최소 요소를 준수하는 SBOM을 생성합니다.

```powershell
# SBOM 생성
.\.sbom\generate-sbom.ps1

# NTIA 준수 유효성 검사
.\.sbom\validate-ntia.ps1
```

CVE 스캔은 GitHub Actions(`sbom.yml`)에서 OSV Scanner로 자동 실행됩니다.

### 규제 문서

- [RTM](docs/rtm.md) - 요구사항 추적성 매트릭스
- [MRD](docs/mrd.md) - 시장 요구사항 문서
- [PRD](docs/prd.md) - 제품 요구사항 문서
- [어댑터 감사](docs/adapter-audit.md) - gRPC 어댑터 구현 감사

---

## 7. 개발 방법론

### Simulator-First Development

모든 HAL 컴포넌트는 시뮬레이터를 통해 먼저 개발됩니다. 물리적 하드웨어 없이 전체 기능 개발이 가능하며, 모든 기능이 Windows 환경에서 통합 개발/테스트됩니다.

### CI/CD

| 파이프라인 | 도구 | 목적 |
|-----------|------|------|
| ci.yml | GitHub Actions | 빌드 + 전체 테스트 |
| sast.yml | CodeQL | 정적 코드 분석 |
| dast.yml | GitHub Actions | 동적 보안 테스트 |
| sbom.yml | CycloneDX + OSV | SBOM 생성 + 취약점 스캔 |
| dependency-scan.yml | Dependabot | NuGet 패키지 취약점 감지 |

---

## 8. 프로젝트 범위

### 이 저장소 (hnvue-console)

의료용 X-ray GUI 콘솔 소프트웨어 핵심 구현:

- WPF MVVM 애플리케이션 (C#/.NET 8)
- Workflow 엔진, DICOM 서비스, 방사선량 관리
- gRPC IPC 인프라 (C# 클라이언트 + C++ 서버)
- HAL 인터페이스 및 시뮬레이터

### 관련 저장소

| 저장소 | 설명 |
|--------|------|
| hnvue-simulators | Python gRPC 기반 HAL 시뮬레이터 서버 |
| fpga-imx8mp | FPGA 기반 Detector 하드웨어 (C++/Verilog) |
| fpga-work | FPGA 워크플로우 및 장치 제어 |

---

## 9. 문서

전체 문서 인덱스: [docs/INDEX.md](docs/INDEX.md)

### SPEC 문서

| SPEC | 설명 | 상태 |
|------|------|------|
| [SPEC-INFRA-001](.moai/specs/SPEC-INFRA-001/spec.md) | Build/CI/CD 인프라 | 완료 |
| [SPEC-IPC-001](.moai/specs/SPEC-IPC-001/spec.md) | gRPC IPC 아키텍처 | 완료 |
| [SPEC-IPC-002](.moai/specs/SPEC-IPC-002/spec.md) | gRPC Adapter 구현 | 완료 |
| [SPEC-HAL-001](.moai/specs/SPEC-HAL-001/spec.md) | Hardware Abstraction Layer | 완료 |
| [SPEC-IMAGING-001](.moai/specs/SPEC-IMAGING-001/spec.md) | Image Processing Pipeline | 완료 |
| [SPEC-DICOM-001](.moai/specs/SPEC-DICOM-001/spec.md) | DICOM Services | 완료 |
| [SPEC-DOSE-001](.moai/specs/SPEC-DOSE-001/spec.md) | Radiation Dose Management | 완료 |
| [SPEC-WORKFLOW-001](.moai/specs/SPEC-WORKFLOW-001/spec.md) | Clinical Workflow Engine | 완료 |
| [SPEC-UI-001](.moai/specs/SPEC-UI-001/spec.md) | WPF Console UI | 완료 |
| [SPEC-UI-002](.moai/specs/SPEC-UI-002/spec.md) | AsyncRelayCommand 개선 | 완료 |
| [SPEC-SECURITY-001](.moai/specs/SPEC-SECURITY-001/spec.md) | 보안 인증 & WORM 저장소 | 완료 |
| [SPEC-INTEGRATION-001](.moai/specs/SPEC-INTEGRATION-001/spec.md) | 통합 테스트 | 완료 |
| [SPEC-TEST-001](.moai/specs/SPEC-TEST-001/spec.md) | Test Infrastructure + E2E | 완료 |

### 변경 이력

전체 변경 이력: [CHANGELOG.md](CHANGELOG.md)

---

## 10. 라이선스 및 연락처

### 라이선스

Copyright 2025 abyz-lab. All rights reserved.

### 기여

이 프로젝트는 의료용 소프트웨어로 IEC 62304 표준을 따릅니다. 기여 방법은 별도 문서를 참고하십시오.

### 연락처

- **Repository**: https://github.com/holee9/hnvue-console
- **Issues**: GitHub Issues
