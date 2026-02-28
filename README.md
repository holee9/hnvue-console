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
| SPEC-DOSE-001 | Dose Monitoring Service | ❌ 미완료 | 0% |
| SPEC-WORKFLOW-001 | Workflow Engine (Class C Safety) | 🟡 진행 중 | 60% |
| SPEC-UI-001 | WPF Console UI | ❌ 미완료 | 0% |
| SPEC-TEST-001 | Test Infrastructure | ❌ 미완료 | 0% |

**전체 진행률: 5/9 SPEC (56%)**

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
│   ├── HnVue.Dicom/         # DICOM Service
│   ├── HnVue.Dose/          # Dose Monitoring
│   ├── HnVue.Workflow/      # Workflow Engine
│   └── HnVue.Console/       # WPF GUI
├── tests/                   # Test suites
│   ├── cpp/                 # C++ tests (Google Test)
│   ├── csharp/              # C# tests (xUnit)
│   └── integration/         # Integration tests
└── .moai/                   # MoAI-ADK configuration
    └── specs/               # SPEC documents
```

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

---

## 문서

- [SPEC 문서](.moai/specs/)
- [아키텍처](docs/)
- [연구 보고서](docs/xray-console-sw-research.md)

---

## 라이선스

Copyright © 2025 abyz-lab. All rights reserved.

---

## 기여

이 프로젝트는 의료용 소프트웨어로 IEC 62304 표준을 따릅니다. 기여 방법은 별도 문서를 참고하십시오.
