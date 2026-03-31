# SBOM 관리 문서 (Software Bill of Materials — Cover Sheet)

> **문서 번호**: HnVue-C01  
> **버전**: 0.1 (Draft)  
> **작성일**: ____-__-__  
> **작성자**: ____________  
> **검토자**: ____________  
> **승인자**: ____________  
> **제품**: HnVue Console SW  
> **회사**: H&abyz Co., Ltd. (에이치앤아비즈)  
> **분류**: ✅ 최소 필수 (FDA / EU MDR) | ⭐ 리스크 최소화 (MFDS)  
> **적용 시장**: FDA (필수), EU MDR (필수), MFDS (권장)  
> **근거 규격**: FD&C Act §524B(b)(3), FDA Cybersecurity Guidance (Feb 2026), Executive Order 14028 (SBOM), SPDX 2.3 또는 CycloneDX 1.5  

---

## ⚠️ 본 문서의 성격에 대한 중요 안내

> **SBOM 파일 자체는 기계 판독 가능한 JSON 또는 XML 형식**으로 별도 생성·첨부된다.  
> 본 문서(HnVue-C01)는 해당 SBOM 파일의 **관리 문서(Cover Sheet)**로서:  
> - SBOM 생성 방법, 도구, 형식, 버전 정보를 기록하고  
> - SBOM 컴포넌트의 주요 항목을 인간 가독 형식으로 요약하며  
> - NTIA 최소 요소 충족 여부 및 CVE 교차검증 결과를 확인한다.  
>
> FDA 제출 시 반드시 SBOM 원본 JSON/XML 파일(`HnVue-vX.X.X-sbom.spdx.json` 등)을 함께 첨부해야 한다.

---

## 1. 문서 정보

| 항목 | 내용 |
|---|---|
| 제품명 | HnVue Console SW |
| 회사명 | H&abyz Co., Ltd. (에이치앤아비즈) |
| SW 버전 | v[작성 필요] |
| SBOM 파일명 | `HnVue-v[작성 필요]-sbom.[spdx.json / bom.json]` |
| SBOM 파일 해시 (SHA-256) | [작성 필요] |
| 기준 날짜 | ____-__-__ |
| 의존 문서 | HnVue-A04 (SOUP 목록) |

---

## 2. SBOM 생성 방법

### 2.1 생성 도구

| 항목 | 내용 |
|---|---|
| 도구 이름 | [예: Syft v1.x / Microsoft SBOM Tool v1.x / CycloneDX Generator / FOSSA] |
| 도구 버전 | v[작성 필요] |
| 도구 출처 | [URL: 예: https://github.com/anchore/syft] |
| 실행 명령어 (참고용) | `syft HnVue-v[버전].exe -o spdx-json > HnVue-sbom.spdx.json` |

> **권장 도구 설명**  
> - **Syft (Anchore)**: 바이너리 및 소스코드 분석, SPDX / CycloneDX 양쪽 출력 지원. 무료 오픈소스.  
> - **Trivy (Aqua Security)**: 컨테이너·파일시스템 스캔. CVE 연동(Grype) 가능.  
> - **FOSSA**: 상용, 라이선스 컴플라이언스 특화. 대규모 조직에 적합.  
> - **Microsoft SBOM Tool**: Windows 환경 .NET 프로젝트에 최적화.

### 2.2 SBOM 형식

| 항목 | 선택 |
|---|---|
| SPDX 2.3 (ISO/IEC 5962:2021) | ☐ 선택 |
| CycloneDX 1.5 (OWASP) | ☐ 선택 |
| 양쪽 모두 생성 | ☐ 선택 (권장 — FDA와 EU 모두 커버) |

> **형식 선택 지침**: FDA는 SPDX와 CycloneDX 모두 수용한다. CycloneDX는 VEX(HnVue-C02) 연동이 더 자연스럽다. 양쪽 모두 생성 시 커버리지가 가장 넓다.

### 2.3 생성 환경 및 SW 버전

| 항목 | 내용 |
|---|---|
| 빌드 환경 OS | [예: Windows 11 22H2 / Ubuntu 22.04] |
| .NET SDK 버전 | [예: .NET 8.0.xxx] |
| 빌드 도구 | [예: Visual Studio 2022 17.x / MSBuild 17.x] |
| CI/CD 파이프라인 통합 여부 | ☐ 예 (자동 생성) / ☐ 아니오 (수동 생성) |
| SBOM 재생성 트리거 | ☐ 릴리즈 빌드 시 자동 / ☐ 수동 |

---

## 3. SBOM 구성 요소 요약

| 구분 | 컴포넌트 수 | 비고 |
|---|---|---|
| 1st Party (자체 개발 모듈) | [작성 필요] | H&abyz 내부 개발 |
| 3rd Party — 오픈소스 (OSS) | [작성 필요] | 라이선스 검토 필요 |
| 3rd Party — 상용 라이브러리 | [작성 필요] | 공급사 계약 확인 |
| 3rd Party — OS/런타임 의존성 | [작성 필요] | .NET Runtime, Windows 구성 요소 등 |
| **총합** | **[작성 필요]** | |

---

## 4. SBOM 컴포넌트 목록 (주요 항목)

> **작성 지침**: SBOM 파일(JSON/XML)에는 모든 컴포넌트가 포함된다. 본 표는 주요 항목만 기재한다. 전체 목록은 첨부 SBOM 파일을 참조.

| # | 패키지 이름 | 버전 | 공급사 | PURL (Package URL) | 라이선스 | 해시 (SHA-256, 앞 8자리) | EOS 날짜 | 관계 |
|---|---|---|---|---|---|---|---|---|
| 1 | HnVue.Core | [작성 필요] | H&abyz Co., Ltd. | `pkg:nuget/HnVue.Core@[버전]` | Proprietary | [작성 필요] | [해당 없음] | DESCRIBES |
| 2 | HnVue.Viewer | [작성 필요] | H&abyz Co., Ltd. | `pkg:nuget/HnVue.Viewer@[버전]` | Proprietary | [작성 필요] | [해당 없음] | CONTAINS |
| 3 | DCMTK | 3.6.8 | OFFIS e.V. | `pkg:github/DCMTK/DCMTK@3.6.8` | BSD-3-Clause | [작성 필요] | [작성 필요] | CONTAINS |
| 4 | OpenSSL | 3.x.x | OpenSSL Project | `pkg:openssl/openssl@3.x.x` | Apache-2.0 | [작성 필요] | [작성 필요] | CONTAINS |
| 5 | .NET Runtime | 8.0.x | Microsoft | `pkg:nuget/Microsoft.NETCore.App@8.0.x` | MIT | [작성 필요] | 2026-11-10 | CONTAINS |
| 6 | [라이브러리명] | [작성 필요] | [작성 필요] | `pkg:[type]/[name]@[버전]` | [작성 필요] | [작성 필요] | [작성 필요] | [작성 필요] |
| ... | | | | | | | | |

> **컬럼 설명**  
> - **PURL**: Package URL 형식 (`pkg:type/namespace/name@version`). 기계 판독 가능 식별자.  
> - **EOS 날짜**: End of Support 날짜. 이 날짜 이후 해당 컴포넌트의 보안 패치가 제공되지 않으므로 VMP에서 관리 필요.  
> - **관계**: SPDX 관계 유형 (`DESCRIBES`, `CONTAINS`, `DEPENDS_ON` 등).

---

## 5. NTIA 최소 요소 체크리스트

> NTIA(미국 통신정보청)가 정의한 SBOM 최소 요소 7가지 준수 여부를 확인한다.  
> FDA는 NTIA 최소 요소 충족을 기본 요건으로 명시하고 있다.

| # | NTIA 최소 요소 | 해당 SBOM 필드 | 충족 여부 | 비고 |
|---|---|---|---|---|
| 1 | **공급자 이름 (Supplier Name)** | `supplier` / `organization` | ☐ 충족 / ☐ 미충족 | |
| 2 | **컴포넌트 이름 (Component Name)** | `name` | ☐ 충족 / ☐ 미충족 | |
| 3 | **컴포넌트 버전 (Version of the Component)** | `version` | ☐ 충족 / ☐ 미충족 | |
| 4 | **기타 고유 식별자 (Other Unique Identifiers)** | `purl` / `cpe` | ☐ 충족 / ☐ 미충족 | PURL 또는 CPE 중 하나 이상 |
| 5 | **종속성 관계 (Dependency Relationship)** | `relationships` / `dependencies` | ☐ 충족 / ☐ 미충족 | |
| 6 | **SBOM 데이터 저작자 (Author of SBOM Data)** | `creator` / `authors` | ☐ 충족 / ☐ 미충족 | |
| 7 | **타임스탬프 (Timestamp)** | `created` / `timestamp` | ☐ 충족 / ☐ 미충족 | ISO 8601 형식 |

> **NTIA 미충족 항목 처리**: 미충족 항목이 있을 경우 해결 계획을 아래 7절에 기재한다.

---

## 6. CVE 교차검증 결과 요약

> SBOM 생성 직후 취약점 스캐너를 실행하여 알려진 CVE를 조회한 결과를 요약한다.  
> 상세 분석은 VEX 리포트(HnVue-C02)에서 수행한다.

### 6.1 스캔 도구 및 결과 개요

| 항목 | 내용 |
|---|---|
| 스캔 도구 | [예: Grype v0.x / Trivy v0.x / Snyk] |
| 스캔 실행 일자 | ____-__-__ |
| NVD 데이터 기준일 | ____-__-__ |
| 스캔 대상 SBOM | `HnVue-v[버전]-sbom.[spdx.json / bom.json]` |

### 6.2 CVE 탐지 요약

| 심각도 | 탐지 CVE 수 | VEX 분석 완료 | Not Affected | Fixed | Affected (패치 필요) | 조사 중 |
|---|---|---|---|---|---|---|
| Critical (CVSS 9.0+) | [작성 필요] | ☐ | [작성 필요] | [작성 필요] | [작성 필요] | [작성 필요] |
| High (CVSS 7.0~8.9) | [작성 필요] | ☐ | [작성 필요] | [작성 필요] | [작성 필요] | [작성 필요] |
| Medium (CVSS 4.0~6.9) | [작성 필요] | ☐ | [작성 필요] | [작성 필요] | [작성 필요] | [작성 필요] |
| Low (CVSS 0.1~3.9) | [작성 필요] | ☐ | [작성 필요] | [작성 필요] | [작성 필요] | [작성 필요] |
| **합계** | **[작성 필요]** | | | | | |

> **참조**: 개별 CVE에 대한 상세 분석 결과(착취 가능성, 판정 근거, 완화 조치)는 **HnVue-C02 (VEX 리포트)**를 참조한다.

### 6.3 즉시 조치 필요 항목 (Affected 판정)

| CVE ID | 컴포넌트 | CVSS | 조치 계획 | 목표 완료일 |
|---|---|---|---|---|
| [작성 필요] | [작성 필요] | [작성 필요] | [작성 필요] | ____-__-__ |

---

## 7. SBOM 업데이트 정책

| 트리거 | SBOM 업데이트 의무 |
|---|---|
| 정기 SW 릴리즈 (Major/Minor/Patch) | ✅ 의무 — 릴리즈 빌드와 동시 재생성 |
| 긴급 보안 패치 릴리즈 | ✅ 의무 |
| 의존성(라이브러리) 추가·변경·삭제 | ✅ 의무 |
| 연간 정기 검토 | ✅ 의무 — 변경 없어도 재확인 |
| 위협 모델 주요 업데이트 | ⭐ 권장 |

> **CI/CD 통합 권장**: SBOM 재생성을 릴리즈 파이프라인에 통합(예: GitHub Actions, Azure DevOps)하면 수작업 오류를 방지할 수 있다.

---

## 8. 첨부 파일 목록

| # | 파일명 | 형식 | 설명 | 해시 (SHA-256) |
|---|---|---|---|---|
| 1 | `HnVue-v[버전]-sbom.spdx.json` | JSON (SPDX 2.3) | SBOM 원본 파일 (기계 판독) | [작성 필요] |
| 2 | `HnVue-v[버전]-bom.json` | JSON (CycloneDX 1.5) | SBOM 원본 파일 (기계 판독) | [작성 필요] |
| 3 | `HnVue-v[버전]-cve-scan.txt` | Text | 취약점 스캔 원시 출력 | [작성 필요] |

---

## 9. 변경 이력

| 버전 | 변경일 | 변경자 | 변경 내용 |
|---|---|---|---|
| 0.1 | ____-__-__ | ____________ | 초안 작성 |

---

## 10. 승인

| 역할 | 이름 | 서명 | 일자 |
|---|---|---|---|
| 작성자 | ____________ | ____________ | ____-__-__ |
| 검토자 (사이버보안 담당) | ____________ | ____________ | ____-__-__ |
| 승인자 (QA/RA 책임자) | ____________ | ____________ | ____-__-__ |

---

## 비고: 왜 이 문서가 필요한가 (개발팀·경영진을 위한 설명)

### ✅ 최소 필수 선택 근거

**FDA (필수)**  
2023년 12월 발효된 FD&C Act §524B(b)(3)에 의거, FDA는 모든 신규 의료기기 SW 510(k) 제출 시 SBOM을 필수로 요구한다. SBOM 없이는 접수 거부(Refuse to Accept, RTA)될 수 있다. 기계 판독 가능 형식(JSON/XML)만 수용하며, 표 형식 PDF는 불충분하다.

**EU MDR (필수)**  
EU MDR Annex I §17.2 (사이버보안 요건)는 SW 구성 요소 목록을 UDI 및 기술문서의 일부로 요구한다. SBOM은 이 요건의 직접적 이행 수단이다.

**MFDS (리스크 최소화 권장)**  
현재 MFDS는 SBOM을 공식 의무화하지 않았으나, 2025년부터 강화되는 디지털의료기기 사이버보안 안내서(안내서-1414-01 개정)에서 SBOM 요건이 포함될 예정이다. 지금 준비하면 추후 재작업이 불필요하다.

### 개발팀 실무 가이드

**SOUP 목록(HnVue-A04)과의 관계**:  
- **SOUP 목록**: IEC 62304 §8.1 규제 요건 — 의료기기 SW에 포함된 제3자 소프트웨어의 위험을 평가하는 인간 가독 문서.  
- **SBOM**: FDA §524B 사이버보안 요건 — 모든 SW 컴포넌트를 기계 판독 형식으로 목록화.  
- 두 문서는 동일한 데이터를 다른 목적으로 활용한다. **SOUP 목록에서 SBOM을 파생 생성**하는 워크플로가 가장 효율적이다.

**자동화 강력 권장**:  
Syft 또는 FOSSA 등을 CI/CD에 한 번 연동하면 릴리즈마다 자동으로 SBOM이 재생성된다. 초기 설정 시간(1~2일)을 투자하면 이후 유지 비용이 거의 없다.
