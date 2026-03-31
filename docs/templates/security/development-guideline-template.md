# Medical Device Security Development Guidelines Template

> Reusable template for secure development practices in medical device software
> Target: Development teams implementing IEC 62304 Class B/C software

---

## Template Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{PROJECT_NAME}` | Project name | HnVue Console |
| `{TECH_STACK}` | Technology stack | C# .NET 8, WPF, gRPC |
| `{IEC_CLASS}` | IEC 62304 safety class | Class B/C |
| `{TEAM_NAME}` | Development team name | Platform Team |
| `{COMPLIANCE_FRAMEWORK}` | Security compliance framework | NIST CSF 2.0, AAMI TIR57 |

---

## 1. Secure Development Lifecycle (SDLC)

### 1.1 Development Phase Security Activities

| Phase | Security Activities | Deliverables | Owner |
|-------|---------------------|--------------|-------|
| **Requirements** | Threat modeling, security requirements | Threat model, security user stories | Security Lead |
| **Design** | Security architecture, control selection | Security architecture document | Architect |
| **Implementation** | Secure coding, code review | Secure code, review logs | Developers |
| **Testing** | Security testing, vulnerability scanning | Test reports, scan results | QA Team |
| **Deployment** | Security configuration, hardening | Deployment checklist | DevOps |
| **Maintenance** | Patch management, monitoring | Update logs, monitoring reports | Operations |

### 1.2 Security Gates

| Gate | Criteria | Approver |
|------|----------|----------|
| Requirements Complete | Threat model approved, security requirements defined | Security Lead |
| Design Complete | Security architecture reviewed, controls specified | Architect + Security Lead |
| Code Ready | SAST scan clean, peer review complete | Tech Lead + Security Reviewer |
| Test Ready | Security tests pass, vulnerability scan clean | QA + Security Lead |
| Release Ready | Penetration test complete, STR signed off | Quality Manager |

---

## 2. Secure Coding Standards

### 2.1 General Principles

| Principle | Description | Example |
|-----------|-------------|---------|
| **Secure by Default** | Deny all, allow explicitly | Whitelist input validation |
| **Defense in Depth** | Multiple security layers | TLS + RBAC + Audit logging |
| **Fail Securely** | Error handling doesn't compromise security | Generic error messages |
| **Principle of Least Privilege** | Minimum required access | Role-based permissions |
| **Complete Mediation** | Validate every access | Server-side permission checks |

### 2.2 Language-Specific Guidelines ({TECH_STACK})

#### C# Secure Coding Rules

| Rule ID | Category | Description | Example |
|---------|----------|-------------|---------|
| CSH-001 | Authentication | Never store passwords in plain text | Use Argon2id/bcrypt |
| CSH-002 | Cryptography | Use approved algorithms only | AES-256-GCM, TLS 1.3+ |
| CSH-003 | Input Validation | Validate all user input | Parameterized queries |
| CSH-004 | Error Handling | Don't expose internals in errors | Generic error messages |
| CSH-005 | Serialization | Safe deserialization practices | Avoid TypeNameHandling |
| CSH-006 | Logging | Don't log sensitive data | Mask PHI, passwords |
| CSH-007 | Dependencies | Keep dependencies updated | Regular vulnerability scans |
| CSH-008 | Concurrency | Thread-safe security checks | Lock-free atomic operations |

**Implementation Example:**

```csharp
// ❌ INSECURE - Do not do this
public void ProcessUser(string username, string password)
{
    // Plain text password storage
    _db.Save("user", username, password);

    // SQL injection vulnerable
    var query = $"SELECT * FROM Users WHERE Username = '{username}'";
    _db.Execute(query);
}

// ✅ SECURE - Do this instead
public async Task ProcessUserAsync(string username, string password)
{
    // Password hashing
    var hash = await _passwordHasher.HashAsync(password);
    await _db.SaveUserAsync(username, hash);

    // Parameterized query
    var query = "SELECT * FROM Users WHERE Username = @Username";
    await _db.ExecuteAsync(query, new { Username = username });
}
```

#### gRPC Security Best Practices

| Rule | Description | Implementation |
|------|-------------|----------------|
| GRPC-001 | Always use TLS | Configure `SslCredentials` |
| GRPC-002 | Implement mutual auth where required | mTLS certificate validation |
| GRPC-003 | Validate message size limits | `MaxReceiveMessageSize` |
| GRPC-004 | Implement rate limiting | Per-client call throttling |
| GRPC-005 | Log security events | Audit log gRPC calls |

**gRPC Channel Configuration:**

```csharp
// ✅ Secure gRPC channel configuration
var channel = GrpcChannel.ForAddress("https://localhost:50051", new GrpcChannelOptions
{
    Credentials = ChannelCredentials.Create(
        SslCredentials.Create(new SslCredentialsOptions
        {
            CertificateAuthorityCollection = new[] { caCert },
            CertificateCollection = new[] { clientCert },
            PrivateKey = clientKey
        })
    ),
    MaxReceiveMessageSize = 4 * 1024 * 1024, // 4 MB limit
    HttpHandler = new SocketsHttpHandler
    {
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
    }
});
```

### 2.3 OWASP Top 10 Mitigation

| OWASP 2021 | Mitigation Strategy | Code Review Checklist |
|------------|---------------------|----------------------|
| **A01: Broken Access Control** | Server-side permission checks, RBAC | [ ] All operations verify permissions |
| **A02: Cryptographic Failures** | TLS 1.3+, AES-256, secure key storage | [ ] No hardcoded secrets |
| **A03: Injection** | Parameterized queries, input validation | [ ] All queries use parameters |
| **A04: Insecure Design** | Threat modeling, secure patterns | [ ] Security requirements defined |
| **A05: Security Misconfiguration** | Secure defaults, hardened config | [ ] No default passwords |
| **A06: Vulnerable Components** | SBOM, dependency scanning | [ ] Zero critical/high CVEs |
| **A07: Authentication Failures** | MFA, secure session management | [ ] Session timeout implemented |
| **A08: Software Integrity** | Signed updates, checksums | [ ] Updates verified |
| **A09: Logging Failures** | Comprehensive audit logging | [ ] Security events logged |
| **A10: SSRF** | Input validation, network segmentation | [ ] URL validation implemented |

---

## 3. Code Review Guidelines

### 3.1 Security Review Checklist

**Every code review MUST include:**

| Category | Review Item | Status |
|----------|-------------|--------|
| **Authentication** | No password in plain text | [ ] |
| | Passwords hashed with Argon2id/bcrypt | [ ] |
| | Session tokens are cryptographically random | [ ] |
| **Authorization** | Permission checks on all protected operations | [ ] |
| | Server-side validation (not client-only) | [ ] |
| | Role-based access control enforced | [ ] |
| **Input Validation** | All user input validated | [ ] |
| | Parameterized queries for DB access | [ ] |
| | Length limits enforced | [ ] |
| **Cryptography** | Approved algorithms only | [ ] |
| | No hardcoded keys/secrets | [ ] |
| | Proper key management | [ ] |
| **Error Handling** | Generic error messages to users | [ ] |
| | Detailed errors logged securely | [ ] |
| | No stack traces to clients | [ ] |
| **Logging** | Security events logged | [ ] |
| | Sensitive data not logged | [ ] |
| | Log integrity protected | [ ] |
| **Dependencies** | No known vulnerabilities | [ ] |
| | Dependencies up to date | [ ] |
| | SBOM updated | [ ] |

### 3.2 Security Review Process

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│ Developer   │ -> │ Peer Review │ -> │ Security    │ -> │ Merge       │
│ Submits PR  │    │ (Code)      │    │ Review      │    │ Approved    │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
                           │                   │
                           v                   v
                      Checklist          SAST Scan
                      Completion         Clean
```

**Security Reviewer Responsibilities:**

1. Verify security checklist completion
2. Review sensitive code changes (auth, crypto, logging)
3. Approve only when security requirements met
4. Document any security concerns

---

## 4. Testing Guidelines

### 4.1 Security Test Categories

| Test Type | When | Frequency | Tools |
|-----------|------|-----------|-------|
| **Unit Tests** | Every commit | Continuous | xUnit, NUnit |
| **SAST** | Every commit | Continuous | SonarQube, Roslyn Analyzers |
| **Dependency Scan** | Daily | Automated | NuGet Audit, Snyk |
| **DAST** | Pre-release | Per sprint | OWASP ZAP |
| **Penetration Test** | Pre-market | 2x/year | External/Internal |

### 4.2 Security Test Case Template

```csharp
// Test template for security functionality
public class AuthenticationServiceTests
{
    [Fact]
    public async Task Authenticate_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var service = new AuthenticationService();
        var request = new AuthenticateRequest
        {
            Username = "testuser",
            Password = "ValidPassword123!"
        };

        // Act
        var result = await service.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result.Token);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(TimeSpan.FromMinutes(30), result.TokenLifetime);
    }

    [Theory]
    [InlineData("", "password")]          // Empty username
    [InlineData("user", "")]              // Empty password
    [InlineData("user", "short")]         // Weak password
    [InlineData("user", "a".Repeat(200))] // Too long
    public async Task Authenticate_InvalidInput_ReturnsError(
        string username, string password)
    {
        // Arrange
        var service = new AuthenticationService();
        var request = new AuthenticateRequest
        {
            Username = username,
            Password = password
        };

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationException>(
            () => service.AuthenticateAsync(request));
    }

    [Fact]
    public async Task Authenticate_FiveFailedAttempts_LocksAccount()
    {
        // Arrange
        var service = new AuthenticationService();
        var request = new AuthenticateRequest
        {
            Username = "testuser",
            Password = "WrongPassword"
        };

        // Act
        for (int i = 0; i < 5; i++)
        {
            await service.AuthenticateAsync(request);
        }

        // Assert
        var isLocked = await service.IsAccountLockedAsync("testuser");
        Assert.True(isLocked);
    }
}
```

### 4.3 Integration Test Template

```csharp
// gRPC security integration test
public class GrpcSecurityTests : IClassFixture<GrpcTestFixture>
{
    private readonly GrpcTestFixture _fixture;

    public GrpcSecurityTests(GrpcTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UserService_UnauthenticatedCall_ReturnsUnauthenticated()
    {
        // Arrange
        var client = new UserService.UserServiceClient(_fixture.Channel);

        // Act
        var exception = await Assert.ThrowsAsync<RpcException>(
            () => client.GetUserProfileAsync(new GetUserProfileRequest()));

        // Assert
        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task UserService_WithValidToken_AuthenticatesSuccessfully()
    {
        // Arrange
        var authClient = new UserService.UserServiceClient(_fixture.Channel);
        var token = await AuthenticateAsync(authClient, "testuser", "password");

        var headers = new Metadata();
        headers.Add("Authorization", $"Bearer {token}");

        // Act
        var profile = await authClient.GetUserProfileAsync(
            new GetUserProfileRequest(),
            headers);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("testuser", profile.Username);
    }
}
```

---

## 5. Dependency Management

### 5.1 Dependency Lifecycle

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│ Add Package│ -> │ Vulnerability│ -> │ Approve     │ -> │ Update      │
│ Request    │    │ Scan         │    │ Decision    │    │ SBOM        │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
```

### 5.2 Dependency Approval Process

| Step | Action | Tool |
|------|--------|------|
| 1 | Request dependency | Team discussion |
| 2 | Scan for vulnerabilities | Snyk, NuGet Audit |
| 3 | Check license compliance | License checker |
| 4 | Update SBOM | SPDX tools |
| 5 | Approve/reject | Tech Lead |

**Dependency Approval Criteria:**

- [ ] No critical or high vulnerabilities
- [ ] Compatible license (MIT, Apache, BSD)
- [ ] Actively maintained
- [ ] Compatible with {TECH_STACK}
- [ ] Necessary for functionality

### 5.3 SBOM Maintenance

**Update SBOM when:**
- New dependency added
- Dependency version updated
- Vulnerability patched
- Preparing for release

**SBOM Validation:**

```bash
# Generate SBOM
dotnet tool run dotnet-project-sbom -i . -o sbom.json

# Validate SBOM
dotnet tool run sbom-tool validate -b sbom.json

# Check for vulnerabilities
dotnet tool run snyk test --file=sbom.json
```

---

## 6. Configuration Management

### 6.1 Secure Configuration Guidelines

| Setting | Requirement | Default |
|---------|-------------|---------|
| TLS Version | 1.3+ minimum | TLS 1.3 |
| Cipher Suites | Approved only | TLS_AES_256_GCM_SHA384 |
| Session Timeout | 30 minutes max | 30 min |
| Password Policy | Min 12 chars, complexity | Enforced |
| Log Retention | 6 years minimum | 6 years |
| Backup Encryption | AES-256 | Enabled |

### 6.2 Configuration Validation

**Pre-deployment checklist:**

```csharp
// Configuration validator
public class SecurityConfigValidator
{
    public ValidationResult Validate(SecurityConfig config)
    {
        var errors = new List<string>();

        if (config.TlsVersion < TlsVersion.Tls13)
            errors.Add("TLS version must be 1.3 or higher");

        if (config.SessionTimeout > TimeSpan.FromMinutes(30))
            errors.Add("Session timeout must not exceed 30 minutes");

        if (config.LogRetention < TimeSpan.FromDays(365 * 6))
            errors.Add("Log retention must be at least 6 years");

        return new ValidationResult(errors);
    }
}
```

### 6.3 Secrets Management

**Rules:**
- ❌ NEVER commit secrets to version control
- ✅ Use environment variables for secrets
- ✅ Use secret management tools (Azure Key Vault, HashiCorp Vault)
- ✅ Rotate secrets regularly

**Example:**

```bash
# .env.local (gitignored)
DATABASE_CONNECTION=Server=localhost;Database=hnvue;...
API_SIGNING_KEY=<generated-key>
ENCRYPTION_KEY=<generated-key>
```

```csharp
// Configuration with secrets
builder.Configuration.AddEnvironmentVariables();
var encryptionKey = builder.Configuration["ENCRYPTION_KEY"];
```

---

## 7. Incident Response for Developers

### 7.1 Reporting Security Issues

**If you discover a security vulnerability:**

1. **DO NOT** commit the fix publicly
2. **DO** notify the security lead immediately
3. **DO** create a security advisory draft
4. **DO** follow responsible disclosure

**Contact:**
- Security Lead: [Email/Slack]
- Security Hotline: [Phone]

### 7.2 Security Incident Categories

| Category | Severity | Response Time |
|----------|----------|---------------|
| Critical | Patient safety at risk | Immediate (1 hour) |
| High | Data breach, auth bypass | Urgent (4 hours) |
| Medium | Service disruption, DoS | Prompt (24 hours) |
| Low | Potential vulnerability | Routine (72 hours) |

---

## 8. Continuous Improvement

### 8.1 Security Metrics

Track these metrics monthly:

| Metric | Target | Current |
|--------|--------|---------|
| Time to fix critical vulnerabilities | < 24 hours | ___ |
| SAST scan pass rate | > 95% | ___ |
| Security test coverage | > 80% | ___ |
| Security training completion | 100% | ___ |
| Known vulnerabilities | 0 critical/high | ___ |

### 8.2 Regular Security Activities

| Activity | Frequency | Owner |
|----------|-----------|-------|
| Threat model review | Quarterly | Security Lead |
| Dependency update | Monthly | Developers |
| Security training | Quarterly | All team |
| Penetration testing | Bi-annual | External |
| Policy review | Annually | Management |

---

## 9. Quick Reference

### 9.1 Secure Coding Checklist (Before Commit)

- [ ] No passwords/secrets in code
- [ ] Input validation on all inputs
- [ ] Parameterized queries for DB
- [ ] Error handling doesn't expose internals
- [ ] Security events logged
- [ ] Sensitive data masked in logs
- [ ] Approved cryptographic algorithms
- [ ] Dependencies scanned, no critical CVEs

### 9.2 Emergency Contacts

| Role | Name | Contact |
|------|------|---------|
| Security Lead | [Name] | [Email/Phone] |
| Tech Lead | [Name] | [Email/Phone] |
| Incident Response | [Team] | [Email/Slack] |

### 9.3 Useful Tools

| Tool | Purpose | Link |
|------|---------|------|
| SonarQube | SAST | https://www.sonarqube.org |
| OWASP ZAP | DAST | https://www.zaproxy.org |
| Snyk | Dependency scan | https://snyk.io |
| SPDX Tools | SBOM | https://spdx.dev |
| NuGet Audit | .NET dependencies | https://www.nuget.org |

---

## 10. Template Usage Instructions

1. **Customize Variables**: Replace `{VARIABLE}` placeholders
2. **Adapt Standards**: Modify for your tech stack
3. **Update Policies**: Align with company security policies
4. **Train Team**: Ensure all developers understand guidelines
5. **Review Regularly**: Update as threats and standards evolve

---

*This template provides reusable secure development guidelines for medical device software projects. Customize per project requirements.*
