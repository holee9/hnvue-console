# SPEC-SECURITY-001: 구현 계획

## Metadata

| Field    | Value                                              |
|----------|----------------------------------------------------|
| SPEC ID  | SPEC-SECURITY-001                                  |
| Title    | HnVue 의료기기 사이버보안 규정 준수                  |
| Package  | src/HnVue.Console/Security/, src/HnVue.Console/Services/Adapters/ |
| Language | C# 12 / .NET 8 LTS                                |
| Safety   | IEC 62304 Class B/C                                |

---

## 1. Milestones

### Primary Goal: 핵심 보안 기능 구현 (Sprint 1-2)

FDA Section 524B, EU MDR, MFDS 규정 준수를 위한 핵심 보안 기능 구현

범위:
- UserService RBAC 구현 (FR-SEC-01 ~ FR-SEC-05)
- AuditLogService 무결성 보장 (FR-SEC-06 ~ FR-SEC-10)
- gRPC TLS 1.3+ 구성 (FR-SEC-11 ~ FR-SEC-14)

- SBOM 자동 생성 (FR-SEC-15 ~ FR-SEC-17)

인수 게이트:
- 모든 보안 테스트 통과
- RBAC 권한 검증 100%
- 감사로그 무결성 검증 100%
- TLS 1.3 연결 확인 100%
- SBOM 생성 검증 100%

### Secondary Goal: mTLS 및 위협 모델링 (Sprint 3-4)
상호 인증 및 위협 분석 문서화

범위:
- mTLS 상호 인증 구현 (FR-SEC-12)
- 인증서 회전 프로세스 (FR-SEC-13)
- STRIDE 위협 분석 (FR-SEC-18)
- 위험 매트릭스 작성 (FR-SEC-19)
- 완화 전략 문서화 (FR-SEC-20)

인수 게이트:
- mTLS 상호 인증 성공
- 인증서 자동 갱신
- 위협 모델 문서 슅인드됨
- 위험 점수 계산 검증

- 완화 상태 추적 검증

### Final Goal: 규정 준수 문서화 (Sprint 5-6)
시장 출시 준비 문서 패키지

범위:
- Software Test Report (STR)
- SBOM 최종 검증
- 침투 시스템 테스트 (DAST)
- 보안 감사 보고서
- 규정 준수 체크리스트

인수 게이트:
- SAST/DAST 스캔 결과 Critical/High 0건
- 침투 테스트 Critical/High 취약점 0건
- 모든 규정 문서 승인됨
- IEC 62304 추적성 매트릭스 완료

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      Security Architecture                         │
├─────────────────────────────────────────────────────────────────┤
│  Layer             │  Components                    │ Security Controls  │
├─────────────────────────────────────────────────────────────────┤
│  Presentation       │  LoginViewModel                 │ Input Validation   │
│                    │  UserManagementViewModel         │ UI RBAC           │
├─────────────────────────────────────────────────────────────────┤
│  Application        │  IAuthenticationService           │ Session Management │
│                    │  IAuthorizationService            │ Permission Check   │
│                    │  IAuditLogger                    │ Audit Logging      │
├─────────────────────────────────────────────────────────────────┤
│  Infrastructure     │  UserServiceAdapter              │ gRPC TLS/mTLS      │
│                    │  AuditLogServiceAdapter           │ Log Signing        │
│                    │  CertificateManager             │ Cert Rotation      │
├─────────────────────────────────────────────────────────────────┤
│  Data              │  SecureUserStore                   │ Password Hashing   │
│                    │  AuditLogStore (WORM)            │ Integrity Chain   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Implementation Phases
### Phase 1: UserService 구현 (1주차)
#### 3.1.1 새 파일 생성
```
src/HnVue.Console/
├── Security/
│   ├── IAuthenticationService.cs      # 인증 인터페이스
│   ├── IAuthorizationService.cs     # 인가 인터페이스
│   ├── AuthenticationResult.cs     # 인증 결과 모델
│   ├── SessionInfo.cs              # 세션 정보 모델
│   ├── PasswordPolicy.cs           # 비밀번호 정책
│   ├── PasswordHasher.cs           # 비밀번호 해싱
│   └── AccountLockoutPolicy.cs     # 계정 잠금 정책
├── Services/
│   └── Adapters/
│       └── UserServiceAdapter.cs    # (기존 파일 수정)
tests/HnVue.Console.Tests/
└── Security/
    ├── AuthenticationServiceTests.cs
    ├── AuthorizationServiceTests.cs
    ├── SessionTests.cs
    ├── PasswordPolicyTests.cs
    ├── PasswordHasherTests.cs
    └── AccountLockoutTests.cs
```
#### 3.1.2 주요 작업
1. IAuthenticationService 인터페이스 정의
2. SessionInfo, 데이터 모델 구현
3. PasswordPolicy (최소 12자, 복잡성 규칙) 구현
4. PasswordHasher (Argon2id/bcrypt) 구현
5. AccountLockoutPolicy (5회 실패 시 30분 잠금) 구현
6. UserServiceAdapter에 인증 로직 통합
7. 단위 테스트 작성

### Phase 2: AuditLogService 구현 (1주차)
#### 3.2.1 새 파일 생성
```
src/HnVue.Console/
├── Security/
│   ├── IAuditLogger.cs             # 감사로그 인터페이스
│   ├── AuditEntry.cs              # 감사 항목 모델
│   ├── AuditLogIntegrity.cs       # 무결성 검증
│   └── PhiMaskingService.cs       # PHI 마스킹
├── Services/
│   └── Adapters/
│       └── AuditLogServiceAdapter.cs  # (기존 파일 수정)
tests/HnVue.Console.Tests/
└── Security/
    ├── AuditLoggerTests.cs
    ├── AuditLogIntegrityTests.cs
    └── PhiMaskingTests.cs
```
#### 3.2.2 주요 작업
1. IAuditLogger 인터페이스 정의
2. AuditEntry 해시 체인 구조 구현
3. AuditLogIntegrity (SHA-256 서명) 구현
4. PhiMaskingService 구현
5. WORM 저장소 구성 (NTFS 설정 또는 전용 디렉토리)
6. UserServiceAdapter에 감사 이벤트 발행 통합
7. 단위 테스트 작성

### Phase 3: gRPC TLS/mTLS 구현 (1주차)
#### 3.3.1 새 파일 생성
```
src/HnVue.Console/
├── Security/
│   ├── GrpcSecurityOptions.cs      # TLS 옵션 구성
│   ├── CertificateManager.cs      # 인증서 관리
│   └── TlsVersionEnforcer.cs      # TLS 버전 강제
├── Configuration/
│   └── grpc_security.json          # TLS 설정 파일
tests/HnVue.Console.Tests/
└── Security/
    ├── GrpcSecurityTests.cs
    ├── CertificateManagerTests.cs
    └── TlsVersionTests.cs
```
#### 3.3.2 주요 작업
1. GrpcSecurityOptions 구현 (TLS 1.3만 허용)
2. CertificateManager (인증서 로드/갱신) 구현
3. TlsVersionEnforcer (TLS 1.2 거부) 구현
4. mTLS 상호 인증 구성
5. gRPC 채널 팩토리 수정
6. 통합 테스트 작성

### Phase 4: SBOM 및 CI/CD 보안 (0.5주차)
#### 3.4.1 새 파일/수정
```
.github/workflows/
├── security-scan.yml          # SAST/DAST 스캔
├── sbom-generation.yml        # SBOM 생성
build/
├── sbom/                     # SBOM 출력 디렉토리
│   └── hnvue-console-{version}.spdx.json
docs/security/
├── threat-model.md           # STRIDE 분석
├── risk-matrix.md            # 위험 매트릭스
└── mitigations.md            # 완화 전략
```
#### 3.4.2 주요 작업
1. SPDX SBOM 생성 스크립트 작성
2. NuGet Audit CI/CD 통합
3. Trivy 취약점 스캔 통합
4. SonarQube SAST 통합
5. STRIDE 위협 분석 문서 작성
6. 위험 매트릭스 작성
7. 완화 전략 문서화

---

## 4. Dependency Management
### 4.1 NuGet 패키지
| Package | Version | Purpose | License |
|---------|---------|---------|---------|
| BCrypt.Net-Next | 4.0.3 | 비밀번호 해싱 | MIT |
| Google.Protobuf | 3.28.3 | Protobuf 직렬화 | BSD-3-Clause |
| Grpc.Net.Client | 2.66.0 | gRPC 클라이언트 | MIT |
| Grpc.Net.ClientFactory | 2.66.0 | gRPC DI | MIT |
| Microsoft.CodeAnalysis.NetAnalyzers | 8.0.0 | 정적 분석 | MIT |

### 4.2 보안 관련 설정
```xml
<!-- Directory.Build.props -->
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

---

## 5. Testing Strategy
### 5.1 테스트 계층
1. **단위 테스트**: 각 보안 컴포넌트의 격리된 테스트
2. **통합 테스트**: UserServiceAdapter, AuditLogServiceAdapter 통합 테스트
3. **보안 테스트**: 인증 우회, 권한 상승, 무결성 검증
4. **성능 테스트**: TLS 핸드셰이크, 암호화 성능

### 5.2 테스트 커버리지 목표
| Module | Target | Priority |
|--------|--------|----------|
| Security/ | 90% | Required |
| Services/Adapters/ | 85% | Required |
| ViewModels/ (인증 관련) | 80% | Required |

### 5.3 보안 테스트 도구
| Tool | Purpose | Integration |
|------|---------|-------------|
| xUnit | 단위/통합 테스트 | Local |
| SonarQube | SAST | CI/CD |
| OWASP ZAP | DAST | Pre-release |
| Trivy | 취약점 스캔 | CI/CD |
| NuGet Audit | 의존성 스캔 | Build |

---

## 6. Documentation Deliverables
### 6.1 필수 문서
| Document | Template | Status |
|----------|----------|--------|
| 위협 모델 (STRIDE) | docs/security/threat-model.md | 작성 필요 |
| 위험 매트릭스 | docs/security/risk-matrix.md | 작성 필요 |
| 완화 전략 | docs/security/mitigations.md | 작성 필요 |
| 보안 아키텍처 | docs/security/architecture.md | 작성 필요 |
| SBOM | build/sbom/*.spdx.json | 자동 생성 |
| Software Test Report | .moai/templates/security/ | 작성 필요 |

### 6.2 규정 준수 체크리스트
- [ ] FDA Section 524B 체크리스트 완료
- [ ] EU MDR MDCG 2019-16 준수
- [ ] MFDS 사이버보안 기술문서 요구사항 충족
- [ ] IEC 62304 추적성 매트릭스 완료

---

## 7. Rollout Plan
### 7.1 배포 단계
1. **Dev 환경**: 보안 기능 개발 및 테스트
2. **QA 환경**: 보안 테스트, 침투 테스트
3. **Staging 환경**: 규정 준수 검증
4. **Production 환경**: 단계적 롤아웃

### 7.2 롤백 조건
- Critical 취약점 발견 시 즉시 롤백
- 인증 서비스 장애 시 이전 버전으로 롤백
- 규정 준수 실패 시 배포 중단

### 7.3 모니터링
- 인증 실패율 모니터링
- 감사 로그 무결성 모니터링
- TLS 연결 상태 모니터링
- 취약점 스캔 결과 알림

---

## 8. Success Metrics
### 8.1 기술 지표
- RBAC 권한 검증 100% 통과
- 세션 타임아웃 정확도 100%
- 감사 로그 무결성 100%
- TLS 1.3 연결 100%

### 8.2 보안 지표
- SAST Critical/High 취약점 0건
- DAST Critical/High 취약점 0건
- 의존성 취약점 0건
- 침투 테스트 Critical 발견 0건

### 8.3 규정 준수 지표
- FDA 체크리스트 100% 완료
- EU MDR 준수 100%
- MFDS 준수 100%
- 문서화 100% 완료

```

---

## 9. Risk Mitigation
| Risk | Mitigation | Owner |
|------|-----------|------|
| 기존 기능 호환성 | 단계적 마이그레이션, 포괄적 테스트 | Dev Team |
| 성능 저하 | 성능 테스트, 최적화 | Dev Team |
| 인증서 만료 | 자동 갱신, 모니터링 | DevOps |
| 규정 변경 | 정기적 모니터링, 유연한 설계 | Regulatory |
| 취약점 공개 | IRP, 패치 관리 프로세스 | Security Team |
```
---

**Document Version**: 1.0.0
**Last Updated**: 2026-03-12
