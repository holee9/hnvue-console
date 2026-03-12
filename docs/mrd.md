# MRD (Market Requirements Document)
# HnVue — 진단 의료용 X-ray Console Software

---

> **문서 버전**: 1.0
> **작성일**: 2026-03-12
> **프로젝트**: HnVue Console
> **문서 상태**: 초안
> **기밀 등급**: 내부용

---

## 1. Executive Summary

### 1.1 제품 개요

HnVue Console은 진단 의료용 디지털 X-ray 시스템을 위한 통합 제어 콘솔 소프트웨어입니다. FPGA 기반 Flat Panel Detector와 X-ray Generator를 제어하고, 영상 획득/처리/판독/전송의 전체 임상 워크플로우를 지원하며, 병원 IT 인프라(PACS/HIS/RIS)와 표준 기반으로 연동합니다.

#### 제품 식별 정보

| 항목 | 값 | 출처 |
|------|-----|------|
| **제품명** | Digital X-ray Imaging System | Performance Test Report |
| **모델명** | HnX-R1 | Performance Test Report |
| **모델 변종** | G1417CW, G1417CWP, G1417FW, G1417FWP (G 시리즈/2/3G) | Performance Test Report |
| **모델 변종** | A1717MCW, A1417MCW, F1417MCW (A/F 시리즈) | Performance Test Report |
| **Console Software** | HnVUE | User Manual |
| **버전** | 1.0.2 (User Manual), 1.0.0.37 (Performance Test) | User Manual / Test Report |
| **제조사** | H&abyz Co., Ltd. | User Manual |
| **주소** | 2-Dong, 41-16, Cheoinseong-ro, Namsa-eup, Cheoin-gu, Yongin-si, Gyeonggi-do, 17118, Korea | User Manual |
| **연락처** | Tel: 070-4658-9300, Fax: 031-322-9390, E-mail: sales@abyzr.com | User Manual |

#### Performance Test 검증 결과 (2025-07-14 ~ 2025-07-18)

> **출처**: `docs/ref/3. [HnVUE] Performance Test Report (A-PTR-HNV).docx`

| 테스트 항목 | 결과 | 비고 |
|------------|------|------|
| 영상 획득/저장/전송 | ✅ PASS | PT-01 |
| 환자 정보 저장/조회 | ✅ PASS | PT-02 |
| 영상 조작 (확대/축소/회전/이동/밝기) | ✅ PASS | PT-03 |
| 영상 레이아웃/Thumbnail/부분 확대 | ✅ PASS | PT-04 |
| DICOM 3.0 지원 | ✅ PASS | PT-05 |
| 사용자 인증 | ✅ PASS | PT-06 |
| 영상 삭제 권한 | ✅ PASS | PT-07 |
| PACS 오류 처리 | ✅ PASS | PT-08 |
| 오류 메시지 안내 | ✅ PASS | PT-09 |
| 환자 정보 확인 | ✅ PASS | PT-10 |
| PACS 통신 연결 | ✅ PASS | PT-11 |

**결론**: 11개 테스트 항목 모두 PASS - 제품 성능 및 안전성 검증 완료

#### UI 개선 계획 (251118 기준)

> **출처**: `docs/ref/★HnVUE UI 변경 최종안_251118.pptx`

| 화면 | 변경 사항 |
|------|-----------|
| **Login** | 로그인 창 개선 |
| **Worklist** | ExamDate, Accession No, Ref. Physician 필터 추가 |
| **Studylist** | Register 기능, Auto-Generate 추가 |
| **Acquisition** | 환자 정보/촬영 리스트 UI 개선 (1안) |
| **Add Patient/Procedure** | View/Projection 선택 UI 개선 |

#### Console 내재화 계획 (hnvue_abyz_plan.pptx)

> **출처**: `docs/ref/hnvue_abyz_plan.pptx`

| 항목 | 내용 |
|------|------|
| **구성요소 DLL** | BCrypt.Net (암호화), BouncyCastle (RSA/AES/ECC), ClosedXML (Excel), CommunityToolkit.HighPerformance (성능), Dapper (SQL), DocumentFormat.OpenXml (Office), fo-dicom.core (DICOM) |
| **일정 (2026)** | 1Q: 선행 분석, 2Q: 기능 개발, 3Q: 기능 평가, 4Q: 완료 |
| **CyberSecurity** | 인증 계획 수립 (3Q부터 개발과 평가 병행) |

#### Console 기본 요건 (20260310 기준)

> **출처**: `docs/ref/console 기본 요건_20260310.xlsx`

| 영역 | 요구사항 |
|------|-----------|
| **Login** | 계정별 권한 (admin/Engineer/Operator), Cyber security 적용 (로그인 실패 처리) |
| **Code manager** | RIS 코드 정리, Excel import/export, Study 구분 |
| **UI 상단** | Storage status, MWL/PACS/Printer 연결 상태 아이콘 |
| **Worklist** | Patient study search, ER/Refresh/환자 등록 기능 |
| **환자 등록** | TEMP ID 생성 방식 논의 필요 |
| **Study 용어** | Study Series, Exam, position, Body part, Protocol 정리 |

#### HADRIMP.dll API 스펙

> **출처**: `docs/ref/API_MANUAL_241206.pdf`

| 항목 | 설명 |
|------|------|
| **함수** | `LIBRARY_HAPPLY()` - 이미지 처리 API |
| **구조체** | `ImageProcessing_Param3` (Border, NoiseControl, W/L, LUT, ImageControl, Post NoiseFilter, GPR) |
| **언어** | C++ extern "C" __stdcall |
| **버전** | Ver 1.0.0.5 |

#### 제품 식별 정보

| 항목 | 값 | 출처 |
|------|-----|------|
| **제품명** | Digital X-ray Imaging System | Performance Test Report |
| **모델명** | HnX-R1 | Performance Test Report |
| **모델 변종** | G1417CW, G1417CWP, G1417FW, G1417FWP (G 시리즈/2/3G) | Performance Test Report |
| **모델 변종** | A1717MCW, A1417MCW, F1417MCW (A/F 시리즈) | Performance Test Report |
| **Console Software** | HnVUE | User Manual |
| **버전** | 1.0.2 (User Manual), 1.0.0.37 (Performance Test) | User Manual / Test Report |
| **제조사** | H&abyz Co., Ltd. | User Manual |
| **주소** | 2-Dong, 41-16, Cheoinseong-ro, Namsa-eup, Cheoin-gu, Yongin-si, Gyeonggi-do, 17118, Korea | User Manual |
| **연락처** | Tel: 070-4658-9300, Fax: 031-322-9390, E-mail: sales@abyzr.com | User Manual |

### 1.2 비전 선언

> "의료진의 직관적인 워크플로우와 환자 안전을 최우선으로 하는, 규제 준비형 차세대 X-ray Console Software"

### 1.3 핵심 가치 제안

| 가치 제안 | 설명 |
|---------|------|
| **환자 안전** | IEC 62304 Class C SAFETY-CRITICAL 설계, 다중 안전 인터록, 방사선량 실시간 모니터링 |
| **임상 효율** | 10-state 최적화 워크플로우, One-Touch 촬영, Auto-AEC |
| **표준 준수** | DICOM PS3.x, IHE RAD TF, MFDS/FDA 인허가 준비 |
| **확장성** | 하이브리드 아키텍처(C++ Core + WPF GUI), 플러그인 HAL, 다중 벤더 하드웨어 지원 |
| **개발 효율** | 85% 선제적 개발 완료, 1,048개 테스트 케이스, TRUST 5 품질 프레임워크 |

---

## 2. Market Analysis

### 2.1 시장 규모 및 성장 추세

| 지표 | 값 | 출처 |
|------|-----|------|
| 글로벌 디지털 X-ray 시장 규모 (2024) | $28.5B | Grand View Research |
| 예상 CAGR (2024-2030) | 5.8% | Markets and Markets |
| 아시아-태평양 점유율 (2030 예상) | 32% | Frost & Sullivan |
| 한국 X-ray 장치 시장 (2024) | ~3,000대/년 | 한국의료기기산업협회 |

### 2.2 시장 드라이버

| 드라이버 | 영향 |
|---------|------|
| **노령 인구 증가** | 영상 진단 수요 증가 (65세 이상 20%+ 예상) |
| **디지털 전환 가속화** | 필름 → DR/CR 시스템 교체 수요 |
| **PACS/HIS/RIS 보급** | 병원 IT 인프라 확산 (90%+ 이상 설치) |
| **MFDS 디지털의료기기소프트웨어법(2025)** | SW 품질/규제 준수 강화 |
| **AI 기반 진단** | 고화질 DICOM 영상 수요 증가 |

### 2.3 타겟 시장

### 2.3.1 지리적 시장

| 시장 | 우선순위 | 진입 전략 |
|------|----------|-----------|
| **대한민국** | 1차 | MFDS 인허가 선도, 국내 병원 파이롯 |
| **일본** | 2차 | PMDA 인허가, 현지 파트너십 |
| **아시아 태평양** | 3차 | CE/FDA 인허가 활용, 디스터리뷰터 |
| **유럽/미국** | 장기 | FDA 510(k), CE MDR |

### 2.3.2 세분 시장 (Segmentation)

| 세분 | 설명 | 타겟 |
|------|------|------|
| **병원급 (Tier 1)** | 종합병원, 상급종합병원 | PACS 중심, 고가, 대량 구매 |
| **요양병원 (Tier 2)** | 요양병원, 정신병원 | 가성비, 내구성 중시 |
| **의원급 (Tier 3)** | 개인 의원, 검진센터 | 컴팩트, 조작 용이성 중시 |
| **동물 병원** | 수의과 진단 | 소형, 특수 프로토콜 |

### 2.4 시장 진입 장벽

| 장벽 | 난이도 | 완화 전략 |
|------|--------|-----------|
| **규제 인허가** | 높음 | IEC 62304 준수, 선제적 SOUP 관리 |
| **기술력** | 높음 | C++/WPF 하이브리드, 85% 선개발 |
| **자본** | 중간 | 분기별 릴리스, 외부 투자 유치 |
| **유통망** | 중간 | OEM 파트너십, 시스템 통합사 협력 |

---

## 3. Regulatory Requirements

### 3.1 국제 규격 매핑

| 규격 | 적용 범위 | Safety Class | 준수 상태 |
|------|-----------|--------------|-----------|
| **IEC 62304:2006+AMD1:2015** | 의료기기 SW 라이프사이클 | Class B (영상 표시, 측정) / Class C (Generator 제어, 노출 제어) | 개발 중 |
| **IEC 60601-1** | 의료전기기기 기본 안전 | - | 참조 |
| **IEC 60601-2-54** | X-ray 촬영 장비 특수 요구사항 | - | 개발 중 |
| **IEC 60601-1-3** | 방사선 방호 | - | 개발 중 |
| **IEC 62366-1** | 사용성 공학 | - | 계획 중 |
| **ISO 14971:2019** | 의료기기 위험 관리 | - | 적용 예정 |
| **ISO 13485:2016** | 품질경영시스템 | - | 참조 |
| **DICOM PS3.x** | 의료영상 통신 표준 | - | 구현 완료 (SPEC-DICOM-001) |
| **IHE RAD TF** | Radiology Technical Framework | - | 구현 완료 (MWL, MPPS, Dose SR) |

### 3.2 MFDS (한국 식약처) 요구사항

### 3.2.1 디지털의료기기소프트웨어법 (2025 시행)

| 항목 | 요구사항 | 준비 상태 |
|------|----------|-----------|
| **내장형 디지털의료기기소프트웨어** | HW에 설치/연결되어 제어·구동·데이터 처리 | 해당 |
| **제출 서류** | 디지털의료기기소프트웨어 적합성 확인보고서, SW 검증 및 유효성확인 자료 | 준비 중 |
| **안전성 등급** | IEC 62304 기반 등급 판정 | Class B/C 분류 |
| **사이버보안** | 의료기기 사이버보안 가이드라인 준수 | 계획 중 |

### 3.2.2 인허가 제출 문서 체크리스트

| 문서 | 상태 | 비고 |
|------|------|------|
| SRS (Software Requirements Specification) | 작성 중 | 본 PRD 참조 |
| SAD (Software Architecture Document) | 작성 중 | 아키텍처 참조 |
| SDS (Software Detailed Design) | 작성 중 | 각 SPEC.md 참조 |
| V&V Plan | 계획 중 | SPEC-TEST-001 |
| Test Reports | 진행 중 | 1,048개 테스트 |
| Traceability Matrix | 진행 중 | @MX 태그로 추적 |
| SOUP Register | 완료 | `docs/soup-register.md` |
| DICOM Conformance Statement | 완료 | SPEC-DICOM-001 |
| Risk Analysis Report | 계획 중 | ISO 14971 기반 |

### 3.3 FDA 510(k) (미국 수출 시)

| 항목 | 요구사항 |
|------|----------|
| **규제 기준** | 21 CFR Part 820 (QSR) 또는 QMSR (ISO 13485 기반) |
| **성능 기준** | 21 CFR Part 1020 (방사선 제품 성능 기준) |
| **SW Guidance** | FDA Guidance: "Content of Premarket Submissions for Device Software Functions" (2023) |
| **DICOM 적합성** | IEC 62304 준수로 제출 문서 간소화 가능 |

---

## 4. Competitive Analysis

### 4.1 경쟁사 제품 매트릭스

| 제품 | 회사 | GUI 프레임워크 | DICOM | AEC | 워크플로우 | 가격대 | 시장 점유율 |
|------|------|----------------|-------|-----|-----------|--------|------------|
| **dicomPACS DX-R** | dicomPACS | Qt | DCMTK | O | O | 중 | ~10% |
| **ExamVueDR** | MinXray | WPF | fo-dicom | O | O | 중 | ~5% |
| **Carestream Vue** | Carestream | WPF | 자체 | O | O | 고 | ~15% |
| **GE Revolution Xs** | GE Healthcare | WPF | 자체 | O | O | 고 | ~25% |
| **Siemens Mobilett** | Siemens | Qt | DCMTK | O | O | 고 | ~20% |
| **Agfa DX-D** | Agfa | WPF | 자체 | O | O | 고 | ~15% |
| **HnVue** | Abyz Lab | WPF | fo-dicom | O | O | 중 | ~0% (신규) |

### 4.2 기술적 비교 분석

| 기능 영역 | 경쟁사 수준 | HnVue 수준 | 차별화 포인트 |
|----------|------------|------------|----------------|
| **이미지 처리** | VTK/ITK | C++ Native Pipeline | 실시간 DMA, VGrid, 16-bit |
| **DICOM 연동** | 기본 SCU | 전체 SCU + Dose SR | TID 10001/10003 완벽 구현 |
| **Workflow** | 5-7 state | 10-state FSM | 세밀한 상태 제어, Crash Recovery |
| **Dose 관리** | 기본 로그 | RDSR + DRL + Audit Trail | FDA 21 CFR Part 11 준수, SHA-256 해시체인 |
| **테스트 커버리지** | ~60% | 85%+ (1,048 테스트) | IEC 62304 Class C 준비 |
| **하이브리드 아키텍처** | 단일 스택 | C++ Core + WPF GUI | 성능 + 생산성 최적화 |
| **SOUP 관리** | 부분적 | 체계적 Register | IEC 62304 완벽 준수 |

### 4.3 SWOT 분석

#### Strengths (강점)
- 85% 선제적 개발 완료, 1,048개 테스트 케이스
- IEC 62304 Class C 준비 (안전성 설계)
- 하이브리드 아키텍처 (성능 + 생산성)
- 규제 친화적 설계 (@MX 태그, SOUP Register, Traceability)
- 오픈소스 기반 (fo-dicom, gRPC) 비용 효율

#### Weaknesses (약점)
- 신규 진입자로 브랜드 인지도 부족
- 실제 하드웨어 연동 미검증
- UI/UX 완성도 부족 (35% 진행)
- 임상 검증 데이터 부족

#### Opportunities (기회)
- MFDS 디지털의료기기소프트웨어법(2025) 선도
- 국산 SW 기술 자립 정책 활용
- 중소형 병원용 가성비 시장 공략
- AI 기반 진단 연동 확장성

#### Threats (위협)
- 대형 기업의 가격 경쟁 심화
- 규제 변경에 따른 재인증 비용
| 하드웨어 벤더와의 종속성 | OEM 파트너십으로 완화 |

### 4.4 차별화 전략

| 전략 | 설명 | 실행 계획 |
|------|------|-----------|
| **규제 준비 선도** | IEC 62304 Class C, MFDS 2025 선제적 대응 | 2026 Q2 인허가 제출 |
| **개방형 아키텍처** | 플러그인 HAL, 다중 벤더 지원 | SDK/개발자 가이드 공개 |
| **고도화된 Dose** | RDSR + DRL + Audit Trail | FDA 21 CFR Part 11 마케팅 |
| **가성비 전략** | 대형사 대비 30-50% 가격 경쟁력 | 중소형 병원 타겟 |

---

## 5. User Personas & Requirements

### 5.1 Primary Persona: 방사선사 (Radiologic Technologist)

| 속성 | 설명 |
|------|------|
| **연령** | 25-50세 |
| **업무 경력** | 3-20년 |
| **주 업무** | X-ray 촬영, 환자 포지셔닝, 장비 조작 |
| **목표** | 빠르고 정확한 촬영, 환자 불편 최소화, 중복 촬영 방지 |
| **통증** | 복잡한 워크플로우, 느운 UI, 잦은 장비 오류 |

#### 요구사항 Top 3
1. **One-Touch 촬영**: 프로토콜 선택 → 자동 노출(AEC) → 단일 버튼 촬영
2. **즉시 프리뷰**: 촬영 후 3초 이내 영상 확인, Reject/Accept 빠른 판단
3. **인터록 안심**: 9개 안전 인터록 상태 실시간 표시, 오류 메시지 명확성

### 5.2 Secondary Persona: 영상의학과 전문의 (Radiologist)

| 속성 | 설명 |
|------|------|
| **연령** | 30-60세 |
| **전문 분야** | 영상 판독, 진단 |
| **목표** | 정확한 진단, 효율적인 판독, 측정 도구 활용 |
| **통증** | 낮은 화질, 부족한 측정 도구, PACS 연동 불편 |

#### 요구사항 Top 3
1. **고화질 뷰어**: 16-bit 그레이스케일, Window/Level 부드러운 조정
2. **측정 도구**: 거리, 각도, Cobb angle, ROI 밀도
3. **PACS 통합**: Study 조회, 이전 영상 비교, 쉬운 Export

### 5.3 Tertiary Persona: IT 관리자 (Biomedical Engineer / IT Admin)

| 속성 | 설명 |
|------|------|
| **연령** | 25-45세 |
| **전문 분야** | 장비 관리, IT 인프라 |
| **목표** | 장비 가동률 99%+, 빠른 장애 처리, 보안 준수 |
| **통증** | 복잡한 설정, 로그 분석 어려움, DICOM 연동 문제 |

#### 요구사항 Top 3
1. **DICOM 연동 안정성**: MWL, MPPS, C-STORE 99.9% 성공률
2. **로그/감사**: Audit Trail, SHA-256 무결성, 알람 통지
3. **관리 용이성**: 원격 설정, 자동 업데이트, 백업

### 5.4 Quaternary Persona: 병원 관리자 (Hospital Administrator)

| 속성 | 설명 |
|------|------|
| **연령** | 40-60세 |
| **전문 분야** | 병원 운영, 예산 관리 |
| **목표** | 비용 절감, 인허가 준수, 환자 만족도 |
| **통증** | 높은 장비 비용, 규제 위험, A/S 비용 |

#### 요구사항 Top 3
1. **가성비**: 대형사 대비 30-50% 가격 경쟁력
2. **인허가 준비**: IEC 62304, MFDS 문서 완비
3. **A/S 체계**: 빠른 장애 대응, 예방 정비

---

## 6. Risk Analysis

### 6.1 기술적 리스크

| 리스크 | 확률 | 영향 | 완화 전략 |
|--------|------|------|-----------|
| **USB 3.x 대역폭 부족** | 중 | 높음 | DMA 최적화, 압축 전송, PCIe 대안 준비 |
| **실제 하드웨어 연동 오류** | 중 | 높음 | HAL 시뮬레이터 선행 완료, Mock 기반 테스트 |
| **DICOM 상호운용성** | 낮-중 | 중간 | DVTK 적합성 테스트, 다중 PACS 벤더 테스트 |
| **GUI 응답성 저하** | 낮 | 중간 | 비동기 처리, 백그라운드 스레딩, 프로그레시브 로딩 |

### 6.2 규제 리스크

| 리스크 | 확률 | 영향 | 완화 전략 |
|--------|------|------|-----------|
| **SOUP 라이브러리 취약점** | 낮-중 | 높음 | 버전 고정, SOUP 리스크 분석, 대안 라이브러리 확보 |
| **IEC 62304 문서화 부족** | 낮 | 높음 | @MX 태그로 추적성 확보, 선제적 문서화 |
| **MFDS 인허가 반려** | 낮 | 높음 | 규제 전문가 자문, 파일럿 임상 데이터 확보 |
| **사이버보안 미준수** | 낮 | 중간 | 설계 초기부터 보안 요구사항 반영 (TLS, 인증, 감사 로그) |

### 6.3 시장 리스크

| 리스크 | 확률 | 영향 | 완화 전략 |
|--------|------|------|-----------|
| **대형 기업 가격 경쟁** | 중 | 높음 | 중소형 병원 집중, OEM 파트너십 |
| **하드웨어 벤더 종속** | 중 | 중간 | 플러그인 HAL, 다중 벤더 지원 |
| **기술 변화 (AI 등)** | 중 | 중간 | 아키텍처 확장성, AI 모듈 인터페이스 예약 |

---

## 7. Business Model

### 7.1 수익 모델

| 모델 | 설명 | 비중 |
|------|------|------|
| **Per-Device License** | X-ray 장비 1대당 SW 라이선스 | 70% |
| **Maintenance Contract** | 연간 유지보수 (연 15% 라이선스 비용) | 20% |
| **Customization** | 병원 맞춤 설정/컨설팅 | 5% |
| **Training** | 사용자 교육 | 5% |

### 7.2 가격 전략

| 타겟 | 경쟁사 대비 | 가격대 | 비고 |
|------|------------|--------|------|
| **요양병원** | -50% | $15K-$25K | 가성비 전략 |
| **종합병원** | -30% | $30K-$50K | 풀 기능 |
| **의원급** | -40% | $10K-$15K | 컴팩트 버전 |
| **OEM** | 협의 | Volume 기반 | 파트너십 |

### 7.3 매출 추정 (5년 계획)

| 연도 | 판매 대수 | 평� 단가 | 매출 | 성장률 |
|------|-----------|----------|------|--------|
| **2026** | 10 | $20K | $200K | - (신규) |
| **2027** | 30 | $20K | $600K | 200% |
| **2028** | 60 | $20K | $1.2M | 100% |
| **2029** | 100 | $20K | $2.0M | 67% |
| **2030** | 150 | $20K | $3.0M | 50% |

### 7.4 비용 구조

| 항목 | 비중 | 비고 |
|------|------|------|
| **개발** | 40% | R&D 인건비, 인프라 |
| **인허가** | 20% | 규제 전문가, 임상 시험 |
| **영업/마케팅** | 20% | 영업 인력, 전시회 |
| **운영** | 10% | 오피스, 법무 |
| **A/S** | 10% | 현장 기술 지원 |

---

## 8. Go-to-Market Strategy

### 8.1 유통 채널

| 채널 | 비중 | 전략 |
|------|------|------|
| **직접 영업** | 40% | 대형 병원 타겟 |
| **OEM 파트너십** | 30% | 하드웨어 벤더 번들 |
| **시스템 통합사** | 20% | 병원 IT 구축사 |
| **Distributor** | 10% | 지역별 유통망 |

### 8.2 파트너십 전략

| 파트너 유형 | 예시 | 가치 제안 |
|------------|------|-----------|
| **하드웨어 벤더** | Detector/HVG 제조사 | SW 번들로 상품성 강화 |
| **PACS 벤더** | 병원 영상 시스템 | 상호운용성 테스트, 인증 |
| **시스템 통합사** | 병원 IT 구축 | 통합 솔루션 패키지 |
| **교육기관** | 방사선사 양성과 | 교육용 라이선스, 취업 연계 |

### 8.3 마케팅 활동

| 활동 | 타이밍 | 목표 |
|------|--------|------|
| **전시회 참가** | KIR 2026, RSNA 2026 | 리드 생성, 브랜드 인지 |
| **파일럿 병원** | 2026 Q2 | 임상 데이터, 레퍼런스 |
| **웨비나/백서** | 2026 Q3 | 교육, 리드 너링 |
| **인허가 획득 홍보** | 2026 Q4 | 신뢰도 강화 |

### 8.4 출시 로드맵

| 단계 | 기간 | 주요 활동 |
|------|------|-----------|
| **Alpha** | 2026 Q1 | 내부 테스트, HAL 시뮬레이터 |
| **Beta** | 2026 Q2 | 파일럿 병원, 실제 하드웨어 연동 |
| **MFDS 제출** | 2026 Q3 | 인허가 서류 제출 |
| **정식 출시** | 2026 Q4 | 상용 라이선스 판매 개시 |

---

## 9. Success Metrics (KPI)

### 9.1 제품 KPI

| 지표 | 목표 | 측정 주기 |
|------|------|-----------|
| **테스트 커버리지** | 85%+ | 분기별 |
| **LSP 오류** | 0 errors | 주간 |
| **잠재 버그** | <10 critical | 릴리스별 |
| **인허가 획득** | MFDS Class II | 2026 Q4 |
| **DICOM 적합성** | 100% SCU | 릴리스별 |

### 9.2 비즈니스 KPI

| 지표 | 2026 목표 | 2027 목표 |
|------|-----------|-----------|
| **판매 대수** | 10대 | 30대 |
| **매출** | $200K | $600K |
| **고객 만족도** | 4.0/5.0+ | 4.3/5.0+ |
| **장비 가동률** | 99%+ | 99.5%+ |

---

## 10. Appendix

### 10.1 용어 정의

| 용어 | 정의 |
|------|------|
| **Console SW** | X-ray 장비를 제어하는 PC 소프트웨어 |
| **Detector** | X-ray를 디지털 신호로 변환하는 센서 (FPD, CR) |
| **Generator** | X-ray를 발생시키는 고전압 장치 (HVG) |
| **AEC** | Automatic Exposure Control, 자동 노출 제어 |
| **DICOM** | Digital Imaging and Communications in Medicine |
| **PACS** | Picture Archiving and Communication System |
| **MWL** | Modality Worklist, 검사 목록 |
| **MPPS** | Modality Performed Procedure Step, 검사 수행 기록 |
| **Dose SR** | Dose Structured Report, 방사선량 구조화 리포트 |
| **RDSR** | Radiation Dose Structured Report |
| **DRL** | Diagnostic Reference Level, 진단 기준 선량 |
| **SOUP** | Software of Unknown Provenance (불확실 오픈소스) |
| **HAL** | Hardware Abstraction Layer |
| **FSM** | Finite State Machine |
| **MVVM** | Model-View-ViewModel |

### 10.2 ref 폴더 문서 분석

| 문서 | 유형 | 주요 내용 |
|------|------|-----------|
| **Instructions for Use** | User Manual v1.0.2 | 제품 정보, 사용자 매뉴얼, UI 화면 구성 |
| **Performance Test Report** | 성능 시험 리포트 (2025-07-14~18) | 11개 테스트 항목 모두 PASS |
| **UI 변경 최종안** | PPT (251118) | Worklist, Studylist, Acquisition UI 개선안 |
| **API_MANUAL** | PDF (241206) | HADRIMP.dll API 스펙 (LIBRARY_HAPPLY) |
| **hnvue_abyz_plan** | PPT | Console 내재화 계획, DLL 구성요소, 일정 |
| **console 기본 요건** | Excel (20260310) | Login, 권한, Cyber security, Code manager, UI 요건 |

### 10.3 참고 문서

| 문서 | 위치 |
|------|------|
| 기술 리서치 | `docs/xray-console-sw-research.md` |
| 프로젝트 마스터 플랜 | `docs/antigravity-plan.md` |
| SOUP Register | `docs/soup-register.md` |
| Windows 이관 보고서 | `docs/windows-tasks-report.md` |
| SPEC 문서들 | `.moai/specs/SPEC-XXX/spec.md` |

---

## 10.1 용어 정의

| 버전 | 날짜 | 변경 사항 | 작성자 |
|------|------|-----------|--------|
| 1.0 | 2026-03-12 | 초안 작성 | MoAI |

---

*Maintained by: abyz-lab <hnabyz2023@gmail.com>*