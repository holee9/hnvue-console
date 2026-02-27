# HnVue 제품 개요 (Product Overview)

## MISSION

의료 진단 장비 엑스레이 GUI 콘솔 소프트웨어를 개발하여 의료진에게 안전하고 신뢰할 수 있는 환자 진단 경험을 제공합니다.

의료용 소프트웨어의 안전성과 신뢰성을 최우선으로 하며, 국제 표준(IEC 62304, ISO 14971, ISO 13485)을 준수하는 규제 대상 소프트웨어로서 환자 안전과 데이터 무결성을 보장합니다.

## VISION

차세대 하이브리드 아키텍처(C++ Core Engine + C# WPF GUI)를 통해 고성능 이미지 처리와 직관적인 사용자 인터페이스를 결합하여, 의료 현장의 생산성을 향상시키고 진단 정확도를 높이는 것을 목표로 합니다.

모듈화된 플러그인 아키텍처를 통해 기능 확장성과 유지보수성을 확보하며, 지속적인 품질 개선과 혁신을 통해 글로벌 의료 장비 시장에서 경쟁력을 갖춘 제품을 제공합니다.

## USER (사용자)

### Primary Users (주요 사용자)

**Radiologist (영상의학 전문의)**
- 역할: 환자의 엑스레이 영상을 판독하고 진단 결과를 작성
- 핵심 필요사항:
  - 고화질 영상 디스플레이 및 조정 기능
  - 빠른 영상 로딩 및 처리 성능
  - 직관적인 영상 조작 도구 (확대/축소, 밝기/대비 조정)
  - 이전 진단 이력 조회 및 비교
  - 안전한 인터락 시스템 (중복 방사선 피노출 방지)

**Radiologic Technologist (방사선사)**
- 역할: 환자를 포지셔닝하고 엑스레이 촬영을 수행
- 핵심 필요사항:
  - 장치 상태 모니터링 및 제어
  - 촬영 파라미터 설정 (kV, mAs, 시간 등)
  - 실시간 장치 연동 상태 표시
  - 안전 인터락 확인 및 해제
  - 촬영 품질 프리뷰

**Biomedical Engineer (생체의공학 담당자)**
- 역할: 장치 유지보수 및 문제 해결
- 핵심 필요사항:
  - 상세한 로그 및 진단 정보
  - 장치 테스트 및 교정 기능
  - 성능 모니터링 도구
  - 펌웨어 업데이트 관리

**Regulatory Compliance Officer (규제 준비 담당자)**
- 역할: 규제 표준 준수 확인 및 문서화
- 핵심 필요사항:
  - 감사 추적 기능 (Audit Trail)
  - 소프트웨어 버전 관리
  - 안전 관련 문서화
  - 위험 관리 기록

### Secondary Users (보조 사용자)

**Hospital IT Administrator**
- 시스템 배포 및 업데이트 관리
- 네트워크 구성 및 DICOM 통신 설정

**Quality Assurance Team**
- 소프트웨어 테스트 수행
- 결함 추적 및 수정 확인

## PROBLEM (해결 과제)

### Current Problems (현재 문제점)

**안전성 요구사항 (Safety Requirements)**
- 문제: 의료용 소프트웨어 오류는 환자 안전에 직접적인 영향을 미침
- 영향: 방사선 과노출, 오진, 장치 손상 등 심각한 부작용 가능
- 긴급도: CRITICAL

**규제 준수 복잡성 (Regulatory Compliance)**
- 문제: IEC 62304 Class B/C, ISO 14971, ISO 13485 표준 준수 필요
- 영향: 개발 프로세스 복잡화, 문서화 부담, 인증 비용 증가
- 긴급도: HIGH

**성능 요구사항 (Performance Requirements)**
- 문제: 실시간 이미지 처리(고해상도 DICOM)와 낮은 지연시간 요구
- 영향: 의료진의 업무 효율 저하, 환자 불편
- 긴급도: HIGH

**통합 복잡성 (Integration Complexity)**
- 문제: 하드웨어 장치(FPGA, Detector), 여러 서비스(DICOM, Dose), GUI 간 통신 필요
- 영향: 시스템 안정성 리스크, 디버깅 어려움
- 긴급도: MEDIUM

## STRATEGY (전략)

### Architectural Strategy (아키텍처 전략)

**하이브리드 아키텍처 (Hybrid Architecture)**
- C++ Core Engine: 고성능 이미지 처리, 하드웨어 제어, IPC 서버
- C# WPF GUI: 사용자 인터페이스, 비즈니스 로직, IPC 클라이언트
- gRPC IPC: 안정적인 프로세스 간 통신

**모듈화된 계층 구조 (Modular Layered Architecture)**
```
┌─────────────────────────────────────┐
│   Presentation Layer (UI - WPF)     │
├─────────────────────────────────────┤
│  Application Layer (Workflow)       │
├─────────────────────────────────────┤
│   Service Layer (Dicom, Dose)       │
├─────────────────────────────────────┤
│    IPC Layer (gRPC Client/Server)   │
├─────────────────────────────────────┤
│  Core Layer (HAL, Imaging - C++)    │
├─────────────────────────────────────┤
│    Infrastructure (Build, CI/CD)    │
└─────────────────────────────────────┘
```

**플러그인 아키텍처 (Plugin Architecture)**
- HAL: 하드웨어 드라이버 플러그인 (DLL)
- Imaging: 이미지 처리 엔진 플러그인 (교체 가능)
- 독립적인 기능 모듈: 순환 의존성 방지

### Development Strategy (개발 전략)

**SPEC-First 개발 (Specification-First Development)**
- 모든 기능은 SPEC 문서(EARS 형식)부터 시작
- 명확한 요구사항 정의 후 구현 착수
- 요구사항 추적 가능성 보장

**하이브리드 개발 방법론 (Hybrid Methodology)**
- 신규 코드: TDD (Test-Driven Development, RED-GREEN-REFACTOR)
- 레거시 리팩토링: DDD (Domain-Driven Development, ANALYZE-PRESERVE-IMPROVE)
- 목표 커버리지: 85% 이상

**TRUST 5 품질 프레임워크 (Quality Framework)**
- Tested: 포괄적인 테스트 커버리지
- Readable: 명확한 네이밍과 문서화
- Unified: 일관된 코드 스타일
- Secured: OWASP 준수, 입력 검증
- Trackable: 추적 가능한 커밋과 변경 이력

### Implementation Strategy (구현 전략)

**구현 순서 (Implementation Order)**
1. INFRA: 빌드 시스템, CI/CD, 프로젝트 구조
2. IPC: gRPC 서버/클라이언트, Protobuf 정의
3. HAL: 하드웨어 추상화 계층
4. Imaging: 이미지 처리 엔진
5. DICOM: 의료 영상 통신 표준
6. Dose: 방사선량 관리
7. WORKFLOW: 비즈니스 로직 및 안전 인터락
8. UI: 사용자 인터페이스
9. TEST: 통합 및 인수 테스트

**독립적인 SPEC 구조 (Independent SPECs)**
- 각 SPEC은 독립적으로 개발 가능
- 명확한 인터페이스 정의
- 최소한의 의존성

## SUCCESS (성공 지표)

### Functional Success (기능적 성공)

**안전성 (Safety)**
- 9개의 안전 인터락 구현 및 검증
- 중복 방사선 피노출 방지 100%
- 장치 오류 상황에서 안전한 정지

**성능 (Performance)**
- 엑스레이 영상 로딩: < 2초 (2048x2048 DICOM)
- IPC 지연시간: < 100ms (95th percentile)
- GUI 응답시간: < 200ms (사용자 작업)

**신뢰성 (Reliability)**
- 평균 무중단 시간 (MTBF): > 1000시간
- 장애 복구 시간 (MTTR): < 30분
- 데이터 무결성: 100% (DICOM 전송)

### Quality Success (품질 성공)

**코드 커버리지 (Code Coverage)**
- 전체 커버리지: 85% 이상
- 안전 관련 코드 (Class C): 100% 분기 커버리지
- 통합 테스트 커버리지: 80% 이상

**규제 준수 (Regulatory Compliance)**
- IEC 62304 Class B/C 준비 완료
- ISO 14971 위험 관리 문서화
- ISO 13485 품질 관리 프로세스

**품질 게이트 통과 (Quality Gates)**
- Zero LSP errors (Type check, Lint)
- Zero security vulnerabilities
- All TRUST 5 principles satisfied

### Delivery Success (전달 성공)

**SPEC 완료율 (SPEC Completion)**
- 9개 SPEC 모두 구현 완료
- 각 SPEC별 인수 기준 충족
- 문서화 동기화 완료

**CI/CD 안정성 (CI/CD Stability)**
- 빌드 성공률: > 95%
- 테스트 자동화: > 90%
- 배포 자동화: 100%

## COMPETITIVE ADVANTAGE (경쟁 우위)

### Technical Advantages (기술적 우위)

**하이브리드 아키텍처**
- C++의 고성능 + C#의 생산성 결합
- 최적의 성능과 개발 효율성
- 플랫폼 간 코드 재사용 (IPC 통한 분리)

**모듈화된 플러그인 시스템**
- 이미지 처리 엔진 교체 가능성
- 하드웨어 드라이버 독립적 개발
- 기능 확장 용이성

**SPEC-First 개발 프로세스**
- 요구사항 변경에 유연한 대응
- 명확한 개발 범위와 일정
- 높은 품질과 규제 준수

### Process Advantages (프로세스 우위)

**MoAI-ADK 통합**
- AI 기반 개발 워크플로우 자동화
- 일관된 문서화 및 코드 품질
- 빠른 프로토타이핑 및 반복

**규제 대응 준비**
- 의료용 소프트웨어 표준 준수
- 위험 관리 프로세스 내재화
- 감사 추적 및 문서화 자동화

## DIFFERENTIATION (차별화 요소)

### vs. Legacy Systems (레거시 시스템 대비)

**사용자 경험**
- 직관적인 WPF GUI vs 복잡한 레거시 인터페이스
- 실시간 피드백 및 상태 표시
- 현대적인 사용자 경험 (UX)

**유지보수성**
- 모듈화된 아키텍처 vs 모놀리식 구조
- 명확한 계층 분리
- 쉬운 기능 확장

### vs. Competitors (경쟁사 대비)

**안전성**
- 9개的综合 인터락 시스템
- 다중 레벨 안전 검증
- 규제 표준 완전 준수

**개방성**
- 표준 기반 아키텍처 (DICOM, gRPC)
- 플러그인 확장 가능
- API 공개 및 통합 용이

**품질**
- 85%+ 테스트 커버리지
- TRUST 5 품질 프레임워크
- 지속적 통합 및 배포

## CONSTRAINTS (제약사항)

### Regulatory Constraints (규제 제약)

- IEC 62304 Class B/C: 의료용 소프트웨어 생명주기 프로세스
- ISO 14971: 의료기기 위험 관리
- ISO 13485: 의료기기 품질 관리 시스템
- 지역 규정: KDFA (한국), FDA (미국), CE (유럽)

### Technical Constraints (기술적 제약)

- 언어: C++ 17, C# 12 (.NET 8 LTS)
- 빌드: CMake 3.25+, vcpkg (C++), MSBuild (C#)
- 플랫폼: Windows 10/11 (x64)
- IPC: gRPC 1.68.x, Protocol Buffers

### Business Constraints (비즈니스 제약)

- 개발 팀: 소규모 팀 (1-3명)
- 일정: 12개월 MVP 목표
- 예산: 제한된 리소스
- 시장: 글로벌 의료 장비 시장

## HISTORY (이력)

### Project Initiation (프로젝트 시작)

**2026-02-17: 프로젝트 초기화**
- MoAI-ADK 설정 완료
- 9개 SPEC 문서 생성 (spec.md만)
- 프로젝트 구조 설정

**2026-02-18: SPEC-INFRA-001 완료**
- 첫 번째 구현 SPEC 완료
- 빌드 시스템, CI/CD 구조 확립
- 프로젝트 문서화 시작

### Key Milestones (주요 마일스톤)

- [ ] Phase 1: INFRA (완료)
- [ ] Phase 2: IPC (진행 예정)
- [ ] Phase 3: HAL + IMAGING (계획됨)
- [ ] Phase 4: DICOM + DOSE (계획됨)
- [ ] Phase 5: WORKFLOW + UI (계획됨)
- [ ] Phase 6: TEST + 인증 (계획됨)

---

**문서 버전:** 1.0.0
**최종 업데이트:** 2026-02-18
**작성자:** abyz-lab
**언어:** Korean (ko)
