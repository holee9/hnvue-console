# HnVue Console 문서 인덱스

프로젝트의 모든 문서 위치를 안내합니다.

---

## 제품 문서 (docs/)

| 문서 | 설명 |
|------|------|
| [architecture.md](architecture.md) | 시스템 아키텍처 (4계층 구조, 컴포넌트 책임, 데이터 흐름) |
| [prd.md](prd.md) | 제품 요구사항 문서 (PRD) |
| [mrd.md](mrd.md) | 시장 요구사항 문서 (MRD) |
| [rtm.md](rtm.md) | 요구사항 추적성 매트릭스 (RTM) |
| [soup-register.md](soup-register.md) | SOUP 컴포넌트 등록부 |
| [adapter-audit.md](adapter-audit.md) | gRPC 어댑터 구현 감사 보고서 |
| [windows-setup-guide.md](windows-setup-guide.md) | Windows 개발 환경 설정 가이드 |
| [xray-console-sw-research.md](xray-console-sw-research.md) | X선 콘솔 소프트웨어 리서치 |

## 보안 문서 (docs/security/)

| 문서 | 설명 |
|------|------|
| [threat-model.md](security/threat-model.md) | 위협 모델링 분석 |
| [pentest-plan.md](security/pentest-plan.md) | 침투 테스트 계획 |
| [security-improvements.md](security/security-improvements.md) | 보안 개선 사항 |
| [sprint3-security-summary.md](security/sprint3-security-summary.md) | Sprint 3 보안 구현 요약 |

## 규제 템플릿 (docs/templates/)

| 디렉토리 | 설명 |
|----------|------|
| [hnvue-templates/](templates/hnvue-templates/) | IEC 62304 규제 문서 템플릿 |
| [security/](templates/security/) | 의료기기 사이버보안 문서 템플릿 |

## 테스트 리포트 (docs/test-reports/)

빌드/테스트 실행 시 자동 생성되는 리포트입니다.

## SPEC 문서 (.moai/specs/)

13개 SPEC 문서가 `.moai/specs/` 디렉토리에 위치합니다. 각 SPEC 디렉토리에는 `spec.md` (요구사항), `plan.md` (구현 계획), `acceptance.md` (인수 기준) 등이 포함됩니다.

## 아카이브 (docs/archive/)

초기 기획 및 리뷰 문서로, 현재는 참고용입니다.

## 참고 자료 (docs/ref/)

레거시 애플리케이션 바이너리, API 매뉴얼, 성능 테스트 보고서 등이 포함됩니다. Git에서 제외(gitignored)되어 있으며, 필요 시 팀 내부 공유 드라이브에서 다운로드하십시오. 상세 내용은 [docs/ref/README.md](ref/README.md)를 참조하세요.
