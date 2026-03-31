# HnVue Console 사이버보안 규정 준수 계획서

**의료기기 소프트웨어 | IEC 62304 Class B/C | C# WPF gRPC IPC**

---

## 문서 정보

| 항목 | 내용 |
|------|------|
| 프로젝트명 | HnVue Console - Medical X-ray GUI Console Software |
| 적용 규격 | IEC 62304:2006+A1:2015 Class B/C |
| 대상 시장 | FDA (US), MFDS (Korea), CE MDR (EU) |
| 작성일 | 2026-03-12 |
| 버전 | 1.0 |

---

## 1. 규정 요구사항 요약 매트릭스

### 1.1 규제 기관별 핵심 요구사항

| 요구사항 영역 | FDA (US) | EU MDR | MFDS (Korea) | 공통 요구사항 |
|--------------|----------|--------|--------------|---------------|
| **사이버보안 위험관리** | SPDF, Section 524B FD&C Act | MDCG 2019-16, MDR Annex I | 의료기기 사이버보안 가이드라인 | 위험 기반 접근, 위협 모델링 |
| **취약점 관리** | 사전/사후 시장 취약점 관리 | 사후 시장 감시 (PMS) | 시판 후 조사 | SBOM, 패치 관리 체계 |
| **인증/인가** | 다중 요소 인증 권장 | IEC 81001-5-1 | RBAC 요구 | 역할 기반 접근 제어 |
| **데이터 보호** | HIPAA (환자 데이터) | GDPR | 개인정보보호법 | 암호화, 접근 로그 |
| **감사 추적** | 21 CFR Part 11 | MDR Article 25 | 의료기기법 | 무결성 있는 감사 로그 |
| **업데이트 관리** | Secure Updatability | MDR Article 10(4) | 소프트웨어 변경 관리 | 서명된 업데이트, 무결성 검증 |

### 1.2 FDA Section 524B Cypher Device 요구사항 (필수)

| 요구사항 | 설명 | HnVue 적용 상태 |
|----------|------|-----------------|
| 사이버보안 기능 | 무단 액세스 방지 | UserService RBAC 구현 필요 |
| 취약점 식별 | 위협 모델링 수행 | 본 문서에서 정의 |
| 소프트웨어 BOM | SBOM 유지 관리 | 생성 필요 |
| 업데이트/패치 | 인증된 업데이트 메커니즘 | 설계 필요 |
| 사후 시장 관리 | 취약점 공시 및 대응 | 프로세스 수립 필요 |

### 1.3 EU MDR MDCG 2019-16 핵심 요구사항

| 요구사항 | 설명 | HnVue 적용 상태 |
|----------|------|-----------------|
| IT 보안 위험 평가 | ISO 14971 연계 | 위험 평가 문서 필요 |
| 최신 보안 상태 유지 | 정기 보안 업데이트 | 프로세스 수립 필요 |
| 사이버보안 사고 대응 | 사고 보고 체계 | IRP (Incident Response Plan) 필요 |
| 사용자 보안 교육 | 사용 설명서에 보안 지침 포함 | 문서화 필요 |

---

## 2. 표준 매핑

### 2.1 핵심 표준 및 가이던스

| 표준 | 목적 | HnVue 적용 범위 |
|------|------|-----------------|
| **IEC 62304** | 의료기기 소프트웨어 수명주기 | 전체 개발 프로세스 |
| **ISO 14971** | 의료기기 위험관리 | 위험 평가, 위협 모델링 |
| **AAMI TIR57** | 의료기기 보안 위험관리 | 보안 특화 위험 분석 |
| **ANSI/AAMI SW96** | 의료기기 보안 위험관리 표준 | 위협 모델링 프레임워크 |
| **IEC 81001-5-1** | 의료기기 IT 보안 기능 | 보안 제어 구현 |
| **NIST CSF 2.0** | 사이버보안 프레임워크 | 보안 통제 체계 |
| **FDA SPDF** | Secure Product Development Framework | 개발 수명주기 보안 |

### 2.2 IEC 62304 Class B/C 보안 요구사항 매핑

| IEC 62304 조항 | 보안 적용 | HnVue 구현 사항 |
|----------------|----------|-----------------|
| 5.1 소프트웨어 개발 계획 | 보안 요구사항 포함 | SPEC 문서에 보안 요건 명시 |
| 5.2 소프트웨어 요구사항 분석 | 보안 요구사항 도출 | 위협 모델링 기반 요구사항 |
| 5.3 소프트웨어 아키텍처 | 보안 아키텍처 설계 | gRPC TLS, 인증/인가 레이어 |
| 5.4 소프트웨어 상세 설계 | 보안 제어 설계 | 암호화, 감사 로그 설계 |
| 5.5 소프트웨어 단위 테스트 | 보안 테스트 케이스 | 침투 테스트, 취약점 스캔 |
| 5.6 소프트웨어 통합 테스트 | 보안 통합 테스트 | E2E 보안 시나리오 |
| 5.7 소프트웨어 검증 | 보안 검증 | OWASP ZAP, 정적 분석 |

### 2.3 NIST CSF 2.0 기능 매핑

| NIST CSF 기능 | 하위 카테고리 | HnVue 적용 |
|---------------|---------------|------------|
| **GOVERN (GV)** | GV.OC, GV.RM, GV.PO | 보안 거버넌스, 위험 관리 정책 |
| **IDENTIFY (ID)** | ID.AM, ID.RA, ID.IM | 자산 식별, 위험 평가, 위협 모델링 |
| **PROTECT (PR)** | PR.AA, PR.AT, PR.DS | 인증/인가, 교육, 데이터 보호 |
| **DETECT (DE)** | DE.CM, DE.DP | 보안 모니터링, 침입 탐지 |
| **RESPOND (RS)** | RS.MA, RS.AN, RS.CO | 사고 대응, 분석, 커뮤니케이션 |
| **RECOVER (RC)** | RC.RP, RC.CO | 복구 계획, 커뮤니케이션 |

---

## 3. 구현 계획

### 3.1 보안 거버넌스

#### 3.1.1 조직 및 책임

| 역할 | 책임 | 담당 |
|------|------|------|
| 보안 책임자 | 보안 정책 수립, 규정 준수 감독 | 품질관리팀 |
| 개발 보안 담당 | 보안 코딩 가이드, 코드 리뷰 | 개발팀 |
| 위험 관리 담당 | 위협 모델링, 위험 평가 | 품질관리팀 |
| 사고 대응 담당 | 보안 사고 대응, 공시 | 운영팀 |

#### 3.1.2 보안 정책 문서

| 문서 | 목적 | 작성 시기 |
|------|------|-----------|
| 보안 정책서 (Security Policy) | 전사 보안 지침 | Phase 1 |
| 소프트웨어 보안 코딩 가이드 | 개발자 보안 코딩 표준 | Phase 1 |
| 취약점 관리 절차서 | 취약점 식별/수정 프로세스 | Phase 1 |
| 사고 대응 계획서 (IRP) | 보안 사고 대응 절차 | Phase 2 |
| SBOM 관리 절차서 | 컴포넌트 추적 관리 | Phase 1 |

### 3.2 보안 아키텍처

#### 3.2.1 시스템 아키텍처 보안 뷰

```
+------------------+     TLS 1.3+      +------------------+
|   HnVue Console  | <===============> |   Core Engine    |
|   (C# WPF GUI)   |     gRPC IPC      |   (C++ Backend)  |
+------------------+                   +------------------+
        |                                      |
        v                                      v
+------------------+                   +------------------+
|  User Auth (RBAC)|                   |   Hardware HAL   |
|  UserService     |                   |   Detector/HVG   |
+------------------+                   +------------------+
        |
        v
+------------------+
|  Audit Logging   |
|  AuditLogService |
+------------------+
```

#### 3.2.2 gRPC IPC 보안 요구사항

| 보안 제어 | 요구사항 | 구현 상태 |
|-----------|----------|-----------|
| 전송 보안 | TLS 1.3+ 필수 | 구현 필요 |
| 상호 인증 | mTLS (Mutual TLS) | 설계 필요 |
| 인증 토큰 | 세션 기반 토큰 | UserService proto 정의됨 |
| 메시지 무결성 | TLS 기본 제공 | TLS 구현 시 확보 |
| 재전송 방지 | 타임스탬프 기반 | proto Timestamp 정의됨 |

#### 3.2.3 데이터 흐름 보안

```
[User Input] --> [Input Validation] --> [Sanitization] --> [Business Logic]
                                                            |
                                                            v
[Database] <-- [Encrypted Storage] <-- [Data Classification]
                                                            |
                                                            v
[Audit Log] <-- [Security Event] <-- [Access Control Check]
```

### 3.3 기술적 보안 제어

#### 3.3.1 인증 (Authentication)

| 제어 항목 | 요구사항 | 구현 가이드 |
|-----------|----------|-------------|
| 비밀번호 정책 | 최소 12자, 복잡성 요구 | ChangePasswordRequest proto 검증 |
| 계정 잠금 | 5회 실패 시 잠금 | UserService 구현 필요 |
| 세션 관리 | 타임아웃 30분, 재인증 | UserSession expires_at 활용 |
| 다중 요소 인증 | 권장 (P2) | 향후 OTP/TOTP 지원 |

**Proto 기반 인증 구현 가이드:**

```protobuf
// hnvue_user.proto 기반
message AuthenticateRequest {
  string username = 1;
  string password = 2;              // 클라이언트 사전 해시 권장
  string workstation_id = 3;        // 워크스테이션 식별
  Timestamp request_timestamp = 4;   // 재전송 방지
}
```

**보안 요구사항:**
- [ ] 비밀번호: bcrypt/Argon2id 해시 (평문 저장 금지)
- [ ] 세션 토큰: 암호학적 난수 생성 (secrets.token_hex 유사)
- [ ] 세션 만료: 절대 시간 기반 (UserSession.expires_at)

#### 3.3.2 인가 (Authorization)

| 제어 항목 | 요구사항 | 구현 가이드 |
|-----------|----------|-------------|
| 역할 기반 접근 제어 | 최소 권한 원칙 | UserRole enum 활용 |
| 권한 검증 | 모든 민감 작업에 권한 확인 | Permission 메시지 활용 |
| 관리자 기능 분리 | 별도 권한 그룹 | USER_ROLE_ADMINISTRATOR |

**UserRole 정의 (proto):**

```protobuf
enum UserRole {
  USER_ROLE_UNSPECIFIED = 0;
  USER_ROLE_ADMINISTRATOR = 1;      // 전체 시스템 접근
  USER_ROLE_RADIOLOGIST = 2;        // 의사 - 리포트 서명
  USER_ROLE_TECHNOLOGIST = 3;       // 방사선사
  USER_ROLE_PHYSICIST = 4;          // 의학물리학자
  USER_ROLE_OPERATOR = 5;           // 기본 운영자
  USER_ROLE_VIEWER = 6;             // 읽기 전용
  USER_ROLE_SERVICE = 7;            // 유지보수 엔지니어
}
```

**권한 매트릭스:**

| 기능 | Admin | Radiologist | Technologist | Operator | Viewer |
|------|-------|-------------|--------------|----------|--------|
| 노출 실행 | X | O | O | O | X |
| 환자 조회 | O | O | O | R | R |
| 설정 변경 | O | X | X | X | X |
| QC 수행 | O | O | O | X | X |
| 감사 로그 조회 | O | R | X | X | X |

#### 3.3.3 암호화 (Cryptography)

| 제어 항목 | 요구사항 | 구현 가이드 |
|-----------|----------|-------------|
| 전송 암호화 | TLS 1.3+ 필수 | gRPC channel 구성 |
| 저장 데이터 암호화 | AES-256-GCM | 민감 데이터 암호화 |
| 비밀번호 해시 | Argon2id 또는 bcrypt | 비밀번호 저장 |
| 키 관리 | HSM 또는 안전한 키 저장소 | 키 회전 정책 |

**.NET 8 암호화 구현 예시:**

```csharp
// 비밀번호 해시 (Argon2id 권장)
using var argon2 = new Argon2id(passwordBytes);
argon2.Salt = salt;
argon2.Iterations = 3;
argon2.MemorySize = 65536; // 64 MB
argon2.DegreeOfParallelism = 4;
byte[] hash = await argon2.GetBytesAsync(32);
```

#### 3.3.4 감사 로깅 (Audit Logging)

**AuditLogService Proto 기반 감사 이벤트:**

| 이벤트 유형 | AuditEventType | 보안 중요도 |
|-------------|----------------|-------------|
| 사용자 로그인 | USER_LOGIN | INFO |
| 로그인 실패 | USER_LOGIN_FAILED | WARNING |
| 권한 거부 | ACCESS_DENIED | WARNING |
| 노출 실행 | EXPOSURE_STARTED | INFO |
| 설정 변경 | CONFIGURATION_CHANGED | WARNING |
| 데이터 내보내기 | DATA_EXPORT | INFO |

**감사 로그 보안 요구사항:**
- [ ] 무결성: 로그 변조 방지 (서명 또는 WORM 저장)
- [ ] 기밀성: 민감 정보 마스킹
- [ ] 가용성: 최소 6년 보관 (의료기기 규정)
- [ ] 타임스탬프: 정확한 시간 기록 (NTP 동기화)

#### 3.3.5 입력 검증 (Input Validation)

| 입력 소스 | 검증 항목 | 구현 위치 |
|-----------|----------|-----------|
| UI 입력 | 길이, 형식, 범위 | ViewModel |
| gRPC 요청 | Proto 제약조건 | Service Adapter |
| 환자 데이터 | DICOM 규격 준수 | DicomService |
| 설정 값 | 허용된 값 범위 | ConfigService |

**OWASP Top 10 대응:**

| OWASP 항목 | HnVue 대응 |
|------------|------------|
| A01:2021 - Broken Access Control | UserRole 기반 RBAC |
| A02:2021 - Cryptographic Failures | TLS 1.3+, AES-256 |
| A03:2021 - Injection | Parameterized queries, 입력 검증 |
| A04:2021 - Insecure Design | 위협 모델링 기반 설계 |
| A05:2021 - Security Misconfiguration | 보안 설정 가이드 |
| A06:2021 - Vulnerable Components | SBOM, 정기 스캔 |
| A07:2021 - Authentication Failures | MFA, 세션 관리 |
| A08:2021 - Software Integrity | 서명된 업데이트 |
| A09:2021 - Logging Failures | AuditLogService |
| A10:2021 - SSRF | 내부 네트워크 격리 |

### 3.4 네트워크 보안

#### 3.4.1 네트워크 분리

```
+-----------------+     +-----------------+     +-----------------+
|   Workstation   |     |   Core Engine   |     |   PACS/DICOM    |
|   (GUI Client)  |     |   (Backend)     |     |   (External)    |
+-----------------+     +-----------------+     +-----------------+
         |                      |                      |
         +---- Internal LAN ----+---- DMZ (Optional)---+
                       |
               +-------+-------+
               |   Firewall    |
               +---------------+
```

#### 3.4.2 네트워크 보안 요구사항

| 제어 항목 | 요구사항 | 구현 |
|-----------|----------|------|
| 포트 제한 | 필요한 포트만 개방 | gRPC (50051), DICOM (104) |
| 방화벽 | 호스트 기반 방화벽 | Windows Firewall 구성 |
| 서비스 분리 | GUI와 백엔드 분리 | gRPC IPC 구조 |
| 외부 연결 | 최소화, VPN 권장 | PACS 연결 시 TLS |

---

## 4. 테스트 및 검증 계획

### 4.1 보안 테스트 전략

| 테스트 유형 | 목적 | 도구 | 시기 |
|-------------|------|------|------|
| 정적 분석 (SAST) | 소스 코드 취약점 | SonarQube, Roslyn Analyzers | CI/CD |
| 동적 분석 (DAST) | 런타임 취약점 | OWASP ZAP, Burp Suite | 스프린트 종료 |
| 의존성 스캔 | 취약한 라이브러리 | NuGet Audit, Snyk | CI/CD |
| 컨테이너 스캔 | 이미지 취약점 | Trivy | 빌드 시 |
| 퍼즈 테스트 | 입력 검증 | AFL, libFuzzer | 주기적 |
| 침투 테스트 | 실제 공격 시뮬레이션 | 수동 + 자동 | 시판 전 |

### 4.2 정적 코드 분석

**.NET Roslyn Analyzers 구성:**

```xml
<!-- .csproj -->
<PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" />
<PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7" />
```

**분석 규칙 활성화:**

| 규칙 범주 | 규칙 ID | 설명 |
|-----------|---------|------|
| Security | CA2100 | SQL injection 검토 |
| Security | CA2300/2301 | 역직렬화 취약점 |
| Security | CA5350/5351 | 약한 암호화 알고리즘 |
| Security | CA3075 | XML 처리 취약점 |

### 4.3 동적 보안 테스트

**OWASP ZAP 스캔 구성:**

```yaml
# zap-scan.yaml
scanners:
  - name: "HnVue Console gRPC"
    target: "localhost:50051"
    scan_type: "grpc"
    authentication:
      type: "token"
      token_endpoint: "/UserService/Authenticate"
    excluded_paths:
      - "/HealthService/*"
```

### 4.4 취약점 스캔 프로세스

```
+------------+    +------------+    +------------+    +------------+
|   Code     | -> |   SAST     | -> |   Review   | -> |   Fix      |
|   Commit   |    |   Scan     |    |   Results  |    |   Issues   |
+------------+    +------------+    +------------+    +------------+
                         |
                         v
                  +------------+
                  |   CVE      |
                  |   DB       |
                  +------------+
```

**취약점 심각도 분류:**

| 심각도 | CVSS 점수 | 대응 기한 | 예시 |
|--------|-----------|-----------|------|
| Critical | 9.0-10.0 | 24시간 | RCE, 인증 우회 |
| High | 7.0-8.9 | 7일 | SQL Injection, XSS |
| Medium | 4.0-6.9 | 30일 | 정보 노출 |
| Low | 0.1-3.9 | 다음 릴리스 | 경미한 정보 노출 |

### 4.5 침투 테스트 체크리스트

| 테스트 항목 | 공격 벡터 | 예상 방어 |
|-------------|-----------|-----------|
| 인증 우회 | 세션 탈취, 토큰 변조 | 세션 검증, 토큰 서명 |
| 권한 상승 | Role manipulation | 서버 측 권한 검증 |
| SQL Injection | PatientService 쿼리 | Parameterized queries |
| 명령 주입 | ConfigService 입력 | 입력 검증, 화이트리스트 |
| 버퍼 오버플로우 | gRPC 메시지 | Proto 크기 제한 |
| 정보 노출 | 에러 메시지 | 일반화된 에러 응답 |
| 재전송 공격 | 요청 재사용 | 타임스탬프 검증 |

---

## 5. 시판 전 최종 테스트 계획

### 5.1 Pre-market Security Checklist

#### 5.1.1 FDA 제출 준비

| 항목 | 요구사항 | 상태 | 비고 |
|------|----------|------|------|
| Security Risk Assessment | 위협 모델링 완료 | [ ] | AAMI TIR57 기반 |
| Security Architecture | 보안 아키텍처 문서 | [ ] | FDA 가이던스 뷰 포함 |
| SBOM | 컴포넌트 목록 | [ ] | NTIA 최소 요소 준수 |
| Vulnerability Testing | 테스트 결과 | [ ] | SAST/DAST/Fuzz |
| Penetration Testing | 독립적 테스트 | [ ] | 제3자 권장 |
| Threat Modeling Report | STRIDE 기반 | [ ] | 본 문서 포함 |
| Security Update Plan | 패치 관리 계획 | [ ] | Section 524B 준수 |

#### 5.1.2 EU MDR 준비

| 항목 | 요구사항 | 상태 | 비고 |
|------|----------|------|------|
| Technical File | 보안 관련 기술 문서 | [ ] | Annex II, III |
| Clinical Evaluation | 보안 관련 임상 평가 | [ ] | MDCG 2019-16 |
| PMS Plan | 사후 시장 감시 계획 | [ ] | 보안 사고 포함 |
| IFU Security Instructions | 사용자 보안 지침 | [ ] | 사용 설명서 포함 |
| UDI | Unique Device Identification | [ ] | GS1 또는 HIBC |

#### 5.1.3 MFDS (Korea) 준비

| 항목 | 요구사항 | 상태 | 비고 |
|------|----------|------|------|
| 사이버보안 기술문서 | 품목허가용 기술문서 | [ ] | MFDS 가이드라인 준수 |
| 위험관리 보고서 | ISO 14971 기반 | [ ] | 보안 위험 포함 |
| 소프트웨어 검증보고서 | IEC 62304 기반 | [ ] | 보안 테스트 포함 |
| SBOM | 컴포넌트 목록 | [ ] | 국문 작성 |

### 5.2 보안 테스트 실행 계획

| 단계 | 활동 | 담당 | 기간 | 산출물 |
|------|------|------|------|--------|
| 1 | 위협 모델링 업데이트 | 보안 담당 | 1주 | TM 문서 v2 |
| 2 | SAST/DAST 스캔 | 개발팀 | 2주 | 스캔 보고서 |
| 3 | 취약점 수정 | 개발팀 | 2주 | 수정 기록 |
| 4 | 재스캔 | 보안 담당 | 1주 | 최종 스캔 보고서 |
| 5 | 침투 테스트 (외부) | 제3자 | 2주 | PT 보고서 |
| 6 | 최종 검증 | 품질팀 | 1주 | 검증 보고서 |

### 5.3 최종 검증 기준

| 기준 | 통과 조건 |
|------|-----------|
| Critical 취약점 | 0개 |
| High 취약점 | 0개 |
| Medium 취약점 | < 5개 (문서화된 완화책) |
| 코드 커버리지 | > 85% |
| 보안 테스트 케이스 | 100% 통과 |

---

## 6. 프로젝트별 구현 로드맵

### 6.1 HnVue Console 보안 구현 현황

| 구성 요소 | 구현 상태 | 보안 요구사항 | 우선순위 |
|-----------|-----------|---------------|----------|
| **gRPC IPC** | Proto 정의 완료 | TLS 1.3+, mTLS | P1 |
| **UserService** | Proto 정의됨, 구현 필요 | RBAC, 세션 관리 | P1 |
| **AuditLogService** | Proto 정의됨, 구현 필요 | 무결성, 보관 정책 | P1 |
| **DoseService** | Proto 정의됨, 구현 필요 | 데이터 무결성 | P1 |
| **ImageService** | Proto 정의됨, 어댑터 Stub | 무결성, 기밀성 | P2 |
| **ConfigService** | Partial 구현 | 변경 감사 | P2 |
| **NetworkService** | Partial 구현 | TLS, 인증서 관리 | P2 |

### 6.2 Phase별 구현 계획

#### Phase 1: 보안 기반 구축 (MVP + 2주)

| 작업 | 산출물 | 담당 |
|------|--------|------|
| 위협 모델링 (STRIDE) | 위협 모델 문서 | 보안 담당 |
| SBOM 생성 | components.json | 개발팀 |
| gRPC TLS 구성 | TLS 구성 가이드 | 개발팀 |
| UserService 보안 구현 | 인증/인가 모듈 | 개발팀 |
| AuditLogService 구현 | 감사 로그 모듈 | 개발팀 |

#### Phase 2: 보안 강화 (MVP + 4주)

| 작업 | 산출물 | 담당 |
|------|--------|------|
| SAST/DAST 파이프라인 | CI/CD 보안 스캔 | DevOps |
| 침투 테스트 | PT 보고서 | 외부 업체 |
| 보안 문서화 | STR, 보안 가이드 | 품질팀 |
| mTLS 구현 | 상호 인증 구성 | 개발팀 |

#### Phase 3: 규정 준수 완료 (시판 전)

| 작업 | 산출물 | 담당 |
|------|--------|------|
| FDA 규정 준수 검토 | 제출 문서 패키지 | 규정 담당 |
| EU MDR 검토 | Technical File | 규정 담당 |
| MFDS 검토 | 품목허가 문서 | 규정 담당 |
| 최종 보안 감사 | 감사 보고서 | 품질팀 |

### 6.3 Stub 어댑터 보안 구현 로드맵

현재 9개 Stub 어댑터에 대한 보안 요구사항:

| 어댑터 | 보안 제어 | Proto 보안 기능 |
|--------|-----------|-----------------|
| ImageService | 무결성 검증 | 해시, 서명 |
| PatientService | 데이터 보호 | 암호화, 접근 제어 |
| WorklistService | 인증 | 세션 토큰 |
| UserService | 인증/인가 | RBAC, 세션 관리 |
| DoseService | 무결성 | 감사 로그 |
| AECService | 무결성 | 상태 검증 |
| ProtocolService | 무결성 | 서명된 프로토콜 |
| AuditLogService | 무결성 | WORM, 서명 |
| QCService | 무결성 | 감사 로그 |

---

## 7. 문서 요구사항

### 7.1 Software Test Report (STR) 템플릿

```markdown
# Software Test Report - HnVue Console

## 1. 문서 정보
- 프로젝트명: HnVue Console
- 버전: X.Y.Z
- 테스트 일자: YYYY-MM-DD
- 담당자: [이름]

## 2. 테스트 범위
- 단위 테스트: [ ] 통과
- 통합 테스트: [ ] 통과
- 보안 테스트: [ ] 통과
- E2E 테스트: [ ] 통과

## 3. 보안 테스트 결과

### 3.1 SAST 결과
| 규칙 ID | 심각도 | 설명 | 상태 |
|---------|--------|------|------|
| CA2100 | Warning | SQL Injection 위험 | 수정됨 |

### 3.2 DAST 결과
| 취약점 | CVSS | 상태 | 비고 |
|--------|------|------|------|
| - | - | - | - |

### 3.3 의존성 스캔
| 패키지 | CVE | CVSS | 상태 |
|--------|-----|------|------|
| - | - | - | - |

## 4. 테스트 요약
- 총 테스트 케이스: [N]
- 통과: [N]
- 실패: [N]
- 스킵: [N]

## 5. 승인
- 테스트 담당자: [서명] [날짜]
- 검토자: [서명] [날짜]
```

### 7.2 SBOM (Software Bill of Materials) 템플릿

```json
{
  "sbom": {
    "specVersion": "1.5",
    "serialNumber": "urn:uuid:...",
    "version": 1,
    "metadata": {
      "timestamp": "2026-03-12T00:00:00Z",
      "component": {
        "type": "application",
        "name": "HnVue Console",
        "version": "1.0.0",
        "supplier": "HnVue Medical"
      }
    },
    "components": [
      {
        "type": "library",
        "name": "Google.Protobuf",
        "version": "3.28.3",
        "purl": "pkg:nuget/Google.Protobuf@3.28.3",
        "licenses": [{"license": {"id": "BSD-3-Clause"}}]
      }
    ],
    "dependencies": [...],
    "vulnerabilities": [...]
  }
}
```

### 7.3 위험 평가 템플릿

```markdown
# Security Risk Assessment - HnVue Console

## 1. 위협 모델 (STRIDE)

| 위협 유형 | 자산 | 공격 벡터 | 영향 | 완화책 |
|-----------|------|-----------|------|--------|
| Spoofing | 사용자 세션 | 세션 탈취 | 높음 | mTLS, 세션 타임아웃 |
| Tampering | 환자 데이터 | DB 변조 | 높음 | 감사 로그, 무결성 검증 |
| Repudiation | 감사 로그 | 로그 삭제 | 중간 | WORM 저장, 서명 |
| Info Disclosure | 환자 정보 | 권한 없는 접근 | 높음 | RBAC, 암호화 |
| Denial of Service | gRPC 서비스 | 플러드 공격 | 중간 | 속도 제한, 타임아웃 |
| Elevation of Privilege | 권한 상승 | Role 조작 | 높음 | 서버 측 권한 검증 |

## 2. 위험 점수 매트릭스

| 위험 | 가능성 | 영향 | 점수 | 우선순위 |
|------|--------|------|------|----------|
| 인증 우회 | 낮음 | 높음 | 6 | 높음 |
| 데이터 유출 | 중간 | 높음 | 8 | 높음 |
| 서비스 거부 | 낮음 | 중간 | 3 | 중간 |

## 3. 완화 계획
- [ ] mTLS 구현
- [ ] RBAC 강화
- [ ] 감사 로그 서명
- [ ] 속도 제한 구현
```

### 7.4 사고 대응 계획 (IRP) 템플릿

```markdown
# Security Incident Response Plan

## 1. 사고 분류

| 등급 | 정의 | 대응 시간 | 공시 기한 |
|------|------|-----------|-----------|
| Critical | 환자 안전 위협 | 1시간 | 24시간 |
| High | 데이터 유출 | 4시간 | 72시간 |
| Medium | 서비스 중단 | 24시간 | 7일 |
| Low | 잠재적 취약점 | 72시간 | 30일 |

## 2. 대응 절차

1. **탐지**: 모니터링, 보고
2. **분석**: 영향 평가, 분류
3. **격리**: 피해 확산 방지
4. **제거**: 원인 제거
5. **복구**: 정상화
6. **사후 분석**: 재발 방지

## 3. 연락망

| 역할 | 담당자 | 연락처 |
|------|--------|--------|
| 보안 담당 | [이름] | [전화] |
| 개발 리드 | [이름] | [전화] |
| 규정 담당 | [이름] | [전화] |

## 4. 규제 공시 요구사항

| 규제 기관 | 공시 기한 | 공시 방법 |
|-----------|-----------|-----------|
| FDA | 30일 (Critical) | eSubmitter |
| EU (NB) | 즉시 | NB 포털 |
| MFDS | 7일 | 의료기기 종합정보 |
```

---

## 8. 부록

### 8.1 참조 문서

| 문서 | 출처 | 버전 |
|------|------|------|
| FDA Cybersecurity Guidance | FDA.gov | 2026-02 |
| MDCG 2019-16 | EU Commission | 2019 |
| IEC 62304 | IEC | 2006+A1:2015 |
| ISO 14971 | ISO | 2019 |
| AAMI TIR57 | AAMI | 2019 |
| NIST CSF | NIST | 2.0 (2024) |
| IEC 81001-5-1 | IEC | 2021 |

### 8.2 용어 정의

| 용어 | 정의 |
|------|------|
| SPDF | Secure Product Development Framework |
| SBOM | Software Bill of Materials |
| STRIDE | Spoofing, Tampering, Repudiation, Info Disclosure, DoS, EoP |
| mTLS | Mutual TLS (상호 인증) |
| RBAC | Role-Based Access Control |
| SAST | Static Application Security Testing |
| DAST | Dynamic Application Security Testing |

### 8.3 변경 이력

| 버전 | 날짜 | 변경 내용 | 작성자 |
|------|------|-----------|--------|
| 1.0 | 2026-03-12 | 최초 작성 | MoAI Security Expert |

---

*본 문서는 HnVue Console 프로젝트의 사이버보안 규정 준수를 위한 계획서입니다. FDA, EU MDR, MFDS 규정 준수를 위한 실천 가능한 가이드를 제공합니다.*
