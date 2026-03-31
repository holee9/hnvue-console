# SPEC-SECURITY-001: Security Foundation Architecture

> Technical Architecture Design Document
> IEC 62304 Class B/C | FDA Section 524B | EU MDR MDCG 2019-16

---

## Document Information

| Item | Content |
|------|---------|
| **SPEC ID** | SPEC-SECURITY-001 |
| **Document Type** | Architecture Design |
| **Version** | 1.0 |
| **Created** | 2026-03-12 |
| **Author** | team-architect |

---

## 1. Executive Overview

This document defines the technical architecture for security foundation components in HnVue Console medical X-ray software. The design addresses FDA, EU MDR, and MFDS cybersecurity requirements.

### 1.1 Scope

| Component | Priority | IEC 62304 Class |
|-----------|----------|-----------------|
| UserService (RBAC, Session, Password) | P0 | B |
| AuditLogService (WORM, SHA-256) | P0 | B |
| gRPC Security (TLS 1.3, mTLS) | P0 | C |
| SBOM Automation | P1 | A |

### 1.2 Design Principles

1. **Defense in Depth**: Multiple security layers at presentation, application, service, and data tiers
2. **Least Privilege**: RBAC with server-side permission enforcement
3. **Non-Repudiation**: Tamper-proof audit logging with cryptographic signatures
4. **Secure by Default**: TLS 1.3 mandatory, plaintext communication blocked

---

## 2. System Architecture Overview

### 2.1 Security Layers Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        HnVue Console Security Architecture              │
├─────────────────────────────────────────────────────────────────────────┤
│  Layer 1: Presentation (WPF UI)                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  - Input Validation (ViewModel validators)                        │  │
│  │  - UI State-based RBAC (CanExecute checks)                        │  │
│  │  - Session timeout handling                                       │  │
│  └──────────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────┤
│  Layer 2: Application (ViewModels)                                      │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  - Business logic validation                                      │  │
│  │  - Permission checks before commands                              │  │
│  │  - Audit event emission                                           │  │
│  └──────────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────┤
│  Layer 3: Service Adapters (gRPC Clients)                               │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  - UserServiceAdapter: Authentication/Authorization               │  │
│  │  - AuditLogServiceAdapter: Audit trail management                 │  │
│  │  - Channel: TLS 1.3 + mTLS                                        │  │
│  └──────────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────┤
│  Layer 4: gRPC Transport Security                                       │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  - TLS 1.3 encryption (mandatory)                                 │  │
│  │  - mTLS mutual authentication                                     │  │
│  │  - Certificate pinning                                            │  │
│  │  - Session token in metadata                                      │  │
│  └──────────────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────────────┤
│  Layer 5: Backend Services (External Process)                           │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  - UserService (RBAC enforcement, session validation)             │  │
│  │  - AuditLogService (WORM storage, SHA-256 signing)                │  │
│  │  - AES-256 data encryption                                        │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 3. UserService Architecture

### 3.1 RBAC Model

#### Role Hierarchy

```
┌──────────────────┐
│ Administrator    │  Full access: Users, Network, Logging, Calibration
└────────┬─────────┘
         │
┌────────▼─────────┐
│ ServiceEngineer  │  Calibration, Service modes, Diagnostic tools
└────────┬─────────┘
         │
┌────────▼─────────┐
│ Supervisor       │  QC, Reporting, Dose monitoring
└────────┬─────────┘
         │
┌────────▼─────────┐
│ Operator         │  Basic acquisition, Patient selection
└──────────────────┘
```

#### Permission Matrix

| Function | Operator | Supervisor | Administrator | ServiceEngineer |
|----------|----------|------------|---------------|-----------------|
| Patient Selection | READ | READ/WRITE | READ/WRITE | READ |
| Acquisition Start | YES | YES | YES | YES |
| Exposure Control | YES | YES | YES | YES |
| QC Functions | NO | YES | YES | YES |
| User Management | NO | NO | YES | NO |
| Network Config | NO | NO | YES | NO |
| Calibration | NO | NO | YES | YES |
| Service Mode | NO | NO | NO | YES |
| Audit Log View | NO | READ | READ/WRITE | READ |

### 3.2 Session Management

#### Session Lifecycle

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   LOGIN     │────▶│   ACTIVE    │────▶│   EXPIRED   │
└─────────────┘     └──────┬──────┘     └─────────────┘
                          │                    │
                          │ Timeout/Logout     │
                          ▼                    ▼
                    ┌─────────────┐     ┌─────────────┐
                    │   LOGOUT    │────▶│  INVALID    │
                    └─────────────┘     └─────────────┘
```

#### Session Token Structure

```csharp
public record SessionToken
{
    public required string SessionId { get; init; }         // GUID v4
    public required string UserId { get; init; }            // User identifier
    public required DateTimeOffset IssuedAt { get; init; }  // Token creation time
    public required DateTimeOffset ExpiresAt { get; init; } // Expiration (30 min idle)
    public required string DeviceId { get; init; }          // Client device fingerprint
    public required byte[] Signature { get; init; }         // HMAC-SHA256 signature
}
```

#### Session Security Properties

| Property | Value | Rationale |
|----------|-------|-----------|
| Idle Timeout | 30 minutes | Balance security vs usability |
| Absolute Timeout | 8 hours | Shift boundary enforcement |
| Max Concurrent Sessions | 1 per user | Prevent session sharing |
| Token Format | JWT (RS256) | Industry standard, tamper-evident |

### 3.3 Password Policy

#### Complexity Requirements

| Requirement | Value | Implementation |
|-------------|-------|----------------|
| Minimum Length | 12 characters | Server-side validation |
| Maximum Length | 128 characters | DoS prevention |
| Character Classes | 3 of 4 (upper, lower, digit, special) | Complexity enforcement |
| History | Last 10 passwords | Prevent password reuse |
| Max Age | 90 days | Compliance requirement |
| Lockout Threshold | 5 failed attempts | Brute force protection |
| Lockout Duration | 30 minutes | Automatic unlock |

#### Password Storage

```
┌─────────────────────────────────────────────────────────────────┐
│ Password Hash Storage Format                                     │
├─────────────────────────────────────────────────────────────────┤
│  Algorithm: Argon2id                                            │
│  Memory Cost: 64 MB                                             │
│  Time Cost: 3 iterations                                        │
│  Parallelism: 4 threads                                         │
│  Salt: 32 bytes (random per password)                           │
│  Hash Output: 32 bytes                                          │
└─────────────────────────────────────────────────────────────────┘
```

### 3.4 Interface Contracts

#### IUserService Extension (Security Enhancement)

```csharp
namespace HnVue.Console.Services;

/// <summary>
/// Extended user service interface with security features.
/// SPEC-SECURITY-001: Authentication and authorization.
/// </summary>
public interface IUserService
{
    // Existing methods (preserved)
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct);
    Task<User> GetCurrentUserAsync(CancellationToken ct);
    Task<UserRole> GetCurrentUserRoleAsync(CancellationToken ct);
    Task<bool> CanAccessSectionAsync(ConfigSection section, CancellationToken ct);
    Task CreateUserAsync(User user, string password, CancellationToken ct);
    Task UpdateUserAsync(User user, CancellationToken ct);
    Task DeactivateUserAsync(string userId, CancellationToken ct);
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct);

    // New security methods
    /// <summary>
    /// Authenticates user and creates a new session.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken ct);

    /// <summary>
    /// Invalidates current session.
    /// </summary>
    Task LogoutAsync(CancellationToken ct);

    /// <summary>
    /// Renews session token before expiration.
    /// </summary>
    Task<SessionToken?> RenewSessionAsync(CancellationToken ct);

    /// <summary>
    /// Changes user password with validation.
    /// </summary>
    Task<PasswordChangeResult> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct);

    /// <summary>
    /// Checks if current session is valid and active.
    /// </summary>
    Task<bool> IsSessionValidAsync(CancellationToken ct);

    /// <summary>
    /// Gets session information for display.
    /// </summary>
    Task<SessionInfo?> GetCurrentSessionInfoAsync(CancellationToken ct);
}

/// <summary>
/// Authentication result with session token.
/// </summary>
public record AuthenticationResult
{
    public required bool Success { get; init; }
    public required string? ErrorMessage { get; init; }
    public required SessionToken? Token { get; init; }
    public required int FailedAttemptsRemaining { get; init; }
}

/// <summary>
/// Password change result.
/// </summary>
public record PasswordChangeResult
{
    public required bool Success { get; init; }
    public required string? ErrorMessage { get; init; }
    public required IReadOnlyList<string> PolicyViolations { get; init; }
}

/// <summary>
/// Session display information.
/// </summary>
public record SessionInfo
{
    public required string UserName { get; init; }
    public required UserRole Role { get; init; }
    public required DateTimeOffset LoginTime { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required int MinutesUntilExpiry { get; init; }
}
```

---

## 4. AuditLogService Architecture

### 4.1 Log Entry Structure (Enhanced)

```csharp
namespace HnVue.Console.Models;

/// <summary>
/// Enhanced audit log entry with cryptographic integrity.
/// SPEC-SECURITY-001: Tamper-proof audit trail (FDA 21 CFR Part 11).
/// </summary>
public record AuditLogEntry
{
    // Existing fields (preserved)
    public required string EntryId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required AuditEventType EventType { get; init; }
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string EventDescription { get; init; }
    public required string? PatientId { get; init; }
    public required string? StudyId { get; init; }
    public required AuditOutcome Outcome { get; init; }

    // New integrity fields
    public required string PreviousEntryHash { get; init; }  // Blockchain-style linking
    public required string EntryHash { get; init; }          // SHA-256 of this entry
    public required string DigitalSignature { get; init; }   // RSA-2048 signature
    public required string SequenceNumber { get; init; }     // Monotonic counter
}
```

### 4.2 Log Integrity Chain

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Audit Log Integrity Chain                         │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Entry 1          Entry 2          Entry 3          Entry N         │
│  ┌────────┐       ┌────────┐       ┌────────┐       ┌────────┐      │
│  │ Data   │       │ Data   │       │ Data   │       │ Data   │      │
│  │ Hash₁  │◀──────│ Hash₂  │◀──────│ Hash₃  │◀──────│ Hashₙ  │      │
│  │ Prev=0 │       │ Prev=H₁│       │ Prev=H₂│       │ Prev=Hₙ│      │
│  │ Sig₁   │       │ Sig₂   │       │ Sig₃   │       │ Sigₙ   │      │
│  └────────┘       └────────┘       └────────┘       └────────┘      │
│                                                                      │
│  Where: Hashₙ = SHA256(Dataₙ || PrevHashₙ || SequenceNumberₙ)       │
│         Sigₙ = RSA-Sign(Hashₙ, PrivateKey)                          │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.3 WORM Storage Strategy

#### Storage Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      WORM Storage Layers                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Hot Storage (Last 7 days)                                   │    │
│  │  - SQLite database with memory-mapped I/O                    │    │
│  │  - Full-text search enabled                                  │    │
│  │  - Automatic backup to Warm Storage                          │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Warm Storage (8-90 days)                                    │    │
│  │  - Append-only log files (JSON Lines format)                 │    │
│  │  - Compressed daily archives (gzip)                          │    │
│  │  - Integrity verification on read                            │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Cold Storage (91+ days, regulatory retention)               │    │
│  │  - Write-once media (WORM tape or immutable cloud storage)   │    │
│  │  - AES-256 encrypted archives                                │    │
│  │  - 7-year minimum retention (medical device regulations)     │    │
│  └─────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

#### Retention Policy

| Age | Storage Tier | Access Speed | Retention |
|-----|--------------|--------------|-----------|
| 0-7 days | Hot (SQLite) | < 100ms | Auto-archive daily |
| 8-90 days | Warm (Compressed) | < 5s | Auto-delete after 90 days |
| 90+ days | Cold (WORM) | < 5 min | 7 years minimum |

### 4.4 Enhanced Audit Event Types

```csharp
namespace HnVue.Console.Models;

/// <summary>
/// Comprehensive audit event types for medical device compliance.
/// </summary>
public enum AuditEventType
{
    // Patient Data Events
    PatientRegistration,
    PatientEdit,
    PatientDataAccess,
    PatientDataExport,

    // Study/Exposure Events
    StudyStart,
    StudyComplete,
    ExposureInitiated,
    ExposureCompleted,
    ExposureAborted,

    // Image Events
    ImageAccepted,
    ImageRejected,
    ImageReprocessed,
    ImageExported,
    ImageDeleted,

    // Configuration Events
    ConfigChange,
    CalibrationPerformed,
    NetworkConfigChange,

    // Security Events
    UserLogin,
    UserLogout,
    UserLoginFailed,
    UserLockedOut,
    PasswordChanged,
    PasswordReset,
    SessionExpired,
    PermissionDenied,

    // System Events
    SystemStartup,
    SystemShutdown,
    SystemError,
    ServiceModeEntered,
    ServiceModeExited,

    // Dose Events
    DoseAlertExceeded,
    DoseProtocolChanged,

    // Audit Events (meta-auditing)
    AuditLogExported,
    AuditLogAccessed,
    AuditIntegrityCheckFailed
}
```

### 4.5 Interface Contracts

#### IAuditLogService Extension

```csharp
namespace HnVue.Console.Services;

/// <summary>
/// Extended audit log service with integrity verification.
/// SPEC-SECURITY-001: Tamper-proof audit logging.
/// </summary>
public interface IAuditLogService
{
    // Existing methods (preserved)
    Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(AuditLogFilter filter, CancellationToken ct);
    Task<PagedAuditLogResult> GetLogsPagedAsync(int pageNumber, int pageSize, AuditLogFilter? filter, CancellationToken ct);
    Task<AuditLogEntry?> GetLogEntryAsync(string entryId, CancellationToken ct);
    Task<int> GetLogCountAsync(AuditLogFilter filter, CancellationToken ct);
    Task<byte[]> ExportLogsAsync(AuditLogFilter filter, CancellationToken ct);

    // New security methods
    /// <summary>
    /// Logs an audit event with automatic integrity protection.
    /// </summary>
    Task<AuditLogEntry> LogEventAsync(AuditEventRequest request, CancellationToken ct);

    /// <summary>
    /// Verifies integrity of the entire audit log chain.
    /// </summary>
    Task<IntegrityVerificationResult> VerifyIntegrityAsync(DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct);

    /// <summary>
    /// Verifies a single entry's integrity.
    /// </summary>
    Task<bool> VerifyEntryAsync(string entryId, CancellationToken ct);

    /// <summary>
    /// Exports logs in regulatory-compliant format (signed PDF/CSV).
    /// </summary>
    Task<SignedExportResult> ExportSignedAsync(AuditLogFilter filter, ExportFormat format, CancellationToken ct);

    /// <summary>
    /// Gets audit statistics for compliance reporting.
    /// </summary>
    Task<AuditStatistics> GetStatisticsAsync(DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct);
}

/// <summary>
/// Request for logging an audit event.
/// </summary>
public record AuditEventRequest
{
    public required AuditEventType EventType { get; init; }
    public required string EventDescription { get; init; }
    public required string? PatientId { get; init; }
    public required string? StudyId { get; init; }
    public required AuditOutcome Outcome { get; init; }
    public required IReadOnlyDictionary<string, string> AdditionalData { get; init; }
}

/// <summary>
/// Result of integrity verification.
/// </summary>
public record IntegrityVerificationResult
{
    public required bool IsValid { get; init; }
    public required int EntriesVerified { get; init; }
    public required IReadOnlyList<string> InvalidEntryIds { get; init; }
    public required string VerificationTimestamp { get; init; }
}

/// <summary>
/// Signed export result.
/// </summary>
public record SignedExportResult
{
    public required byte[] Data { get; init; }
    public required string FileName { get; init; }
    public required string Signature { get; init; }
    public required DateTimeOffset ExportTimestamp { get; init; }
}

/// <summary>
/// Audit statistics for compliance reporting.
/// </summary>
public record AuditStatistics
{
    public required int TotalEvents { get; init; }
    public required int SecurityEvents { get; init; }
    public required int PatientDataEvents { get; init; }
    public required int FailedLogins { get; init; }
    public required int PermissionDeniedCount { get; init; }
}
```

---

## 5. gRPC Security Architecture

### 5.1 TLS 1.3 Configuration

#### Channel Configuration

```csharp
namespace HnVue.Console.Security;

/// <summary>
/// Secure gRPC channel configuration.
/// SPEC-SECURITY-001: Transport security (TLS 1.3).
/// </summary>
public static class GrpcSecurityConfiguration
{
    /// <summary>
    /// Creates a secure gRPC channel with TLS 1.3.
    /// </summary>
    public static GrpcChannel CreateSecureChannel(string address, X509Certificate2 clientCertificate)
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                // TLS 1.3 mandatory
                EnabledSslProtocols = SslProtocols.Tls13,

                // Certificate validation
                RemoteCertificateValidationCallback = ValidateServerCertificate,

                // Client certificate for mTLS
                ClientCertificates = new X509CertificateCollection { clientCertificate },

                // Certificate revocation check
                CertificateRevocationCheckMode = X509RevocationMode.Online,

                // Cipher suite preference
                CipherSuitesPolicy = new CipherSuitesPolicy(new[]
                {
                    TlsCipherSuite.TLS_AES_256_GCM_SHA384,
                    TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                    TlsCipherSuite.TLS_AES_128_GCM_SHA256
                })
            },

            // Connection timeout
            ConnectTimeout = TimeSpan.FromSeconds(10),

            // Enable HTTP/2
            EnableMultipleHttp2Connections = true
        };

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = handler,
            // Session token injection
            Credentials = CallCredentials.FromInterceptor(InjectSessionToken)
        });
    }

    /// <summary>
    /// Server certificate validation with pinning.
    /// </summary>
    private static bool ValidateServerCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors != SslPolicyErrors.None)
        {
            // Log validation failure
            return false;
        }

        // Certificate pinning: verify expected thumbprint
        var thumbprint = certificate?.GetCertHashString(HashAlgorithmName.SHA256);
        return thumbprint == ExpectedServerThumbprint;
    }

    /// <summary>
    /// Session token injection into gRPC metadata.
    /// </summary>
    private static async Task InjectSessionToken(
        Metadata metadata,
        string serviceUrl,
        CallCredentialsProvider provider)
    {
        var sessionToken = await provider.GetTokenAsync();
        if (!string.IsNullOrEmpty(sessionToken))
        {
            metadata.Add("Authorization", $"Bearer {sessionToken}");
        }
    }
}
```

### 5.2 mTLS Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    mTLS Authentication Flow                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Client (HnVue Console)              Server (Backend Service)        │
│  ┌─────────────────────┐             ┌─────────────────────┐        │
│  │ 1. Client Cert      │             │                     │        │
│  │    (device-bound)   │────────────▶│ Verify Client Cert  │        │
│  │                     │             │                     │        │
│  │ 2. Verify Server    │◀────────────│ 2. Server Cert      │        │
│  │    Certificate      │             │    (CA signed)      │        │
│  │                     │             │                     │        │
│  │ 3. Establish TLS    │◀───────────▶│ 3. TLS 1.3 Channel  │        │
│  │    1.3 Tunnel       │             │    Established      │        │
│  │                     │             │                     │        │
│  │ 4. Session Token    │────────────▶│ 4. Validate Token   │        │
│  │    (JWT in header)  │             │    + RBAC Check     │        │
│  └─────────────────────┘             └─────────────────────┘        │
│                                                                      │
│  Certificate Requirements:                                           │
│  - Client: Device-specific, 1-year validity, RSA-2048 or ECDSA P-256│
│  - Server: CA-signed, 2-year validity, RSA-4096 or ECDSA P-384     │
│  - Root CA: Offline, 10-year validity, hardware security module     │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.3 Certificate Rotation Strategy

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Certificate Lifecycle                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Phase 1: Normal Operation                                    │   │
│  │  - Current certificate active                                 │   │
│  │  - 30+ days until expiration                                  │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Phase 2: Pre-Rotation (30 days before expiry)                │   │
│  │  - Generate new certificate request                           │   │
│  │  - Submit to CA for signing                                   │   │
│  │  - Stage new certificate (not yet active)                     │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Phase 3: Graceful Rotation (14 days before expiry)           │   │
│  │  - Both certificates accepted during overlap                  │   │
│  │  - Gradual migration to new certificate                       │   │
│  │  - Old certificate marked for revocation                      │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Phase 4: Post-Rotation                                       │   │
│  │  - Old certificate revoked in CRL                             │   │
│  │  - New certificate fully active                               │   │
│  │  - Audit log records rotation event                           │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.4 GrpcAdapterBase Enhancement

```csharp
namespace HnVue.Console.Services.Adapters;

/// <summary>
/// Enhanced gRPC adapter base with security features.
/// SPEC-SECURITY-001: Secure gRPC communication.
/// </summary>
public abstract class GrpcAdapterBase : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ILogger _logger;
    private readonly ISessionManager _sessionManager;
    private bool _disposed;

    protected GrpcAdapterBase(
        IConfiguration configuration,
        ILogger logger,
        ISessionManager sessionManager,
        X509Certificate2? clientCertificate = null)
    {
        var address = configuration["GrpcServer:Address"]
            ?? throw new InvalidOperationException("GrpcServer:Address not configured");

        // TLS enforcement check
        if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException("Non-TLS gRPC connections are prohibited");
        }

        _channel = CreateSecureChannel(address, configuration, clientCertificate);
        _logger = logger;
        _sessionManager = sessionManager;

        _logger.LogInformation(
            "Secure gRPC channel created for {Address} with TLS 1.3",
            address);
    }

    /// <summary>
    /// Creates a secure channel with TLS 1.3 and mTLS.
    /// </summary>
    private static GrpcChannel CreateSecureChannel(
        string address,
        IConfiguration configuration,
        X509Certificate2? clientCertificate)
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls13,
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                    CertificateValidator.Validate(cert, chain, errors, configuration),

                ClientCertificates = clientCertificate is not null
                    ? new X509CertificateCollection { clientCertificate }
                    : new X509CertificateCollection()
            }
        };

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = handler,
            Credentials = CallCredentials.FromInterceptor(async (metadata, url) =>
            {
                // Inject session token for authenticated requests
                var token = await _sessionManager.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    metadata.Add("Authorization", $"Bearer {token}");
                }
            })
        });
    }

    /// <summary>
    /// Creates a typed gRPC client with automatic session handling.
    /// </summary>
    protected T CreateClient<T>() where T : ClientBase<T>
    {
        return (T)Activator.CreateInstance(typeof(T), _channel)!;
    }

    /// <summary>
    /// Validates channel connectivity and security.
    /// </summary>
    protected async Task<ChannelHealthResult> ValidateChannelHealthAsync(
        CancellationToken ct = default)
    {
        try
        {
            await _channel.ConnectAsync(ct);
            var state = _channel.State;

            return new ChannelHealthResult
            {
                IsHealthy = state == ConnectivityState.Ready,
                State = state,
                SecurityProtocol = "TLS 1.3",
                ServerCertificateThumbprint = GetServerCertificateThumbprint()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Channel health check failed");
            return new ChannelHealthResult
            {
                IsHealthy = false,
                State = _channel.State,
                ErrorMessage = ex.Message
            };
        }
    }
}

public record ChannelHealthResult
{
    public required bool IsHealthy { get; init; }
    public required ConnectivityState State { get; init; }
    public string? SecurityProtocol { get; init; }
    public string? ServerCertificateThumbprint { get; init; }
    public string? ErrorMessage { get; init; }
}
```

---

## 6. SBOM Architecture

### 6.1 components.json Structure (NTIA Minimum Elements)

```json
{
  "sbom": {
    "version": "1.5",
    "format": "SPDX-2.3",
    "created": "2026-03-12T10:00:00Z",
    "creators": ["Tool: HnVue-SBOM-Generator-1.0"],
    "documentNamespace": "https://hnvue.abyzr.com/sbom/hnvue-console/1.0.0"
  },
  "packages": [
    {
      "SPDXID": "SPDXRef-Package-NuGet-Microsoft.Extensions.DependencyInjection-8.0.0",
      "name": "Microsoft.Extensions.DependencyInjection",
      "versionInfo": "8.0.0",
      "supplier": "Organization: Microsoft",
      "downloadLocation": "https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/8.0.0",
      "filesAnalyzed": false,
      "licenseConcluded": "MIT",
      "licenseDeclared": "MIT",
      "copyrightText": "Copyright (c) Microsoft Corporation",
      "externalRefs": [
        {
          "referenceCategory": "SECURITY",
          "referenceType": "cpe23Type",
          "referenceLocator": "cpe:2.3:a:microsoft:extensions.dependencyinjection:8.0.0:*:*:*:*:*:*:*"
        },
        {
          "referenceCategory": "PACKAGE-MANAGER",
          "referenceType": "purl",
          "referenceLocator": "pkg:nuget/Microsoft.Extensions.DependencyInjection@8.0.0"
        }
      ]
    }
  ],
  "relationships": [
    {
      "spdxElementId": "SPDXRef-DOCUMENT",
      "relationshipType": "DESCRIBES",
      "relatedSpdxElement": "SPDXRef-Package-HnVue-Console-1.0.0"
    }
  ]
}
```

### 6.2 CI Pipeline Integration

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SBOM Generation Pipeline                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  .github/workflows/sbom.yml                                          │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Trigger: push to main, PR, release, manual                  │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Step 1: Restore Dependencies                                │    │
│  │  - dotnet restore --locked-mode                              │    │
│  │  - Use packages.lock.json for reproducibility                │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Step 2: Generate SBOM                                       │    │
│  │  - PowerShell: .sbom/generate-sbom.ps1                       │    │
│  │  - Parse project files, extract package metadata             │    │
│  │  - Output: components.json (SPDX 2.3 format)                 │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Step 3: CVE Scanning (OSV-Scanner)                          │    │
│  │  - Scan components.json and lock files                       │    │
│  │  - Query OSV database for known vulnerabilities              │    │
│  │  - Output: osv-results.json                                  │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Step 4: NTIA Compliance Validation                          │    │
│  │  - PowerShell: .sbom/validate-ntia.ps1                       │    │
│  │  - Check all required fields present                         │    │
│  │  - Fail build on non-compliance                              │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  Step 5: Artifact Upload                                     │    │
│  │  - Upload components.json (90-day retention)                 │    │
│  │  - Upload packages.lock.json (90-day retention)              │    │
│  │  - Upload osv-results.json (90-day retention)                │    │
│  │  - On release: Attach SBOM to release assets                 │    │
│  └─────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

### 6.3 File Impact Analysis

#### New Files

| Path | Purpose |
|------|---------|
| `.sbom/generate-sbom.ps1` | SBOM generation script |
| `.sbom/validate-ntia.ps1` | NTIA compliance validation |
| `.sbom/templates/spdx-2.3.json` | SPDX template |
| `docs/security/sbom-policy.md` | SBOM management policy |

#### Modified Files

| Path | Changes |
|------|---------|
| `.github/workflows/sbom.yml` | Already exists, verify integration |
| `Directory.Build.props` | Add SBOM metadata properties |
| `src/HnVue.Console/HnVue.Console.csproj` | Add package metadata |

---

## 7. Implementation Order

### Phase 1: Foundation (Week 1-2)

```
1.1 gRPC TLS Configuration
    ├── GrpcSecurityConfiguration.cs
    ├── CertificateValidator.cs
    ├── GrpcAdapterBase enhancement
    └── Tests: GrpcSecurityTests.cs

1.2 SBOM Infrastructure
    ├── .sbom/generate-sbom.ps1
    ├── .sbom/validate-ntia.ps1
    └── CI workflow verification
```

### Phase 2: Authentication (Week 3-4)

```
2.1 Session Management
    ├── SessionToken.cs
    ├── ISessionManager.cs
    ├── SessionManager.cs
    └── Tests: SessionManagerTests.cs

2.2 Password Policy
    ├── PasswordPolicy.cs
    ├── IPasswordValidator.cs
    ├── Argon2PasswordHasher.cs
    └── Tests: PasswordPolicyTests.cs

2.3 UserService Implementation
    ├── IUserService extension
    ├── UserServiceAdapter enhancement
    └── Tests: UserServiceTests.cs
```

### Phase 3: Audit Logging (Week 5-6)

```
3.1 Audit Integrity
    ├── AuditLogEntry enhancement
    ├── AuditIntegrityCalculator.cs
    ├── AuditSigner.cs
    └── Tests: AuditIntegrityTests.cs

3.2 WORM Storage
    ├── IAuditStorage.cs
    ├── SqliteAuditStorage.cs (Hot)
    ├── FileAuditStorage.cs (Warm)
    └── Tests: AuditStorageTests.cs

3.3 AuditLogService Implementation
    ├── IAuditLogService extension
    ├── AuditLogServiceAdapter enhancement
    └── Tests: AuditLogServiceTests.cs
```

### Phase 4: mTLS (Week 7)

```
4.1 Certificate Management
    ├── ICertificateManager.cs
    ├── CertificateManager.cs
    ├── CertificateRotationService.cs
    └── Tests: CertificateTests.cs
```

---

## 8. Risk Mitigation

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| TLS 1.3 compatibility issues | Low | High | Fallback to TLS 1.2 with deprecation warning |
| Certificate pinning breaks on rotation | Medium | High | Graceful overlap period, both certs accepted |
| Password hashing performance | Low | Medium | Benchmark Argon2id parameters for <100ms |
| Audit log storage overflow | Medium | Medium | Implement automatic archiving and compression |
| mTLS client cert distribution | Medium | High | Centralized cert management, auto-renewal |

### Security Risks

| Risk | Mitigation |
|------|------------|
| Session token theft | Short expiry, token binding to client cert |
| Audit log tampering | Blockchain-style hash chain, digital signatures |
| Brute force attacks | Account lockout, rate limiting |
| Certificate compromise | Short validity, CRL/OCSP, rotation automation |

---

## 9. Testing Strategy

### Security Test Categories

| Category | Coverage Target | Tools |
|----------|-----------------|-------|
| Unit Tests | 90% | xUnit, NSubstitute |
| Integration Tests | 80% | Testcontainers, WireMock |
| Security Tests | 100% critical paths | Custom security test suite |
| Penetration Tests | Annual | Third-party assessment |

### TDD Approach (per quality.yaml)

Following RED-GREEN-REFACTOR:
1. **RED**: Write security test first (e.g., test that TLS 1.2 is rejected)
2. **GREEN**: Implement minimum code to pass
3. **REFACTOR**: Optimize while keeping tests green

---

## 10. Compliance Mapping

### Regulatory Requirements Coverage

| Requirement | Architecture Component | Verification Method |
|-------------|------------------------|---------------------|
| FDA 524B(a)(1) - Authentication | UserService, RBAC | Penetration test |
| FDA 524B(a)(2) - Authorization | Permission matrix | Unit tests |
| FDA 524B(a)(3) - Audit | AuditLogService, WORM | Integrity verification |
| FDA 524B(a)(4) - Encryption | TLS 1.3, AES-256 | Security scan |
| FDA 524B(a)(5) - SBOM | components.json | NTIA validation |
| EU MDR MDCG 2019-16 | All components | Technical file review |

---

## Appendix A: Existing Code References

### Files to Preserve/Enhance

| File | Current Status | Enhancement Required |
|------|----------------|---------------------|
| `src/HnVue.Console/Services/IUserService.cs` | Interface defined | Add security methods |
| `src/HnVue.Console/Services/IAuditLogService.cs` | Interface defined | Add integrity methods |
| `src/HnVue.Console/Services/Adapters/UserServiceAdapter.cs` | Partial implementation | Full RBAC implementation |
| `src/HnVue.Console/Services/Adapters/AuditLogServiceAdapter.cs` | Partial implementation | Integrity protection |
| `src/HnVue.Console/Services/Adapters/GrpcAdapterBase.cs` | Basic channel | TLS 1.3, mTLS support |
| `src/HnVue.Console/Models/ConfigModels.cs` | UserRole enum | Permission system |
| `src/HnVue.Console/Models/AuditModels.cs` | Basic models | Integrity fields |
| `.github/workflows/sbom.yml` | Implemented | Verify NTIA compliance |

---

**Document Status**: Complete - Ready for Review

**Next Steps**:
1. Team lead review and approval
2. Threat modeling validation (Task #6)
3. Implementation sprint planning
