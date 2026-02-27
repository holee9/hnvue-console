# HnVue 기술 스택 (Technology Stack)

## STACK OVERVIEW (스택 개요)

HnVue는 하이브리드 아키텍처(C++ + C#) 기반의 의료용 진단 장비 GUI 콘솔 소프트웨어로, 고성능 이미지 처리와 직관적인 사용자 인터페이스를 결합합니다. 모든 기술 선택은 안전성, 성능, 규제 준수를 최우선으로 고려하여 이루어졌습니다.

### Technology Matrix (기술 매트릭스)

| 계층 | 기술 | 버전 | 용도 | SPEC |
|------|------|------|------|------|
| **C++ 언어** | C++ | 17 | Core Engine, HAL, Imaging | HAL, IMAGING |
| **C# 언어** | C# | 12 (.NET 8 LTS) | GUI, Services, Workflow | UI, WORKFLOW |
| **C++ 빌드** | CMake | 3.25+ | C++ 빌드 시스템 | INFRA |
| **C++ 패키지** | vcpkg | Latest | C++ 의존성 관리 | INFRA |
| **C# 빌드** | MSBuild | 17.0+ | C# 프로젝트 빌드 | UI |
| **C# 패키지** | NuGet | 6.0+ | C# 의존성 관리 | UI |
| **IPC** | gRPC | 1.68.x | 프로세스 간 통신 | IPC |
| **직렬화** | Protocol Buffers | 25.0+ | 메시지 포맷 | IPC |
| **GUI** | WPF | .NET 8 | 사용자 인터페이스 | UI |
| **MVVM** | Prism | 9.0.537 | WPF MVVM 프레임워크 | UI |
| **DICOM** | fo-dicom | 5.1.1 | DICOM 처리 | DICOM |
| **로깅 (C++)** | spdlog | 1.13.0+ | 구조화된 로깅 | INFRA |
| **로깅 (C#)** | Microsoft.Extensions.Logging | 8.0.0 | 구조화된 로깅 | WORKFLOW |
| **포맷팅 (C++)** | fmt | 10.0.0+ | 문자열 포맷팅 | INFRA |
| **테스트 (C++)** | Google Test | 1.14.0+ | 단위 테스트 | TEST |
| **테스트 (C#)** | xUnit | 2.7.0 | 단위 테스트 | TEST |
| **모의 (C#)** | Moq | 4.20.70 | 목 오브젝트 | TEST |
| **이미지 처리** | OpenCV | Latest | 이미지 처리 엔진 | IMAGING |
| **FFT** | FFTW | Latest | 고속 푸리에 변환 | IMAGING |
| **CI/CD** | Gitea Actions | Latest | 자동화 빌드/테스트 | INFRA |
| **버전 관리** | Git | Latest | 소스 코드 관리 | INFRA |

---

## C++ TECHNOLOGY STACK (C++ 기술 스택)

### Language & Compiler (언어 및 컴파일러)

**C++17 Standard**
- **선택 이유**: 모던 C++ 기능 (std::optional, std::variant, structured bindings)
- **주요 기능 사용**:
  - `std::filesystem`: 파일 시스템 조작
  - `std::optional`: 선택적 반환 값
  - `std::chrono`: 시간 측정
  - Smart pointers: 메모리 관리 자동화

**Visual Studio 2022 (v143)**
- **컴파일러**: MSVC (Microsoft C/C++)
- **C++ Standard**: /std:c++17
- **Warning Level**: /W4 (모든 경고 표시)
- **Treat Warnings as Errors**: /WX (경고를 오류로 처리)

### Build System (빌드 시스템)

**CMake 3.25+**
- **선택 이유**: 크로스 플랫폼 빌드, vcpkg 통합, IDE 통합
- **CMakePresets.json 사용**:
  ```json
  {
    "configurePresets": [
      {
        "name": "ci",
        "displayName": "CI Build",
        "cacheVariables": {
          "CMAKE_BUILD_TYPE": "Release",
          "BUILD_TESTING": "ON"
        }
      }
    ]
  }
  ```
- **주요 타겟**:
  - `hnvue-infra`: 공유 라이브러리
  - `hnvue-ipc`: gRPC 서버 (공유 라이브러리)
  - `hnvue-hal`: 공유 라이브러리 + 플러그인 DLL
  - `hnvue-imaging`: 공유 라이브러리 + 엔진 플러그인 DLL

**vcpkg (C++ Package Manager)**
- **버전**: Latest (builtin-baseline: 97e9c92ba648e30e6ffe7c8bca81ae4c786f8e3a)
- **의존성**:
  ```json
  {
    "dependencies": [
      {"name": "gtest", "version>=": "1.14.0"},
      {"name": "protobuf", "version>=": "25.0"},
      {"name": "grpc", "version>=": "1.68.0"},
      {"name": "spdlog", "version>=": "1.13.0"},
      {"name": "fmt", "version>=": "10.0.0"}
    ]
  }
  ```
- **triplet**: x64-windows

### Libraries (라이브러리)

**gRPC 1.68.x + Protocol Buffers 25.0+**
- **용도**: IPC 서버, 메시지 직렬화
- **구현**:
  ```cpp
  // hnvue-ipc/include/hnvue/ipc/grpc_server.hpp
  class GrpcServer {
  public:
      void Start(std::string_view address);
      void Stop();
      void WaitForShutdown();
  };
  ```
- **생성 코드**: `proto/` 디렉토리에 protobuf 컴파일러로 생성

**spdlog 1.13.0+**
- **용도**: 구조화된 로깅
- **특징**:
  - 헤더 온니 라이브러리 (빠른 컴파일)
  - 다중 싱크 (파일, 콘솔, rotating 파일)
  - 포맷팅 지원 (fmt 기반)
- **사용 예**:
  ```cpp
  #include <spdlog/spdlog.h>
  spdlog::info("Detector acquired image: {}x{}", width, height);
  spdlog::error("HV Generator communication failed: {}", error_msg);
  ```

**fmt 10.0.0+**
- **용도**: 타입 안전한 문자열 포맷팅
- **특징**: Python 스타일 포맷팅, 컴파일 타임 검사

**Google Test 1.14.0+**
- **용도**: C++ 단위 테스트
- **사용 패턴**:
  ```cpp
  TEST(DetectorTest, AcquireImageSuccess) {
      auto detector = CreateMockDetector();
      ImageBuffer image;
      ASSERT_TRUE(detector->Acquire(image));
      EXPECT_EQ(image.width, 2048);
      EXPECT_EQ(image.height, 2048);
  }
  ```

**OpenCV (Latest)**
- **용도**: 이미지 처리 엔진
- **기능**: 이미지 필터, 기하학적 변환, 색상 공간 변환

**FFTW (Latest)**
- **용도**: 고속 푸리에 변환
- **기능**: 주파수 도메인 필터링

### Coding Standards (코딩 표준)

**네이밍 컨벤션 (Naming Convention)**
- 파일: `snake_case` (예: `detector_driver.cpp`, `image_buffer.h`)
- 클래스/구조체: `PascalCase` (예: `ImageBuffer`, `DetectorDriver`)
- 함수: `PascalCase` (예: `AcquireImage()`, `ProcessImage()`)
- 변수: `snake_case` (예: `image_width`, `exposure_time`)
- 상수: `UPPER_SNAKE_CASE` (예: `MAX_IMAGE_SIZE`, `DEFAULT_EXPOSURE_TIME`)
- 네임스페이스: `lowercase` (예: `hnvue::hal`, `hnvue::imaging`)

**코드 스타일 (Code Style)**
- 들여쓰기: 2 스페이스
- 최대 라인 길이: 120자
- 중괄호: Allman 스타일 (여는 중괄호 새 라인)
- 포인터/참조: `type* var` (별표 타입 쪽)

```cpp
// 예시
namespace hnvue::hal {

class IDetector {
public:
    virtual bool Initialize() = 0;
    virtual bool Acquire(ImageBuffer& image) = 0;
    virtual void SetExposureParams(const ExposureParams& params) = 0;
};

} // namespace hnvue::hal
```

---

## C# TECHNOLOGY STACK (C# 기술 스택)

### Language & Runtime (언어 및 런타임)

**C# 12 (.NET 8 LTS)**
- **선택 이유**: Long-Term Support (2026년 11월까지), 최신 C# 기능
- **주요 기능 사용**:
  - Records: 불변 데이터 모델
  - Pattern Matching: 복잡한 조건 로직
  - Nullable Reference Types: Null 안전성
  - Async/Await: 비동기 I/O

**.NET 8 LTS**
- **Target Framework**: net8.0
- **Platform**: Windows 10/11 x64
- ** Garbage Collection**: Workstation Server GC (높은 처리량)

### Build System (빌드 시스템)

**MSBuild 17.0+**
- **프로젝트 형식**: SDK 스타일 .csproj
- **공통 설정** (`Directory.Build.props`):
  ```xml
  <PropertyGroup>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors> <!-- XML 문서 주소 -->
  </PropertyGroup>
  ```

**NuGet 6.0+ (Central Package Management)**
- **중앙 패키지 관리**: `Directory.Packages.props`에 모든 버전 정의
- **주요 패키지**:
  ```xml
  <ItemGroup>
    <!-- Core Framework -->
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />

    <!-- gRPC -->
    <PackageVersion Include="Google.Protobuf" Version="3.25.2" />
    <PackageVersion Include="Grpc.Net.Client" Version="2.59.0" />
    <PackageVersion Include="Grpc.Tools" Version="2.60.0" />

    <!-- DICOM -->
    <PackageVersion Include="fo-dicom" Version="5.1.1" />

    <!-- MVVM -->
    <PackageVersion Include="Prism.Wpf" Version="9.0.537" />

    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.7.0" />
    <PackageVersion Include="Moq" Version="4.20.70" />
    <PackageVersion Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>
  ```

### Libraries (라이브러리)

**gRPC Client 2.59.0 + Google.Protobuf 3.25.2**
- **용도**: IPC 클라이언트
- **구현**:
  ```csharp
  using Grpc.Net.Client;
  using HnVue.Ipc;

  public class GrpcChannelService {
      private readonly GrpcChannel _channel;

      public async Task<ExposureResponse> SendExposureCommandAsync(ExposureRequest request) {
          var client = new ExposureService.ExposureServiceClient(_channel);
          return await client.ExecuteExposureAsync(request);
      }
  }
  ```

**fo-dicom 5.1.1**
- **용도**: DICOM 파일 처리
- **특징**:
  - DICOM 파서/인코더
  - DICOM 네트워크 (SCU/SCP)
  - 메타데이터 관리
- **사용 예**:
  ```csharp
  using Dicom;

  public class DicomExportService {
      public void ExportToDicom(string filePath, ImageBuffer image, DicomMetadata metadata) {
          var dicomFile = DicomFile.New();
          dicomFile.Dataset.Add(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage);
          dicomFile.Dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
          // ... 메타데이터 추가
          dicomFile.Dataset.Add(DicomTag.PixelData, image.RawData);
          dicomFile.Save(filePath);
      }
  }
  ```

**Prism.Wpf 9.0.537**
- **용도**: MVVM 프레임워크, 의존성 주입, 이벤트 агрегator
- **특징**:
  - `IEventAggregator`: 게시-구독 패턴
  - `IContainerProvider`: DI 컨테이너
  - `DelegateCommand`: 커맨드 패턴

**Microsoft.Extensions.Logging 8.0.0**
- **용도**: 구조화된 로깅
- **특징**:
  - Provider: 콘솔, 파일, Debug
  - 필터링: 로그 레벨별 제어
  - 스코프: 관련 로그 그룹화
- **사용 예**:
  ```csharp
  using Microsoft.Extensions.Logging;

  public class WorkflowEngine {
      private readonly ILogger<WorkflowEngine> _logger;

      public async Task<ExposureResult> PrepareExposureAsync(ExposureRequest request) {
          _logger.LogInformation("Preparing exposure for patient: {PatientId}", request.PatientId);
          try {
              // ... 로직
              _logger.LogInformation("Exposure preparation completed");
              return result;
          } catch (Exception ex) {
              _logger.LogError(ex, "Exposure preparation failed");
              throw;
          }
      }
  }
  ```

**xUnit 2.7.0 + Moq 4.20.70 + FluentAssertions 6.12.0**
- **용도**: C# 단위 테스트
- **사용 패턴**:
  ```csharp
  using Xunit;
  using Moq;
  using FluentAssertions;

  public class WorkflowEngineTests {
      [Fact]
      public async Task PrepareExposureAsync_AllInterlocksPass_ReturnsSuccess() {
          // Arrange
          var mockInterlockChecker = new Mock<IInterlockChecker>();
          mockInterlockChecker.Setup(x => x.CheckAllInterlocks())
                             .Returns(InterlockResult.AllPass);

          var engine = new WorkflowEngine(mockInterlockChecker.Object);

          // Act
          var result = await engine.PrepareExposureAsync(CreateTestRequest());

          // Assert
          result.Status.Should().Be(ExposureStatus.Ready);
      }

      [Theory]
      [InlineData(ExposureParams.LowDose, 10.0)]
      [InlineData(ExposureParams.NormalDose, 20.0)]
      public async Task PrepareExposureAsync_VariousDoses_CalculatesCorrectDose(
          ExposureParams exposureParams, double expectedDose) {
          // ... 테스트 구현
      }
  }
  ```

### Coding Standards (코딩 표준)

**네이밍 컨벤션 (Naming Convention)**
- 파일: `PascalCase` (예: `WorkflowEngine.cs`, `InterlockChecker.cs`)
- 클래스/인터페이스: `PascalCase` (예: `WorkflowEngine`, `IInterlockChecker`)
- 메서드: `PascalCase` (예: `PrepareExposureAsync()`, `CheckInterlocks()`)
- 속성: `PascalCase` (예: `PatientId`, `ExposureTime`)
- 지역 변수: `_camelCase` (예: `_logger`, `_interlockChecker`)
- 매개변수: `camelCase` (예: `patientId`, `exposureParams`)

**코드 스타일 (Code Style)**
- 들여쓰기: 4 스페이스 (Visual Studio 기본값)
- 최대 라인 길이: 120자
- 중괄호: Allman 스타일 (여는 중괄호 새 라인)
- using 정렬: System 외부 → Microsoft 외부 → 내부 네임스페이스

```csharp
// 예시
namespace HnVue.Workflow.Core;

using Microsoft.Extensions.Logging;

public class WorkflowEngine {
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly IInterlockChecker _interlockChecker;

    public WorkflowEngine(
        ILogger<WorkflowEngine> logger,
        IInterlockChecker interlockChecker) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interlockChecker = interlockChecker ?? throw new ArgumentNullException(nameof(interlockChecker));
    }

    public async Task<ExposureResult> PrepareExposureAsync(ExposureRequest request) {
        _logger.LogInformation("Preparing exposure for patient: {PatientId}", request.PatientId);

        var interlockResult = _interlockChecker.CheckAllInterlocks();
        if (!interlockResult.AllPass) {
            _logger.LogWarning("Interlock check failed: {FailedInterlocks}", interlockResult.FailedInterlocks);
            return ExposureResult.Failed(interlockResult);
        }

        // ... 노출 준비 로직

        return ExposureResult.Success();
    }
}
```

**XML 문서 주석 (XML Documentation Comments)**
- 공개 API에 필수 (CS1591 경고 비활성화로 인한 컴파일 오류 방지)
- 삼중 슬래시 `///` 사용

```csharp
/// <summary>
/// 노출 준비 및 실행을 관리하는 워크플로우 엔진
/// </summary>
public class WorkflowEngine {
    /// <summary>
    /// 지정된 요청으로 노출 준비를 수행합니다
    /// </summary>
    /// <param name="request">노출 요청 매개변수</param>
    /// <returns>노출 준비 결과</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/>가 <c>null</c>인 경우 발생
    /// </exception>
    public async Task<ExposureResult> PrepareExposureAsync(ExposureRequest request) {
        // ...
    }
}
```

---

## TESTING STRATEGY (테스트 전략)

### Test Coverage Goals (테스트 커버리지 목표)

**전체 커버리지 (Overall Coverage)**
- 목표: 85% 이상
- 측정 도구:
  - C++: `codecov` (GCC/Clang), `OpenCppCoverage` (MSVC)
  - C#: `coverlet.collector` (xUnit)

**레벨별 커버리지 요구사항 (Coverage by Safety Class)**
- Class C (안전 관련): 100% 분기 커버리지
  - `HnVue.Workflow/Safety/Interlocks/`: 9개 인터락
  - `HnVue.Workflow/Safety/ExposureController.cs`
- Class B: 90% 라인 커버리지
- Class A: 85% 라인 커버리지

### Test Structure (테스트 구조)

**C++ 테스트 구조**
```
tests/cpp/
├── hnvue-infra.Tests/
│   ├── logging_test.cpp
│   ├── config_test.cpp
│   └── thread_pool_test.cpp
├── hnvue-ipc.Tests/
│   ├── grpc_server_test.cpp
│   └── service_impl_test.cpp
├── hnvue-hal.Tests/
│   ├── detector_test.cpp
│   ├── hv_generator_test.cpp
│   └── plugin_manager_test.cpp
└── hnvue-imaging.Tests/
    ├── image_buffer_test.cpp
    └── processing_pipeline_test.cpp
```

**C# 테스트 구조**
```
tests/csharp/
├── HnVue.Workflow.Tests/
│   ├── WorkflowEngineTests.cs
│   ├── InterlockCheckerTests.cs
│   └── Interlocks/
│       ├── DetectorReadyInterlockTests.cs
│       ├── DoseLimitInterlockTests.cs
│       └── ... (9개 인터락 테스트)
├── HnVue.Dicom.Tests/
│   ├── DicomExportServiceTests.cs
│   └── DicomMetadataServiceTests.cs
├── HnVue.Dose.Tests/
│   ├── DoseTrackingServiceTests.cs
│   └── DoseAlertServiceTests.cs
└── HnVue.Ipc.Client.Tests/
    └── GrpcChannelServiceTests.cs
```

**통합 테스트 (Integration Tests)**
```
tests/integration/
└── HnVue.Integration.Tests/
    ├── IpcCommunicationTests.cs      # C++ 서버 ↔ C# 클라이언트
    ├── ExposureWorkflowTests.cs      # 전체 워크플로우
    └── DicomExportTests.cs           # DICOM 파일 검증
```

### Test Automation (테스트 자동화)

**C++ 테스트 실행 (CMake + CTest)**
```bash
# 빌드
cmake --preset ci
cmake --build --preset ci

# 테스트 실행
ctest --preset ci --output-on-failure

# 커버리지 보고서
cmake --preset ci
cmake --build --preset ci --target coverage
```

**C# 테스트 실행 (dotnet test)**
```bash
# 모든 테스트 실행
dotnet test

# 커버리지 포함
dotnet test --collect:"XPlat Code Coverage"

# 특정 프로젝트 테스트
dotnet test tests/csharp/HnVue.Workflow.Tests/HnVue.Workflow.Tests.csproj
```

### Test Quality Gates (테스트 품질 게이트)

**실행 전 (Pre-commit)**
- LSP 통과: Zero errors, zero warnings
- 단위 테스트: 100% 통과
- 코드 커버리지: 85% 이상

**CI/CD 파이프라인 (Pre-merge)**
- 모든 테스트: 100% 통과
- 통합 테스트: 100% 통과
- 코드 커버리지: 85% 이상 (변동 없음)
- 정적 분석: Zero security vulnerabilities

---

## CI/CD PIPELINE (CI/CD 파이프라인)

### Gitea Actions (자동화 빌드 및 테스트)

**CI 파이프라인 (`.gitea/workflows/ci.yml`)**
```yaml
name: CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  cpp-build-and-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup vcpkg
        run: |
          git clone https://github.com/Microsoft/vcpkg.git
          ./vcpkg/bootstrap-vcpkg.bat
      - name: Configure CMake
        run: cmake --preset ci
      - name: Build
        run: cmake --build --preset ci
      - name: Test
        run: ctest --preset ci --output-on-failure

  csharp-build-and-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal
```

### Deployment Strategy (배포 전략)

**개발 환경 (Development)**
- 자동 배포: `develop` 브랜치 푸시 시
- 아티팩트: Debug 빌드, 테스트 포함

**스테이징 환경 (Staging)**
- 자동 배포: `main` 브랜치 머지 시
- 아티팩트: Release 빌드, 통합 테스트 통과

**프로덕션 환경 (Production)**
- 수동 배포: 태그 생성 시 (`git tag v1.0.0`)
- 아티팩트: Release 빌드, 규제 문서 포함
- 조건: 모든 테스트 통과, 코드 리뷰 완료, 인증 획득

---

## SECURITY & COMPLIANCE (보안 및 규제 준수)

### Security Best Practices (보안 모범 사례)

**OWASP Top 10 준수**
- 입력 검증: 모든 외부 입력 검증
- 암호화: IPC 통신 암호화 (gRPC TLS)
- 인증: (향후 구현) 사용자 인증, 권한 부여
- 로깅: 보안 이벤트 기록
- 업데이트: 취약성 패치 즉시 적용

**취약성 스캔 (Vulnerability Scanning)**
- 도구: GitHub Dependabot, Snyk
- 주기: 매일 자동 스캔
- 정책: 취약성 발견 시 즉시 수정

### Regulatory Compliance (규제 준수)

**IEC 62304 Class B/C**
- 요구사항 추적 가능성: SPEC → 테스트
- 소프트웨어 개발 프로세스: PLAN-DO-CHECK-ACT
- 단위 테스트: 85%+ 커버리지
- 통합 테스트: 모든 인터페이스

**ISO 14971 (위험 관리)**
- 위험 분석: 초기 위험 식별
- 위험 평가: 심각도, 발생 가능성
- 위험 경감: 안전 인터락 구현
- 잔여 위험: 허용 가능 수준 확인

**ISO 13485 (품질 관리)**
- 문서화: 모든 프로세스 문서화
- 검증: 각 단계 검증
- 추적성: 요구사항-구현-테스트 추적
- 감사: 정기적 내부 감사

---

## DEVELOPMENT ENVIRONMENT (개발 환경)

### IDE Settings (IDE 설정)

**Visual Studio 2022**
- **C++ 설정**:
  - Language Standard: ISO C++17 Standard (/std:c++17)
  - Warning Level: Level4 (/W4)
  - Treat Warnings as Errors: Yes (/WX)
  - Conformance Mode: Yes (/permissive-)
  - SDL Checks: Yes (/sdl)

- **C# 설정**:
  - Language Version: C# 12.0
  - Nullable: Enable
  - Implicit Usings: Enable
  - Treat Warnings as Errors: Yes
  - Analyzer: Microsoft.CodeAnalysis.NetAnalyzers (모든 규칙)

**Extensions (확장)**
- C/C++: Microsoft C/C++ Extension
- C# Dev Kit: Microsoft C# Dev Kit
- CMake Tools: Microsoft CMake Tools
- .NET Core Test Explorer: Adobe .NET Core Test Explorer

### Environment Variables (환경 변수)

**개발 (Development)**
```bash
# C++
VCPKG_ROOT=D:\vcpkg
CMAKE_PREFIX_PATH=D:\vcpkg\installed\x64-windows

# C#
DOTNET_CLI_TELEMETRY_OPTOUT=1
DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# IPC
GRPC_SERVER_ADDRESS=localhost:50051
```

**테스트 (Testing)**
```bash
# 단위 테스트
TEST_MODE=UNIT
COVERAGE_OUTPUT=coverage.xml

# 통합 테스트
TEST_MODE=INTEGRATION
GRPC_SERVER_ADDRESS=127.0.0.1:50051
DICOM_SERVER_PORT=11112
```

---

## PERFORMANCE REQUIREMENTS (성능 요구사항)

### Non-Functional Requirements (비기능 요구사항)

**응답 시간 (Response Time)**
- UI 응답: < 200ms (사용자 작업)
- IPC 지연: < 100ms (95th percentile)
- 이미지 처리: < 2초 (2048x2048 DICOM)

**처리량 (Throughput)**
- 이미지 캡처: > 10 fps (고속 모드)
- DICOM 전송: > 100 MB/s (로컬 네트워크)

**자원 사용량 (Resource Usage)**
- 메모리: < 2GB (C++ Core Engine)
- 메모리: < 1GB (C# WPF GUI)
- CPU: < 80% (평균, 4코어)

### Performance Testing (성능 테스트)

**벤치마크 (Benchmarking)**
- 도구: Google Benchmark (C++), BenchmarkDotNet (C#)
- 주기: 각 릴리즈 전
- 기준: 이전 버전 대비 퇴보 없음

**프로파일링 (Profiling)**
- 도구: Visual Studio Profiler, PerfView
- 주기: 성능 저하 보고 시
- 목표: 병목 지점 식별 및 최적화

---

## TROUBLESHOOTING (문제 해결)

### Common Issues (일반적인 문제)

**빌드 실패 (Build Failures)**
- C++: vcpkg 의존성 해결 → `vcpkg install`
- C#: NuGet 패키지 복원 → `dotnet restore`
- CMake: 캐시 삭제 → `cmake --preset ci --fresh`

**테스트 실패 (Test Failures)**
- 단위 테스트: 로그 확인 → `ctest --preset ci --verbose`
- 통합 테스트: IPC 연결 확인 → `netstat -an | findstr 50051`

**IPC 통신 오류 (IPC Communication Errors)**
- 방화벽: Windows Defender 허용
- 포트 충돌: 다른 포트 사용
- 직렬화: Protobuf 버전 일치 확인

---

**문서 버전:** 1.0.0
**최종 업데이트:** 2026-02-18
**작성자:** abyz-lab
**언어:** Korean (ko)
