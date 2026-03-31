# WORM Storage Implementation Plan

## Document Information

| Field | Value |
|-------|-------|
| SPEC ID | SPEC-SECURITY-001 |
| Requirement | FR-SEC-06 (Audit Log Integrity - WORM storage) |
| Created | 2026-03-13 |
| Version | 1.0.0 |
| Status | Planning |

---

## 1. Current Architecture Analysis

### 1.1 Existing Audit Log Storage

**Current Implementation** (`AuditLogServiceAdapter.cs`):

- **Storage Location**: `%LocalAppData%\HnVue\AuditLogs\`
- **File Format**: JSON files with `.audit` extension
- **Naming Convention**: `{timestamp}_{entryId}.audit` (chronologically sortable)
- **Atomic Write**: Temporary file (`.tmp`) with atomic rename for crash safety
- **Hash Chain**: SHA-256 based chain linking each entry to previous
- **Retention**: 6-year policy with `EnforceRetentionPolicyAsync()`

**Example File Path**:
```
C:\Users\{user}\AppData\Local\HnVue\AuditLogs\20260313143052_3a7b8c9d12ef.audit
```

**File Content Structure**:
```json
{
  "entryId": "3a7b8c9d12ef",
  "eventType": 1,
  "timestamp": "2026-03-13T14:30:52.1234567+00:00",
  "userId": "admin",
  "userName": "System Administrator",
  "eventDescription": "User login successful",
  "outcome": 0,
  "patientId": null,
  "studyId": null,
  "previousRecordHash": "A1B2C3D4...",
  "currentRecordHash": "E5F6G7H8...",
  "ntpSynchronized": true
}
```

### 1.2 Current WORM Simulation

**Current "Soft WORM" Implementation**:

1. **Atomic Rename** (`WriteEntryAtomically` method, line 561-570):
   ```csharp
   File.WriteAllText(tempPath, json, _utf8Encoding);
   File.Move(tempPath, filePath, overwrite: false);
   ```
   - Creates `.tmp` file first
   - Atomic rename prevents partial writes
   - `overwrite: false` prevents accidental overwrites

2. **No Delete/Modify Methods**:
   - `IAuditLogService` interface has no `UpdateLogAsync()` or `DeleteLogAsync()` methods
   - Only `EnforceRetentionPolicyAsync()` can delete after 6 years

3. **Limitations**:
   - Files can be manually deleted with admin privileges
   - No OS-level immutability enforcement
   - No protection against disk-level tampering
   - Relies entirely on application-layer controls

---

## 2. WORM Storage Options Analysis

### Option 1: Windows Immutable Files (Recommended for Production)

**Description**: Windows file system attributes that prevent modification/deletion.

**Pros**:
- Native Windows support (no additional infrastructure)
- OS-level enforcement (stronger than application controls)
- Simple implementation with `File.SetAttributes()`
- No additional cost

**Cons**:
- Can be removed by administrators (weaker than true WORM)
- Not compliant with strict regulatory requirements without additional controls
- Limited audit capabilities for modification attempts

**Implementation**:
```csharp
// After writing the file atomically
var fileInfo = new FileInfo(filePath);
fileInfo.Attributes |= FileAttributes.ReadOnly | FileAttributes.Archive;

// For stronger immutability (Windows 10+)
using var fileStream = new FileStream(
    filePath,
    FileMode.Open,
    FileAccess.Read,
    FileShare.Read,
    bufferSize: 4096,
    FileOptions.WriteThrough);
```

**Regulatory Compliance**: Medium confidence - may not satisfy strict FDA/MFDS requirements without additional controls.

---

### Option 2: Azure Immutable Blob Storage (Recommended for Cloud)

**Description**: Cloud-based WORM storage with legal hold support.

**Pros**:
- True WORM semantics (cannot be deleted until retention expires)
- Legal hold support (indefinite retention for litigation)
- Time-based retention policies
- Compliance with SEC 17a-4, FINRA, CFTC, and FDA guidelines
- Built-in redundancy and geo-replication

**Cons**:
- Requires Azure subscription
- Network dependency for audit log writes
- Additional cost (~$0.002 per GB/month for cool tier)
- Requires internet connectivity

**Implementation**:
```csharp
// Azure Blob Storage SDK
var blobClient = containerClient.GetBlobClient(blobName);
await blobClient.UploadAsync(binaryData);
await blobClient.SetImmutabilityPolicyAsync(
    immutabilityPolicy: new BlobImmutabilityPolicy(exppiresOn: retentionDate),
    legalHold: false);
```

**Cost Estimate**:
- Assumption: 10,000 audit entries per day, ~1KB per entry
- Daily: 10 MB/day
- Monthly: 300 MB/month
- Annual: 3.6 GB/year
- 6-year retention: 21.6 GB
- Monthly cost (Cool tier): $0.002 * 21.6 GB = ~$0.04/month

**Regulatory Compliance**: High confidence - compliant with SEC 17a-4, FINRA, CFTC, and FDA guidelines.

---

### Option 3: NAS WORM Storage (Recommended for On-Premises)

**Description**: Network-attached storage with WORM capabilities (e.g., Dell EMC, NetApp, QNAP).

**Pros**:
- True WORM semantics (hardware enforcement)
- No cloud dependency
- Can be integrated with existing hospital infrastructure
- Supports air-gapped environments

**Cons**:
- Additional hardware cost ($500-$5,000 for enterprise NAS)
- Requires network configuration
- More complex deployment

**Implementation**:
- Mount NAS share as network drive
- Use same file-based approach with OS-level immutability
- NAS provides hardware-level WORM enforcement

**Cost Estimate**:
- Enterprise NAS with WORM support: $1,000-$3,000
- Storage: 100 TB for $200-$500
- Redundancy: RAID 6/10 configuration

**Regulatory Compliance**: High confidence - hardware-enforced WORM.

---

### Option 4: Write-Once Optical Media (Legacy/Archival)

**Description**: Blu-ray or DVD archival with WORM properties.

**Pros**:
- True WORM (physically impossible to modify)
- Air-gapped (no network attacks)
- Long-term retention (10-50 years for archival-grade media)
- Low cost per GB

**Cons**:
- Manual intervention required
- Not suitable for real-time logging
- Requires offline archival workflow
- Slow write speed (not suitable for immediate logging)

**Implementation**:
- Daily/weekly export to optical media
- Two-tier approach: real-time logs + offline archive

**Regulatory Compliance**: High confidence - physical WORM, but requires hybrid approach.

---

## 3. Recommended Solution

### 3.1 Primary Recommendation: Tiered WORM Approach

**Development Environment**: Windows Immutable Files (Option 1)
- Simulation of WORM with read-only file attributes
- Sufficient for development and testing
- Zero infrastructure cost

**Production Environment**: Azure Immutable Blob Storage (Option 2)
- True WORM with legal hold support
- Regulatory compliant (SEC 17a-4, FINRA, FDA)
- Automatic retention policy enforcement
- Built-in redundancy and disaster recovery

**Rationale**:
- **Development**: Native Windows support enables rapid development without cloud dependencies
- **Production**: Azure provides true WORM semantics required for medical device compliance
- **Cost**: Minimal Azure cost for audit log volume (~$0.04/month)
- **Compliance**: Meets FDA 21 CFR Part 11, EU MDR, and MFDS requirements for audit log integrity
- **Scalability**: Cloud storage scales without hardware upgrades
- **Disaster Recovery**: Built-in geo-replication protects against site-wide disasters

---

## 4. Development vs Production Differences

### 4.1 Development Environment (Windows Immutable Files)

**Storage Location**:
```
%LocalAppData%\HnVue\AuditLogs\
```

**WORM Implementation**:
```csharp
private void SetFileReadOnly(string filePath)
{
    var fileInfo = new FileInfo(filePath);
    fileInfo.Attributes |= FileAttributes.ReadOnly | FileAttributes.Archive;
}
```

**Configuration** (`appsettings.Development.json`):
```json
{
  "AuditLog": {
    "StorageType": "FileSystem",
    "WormEnabled": true,
    "WormSimulation": true,
    "Directory": "%LocalAppData%\\HnVue\\AuditLogs"
  }
}
```

**Limitations**:
- Files can be modified/deleted by administrators
- Suitable for development/testing only
- Not production-compliant

---

### 4.2 Production Environment (Azure Immutable Blob Storage)

**Storage Location**:
```
Azure Blob Storage Account
Container: hnvue-audit-logs
Blob: audit/{year}/{month}/{day}/{entryId}.audit
```

**WORM Implementation**:
```csharp
private async Task WriteToImmutableBlobAsync(
    AuditEntryRecord entry,
    CancellationToken ct)
{
    var blobName = $"audit/{entry.Timestamp:yyyy/MM/dd}/{entry.EntryId}.audit";
    var blobClient = _containerClient.GetBlobClient(blobName);

    var json = JsonSerializer.Serialize(entry, _jsonOptions);
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

    await blobClient.UploadAsync(
        stream,
        overwrite: false,
        cancellationToken: ct);

    // Set immutability policy for 6 years
    var retentionDate = DateTimeOffset.UtcNow.AddYears(RetentionYears);
    await blobClient.SetImmutabilityPolicyAsync(
        new BlobImmutabilityPolicy(expiresOn: retentionDate),
        legalHold: false,
        cancellationToken: ct);
}
```

**Configuration** (`appsettings.Production.json`):
```json
{
  "AuditLog": {
    "StorageType": "AzureBlob",
    "WormEnabled": true,
    "WormSimulation": false,
    "AzureBlobStorage": {
      "ConnectionString": "AzureStorageConnectionString",
      "ContainerName": "hnvue-audit-logs",
      "RetentionYears": 6
    }
  }
}
```

**Regulatory Compliance**:
- True WORM (files cannot be modified or deleted until retention expires)
- Legal hold support for litigation
- Meets SEC 17a-4, FINRA, CFTC, FDA requirements
- Built-in audit trail for all access attempts

---

## 5. Implementation Phases

### Phase 1: WORM Interface Abstraction (Priority: High)

**Objective**: Define abstraction layer for pluggable WORM storage backends.

**Files to Create**:
1. `src/HnVue.Console/Security/IWormStorageProvider.cs` - Interface definition
2. `src/HnVue.Console/Security/Models/WormEntry.cs` - Data model

**Interface Definition**:
```csharp
public interface IWormStorageProvider
{
    /// <summary>
    /// Writes an audit log entry to WORM storage.
    /// </summary>
    Task WriteEntryAsync(WormEntry entry, CancellationToken ct);

    /// <summary>
    /// Reads an audit log entry from WORM storage.
    /// </summary>
    Task<WormEntry?> ReadEntryAsync(string entryId, CancellationToken ct);

    /// <summary>
    /// Queries audit log entries with filtering.
    /// </summary>
    Task<IReadOnlyList<WormEntry>> QueryEntriesAsync(
        AuditLogFilter filter,
        CancellationToken ct);

    /// <summary>
    /// Verifies integrity of audit log chain.
    /// </summary>
    Task<AuditVerificationResult> VerifyIntegrityAsync(CancellationToken ct);

    /// <summary>
    /// Enforces retention policy (WORM-compliant deletion after retention expires).
    /// </summary>
    Task<int> EnforceRetentionPolicyAsync(CancellationToken ct);
}
```

**Effort**: 4-6 hours

---

### Phase 2: Windows Immutable File Implementation (Priority: High)

**Objective**: Implement WORM simulation for development environment.

**Files to Create**:
1. `src/HnVue.Console/Security/FileSystemWormStorageProvider.cs` - Implementation
2. Update `src/HnVue.Console/Services/Adapters/AuditLogServiceAdapter.cs` - Use new provider

**Implementation Details**:
```csharp
public sealed class FileSystemWormStorageProvider : IWormStorageProvider
{
    private readonly string _auditDirectory;
    private readonly bool _simulateWorm;

    public FileSystemWormStorageProvider(
        IConfiguration configuration,
        ILogger<FileSystemWormStorageProvider> logger)
    {
        _auditDirectory = configuration["AuditLog:Directory"]
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HnVue",
                "AuditLogs");
        _simulateWorm = configuration.GetValue<bool>("AuditLog:WormSimulation");

        Directory.CreateDirectory(_auditDirectory);
    }

    public async Task WriteEntryAsync(WormEntry entry, CancellationToken ct)
    {
        var filePath = GetFilePath(entry.EntryId, entry.Timestamp);

        // Write atomically to temporary file
        var tempPath = filePath + ".tmp";
        var json = JsonSerializer.Serialize(entry);
        await File.WriteAllTextAsync(tempPath, json, ct);

        // Atomic rename for crash safety
        File.Move(tempPath, filePath, overwrite: false);

        // Set read-only attribute (WORM simulation)
        if (_simulateWorm)
        {
            var fileInfo = new FileInfo(filePath);
            fileInfo.Attributes |= FileAttributes.ReadOnly | FileAttributes.Archive;
        }
    }

    // ... other methods
}
```

**Effort**: 6-8 hours

---

### Phase 3: Azure Immutable Blob Implementation (Priority: Medium)

**Objective**: Implement production WORM storage for Azure.

**Files to Create**:
1. `src/HnVue.Console/Security/AzureBlobWormStorageProvider.cs` - Implementation
2. Update `src/HnVue.Console/appsettings.Production.json` - Configuration

**Dependencies**:
- `Azure.Storage.Blobs` (NuGet package)

**Implementation Details**:
```csharp
public sealed class AzureBlobWormStorageProvider : IWormStorageProvider
{
    private readonly BlobContainerClient _containerClient;
    private readonly int _retentionYears;

    public AzureBlobWormStorageProvider(
        IConfiguration configuration,
        ILogger<AzureBlobWormStorageProvider> logger)
    {
        var connectionString = configuration["AuditLog:AzureBlobStorage:ConnectionString"];
        var containerName = configuration["AuditLog:AzureBlobStorage:ContainerName"];

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        _retentionYears = configuration.GetValue<int>(
            "AuditLog:AzureBlobStorage:RetentionYears",
            defaultValue: 6);

        // Ensure container exists
        _containerClient.CreateIfNotExists();
    }

    public async Task WriteEntryAsync(WormEntry entry, CancellationToken ct)
    {
        var blobName = $"audit/{entry.Timestamp:yyyy/MM/dd}/{entry.EntryId}.audit";
        var blobClient = _containerClient.GetBlobClient(blobName);

        var json = JsonSerializer.Serialize(entry);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await blobClient.UploadAsync(stream, overwrite: false, cancellationToken: ct);

        // Set immutability policy for regulatory compliance
        var retentionDate = DateTimeOffset.UtcNow.AddYears(_retentionYears);
        await blobClient.SetImmutabilityPolicyAsync(
            new BlobImmutabilityPolicy(expiresOn: retentionDate),
            legalHold: false,
            cancellationToken: ct);
    }

    // ... other methods
}
```

**Effort**: 8-10 hours

---

### Phase 4: Configuration & Testing (Priority: Medium)

**Objective**: Update configuration and create comprehensive tests.

**Files to Update**:
1. `src/HnVue.Console/appsettings.json` - Default configuration
2. `src/HnVue.Console/appsettings.Development.json` - Development config
3. `src/HnVue.Console/appsettings.Production.json` - Production config
4. `tests/csharp/HnVue.Console.Tests/Security/WormStorageTests.cs` - Tests

**Configuration Strategy**:

**appsettings.json** (Default):
```json
{
  "AuditLog": {
    "StorageType": "FileSystem",
    "WormEnabled": true,
    "WormSimulation": true
  }
}
```

**appsettings.Development.json**:
```json
{
  "AuditLog": {
    "StorageType": "FileSystem",
    "WormEnabled": true,
    "WormSimulation": true,
    "Directory": "%LocalAppData%\\HnVue\\AuditLogs"
  }
}
```

**appsettings.Production.json**:
```json
{
  "AuditLog": {
    "StorageType": "AzureBlob",
    "WormEnabled": true,
    "WormSimulation": false,
    "AzureBlobStorage": {
      "ConnectionString": "AzureStorageConnectionString",
      "ContainerName": "hnvue-audit-logs",
      "RetentionYears": 6
    }
  }
}
```

**Effort**: 4-6 hours

---

### Phase 5: Deployment & Documentation (Priority: Low)

**Objective**: Deploy to production and create operational documentation.

**Deliverables**:
1. Azure Resource Manager (ARM) template or Bicep for infrastructure setup
2. Operational runbook for audit log management
3. Incident response procedures for WORM storage failures
4. Migration guide from file-based to Azure storage

**Effort**: 8-10 hours

---

## 6. Testing Strategy

### 6.1 Unit Tests

**Test Coverage Targets**:
- Line coverage: 90%+ for security modules
- Branch coverage: 85%+ for all WORM providers

**Test Categories**:

1. **Immutability Tests**:
   - Verify files cannot be modified after write
   - Verify files cannot be deleted before retention expires
   - Verify modification attempts are logged

2. **Integrity Tests**:
   - Verify hash chain integrity
   - Verify detection of tampered entries
   - Verify corruption handling

3. **Retention Tests**:
   - Verify retention policy enforcement
   - Verify expired entries are deleted
   - Verify retention period calculation

**Example Test**:
```csharp
[Fact]
public async Task WriteEntry_ShouldSetReadOnlyAttribute()
{
    // Arrange
    var provider = new FileSystemWormStorageProvider(_configuration, _logger);
    var entry = CreateTestEntry();

    // Act
    await provider.WriteEntryAsync(entry, CancellationToken.None);
    var filePath = provider.GetFilePath(entry.EntryId, entry.Timestamp);

    // Assert
    var fileInfo = new FileInfo(filePath);
    Assert.True(fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly));
}
```

---

### 6.2 Integration Tests

**Test Scenarios**:

1. **Azure Blob Integration**:
   - End-to-end write and read from Azure
   - Immutability policy verification
   - Retention enforcement

2. **Failover Tests**:
   - Network interruption handling
   - Retry policy verification
   - Fallback to local storage

3. **Performance Tests**:
   - Write latency < 100ms
   - Read latency < 50ms
   - Concurrent write handling

---

### 6.3 Regulatory Compliance Tests

**Compliance Verification**:

1. **FDA 21 CFR Part 11**:
   - Audit trail integrity
   - Non-repudiation
   - Timestamp accuracy
   - Electronic signatures

2. **IEC 62304 Class B/C**:
   - Security module validation
   - Failure mode handling
   - Risk mitigation verification

3. **MFDS Guidelines**:
   - 6-year retention verification
   - WORM storage validation
   - Export functionality

---

## 7. Migration Path

### 7.1 Current State Migration

**Migration Strategy**: Phased rollout with fallback capability.

**Phase 1: Dual-Write** (1 week)
- Write to both file-based storage and Azure Blob
- Compare entries for consistency
- Monitor for errors and performance issues

**Phase 2: Read-Through** (1 week)
- Read from Azure Blob, fallback to file-based
- Continue writing to both
- Validate all entries are accessible

**Phase 3: Azure-Primary** (1 week)
- Write to Azure Blob only
- Keep file-based as backup
- Monitor for failures

**Phase 4: Archive Legacy** (1 day)
- Export all legacy file-based logs to Azure
- Verify hash chain continuity
- Remove local files (after backup)

---

### 7.2 Rollback Plan

**Rollback Triggers**:
- Azure storage unavailability for > 1 hour
- Data corruption detected
- Performance degradation > 50%
- Regulatory compliance concerns

**Rollback Procedure**:
1. Switch configuration to file-based storage
2. Verify all entries are accessible
3. Reconcile any missing entries from Azure
4. Investigate root cause before retrying migration

---

## 8. Cost Analysis

### 8.1 Development Environment

**Cost**:
- Windows Immutable Files: $0 (native OS feature)
- Development time: 18-24 hours (across Phase 1-4)

**Total**: $0 (infrastructure) + engineering time

---

### 8.2 Production Environment (Azure)

**Infrastructure Costs**:
- Azure Blob Storage (Cool tier): $0.002 per GB/month
- Estimated annual storage: 3.6 GB (10,000 entries/day × 1KB × 365)
- Monthly cost: $0.002 × 21.6 GB (6-year retention) = **$0.04/month**
- Annual cost: **$0.48/year**

**Data Transfer Costs**:
- Ingress (write): Free
- Egress (read): $0.087 per GB (rare - only for exports)
- Estimated monthly read: < 1 GB (audit log viewer)
- Monthly egress cost: **$0.09**

**Total Monthly Cost**: **~$0.13/month** ($1.56/year)

**One-Time Setup**:
- Engineering time: 8-10 hours (Phase 3 + Phase 5)
- Azure resource setup: 2 hours

**Comparison with Alternatives**:
- Enterprise NAS: $1,000-$3,000 (hardware) + ongoing maintenance
- On-premises server: $2,000-$5,000 (hardware) + licensing
- Optical media archival: $500-$1,000 (drives) + manual labor

---

## 9. Risk Mitigation

### 9.1 Identified Risks

| Risk ID | Description | Probability | Impact | Mitigation |
|---------|-------------|-------------|--------|------------|
| W-01 | Azure service unavailability | Low | High | Dual-write during migration, fallback to local storage |
| W-02 | Data corruption during migration | Low | Critical | Hash chain verification, backup before migration |
| W-03 | Cost overruns | Very Low | Low | Monitor storage usage, set up Azure budget alerts |
| W-04 | Regulatory non-compliance | Very Low | Critical | Use Azure Immutable Blob Storage with legal hold support |
| W-05 | Performance degradation | Low | Medium | Implement caching, use async operations, monitor latency |

---

### 9.2 Monitoring & Alerting

**Key Metrics to Monitor**:

1. **Storage Metrics**:
   - Daily blob count
   - Total storage consumption
   - Write latency (P50, P95, P99)
   - Read latency (P50, P95, P99)

2. **Integrity Metrics**:
   - Hash chain verification success rate
   - Corruption detection count
   - Tampering attempt count

3. **Compliance Metrics**:
   - Retention policy enforcement
   - Legal hold status
   - Audit trail completeness

**Alerting Thresholds**:
- Write latency > 500ms for 5 consecutive minutes
- Read failure rate > 1% for 5 minutes
- Verification failure (any occurrence)
- Storage usage > 80% of quota

---

## 10. Success Criteria

### 10.1 Functional Requirements

- [ ] FR-SEC-06.1: Audit logs written to WORM storage after creation
- [ ] FR-SEC-06.2: Audit logs cannot be modified after write (until retention expires)
- [ ] FR-SEC-06.3: Audit logs cannot be deleted before retention expires
- [ ] FR-SEC-06.4: SHA-256 hash chain integrity is maintained
- [ ] FR-SEC-07: 6-year retention policy is enforced

### 10.2 Non-Functional Requirements

- [ ] NFR-SEC-01: Audit log write latency < 100ms (P95)
- [ ] NFR-SEC-02: Audit log service availability 99.99%
- [ ] NFR-SEC-04: Security module test coverage 90%+

### 10.3 Regulatory Compliance

- [ ] FDA 21 CFR Part 11: Electronic records compliance
- [ ] IEC 62304 Class B/C: Safety classification compliance
- [ ] MFDS: 6-year audit log retention
- [ ] EU MDR MDCG 2019-16: Audit trail integrity

---

## 11. Recommendations

### 11.1 Immediate Actions (This Week)

1. **Review and approve this plan** with stakeholders
2. **Create Azure storage account** for testing
3. **Implement Phase 1** (WORM interface abstraction)
4. **Estimate engineering effort** for remaining phases

### 11.2 Short-Term Actions (Next 2 Weeks)

1. **Implement Phase 2** (Windows Immutable Files)
2. **Implement Phase 3** (Azure Immutable Blob)
3. **Create comprehensive unit tests** (Phase 4)
4. **Perform integration testing** with Azure

### 11.3 Medium-Term Actions (Next Month)

1. **Execute migration** (Phase 5)
2. **Create operational documentation**
3. **Train operations team** on WORM storage management
4. **Update SPEC-SECURITY-001** acceptance criteria

### 11.4 Long-Term Actions (Next Quarter)

1. **Monitor production metrics** and optimize performance
2. **Review regulatory compliance** with legal/audit team
3. **Plan disaster recovery testing** (failover to backup region)
4. **Evaluate additional security features** (legal hold, compliance reports)

---

## 12. Open Questions

1. **Legal hold requirements**: Does the organization need indefinite legal hold support for litigation? (Affects Azure configuration)
2. **Data residency requirements**: Are there geographic restrictions on audit log storage? (Affects Azure region selection)
3. **Backup frequency**: How frequently should audit logs be backed up for disaster recovery?
4. **Archive format**: Should archived logs be exported to a specific format (CSV, JSON, DICOM)?
5. **Access control**: Who should have access to audit logs for compliance reporting?

---

## Appendix A: Azure Resource Configuration

### A.1 Azure Blob Storage Setup

**PowerShell Script**:
```powershell
# Variables
$resourceGroupName = "hnvue-production"
$storageAccountName = "hnvueauditlogs"
$containerName = "hnvue-audit-logs"
$location = "eastus"

# Create resource group
New-AzResourceGroup -Name $resourceGroupName -Location $location

# Create storage account (Standard V2, Cool tier)
$storageAccount = New-AzStorageAccount `
    -ResourceGroupName $resourceGroupName `
    -Name $storageAccountName `
    -Location $location `
    -SkuName Standard_RAGRS `
    -Kind StorageV2 `
    -AccessTier Cool

# Get storage account context
$ctx = $storageAccount.Context

# Create container
New-AzStorageContainer `
    -Name $containerName `
    -Context $ctx `
    -Permission Off

# Enable immutable storage with versioning
Update-AzStorageBlobServiceProperty `
    -ResourceGroupName $resourceGroupName `
    -StorageAccountName $storageAccountName `
    -EnableImmutableStorageWithVersioning $true
```

---

## Appendix B: Troubleshooting Guide

### B.1 Common Issues

**Issue**: "Access denied" when writing to Azure Blob

**Cause**: Managed identity not configured, connection string incorrect

**Solution**:
1. Verify `AzureStorageConnectionString` configuration
2. Check managed identity permissions on storage account
3. Review Azure RBAC assignments

**Issue**: Write latency exceeds 100ms threshold

**Cause**: Network latency, Azure region selection, throttling

**Solution**:
1. Select Azure region closest to application
2. Implement retry policy with exponential backoff
3. Monitor Azure Storage metrics for throttling

**Issue**: Immutability policy cannot be set

**Cause**: Immutable storage not enabled on storage account

**Solution**:
1. Enable immutable storage with versioning on storage account
2. Ensure blob versioning is enabled
3. Verify account tier supports immutability (Standard V2 required)

---

**Document Version**: 1.0.0
**Last Updated**: 2026-03-13
**Author**: MoAI DevOps Team
**Status**: Ready for Review
