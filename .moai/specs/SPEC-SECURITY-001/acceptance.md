# SPEC-SECURITY-001: 인수 기준

## Metadata

| Field    | Value                                              |
|----------|----------------------------------------------------|
| SPEC ID  | SPEC-SECURITY-001                                  |
| Title    | HnVue 의료기기 사이버보안 규정 준수                  |
| Format   | Given-When-Then (Gherkin-style)                    |

---

## Definition of Done

요구사항은 다음 조건이 모두 충족될 때 완료로 간주됩니다:

1. 해당 요구사항의 모든 인수 시나리오가 자동화된 테스트 스위트에서 통과함
2. 구현이 추적성 매트릭스에서 본 SPEC으로 추적 가능함
3. IEC 62304 Class B/C 단위 테스트 문서가 해당 컴포넌트에 존재함
4. PHI(환자 ID, 환자 이름)가 INFO 레벨 로그에 평문으로 표시되지 않음
5. 보안 모듈의 라인 커버리지가 90% 이상임
6. SAST/DAST 스캔에서 Critical/High 취약점이 0건임

---

## AC-01: RBAC (역할 기반 접근 제어) - FR-SEC-01

### Scenario 1.1 - 역할별 권한 부여

```
Given 유효한 사용자 세션이 존재하고
  And 사용자의 역할이 RADIOLOGIST로 설정됨
When 사용자가 보고서 서명 기능에 접근하면
Then 시스템은 접근을 허용한다
  And AuditLog에 ACCESS_GRANTED 이벤트가 기록된다
```

### Scenario 1.2 - 권한 없는 접근 거부

```
Given 유효한 사용자 세션이 존재하고
  And 사용자의 역할이 OPERATOR로 설정됨
When 사용자가 관리자 설정 변경 기능에 접근하면
Then 시스템은 ACCESS_DENIED 오류를 반환한다
  And AuditLog에 ACCESS_DENIED 이벤트가 기록된다
```

### Scenario 1.3 - 만료된 세션 접근 거부

```
Given 세션이 30분 전에 생성되었고
  And 마지막 활동이 30분 이상 경과함
When 사용자가 보호된 리소스에 접근하면
Then 시스템은 SESSION_EXPIRED 오류를 반환한다
  And 사용자는 재인증을 요청받는다
```

---

## AC-02: 세션 타임아웃 - FR-SEC-02

### Scenario 2.1 - 비활성 세션 자동 만료

```
Given 사용자가 로그인하여 세션이 생성됨
  And 마지막 활동으로부터 30분이 경과함
When ValidateSession RPC가 호출되면
Then is_valid는 false를 반환한다
  And 세션은 서버에서 제거된다
  And AuditLog에 USER_LOGOUT 이벤트가 기록된다
```

### Scenario 2.2 - 활성 세션 연장

```
Given 사용자가 로그인하여 세션이 생성됨
  And 마지막 활동으로부터 25분이 경과함
When 사용자가 시스템 작업을 수행하면
Then 세션 만료 시간이 현재 시점으로부터 30분으로 연장된다
```

### Scenario 2.3 - 세션 만료 알림

```
Given 사용자 세션이 존재하고
  And 세션이 25분 경과 상태임
When UI가 세션 상태를 확인하면
Then 시스템은 5분 내 만료 예정 알림을 표시한다
```

---

## AC-03: 계정 잠금 - FR-SEC-03

### Scenario 3.1 - 5회 실패 후 계정 잠금

```
Given 사용자 계정이 활성 상태임
  And 현재 실패 횟수가 4회임
When 잘못된 비밀번호로 로그인을 시도하면
Then 시스템은 계정을 30분간 잠금한다
  And ACCOUNT_LOCKED 이벤트가 AuditLog에 기록된다
  And 로그인 응답은 "계정이 잠겼습니다" 메시지를 반환한다
```

### Scenario 3.2 - 잠금 해제

```
Given 사용자 계정이 잠금 상태임
  And 잠금 시점으로부터 30분이 경과함
When 올바른 비밀번호로 로그인을 시도하면
Then 시스템은 로그인을 허용한다
  And 실패 횟수는 0으로 초기화된다
```

### Scenario 3.3 - 관리자 수동 잠금 해제

```
Given 사용자 계정이 잠금 상태임
  And 관리자가 세션을 가지고 있음
When 관리자가 계정 잠금 해제를 수행하면
Then 계정은 즉시 활성 상태로 변경된다
  And ACCOUNT_UNLOCKED 이벤트가 AuditLog에 기록된다
```

---

## AC-04: 비밀번호 복잡성 - FR-SEC-04

### Scenario 4.1 - 최소 길이 요구사항

```
Given 사용자가 비밀번호 변경을 시도함
When 새 비밀번호가 12자 미만인 경우
Then 시스템은 비밀번호 변경을 거부한다
  And "비밀번호는 최소 12자 이상이어야 합니다" 오류를 반환한다
```

### Scenario 4.2 - 복잡성 요구사항

```
Given 사용자가 비밀번호 변경을 시도함
When 새 비밀번호가 다음 조건을 만족하지 않음:
  - 대문자 1개 이상
  - 소문자 1개 이상
  - 숫자 1개 이상
  - 특수문자 1개 이상
Then 시스템은 비밀번호 변경을 거부한다
  And 복잡성 요구사항 오류 메시지를 반환한다
```

### Scenario 4.3 - 이전 비밀번호 재사용 방지

```
Given 사용자가 비밀번호 변경을 시도함
When 새 비밀번호가 최근 10개 비밀번호 중 하나와 일치함
Then 시스템은 비밀번호 변경을 거부한다
  And "이전에 사용한 비밀번호는 재사용할 수 없습니다" 오류를 반환한다
```

---

## AC-05: 감사로그 무결성 - FR-SEC-06

### Scenario 5.1 - SHA-256 서명 생성

```
Given 새 감사 이벤트가 발생함
When AuditLogService.LogEvent가 호출되면
Then 로그 항목에 SHA-256 해시 서명이 포함된다
  And 서명은 이전 로그 항목의 해시를 포함하는 체인 구조를 형성한다
```

### Scenario 5.2 - 로그 무결성 검증

```
Given 100개의 감사 로그 항목이 존재함
When 무결성 검증이 수행되면
Then 시스템은 모든 해시 체인이 유효함을 확인한다
  And 변조된 항목이 없으면 "무결성 검증 통과"를 반환한다
```

### Scenario 5.3 - 로그 변조 감지

```
Given 감사 로그 항목이 존재함
When 로그 항목의 내용이 외부에서 수정됨
Then 무결성 검증 시 해당 항목에서 검증 실패가 감지된다
  And 변조된 항목 ID가 보고된다
```

---

## AC-06: WORM 저장소 - FR-SEC-07

### Scenario 6.1 - 로그 수정 방지

```
Given 감사 로그가 WORM 저장소에 저장됨
When 로그 항목을 수정하려는 시도가 발생함
Then 시스템은 쓰기 작업을 거부한다
  And "읽기 전용 저장소" 오류를 반환한다
```

### Scenario 6.2 - 로그 삭제 방지

```
Given 감사 로그가 WORM 저장소에 저장됨
When 로그 항목을 삭제하려는 시도가 발생함
Then 시스템은 삭제 작업을 거부한다
  And 보안 이벤트가 기록된다
```

---

## AC-07: 감사로그 보관 기간 - FR-SEC-07

### Scenario 7.1 - 자동 보관

```
Given 감사 로그 항목이 생성됨
When 시스템이 정상적으로 운영됨
Then 로그 항목은 최소 6년간 보관된다
  And 보관 기간은 법적 요구사항에 따라 연장될 수 있다
```

### Scenario 7.2 - 기간 만료 로그 처리

```
Given 감사 로그 항목이 6년 이상 보관됨
  And 법적 보관 의무가 만료됨
When 자동 정리 작업이 실행됨
Then 시스템은 만료된 로그를 안전하게 삭제한다
  And 삭제 작업이 AuditLog에 기록된다
```

---

## AC-08: NTP 타임스탬프 - FR-SEC-08

### Scenario 8.1 - UTC 타임스탬프 저장

```
Given 감사 이벤트가 발생함
When 로그 항목이 생성됨
Then event_timestamp는 UTC로 저장된다
  And 타임스탬프는 ISO 8601 형식을 따른다
```

### Scenario 8.2 - NTP 동기화 검증

```
Given 시스템이 NTP 서버와 동기화됨
When 타임스탬프가 생성됨
Then 로컬 시간과 NTP 서버 시간의 차이가 100ms 이내이다
  And 동기화 상태가 시스템 상태에 기록된다
```

---

## AC-09: PHI 마스킹 - FR-SEC-10

### Scenario 9.1 - 환자 ID 마스킹

```
Given 환자 관련 감사 이벤트가 발생함
When 로그 항목이 생성됨
Then 환자 ID는 마스킹되어 저장된다 (예: PAT***123)
  And 원본 ID는 별도 암호화 저장소에만 보관된다
```

### Scenario 9.2 - 로그 검색 시 마스킹 유지

```
Given 마스킹된 환자 ID가 포함된 로그가 존재함
When 관리자가 로그를 조회함
Then 환자 ID는 마스킹된 상태로 표시된다
  And 권한이 있는 사용자만 원본 ID를 볼 수 있다
```

---

## AC-10: TLS 1.3+ - FR-SEC-11

### Scenario 10.1 - TLS 1.3 연결 설정

```
Given 클라이언트가 gRPC 서버에 연결함
When TLS 핸드셰이크가 수행됨
Then 연결은 TLS 1.3을 사용한다
  And 연결 정보에 TLS 버전이 기록된다
```

### Scenario 10.2 - TLS 1.2 연결 거부

```
Given 클라이언트가 TLS 1.2만 지원함
When gRPC 연결을 시도함
Then 시스템은 연결을 거부한다
  And "TLS 1.3 이상이 필요합니다" 오류를 반환한다
```

---

## AC-11: mTLS 상호 인증 - FR-SEC-12

### Scenario 11.1 - 클라이언트 인증서 검증

```
Given 클라이언트가 인증서를 가지고 있음
When mTLS 연결을 시도함
Then 서버는 클라이언트 인증서를 검증한다
  And 인증서가 유효하면 연결이 수립된다
```

### Scenario 11.2 - 인증서 없는 연결 거부

```
Given 클라이언트가 인증서를 가지고 있지 않음
When mTLS 연결을 시도함
Then 서버는 연결을 거부한다
  And 인증서 필요 오류를 반환한다
```

### Scenario 11.3 - 만료된 인증서 거부

```
Given 클라이언트 인증서가 만료됨
When mTLS 연결을 시도함
Then 서버는 연결을 거부한다
  And "인증서가 만료되었습니다" 오류를 반환한다
```

---

## AC-12: 인증서 회전 - FR-SEC-13

### Scenario 12.1 - 무중단 인증서 교체

```
Given 서비스가 실행 중임
  And 새 인증서가 배포됨
When 인증서 회전이 트리거됨
Then 서비스는 중단 없이 새 인증서를 로드한다
  And 기존 연결은 유지된다
  And 새 연결은 새 인증서를 사용한다
```

### Scenario 12.2 - 만료 예정 알림

```
Given 인증서가 30일 이내에 만료됨
When 시스템이 인증서 상태를 확인함
Then 관리자에게 만료 예정 알림이 전송된다
  And 알림에 남은 일수가 포함된다
```

---

## AC-13: SBOM 생성 - FR-SEC-15

### Scenario 13.1 - SPDX 1.5 형식 생성

```
Given 소프트웨어 빌드가 수행됨
When 빌드 파이프라인이 완료됨
Then SPDX 1.5 형식의 SBOM 파일이 생성된다
  And 파일명은 hnvue-console-sbom-{version}.spdx.json이다
```

### Scenario 13.2 - NTIA 최소 요소 포함

```
Given SBOM 파일이 생성됨
When SBOM 내용을 검증함
Then 다음 요소가 모두 포함됨:
  - Supplier: HnVue
  - Component Name: 각 NuGet 패키지명
  - Version: 버전 번호
  - Unique Identifier: purl
  - Dependency Relationship: depends-on
  - Author: Build Pipeline
  - Timestamp: 빌드 시간
```

---

## AC-14: CVE 스캔 - FR-SEC-16

### Scenario 14.1 - CI/CD CVE 스캔

```
Given 코드가 저장소에 커밋됨
When CI/CD 파이프라인이 실행됨
Then 의존성 CVE 스캔이 자동으로 수행된다
  And 결과가 빌드 보고서에 포함된다
```

### Scenario 14.2 - Critical 취약점 발견 시 차단

```
Given CVE 스캔이 수행 중임
When Critical 등급 취약점이 발견됨
Then 빌드는 실패로 처리된다
  And 취약점 상세 보고서가 생성된다
  And 관련 팀에 알림이 전송된다
```

### Scenario 14.3 - 취약점 무시 규칙

```
Given 취약점이 발견됨
  And 해당 취약점이 프로젝트에 영향이 없음이 확인됨
When 무시 규칙이 등록됨
Then 이후 스캔에서 해당 취약점은 무시된다
  And 무시 사유가 문서화된다
```

---

## AC-15: STRIDE 위협 분석 - FR-SEC-18

### Scenario 15.1 - 위협 분석 문서 존재

```
Given 프로젝트가 시작됨
When 보안 검토가 수행됨
Then STRIDE 분석 문서가 존재한다
  And 각 위협 유형별 분석이 포함된다
```

### Scenario 15.2 - 위협 완화 상태 추적

```
Given STRIDE 분석에서 위협이 식별됨
When 개발이 진행됨
Then 각 위협에 대한 완화 상태가 추적된다
  And 모든 Critical/High 위협은 완화됨 상태여야 한다
```

---

## AC-16: 위험 매트릭스 - FR-SEC-19

### Scenario 16.1 - 위험 점수 계산

```
Given 위협이 식별됨
When 위험 평가가 수행됨
Then 위험 점수가 계산된다 (가능성 × 영향)
  And 위험 등급이 결정된다 (Critical/High/Medium/Low)
```

### Scenario 16.2 - 위험 우선순위 정렬

```
Given 여러 위험이 존재함
When 위험 보고서를 생성함
Then 위험은 점수 순으로 정렬된다
  And Critical/High 위험이 최상단에 표시된다
```

---

## Validation Checklist

### 기능 테스트

| Test Category | Test Count | Pass Criteria |
|---------------|------------|---------------|
| RBAC Tests | 15 | 100% pass |
| Session Tests | 10 | 100% pass |
| Account Lockout Tests | 8 | 100% pass |
| Password Policy Tests | 12 | 100% pass |
| Audit Integrity Tests | 10 | 100% pass |
| TLS/mTLS Tests | 15 | 100% pass |
| SBOM Tests | 5 | 100% pass |
| **Total** | **75** | **100% pass** |

### 보안 테스트

| Test Type | Tool | Pass Criteria |
|-----------|------|---------------|
| SAST | SonarQube | 0 Critical, 0 High |
| DAST | OWASP ZAP | 0 Critical, 0 High |
| Dependency Scan | Trivy/NuGet Audit | 0 Critical, 0 High |
| Penetration Test | Manual | No critical findings |

### 커버리지

| Module | Target Coverage | Verification |
|--------|-----------------|--------------|
| UserServiceAdapter | 90% | dotnet coverage |
| AuditLogServiceAdapter | 90% | dotnet coverage |
| GrpcSecurity | 90% | dotnet coverage |
| PasswordPolicy | 95% | dotnet coverage |
| Overall Security | 90% | dotnet coverage |

---

**Document Version**: 1.0.0
**Last Updated**: 2026-03-12
