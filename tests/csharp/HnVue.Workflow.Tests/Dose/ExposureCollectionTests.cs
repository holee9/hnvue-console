using FluentAssertions;
using HnVue.Workflow.Dose;
using HnVue.Workflow.Protocol;
using HnVue.Workflow.Study;
using Xunit;
using ProtocolType = HnVue.Workflow.Protocol.Protocol;

namespace HnVue.Workflow.Tests.Dose;

/// <summary>
/// Unit tests for ExposureCollection dose limit checking.
/// SPEC-WORKFLOW-001: Multi-exposure dose tracking tests.
/// </summary>
public class ExposureCollectionTests
{
    private readonly DoseLimitConfiguration _configuration;

    public ExposureCollectionTests()
    {
        _configuration = new DoseLimitConfiguration
        {
            StudyDoseLimit = 1000m,
            DailyDoseLimit = 5000m,
            WarningThresholdPercent = 0.8m
        };
    }

    [Fact]
    public void GetCumulativeDose_EmptyCollection_ReturnsZeroDose()
    {
        // Arrange
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", _configuration);

        // Act
        var result = sut.GetCumulativeDose();

        // Assert
        result.TotalDap.Should().Be(0m);
        result.ExposureCount.Should().Be(0);
        result.AcceptedCount.Should().Be(0);
        result.IsWithinLimits.Should().BeTrue();
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void GetCumulativeDose_SingleExposureWithinLimit_ReturnsIsWithinLimitsTrue()
    {
        // Arrange
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", _configuration);
        sut.AddExposure(CreateExposure(500m));

        // Act
        var result = sut.GetCumulativeDose();

        // Assert
        result.TotalDap.Should().Be(500m);
        result.ExposureCount.Should().Be(1);
        result.AcceptedCount.Should().Be(1);
        result.IsWithinLimits.Should().BeTrue();
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void GetCumulativeDose_SingleExposureExceedsLimit_ReturnsIsWithinLimitsFalse()
    {
        // Arrange
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", _configuration);
        sut.AddExposure(CreateExposure(1500m));

        // Act
        var result = sut.GetCumulativeDose();

        // Assert
        result.TotalDap.Should().Be(1500m);
        result.ExposureCount.Should().Be(1);
        result.IsWithinLimits.Should().BeFalse();
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void GetCumulativeDose_MultipleExposures_CumulativeDoseAccumulates()
    {
        // Arrange
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", _configuration);
        sut.AddExposure(CreateExposure(300m));
        sut.AddExposure(CreateExposure(400m));

        // Act
        var result = sut.GetCumulativeDose();

        // Assert
        result.TotalDap.Should().Be(700m);
        result.ExposureCount.Should().Be(2);
        result.AcceptedCount.Should().Be(2);
        result.IsWithinLimits.Should().BeTrue();
    }

    [Fact]
    public void GetCumulativeDose_MultipleExposuresExceedLimit_ReturnsIsWithinLimitsFalse()
    {
        // Arrange
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", _configuration);
        sut.AddExposure(CreateExposure(600m));
        sut.AddExposure(CreateExposure(500m));  // Total 1100m - exceeds limit

        // Act
        var result = sut.GetCumulativeDose();

        // Assert
        result.TotalDap.Should().Be(1100m);
        result.ExposureCount.Should().Be(2);
        result.IsWithinLimits.Should().BeFalse();
    }

    [Fact]
    public void GetCumulativeDose_AtBoundary_ExactlyAtLimit_ReturnsIsWithinLimitsTrue()
    {
        // Arrange
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", _configuration);
        sut.AddExposure(CreateExposure(1000m));

        // Act
        var result = sut.GetCumulativeDose();

        // Assert
        result.TotalDap.Should().Be(1000m);
        result.IsWithinLimits.Should().BeTrue();  // At limit is within limits
    }

    [Fact]
    public void GetCumulativeDose_WithNoLimit_IsWithinLimitsAlwaysTrue()
    {
        // Arrange
        var noLimitConfig = new DoseLimitConfiguration
        {
            StudyDoseLimit = null,
            DailyDoseLimit = null,
            WarningThresholdPercent = 0.8m
        };
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", noLimitConfig);
        sut.AddExposure(CreateExposure(10000m));  // Very high dose

        // Act
        var result = sut.GetCumulativeDose();

        // Assert
        result.TotalDap.Should().Be(10000m);
        result.IsWithinLimits.Should().BeTrue();
        result.DoseLimit.Should().BeNull();
    }

    [Fact]
    public void GetCumulativeDose_OnlyCountsAcceptedExposure_StatusAccepted()
    {
        // Arrange
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", _configuration);
        var acceptedExposure = CreateExposure(300m);
        acceptedExposure.Status = ExposureStatus.Accepted;
        var rejectedExposure = CreateExposure(200m);
        rejectedExposure.Status = ExposureStatus.Rejected;

        sut.AddExposure(acceptedExposure);
        sut.AddExposure(rejectedExposure);

        // Act
        var result = sut.GetCumulativeDose();

        // Assert
        result.TotalDap.Should().Be(500m);  // Both contribute to dose
        result.ExposureCount.Should().Be(2);
        result.AcceptedCount.Should().Be(1);  // Only one accepted
    }

    [Fact]
    public async Task AddExposure_ThreadSafe_MultipleThreads()
    {
        // Arrange
        var sut = new ExposureCollection("STUDY-001", "PATIENT-001", _configuration);
        var tasks = new List<Task>();
        const int threadCount = 100;

        // Act - Add exposures from multiple threads
        for (int i = 0; i < threadCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => sut.AddExposure(CreateExposure(10m))));
        }

        await Task.WhenAll(tasks);

        // Assert
        var result = sut.GetCumulativeDose();
        result.ExposureCount.Should().Be(threadCount);
        result.TotalDap.Should().Be(threadCount * 10m);
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new ExposureCollection("STUDY-001", "PATIENT-001", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullStudyId_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new ExposureCollection(null!, "PATIENT-001", _configuration);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPatientId_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new ExposureCollection("STUDY-001", null!, _configuration);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ExposureRecord CreateExposure(decimal dap)
    {
        return new ExposureRecord
        {
            ExposureIndex = 1,
            Protocol = CreateTestProtocol(),
            Status = ExposureStatus.Accepted,
            AdministeredDap = dap,
            AcquiredAt = DateTime.Now,
            OperatorId = "TEST_OPERATOR"
        };
    }

    private static ProtocolType CreateTestProtocol()
    {
        return new ProtocolType
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "AP",
            Kv = 120m,
            Ma = 100m,
            ExposureTimeMs = 100
        };
    }
}
