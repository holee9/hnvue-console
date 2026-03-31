# HnVue Console 차기 개발 구현 계획서 (Phase 2)

> **문서 버전**: 1.0
> **작성일**: 2026-03-12
> **대상**: MFDS 인허가 준비 (2026 Q4 목표)
> **이전 기준**: Phase 1 완료 (SPEC 10개, 테스트 1,048개 통과)

---

## Executive Summary

본 문서는 HnVue Console 프로젝트의 **Phase 1 완료(SPEC 10개, 테스트 1,048개)** 상태에서 **MFDS 인허가 제출(2026 Q4 목표)**까지의 개발 로드맵을 정의합니다.

### 핵심 성과 (Phase 1 완료)

| 항목 | 성과 | 비고 |
|------|------|------|
| **SPEC 문서** | 10개 완료 (100%) | INFRA, IPC, HAL, IMAGING, DICOM, DOSE, WORKFLOW, UI x2, TEST |
| **테스트** | 1,048개 통과 (92% 커버리지) | 단위/통합 테스트 완료 |
| **문서화** | MRD, PRD, RTM, 보안 규정 준수 계획 | 규제 준비 기반 완료 |
| **아키텍처** | IEC 62304 Class B/C | 하이브리드(C++ Core + WPF GUI) |

### 남은 과제 (Phase 2)

| 영역 | 현재 상태 | 목표 상태 | Gap |
|------|----------|----------|-----|
| **gRPC 어댑터** | 16% 구현 (2/13 Partial, 9/13 Stub) | 100% 실제 구현 | **P1** |
| **보안 제어** | Proto 정의됨, 구현 안 됨 | 모든 보안 제어 구현 | **P0** |
| **규정 문서** | 40% 완료 | 100% 완료 | **P1** |

---

## 1. Gap 분석

### 1.1 기술적 Gap

```
┌─────────────────────────────────────────────────────────────┐
│              Current State → Target State Gap               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  gRPC Adapters:                                             │
│    Current: ████░░░░░░░░░░░░░ 16% (2 partial, 9 stub)        │
│    Target:  ████████████████████ 100% (all real)            │
│    Gap:     84% 구현 필요                                   │
│                                                             │
│  Security Controls:                                         │
│    Current: Proto defined only                             │
│    Target: All controls implemented + verified              │
│    Gap:     Full implementation required                    │
│                                                             │
│  Regulatory Documents:                                      │
│    Current: 40% (MRD, PRD, RTM, Security Plan)              │
│    Target: 100% (+STR, SBOM, Risk Report, Submission)      │
│    Gap:     60% documentation needed                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 규정 준용 Gap

| 규정 요구사항 | 현재 상태 | 목표 상태 | Gap | 우선순위 |
|-------------|----------|----------|-----|----------|
| **FDA Section 524B** | 보안 요구사항 정의됨 | 구현 + 증빙 | P0 | Critical |
| **IEC 62304 문서** | SPEC 10개 | STR, RTM 완결 | P1 | High |
| **ISO 14971 위험관리** | 사이버보안 위협모델 | 전체 위험평가 | P1 | High |
| **SBOM** | 미생성 | NTIA 최소 요소 준수 | P0 | Critical |
| **보안 취약점** | 정적 분석 기본 설정 | Critical/High 0개 | P1 | High |
| **감사 로그** | Proto 정의됨 | WORM 저장 + 서명 | P0 | Critical |

---

## 2. 위험 기반 우선순위 매트릭스

### 2.1 잔여 작업 위험 평가

| 작업 항목 | 심각도 | 시급성 | 영향도 | 위험 점수 | 우선순위 | Phase |
|----------|--------|--------|--------|----------|----------|-------|
| **UserService 구현** | Critical | High | Critical | **10** | P0 | 1 |
| **AuditLogService 구현** | Critical | High | Critical | **10** | P0 | 1 |
| **gRPC TLS/mTLS 구성** | Critical | High | High | **9** | P0 | 1 |
| **SBOM 생성 + CI 통합** | High | High | High | **8** | P0 | 1 |
| **위협 모델링 (STRIDE)** | Medium | High | High | **7** | P1 | 1 |
| **DoseService 구현** | High | Medium | Critical | **8** | P1 | 2 |
| **ImageService 구현** | High | Medium | High | **7** | P1 | 2 |
| **PatientService 구현** | High | Medium | High | **7** | P1 | 2 |
| **취약점 스캔 파이프라인** | High | Medium | High | **7** | P1 | 3 |
| **나머지 6개 Stub 어댑터** | Medium | Low | Medium | **4** | P2 | 2 |
| **문서화 (STR, RTM)** | Low | Low | Medium | **3** | P2 | 4 |

### 2.2 의존성 분석

```
[Critical Path - MFDS Submission 2026 Q4]
   │
   ├─ Phase 1: 보안 기반 (3-4주) ─────────────┐
   │   │                                     │
   │   ├─ UserService (RBAC)                  ├─── Phase 2로 진입 조건
   │   ├─ AuditLogService                    │
   │   ├─ gRPC TLS/mTLS                      │
   │   └─ SBOM 자동화                        │
   │                                         │
   ├─ Phase 2: 어댑터 구현 (4-6주)          │
   │   │                                     │
   │   ├─ DoseService (안전성)               ├─── Phase 3로 진입 조건
   │   ├─ ImageService (핵심 기능)           │
   │   ├─ PatientService (데이터 보호)       │
   │   └─ 나머지 6개 Stub                     │
   │                                         │
   ├─ Phase 3: 보안 강화 (2-3주)            │
   │   │                                     │
   │   ├─ SAST/DAST 파이프라인               ├─── Phase 4로 진입 조건
   │   ├─ 침투 테스트                         │
   │   └─ 취약점 수정                        │
   │                                         │
   └─ Phase 4: 규정 준용 문서 (2-3주)        │
       ├─ STR, RTM 업데이트                  │
       ├─ 위험관리 보고서                     │
       └─ MFDS 제출 패키지                    │
```

---

## 3. 단계별 개발 로드맵

### Phase 1: 보안 기반 구축 (3-4주)

**목표:** FDA Section 524B 핵심 요구사항 충족

**새 SPEC 생성 권장:** `SPEC-SECURITY-001`

| 작업 | 산출물 | 담당 | 기간 | 성공 기준 |
|------|--------|------|------|-----------|
| **1.1 UserService 구현** | 인증/인가 모듈 | Backend | 1주 | [ ] RBAC 동작<br>[ ] 세션 관리 (30분 타임아웃)<br>[ ] 로그인 5회 실패 시 잠금<br>[ ] 비밀번호 복잡성 검증 |
| **1.2 AuditLogService 구현** | 감사 로그 모듈 | Backend | 1주 | [ ] 무결성 (SHA-256 서명)<br>[ ] WORM 저장<br>[ ] 6년 보관 정책<br>[ ] 타임스탬프 정확도 (NTP) |
| **1.3 gRPC TLS/mTLS 구성** | TLS 구성 가이드 + 구현 | DevOps | 3일 | [ ] TLS 1.3+ 필수<br>[ ] 상호 인증 (mTLS)<br>[ ] 인증서 회전 정책 |
| **1.4 SBOM 자동화** | components.json + CI | DevOps | 3일 | [ ] NTIA 최소 요소 준수<br>[ ] CI 빌드 시 자동 생성<br>[ ] CVE 스캔 연동 |
| **1.5 위협 모델링 (STRIDE)** | 위협 모델 문서 | Security | 3일 | [ ] STRIDE 6가지 분석<br>[ ] 위험 점수 매트릭스<br>[ ] 완화책 정의 |

**Phase 1 성공 기준:**
- Critical 보안 제어 100% 구현
- SBOM 자동화 CI 통합 완료
- 위협 모델 문서 승인
- UserService + AuditLogService 테스트 커버리지 85%+

---

### Phase 2: 어댑터 구현 (4-6주)

**목표:** 핵심 기능 Stub 어댑터를 실제 gRPC 호출로 교체

**새 SPEC 생성 권장:** `SPEC-ADAPTER-001`

#### 2.1 P1 어댑터 (안전성/핵심 기능)

| 어댑터 | proto 정의 | 실제 메서드 | 기간 | 우선순위 | Safety Class |
|--------|-----------|-------------|------|----------|--------------|
| **DoseService** | ✅ 완료 | 0 → 5 메서드 | 1주 | P1 | Class B |
| **ImageService** | ✅ 완료 | 0 → 7 메서드 | 2주 | P1 | Class B |
| **PatientService** | ✅ 완료 | 0 → 4 메서드 | 1주 | P1 | Class B |

#### 2.2 P2 어댑터 (운영 편의성)

| 어댑터 | proto 정의 | 실제 메서드 | 기간 | 우선순위 | Safety Class |
|--------|-----------|-------------|------|----------|--------------|
| **WorklistService** | ✅ 완료 | 0 → 3 메서드 | 3일 | P2 | Class B |
| **AECService** | ✅ 완료 | 0 → 4 메서드 | 3일 | P2 | Class C |
| **ProtocolService** | ✅ 완료 | 0 → 4 메서드 | 3일 | P2 | Class B |
| **QCService** | ✅ 완료 | 0 → 5 메서드 | 1주 | P2 | Class B |
| **UserService** | ✅ 완료 | Phase 1에서 구현 | - | - | Class B |
| **AuditLogService** | ✅ 완료 | Phase 1에서 구현 | - | - | Class B |

**Phase 2 성공 기준:**
- Stub → Real 구현률 16% → 100%
- 각 어댑터 테스트 커버리지 85%+
- gRPC 통합 테스트 통과
- Proto 버전 호환성 검증 완료

---

### Phase 3: 보안 강화 (2-3주)

**목표:** 취약점 0개 달성 (Critical/High)

| 작업 | 도구 | 산출물 | 기간 | 성공 기준 |
|------|------|--------|------|-----------|
| **3.1 SAST 파이프라인** | SonarQube, Roslyn Analyzers | CI/CD 스캔 | 3일 | [ ] CI 빌드 시 자동 실행<br>[ ] Critical 경고 0개 |
| **3.2 DAST 스캔** | OWASP ZAP, Burp Suite | 스캔 보고서 | 1주 | [ ] OWASP Top 10 대응<br>[ ] High 취약점 0개 |
| **3.3 의존성 스캔** | NuGet Audit, Snyk | CVE 보고서 | 3일 | [ ] High CVE 0개<br>[ ] Medium CVE < 5개 |
| **3.4 침투 테스트** | 외부 업체 | PT 보고서 | 2주 | [ ] 인증 우회 불가<br>[ ] 권한 상승 불가<br>[ ] SQL Injection 없음 |

**Phase 3 성공 기준:**
- Critical 취약점: 0개
- High 취약점: 0개
- Medium 취약점: < 5개 (완화책 문서화)
- 코드 커버리지: > 85%

---

### Phase 4: 규정 준용 문서 (2-3주)

**목표:** MFDS 제출 패키지 완결

**새 SPEC 생성 권장:** `SPEC-DOCS-001`

| 문서 | 템플릿 | 규정 | 기간 | 성공 기준 |
|------|--------|------|------|-----------|
| **4.1 Software Test Report (STR)** | `.moai/templates/security/...` | IEC 62304 | 1주 | [ ] 모든 테스트 결과 포함<br>[ ] 보안 테스트 결과 포함<br>[ ] 승인 서명 |
| **4.2 RTM 업데이트** | 기존 `docs/rtm.md` | IEC 62304 | 3일 | [ ] 최신 SPEC 반영<br>[ ] 테스트 커버리지 100% |
| **4.3 위험관리 보고서** | `.moai/templates/security/...` | ISO 14971 | 1주 | [ ] 사이버보안 위험 포함<br>[ ] 완화책 검증 |
| **4.4 SBOM** | components.json | FDA Section 524B | 1일 | [ ] NTIA 최소 요소<br>[ ] 최신 버전 |
| **4.5 MFDS 제출 패키지** | 의료기기종합정보 | MFDS | 3일 | [ ] 사이버보안 기술문서<br>[ ] 소프트웨어 검증보고서 |

**Phase 4 성공 기준:**
- 모든 규정 요구사항 문서화 완료
- 내부 품질 검토 통과
- 규정 담당자 승인

---

## 4. 리소스 추정

### 4.1 일정 요약

| Phase | 기간 | 주요 작업 | 산출물 | 마일스톤 |
|-------|------|----------|--------|--------|
| **Phase 1** | 3-4주 | 보안 기반 | UserService, AuditLogService, TLS, SBOM | M1 |
| **Phase 2** | 4-6주 | 어댑터 구현 | 9개 Stub → Real 구현 | M2 |
| **Phase 3** | 2-3주 | 보안 강화 | 취약점 0개 달성 | M3 |
| **Phase 4** | 2-3주 | 문서 완결 | STR, RTM, 제출 패키지 | M4 |
| **총계** | **11-16주** | **MFDS 인허가 준비 완료** | **2026 Q4 제출 가능** | MFDS Submit |

### 4.2 인력 요구사항

| 역할 | 전담 투입 | Phase 1 | Phase 2 | Phase 3 | Phase 4 |
|------|----------|---------|---------|---------|---------|
| **Backend 개발자** | 2명 | UserService, AuditLogService | 어댑터 9개 | 취약점 수정 | - |
| **Frontend 개발자** | 1명 | - | ImageService UI | 보안 UI 강화 | - |
| **DevOps 엔지니어** | 1명 | TLS, SBOM | - | SAST/DAST 파이프라인 | - |
| **보안 전문가** | 1명 (파트) | 위협 모델링 | - | 침투 테스트 리뷰 | 문서 검토 |
| **QA 엔지니어** | 1명 | 테스트 케이스 | 통합 테스트 | 보안 테스트 | STR 작성 |
| **규정 담당자** | 1명 (파트) | - | - | - | 제출 패키지 |
| **PM** | 1명 | 전체 관리 | 전체 관리 | 전체 관리 | 전체 관리 |

**총 인월:** 약 20-25 인월 (14-16주, 5명 팀 구성)

---

## 5. 성공 기준

### 5.1 기술적 성공 기준

| 카테고리 | 지표 | 목표 | 측정 방법 |
|----------|------|------|----------|
| **테스트 커버리지** | 라인 커버리지 | > 85% | `dotnet test --collect:"XPlat Code Coverage"` |
| **보안 취약점** | Critical/High 개수 | 0개 | SAST/DAST 스캔 보고서 |
| **코드 품질** | LSP 경고 | < 10개 | Roslyn Analyzers |
| **어댑터 구현** | Stub → Real 비율 | 100% | `adapter-audit.md` |
| **보안 제어** | IEC 81001-5-1 준수 | 100% | 보안 검증 체크리스트 |

### 5.2 규정 준용 성공 기준

| 규정 | 요구사항 | 증빙 자료 | 승인 기관 |
|------|----------|-----------|----------|
| **FDA Section 524B** | 사이버보안 위험관리 | 위협 모델, SBOM, 취약점 보고서 | FDA |
| **IEC 62304** | 소프트웨어 수명주기 | STR, RTM, 단위/통합 테스트 | MFDS |
| **ISO 14971** | 위험관리 | 위험관리 보고서 | MFDS |
| **IEC 81001-5-1** | IT 보안 기능 | 보안 아키텍처, 검증 보고서 | MFDS |

### 5.3 프로젝트 관리 성공 기준

- 마일스톤 준수율: > 90%
- 버전 창출 주기: 2주 (Agile Sprint)
- 결함 수정 시간: < 3일 (P0/P1)
- 문서화 완결도: 100%

---

## 6. 위험 관리 계획

### 6.1 상위 5가지 위험 요소

| 위험 | 가능성 | 영향 | 완화 전략 | 대응 계획 |
|------|--------|------|----------|----------|
| **Proto 서버 지연** | Medium | Critical | Early Integration, Mock Server | C++ 팀과 주간 Sync |
| **보안 취약점 발견** | High | High | SAST 선행 실시, 코드 리뷰 | 버퍼 1주 확보 |
| **규정 요구사항 변경** | Low | High | 규정 담당자 상주 지원 | Flex 시간 10% 확보 |
| **인력 부족** | Medium | Medium | 외부 협력사 활용 | Phase 2부터 고려 |
| **일정 지연** | Medium | Medium | Critical Path 관리, Agile | 주간 진단 회의 |

### 6.2 감시 지표 (Leading Indicators)

- Sprint Velocity (Story Points/주)
- 코드 커버리지 추이 (%)
- 취약점 발생/수정 비율
- 팀 Burn-down Chart
- 규정 문서 완결도 (%)

---

## 7. 다음 단계 권장사항

### 7.1 즉시 실행 (Immediate)

1. **SPEC-SECURITY-001 생성**: Phase 1 보안 기반 구축을 위한 신규 SPEC
   - `/moai:1-plan "security foundation - UserService, AuditLogService, TLS, SBOM, threat modeling"`

2. **C++ Backend 팀 협의**: Proto 서버 구현 일정 조율
   - gRPC 서버 인터페이스 정의 확인
   - 개발 일정 공유

3. **보안 전문가 확보**: 위협 모델링 및 침투 테스트 지원

### 7.2 단기 실행 (Short-term, Phase 1 완료 후)

1. **SPEC-ADAPTER-001 생성**: Phase 2 어댑터 구현을 위한 신규 SPEC
   - `/moai:1-plan "gRPC adapter implementation - 9 stub to real"`

2. **DevOps 파이프라인 구축**: SAST/DAST 자동화

### 7.3 중기 실행 (Mid-term, Phase 2 완료 후)

1. **SPEC-DOCS-001 생성**: Phase 4 문서화를 위한 신규 SPEC
   - `/moai:1-plan "regulatory documentation - STR, RTM, MFDS submission"`

2. **제3자 침투 테스트 업체 선정**

### 7.4 MFDS 제출 체크리스트 (Pre-market)

- [ ] IEC 62304 Class B/C 문서 패키지
- [ ] ISO 14971 위험관리 보고서 (사이버보안 포함)
- [ ] FDA Section 524B 사이버보안 문서
- [ ] SBOM (NTIA 최소 요소)
- [ ] SAST/DAST/Fuzz 테스트 보고서
- [ ] 침투 테스트 보고서 (제3자)
- [ ] Software Test Report (STR)
- [ ] Requirements Traceability Matrix (RTM)
- [ ] 감사 로그 무결성 검증
- [ ] TLS/mTLS 구성 검증

---

## 8. 부록

### 8.1 관련 문서

| 문서 | 경로 | 설명 |
|------|------|------|
| **MRD** | [docs/mrd.md](../mrd.md) | 시장 요구사항 |
| **PRD** | [docs/prd.md](../prd.md) | 제품 요구사항 |
| **RTM** | [docs/rtm.md](../rtm.md) | 요구사항 추적성 매트릭스 |
| **보안 규정 준수 계획** | [docs/cybersecurity-compliance-plan.md](../cybersecurity-compliance-plan.md) | 사이버보안 규정 준수 |
| **어댑터 감사** | [docs/adapter-audit.md](../adapter-audit.md) | gRPC 어댑터 현황 |
| **보안 템플릿** | [docs/templates/security/](../templates/security/) | 보안 문서 템플릿 |

### 8.2 SPEC 문서 매핑

| Phase | 관련 SPEC | 상태 |
|-------|----------|------|
| **Phase 1** | SPEC-SECURITY-001 (신규 권장) | 생성 필요 |
| **Phase 2** | SPEC-ADAPTER-001 (신규 권장) | 생성 필요 |
| **Phase 3** | SPEC-TEST-001 (기존, Phase 4 활용) | 35% 완료 |
| **Phase 4** | SPEC-DOCS-001 (신규 권장) | 생성 필요 |

### 8.3 변경 이력

| 버전 | 날짜 | 변경 사항 | 작성자 |
|------|------|-----------|--------|
| 1.0 | 2026-03-12 | 초안 작성 (Phase 2 계획) | MoAI Orchestrator |

---

*본 문서는 HnVue Console 프로젝트의 Phase 2 개발 계획을 정의합니다. MFDS 인허가 제출(2026 Q4)을 목표로 단계별 실행 계획을 제시합니다.*
