# Medical Device Cybersecurity Templates

> Reusable templates for medical device cybersecurity compliance across the software development lifecycle

---

## Overview

This collection provides comprehensive, reusable templates for implementing cybersecurity in medical device software. Designed for IEC 62304 Class B/C software requiring FDA, EU MDR, and MFDS compliance.

**Target Users:**
- Medical device software developers
- Security engineers
- Quality assurance teams
- Regulatory affairs specialists
- Project managers

---

## Template Collection

### 1. Medical Device Cybersecurity Compliance Template

**File:** `medical-device-cybersecurity-template.md`
**Size:** ~27 KB (800+ lines)

**Purpose:** Complete regulatory compliance planning document covering FDA Section 524B, EU MDR MDCG 2019-16, and MFDS requirements.

**Contents:**
- Regulatory summary matrix (FDA, EU, Korea)
- Standards mapping (IEC 62304, ISO 14971, AAMI TIR57, NIST CSF)
- Implementation plan (governance, architecture, controls)
- Testing & validation strategy
- Pre-market security checklist
- Project-specific roadmap
- Documentation templates (STR, SBOM, Risk Assessment, IRP)

**When to Use:**
- Starting a new medical device software project
- Preparing for regulatory submission
- Establishing security program governance
- Creating security architecture documentation

**Template Variables:**
```
{PROJECT_NAME}, {DEVICE_TYPE}, {IEC_CLASS}, {TECH_STACK},
{TARGET_MARKETS}, {CONNECTION_TYPE}, {DATA_TYPE}
```

---

### 2. Development Guidelines Template

**File:** `development-guideline-template.md`
**Size:** ~18 KB (600+ lines)

**Purpose:** Secure development practices and coding standards for medical device software.

**Contents:**
- Secure Development Lifecycle (SDLC)
- Secure coding standards (C# specific examples)
- OWASP Top 10 mitigation
- Code review guidelines
- Security testing specifications
- Dependency management
- Configuration management
- Incident response for developers

**When to Use:**
- Establishing secure coding practices
- Training development teams
- Setting up security gates in SDLC
- Creating code review checklists

**Key Features:**
- Technology-specific examples (C# .NET 8)
- gRPC security best practices
- Ready-to-use code examples
- Security metrics tracking
- Quick reference checklists

---

### 3. Penetration Testing Preparation Template

**File:** `pentest-preparation-template.md`
**Size:** ~19 KB (550+ lines)

**Purpose:** Comprehensive preparation for third-party security assessments.

**Contents:**
- Pre-engagement timeline and checklists
- Scope definition (in-scope/out-of-scope)
- Vendor selection criteria and RFP template
- Test environment setup specifications
- Rules of engagement (ROE)
- Approved testing tools
- Deliverables specifications
- Communication plan
- Budget management
- Regulatory submission preparation

**When to Use:**
- Planning third-party penetration testing
- Selecting security vendors
- Preparing test environments
- Managing testing engagements
- Documenting for regulatory submission

**Key Features:**
- 12-week preparation timeline
- Vendor evaluation matrix
- Test environment architecture
- Cost breakdown templates
- Post-test activities guidance

---

### 4. DIY Security Testing Template

**File:** `diy-security-testing-template.md`
**Size:** ~25 KB (700+ lines)

**Purpose:** Cost-effective internal security testing using free/open-source tools.

**Contents:**
- Free tool arsenal (SAST, DAST, dependency scanning)
- Test environment setup (Docker Compose)
- Testing procedures and templates
- Reporting and documentation
- CI/CD security integration
- Skill development resources
- Cost optimization strategies

**When to Use:**
- Budget-constrained projects
- Continuous security validation
- Pre-test preparation for professional testing
- Development-phase security testing
- Reducing professional testing costs

**Key Features:**
- Complete toolchain with Docker setup
- 24-hour quick start guide
- Hybrid testing strategy (DIY + professional)
- Cost comparison table (67% savings potential)
- Ready-to-use CI/CD pipelines

**Tools Covered:**
- SonarQube, Roslyn Analyzers (SAST)
- OWASP ZAP, gRPCurl (DAST)
- NuGet Audit, Snyk, Trivy (dependency)
- Nmap, OpenVAS (infrastructure)

---

## Quick Reference

### By Project Phase

| Phase | Recommended Template(s) |
|-------|-------------------------|
| **Planning** | Medical Device Cybersecurity Compliance |
| **Development** | Development Guidelines |
| **Testing Preparation** | Penetration Testing Preparation |
| **Continuous Testing** | DIY Security Testing |
| **Regulatory Submission** | All templates (for evidence) |

### By Role

| Role | Primary Templates |
|------|------------------|
| **Security Architect** | Compliance, Development Guidelines |
| **Developer** | Development Guidelines, DIY Testing |
| **QA Engineer** | DIY Testing, Pentest Preparation |
| **Project Manager** | All templates (planning focus) |
| **Regulatory Affairs** | Compliance, Pentest Preparation |

### By Budget

| Budget | Recommended Approach |
|--------|---------------------|
| **Full** ($15K-30K) | Compliance → Pentest Preparation → Professional Testing |
| **Limited** ($5K-10K) | Compliance → Development Guidelines → DIY → Hybrid Testing |
| **Minimal** ($0-2K) | Compliance → Development Guidelines → DIY Testing Only* |

*DIY-only not recommended for regulatory submission evidence

---

## Template Variables Reference

### Common Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{PROJECT_NAME}` | Project name | HnVue Console |
| `{DEVICE_TYPE}` | Medical device type | X-ray Imaging System |
| `{IEC_CLASS}` | IEC 62304 safety class | Class B/C |
| `{TECH_STACK}` | Technology stack | C# .NET 8, WPF, gRPC |
| `{TARGET_MARKETS}` | Regulatory targets | FDA, EU MDR, MFDS |

### Template-Specific Variables

**Development Guidelines:**
```
{TEAM_NAME}, {COMPLIANCE_FRAMEWORK}
```

**Pentest Preparation:**
```
{TESTING_WINDOW}, {BUDGET}, {CONTACT_PERSON}
```

**DIY Testing:**
```
{TEST_NETWORK}, {BUDGET_RANGE}
```

---

## Usage Instructions

### Step 1: Select Template

Choose template based on project phase and requirements (see Quick Reference above).

### Step 2: Copy and Customize

1. Copy the template to your project
2. Replace all `{VARIABLE}` placeholders
3. Adapt sections to your specific context
4. Remove irrelevant sections

### Step 3: Review and Approve

1. Internal review by security/architecture
2. Management approval for compliance documents
3. Regulatory review for submission documents

### Step 4: Implement and Maintain

1. Follow guidelines during development
2. Update documents as project evolves
3. Conduct regular reviews (quarterly recommended)

---

## Integration with MoAI-ADK

These templates integrate with the MoAI Autonomous Development Kit:

### SPEC Workflow

- **Plan Phase:** Use Compliance template for threat modeling
- **Run Phase:** Use Development Guidelines for implementation
- **Sync Phase:** Use Pentest/DIY templates for testing and documentation

### File Structure

```
.moai/
├── specs/
│   └── SPEC-SECURITY-001/
│       ├── spec.md           (uses Compliance template)
│       ├── plan.md           (uses Pentest Preparation template)
│       └── acceptance.md     (uses DIY Testing template)
│
docs/
├── templates/
│   └── security/
│       ├── medical-device-cybersecurity-template.md
│       ├── development-guideline-template.md
│       ├── pentest-preparation-template.md
│       └── diy-security-testing-template.md
│
└── security/                    (generated documents)
    ├── threat-model.md
    ├── security-architecture.md
    ├── str.md
    └── pentest-report.md
```

---

## Compliance Mapping

### FDA Section 524B

| Requirement | Template Coverage |
|-------------|-------------------|
| Cybersecurity features | Compliance, Development Guidelines |
| Vulnerability identification | Compliance, DIY Testing |
| Software BOM | Compliance, DIY Testing |
| Updates/patches | Compliance, Development Guidelines |
| Post-market management | Compliance |

### EU MDR MDCG 2019-16

| Requirement | Template Coverage |
|-------------|-------------------|
| IT security risk assessment | Compliance, DIY Testing |
| State-of-the-art security | Development Guidelines |
| Cybersecurity incident response | Compliance (IRP template) |
| User security training | Development Guidelines |

### MFDS Requirements

| Requirement | Template Coverage |
|-------------|-------------------|
| Cybersecurity technical document | Compliance |
| Risk management report | Compliance, DIY Testing |
| Software verification report | Pentest Preparation, DIY Testing |
| SBOM | Compliance, DIY Testing |

---

## Support and Contribution

### Template Maintenance

Templates are maintained as part of the MoAI-ADK project. Update considerations:

- **Quarterly:** Review regulatory updates (FDA, EU, MFDS)
- **Annually:** Update tool versions and best practices
- **As Needed:** Incorporate lessons learned from projects

### Contributing

To suggest improvements or additions:

1. Fork the repository
2. Modify templates with clear rationale
3. Submit pull request with description
4. Reference relevant regulatory updates

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-03-12 | Initial template collection |

---

## License

These templates are part of the MoAI Autonomous Development Kit and are provided for reuse in medical device software projects.

---

**Note:** These templates provide guidance and structure. Customization is required for each project's specific context, regulatory requirements, and technical constraints. Always consult with regulatory affairs and legal teams for compliance verification.

---

*Last Updated: 2026-03-12*
