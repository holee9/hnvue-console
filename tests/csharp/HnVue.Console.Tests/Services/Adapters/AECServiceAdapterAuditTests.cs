using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Services.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.Services.Adapters;

/// <summary>
/// Tests for AECServiceAdapter audit logging.
/// SPEC-IPC-002: REQ-AUDIT-003 - EnableAECAsync/DisableAECAsync must log audit events.
/// SPEC-IPC-002: REQ-AUDIT-004 - Audit log failure must NOT block operation.
/// </summary>
public class AECServiceAdapterAuditTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<AECServiceAdapter>> _mockLogger;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly AECServiceAdapter _adapter;

    public AECServiceAdapterAuditTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcServer:Address"] = "http://localhost:50051",
                ["GrpcSecurity:EnableTls"] = "false",
                ["GrpcSecurity:EnableMutualTls"] = "false",
                ["GrpcSecurity:CertificateRotationDays"] = "90",
                ["GrpcSecurity:CertificateExpirationWarningDays"] = "30",
            })
            .Build();

        _mockLogger = new Mock<ILogger<AECServiceAdapter>>();
        _mockAuditLogService = new Mock<IAuditLogService>();

        // SPEC-IPC-002: REQ-AUDIT-003 - AECServiceAdapter requires IAuditLogService
        _adapter = new AECServiceAdapter(_configuration, _mockLogger.Object, _mockAuditLogService.Object);
    }

    public void Dispose() => _adapter.Dispose();

    // --- REQ-AUDIT-003: EnableAECAsync must log audit event ---

    [Fact]
    public async Task EnableAECAsync_WhenCalled_LogsAuditEvent()
    {
        // SPEC-IPC-002: REQ-AUDIT-003 - Action type and timestamp must be logged
        _mockAuditLogService
            .Setup(x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("audit-entry-id");

        // Server is not running, so gRPC will fail - but audit log should still be called
        await Record.ExceptionAsync(() =>
            _adapter.EnableAECAsync(CancellationToken.None));

        _mockAuditLogService.Verify(
            x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(desc =>
                    desc.ToLower().Contains("enable") || desc.ToLower().Contains("aec") || desc.ToLower().Contains("enabled")),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "EnableAECAsync must log an audit event with 'enable' or 'aec' in description");
    }

    // --- REQ-AUDIT-003: DisableAECAsync must log audit event ---

    [Fact]
    public async Task DisableAECAsync_WhenCalled_LogsAuditEvent()
    {
        // SPEC-IPC-002: REQ-AUDIT-003 - Action type and timestamp must be logged
        _mockAuditLogService
            .Setup(x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("audit-entry-id");

        await Record.ExceptionAsync(() =>
            _adapter.DisableAECAsync(CancellationToken.None));

        _mockAuditLogService.Verify(
            x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(desc =>
                    desc.ToLower().Contains("disable") || desc.ToLower().Contains("aec") || desc.ToLower().Contains("disabled")),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "DisableAECAsync must log an audit event with 'disable' or 'aec' in description");
    }

    // --- REQ-AUDIT-004: Audit log failure must NOT block operation ---

    [Fact]
    public async Task EnableAECAsync_WhenAuditLogFails_DoesNotPropagateAuditException()
    {
        // SPEC-IPC-002: REQ-AUDIT-004 - Audit log failure must not block EnableAECAsync
        _mockAuditLogService
            .Setup(x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Audit log service unavailable"));

        var exception = await Record.ExceptionAsync(() =>
            _adapter.EnableAECAsync(CancellationToken.None));

        // If exception occurs, it must NOT be the InvalidOperationException from audit log
        if (exception != null)
        {
            Assert.IsNotType<InvalidOperationException>(exception);
        }
    }

    [Fact]
    public async Task DisableAECAsync_WhenAuditLogFails_DoesNotPropagateAuditException()
    {
        // SPEC-IPC-002: REQ-AUDIT-004 - Audit log failure must not block DisableAECAsync
        _mockAuditLogService
            .Setup(x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Audit log service unavailable"));

        var exception = await Record.ExceptionAsync(() =>
            _adapter.DisableAECAsync(CancellationToken.None));

        if (exception != null)
        {
            Assert.IsNotType<InvalidOperationException>(exception);
        }
    }
}
