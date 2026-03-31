# SPEC-SECURITY-001: 의료기기 사이버보안 규정 준수

---

## Metadata

| Field          | Value                                              |
|----------------|----------------------------------------------------|
| SPEC ID        | SPEC-SECURITY-001                                  |
| Title          | HnVue 의료기기 사이버보안 규정 준수                  |
| Product        | HnVue - 진단용 의료기기 X선 GUI Console SW         |
| Component      | `src/HnVue.Console/Security/`, `src/HnVue.Console/Services/Adapters/` |
| Status         | Planned                                            |
| Priority       | Critical                                           |
| Safety Class   | IEC 62304 Class B/C                                |
| Created        | 2026-03-12                                         |
| Version        | 1.0.0                                              |

---

## 1. Environment

### 1.1 System Context

HnVue는 의료용 X선 영상 시스템의 GUI Console Software로서, FDA Section 524B, EU MDR MDCG 2019-16, 그리고 MFDS 의료기기 사이버보안 가이드라인을 준수해야 합니다. 본 SPEC은 사용자 인증/인가, 감사로그, 통신 보안, SBOM 관리, 위협 모델링의 5대 보안 영역을 정의합니다.

시스템 구성요소:
- HnVue Console (WPF GUI Client)
- Core Engine (Backend Service via gRPC IPC)
- 외부 시스템 (PACS/DICOM, 병원 정보 시스템)

### 1.2 Regulatory Environment

| Standard / Regulation        | Scope                                                        |
|------------------------------|--------------------------------------------------------------|
| FDA Section 524B             | 의료기기 사이버보안 사전시장 요구사항                        |
| EU MDR MDCG 2019-16          | 유럽 의료기기 사이버보안 가이드라인                          |
| MFDS Guidelines              | 식약처 의료기기 사이버보안 기술문서 요구사항                 |
| IEC 62304                    | 의료기기 소프트웨어 수명주기 - Class B/C 분류               |
| IEC 81001-5-1                | 의료기기 IT 보안 기능 요구사항                              |
| ISO 14971                    | 의료기기 위험관리                                            |
| AAMI TIR57                   | 의료기기 보안 위험관리 원칙                                  |
| NIST CSF 2.0                 | 사이버보안 프레임워크                                        |
| FDA 21 CFR Part 11           | 전자기록, 전자서식 규정                                      |

### 1.3 Safety Classification

IEC 62304 **Class B/C** — 환자 안전에 직접적인 영향을 줄 수 있는 소프트웨어로, 엄격한 보안 통제와 감사 추적이 요구됩니다.

---

## 2. Assumptions

| ID     | Assumption                                                                                          | Confidence | Risk if Wrong                                                   | Validation Method                          |
|--------|-----------------------------------------------------------------------------------------------------|------------|------------------------------------------------------------------|--------------------------------------------|
| A-01   | 사용자 인증 정보는 Windows 자격 증명 관리자 또는 안전한 저장소에 보관된다                           | High       | 인증 정보 유출 시 무단 접근 가능                                 | 보안 저장소 구현 검증                       |
| A-02   | gRPC 통신은 내부 네트워크(LAN)에서만 수행되며, 외부 직접 접속은 차단된다                              | High       | 외부 공격면 증가                                                 | 네트워크 구성 검토                          |
| A-03   | NTP 서버가 시스템에 구성되어 있어 정확한 타임스탬프 동기화가 가능하다                                 | Medium     | 감사로그 타임스탬프 부정확                                        | NTP 설정 확인                               |
| A-04   | 인증서는 조직의 PKI 인프라 또는 신뢰할 수 있는 CA에서 발급된다                                        | High       | 인증서 신뢰성 문제                                               | 인증서 출처 검증                            |
| A-05   | WORM(Write Once Read Many) 저장소가 감사로그 보관을 위해 사용 가능하다                               | Medium     | 감사로그 변조 가능성                                             | WORM 구성 검토                              |
| A-06   | 패키지 복원은 NuGet 공식 피드 또는 승인된 사설 피드에서만 수행된다                                     | High       | 악성 패키지 주입 가능                                            | NuGet 설정 검증                             |
| A-07   | 개발팀이 안전한 코딩 가이드라인을 준수하도록 교육받는다                                               | Medium     | 보안 취약점 포함 코드 작성                                        | 교육 이수 확인                              |

---

## 3. Requirements

### 3.1 R1: UserService (인증 및 인가)

#### 3.1.1 기능적 요구사항

**FR-SEC-01: 역할 기반 접근 제어 (RBAC)**

시스템은 다음 역할을 지원한다:
- `ADMINISTRATOR`: 전체 시스템 접근
- `RADIOLOGIST`: 의사 - 보고서 서명, 환자 데이터 접근
- `TECHNOLOGIST`: 방사선사 - 촬영 수행
- `PHYSICIST`: 의물리사 - QC 수행, 장비 설정
- `OPERATOR`: 기본 조작자
- `VIEWER`: 읽기 전용 접근
- `SERVICE`: 유지보수 엔지니어

```
When 사용자가 시스템에 로그인하면,
the system shall 사용자의 역할(Role)에 따라 접근 가능한 기능을 제한한다.
And 권한이 없는 기능에 접근 시도 시 ACCESS_DENIED 이벤트를 감사로그에 기록한다.
```

**FR-SEC-02: 세션 관리**

```
When 사용자 세션이 생성되면,
the system shall 세션 만료 시간을 30분으로 설정한다.
And 세션 ID는 암호학적으로 안전한 난수로 생성한다.
```

```
While 사용자가 활성 상태(요청 수행 중)이면,
the system shall 세션 만료 시간을 자동으로 연장한다.
```

```
When 세션이 30분간 비활성 상태이면,
the system shall 세션을 자동으로 종료하고 사용자를 로그인 화면으로 이동시킨다.
```

**FR-SEC-03: 계정 잠금 정책**

```
When 동일 계정으로 5회 연속 로그인 실패가 발생하면,
the system shall 해당 계정을 30분간 잠금한다.
And USER_LOGIN_FAILED 이벤트를 감사로그에 기록한다.
And 관리자에게 알림을 발송한다 (선택사항).
```

```
When 계정이 잠금된 후 30분이 경과하면,
the system shall 계정 잠금을 자동으로 해제한다.
```

**FR-SEC-04: 비밀번호 복잡성 정책**

```
When 사용자가 비밀번호를 생성하거나 변경하면,
the system shall 다음 조건을 검증한다:
  - 최소 12자 이상
  - 대문자, 소문자, 숫자, 특수문자 각각 1개 이상 포함
  - 이전 5개 비밀번호와 중복되지 않음
  - 일반적인 단어나 패턴 포함하지 않음
```

```
When 비밀번호 정책을 위반하는 입력이 제출되면,
the system shall 구체적인 오류 메시지를 표시하고 비밀번호 변경을 거부한다.
```

**FR-SEC-05: 비밀번호 저장**

```
Where 비밀번호를 저장하는 경우,
the system shall Argon2id 또는 bcrypt 해시 알고리즘을 사용한다.
And 솔트(salt)는 계정별로 고유하게 생성한다.
And 평문 비밀번호는 저장하지 않는다.
```

### 3.2 R2: AuditLogService (감사로그)

#### 3.2.1 기능적 요구사항

**FR-SEC-06: 감사로그 무결성**

```
When 감사로그 항목이 생성되면,
the system shall SHA-256 해시 서명을 생성하여 로그 무결성을 보장한다.
And 각 로그 항목은 이전 항목의 해시를 참조하는 체인 구조를 갖는다.
```

```
Where 감사로그 저장소는 WORM(Write Once Read Many) 방식으로 구성된다,
the system shall 로그 생성 후 수정 또는 삭제를 방지한다.
```

**FR-SEC-07: 감사로그 보관 기간**

```
Where 의료기기 규정 준수를 위해,
the system shall 감사로그를 최소 6년간 보관한다.
And 보관 기간 만료 후에도 안전하게 삭제한다.
```

**FR-SEC-08: 타임스탬프 정확성**

```
When 감사로그 항목이 생성되면,
the system shall NTP 동기화된 정확한 타임스탬프를 기록한다.
And 타임스탬프는 UTC 기준으로 저장하고 현지 시간으로 표시한다.
```

**FR-SEC-09: 감사 이벤트 유형**

시스템은 최소 다음 이벤트 유형을 기록한다:

| Event Type | Severity | Description |
|------------|----------|-------------|
| USER_LOGIN | INFO | 사용자 로그인 성공 |
| USER_LOGIN_FAILED | WARNING | 로그인 실패 |
| USER_LOGOUT | INFO | 로그아웃 |
| ACCESS_DENIED | WARNING | 권한 없는 접근 시도 |
| PASSWORD_CHANGE | INFO | 비밀번호 변경 |
| CONFIGURATION_CHANGED | WARNING | 시스템 설정 변경 |
| DATA_EXPORT | INFO | 데이터 내보내기 |
| EXPOSURE_STARTED | INFO | 노출 시작 |
| EXPOSURE_COMPLETED | INFO | 노출 완료 |

**FR-SEC-10: PHI 마스킹**

```
Where 감사로그에 환자 식별 정보(PHI)가 포함되는 경우,
the system shall Patient ID와 Patient Name을 마스킹하여 기록한다.
And 마스킹되지 않은 PHI는 INFO 레벨 이상의 로그에 포함하지 않는다.
```

### 3.3 R3: gRPC TLS/mTLS (통신 보안)

#### 3.3.1 기능적 요구사항

**FR-SEC-11: TLS 버전 요구사항**

```
Where gRPC 통신을 수행하는 경우,
the system shall TLS 1.3 이상을 사용한다.
And TLS 1.0, 1.1, 1.2는 허용하지 않는다.
```

**FR-SEC-12: 상호 인증 (mTLS)**

```
When 클라이언트가 Core Engine에 연결하면,
the system shall 클라이언트 인증서를 검증한다.
And 유효하지 않은 인증서로의 연결을 거부한다.
```

```
Where 서버 인증서 검증 시,
the system shall 인증서 만료일, 신뢰 체인, 폐지 상태(CRL/OCSP)를 확인한다.
```

**FR-SEC-13: 인증서 회전**

```
When 인증서 만료 30일 전이 되면,
the system shall 관리자에게 인증서 갱신 알림을 발송한다.
```

```
Where 인증서 회전 수행 시,
the system shall 서비스 중단 없이 새 인증서를 로드한다.
```

**FR-SEC-14: 암호화 스위트**

```
Where TLS 연결 설정 시,
the system shall 다음 암호화 스위트만 허용한다:
  - TLS_AES_256_GCM_SHA384
  - TLS_AES_128_GCM_SHA256
  - TLS_CHACHA20_POLY1305_SHA256
```

### 3.4 R4: SBOM (소프트웨어 자재 명세서)

#### 3.4.1 기능적 요구사항

**FR-SEC-15: SBOM 생성**

```
When 소프트웨어 빌드가 수행되면,
the system shall SPDX 1.5 형식의 SBOM을 자동 생성한다.
And SBOM은 NTIA 최소 요소사항을 모두 포함한다:
  - 공급자 이름
  - 컴포넌트 이름
  - 컴포넌트 버전
  - 고유 식별자
  - 종속성 관계
  - SBOM 작성자
  - 타임스탬프
```

**FR-SEC-16: CVE 스캔 통합**

```
When SBOM이 생성되면,
the system shall 알려진 CVE 데이터베이스와 자동으로 교차 검사한다.
And Critical/High 심각도의 취약점이 발견되면 빌드를 실패시킨다.
```

```
Where 취약점 스캔 결과는 다음 정보를 포함한다:
  - CVE ID
  - CVSS 점수
  - 영향받는 컴포넌트
  - 권장 조치
```

**FR-SEC-17: SBOM 버전 관리**

```
Where 각 릴리스 버전에 대해,
the system shall 해당 버전의 SBOM을 버전 관리 시스템에 저장한다.
And SBOM 파일은 디지털 서명되어 무결성이 보장된다.
```

### 3.5 R5: Threat Modeling (위협 모델링)

#### 3.5.1 기능적 요구사항

**FR-SEC-18: STRIDE 분석**

```
Where 시스템 설계 단계에서,
the system shall STRIDE 방법론을 사용한 위협 분석을 수행한다:
  - Spoofing (스푸핑)
  - Tampering (변조)
  - Repudiation (부인)
  - Information Disclosure (정보 유출)
  - Denial of Service (서비스 거부)
  - Elevation of Privilege (권한 상승)
```

**FR-SEC-19: 위험 점수 매트릭스**

```
When 위협이 식별되면,
the system shall 다음 기준으로 위험 점수를 산정한다:
  - 가능성(Likelihood): 1-5 (Low-High)
  - 영향(Impact): 1-5 (Low-High)
  - 위험 점수 = 가능성 × 영향
```

| Risk Score | Priority | Response Time |
|------------|----------|---------------|
| 1-4 | Low | 다음 릴리스 |
| 5-9 | Medium | 30일 이내 |
| 10-15 | High | 7일 이내 |
| 16-25 | Critical | 24시간 이내 |

**FR-SEC-20: 완화 전략**

```
When 위협이 식별되면,
the system shall 각 위협에 대한 완화 전략을 문서화한다.
And 완화 전략은 구현 상태(계획됨/진행중/완료)를 추적한다.
```

---

## 4. Non-Functional Requirements

### 4.1 NFR-SEC-01: 성능

- 인증 요청 응답 시간: 500ms 이내
- 감사로그 기록 지연: 100ms 이내
- TLS 핸드셰이크 오버헤드: 50ms 이내

### 4.2 NFR-SEC-02: 가용성

- 인증 서비스 가용성: 99.9%
- 감사로그 서비스 가용성: 99.99% (규정 준수)

### 4.3 NFR-SEC-03: 호환성

- Windows 10/11 지원
- .NET 8 LTS 호환
- 기존 gRPC 서비스와의 하위 호환성 유지

### 4.4 NFR-SEC-04: 테스트 커버리지

- 보안 모듈 라인 커버리지: 90% 이상
- 보안 테스트 케이스: 100% 통과

---

## 5. Traceability Matrix

| Requirement | Proto Definition | Adapter | Test File |
|-------------|------------------|---------|-----------|
| FR-SEC-01 | hnvue_user.proto: UserRole | UserServiceAdapter | UserServiceTests.cs |
| FR-SEC-02 | hnvue_user.proto: UserSession | UserServiceAdapter | SessionTests.cs |
| FR-SEC-03 | - | UserServiceAdapter | AccountLockoutTests.cs |
| FR-SEC-04 | - | UserServiceAdapter | PasswordPolicyTests.cs |
| FR-SEC-05 | - | UserServiceAdapter | PasswordHashTests.cs |
| FR-SEC-06 | hnvue_audit_log.proto | AuditLogServiceAdapter | AuditIntegrityTests.cs |
| FR-SEC-07 | - | AuditLogServiceAdapter | RetentionTests.cs |
| FR-SEC-08 | hnvue_audit_log.proto: Timestamp | AuditLogServiceAdapter | TimestampTests.cs |
| FR-SEC-09 | hnvue_audit_log.proto: AuditEventType | AuditLogServiceAdapter | EventTypesTests.cs |
| FR-SEC-10 | - | AuditLogServiceAdapter | PhiMaskingTests.cs |
| FR-SEC-11 | - | GrpcChannelFactory | TlsVersionTests.cs |
| FR-SEC-12 | - | GrpcChannelFactory | MtlsTests.cs |
| FR-SEC-13 | - | CertificateManager | CertRotationTests.cs |
| FR-SEC-14 | - | GrpcChannelFactory | CipherSuiteTests.cs |
| FR-SEC-15 | - | Build Pipeline | SbomGenerationTests.cs |
| FR-SEC-16 | - | CI/CD Pipeline | CveScanTests.cs |
| FR-SEC-17 | - | Release Process | SbomVersioningTests.cs |
| FR-SEC-18 | - | docs/security/ | ThreatModelReview.cs |
| FR-SEC-19 | - | docs/security/ | RiskMatrixTests.cs |
| FR-SEC-20 | - | docs/security/ | MitigationTrackingTests.cs |

---

## 6. Dependencies

### 6.1 Internal Dependencies

| Component | Dependency | Type |
|-----------|------------|------|
| UserService | SPEC-IPC-001 (gRPC) | Blocker |
| AuditLogService | SPEC-IPC-001 (gRPC) | Blocker |
| TLS Configuration | SPEC-IPC-001 (gRPC) | Blocker |

### 6.2 External Dependencies

| Library | Version | Purpose |
|---------|---------|---------|
| Google.Protobuf | 3.28.3 | Protobuf 직렬화 |
| Grpc.Net.Client | 2.66.0 | gRPC 클라이언트 |
| BCrypt.Net | 4.0.0 | 비밀번호 해싱 |
| System.Security.Cryptography | .NET 8 | 암호화 기능 |

---

## 7. Risks

| Risk ID | Description | Probability | Impact | Mitigation |
|---------|-------------|-------------|--------|------------|
| R-01 | 기존 gRPC 서비스와 mTLS 호환성 문제 | Medium | High | 단계적 마이그레이션, 롤백 계획 |
| R-02 | 성능 저하로 인한 사용자 경험 악화 | Medium | Medium | 성능 테스트, 최적화 |
| R-03 | 인증서 만료로 인한 서비스 중단 | Low | Critical | 자동 갱신, 모니터링 |
| R-04 | SBOM 생성 실패로 인한 빌드 중단 | Low | Medium | 수동 생성 대체 방안 |
| R-05 | 규정 준수 요구사항 변경 | Medium | High | 정기적 규정 모니터링 |

---

## 8. Acceptance Criteria Summary

- [x] RBAC가 모든 서비스에 적용됨 (7개 역할: Administrator, Radiologist, Technologist, Physicist, Operator, Viewer, Service)
- [x] 세션 30분 타임아웃 구현됨 (SessionTimeoutMinutes = 30)
- [x] 5회 실패 시 계정 잠금 구현됨 (MaxFailedLoginAttempts = 5, LockoutDurationMinutes = 30)
- [x] 비밀번호 복잡성 정책 적용됨 (12자+, 대소문자+숫자+특수문자)
- [x] 감사로그 SHA-256 서명 구현됨 (EntryHash, PreviousEntryHash 체인)
- [ ] WORM 저장소 구성됨 (인프라 구성 필요 - 운영 환경에서 적용)
- [x] 6년 보관 기간 설정됨 (RetentionYears = 6, EnforceRetentionPolicyAsync)
- [x] NTP 타임스탬프 동기화됨 (UTC 기준 타임스탬프, WorkstationId 포함)
- [x] TLS 1.3+ 적용됨 (MinTlsVersion = Tls13 기본값)
- [x] mTLS 상호 인증 구현됨 (EnableMutualTls, ClientCertificatePath)
- [x] 인증서 회전 프로세스 구축됨 (CertificateRotationDays = 90, ExpirationWarningDays = 30)
- [x] SPDX 1.5 SBOM 자동 생성됨 (.sbom/generate-sbom.ps1, CycloneDX 1.5)
- [x] CVE 스캔 CI/CD 통합됨 (.github/workflows/sbom.yml, OSV-Scanner)
- [x] STRIDE 위협 분석 완료됨 (docs/security/threat-model.md 참조)
- [x] 위험 매트릭스 작성됨 (위험 점수 = 영향도 × 발생 가능성 - 보안 제어, 4등급 체계)

---

**Document Version**: 1.1.0
**Last Updated**: 2026-03-13
**Author**: MoAI Security Team
**Implementation Status**: R1-R5 Complete | 14/15 Acceptance Criteria Met (1 infrastructure-dependent)

**Notes**:
- WORM 저장소는 운영 환경 인프라 구성이 필요하며 개발 환경에서는 시뮬레이션으로 대체
- 모든 코드 수준 보안 요구사항은 구현 완료
