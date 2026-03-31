# Medical Device Cybersecurity Compliance Template

> Reusable template for medical device cybersecurity compliance planning
> Target: IEC 62304 Class B/C software requiring FDA, EU MDR, MFDS approval

---

## Template Variables

Replace the following variables when using this template:

| Variable | Description | Example |
|----------|-------------|---------|
| `{PROJECT_NAME}` | Project name | HnVue Console |
| `{DEVICE_TYPE}` | Medical device type | X-ray Imaging System |
| `{IEC_CLASS}` | IEC 62304 safety class | Class B/C |
| `{TECH_STACK}` | Technology stack | C# .NET 8, WPF, gRPC |
| `{TARGET_MARKETS}` | Target markets | FDA (US), EU MDR, MFDS (Korea) |
| `{CONNECTION_TYPE}` | External connections | PACS/DICOM, Network |
| `{DATA_TYPE}` | Patient data type | DICOM Images, PHI |

---

## 1. Regulatory Summary Matrix

### 1.1 Cross-Regulatory Requirements

| Requirement Area | FDA (US) | EU MDR | MFDS (Korea) | Common Requirement |
|------------------|----------|--------|--------------|-------------------|
| Cybersecurity Risk Management | SPDF, Section 524B | MDCG 2019-16, MDR Annex I | Medical Device Cybersecurity Guidelines | Risk-based approach, threat modeling |
| Vulnerability Management | Pre/post-market vulnerability mgmt | Post-market surveillance (PMS) | Post-market investigation | SBOM, patch management |
| Authentication/Authorization | MFA recommended | IEC 81001-5-1 | RBAC required | Role-based access control |
| Data Protection | HIPAA (patient data) | GDPR | Personal Information Protection Act | Encryption, access logs |
| Audit Trails | 21 CFR Part 11 | MDR Article 25 | Medical Device Act | Tamper-proof audit logs |
| Update Management | Secure Updatability | MDR Article 10(4) | Software change management | Signed updates, integrity verification |

### 1.2 FDA Section 524B Requirements

| Requirement | Description | {PROJECT_NAME} Status |
|-------------|-------------|----------------------|
| Cybersecurity features | Prevent unauthorized access | [ ] Implement RBAC |
| Vulnerability identification | Threat modeling required | [ ] Complete STRIDE analysis |
| Software BOM | SBOM maintenance | [ ] Generate SBOM |
| Updates/patches | Authenticated update mechanism | [ ] Design secure update |
| Post-market management | Vulnerability disclosure & response | [ ] Establish process |

### 1.3 EU MDR MDCG 2019-16 Requirements

| Requirement | Description | {PROJECT_NAME} Status |
|-------------|-------------|----------------------|
| IT security risk assessment | ISO 14971 linkage | [ ] Risk assessment document |
| State-of-the-art security | Regular security updates | [ ] Update process |
| Cybersecurity incident response | Incident reporting framework | [ ] IRP document |
| User security training | Security instructions in IFU | [ ] Document user guidance |

---

## 2. Standards Mapping

### 2.1 Core Standards

| Standard | Purpose | {PROJECT_NAME} Scope |
|----------|---------|---------------------|
| **IEC 62304** | Medical device software lifecycle | Entire development process |
| **ISO 14971** | Medical device risk management | Risk assessment, threat modeling |
| **AAMI TIR57** | Medical device security risk management | Security-specific risk analysis |
| **ANSI/AAMI SW96** | Medical device security risk standard | Threat modeling framework |
| **IEC 81001-5-1** | Medical device IT security capabilities | Security control implementation |
| **NIST CSF 2.0** | Cybersecurity framework | Security control framework |
| **FDA SPDF** | Secure Product Development Framework | Development lifecycle security |

### 2.2 IEC 62304 {IEC_CLASS} Security Mapping

| IEC 62304 Clause | Security Application | {PROJECT_NAME} Implementation |
|------------------|---------------------|-------------------------------|
| 5.1 Software development planning | Include security requirements | [ ] Specify security requirements in SPEC |
| 5.2 Software requirements analysis | Derive security requirements | [ ] Threat modeling based requirements |
| 5.3 Software architecture | Design security architecture | [ ] gRPC TLS, auth/authn layers |
| 5.4 Software detailed design | Design security controls | [ ] Encryption, audit log design |
| 5.5 Software unit testing | Security test cases | [ ] Penetration testing, vuln scanning |
| 5.6 Software integration testing | Security integration testing | [ ] E2E security scenarios |
| 5.7 Software verification | Security verification | [ ] OWASP ZAP, static analysis |

### 2.3 NIST CSF 2.0 Function Mapping

| NIST CSF Function | Subcategory | {PROJECT_NAME} Application |
|-------------------|-------------|---------------------------|
| **GOVERN (GV)** | GV.OC, GV.RM, GV.PO | Security governance, risk management policy |
| **IDENTIFY (ID)** | ID.AM, ID.RA, ID.IM | Asset identification, risk assessment, threat modeling |
| **PROTECT (PR)** | PR.AA, PR.AT, PR.DS | Authentication/authorization, training, data protection |
| **DETECT (DE)** | DE.CM, DE.DP | Security monitoring, intrusion detection |
| **RESPOND (RS)** | RS.MA, RS.AN, RS.CO | Incident response, analysis, communication |
| **RECOVER (RC)** | RC.RP, RC.CO | Recovery planning, communication |

---

## 3. Implementation Plan

### 3.1 Security Governance

#### 3.1.1 Organization & Responsibilities

| Role | Responsibility | Assigned To |
|------|----------------|-------------|
| Security Officer | Security policy, compliance oversight | Quality Team |
| Development Security | Secure coding guide, code review | Development Team |
| Risk Manager | Threat modeling, risk assessment | Quality Team |
| Incident Response | Security incident response, disclosure | Operations Team |

#### 3.1.2 Security Policy Documents

| Document | Purpose | Timing |
|----------|---------|---------|
| Security Policy | Enterprise security guidelines | Phase 1 |
| Secure Coding Guide | Developer security coding standards | Phase 1 |
| Vulnerability Management Procedure | Vulnerability identification/fix process | Phase 1 |
| Incident Response Plan (IRP) | Security incident response procedures | Phase 2 |
| SBOM Management Procedure | Component tracking management | Phase 1 |

### 3.2 Security Architecture

#### 3.2.1 System Architecture Security View

```
+------------------+     TLS 1.3+      +------------------+
| {PROJECT_NAME}   | <===============> |   Core Engine    |
|   (GUI Client)   |     gRPC IPC      |   (Backend)      |
+------------------+                   +------------------+
        |                                      |
        v                                      v
+------------------+                   +------------------+
|  User Auth (RBAC)|                   |   Hardware HAL   |
|  UserService     |                   |   Detectors      |
+------------------+                   +------------------+
        |
        v
+------------------+
|  Audit Logging   |
|  AuditLogService |
+------------------+
```

#### 3.2.2 {CONNECTION_TYPE} Security Requirements

| Security Control | Requirement | Implementation Status |
|------------------|-------------|----------------------|
| Transport Security | TLS 1.3+ required | [ ] Implement |
| Mutual Authentication | mTLS (Mutual TLS) | [ ] Design |
| Auth Token | Session-based token | [ ] Define in proto |
| Message Integrity | TLS provided | [ ] Verify with TLS |
| Replay Prevention | Timestamp-based | [ ] Proto Timestamp defined |

#### 3.2.3 Data Flow Security

```
[User Input] --> [Input Validation] --> [Sanitization] --> [Business Logic]
                                                            |
                                                            v
[Database] <-- [Encrypted Storage] <-- [Data Classification]
                                                            |
                                                            v
[Audit Log] <-- [Security Event] <-- [Access Control Check]
```

### 3.3 Technical Security Controls

#### 3.3.1 Authentication

| Control Item | Requirement | Implementation Guide |
|--------------|-------------|---------------------|
| Password Policy | Min 12 chars, complexity required | [ ] Validate in ViewModel |
| Account Lockout | Lock after 5 failures | [ ] Implement in UserService |
| Session Management | Timeout 30 min, re-authentication | [ ] Use session expires_at |
| Multi-Factor Auth | Recommended (P2) | [ ] Future OTP/TOTP support |

#### 3.3.2 Authorization

| Control Item | Requirement | Implementation Guide |
|--------------|-------------|---------------------|
| Role-Based Access Control | Least privilege principle | [ ] Use Role enum |
| Permission Verification | Permission check on sensitive operations | [ ] Use Permission message |
| Admin Function Separation | Separate permission group | [ ] Define ADMINISTRATOR role |

**Role Definitions (Customize per project):**

```protobuf
enum Role {
  ROLE_UNSPECIFIED = 0;
  ROLE_ADMINISTRATOR = 1;      // Full system access
  ROLE_PHYSICIAN = 2;          // Doctor - report signing
  ROLE_TECHNOLOGIST = 3;       // Technologist
  ROLE_OPERATOR = 4;           // Basic operator
  ROLE_VIEWER = 5;             // Read-only
  ROLE_SERVICE = 6;            // Maintenance engineer
}
```

**Permission Matrix (Customize per project):**

| Function | Admin | Physician | Technologist | Operator | Viewer |
|----------|-------|-----------|--------------|----------|--------|
| Exposure Start | X | O | O | O | X |
| Patient Query | O | O | O | R | R |
| Settings Change | O | X | X | X | X |
| QC Perform | O | O | O | X | X |
| Audit Log View | O | R | X | X | X |

#### 3.3.3 Cryptography

| Control Item | Requirement | Implementation Guide |
|--------------|-------------|---------------------|
| Transport Encryption | TLS 1.3+ required | [ ] Configure gRPC channel |
| Data at Rest Encryption | AES-256-GCM | [ ] Encrypt sensitive data |
| Password Hashing | Argon2id or bcrypt | [ ] Hash password storage |
| Key Management | HSM or secure key storage | [ ] Key rotation policy |

#### 3.3.4 Audit Logging

**Audit Event Types (Customize per project):**

| Event Type | Security Severity |
|------------|------------------|
| USER_LOGIN | INFO |
| USER_LOGIN_FAILED | WARNING |
| ACCESS_DENIED | WARNING |
| EXPOSURE_STARTED | INFO |
| CONFIGURATION_CHANGED | WARNING |
| DATA_EXPORT | INFO |

**Audit Log Security Requirements:**
- [ ] Integrity: Log tamper prevention (signature or WORM storage)
- [ ] Confidentiality: Sensitive information masking
- [ ] Availability: Minimum 6-year retention (medical device regulation)
- [ ] Timestamp: Accurate time recording (NTP sync)

#### 3.3.5 Input Validation

| Input Source | Validation Items | Implementation Location |
|--------------|------------------|------------------------|
| UI Input | Length, format, range | ViewModel |
| gRPC Request | Proto constraints | Service Adapter |
| {DATA_TYPE} Data | DICOM compliance | DicomService |
| Config Values | Allowed value ranges | ConfigService |

**OWASP Top 10 Mitigation:**

| OWASP Item | {PROJECT_NAME} Mitigation |
|------------|--------------------------|
| A01:2021 - Broken Access Control | Role-based RBAC |
| A02:2021 - Cryptographic Failures | TLS 1.3+, AES-256 |
| A03:2021 - Injection | Parameterized queries, input validation |
| A04:2021 - Insecure Design | Threat modeling based design |
| A05:2021 - Security Misconfiguration | Security configuration guide |
| A06:2021 - Vulnerable Components | SBOM, regular scanning |
| A07:2021 - Authentication Failures | MFA, session management |
| A08:2021 - Software Integrity | Signed updates |
| A09:2021 - Logging Failures | AuditLogService |
| A10:2021 - SSRF | Internal network isolation |

### 3.4 Network Security

#### 3.4.1 Network Segmentation

```
+-----------------+     +-----------------+     +-----------------+
|   Workstation   |     |   Core Engine   |     |   External      |
|   (GUI Client)  |     |   (Backend)     |     |   (PACS/DICOM)  |
+-----------------+     +-----------------+     +-----------------+
         |                      |                      |
         +---- Internal LAN ----+---- DMZ (Optional)---+
                       |
               +-------+-------+
               |   Firewall    |
               +---------------+
```

#### 3.4.2 Network Security Requirements

| Control Item | Requirement | Implementation |
|--------------|-------------|----------------|
| Port Restriction | Only necessary ports open | [ ] gRPC (50051), DICOM (104) |
| Firewall | Host-based firewall | [ ] Configure Windows Firewall |
| Service Separation | GUI and backend separation | [ ] gRPC IPC structure |
| External Connection | Minimize, VPN recommended | [ ] TLS for PACS connection |

---

## 4. Testing & Validation Plan

### 4.1 Security Testing Strategy

| Test Type | Purpose | Tools | Timing |
|-----------|---------|-------|--------|
| Static Analysis (SAST) | Source code vulnerabilities | SonarQube, Roslyn Analyzers | CI/CD |
| Dynamic Analysis (DAST) | Runtime vulnerabilities | OWASP ZAP, Burp Suite | Sprint end |
| Dependency Scan | Vulnerable libraries | NuGet Audit, Snyk | CI/CD |
| Container Scan | Image vulnerabilities | Trivy | Build time |
| Fuzz Testing | Input validation | AFL, libFuzzer | Periodic |
| Penetration Testing | Real attack simulation | Manual + Automated | Pre-market |

### 4.2 Static Code Analysis

**Tool Configuration:**

```xml
<!-- .csproj -->
<PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" />
<PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7" />
```

**Analysis Rules:**

| Rule Category | Rule ID | Description |
|---------------|---------|-------------|
| Security | CA2100 | SQL injection review |
| Security | CA2300/2301 | Deserialization vulnerabilities |
| Security | CA5350/5351 | Weak encryption algorithms |
| Security | CA3075 | XML processing vulnerabilities |

### 4.3 Dynamic Security Testing

**OWASP ZAP Scan Configuration:**

```yaml
# zap-scan.yaml
scanners:
  - name: "{PROJECT_NAME} gRPC"
    target: "localhost:50051"
    scan_type: "grpc"
    authentication:
      type: "token"
      token_endpoint: "/UserService/Authenticate"
    excluded_paths:
      - "/HealthService/*"
```

### 4.4 Vulnerability Scan Process

```
+------------+    +------------+    +------------+    +------------+
|   Code     | -> |   SAST     | -> |   Review   | -> |   Fix      |
|   Commit   |    |   Scan     |    |   Results  |    |   Issues   |
+------------+    +------------+    +------------+    +------------+
                         |
                         v
                  +------------+
                  |   CVE      |
                  |   DB       |
                  +------------+
```

**Vulnerability Severity Classification:**

| Severity | CVSS Score | Response Time | Example |
|----------|------------|---------------|---------|
| Critical | 9.0-10.0 | 24 hours | RCE, auth bypass |
| High | 7.0-8.9 | 7 days | SQL Injection, XSS |
| Medium | 4.0-6.9 | 30 days | Info disclosure |
| Low | 0.1-3.9 | Next release | Minor info disclosure |

### 4.5 Penetration Testing Checklist

| Test Item | Attack Vector | Expected Defense |
|-----------|---------------|------------------|
| Authentication Bypass | Session theft, token tampering | Session validation, token signing |
| Privilege Escalation | Role manipulation | Server-side permission check |
| SQL Injection | {DATA_TYPE} Service queries | Parameterized queries |
| Command Injection | ConfigService input | Input validation, whitelist |
| Buffer Overflow | gRPC messages | Proto size limits |
| Information Disclosure | Error messages | Generic error responses |
| Replay Attack | Request reuse | Timestamp validation |

---

## 5. Pre-market Final Test Plan

### 5.1 Pre-market Security Checklist

#### 5.1.1 FDA Submission Preparation

| Item | Requirement | Status | Notes |
|------|-------------|--------|-------|
| Security Risk Assessment | Threat modeling complete | [ ] | AAMI TIR57 based |
| Security Architecture | Security architecture document | [ ] | FDA guidance view included |
| SBOM | Component list | [ ] | NTIA minimum elements |
| Vulnerability Testing | Test results | [ ] | SAST/DAST/Fuzz |
| Penetration Testing | Independent testing | [ ] | Third-party recommended |
| Threat Modeling Report | STRIDE based | [ ] | Include in this document |
| Security Update Plan | Patch management plan | [ ] | Section 524B compliance |

#### 5.1.2 EU MDR Preparation

| Item | Requirement | Status | Notes |
|------|-------------|--------|-------|
| Technical File | Security related technical documents | [ ] | Annex II, III |
| Clinical Evaluation | Security related clinical evaluation | [ ] | MDCG 2019-16 |
| PMS Plan | Post-market surveillance plan | [ ] | Include security incidents |
| IFU Security Instructions | User security guidelines | [ ] | Include in user manual |
| UDI | Unique Device Identification | [ ] | GS1 or HIBC |

#### 5.1.3 MFDS (Korea) Preparation

| Item | Requirement | Status | Notes |
|------|-------------|--------|-------|
| Cybersecurity Technical Document | Technical document for approval | [ ] | MFDS guideline compliance |
| Risk Management Report | ISO 14971 based | [ ] | Include security risks |
| Software Verification Report | IEC 62304 based | [ ] | Include security testing |
| SBOM | Component list | [ ] | Korean language |

### 5.2 Security Test Execution Plan

| Phase | Activity | Owner | Duration | Deliverable |
|-------|----------|-------|----------|-------------|
| 1 | Threat modeling update | Security Owner | 1 week | TM document v2 |
| 2 | SAST/DAST scan | Dev Team | 2 weeks | Scan report |
| 3 | Vulnerability fix | Dev Team | 2 weeks | Fix record |
| 4 | Re-scan | Security Owner | 1 week | Final scan report |
| 5 | Penetration testing (external) | Third-party | 2 weeks | PT report |
| 6 | Final verification | Quality Team | 1 week | Verification report |

### 5.3 Final Verification Criteria

| Criteria | Pass Condition |
|----------|----------------|
| Critical vulnerabilities | 0 |
| High vulnerabilities | 0 |
| Medium vulnerabilities | < 5 (documented mitigation) |
| Code coverage | > 85% |
| Security test cases | 100% pass |

---

## 6. Project-Specific Implementation Roadmap

### 6.1 {PROJECT_NAME} Security Implementation Status

| Component | Implementation Status | Security Requirements | Priority |
|-----------|----------------------|----------------------|----------|
| **gRPC IPC** | Proto defined | TLS 1.3+, mTLS | P1 |
| **UserService** | Proto defined, implementation needed | RBAC, session management | P1 |
| **AuditLogService** | Proto defined, implementation needed | Integrity, retention policy | P1 |
| **{PRIMARY_SERVICE_1}** | Proto defined | Data integrity | P1 |
| **{PRIMARY_SERVICE_2}** | Proto defined, adapter Stub | Confidentiality | P2 |
| **ConfigService** | Partial implementation | Change audit | P2 |
| **NetworkService** | Partial implementation | TLS, certificate management | P2 |

### 6.2 Phase-by-Phase Implementation Plan

#### Phase 1: Security Foundation (MVP + 2 weeks)

| Task | Deliverable | Owner |
|------|-------------|-------|
| Threat modeling (STRIDE) | Threat model document | Security Owner |
| SBOM generation | components.json | Dev Team |
| gRPC TLS configuration | TLS configuration guide | Dev Team |
| UserService security implementation | Authentication/authorization module | Dev Team |
| AuditLogService implementation | Audit log module | Dev Team |

#### Phase 2: Security Hardening (MVP + 4 weeks)

| Task | Deliverable | Owner |
|------|-------------|-------|
| SAST/DAST pipeline | CI/CD security scan | DevOps |
| Penetration testing | PT report | External vendor |
| Security documentation | STR, security guide | Quality Team |
| mTLS implementation | Mutual authentication configuration | Dev Team |

#### Phase 3: Regulatory Compliance Completion (Pre-market)

| Task | Deliverable | Owner |
|------|-------------|-------|
| FDA compliance review | Submission document package | Regulatory Owner |
| EU MDR review | Technical File | Regulatory Owner |
| MFDS review | Product approval document | Regulatory Owner |
| Final security audit | Audit report | Quality Team |

### 6.3 Stub Adapter Security Implementation Roadmap

**Customize based on project's stub adapters:**

| Adapter | Security Controls | Proto Security Features |
|---------|-------------------|------------------------|
| {SERVICE_1}Service | Integrity verification | Hash, signature |
| {SERVICE_2}Service | Data protection | Encryption, access control |
| {SERVICE_3}Service | Authentication | Session token |
| UserService | Authentication/authorization | RBAC, session management |
| {SERVICE_4}Service | Integrity | Audit logging |
| {SERVICE_5}Service | Integrity | Status verification |
| {SERVICE_6}Service | Integrity | Signed protocol |
| AuditLogService | Integrity | WORM, signature |
| {SERVICE_7}Service | Integrity | Audit logging |

---

## 7. Documentation Requirements

### 7.1 Software Test Report (STR) Template

```markdown
# Software Test Report - {PROJECT_NAME}

## 1. Document Information
- Project: {PROJECT_NAME}
- Version: X.Y.Z
- Test Date: YYYY-MM-DD
- Owner: [Name]

## 2. Test Scope
- Unit tests: [ ] Pass
- Integration tests: [ ] Pass
- Security tests: [ ] Pass
- E2E tests: [ ] Pass

## 3. Security Test Results

### 3.1 SAST Results
| Rule ID | Severity | Description | Status |
|---------|----------|-------------|--------|
| CA2100 | Warning | SQL Injection risk | Fixed |

### 3.2 DAST Results
| Vulnerability | CVSS | Status | Notes |
|---------------|------|--------|-------|
| - | - | - | - |

### 3.3 Dependency Scan
| Package | CVE | CVSS | Status |
|---------|-----|------|--------|
| - | - | - | - |

## 4. Test Summary
- Total test cases: [N]
- Passed: [N]
- Failed: [N]
- Skipped: [N]

## 5. Approval
- Test Owner: [Signature] [Date]
- Reviewer: [Signature] [Date]
```

### 7.2 SBOM (Software Bill of Materials) Template

```json
{
  "sbom": {
    "specVersion": "1.5",
    "serialNumber": "urn:uuid:...",
    "version": 1,
    "metadata": {
      "timestamp": "YYYY-MM-DDTHH:MM:SSZ",
      "component": {
        "type": "application",
        "name": "{PROJECT_NAME}",
        "version": "1.0.0",
        "supplier": "{COMPANY_NAME}"
      }
    },
    "components": [
      {
        "type": "library",
        "name": "Google.Protobuf",
        "version": "3.28.3",
        "purl": "pkg:nuget/Google.Protobuf@3.28.3",
        "licenses": [{"license": {"id": "BSD-3-Clause"}}]
      }
    ],
    "dependencies": [...],
    "vulnerabilities": [...]
  }
}
```

### 7.3 Risk Assessment Template

```markdown
# Security Risk Assessment - {PROJECT_NAME}

## 1. Threat Model (STRIDE)

| Threat Type | Asset | Attack Vector | Impact | Mitigation |
|-------------|-------|---------------|--------|------------|
| Spoofing | User session | Session theft | High | mTLS, session timeout |
| Tampering | {DATA_TYPE} data | DB tampering | High | Audit logging, integrity verification |
| Repudiation | Audit logs | Log deletion | Medium | WORM storage, signing |
| Info Disclosure | Patient info | Unauthorized access | High | RBAC, encryption |
| Denial of Service | gRPC services | Flood attack | Medium | Rate limiting, timeout |
| Elevation of Privilege | Privilege escalation | Role manipulation | High | Server-side permission check |

## 2. Risk Score Matrix

| Risk | Likelihood | Impact | Score | Priority |
|------|------------|--------|-------|----------|
| Auth bypass | Low | High | 6 | High |
| Data leakage | Medium | High | 8 | High |
| DoS | Low | Medium | 3 | Medium |

## 3. Mitigation Plan
- [ ] Implement mTLS
- [ ] Strengthen RBAC
- [ ] Sign audit logs
- [ ] Implement rate limiting
```

### 7.4 Incident Response Plan (IRP) Template

```markdown
# Security Incident Response Plan

## 1. Incident Classification

| Level | Definition | Response Time | Disclosure Deadline |
|-------|------------|---------------|---------------------|
| Critical | Patient safety threat | 1 hour | 24 hours |
| High | Data breach | 4 hours | 72 hours |
| Medium | Service disruption | 24 hours | 7 days |
| Low | Potential vulnerability | 72 hours | 30 days |

## 2. Response Procedures

1. **Detection**: Monitoring, reporting
2. **Analysis**: Impact assessment, classification
3. **Containment**: Prevent damage spread
4. **Eradication**: Remove root cause
5. **Recovery**: Restore normal operation
6. **Post-incident**: Prevent recurrence

## 3. Contact List

| Role | Person | Contact |
|------|--------|---------|
| Security Owner | [Name] | [Phone] |
| Dev Lead | [Name] | [Phone] |
| Regulatory Owner | [Name] | [Phone] |

## 4. Regulatory Disclosure Requirements

| Regulatory | Disclosure Deadline | Disclosure Method |
|------------|---------------------|-------------------|
| FDA | 30 days (Critical) | eSubmitter |
| EU (NB) | Immediate | NB portal |
| MFDS | 7 days | Medical device info system |
```

---

## 8. Appendix

### 8.1 Reference Documents

| Document | Source | Version |
|----------|--------|---------|
| FDA Cybersecurity Guidance | FDA.gov | 2026-02 |
| MDCG 2019-16 | EU Commission | 2019 |
| IEC 62304 | IEC | 2006+A1:2015 |
| ISO 14971 | ISO | 2019 |
| AAMI TIR57 | AAMI | 2019 |
| NIST CSF | NIST | 2.0 (2024) |
| IEC 81001-5-1 | IEC | 2021 |

### 8.2 Glossary

| Term | Definition |
|------|------------|
| SPDF | Secure Product Development Framework |
| SBOM | Software Bill of Materials |
| STRIDE | Spoofing, Tampering, Repudiation, Info Disclosure, DoS, EoP |
| mTLS | Mutual TLS (mutual authentication) |
| RBAC | Role-Based Access Control |
| SAST | Static Application Security Testing |
| DAST | Dynamic Application Security Testing |

### 8.3 Change History

| Version | Date | Change | Author |
|---------|------|--------|--------|
| 1.0 | YYYY-MM-DD | Initial template creation | MoAI Security Expert |

---

## Template Usage Instructions

1. **Replace Variables**: Substitute all `{VARIABLE}` placeholders with project-specific values
2. **Customize Sections**: Adapt role definitions, permission matrices, and service lists to your project
3. **Regulatory Selection**: Focus on relevant regulatory bodies (FDA, EU MDR, MFDS, or others)
4. **Technology Stack**: Update tool references and code examples to match your tech stack
5. **Phase Adaptation**: Adjust implementation phases based on project timeline
6. **Document Generation**: Use templates to generate actual project documents

---

*This template is reusable across medical device software projects requiring cybersecurity compliance. Customize variables and sections per project requirements.*
