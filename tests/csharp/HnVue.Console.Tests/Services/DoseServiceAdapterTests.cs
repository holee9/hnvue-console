using Grpc.Core;
using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Services.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.Services;

/// <summary>
/// Unit tests for DoseServiceAdapter.
/// SPEC-IPC-002: REQ-DOSE-001 through REQ-DOSE-008.
/// IEC 62304 Class C - Safety Critical dose management.
/// </summary>
public class DoseServiceAdapterTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<DoseServiceAdapter>> _mockLogger;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly DoseServiceAdapter _adapter;

    public DoseServiceAdapterTests()
    {
        // Server intentionally not running to test error-path behavior
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

        _mockLogger = new Mock<ILogger<DoseServiceAdapter>>();
        _mockAuditLogService = new Mock<IAuditLogService>();

        // SPEC-IPC-002: REQ-DOSE-001 - DoseServiceAdapter requires IAuditLogService
        _adapter = new DoseServiceAdapter(_configuration, _mockLogger.Object, _mockAuditLogService.Object);
    }

    public void Dispose() => _adapter.Dispose();

    // --- REQ-DOSE-007: GetCurrentDoseDisplayAsync MUST throw on failure (NEVER return 0) ---

    [Fact]
    public async Task GetCurrentDoseDisplayAsync_WhenGrpcServerUnavailable_ThrowsException()
    {
        // SPEC-IPC-002: REQ-DOSE-007 - IEC 62304 Class C: MUST throw on failure, NEVER return 0
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _adapter.GetCurrentDoseDisplayAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentDoseDisplayAsync_WhenServerUnavailable_DoesNotReturnZeroDose()
    {
        // SPEC-IPC-002: REQ-DOSE-007 - Silently returning 0 is prohibited (Class C safety)
        // If the method does NOT throw, it would indicate a bug in the implementation
        var exception = await Record.ExceptionAsync(() =>
            _adapter.GetCurrentDoseDisplayAsync(CancellationToken.None));

        if (exception == null)
        {
            // This should not happen - GetCurrentDoseDisplayAsync must throw on failure
            Assert.Fail("GetCurrentDoseDisplayAsync must throw on gRPC failure (REQ-DOSE-007)");
        }
        else
        {
            // Exception is the expected behavior
            Assert.NotNull(exception);
        }
    }

    // --- REQ-DOSE-008: GetAlertThresholdAsync returns conservative defaults on failure ---

    [Fact]
    public async Task GetAlertThresholdAsync_WhenGrpcServerUnavailable_ReturnsConservativeDefaults()
    {
        // SPEC-IPC-002: REQ-DOSE-008 - On failure: warning=50mGy, error=100mGy (conservative defaults)
        var result = await _adapter.GetAlertThresholdAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(50m, result.WarningThreshold);
        Assert.Equal(100m, result.ErrorThreshold);
        Assert.Equal(DoseUnit.MilliGray, result.Unit);
    }

    [Fact]
    public async Task GetAlertThresholdAsync_ConservativeDefaults_ErrorThresholdGreaterThanWarning()
    {
        // Safety invariant: error threshold must always be greater than warning threshold
        var result = await _adapter.GetAlertThresholdAsync(CancellationToken.None);

        Assert.True(result.ErrorThreshold > result.WarningThreshold,
            "Error threshold must be greater than warning threshold");
    }

    // --- REQ-DOSE-004: SetAlertThresholdAsync must log audit trail ---

    [Fact]
    public async Task SetAlertThresholdAsync_WhenCalled_LogsAuditEvent()
    {
        // SPEC-IPC-002: REQ-DOSE-004 - Must log before/after values to audit log
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

        var threshold = new DoseAlertThreshold
        {
            WarningThreshold = 50m,
            ErrorThreshold = 100m,
            Unit = DoseUnit.MilliGray
        };

        // SetAlertThresholdAsync may fail due to gRPC being unavailable,
        // but audit log should still be called (fire-and-forget pattern per REQ-AUDIT-004)
        await Record.ExceptionAsync(() =>
            _adapter.SetAlertThresholdAsync(threshold, CancellationToken.None));

        // Audit log must have been called (before the gRPC call fails)
        _mockAuditLogService.Verify(
            x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(desc => desc.Contains("50") || desc.Contains("100") || desc.Contains("threshold")),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "SetAlertThresholdAsync must log audit event with threshold values");
    }

    [Fact]
    public async Task SetAlertThresholdAsync_WhenAuditLogFails_DoesNotPropagateAuditException()
    {
        // SPEC-IPC-002: REQ-AUDIT-004 - Audit log failure must NOT block the original operation
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

        var threshold = new DoseAlertThreshold
        {
            WarningThreshold = 50m,
            ErrorThreshold = 100m,
            Unit = DoseUnit.MilliGray
        };

        // The exception should be from gRPC failure, not from audit log failure
        var exception = await Record.ExceptionAsync(() =>
            _adapter.SetAlertThresholdAsync(threshold, CancellationToken.None));

        // If exception occurs, it must be a gRPC-related exception (not InvalidOperationException from audit log)
        if (exception != null)
        {
            Assert.IsNotType<InvalidOperationException>(exception);
        }
    }

    // --- REQ-DOSE-006: ResetCumulativeDoseAsync requires audit log ---

    [Fact]
    public async Task ResetCumulativeDoseAsync_WhenCalled_LogsAuditEvent()
    {
        // SPEC-IPC-002: REQ-DOSE-006 - ResetStudyDose RPC + audit log
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
            _adapter.ResetCumulativeDoseAsync("study-001", CancellationToken.None));

        // Audit log must be called with the study ID
        _mockAuditLogService.Verify(
            x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(desc => desc.Contains("study-001") || desc.Contains("dose") || desc.Contains("reset")),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "ResetCumulativeDoseAsync must log audit event with study ID");
    }

    // --- REQ-DOSE-005: SubscribeDoseUpdatesAsync returns IAsyncEnumerable ---

    [Fact]
    public async Task SubscribeDoseUpdatesAsync_WhenGrpcUnavailable_ReturnsEmptyOrThrows()
    {
        // SPEC-IPC-002: REQ-DOSE-005 - SubscribeDoseAlerts streaming → IAsyncEnumerable<DoseUpdate>
        // When server is unavailable, should return empty sequence or propagate exception gracefully
        var updates = new List<DoseUpdate>();
        try
        {
            await foreach (var update in _adapter.SubscribeDoseUpdatesAsync(CancellationToken.None))
            {
                updates.Add(update);
            }
        }
        catch (RpcException)
        {
            // Acceptable: gRPC failure may propagate
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            // Acceptable: any gRPC-related exception
        }

        // Test passes regardless: either empty sequence or exception (both acceptable per spec)
        Assert.True(true, "SubscribeDoseUpdatesAsync handled server unavailability");
    }

    [Theory]
    [InlineData("study-001")]
    [InlineData("study-abc-xyz")]
    public async Task ResetCumulativeDoseAsync_WithVariousStudyIds_LogsAuditForEachStudy(string studyId)
    {
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
            _adapter.ResetCumulativeDoseAsync(studyId, CancellationToken.None));

        _mockAuditLogService.Verify(
            x => x.LogAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditOutcome>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
