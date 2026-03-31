# eSTAR v6.1 제출 가이드 (eSTAR Submission Guide)

> **문서 번호**: HnVue-E04
> **버전**: 0.1 (Draft)
> **작성일**: ____-__-__
> **작성자**: ____________
> **검토자**: ____________
> **승인자**: ____________
> **제품**: HnVue Console SW
> **회사**: H&abyz Co., Ltd. (에이치앤아비즈)
> **분류**: ✅ 최소 필수 — 2024년부터 FDA는 510(k) 제출 시 eSTAR 형식을 의무화. 구 형식(iSubmitter 등)은 더 이상 수용되지 않는다. eSTAR가 없으면 FDA에 510(k)를 제출할 수 없다.
> **적용 시장**: FDA (전용)
> **근거 규격**: FDA eSTAR v6.1 (Electronic Submission Template and Resource), 21 CFR §807 Subpart E, FDA SW Guidance (2023), FDA Cybersecurity Guidance (2023)

---

> ℹ️ **문서 성격**: 본 문서는 실제 eSTAR Excel 파일이 아닌, eSTAR 각 섹션에 어떤 HnVue 산출물을 매핑·첨부해야 하는지를 정리한 **제출 준비 가이드**이다. 실제 제출은 FDA eSTAR v6.1 Excel 파일을 FDA 웹사이트에서 다운로드하여 작성한다.

---

## 1. eSTAR 개요

### 1.1 eSTAR란?

eSTAR(Electronic Submission Template and Resource)는 FDA CDRH가 510(k) 사전 시장 신고 제출을 위해 제공하는 전자 제출 양식(Excel 기반)이다. 2024년부터 510(k) 전자 제출에 eSTAR 형식이 의무화되었다.

### 1.2 eSTAR 다운로드 및 버전 확인

- 다운로드: https://www.fda.gov/medical-devices/premarket-notification-510k/estar-electronic-submission-template-and-resource
- **버전 확인 필수**: FDA는 수시로 eSTAR를 업데이트한다. 제출 직전 최신 버전(v6.1 이상)인지 반드시 확인.
- 버전 불일치 시 FDA가 제출 거부 또는 Hold 처리 가능.

### 1.3 제출 정보

| 항목 | 내용 |
|---|---|
| 제출 유형 | 510(k) Premarket Notification |
| 규제 분류 | 21 CFR 892.2050 |
| 제품 코드 | LLZ |
| 제출 기관 | FDA CDRH, Office of In Vitro Diagnostics and Radiological Health (OIR) |
| 전자 제출 방법 | FDA Electronic Submissions Gateway (ESG) |
| ESG URL | https://esg.fda.gov |
| 주 연락 담당자 | [RA 담당자 이름, 이메일, 전화] |
| US Agent | [미국 대리인 이름, 주소, 연락처] |

---

## 2. eSTAR 섹션 구성 및 HnVue 산출물 매핑

### 2.1 전체 섹션 매핑표

| eSTAR 섹션 | 섹션 이름 | HnVue 대응 산출물 | 작성 상태 | 제출 파일명 |
|---|---|---|---|---|
| **Cover** | 커버 레터 | (본 문서 §3 템플릿) | [____] | eCover.pdf |
| **Section 1** | 제출 정보 (Administrative) | eSTAR 내 직접 입력 | [____] | — (eSTAR 내 입력) |
| **Section 2** | 연락처 정보 | eSTAR 내 직접 입력 | [____] | — |
| **Section 3** | 기기 설명 (Device Description) | HnVue-A02 (SRS) 일부 + HnVue-E02 (IFU §4) | [____] | HnVue-E02-DeviceDesc.pdf |
| **Section 4** | 규제 이력 (Regulatory History) | N/A (신규 기기, 이전 허가 이력 없음) | N/A | — |
| **Section 5** | 의도된 사용 (Intended Use) | HnVue-E02 §Section 1 (IFU) | [____] | HnVue-E02-IntendedUse.pdf |
| **Section 6** | 현행 기기 변경 사항 | N/A (신규 기기) | N/A | — |
| **Section 7** | 실질적 동등성 (SE Comparison) | HnVue-E01 (Predicate 비교표), HnVue-B05 | [____] | HnVue-E01-Predicate.pdf |
| **Section 8** | 성능 시험 (Performance Testing) | HnVue-B02 (시스템 시험 결과서) | [____] | HnVue-B02-ST.pdf |
| **Section 9** | 생체적합성 (Biocompatibility) | N/A (SW 전용, 신체 접촉 없음) | N/A | — |
| **Section 10** | 멸균 및 포장 (Sterility/Packaging) | N/A (SW 전용, 전자 배포) | N/A | — |
| **Section 11** | 라벨링 (Labeling) | HnVue-E02 (라벨링/IFU — 전체) | [____] | HnVue-E02-Labeling.pdf |
| **Section 12** | SW 문서 (Software Documentation) | HnVue-A01~A08, HnVue-B01~B05 (Group A+B 전체) | [____] | (§2.2 상세 참조) |
| **Section 13** | 전자기 호환성 (EMC) | N/A (SW 전용) | N/A | — |
| **Section 14** | 위험 관리 (Risk Management) | HnVue-D01 (위험 관리 파일) | [____] | HnVue-D01-RMF.pdf |
| **Section 15** | 사이버보안 (Cybersecurity) | HnVue-C01~C07 (Group C 전체) | [____] | (§2.3 상세 참조) |
| **Section 16** | 기타 (Miscellaneous) | 해당 시 추가 | — | — |

---

### 2.2 Section 12 (SW 문서) 상세 매핑 — FDA SW Guidance 대응

FDA 2023 Software Guidance ("Guidance for the Content of Premarket Submissions for Device Software Functions") 대응.

| SW 문서 요건 항목 | 제출 수준 (Basic/Enhanced) | 대응 산출물 | 제출 파일명 |
|---|---|---|---|
| SW 설계 수준 판단 근거 (Minor/Major/Enhancement) | Basic | HnVue-A01 (SDP §SW 변경 수준) | HnVue-A01-SDP.pdf |
| SW 개발 계획 | Basic | HnVue-A01 (SDP) | HnVue-A01-SDP.pdf |
| 아키텍처 다이어그램 | Basic | HnVue-A03 (SAD) | HnVue-A03-SAD.pdf |
| SW 요구사항 명세 | Basic | HnVue-A02 (SRS) | HnVue-A02-SRS.pdf |
| 통합 시험 계획 및 결과 | Basic | HnVue-B01 (통합 시험) | HnVue-B01-IT.pdf |
| 시스템 시험 계획 및 결과 | Basic | HnVue-B02 (시스템 시험) | HnVue-B02-ST.pdf |
| 요구사항 추적성 매트릭스 (RTM) | Basic | HnVue-B03 (RTM) | HnVue-B03-RTM.pdf |
| 알려진 결함 목록 (Anomalies) | Basic | HnVue-A08 (결함 목록) | HnVue-A08-Anomalies.pdf |
| 릴리즈 버전 정보 | Basic | HnVue-A06 (릴리즈 기록) | HnVue-A06-Release.pdf |
| SOUP 목록 | Basic | HnVue-A04 (SOUP 목록) | HnVue-A04-SOUP.pdf |
| 형상 관리 계획서 | Basic | HnVue-A05 (형상관리 계획) | HnVue-A05-CM.pdf |
| 사용적합성 평가 | Basic | HnVue-B04 (사용적합성) | HnVue-B04-Usability.pdf |

> **SW 설계 수준 판단**: HnVue는 신규 개발 제품으로 "Major Level" 해당. Basic Level 문서 요건 전체 적용. SOUP에 의존성 있는 경우 Enhanced Level 추가 요건 확인.

---

### 2.3 Section 15 (사이버보안) 상세 매핑 — FDA §524B 대응

FDA 2023 Cybersecurity Guidance ("Cybersecurity in Medical Devices: Quality System Considerations and Content of Premarket Submissions") 대응.

| 사이버보안 요건 (FDA §524B) | 대응 산출물 | 제출 파일명 | 형식 |
|---|---|---|---|
| 사이버보안 설계 및 통제 계획 | HnVue-C03 (8대 보안 통제 명세서) | HnVue-C03-Controls.pdf | PDF |
| SBOM (Software Bill of Materials) | HnVue-C01 (SBOM) | HnVue-C01-SBOM.json | **JSON (기계 판독 형식)** |
| VEX (Vulnerability Exploitability eXchange) | HnVue-C02 (VEX) | HnVue-C02-VEX.json | **JSON (기계 판독 형식)** |
| 위협 모델링 | HnVue-C04 (위협 모델) | HnVue-C04-ThreatModel.pdf | PDF |
| 보안 위험 평가 | HnVue-C05 (보안 위험 평가) | HnVue-C05-SecRisk.pdf | PDF |
| 침투 테스트 결과 | HnVue-C06 (침투 테스트 결과서) | HnVue-C06-PenTest.pdf | PDF |
| 취약점 관리 계획 (VMP) | HnVue-C07 (취약점 관리 계획) | HnVue-C07-VMP.pdf | PDF |

> ⚠️ **SBOM/VEX 제출 형식 주의**: FDA는 SBOM과 VEX를 JSON 또는 XML 기계 판독 형식으로 제출하도록 요구한다. PDF로 변환하지 말고 원본 CycloneDX JSON 파일 그대로 첨부.

---

## 3. 커버 레터 (Cover Letter) 템플릿

```
[일자: ____년 __월 __일]

Center for Devices and Radiological Health
Food and Drug Administration
10903 New Hampshire Avenue
Silver Spring, MD 20993

Re: 510(k) Premarket Notification for HnVue Console Software
    Product Code: LLZ
    21 CFR: 892.2050
    IEC 62304 Level: Class B / Basic

Dear FDA Reviewer:

H&abyz Co., Ltd. (hereinafter "H&abyz") is pleased to submit 
this 510(k) Premarket Notification for HnVue Console Software 
("HnVue"), a Class II medical device software intended for the 
acquisition, display, processing, storage, and transmission of 
digital radiography (DR) images by licensed healthcare 
professionals.

HnVue is substantially equivalent to:
  - Vieworks VXvue (K251410) — Primary Predicate
  - DRTECH EConsole1 (K231225) — Secondary Predicate
  - Rayence XmaruView (K160579) — Supporting Predicate

The device is classified under 21 CFR 892.2050 (Product Code: 
LLZ) as a Class II device. This submission is prepared in 
accordance with FDA eSTAR v6.1 guidelines and the following 
applicable guidance documents:
  - Guidance for the Content of Premarket Submissions for 
    Device Software Functions (2023)
  - Cybersecurity in Medical Devices: Quality System 
    Considerations and Content of Premarket Submissions (2023)
  - Applying Human Factors and Usability Engineering to Medical 
    Devices (2016)

This submission includes all required sections of eSTAR v6.1.
Sections 4, 6, 9, 10, and 13 are marked N/A as HnVue is a 
standalone software-only device with no hardware components, 
no patient contact, and electronic distribution only.

Pursuant to Section 524B of the FD&C Act (as amended by the 
Omnibus Appropriations Act of 2023), this submission includes 
a complete cybersecurity documentation package including SBOM, 
VEX, threat model, security risk assessment, penetration test 
results, and vulnerability management plan.

For questions regarding this submission, please contact:

  [RA 담당자 이름]
  [직책], Regulatory Affairs
  H&abyz Co., Ltd.
  [주소], Republic of Korea
  Tel: [전화번호]
  Email: [이메일]

  U.S. Agent:
  [미국 대리인 이름]
  [주소], [미국 주]
  Tel: [전화번호]
  Email: [이메일]

Sincerely,

________________________________
[서명]
[이름]
[직책], H&abyz Co., Ltd.
```

---

## 4. QMSR (Quality Management System Regulation) 대응

FDA는 2026년부터 기존 QSR(21 CFR §820)을 ISO 13485:2016 기반의 QMSR로 전환한다.

| QMSR 요건 항목 | 대응 내부 문서 | 비고 |
|---|---|---|
| QMS 체계 | 내부 QMS 절차서 | ISO 13485:2016 기반 |
| 설계 관리 (Design Controls) | HnVue-A01~A08 (Group A) | 21 CFR §820.30 대응 |
| 위험 관리 통합 | HnVue-D01 (위험 관리 파일) | ISO 14971 적용 |
| 문서 관리 | 내부 문서 관리 절차서 | |
| 기록 관리 | 내부 기록 관리 절차서 | |
| 교정 기록 | 측정 장비 교정 기록 | 시험 장비 해당 시 |

> HnVue는 SW 전용 기기로 물리적 생산 공정 없음. QSR §820.70(생산·공정 관리), §820.65(컴포넌트 추적성) 등 하드웨어 제조 관련 조항은 SW 개발 공정으로 대응.

---

## 5. N/A 섹션 처리 지침

eSTAR에서 N/A로 처리하는 섹션에 대해 반드시 명확한 이유를 기재한다.

| 섹션 | N/A 처리 사유 (eSTAR 내 기재 문구 예시) |
|---|---|
| Section 4 (Regulatory History) | "HnVue Console Software is a new device with no prior FDA clearance, approval, or de novo classification." |
| Section 6 (Device Modifications) | "Not applicable. This is a new device with no modifications to a previously cleared device." |
| Section 9 (Biocompatibility) | "Not applicable. HnVue Console Software is a software-only device with no patient contact. No hardware components are included." |
| Section 10 (Sterility/Packaging) | "Not applicable. HnVue is distributed electronically (digital download). No physical packaging or sterilization is involved." |
| Section 13 (EMC) | "Not applicable. HnVue Console Software is a software-only device. The device does not generate or emit electromagnetic radiation. The PC platform on which HnVue operates is a commercially available product that complies with applicable EMC standards independently." |

---

## 6. eSTAR 제출 전 최종 체크리스트

### 6.1 내용 완성도 체크

| 항목 | 확인 여부 |
|---|---|
| 최신 eSTAR 버전 사용 (v6.1 이상) | ✅ / ❌ |
| 커버 레터 완성 및 서명 | ✅ / ❌ |
| 모든 N/A 섹션에 사유 기재 | ✅ / ❌ |
| Section 7 SE 비교표 완성 (E01) | ✅ / ❌ |
| Section 11 라벨링 최신 버전 (E02) | ✅ / ❌ |
| Section 12 전체 SW 문서 첨부 | ✅ / ❌ |
| Section 15 SBOM JSON 파일 첨부 | ✅ / ❌ |
| Section 15 VEX JSON 파일 첨부 | ✅ / ❌ |
| Section 14 위험 관리 파일 첨부 | ✅ / ❌ |

### 6.2 파일 형식 체크

| 파일명 | 요구 형식 | 준비 여부 |
|---|---|---|
| eCover.pdf | PDF | ✅ / ❌ |
| eSTAR-HnVue.xlsx | Excel (eSTAR 원본) | ✅ / ❌ |
| HnVue-A01-SDP.pdf | PDF | ✅ / ❌ |
| HnVue-A02-SRS.pdf | PDF | ✅ / ❌ |
| HnVue-A03-SAD.pdf | PDF | ✅ / ❌ |
| HnVue-A04-SOUP.pdf | PDF | ✅ / ❌ |
| HnVue-A05-CM.pdf | PDF | ✅ / ❌ |
| HnVue-A06-Release.pdf | PDF | ✅ / ❌ |
| HnVue-A08-Anomalies.pdf | PDF | ✅ / ❌ |
| HnVue-B01-IT.pdf | PDF | ✅ / ❌ |
| HnVue-B02-ST.pdf | PDF | ✅ / ❌ |
| HnVue-B03-RTM.pdf | PDF | ✅ / ❌ |
| HnVue-B04-Usability.pdf | PDF | ✅ / ❌ |
| HnVue-C01-SBOM.json | **JSON (기계 판독)** | ✅ / ❌ |
| HnVue-C02-VEX.json | **JSON (기계 판독)** | ✅ / ❌ |
| HnVue-C03-Controls.pdf | PDF | ✅ / ❌ |
| HnVue-C04-ThreatModel.pdf | PDF | ✅ / ❌ |
| HnVue-C05-SecRisk.pdf | PDF | ✅ / ❌ |
| HnVue-C06-PenTest.pdf | PDF | ✅ / ❌ |
| HnVue-C07-VMP.pdf | PDF | ✅ / ❌ |
| HnVue-D01-RMF.pdf | PDF | ✅ / ❌ |
| HnVue-E01-Predicate.pdf | PDF | ✅ / ❌ |
| HnVue-E02-Labeling.pdf | PDF | ✅ / ❌ |

### 6.3 ESG 제출 전 최종 확인

| 항목 | 확인 여부 |
|---|---|
| FDA ESG 계정 생성 완료 | ✅ / ❌ |
| eCopy 요건 충족 (파일 용량 1GB 이하) | ✅ / ❌ |
| 전자 서명 또는 적법한 서명 포함 | ✅ / ❌ |
| 제출 전 eSTAR 내부 유효성 검사 통과 | ✅ / ❌ |

---

## 7. 사이버보안 섹션 (Section 15) 상세 작성 가이드

FDA 2023 Cybersecurity Guidance 핵심 질문 대응:

| eSTAR §15 질문 | HnVue 대응 방법 | 근거 문서 |
|---|---|---|
| 15.1 기기의 사이버보안 위험 최소화 방법은? | 8대 보안 통제(TLS, RBAC, 코드서명 등) 적용 설명 | HnVue-C03 |
| 15.2 SBOM을 제공하는가? | 예. CycloneDX JSON 형식 첨부. | HnVue-C01 |
| 15.3 취약점을 어떻게 모니터링/관리하는가? | VMP에 따라 NVD/CISA 월 1회 조회, 패치 SLA 관리 | HnVue-C07 |
| 15.4 보안 업데이트 제공 계획은? | Critical: 14일, High: 30일, Medium: 90일 SLA | HnVue-C07 |
| 15.5 취약점 공시 정책(VDP)이 있는가? | 예. security@h-abyz.com, 90일 내 공시 목표 | HnVue-C03 §VDP |
| 15.6 사이버보안 위험 관리를 ISO 14971과 통합하였는가? | 예. 보안 위험을 HnVue-D01에 통합 관리 | HnVue-D01 §9 |

---

## 8. 변경 이력

| 버전 | 변경 일자 | 변경 내용 | 변경자 |
|---|---|---|---|
| 0.1 | ____-__-__ | 초안 작성 | |
| | | | |

## 9. 승인

| 역할 | 성명 | 서명 | 일자 |
|---|---|---|---|
| 작성자 | | | |
| 검토자 (RA — 510(k) 경험자) | | | |
| 승인자 | | | |

---

## 비고: 최소 필수 선택 근거 (개발팀 안내)

**왜 이 문서가 FDA 필수인가?**

FDA는 2024년부터 510(k) 전자 제출 시 eSTAR 형식을 의무화하였다. 구 형식(iSubmitter, Word/PDF 패키지)은 더 이상 수용되지 않는다. eSTAR 없이는 FDA에 510(k)를 제출할 수 없으므로 절대적으로 필수이다.

**eSTAR가 단순 양식이 아닌 이유**: eSTAR는 내부 유효성 검사(Validation Check) 기능이 있어, 필수 섹션 누락 또는 N/A 사유 미기재 시 제출이 차단된다. FDA 심사관이 eSTAR를 기반으로 심사를 진행하므로, 섹션 구성과 내용이 심사 속도에 직접적인 영향을 미친다.

**SW 전용 기기 제출의 장점**: HnVue는 SW 전용 기기로 생체적합성, EMC, 멸균/포장 등 다수 섹션이 N/A 처리된다. 이로 인해 전통적인 하드웨어 의료기기 대비 제출 패키지가 간결하다. 다만 SW 섹션(Section 12)과 사이버보안 섹션(Section 15)에 대한 상세한 기술이 요구된다.

**SBOM/VEX 기계 판독 형식 제출**: 2023년 이후 FDA는 SBOM과 VEX를 JSON 또는 XML 형식으로 제출하도록 요구한다. PDF 변환본은 수용되지 않으므로, CycloneDX JSON 원본 파일을 그대로 첨부해야 한다.
