# Windows 개발 환경 설정 가이드

의료용 X-ray GUI 콘솔 소프트웨어(HnVue.Console) 개발을 위한 Windows 전용 개발 환경 설정 가이드입니다.

## 목차

1. [사전 요구사항](#1-사전-요구사항)
2. [개발 도구 설치](#2-개발-도구-설치)
3. [환경 변수 설정](#3-환경-변수-설정)
4. [설치 검증](#4-설치-검증)
5. [프로젝트 설정](#5-프로젝트-설정)
6. [문제 해결](#6-문제-해결)

---

## 1. 사전 요구사항

### 시스템 요구사항

- **운영체제**: Windows 10 64-bit 또는 Windows 11 (Build 19041 이상)
- **RAM**: 16GB 이상 권장
- **디스크 공간**: 50GB 이상 여유 공간 (SSD 권장)
- **인터넷 연결**: 패키지 다운로드 및 Docker 이미지 pull을 위한 연결

### 필수 권한

- 관리자 권한 (도구 설치 및 환경 변수 설정을 위해)
- PowerShell 실행 정책 제어 (필요시)

---

## 2. 개발 도구 설치

### 2.1 Visual Studio 2022 Professional

**목적**: C# 12, .NET 8, WPF 애플리케이션 개발

**다운로드 링크**: https://visualstudio.microsoft.com/ko/vs/professional/

**설치 단계**:

1. Visual Studio 2022 Professional 다운로드 페이지 접속
2. 설치 프로그램 실행
3. **개별 구성 요소** 탭에서 다음 항목 선택:
   - .NET Desktop Build Tools
   - C# and Visual Basic Roslyn compilers
   - MSBuild
   - Windows 10/11 SDK (최신 버전)
   - Git for Windows
4. **워크로드** 탭에서 다음 선택:
   - .NET 데스크톱 개발
5. 설치 진행 (약 20-30GB 디스크 공간 필요)

**검증 명령**:

```powershell
# Visual Studio 버전 확인
"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" -version

# 예상 출력: MSBuild version 17.x.x
```

---

### 2.2 .NET 8 SDK

**목적**: 최신 .NET 플랫폼 및 런타임

**다운로드 링크**: https://dotnet.microsoft.com/download/dotnet/8.0

**설치 단계**:

1. .NET 8 SDK 다운로드 (Windows x64 installer)
2. 설치 프로그램 실행 (관리자 권한으로 실행)
3. 라이선스 동의 후 설치 진행
4. 설치 완료 후 시스템 재시작 (권장)

**검증 명령**:

```powershell
# .NET SDK 버전 확인
dotnet --version

# 예상 출력: 8.0.xxx

# 설치된 SDK 목록
dotnet --list-sdks

# 예상 출력: 8.0.xxx [C:\Program Files\dotnet\sdk]
```

---

### 2.3 CMake (v3.25+)

**목적**: C++ 프로젝트 빌드 시스템

**다운로드 링크**: https://cmake.org/download/

**설치 단계**:

1. CMake Windows x64 Installer 다운로드
2. 설치 프로그램 실행
3. **Add CMake to the system PATH for all users** 선택
4. 설치 경로 확인 (기본값: `C:\Program Files\CMake`)

**검증 명령**:

```powershell
# CMake 버전 확인
cmake --version

# 예상 출력: cmake version 3.25.x 또는 그 이상
```

---

### 2.4 vcpkg 패키지 관리자

**목적**: C++ 라이브러리 및 의존성 관리

**다운로드 링크**: https://github.com/microsoft/vcpkg

**설치 단계**:

```powershell
# PowerShell 관리자 모드에서 실행
cd D:\

# vcpkg 리포지토리 복제
git clone https://github.com/microsoft/vcpkg.git

# vcpkg 디렉토리로 이동
cd D:\vcpkg

# 부트스트래핑 실행
.\bootstrap-vcpkg.bat

# 시스템 통합 실행 (필요시)
.\vcpkg integrate install
```

**검증 명령**:

```powershell
# vcpkg 버전 확인
D:\vcpkg\vcpkg version

# 예상 출력: vcpkg package management program version 2024.xx.x
```

---

### 2.5 Docker Desktop for Windows

**목적**: Orthanc DICOM 서버, Python 시뮬레이터 컨테이너 실행

**다운로드 링크**: https://www.docker.com/products/docker-desktop/

**설치 단계**:

1. Docker Desktop for Windows 다운로드
2. 설치 프로그램 실행
3. **Use WSL 2 instead of Hyper-V** 옵션 선택 (권장)
4. 설치 완료 후 시스템 재시작
5. Docker Desktop 시작 및 로그인

**검증 명령**:

```powershell
# Docker 버전 확인
docker --version

# 예상 출력: Docker version 24.x.x

# Docker 실행 상태 확인
docker ps

# 예상 출력: CONTAINER ID   IMAGE     COMMAND   CREATED   STATUS    PORTS     NAMES (빈 목록)
```

**참고**: WSL 2 백엔드를 사용하는 경우 Windows Subsystem for Linux 업데이트 필요

---

## 3. 환경 변수 설정

### 3.1 시스템 환경 변수

Windows 환경 변수 설정 방법:

1. **시스템 환경 변수 편집** 열기:
   - Windows 키 + R → `sysdm.cpl` 입력
   - **고급** 탭 → **환경 변수** 클릭

2. **시스템 변수** 섹션에서 다음 변수 추가/편집:

| 변수 이름 | 값 | 목적 |
|----------|-----|------|
| `VCPKG_ROOT` | `D:\vcpkg` | vcpkg 설치 경로 |
| `CMAKE_PREFIX_PATH` | `D:\vcpkg\installed\x64-windows` | CMake가 vcpkg 라이브러리 찾기 |
| `DOTNET_ROOT` | `C:\Program Files\dotnet` | .NET SDK 경로 |
| `MSBUILD_PATH` | `C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe` | MSBuild 경로 |

3. **Path** 변수에 다음 항목 추가 (이미 있는 경우 확인만):

```
C:\Program Files\dotnet\
C:\Program Files\CMake\bin
D:\vcpkg
C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin
```

4. **확인** 클릭하여 모든 창 닫기

### 3.2 환경 변수 적용 확인

```powershell
# 새 PowerShell 세션 열기

# 환경 변수 확인
$env:VCPKG_ROOT
# 예상 출력: D:\vcpkg

$env:CMAKE_PREFIX_PATH
# 예상 출력: D:\vcpkg\installed\x64-windows

$env:DOTNET_ROOT
# 예상 출력: C:\Program Files\dotnet

# Path 변수에서 도구 접근 확인
where.exe dotnet
# 예상 출력: C:\Program Files\dotnet\dotnet.exe

where.exe cmake
# 예상 출력: C:\Program Files\CMake\bin\cmake.exe
```

---

## 4. 설치 검증

### 4.1 종합 검증 스크립트

PowerShell 관리자 모드에서 다음 스크립트 실행:

```powershell
# Visual Studio 2022 Professional 검증
Write-Host "=== Visual Studio 2022 검증 ===" -ForegroundColor Cyan
$msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
if (Test-Path $msbuildPath) {
    $version = & $msbuildPath -version
    Write-Host "✓ MSBuild 설치됨: $version" -ForegroundColor Green
} else {
    Write-Host "✗ MSBuild를 찾을 수 없음" -ForegroundColor Red
}

# .NET 8 SDK 검증
Write-Host "`n=== .NET 8 SDK 검증 ===" -ForegroundColor Cyan
$dotnetVersion = dotnet --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ .NET SDK 설치됨: $dotnetVersion" -ForegroundColor Green
    dotnet --list-sdks
} else {
    Write-Host "✗ .NET SDK를 찾을 수 없음" -ForegroundColor Red
}

# CMake 검증
Write-Host "`n=== CMake 검증 ===" -ForegroundColor Cyan
$cmakeVersion = cmake --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ CMake 설치됨" -ForegroundColor Green
    Write-Host $cmakeVersion[0]
} else {
    Write-Host "✗ CMake를 찾을 수 없음" -ForegroundColor Red
}

# vcpkg 검증
Write-Host "`n=== vcpkg 검증 ===" -ForegroundColor Cyan
if (Test-Path $env:VCPKG_ROOT) {
    $vcpkgVersion = & "$env:VCPKG_ROOT\vcpkg" version
    Write-Host "✓ vcpkg 설치됨: $vcpkgVersion" -ForegroundColor Green
} else {
    Write-Host "✗ vcpkg를 찾을 수 없음 (VCPKG_ROOT 확인 필요)" -ForegroundColor Red
}

# Docker 검증
Write-Host "`n=== Docker Desktop 검증 ===" -ForegroundColor Cyan
$dockerVersion = docker --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Docker 설치됨: $dockerVersion" -ForegroundColor Green

    # Docker 실행 상태 확인
    $dockerStatus = docker ps 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Docker 실행 중" -ForegroundColor Green
    } else {
        Write-Host "⚠ Docker가 설치되었으나 실행 중이 아님" -ForegroundColor Yellow
    }
} else {
    Write-Host "✗ Docker를 찾을 수 없음" -ForegroundColor Red
}

Write-Host "`n=== 검증 완료 ===" -ForegroundColor Cyan
Write-Host "모든 항목에 ✓ 표시가 있는지 확인하세요." -ForegroundColor Yellow
```

### 4.2 프로젝트 빌드 테스트

```powershell
# 프로젝트 디렉토리로 이동
cd D:\workspace-github\hnvue-console

# 솔루션 빌드
dotnet build HnVue.sln

# 예상 출력:
# - Microsoft (R) Build Engine version 17.x.x
# - 0 Warning(s)
# - 0 Error(s)

# 테스트 실행
dotnet test

# 예상 출력:
# - Total tests: 1,048
# - Passed: 1,048
# - Failed: 0
```

---

## 5. 프로젝트 설정

### 5.1 Git 리포지토리 클론

```powershell
# GitHub 인증 정보 설정 (필요시)
git config --global user.name "drake.lee"
git config --global user.email "drake.lee@abyzr.com"

# 리포지토리 클론
git clone https://github.com/holee9/hnvue-console.git D:\workspace-github\hnvue-console

# 프로젝트 디렉토리로 이동
cd D:\workspace-github\hnvue-console
```

### 5.2 NuGet 패키지 복원

```powershell
# 솔루션 수준에서 패키지 복원
dotnet restore

# 또는 Visual Studio에서:
# 1. HnVue.sln 열기
# 2. 도구 → NuGet 패키지 관리자 → 솔루션용 NuGet 패키지 복원
```

### 5.3 개발 환경 설정 확인

```powershell
# 프로젝트 구조 확인
Get-ChildItem -Directory

# 예상 출력:
# - .claude
# - .github
# - .moai
# - docs
# - src
# - tests

# 솔루션 파일 확인
Get-ChildItem -Filter "*.sln"

# 예상 출력: HnVue.sln
```

---

## 6. 문제 해결

### 6.1 Visual Studio 2022 관련

**문제**: MSBuild를 찾을 수 없음

**해결**:
```powershell
# Visual Studio 설치 경로 확인
Test-Path "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"

# 다른 에디션 사용 중인 경우 경로 수정:
# - Community: C:\Program Files\Microsoft Visual Studio\2022\Community\...
# - Enterprise: C:\Program Files\Microsoft Visual Studio\2022\Enterprise\...
```

---

### 6.2 .NET SDK 관련

**문제**: `dotnet --version` 실행 실패

**해결**:
```powershell
# Path 변수 재로드
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

# .NET 재설치 필요시:
# 1. 제어판 → 프로그램 및 기능 → Microsoft .NET SDK 제거
# 2. .NET 8 SDK 재설치
```

---

### 6.3 CMake 관련

**문제**: CMake가 vcpkg 라이브러리를 찾지 못함

**해결**:
```powershell
# CMAKE_PREFIX_PATH 확인
echo $env:CMAKE_PREFIX_PATH

# vcpkg 트리플릿 확인
D:\vcpkg\vcpkg list
# 예상 출력: <패키지 이름>:x64-windows

# CMake 캐시 삭제 후 재시도
Remove-Item -Recurse -Force CMakeCache.txt, CMakeFiles
cmake -DCMAKE_TOOLCHAIN_FILE=D:\vcpkg\scripts\buildsystems\vcpkg.cmake ..
```

---

### 6.4 Docker 관련

**문제**: Docker Desktop 시작 실패

**해결**:
```powershell
# WSL 2 설치 상태 확인
wsl --list --verbose

# WSL 2 업데이트 필요시:
wsl --update

# Hyper-V 충돌 확인
# Windows 기능 켜기/끼기에서 Hyper-V 비활성화

# Docker Desktop 재설치 필요시:
# 1. 앱 및 기능 → Docker Desktop 제거
# 2. C:\ProgramData\Docker 삭제
# 3. Docker Desktop 재설치
```

---

### 6.5 vcpkg 관련

**문제**: vcpkg 부트스트래핑 실패

**해결**:
```powershell
# 수동 부트스트래핑
cd D:\vcpkg
.\bootstrap-vcpkg.bat -disableMetrics

# 방화벽 문제인 경우:
# 1. Windows 보안 → 방화벽 및 네트워크 보호
# 2. 방화벽을 통해 앱 허용 → vcpkg.exe 허용

# 네트워크 문제인 경우 미러 사용:
.\vcpkg install <패키지명> --overlay-ports=<미러경로>
```

---

## 7. 추가 리소스

### 공식 문서

- [.NET 8 Documentation](https://learn.microsoft.com/ko-kr/dotnet/core/)
- [CMake Documentation](https://cmake.org/documentation/)
- [vcpkg Documentation](https://vcpkg.io/en/)
- [Docker Desktop Documentation](https://docs.docker.com/desktop/install/windows-install/)
- [Visual Studio 2022 Documentation](https://learn.microsoft.com/ko-kr/visualstudio/)

### 프로젝트 관련

- [HnVue.Console README](../README.md)
- [개발 로드맵](./development-roadmap-phase2.md)
- [사이버 보안 규정 준용 계획](./cybersecurity-compliance-plan.md)

### 지원 및 문의

- GitHub Issues: https://github.com/holee9/hnvue-console/issues
- 이메일: drake.lee@abyzr.com

---

## 버전 기록

- **v1.0.0** (2026-03-12): 초기 버전 출시
  - Windows 10/11 개발 환경 설정 가이드
  - 필수 도구 설치 절차
  - 환경 변수 설정 방법
  - 검증 절차 및 문제 해결

---

**작성일**: 2026-03-12
**마지막 수정**: 2026-03-12
**유지 보수**: MoAI DevOps 팀
