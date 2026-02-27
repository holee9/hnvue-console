# HnVue Proto File Generator

gRPC proto 파일을 컴파일하여 C++ 스텁을 생성하는 스크립트 모음입니다.

## 개요

SPEC-IPC-001에 따라 프로토콜 버퍼 정의 파일(.proto)로부터 C++ gRPC 클라이언트/서버 스텁 코드를 생성합니다.

## 파일 구조

```
scripts/
├── generate-proto.sh    # Linux/macOS용 Bash 스크립트
├── generate-proto.ps1   # Windows용 PowerShell 스크립트
└── README.md            # 이 문서
```

## 전제 조건

### 공통 요구사항

- Protocol Buffers 컴파일러 (protoc) 3.15+
- gRPC C++ 플러그인 (grpc_cpp_plugin)

### Linux/Ubuntu/Debian

```bash
sudo apt-get update
sudo apt-get install -y \
    protobuf-compiler \
    libprotobuf-dev \
    libgrpc++-dev \
    grpc-plugins
```

### macOS

```bash
brew install protobuf grpc
```

### Windows (vcpkg)

```powershell
vcpkg install protobuf:x64-windows
vcpkg install grpc:x64-windows
```

## 사용 방법

### Linux/macOS

```bash
# 실행 권한 부여
chmod +x scripts/generate-proto.sh

# 스크립트 실행
./scripts/generate-proto.sh
```

### Windows (PowerShell)

```powershell
# PowerShell 실행
pwsh

# 스크립트 실행 (정책이 제한된 경우)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process
.\scripts\generate-proto.ps1
```

## 생성되는 파일

스크립트는 다음 파일들을 `libs/hnvue-ipc/generated/` 디렉토리에 생성합니다:

| 파일 | 설명 |
|------|------|
| `hnvue_common.pb.h/cc` | 공통 타입 protobuf 스텁 |
| `hnvue_common.grpc.pb.h/cc` | 공통 타입 gRPC 스텁 |
| `hnvue_command.pb.h/cc` | 명령 서비스 protobuf 스텁 |
| `hnvue_command.grpc.pb.h/cc` | 명령 서비스 gRPC 스텁 |
| `hnvue_image.pb.h/cc` | 이미지 서비스 protobuf 스텁 |
| `hnvue_image.grpc.pb.h/cc` | 이미지 서비스 gRPC 스텁 |
| `hnvue_health.pb.h/cc` | 상태 서비스 protobuf 스텁 |
| `hnvue_health.grpc.pb.h/cc` | 상태 서비스 gRPC 스텁 |
| `hnvue_config.pb.h/cc` | 설정 서비스 protobuf 스텁 |
| `hnvue_config.grpc.pb.h/cc` | 설정 서비스 gRPC 스텁 |

## CMake 빌드

CMake를 사용하는 경우, proto 파일은 빌드 시 자동으로 생성됩니다:

```bash
# 빌드 디렉토리 생성
cmake -B build -S .

# 빌드 (proto 파일 자동 생성)
cmake --build build
```

## C# 코드 생성

C# 코드는 MSBuild + Grpc.Tools를 통해 자동으로 생성됩니다:

```bash
# C# 프로젝트 빌드 시 자동 생성
dotnet build src/HnVue.Ipc.Client/HnVue.Ipc.Client.csproj
```

생성된 C# 파일들은 `src/HnVue.Ipc.Client/artifacts/obj/` 디렉토리에 위치합니다.

## 스크립트 기능

### 사전 검사

- `protoc` 설치 확인
- `grpc_cpp_plugin` 설치 확인
- proto 파일 존재 확인

### 파일 생성

- Protobuf 코드 생성 (`--cpp_out`)
- gRPC 서비스 스텁 생성 (`--grpc_out`)

### 검증

- 예상되는 모든 파일 생성 확인
- 누락된 파일 경고

## 문제 해결

### protoc를 찾을 수 없음

**오류**: `protoc not found`

**해결**:
- Linux: `sudo apt-get install protobuf-compiler`
- macOS: `brew install protobuf`
- Windows: vcpkg 또는 수동 설치 후 PATH에 추가

### grpc_cpp_plugin을 찾을 수 없음

**오류**: `grpc_cpp_plugin not found`

**해결**:
- Linux: `sudo apt-get install grpc-plugins`
- macOS: `brew install grpc`
- Windows: vcpkg 설치

### import 오류

**오류**: `Import "hnvue_common.proto" was not found`

**해결**:
- `--proto_path` 인자가 proto 디렉토리를 올바르게 가리키는지 확인
- 모든 proto 파일이 `proto/` 디렉토리에 있는지 확인

## 관련 문서

- [SPEC-IPC-001](/.moai/specs/SPEC-IPC-001/spec.md) - IPC 계층 사양
- [proto/CMakeLists.txt](/proto/CMakeLists.txt) - CMake 통합 설정
- [src/HnVue.Ipc.Client/HnVue.Ipc.Client.csproj](/src/HnVue.Ipc.Client/HnVue.Ipc.Client.csproj) - C# 프로젝트 설정

## 라이선스

Copyright (c) 2026 abyz-lab <hnabyz2023@gmail.com>
