# HnVue 단독 인허가 — 산출물 템플릿 패키지

> **제품**: HnVue Console SW  
> **회사**: H&abyz Co., Ltd. (에이치앤아비즈)  
> **작성일**: 2026-03-31  
> **총 산출물**: 28종 (최소 필수 22종 + 리스크 최소화 6종)

---

## 폴더 구조

```
hnvue-templates/
├── README.md                          ← 본 파일
├── A_SW_Design/                       ← Group A: SW 설계 및 개발 (8종)
│   ├── HnVue-A01_SDP.md              SW 개발 계획서
│   ├── HnVue-A02_SRS.md              SW 요구사항 명세서
│   ├── HnVue-A03_SAD.md              SW 아키텍처 설계서
│   ├── HnVue-A04_SOUP.md             SOUP 목록
│   ├── HnVue-A05_CMP.md              형상관리 계획서
│   ├── HnVue-A06_Release.md          SW 릴리즈 기록
│   ├── HnVue-A07_Build.md            소스코드 및 빌드 환경
│   └── HnVue-A08_KnownAnomalies.md   알려진 결함 목록
├── B_VnV/                             ← Group B: 검증 및 유효성 확인 (5종)
│   ├── HnVue-B01_IntegrationTest.md  SW 통합 시험 결과서
│   ├── HnVue-B02_SystemTest.md       SW 시스템 시험 결과서
│   ├── HnVue-B03_RTM.md              추적성 매트릭스
│   ├── HnVue-B04_Usability.md        사용적합성 평가
│   └── HnVue-B05_ClinicalEquivalence.md  임상 평가 / 동등성 비교
├── C_Cybersecurity/                   ← Group C: 사이버보안 (7종)
│   ├── HnVue-C01_SBOM.md             SBOM 관리 문서
│   ├── HnVue-C02_VEX.md              VEX 리포트
│   ├── HnVue-C03_SecurityControls.md 8대 보안 통제 명세서
│   ├── HnVue-C04_ThreatModel.md      위협 모델
│   ├── HnVue-C05_SecurityRiskAssessment.md  보안 위험 평가
│   ├── HnVue-C06_PenTest.md          침투 테스트 + Retest
│   └── HnVue-C07_VMP.md              취약점 관리 계획
├── D_Risk_Management/                 ← Group D: 위험 관리 (2종)
│   ├── HnVue-D01_RiskManagement.md   위험 관리 파일 (ISO 14971)
│   └── HnVue-D02_IEC81001.md         IEC 81001-5-1 준수 문서
├── E_Regulatory_Submission/           ← Group E: 인허가 제출 (4종)
│   ├── HnVue-E01_PredicateComparison.md  Predicate 비교표
│   ├── HnVue-E02_Labeling.md         라벨링 / IFU
│   ├── HnVue-E03_GSPR.md             GSPR 체크리스트
│   └── HnVue-E04_eSTAR.md            eSTAR v6.1 제출 가이드
└── F_PostMarket_Clinical/             ← Group F: 시판후/임상 (2종)
    ├── HnVue-F01_CER.md              임상 평가 보고서 (CER)
    └── HnVue-F02_PMS_PMCF.md         PMS/PMCF 통합 패키지
```

## 사용 방법

1. **최소 필수(22종)로 시작**: 각 파일의 헤더에 ✅ 최소 필수로 표기된 22종을 우선 작성
2. **리스크 최소화(+6종) 추가**: ⭐ 리스크 최소화로 표기된 6종을 EU MDR 대응 또는 FDA deficiency 방지 목적으로 추가
3. **빈칸 채우기**: `____` 또는 `[작성 필요]`로 표기된 부분을 실제 내용으로 채움
4. **비고란 참조**: 각 파일 하단의 "비고" 섹션에서 최소 필수/리스크 최소화 선택 근거 확인

## 작성 권장 순서

```
[1단계] A그룹 → SDP → SRS → SAD → SOUP → 형상관리
[2단계] D그룹 → 위험관리파일 (B/C 입력값 제공)
[3단계] B그룹 + C그룹 (병렬 진행 가능)
[4단계] A그룹 후반 → 릴리즈기록, 빌드환경, 결함목록
[5단계] D그룹 후반 → IEC 81001-5-1 (EU MDR 대비)
[6단계] E그룹 + F그룹 → eSTAR, 라벨링, CER, PMS
```

## 시장별 커버리지

| 시장 | 필수 | 리스크 최소화 | 합계 |
|------|:----:|:-----------:|:----:|
| FDA 510(k) | 22 | 3 | 25 |
| MFDS 2등급 | 17 | 6 | 23 |
| EU MDR Class IIa | 21 | 5 | 26 |

## 분류 범례

- ✅ **최소 필수**: FDA 510(k) 제출에 반드시 필요. 없으면 RTA(Refuse to Accept) 또는 Technical Screening Hold
- ⭐ **리스크 최소화**: FDA deficiency 방지 또는 EU MDR/MFDS 대응에 강력 권장. 없으면 추가 질의(AI) 또는 NB 지적 리스크

---

*본 템플릿 패키지는 HnVue Console SW 단독 인허가(Plan B) 전용으로 작성되었습니다.*
