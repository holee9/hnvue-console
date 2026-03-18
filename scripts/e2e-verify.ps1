#!/usr/bin/env pwsh
<#
.SYNOPSIS
    E2E 실증 동작검증 - WPF 앱 자동 클릭으로 구현 기능 검증

.DESCRIPTION
    HnVue Console 앱을 실제 실행하여 UI 자동 클릭(FlaUI UIA3)으로
    각 뷰의 기능 구현 여부를 검증합니다.

    검증 내용:
    - 앱이 크래시 없이 시작되는가
    - 9개 뷰 전체 렌더링이 가능한가 (XAML 바인딩 크래시 감지)
    - 네비게이션 버튼이 동작하는가
    - 핵심 UI 요소(버튼, DataGrid 등)가 존재하는가

    실행 환경:
    - Windows 인터랙티브 세션 필수 (FlaUI UIAutomation)
    - gRPC 서버 불필요 (E2E 모드에서 Mock 서비스 자동 사용)

.PARAMETER Build
    E2E 실행 전 앱을 빌드합니다

.PARAMETER Config
    빌드 구성 (Debug / Release). 기본값: Debug

.PARAMETER Filter
    테스트 필터 (특정 테스트 클래스만 실행)
    예: -Filter "NavigationTests" 또는 -Filter "ImageReview"

.PARAMETER Verbose
    상세 테스트 출력 표시

.EXAMPLE
    # 기본: 앱 빌드 후 전체 E2E 검증
    .\scripts\e2e-verify.ps1 -Build

.EXAMPLE
    # 빠른 실행 (이미 빌드된 경우)
    .\scripts\e2e-verify.ps1

.EXAMPLE
    # 특정 뷰만 검증
    .\scripts\e2e-verify.ps1 -Filter "ImageReview"

.EXAMPLE
    # Release 빌드 검증
    .\scripts\e2e-verify.ps1 -Build -Config Release
#>

[CmdletBinding()]
param(
    [switch]$Build,

    [ValidateSet("Debug", "Release")]
    [string]$Config = "Debug",

    [string]$Filter = "",

    [switch]$ShowVerbose
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptRoot
$E2EProject = Join-Path $ProjectRoot "tests\e2e\HnVue.Console.E2E.Tests\HnVue.Console.E2E.Tests.csproj"
$AppProject = Join-Path $ProjectRoot "src\HnVue.Console\HnVue.Console.csproj"

function Write-Banner {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║    HnVue Console - E2E 실증 동작검증                    ║" -ForegroundColor Cyan
    Write-Host "║    WPF UI 자동 클릭으로 기능 구현 검증                   ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-Host "  ▶ $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✅ $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  ❌ $Message" -ForegroundColor Red
}

# Check desktop environment
$isCI = $env:CI -eq "true" -or -not [string]::IsNullOrEmpty($env:GITHUB_ACTIONS)
if ($isCI) {
    Write-Host ""
    Write-Host "  ⚠️  CI 환경 감지됨. E2E 테스트는 인터랙티브 데스크탑 필요." -ForegroundColor Yellow
    Write-Host "     로컬 개발 머신에서 실행하거나 HNVUE_E2E_FORCE=1 설정하세요." -ForegroundColor Yellow
    Write-Host ""
}

Write-Banner

# Step 1: Build
if ($Build) {
    Write-Step "앱 빌드 중... ($Config)"
    $buildResult = dotnet build $AppProject -c $Config --nologo -v quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "빌드 실패"
        Write-Host $buildResult -ForegroundColor Red
        exit 1
    }
    Write-Success "빌드 완료"
}

# Step 2: Run E2E tests
Write-Step "E2E 자동 클릭 검증 시작..."
Write-Host "     - gRPC 서버 불필요 (Mock 서비스 자동 사용)" -ForegroundColor DarkGray
Write-Host "     - 스크린샷 저장: tests/e2e/screenshots/" -ForegroundColor DarkGray
Write-Host ""

$testArgs = @(
    "test", $E2EProject,
    "-c", $Config,
    "--no-build",
    "--logger", "console;verbosity=$(if ($ShowVerbose) { 'detailed' } else { 'minimal' })"
)

if ($Filter -ne "") {
    $testArgs += "--filter"
    $testArgs += "FullyQualifiedName~$Filter"
    Write-Step "필터 적용: $Filter"
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet @testArgs
$exitCode = $LASTEXITCODE
$sw.Stop()

Write-Host ""
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

if ($exitCode -eq 0) {
    Write-Success "E2E 실증 검증 통과! ($([math]::Round($sw.Elapsed.TotalSeconds, 1))s)"
    Write-Host ""
    Write-Host "  모든 뷰가 크래시 없이 렌더링되고 핵심 UI 요소가 동작합니다." -ForegroundColor Green
} else {
    Write-Fail "E2E 검증 실패 ($([math]::Round($sw.Elapsed.TotalSeconds, 1))s)"
    Write-Host ""
    Write-Host "  실패 진단:" -ForegroundColor Yellow
    Write-Host "    1. tests/e2e/screenshots/ 에서 실패 스크린샷 확인" -ForegroundColor Yellow
    Write-Host "    2. tests/e2e/e2e_logs/ 에서 상세 로그 확인" -ForegroundColor Yellow
    Write-Host "    3. Windows Event Log 확인: Get-EventLog -LogName Application -Source '.NET Runtime' -Newest 5" -ForegroundColor Yellow
    Write-Host "    4. -ShowVerbose 옵션으로 재실행" -ForegroundColor Yellow
}

Write-Host ""
exit $exitCode
