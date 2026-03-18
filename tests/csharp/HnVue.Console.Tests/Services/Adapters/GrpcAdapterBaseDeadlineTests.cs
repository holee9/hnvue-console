using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.Services.Adapters;

/// <summary>
/// Tests for GrpcAdapterBase deadline support.
/// SPEC-IPC-002: REQ-INFRA-001/002 - All gRPC calls must have deadlines.
/// </summary>
public class GrpcAdapterBaseDeadlineTests
{
    private readonly IConfiguration _configuration;

    public GrpcAdapterBaseDeadlineTests()
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
    }

    [Fact]
    public void CommandDeadline_Is5Seconds()
    {
        // SPEC-IPC-002: REQ-INFRA-001 - Command RPCs have 5s deadline
        var mockLogger = new Mock<ILogger<TestableGrpcAdapter>>();
        using var adapter = new TestableGrpcAdapter(_configuration, mockLogger.Object);

        Assert.Equal(TimeSpan.FromSeconds(5), adapter.ExposedCommandDeadline);
    }

    [Fact]
    public void ImageStreamDeadline_Is30Seconds()
    {
        // SPEC-IPC-002: REQ-INFRA-002 - Streaming RPCs have 30s deadline
        var mockLogger = new Mock<ILogger<TestableGrpcAdapter>>();
        using var adapter = new TestableGrpcAdapter(_configuration, mockLogger.Object);

        Assert.Equal(TimeSpan.FromSeconds(30), adapter.ExposedImageStreamDeadline);
    }

    [Fact]
    public void CreateCallOptions_WithCommandDeadline_SetsDeadlineInFuture()
    {
        // SPEC-IPC-002: REQ-INFRA-001 - CreateCallOptions must set a future deadline
        var mockLogger = new Mock<ILogger<TestableGrpcAdapter>>();
        using var adapter = new TestableGrpcAdapter(_configuration, mockLogger.Object);

        var before = DateTime.UtcNow;
        var options = adapter.ExposedCreateCallOptions(TimeSpan.FromSeconds(5));
        var after = DateTime.UtcNow;

        Assert.True(options.Deadline.HasValue, "CallOptions must have a deadline set");
        Assert.True(options.Deadline!.Value > before, "Deadline must be in the future");
        Assert.True(options.Deadline!.Value <= after.Add(TimeSpan.FromSeconds(5)).AddMilliseconds(100),
            "Deadline must be within 5 seconds from now");
    }

    [Fact]
    public void CreateCallOptions_WithImageStreamDeadline_SetsCorrectDeadline()
    {
        // SPEC-IPC-002: REQ-INFRA-002 - Image stream deadline is 30 seconds
        var mockLogger = new Mock<ILogger<TestableGrpcAdapter>>();
        using var adapter = new TestableGrpcAdapter(_configuration, mockLogger.Object);

        var before = DateTime.UtcNow;
        var options = adapter.ExposedCreateCallOptions(TimeSpan.FromSeconds(30));

        Assert.True(options.Deadline.HasValue);
        // Deadline should be approximately 30 seconds from now
        var expectedDeadline = before.Add(TimeSpan.FromSeconds(30));
        Assert.True(options.Deadline!.Value >= expectedDeadline.AddMilliseconds(-100));
        Assert.True(options.Deadline!.Value <= expectedDeadline.AddMilliseconds(500));
    }
}

/// <summary>
/// Concrete test double for GrpcAdapterBase to expose protected members.
/// </summary>
public sealed class TestableGrpcAdapter : HnVue.Console.Services.Adapters.GrpcAdapterBase
{
    public TestableGrpcAdapter(IConfiguration configuration, ILogger<TestableGrpcAdapter> logger)
        : base(configuration, logger) { }

    public TimeSpan ExposedCommandDeadline => CommandDeadline;
    public TimeSpan ExposedImageStreamDeadline => ImageStreamDeadline;
    public CallOptions ExposedCreateCallOptions(TimeSpan deadline) => CreateCallOptions(deadline);
}
