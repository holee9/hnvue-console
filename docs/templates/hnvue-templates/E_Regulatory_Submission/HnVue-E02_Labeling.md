# 라벨링 / 사용설명서 (Labeling & IFU)

> **문서 번호**: HnVue-E02
> **버전**: 0.1 (Draft)
> **작성일**: ____-__-__
> **작성자**: ____________
> **검토자**: ____________
> **승인자**: ____________
> **제품**: HnVue Console SW
> **회사**: H&abyz Co., Ltd. (에이치앤아비즈)
> **분류**: ✅ 최소 필수 — FDA/MFDS/EU MDR 3개 시장 모두 라벨링 및 IFU를 법적 의무로 요구. SW 출시 전 반드시 확정. IFU 내용 자체가 사용적합성 평가(HnVue-B04) 대상이므로 초안을 조기 작성 후 사용성 테스트에 포함할 것.
> **적용 시장**: FDA / MFDS / EU MDR (각 시장별 버전 분리)
> **근거 규격**: 21 CFR §801, EU MDR 2017/745 Annex I Chapter III §23, MFDS 의료기기 표시기재 등에 관한 규정, IEC 62366-1:2015/AMD1:2020 §5.9

---

> ⚠️ **버전 관리 주의**: 본 문서는 FDA 영문 버전을 기본으로 작성하되, MFDS 한국어 버전 및 EU MDR 다국어 버전을 별도 문서로 분리 관리한다. 각 시장 버전의 내용은 동일하나 언어, 형식, 포함 필수 항목이 상이하다.

---

## [파트 I] FDA 버전 (영문 라벨 및 IFU)

### 1. 제품 라벨 (Device Label) — FDA

FDA 21 CFR §801 요건 준수 라벨. 제품 패키지(설치 미디어 또는 다운로드 페이지) 및 SW 내 "정보" 화면에 표시.

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HnVue Console Software  v[X.X.X]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Manufacturer:  H&abyz Co., Ltd.
               [주소 — 도로명, 도시명], Republic of Korea
               Tel: [전화번호]
               Email: info@h-abyz.com

Authorized US Agent:
               [미국 대리인 이름]
               [미국 주소]
               Tel: [전화번호]

CAUTION: Federal law restricts this device to sale by or on
         the order of a licensed practitioner.
                                               ℞ ONLY

UDI:  [UDI-DI]/[UDI-PI]

510(k) Number: K[XXXXXX]
Product Code:  LLZ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 2. IFU (Instructions for Use) 목차 — FDA 영문 버전

---

#### Section 1. Intended Use (의도된 사용)

HnVue Console Software is a prescription-use medical device software intended for use by licensed healthcare professionals (radiologic technologists and radiologists) to acquire, display, process, store, and transmit digital radiography (DR) X-ray images to a Picture Archiving and Communication System (PACS).

**Intended Users**: Licensed radiologic technologists and radiologists.

**Intended Use Environment**: Hospital radiology rooms and reading rooms.

**Indications for Use**: HnVue is indicated for the acquisition, display, post-processing, storage, and PACS transmission of digital radiography (DR) images for use in diagnostic interpretation by qualified medical professionals.

> ⚠️ **중요**: HnVue does not provide automated disease detection or AI-based diagnostic decision support. All diagnostic decisions remain the responsibility of the qualified healthcare professional.

---

#### Section 2. Contraindications (금기사항)

- Use on unsupported operating systems (see Section 5 for supported OS list)
- Use in a network environment directly connected to the internet without an enterprise-grade firewall
- Use by individuals who are not licensed healthcare professionals

---

#### Section 3. Warnings and Precautions (경고 및 주의사항)

**⚠️ WARNINGS (경고)**

| 번호 | 경고 내용 |
|---|---|
| W-001 | HnVue is intended for diagnostic use only. It does not provide automated diagnosis. Clinical interpretation must be performed by a qualified healthcare professional. |
| W-002 | Do not use HnVue as the sole basis for a medical decision. Ensure independent clinical judgment is applied. |
| W-003 | Failure to install security updates in a timely manner may expose the system to cybersecurity vulnerabilities. Follow the update policy in Section 9. |
| W-004 | Do not connect HnVue to an unsecured network. Use only within a secured hospital LAN environment with appropriate firewall protection. |

**⚠️ PRECAUTIONS (주의사항)**

| 번호 | 주의 내용 |
|---|---|
| P-001 | Perform regular monitor calibration. Uncalibrated displays may affect image quality and diagnostic accuracy. |
| P-002 | Maintain the default session timeout setting (15 minutes) to prevent unauthorized access. |
| P-003 | Perform regular data backups per your institution's policy. |
| P-004 | Verify image orientation markers (L/R) before clinical interpretation. |
| P-005 | Do not use HnVue on a system shared with non-medical applications that could compromise performance. |

---

#### Section 4. Product Description (제품 설명)

HnVue Console Software is a standalone medical device software application that runs on Windows-based PC workstations. Key features include:

| 기능 | 설명 |
|---|---|
| Image Acquisition | Connects to DR detectors via proprietary or standard protocol |
| Image Display | High-resolution DICOM image display on WQXGA or higher monitors |
| Image Post-processing | Window/Level adjustment, zoom, pan, rotation, flip, filters |
| Measurements | Length, angle, area measurement tools with annotation |
| PACS Transmission | DICOM C-STORE to hospital PACS system |
| User Management | Role-Based Access Control (RBAC) with audit logging |
| Security | TLS 1.2+ encrypted communications, code-signed software |

---

#### Section 5. System Requirements (시스템 요구사항)

| 항목 | 최소 요구사항 | 권장 사양 |
|---|---|---|
| OS | Windows 10 (22H2 이상) | Windows 11 |
| CPU | Intel Core i5 (10세대) / AMD Ryzen 5 (3세대) | Intel Core i7 (12세대 이상) |
| RAM | 16 GB | 32 GB |
| Storage | 256 GB SSD | 1 TB SSD |
| Display | 1920×1080 (FHD), IEC 62868-1 준수 | 2560×1600 (WQXGA), DICOM Part 14 캘리브레이션 |
| Network | 100 Mbps Ethernet | Gigabit Ethernet |
| GPU | DirectX 11 이상 지원 | [권장 GPU 모델] |

> **지원 중단 OS**: Windows 7, Windows 8.x, Windows 10 21H1 이하 — 지원하지 않음

---

#### Section 6. Installation Instructions (설치 지침)

**6.1 사전 확인사항**

1. 시스템 요구사항 충족 여부 확인 (Section 5)
2. 관리자 권한으로 로그인
3. 설치 파일 무결성 확인:
   - SHA-256 해시값: `[설치 파일 해시값 — 릴리즈 시 기재]`
   - 확인 명령 (PowerShell): `Get-FileHash HnVue_v[X.X.X]_Setup.exe -Algorithm SHA256`

**6.2 설치 절차**

| 단계 | 작업 내용 |
|---|---|
| 1 | `HnVue_v[X.X.X]_Setup.exe` 실행 (관리자 권한) |
| 2 | 라이선스 계약서 동의 |
| 3 | 설치 경로 선택 (기본: `C:\Program Files\HnVue\`) |
| 4 | 서버 설정: PACS IP 주소, 포트 번호(기본 104), AE Title 입력 |
| 5 | 초기 관리자 계정 생성 (ID/비밀번호 설정) |
| 6 | 설치 완료 후 소프트웨어 디지털 서명 검증 화면 확인 |
| 7 | 초기 동작 확인 (SW 실행 → 버전 정보 확인) |

**6.3 비설치 (Uninstallation)**

제어판 → 프로그램 및 기능 → HnVue Console Software → 제거
(또는 설치 폴더의 `Uninstall.exe` 실행)

---

#### Section 7. Operating Instructions (주요 기능 사용 지침)

**7.1 영상 취득 (Image Acquisition)**

1. DR 검출기 연결 상태 확인 (상태 표시줄 녹색 아이콘)
2. 환자 정보 입력 또는 Worklist에서 환자 선택
3. 노출 파라미터 설정 후 "Acquire" 버튼 클릭
4. 취득 완료 후 영상 방향 마커(L/R) 확인
5. 영상 품질 확인 후 PACS 전송 또는 로컬 저장

**7.2 영상 처리 및 조작**

| 기능 | 방법 |
|---|---|
| Window/Level 조절 | 우클릭 드래그 또는 도구 패널 슬라이더 |
| 줌 (Zoom) | 마우스 휠 또는 Ctrl+/- |
| 이동 (Pan) | 가운데 버튼 드래그 또는 스페이스바 + 드래그 |
| 회전 (Rotation) | 도구 패널 → 회전 버튼 (90° 단위) |
| 반전 (Flip) | 도구 패널 → 상하/좌우 반전 |
| 측정 (Measurement) | 도구 패널 → 길이/각도/면적 선택 후 드래그 |
| 주석 (Annotation) | 도구 패널 → 텍스트 도구 선택 후 클릭 입력 |

**7.3 PACS 전송**

1. 전송할 영상 선택
2. "Send to PACS" 버튼 클릭
3. 전송 성공 메시지 확인 (우측 하단 알림)
4. 전송 실패 시 오류 코드 확인 후 Section 8 참조

---

#### Section 8. Error Messages and Troubleshooting (오류 메시지 및 조치 방법)

| 오류 코드 | 오류 메시지 | 원인 | 조치 방법 |
|---|---|---|---|
| E-001 | "PACS Connection Failed" | 네트워크 단절 또는 PACS 서버 응답 없음 | 네트워크 연결 확인, PACS 서버 상태 확인, PACS 관리자 연락 |
| E-002 | "Image File Corrupted" | 불완전한 DICOM 파일 | 영상 재취득, DR 검출기 상태 확인 |
| E-003 | "Authentication Failed" | 잘못된 로그인 정보 또는 계정 잠김 | 비밀번호 확인, 관리자에게 계정 잠금 해제 요청 |
| E-004 | "Disk Space Insufficient" | 저장 공간 부족 | 로컬 스토리지 여유 공간 확보 또는 오래된 데이터 삭제 |
| E-005 | "TLS Handshake Failed" | TLS 인증서 오류 또는 버전 불일치 | IT 관리자에게 TLS 설정 확인 요청 |
| E-006 | "License Validation Failed" | 라이선스 만료 또는 비인가 복사 | H&abyz 기술 지원팀 연락 |
| [추가] | [작성 필요] | | |

---

#### Section 9. Cybersecurity Information (사이버보안 지침)

**9.1 보안 구성 가이드**

| 보안 설정 항목 | 권장 설정 | 비고 |
|---|---|---|
| TLS 버전 | TLS 1.2 이상 필수 (TLS 1.0/1.1 비활성화) | 서버 측에서도 동일 설정 필요 |
| 비밀번호 정책 | 최소 12자, 영문+숫자+특수문자 조합 | 90일 주기 변경 권장 |
| 세션 타임아웃 | 15분 이하 유지 | 기본값 유지 권장 |
| 감사 로그 | 활성화 상태 유지 (기본: 활성화) | 로그 90일 보존 권장 |
| 자동 업데이트 알림 | 활성화 권장 | 보안 패치 지연 방지 |
| 방화벽 | HnVue 포트(기본: 104, 443)만 허용 | IT 관리자와 협의 |

**9.2 보안 업데이트 정책**

| 패치 분류 | SLA (패치 배포 목표) | 사용자 적용 권장 기한 |
|---|---|---|
| Critical (CVSS ≥ 9.0) | 14일 이내 | 배포 후 즉시 |
| High (CVSS 7.0~8.9) | 30일 이내 | 배포 후 14일 이내 |
| Medium (CVSS 4.0~6.9) | 90일 이내 또는 정기 릴리즈 포함 | 배포 후 30일 이내 |
| Low (CVSS < 4.0) | 다음 정기 릴리즈 | 정기 업데이트 시 |

**9.3 네트워크 요구사항**

| 항목 | 요구사항 |
|---|---|
| 네트워크 환경 | 병원 내부 LAN 전용 운용 권장 |
| 인터넷 연결 | 직접 인터넷 연결 금지 (방화벽 필수) |
| PACS 통신 포트 | 기본 DICOM: 104/TCP, 보안 DICOM: 2762/TCP |
| HnVue 업데이트 서버 | HTTPS (443/TCP) — 방화벽 허용 필요 |

**9.4 취약점 신고 (Vulnerability Disclosure)**

보안 취약점 발견 시 아래로 신고:
- 이메일: security@h-abyz.com
- 응답 목표: 3영업일 이내
- 처리 절차: H&abyz 취약점 공시 정책(VDP) 준수

---

#### Section 10. Maintenance and Service (유지보수 지침)

**10.1 정기 점검 항목**

| 점검 항목 | 주기 | 담당 |
|---|---|---|
| 감사 로그 검토 | 월 1회 | IT 관리자 |
| 로컬 백업 검증 | 월 1회 | 시설 관리자 |
| 모니터 캘리브레이션 | 분기 1회 | 방사선 기사 또는 의료물리사 |
| SW 버전 및 보안 패치 확인 | 월 1회 | IT 관리자 |
| 스토리지 여유 공간 확인 | 월 1회 | IT 관리자 |

**10.2 소프트웨어 업데이트 절차**

1. 업데이트 알림 수신 (제품 내 알림 또는 이메일)
2. H&abyz 공식 다운로드 페이지에서 업데이트 파일 수령
3. SHA-256 해시 검증 후 설치
4. 설치 완료 후 기본 동작 확인

---

#### Section 11. Technical Support and Contact (기술 지원 및 연락처)

| 항목 | 내용 |
|---|---|
| 기술 지원 이메일 | support@h-abyz.com |
| 보안 취약점 신고 | security@h-abyz.com |
| 전화 지원 | [전화번호] |
| 서비스 시간 | [영업 시간 — 예: 평일 09:00~18:00 KST] |
| 제조사 웹사이트 | https://www.h-abyz.com |

---

#### Section 12. Regulatory Information (규제 정보)

| 항목 | 내용 |
|---|---|
| FDA 510(k) 번호 | K[XXXXXX] (허가 후 기재) |
| Product Code | LLZ |
| Classification | 21 CFR 892.2050, Class II |
| MFDS 허가 번호 | 제[XXXX]-[XXXXX]호 (허가 후 기재) |
| CE Mark | CE [XXXX] (EU MDR 인증 후 기재) |
| Notified Body | [Notified Body 명칭 및 번호] |
| EU Authorized Representative | [EU 수권 대리인 정보] |
| EUDAMED SRN | [등록 번호 — 등록 후 기재] |

---

#### Section 13. UDI Information (UDI 정보)

| 항목 | 내용 |
|---|---|
| UDI-DI (Device Identifier) | [UDI-DI — 등록 후 기재] |
| UDI-PI (Production Identifier) | [버전 번호 기반 — 릴리즈 시 생성] |
| UDI 발급 기관 | GS1 / HIBCC / ICCBBA 중 선택: [____] |
| UDI 표시 위치 | SW 내 "About" 화면, 설치 패키지 라벨 |
| GUDID 등록 | [등록 일자 — 허가 후 기재] |

---

## [파트 II] MFDS 버전 (한국어) — 추가 요소

본 파트는 FDA 버전을 기반으로 MFDS 특화 요소를 추가한다.

### MFDS 추가 필수 표시 항목

| 항목 | 내용 |
|---|---|
| 제조업허가번호 | 제[XXXX]-[XXXXX]호 |
| 의료기기 허가번호 | 제[XXXX]-[XXXXX]호 |
| 한글 제품명 | HnVue 콘솔 소프트웨어 |
| 제조원 | 에이치앤아비즈 주식회사 (H&abyz Co., Ltd.) |
| 제조원 주소 | [한국 주소 — 도로명주소] |
| 생산 연월일 / SW 버전 | v[X.X.X] (____년 __월 __일) |
| 보관 및 취급 방법 | 전자 배포, OS 지원 환경에서 운용 |
| 의료기기법 준거 표기 | 이 제품은 「의료기기법」 제2조 제1항에 따른 의료기기입니다. |

### MFDS 한국어 IFU 특화 사항

- 전체 IFU를 한국어로 번역 제공
- "처방·지도하에 사용하십시오" 문구 포함
- MFDS 이상사례 신고 안내: 이상사례 발생 시 한국의료기기안전정보원(NIDS) 신고 (1577-4262)

---

## [파트 III] EU MDR 버전 — 추가 요소

본 파트는 FDA 버전을 기반으로 EU MDR Annex I Chapter III §23 특화 요소를 추가한다.

### EU MDR 추가 필수 표시 항목

| 항목 | 내용 |
|---|---|
| CE 마킹 | CE [XXXX] (EU MDR 인증 번호) |
| Notified Body 번호 | [Notified Body 번호] |
| EU 수권 대리인 (EC REP) | [이름, 주소, EU 국가] |
| EUDAMED 등록 번호 (SRN) | [등록 번호] |
| 제조 국가 | Republic of Korea |
| 상징물 표시 | EC REP 심볼, CE 마크, 제조사 심볼 (EN ISO 15223-1 준수) |
| 전자 IFU 참조 | eIFU 웹주소: https://www.h-abyz.com/ifu/hnvue |
| UDI (EU) | [EUDAMED UDI-DI] |

### EU MDR 다국어 IFU 요건

EU MDR Article 10(11): 각 회원국 공식 언어로 IFU 제공 의무. 출시 국가 목록에 따라 번역 제공.

| 출시 국가 | 언어 | 번역 완료 여부 |
|---|---|---|
| 독일 | 독일어 | [작성 필요] |
| 프랑스 | 프랑스어 | [작성 필요] |
| 영국 | 영어 (British English) | [작성 필요] |
| [추가 국가] | [언어] | [작성 필요] |

---

## 변경 이력

| 버전 | 변경 일자 | 변경 내용 | 변경자 |
|---|---|---|---|
| 0.1 | ____-__-__ | 초안 작성 | |
| | | | |

## 승인

| 역할 | 성명 | 서명 | 일자 |
|---|---|---|---|
| 작성자 | | | |
| 검토자 (RA) | | | |
| 검토자 (번역 전문가, 해당 시) | | | |
| 승인자 | | | |

---

## 비고: 최소 필수 선택 근거 (개발팀 안내)

**왜 3개 시장 모두 필수인가?**

- **FDA**: 21 CFR §801은 의료기기 라벨링을 법적 의무로 규정. IFU 없는 의료기기 판매는 불법.
- **MFDS**: 의료기기법 제20조 및 의료기기 표시기재 규정에서 한국어 IFU 및 라벨 의무화.
- **EU MDR**: Annex I §23에서 라벨 및 IFU의 상세 내용을 목록으로 규정. 준수하지 않으면 CE 마킹 불가.

**사이버보안 지침의 IFU 포함 이유**: FDA §524B(2023년) 및 EU MDR Annex I §21은 사이버보안 구성 정보를 라벨링/IFU에 포함하도록 요구. Section 9(사이버보안 지침)는 이 요건을 충족하기 위한 항목이다.

**e-IFU 허용**: 3개 시장 모두 전자 IFU를 허용하나, 종이 IFU 요청 시 제공 가능해야 한다. HnVue는 SW 내 팝업 + PDF 다운로드 + 웹페이지 방식으로 제공 권장.

**사용적합성 연계**: IFU 초안 완성 후 반드시 HnVue-B04(사용적합성 평가)의 사용성 테스트에 IFU 이해도 평가를 포함할 것. IFU가 사용자에게 이해되지 않으면 MFDS/FDA/EU MDR 모두 문제 지적 가능.
