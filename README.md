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

### 현재 상태 (2026-03-18)
- **SPEC 완료**: 13/13 (100%) ✅
- **gRPC 어댑터**: 13개 전체 실제 구현 완료 ✅
- **보안 구현**: WORM 저장소 Phase 1-3 + 인증/감사 보안 레이어 완료 ✅
- **CI/CD**: SAST, DAST, SBOM, Dependency Scan 파이프라인 구축 ✅
- **테스트 통과**: 1,451개 (C#), 206개 (Python), **62개 (E2E)** ✅
  - HnVue.Console.Tests: 622 pass
  - HnVue.Dose.Tests: 222 pass
  - HnVue.Workflow.Tests: 351 pass
  - HnVue.Dicom.Tests: 256 pass
  - Python (simulator/traceability/scripts/coverage_gates): 206 pass
  - **E2E UI 자동화 테스트: 62/62 pass (전체 뷰 커버)** ✅
- **빌드 상태**: 0 errors, acceptable warnings ✅

### SPEC 완료 목록
| SPEC | 설명 | 안전 등급 | 상태 |
|------|------|----------|------|
| SPEC-INFRA-001 | Build/CI/CD 인프라 | A | ✅ 완료 |
| SPEC-IPC-001 | gRPC IPC 기반 아키텍처 | C | ✅ 완료 |
| SPEC-IPC-002 | gRPC Adapter 구현 (Image+Dose+Audit) | B/C | ✅ 완료 |
| SPEC-HAL-001 | Hardware Abstraction Layer | C | ✅ 완료 |
| SPEC-IMAGING-001 | Image Processing Pipeline | A | ✅ 완료 |
| SPEC-DICOM-001 | DICOM Services | B | ✅ 완료 |
| SPEC-DOSE-001 | Radiation Dose Management | B | ✅ 완료 |
| SPEC-WORKFLOW-001 | Clinical Workflow Engine | C | ✅ 완료 |
| SPEC-UI-001 | WPF Console UI (MVVM) | B | ✅ 완료 |
| SPEC-UI-002 | AsyncRelayCommand Improvements | B | ✅ 완료 |
| SPEC-SECURITY-001 | 보안 인증 & WORM 저장소 | C | ✅ 완료 |
| SPEC-INTEGRATION-001 | 통합 테스트 (INT-001~006) | B | ✅ 완료 |
| SPEC-TEST-001 | Test Infrastructure + E2E 자동화 | B | ✅ 완료 |

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
- **Security Layer**: 인증 Rate Limiting, 보안 감사 로그, 입력 검증 (OWASP)
- **WORM Storage**: FileSystem(개발) / Azure Blob(운영) 이중화 불변 저장소

---

## 3. 빠른 시작 (Quick Start)

### 사전 요구 사항
- **C++**: CMake 3.25+, MSVC 2022 (Windows)
- **C#**: .NET 8 SDK
- **라이브러리**: vcpkg, OpenCV 4.x, FFTW 3.x
- **운영체제**: Windows 10/11 (64-bit)
- **Docker**: Docker Desktop for Windows (테스트용 Orthanc/Python 시뮬레이터)

### 빌드

```powershell
# C++ Core Engine (이미지 처리, HAL)
cd libs/hnvue-imaging
cmake -B build -S .
cmake --build build

# C# Application (전체 솔루션)
dotnet build src/HnVue.Console/HnVue.Console.sln
```

### 테스트 실행

```powershell
# C# 전체 단위/통합 테스트 (1,451개)
dotnet test

# 개별 테스트 스위트
dotnet test tests/csharp/HnVue.Workflow.Tests/    # 351 tests
dotnet test tests/csharp/HnVue.Dose.Tests/        # 222 tests
dotnet test tests/csharp/HnVue.Dicom.Tests/       # 256 tests
dotnet test tests/csharp/HnVue.Console.Tests/     # 622 tests (gRPC Adapter + Security + UI)

# ViewModel 테스트만 필터링
dotnet test --filter "FullyQualifiedName~ViewModels"

# Python 테스트 (206개)
$PYTHON -m pytest tests/ --ignore=tests/csharp --ignore=tests/e2e -q
```

### E2E 실증 동작검증 (Proof-of-Operation Verification)

WPF 앱을 **실제 실행**하여 UI 자동 클릭으로 기능 구현을 검증합니다.
gRPC 서버 없이도 동작 (Mock 서비스 자동 주입). 인터랙티브 Windows 세션 필요.

```powershell
# ★ 원스텝 검증 (빌드 + 전체 62개 E2E 테스트)
.\scripts\e2e-verify.ps1 -Build

# 빠른 재검증 (이미 빌드된 경우)
.\scripts\e2e-verify.ps1

# 특정 뷰만 검증
.\scripts\e2e-verify.ps1 -Filter "ImageReview"
.\scripts\e2e-verify.ps1 -Filter "Navigation"

# 직접 실행 (상세 출력)
dotnet test tests/e2e/HnVue.Console.E2E.Tests/ -c Debug
```

**검증 커버리지**: 9개 뷰 × 62 테스트케이스 (앱 시작 → 네비게이션 → 각 뷰 렌더링 → 핵심 UI 조작)

| 검증 항목 | 방법 |
|---------|------|
| XAML 바인딩 크래시 감지 | 실제 앱 실행 후 뷰 탐색 |
| 뷰 렌더링 완료 | 핵심 `AutomationId` 요소 존재 확인 |
| 버튼/컨트롤 동작 | `InvokePattern.Invoke()` 포커스 독립 클릭 |
| 실패 진단 | 자동 스크린샷 + UIA 트리 덤프 (`tests/e2e/screenshots/`) |

**테스트 리포트**: [docs/test-reports/](docs/test-reports/)

### 애플리케이션 실행

```powershell
# WPF GUI (Windows 전용)
dotnet run --project src/HnVue.Console/HnVue.Console.csproj
```

---

## 4. 프로젝트 범위 및 저장소 분리

### 이 저장소 (hnvue-console)
의료용 X-ray GUI 콘솔 소프트웨어 핵심 구현

- **WPF MVVM 애플리케이션** (C#/.NET 8)
  - 16개 ViewModels, 10+ Views
  - gRPC 어댑터 13개 (전체 실제 구현 완료)
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
| SPEC-IPC-002 | gRPC Adapter (Dose=Class C) | **Class B/C** |
| SPEC-SECURITY-001 | 보안 인증 & 감사 로그 | **Class C** |
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
- **Windows 개발**: 모든 기능이 Windows 환경에서 통합 개발 가능

### CI/CD with Simulators
- **Gitea Actions**: 자동화된 빌드 및 테스트
- **GitHub Actions**: SAST (CodeQL), DAST, SBOM 생성, Dependency 취약점 스캔
- **Python 시뮬레이터**: gRPC 기반 HAL 시뮬레이션
- **테스트 커버리지**: 85%+ 목표

### 플랫폼 지원
**✅ Windows 전용 개발 환경**
- HnVue.Console.Tests (622 tests - gRPC Adapter, Security, ViewModels)
- Workflow Engine (351 tests)
- Dose Management (222 tests)
- DICOM Services (256 tests)
- Python: Simulator/RTM/Scripts/Coverage Gates (206 tests)
- WPF GUI 및 하드웨어 드라이버 통합

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
- **Windows 개발 환경 설정**: [docs/windows-setup-guide.md](docs/windows-setup-guide.md)
  - 필수 도구 설치
  - 환경 변수 설정
  - 검증 절차

### Phase별 진행 계획
- **Phase 1 (완료)**: 기반 구축 (2026-03-12)
  - SPEC 10개 완료 (100%)
  - 테스트 1,048개 통과
- **Phase 2 (진행 중)**: MFDS 인허가 준비
  - [개발 로드맵](docs/development-roadmap-phase2.md)
  - [보안 규정 준용 계획](docs/cybersecurity-compliance-plan.md)

---

## 6.1 SBOM (Software Bill of Materials)

### 개요
SPEC-SECURITY-001 FR-SEC-08에 따라 NTIA 최소 요소를 준수하는 SBOM을 자동 생성합니다.

### SBOM 파일
- **위치**: `components.json` (프로젝트 루트)
- **형식**: CycloneDX 1.5
- **NTIA 최소 요소 포함**:
  1. 모든 컴포넌트 이름
  2. 모든 컴포넌트 버전
  3. 모든 공급자 (저자/조직)
  4. 의존성 관계 (상위/하위)
  5. SBOM 작성자 이름
  6. SBOM 생성 타임스탬프
  7. SBOM 버전 (스키마 버전)

### SBOM 생성 방법
```powershell
# 수동 생성
.\.sbom\generate-sbom.ps1

# NTIA 준수 유효성 검사
.\.sbom\validate-ntia.ps1

# 빌드 시 자동 생성 (Directory.Build.targets 참조)
dotnet build
```

### CVE 스캔
- **OSV Scanner**: 자동 취약점 스캔 (GitHub Actions)
- **Dependabot**: 주간 NuGet 패키지 업데이트 PR
- **보고서**: `.github/workflows/sbom.yml` 참조

### 규정 준용 문서
- **RTM**: [docs/rtm.md](docs/rtm.md) - 요구사항 추적성 매트릭스
- **MRD**: [docs/mrd.md](docs/mrd.md) - 시장 요구사항
- **PRD**: [docs/prd.md](docs/prd.md) - 제품 요구사항
- **어댑터 감사**: [docs/adapter-audit.md](docs/adapter-audit.md)

### SPEC 문서
- [SPEC-IPC-001](.moai/specs/SPEC-IPC-001/spec.md): Inter-Process Communication (gRPC 기반 아키텍처)
- [SPEC-IPC-002](.moai/specs/SPEC-IPC-002/spec.md): gRPC Adapter 구현 (Image+Dose+Audit, IEC 62304 Class B/C)
- [SPEC-UI-001](.moai/specs/SPEC-UI-001/spec.md): GUI Console User Interface
- [SPEC-DOSE-001](.moai/specs/SPEC-DOSE-001/spec.md): Radiation Dose Management
- [SPEC-WORKFLOW-001](.moai/specs/SPEC-WORKFLOW-001/spec.md): Clinical Workflow Engine
- [SPEC-DICOM-001](.moai/specs/SPEC-DICOM-001/spec.md): DICOM Communication Services
- [SPEC-SECURITY-001](.moai/specs/SPEC-SECURITY-001/spec.md): 보안 인증 & WORM 저장소

### 참고 문서
- [연구 보고서](docs/xray-console-sw-research.md)
- [CHANGELOG](CHANGELOG.md) - 전체 변경 이력

---

## 8. 최신 업데이트 (Recent Updates)

### 2026-03-18: E2E UI 자동화 테스트 62/62 전체 통과 ✅

WPF 애플리케이션의 실제 실행 환경에서 UI 자동화 클릭 테스트를 구현하고, 3개의 XAML 크래시 버그를 수정하여 모든 62개 E2E 테스트가 통과합니다.

#### E2E 테스트 커버리지 (62개)

| 뷰 | 테스트 수 | 검증 항목 |
|----|---------|---------|
| **Main Window** | 6 | 앱 실행, 네비게이션 바, 상태 바, 로케일 선택기, 기본 뷰 |
| **Navigation** | 6 | Patient/Worklist/Status/Config/AuditLog 뷰 전환, 다중 순차 이동 |
| **Patient View** | 5 | 검색 컨트롤, DataGrid, 검색 버튼, 텍스트 입력, 응급 환자 버튼, 상태 바 |
| **Worklist View** | 4 | 새로고침, DataGrid, 상태 바, 컬럼 헤더 |
| **Image Review** | 10 | 헤더, 측정 도구 패널, QC 패널, 거리/각도/Cobb/주석 버튼, 수용/거부/재처리 버튼 |
| **Acquisition** | 7 | 헤더, AEC 패널, 프로토콜 선택, 미리보기 시작/중지, 촬영 트리거, 이미지 없음 표시 |
| **System Status** | 5 | 헤더, 시스템 상태 인디케이터, 새로고침, 컴포넌트 수 |
| **Configuration** | 5 | 헤더, 사용자 역할 표시, 탭 컨트롤, 저장, 새로고침 |
| **Audit Log** | 6 | 헤더, 로그 DataGrid, 날짜 필터, 검색, 내보내기, 페이지네이션 |
| **Locale** | 4 | 로케일 ComboBox, 한국어/영어 옵션, 영어 전환, 기본값 한국어 |

#### E2E 테스트 기술 스택
- **FlaUI.UIA3**: Windows UI Automation 기반 WPF 자동화
- **InvokePattern**: 포커스 독립적 버튼 클릭 (MouseSimulator 대체)
- **E2E 모드**: `HNVUE_E2E_TEST=1` 환경변수 → gRPC 어댑터를 Mock으로 교체
- **로그**: `tests/e2e/e2e_logs/` 타임스탬프 기반 상세 실행 로그

#### WPF XAML 버그 수정 (3건)
1. **TwoWay 바인딩 크래시** (`ImageViewerPanel.xaml`): read-only `Orientation` 프로퍼티에 TwoWay 모드 적용 → `InvalidOperationException` → `Mode=OneWay` 추가
2. **XamlParseException** (`QCActionPanel.xaml`): `{Binding Converter=...}` Path 누락 → `{Binding Path=., Mode=OneWay}` 수정
3. **E2E 역할 폴백** (`ServiceCollectionExtensions.cs`): E2E 모드에서 `IUserService` 미목킹 → gRPC 실패 → Operator 역할 폴백 → 탭 숨김 → `MockUserService` 등록 추가

### 2026-03-18: SPEC-IPC-002 gRPC Adapter 실제 구현 완료 ✅
- **ImageServiceAdapter**: `GetImage` RPC 구현 (5s deadline), `SubscribeImageStream` 청크 조립 → `ImageData` 반환
  - REQ-IMG-005: gRPC 실패 시 null/예외 반환 (빈 ImageData 반환 금지)
- **DoseServiceAdapter (IEC 62304 Class C)**: `GetDoseSummary` + `ConfigService` 연동, 안전 fail-safe 적용
  - REQ-DOSE-007: `GetCurrentDoseDisplayAsync` 실패 시 예외 전파 (0 반환 절대 금지)
  - REQ-DOSE-008: AlertThreshold 기본값 warning=50mGy / error=100mGy (보수적 값)
  - `IAuditLogService` 주입: SetAlertThreshold 변경 전/후 값 감사 로그
- **GrpcAdapterBase**: deadline 정책 추가 (Command=5s, ImageStream=30s, AlertStream=무제한)
- **AECServiceAdapter**: `IAuditLogService` 주입, EnableAEC/DisableAEC 감사 로그 (fire-and-forget)
- **Proto 확장**: `hnvue_image.proto`에 `GetImage`, `hnvue_dose.proto`에 `ResetStudyDose` RPC 추가
- **테스트**: HnVue.Console.Tests 614 → **622** pass (+8: GrpcAdapterBaseDeadlineTests 4, AECServiceAdapterAuditTests 4)

### 2026-03-16: 보안 레이어 강화 및 CI/CD 파이프라인 구축 ✅
- **SecurityValidator**: 입력 값 검증, SQL Injection/XSS 방지 (OWASP Top 10 준수)
- **AuthenticationRateLimiter**: 로그인 실패 횟수 기반 Rate Limiting (Brute Force 방어)
- **SecurityAuditLogger**: 보안 이벤트 구조화 감사 로그 (RFC 5424 형식)
- **GitHub Actions CI 구축**: 4개 워크플로우 추가
  - `sast.yml`: CodeQL 정적 코드 분석
  - `dast.yml`: 동적 애플리케이션 보안 테스트
  - `sbom.yml`: SBOM 자동 생성 및 OSV 취약점 스캔
  - `dependency-scan.yml`: NuGet 패키지 취약점 자동 감지 (Dependabot)
- **보안 테스트**: SecurityValidator, AuthenticationRateLimiter, SecurityAuditLogger 단위 테스트 추가
- **서비스 어댑터 개선**: AuditLog, Dose, Image, Patient 등 9개 어댑터 gRPC 통신 강화

### 2026-03-14: WORM 저장소 구현 및 E2E 테스트 안정화 ✅
- **WORM 저장소 Phase 1-3**: 인터페이스 추상화, Windows Immutable Files, Azure Immutable Blob Storage 구현
- **IWormStorageProvider**: WORM 저장소 추상화 계층 정의
- **FileSystemWormStorageProvider**: Windows 파일 시스템 기반 WORM 시뮬레이션 (개발용)
- **AzureBlobWormStorageProvider**: Azure Blob Storage 기반 진짜 WORM 구현 (운영용, SEC 17a-4 준수)
- **E2E 테스트 안정화**: TestBase.cs 프로세스 초기화 로직 개선, WorklistView.xaml XAML 스타일 문제 수정 (23/25 통과)
- **Azure.Storage.Blobs 12.21.0**: WORM 저장소용 패키지 추가
- **appsettings.Production.json**: Azure WORM 구성 템플릿 추가

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

### 2026-03-18: E2E 인프라 강화 (딥싱크 - system-emul-sim 교차 개선) ✅
- `RequiresDesktopFact`: CI 환경에서 E2E 테스트 자동 스킵 (거짓 실패 방지)
- `TreeDumper`: 테스트 실패 시 UIA 자동화 트리 텍스트 덤프 (디버거 없이 진단)
- `WaitHelper`: 독립 유틸리티 클래스 + `HNVUE_E2E_TIMEOUT_MS` 환경변수 오버라이드
- `RecordTestPassed()`: 실패 테스트에만 자동 스크린샷 캡처
- `scripts/e2e-verify.ps1`: 원스텝 실증 동작검증 스크립트

**문서 최종 업데이트**: 2026-03-18 (E2E 인프라 강화 + 원스텝 검증 스크립트)
