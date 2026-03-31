# SW 아키텍처 설계서 (Software Architecture Design)

> **문서 번호**: HnVue-A03  
> **버전**: 0.1 (Draft)  
> **작성일**: ____-__-__  
> **작성자**: ____________  
> **검토자**: ____________  
> **승인자**: ____________  
> **제품**: HnVue Console SW  
> **회사**: H&abyz Co., Ltd. (에이치앤아비즈)  
> **분류**: ✅ 최소 필수  
> **적용 시장**: FDA ✅ / MFDS ✅ / EU MDR ✅  
> **근거 규격**: IEC 62304 §5.3, FDA SW Guidance (2019) Section IV-C  
> **IEC 62304 Class**: B (Basic Level)

---

## 변경 이력

| 버전 | 일자 | 변경 내용 | 작성자 |
|------|------|-----------|--------|
| 0.1  |      | 최초 작성  |        |

---

## 1. 목적

본 문서는 SRS(HnVue-A02) 요구사항을 충족하기 위한 SW 구성 요소(컴포넌트), 인터페이스, 데이터 흐름을 정의한다.  
IEC 62304 Class B에서 요구하는 아키텍처 수준 명세를 제공하며, FDA eSTAR에서 요구하는 SW 아키텍처 다이어그램의 문서 형태이다.  
본 설계서를 기반으로 구현(코딩) 및 통합 시험 계획이 수립된다.

---

## 2. 범위

| 항목 | 내용 |
|------|------|
| 적용 SW | HnVue Console SW v[작성 필요] |
| 설계 수준 | 아키텍처 레벨 (IEC 62304 Class B 최소 요건) |
| 상세 설계서(SDD) | Class B에서 선택적 — 본 문서에서 대체 |
| 운영 환경 | Windows 10/11 64-bit 워크스테이션 |
| 제외 범위 | 검출기 펌웨어, PACS 서버 내부 구조 |

---

## 3. 관련 문서

| 문서 번호 | 문서명 | 관계 |
|-----------|--------|------|
| HnVue-A02 | SW 요구사항 명세서 (SRS) | 본 SAD의 입력 |
| HnVue-A04 | SOUP 목록 | SOUP 연동 참조 |
| HnVue-B01 | 통합 시험 결과서 | 본 SAD 기반 시험 |
| HnVue-B03 | 추적성 매트릭스 (RTM) | SRS ↔ SAD 추적 |
| HnVue-D01 | 위험 관리 파일 | 안전 아키텍처 근거 |
| IEC 62304:2006+AMD1:2015 | SW 수명주기 표준 | 근거 규격 §5.3 |

---

## 4. 본문

### 4.1 아키텍처 개요

#### 4.1.1 설계 원칙

- **모듈화**: 각 기능은 독립적인 컴포넌트로 분리하여 변경 영향 최소화
- **SOUP 격리**: 모든 SOUP(제3자 라이브러리)는 Wrapper 또는 Adapter 패턴으로 감싸 직접 의존성 최소화
- **보안 내재화**: 인증·암호화·감사 로그는 별도 Security Module이 전담
- **단방향 의존성**: 상위 레이어는 하위 레이어에만 의존 (순환 의존 금지)

#### 4.1.2 시스템 블록 다이어그램 (개념)

```
┌──────────────────────────────────────────────────────────┐
│                   HnVue Console SW                        │
│                                                           │
│  ┌──────────────┐   ┌──────────────┐   ┌─────────────┐  │
│  │  UI Module   │   │ Image        │   │  Security   │  │
│  │  (Presenta-  │◄──│ Processing   │   │  Module     │  │
│  │   tion)      │   │  Module      │   │             │  │
│  └──────┬───────┘   └──────┬───────┘   └──────┬──────┘  │
│         │                  │                   │          │
│  ┌──────▼───────────────────▼───────────────────▼──────┐ │
│  │              Business Logic Layer                     │ │
│  │  (Worklist, Study Management, Report Generation)      │ │
│  └──────────────────────────┬────────────────────────── ┘ │
│                             │                             │
│  ┌──────────────────────────▼────────────────────────── ┐ │
│  │                  Data Access Layer                     │ │
│  │    (DICOM Engine / Local Storage / Config Manager)    │ │
│  └──────┬────────────────────────────────────┬──────────┘ │
│         │                                    │             │
│  ┌──────▼──────┐                    ┌────────▼─────────┐  │
│  │  HW Abstrac-│                    │  Network Stack   │  │
│  │  tion Layer │                    │  (TLS/DICOM Net) │  │
│  └──────┬──────┘                    └────────┬─────────┘  │
└─────────┼──────────────────────────────────── ┼───────────┘
          │                                     │
   ┌──────▼──────┐                    ┌─────────▼────────┐
   │  DR 검출기   │                    │  PACS / HIS/RIS  │
   └─────────────┘                    └──────────────────┘
```

> **주의**: 위 다이어그램은 개념적 표현이다. 실제 구현 시 상세 블록 다이어그램(UML Component Diagram 또는 Block Diagram)을 첨부하여 본 섹션을 대체한다.

#### 4.1.3 레이어 구조

| 레이어 | 구성 모듈 | 책임 |
|--------|-----------|------|
| Presentation Layer | UI Module | 사용자 인터페이스, 영상 렌더링, 이벤트 처리 |
| Business Logic Layer | Worklist Manager, Study Manager, Report Generator | 임상 워크플로우 로직 |
| Data Access Layer | DICOM Engine, Local Storage, Config Manager | 데이터 I/O 추상화 |
| HW Abstraction Layer | Detector Interface | 검출기 드라이버 추상화 |
| Network Stack | DICOM Network, TLS Manager | 네트워크 통신 |
| Security Module | Auth Manager, Session Manager, Audit Logger | 보안 기능 (횡단 관심사) |

---

### 4.2 SW 컴포넌트 목록

| 컴포넌트 ID | 컴포넌트명 | 기능 요약 | IEC 62304 클래스 | SOUP 여부 | 관련 SRS |
|-------------|-----------|----------|-----------------|----------|---------|
| COMP-001 | UI Module | 영상 표시, WL/WW 조절, Pan/Zoom, 주석 도구 | B | No | SWR-010~SWR-034 |
| COMP-002 | DICOM Engine | DICOM 파싱·생성·C-STORE·C-FIND·MWL | B | Yes (DCMTK) | SWR-040~SWR-043 |
| COMP-003 | Image Processing Module | 히스토그램, LUT, 노이즈 저감, 측정 계산 | B | No | SWR-020~SWR-023 |
| COMP-004 | Security Module | 사용자 인증(RBAC), 세션 관리, TLS, 감사 로그 | B | Yes (OpenSSL) | SWR-050~SWR-054 |
| COMP-005 | Detector Interface | 검출기 드라이버 Wrapper, 영상 데이터 수신 | B | Yes (검출기 SDK) | SWR-001~SWR-004 |
| COMP-006 | Local Storage Manager | DICOM 파일 I/O, 환자 데이터 로컬 저장, 암호화 | B | No | SWR-SEC-002 |
| COMP-007 | Worklist Manager | MWL 조회, 환자 정보 연동, HIS/RIS 인터페이스 | B | No | SWR-IF-012, SWR-IF-013 |
| COMP-008 | Config Manager | 시스템 설정 관리, DICOM 노드 설정 | B | No | - |
| COMP-009 | Update Manager | SW 자동 업데이트, 버전 검증 | B | No | SWR-SEC-003 |

---

### 4.3 컴포넌트 간 인터페이스

| 인터페이스 ID | 제공자 | 소비자 | 설명 | 데이터 형식 | 방향 |
|--------------|--------|--------|------|------------|------|
| IF-001 | COMP-002 (DICOM Engine) | COMP-001 (UI Module) | 영상 픽셀 데이터 전달 | Internal Buffer (16-bit/Pixel) | → |
| IF-002 | COMP-005 (Detector Interface) | COMP-002 (DICOM Engine) | 검출기 원시 데이터 → DICOM 변환 | Raw Image Data | → |
| IF-003 | COMP-001 (UI Module) | COMP-003 (Image Processing) | 처리 요청 및 결과 수신 | API Call / Callback | ↔ |
| IF-004 | COMP-004 (Security Module) | COMP-001, 002, 006, 007 | 인증 상태 확인, 세션 토큰 | API / Event | → |
| IF-005 | COMP-002 (DICOM Engine) | COMP-006 (Local Storage) | DICOM 파일 저장/로드 | DICOM File (*.dcm) | ↔ |
| IF-006 | COMP-007 (Worklist Manager) | COMP-001 (UI Module) | 환자 워크리스트 표시 | HL7/DICOM MWL | → |
| IF-007 | COMP-002 (DICOM Engine) | 외부 PACS | C-STORE, C-FIND, C-MOVE | DICOM over TLS | ↔ |

---

### 4.4 외부 인터페이스 상세

#### 4.4.1 검출기 인터페이스

| 항목 | 내용 |
|------|------|
| 연결 방식 | USB 3.0 / GigE (검출기 모델별 상이 — 지원 모델 목록 별도 관리) |
| 드라이버 계층 | COMP-005 Detector Interface (Wrapper 패턴) |
| 검출기 SDK | [작성 필요 — SOUP-00x 참조] |
| 데이터 형식 | 원시 X-ray 이미지 데이터 → DICOM 변환 |
| 오류 처리 | 연결 실패/타임아웃 시 사용자 알림 및 로그 기록 |

#### 4.4.2 PACS 인터페이스

| DICOM 서비스 | 역할 | 프로토콜 | 포트 |
|-------------|------|---------|------|
| C-STORE SCU | PACS로 영상 전송 | DICOM over TLS | [작성 필요] |
| C-FIND SCU | PACS에서 영상 조회 | DICOM over TLS | [작성 필요] |
| C-MOVE SCU | PACS에서 영상 수신 | DICOM over TLS | [작성 필요] |
| C-ECHO SCU | PACS 연결 확인 | DICOM | [작성 필요] |

#### 4.4.3 네트워크 보안

- 모든 DICOM 네트워크 통신은 TLS 1.2 이상 적용
- 허용 Cipher Suite: [작성 필요]
- 인증서 관리: [작성 필요]

---

### 4.5 데이터 흐름도 (Data Flow)

#### 4.5.1 주요 시나리오: 영상 획득 및 PACS 전송

```
[DR 검출기] → (Raw Image) → [COMP-005 Detector I/F]
    → (Raw Image) → [COMP-002 DICOM Engine]
    → (DICOM 생성) → [COMP-006 Local Storage] (로컬 저장)
    → (DICOM 영상 데이터) → [COMP-001 UI Module] (화면 표시)
    ← (전송 요청) ← [사용자 조작]
    → [COMP-002 DICOM Engine] → (C-STORE over TLS) → [외부 PACS]
```

#### 4.5.2 주요 시나리오: 워크리스트 조회

```
[사용자] → (환자 검색 요청) → [COMP-007 Worklist Manager]
    → (DICOM MWL C-FIND) → [HIS/RIS 또는 PACS]
    ← (환자 목록) ← [COMP-007]
    → (화면 표시) → [COMP-001 UI Module]
```

---

### 4.6 배포 구성 (Deployment Architecture)

| 항목 | 내용 |
|------|------|
| 운영 OS | Windows 10 (22H2 이상) / Windows 11 64-bit |
| 설치 방식 | MSI 인스톨러 (코드 서명 적용) |
| 설치 경로 | `C:\Program Files\HnVue\` (기본값) |
| 데이터 저장 경로 | `[작성 필요]` (DICOM 로컬 저장소) |
| 로그 파일 경로 | `[작성 필요]` |
| 다중 인스턴스 | 지원 여부: [작성 필요] |
| 최소 HW 사양 | CPU: [작성 필요], RAM: [작성 필요] GB, Disk: [작성 필요] GB |
| 권장 HW 사양 | CPU: [작성 필요], RAM: [작성 필요] GB, GPU: [작성 필요] |

---

### 4.7 SOUP 연동 아키텍처

> SOUP 상세 정보는 SOUP 목록(HnVue-A04) 참조

| SOUP | 연동 컴포넌트 | 연동 방식 | 격리 전략 |
|------|-------------|----------|----------|
| .NET Runtime | 전체 애플리케이션 | 런타임 환경 | OS 레벨 분리 |
| DCMTK | COMP-002 DICOM Engine | Wrapper 클래스 | DicomEngineWrapper 클래스로 격리 |
| OpenSSL | COMP-004 Security Module | Wrapper 클래스 | CryptoWrapper 클래스로 격리 |
| 검출기 SDK | COMP-005 Detector Interface | Adapter 패턴 | DetectorAdapter 인터페이스로 격리 |
| [추가 SOUP] | [컴포넌트] | [방식] | [격리 전략] |

**SOUP 격리 원칙**: 모든 SOUP는 직접 호출 대신 Wrapper/Adapter 클래스를 통해 호출한다. 이를 통해 SOUP 버전 변경 시 영향 범위를 Wrapper 클래스로 한정한다.

---

### 4.8 보안 아키텍처

#### 4.8.1 인증 및 인가 흐름

```
[사용자 로그인] → [COMP-004 Auth Manager]
    → 비밀번호 해시 검증 (bcrypt/PBKDF2)
    → 세션 토큰 발급 (JWT 또는 동등 메커니즘)
    → RBAC 역할 확인 (Radiographer / Radiologist / Admin)
    → 역할별 기능 접근 제어
```

#### 4.8.2 데이터 암호화 범위

| 대상 | 암호화 방식 | 키 관리 |
|------|-----------|--------|
| 네트워크 전송 (DICOM) | TLS 1.2 이상 | 인증서 기반 |
| 로컬 저장 PHI | AES-256 | [작성 필요] |
| 설정 파일 (비밀번호 포함) | [작성 필요] | [작성 필요] |

#### 4.8.3 감사 로그 (Audit Log) 아키텍처

| 로그 이벤트 유형 | 기록 내용 |
|----------------|----------|
| 로그인/로그아웃 | 사용자 ID, 타임스탬프, IP |
| 영상 조회/출력 | 사용자 ID, 환자 ID, 영상 ID, 타임스탬프 |
| PACS 전송 | 사용자 ID, 대상 PACS, 전송 결과 |
| 설정 변경 | 사용자 ID, 변경 항목, 전/후 값 |
| 오류 이벤트 | 오류 코드, 스택 트레이스 (PHI 제외) |

- 로그 저장소: [작성 필요]  
- 로그 보존 기간: [최소 2년 이상 권장]  
- 로그 무결성 보호: [작성 필요]

---

## 5. 비고: 최소 필수 vs 리스크 최소화 선택 가이드

**IEC 62304 Class B 최소 요건**:  
Class B에서는 아키텍처 설계서(SAD)가 필수이며, 상세 설계서(SDD)는 선택 사항이다. 따라서 본 SAD만으로 IEC 62304 Class B 요건을 충족한다.  
단, 복잡한 영상 처리 알고리즘(예: AI/ML 통합 시)이 있는 경우 해당 컴포넌트에 대해 SDD 수준의 상세화를 권장한다.

**FDA 관점**:  
eSTAR Section 12에서 "Architecture diagram"을 명시적으로 요구한다. §4.1.2의 블록 다이어그램은 반드시 포함되어야 하며, 다이어그램만으로는 불충분하고 컴포넌트 설명(§4.2)이 함께 제출되어야 한다.

**EU MDR 관점**:  
Annex II §3.2 (Design and Manufacturing Information)에서 SW 아키텍처 설명을 요구한다. SOUP 격리 전략(§4.7)은 특히 EU MDR Notified Body 심사에서 중요하게 검토된다.

**SOUP 격리 전략의 중요성**:  
SOUP를 Wrapper 없이 직접 호출하면 SOUP 버전 업데이트 시 광범위한 변경이 필요해지고, 위험 평가가 어려워진다. Wrapper 패턴 적용은 추가 개발 비용이 소요되지만 규제 대응과 유지보수 측면에서 강력히 권장된다.

---

## 6. 변경 이력

| 버전 | 일자 | 변경 내용 | 작성자 |
|------|------|-----------|--------|
| 0.1  |      | 최초 작성  |        |

---

## 7. 승인란

| 역할 | 성명 | 서명 | 일자 |
|------|------|------|------|
| 작성자 (SW 아키텍트) | | | |
| 검토자 (QA 책임자) | | | |
| 검토자 (RA 담당자) | | | |
| 검토자 (사이버보안 담당) | | | |
| 승인자 (대표이사/QA 총괄) | | | |
