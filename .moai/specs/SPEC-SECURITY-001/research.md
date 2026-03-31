# SPEC-SECURITY-001: 연구 문서

## 코드베이스 분석 요약

### 기존 보안 관련 코드

| 영역 | 파일/폴더 | 현황 | 보안 영향 |
|------|----------|------|----------|
| gRPC | `proto/hnvue_user.proto` | 정의 완료 | RBAC 구조 필요 |
| gRPC | `proto/hnvue_audit_log.proto` | 정의 완료 | 감사 이벤트 타입 필요 |
| Adapter | `UserServiceAdapter.cs` | Stub | 구현 필요 |
| Adapter | `AuditLogServiceAdapter.cs` | Stub | 구현 필요 |
| Config | `.moai/config/` | 구성 필요 | 보안 설정 추가 예정 |

### 기존 Proto 정의 분석
#### UserService (hnvue_user.proto)
- UserRole enum: 7개 역할 정의 (ADMINISTRATOR, RADIOLOGIST, TECHNOLOGIST, PHYSICIST, OPERATOR, VIEWER, SERVICE)
- UserSession message: 세션 정보 (session_id, access_token, expires_at, roles)
- Permission message: 권한 정보 (resource_type, allowed_actions)

#### AuditLogService (hnvue_audit_log.proto)
- AuditEventType enum: 15개 이벤트 타입
- SeverityLevel enum: 5단계 심각도 (INFO, WARNING, ERROR, CRITICAL)
- AuditEntry message: 감사 항목 (event_type, user_id, timestamp, correlation_id)

### 보안 요구사항 매핑
| 요구사항 | 기존 Proto 매핑 | 추가 구현 필요 |
|----------|------------------|------------------|
| R1: RBAC | UserRole enum | 권한 검증 로직 |
| R1: 세션 타임아웃 | UserSession.expires_at | 타임아웃 확인 로직 |
| R2: 감사 로그 | AuditEventType enum | WORM/서명 로직 |
| R3: TLS/mTLS | N/A (전송 계층) | TLS 구성 |
| R4: SBOM | N/A | CI/CD 파이프라인 |

### 기술 부채 분석
| 영역 | 권장 라이브러리 | 비고 |
|------|------------|------------------|------|
| 암호화 | System.Security.Cryptography | .NET 8 내장 | AES-256, SHA-256 제공 |
| 해시 | BCrypt.Net | NuGet | 비밀번호 저장용 Argon2id 권장 |
| gRPC | Grpc.Net.Client | 2.66.0 | TLS 지원 |
| Protobuf | Google.Protobuf | 3.28.3 | 직렬화 |
| SBOM | SPDX | NuGet | JSON 기반 |

### 구현 복잡도 추정
| 요구사항 | 예상 복잡도 | 위험 요소 |
|----------|------------|--------------|
| R1: UserService | 중간 | 기존 Proto 확장 필요 |
| R2: AuditLogService | 중간 | WORM/서명 구현 필요 |
| R3: mTLS | 낮음 | 인증서 관리 복잡 |
| R4: SBOM | 낮음 | CI/CD 통합 |

| R5: STRIDE | 낮음 | 문서화 중심 |

---

**Document Version**: 1.0.0
**Last Updated**: 2026-03-12
