using FluentAssertions;
using HnVue.Workflow.Dose;
using HnVue.Workflow.Protocol;
using HnVue.Workflow.Study;
using Xunit;
using ProtocolType = HnVue.Workflow.Protocol.Protocol;

namespace HnVue.Workflow.Tests.Dose;

/// <summary>
/// Unit tests for DoseTrackingCoordinator dose limit checking.
/// SPEC-WORKFLOW-001: Dose limit enforcement tests.
/// </summary>
public class DoseTrackingCoordinatorTests
{
    private readonly DoseLimitConfiguration _configuration;

    public DoseTrackingCoordinatorTests()
    {
        _configuration = new DoseLimitConfiguration
        {
            StudyDoseLimit = 1000m,  // 1000 cGycm²
            DailyDoseLimit = 5000m,  // 5000 cGycm²
            WarningThresholdPercent = 0.8m  // 80%
        };
    }

    [Fact]
    public void RecordDose_WithNoExposure_ReturnsZeroDose()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var exposure = CreateExposure(0m);  // No dose

        // Act
        var result = sut.RecordDose(studyId, patientId, exposure);

        // Assert
        result.TotalDap.Should().Be(0m);
        result.ExposureCount.Should().Be(1);
        result.IsWithinLimits.Should().BeTrue();
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void RecordDose_WithinLimit_ReturnsIsWithinLimitsTrue()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var exposure = CreateExposure(500m);  // 500 cGycm² - within limit

        // Act
        var result = sut.RecordDose(studyId, patientId, exposure);

        // Assert
        result.TotalDap.Should().Be(500m);
        result.ExposureCount.Should().Be(1);
        result.IsWithinLimits.Should().BeTrue();
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void RecordDose_ExceedsLimit_ReturnsIsWithinLimitsFalse()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var exposure = CreateExposure(1500m);  // 1500 cGycm² - exceeds limit

        // Act
        var result = sut.RecordDose(studyId, patientId, exposure);

        // Assert
        result.TotalDap.Should().Be(1500m);
        result.ExposureCount.Should().Be(1);
        result.IsWithinLimits.Should().BeFalse();
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void RecordDose_AtBoundary_ExactlyAtLimit_ReturnsIsWithinLimitsTrue()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var exposure = CreateExposure(1000m);  // Exactly at limit

        // Act
        var result = sut.RecordDose(studyId, patientId, exposure);

        // Assert
        result.TotalDap.Should().Be(1000m);
        result.IsWithinLimits.Should().BeTrue();  // At limit is considered within limits
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void RecordDose_MultipleExposures_CumulativeDoseAccumulates()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";

        // Act - First exposure
        var result1 = sut.RecordDose(studyId, patientId, CreateExposure(300m));
        // Second exposure
        var result2 = sut.RecordDose(studyId, patientId, CreateExposure(400m));
        // Third exposure - exceeds limit
        var result3 = sut.RecordDose(studyId, patientId, CreateExposure(500m));

        // Assert
        result1.TotalDap.Should().Be(300m);
        result1.IsWithinLimits.Should().BeTrue();

        result2.TotalDap.Should().Be(700m);
        result2.IsWithinLimits.Should().BeTrue();

        result3.TotalDap.Should().Be(1200m);
        result3.IsWithinLimits.Should().BeFalse();
    }

    [Fact]
    public void RecordDose_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);

        // Act & Assert - Configuration is passed in constructor, not RecordDose
        // Test that null configuration throws in constructor
        var act = () => new DoseTrackingCoordinator(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordDose_WithNoStudyLimit_AlwaysReturnsIsWithinLimitsTrue()
    {
        // Arrange
        var noLimitConfig = new DoseLimitConfiguration
        {
            StudyDoseLimit = null,  // No limit
            DailyDoseLimit = null,
            WarningThresholdPercent = 0.8m
        };
        var sut = new DoseTrackingCoordinator(noLimitConfig);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var exposure = CreateExposure(10000m);  // Very high dose

        // Act
        var result = sut.RecordDose(studyId, patientId, exposure);

        // Assert
        result.TotalDap.Should().Be(10000m);
        result.IsWithinLimits.Should().BeTrue();  // No limit means always within limits
        result.DoseLimit.Should().BeNull();
    }

    [Fact]
    public void CheckDoseLimits_WithinLimits_ReturnsTrue()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var patientId = "PATIENT-001";
        var proposedDap = 500m;

        // Act
        var result = sut.CheckDoseLimits(patientId, proposedDap);

        // Assert
        result.CurrentCumulativeDose.Should().Be(0m);
        result.ProposedDose.Should().Be(500m);
        result.ProjectedCumulativeDose.Should().Be(500m);
        result.WithinStudyLimit.Should().BeTrue();
        result.WithinDailyLimit.Should().BeTrue();
        result.IsWithinLimits.Should().BeTrue();
    }

    [Fact]
    public void CheckDoseLimits_ExceedsStudyLimit_ReturnsFalse()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var patientId = "PATIENT-001";
        var proposedDap = 1500m;  // Exceeds study limit of 1000

        // Act
        var result = sut.CheckDoseLimits(patientId, proposedDap);

        // Assert
        result.ProjectedCumulativeDose.Should().Be(1500m);
        result.WithinStudyLimit.Should().BeFalse();
        result.WithinDailyLimit.Should().BeTrue();  // Still within daily limit
        result.IsWithinLimits.Should().BeFalse();
    }

    [Fact]
    public void GetCumulativeDose_ForNonExistentStudy_ReturnsNull()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);

        // Act
        var result = sut.GetCumulativeDose("NON-EXISTENT");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CheckDoseLimits_BelowWarningThreshold_SetsShouldWarnFalse()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var patientId = "PATIENT-001";
        var proposedDap = 700m;  // Below 80% warning threshold (800m)

        // Act
        var result = sut.CheckDoseLimits(patientId, proposedDap);

        // Assert
        result.ProjectedCumulativeDose.Should().Be(700m);
        result.IsWithinLimits.Should().BeTrue();
        result.ShouldWarn.Should().BeFalse();
    }

    [Fact]
    public void CheckDoseLimits_AboveWarningThreshold_BelowLimit_SetsShouldWarnTrue()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var patientId = "PATIENT-001";
        var proposedDap = 850m;  // Above 80% (800m) but below limit (1000m)

        // Act
        var result = sut.CheckDoseLimits(patientId, proposedDap);

        // Assert
        result.ProjectedCumulativeDose.Should().Be(850m);
        result.IsWithinLimits.Should().BeTrue();
        result.ShouldWarn.Should().BeTrue();
    }

    [Fact]
    public void CheckDoseLimits_AtExactWarningThreshold_SetsShouldWarnFalse()
    {
        // Arrange
        var sut = new DoseTrackingCoordinator(_configuration);
        var patientId = "PATIENT-001";
        var proposedDap = 800m;  // Exactly at 80% warning threshold

        // Act
        var result = sut.CheckDoseLimits(patientId, proposedDap);

        // Assert
        result.ProjectedCumulativeDose.Should().Be(800m);
        result.IsWithinLimits.Should().BeTrue();
        result.ShouldWarn.Should().BeFalse();  // At threshold is not a warning
    }

    [Fact]
    public void CheckDoseLimits_ExceedingDailyLimit_ReturnsIsWithinLimitsFalse()
    {
        // Arrange
        var config = new DoseLimitConfiguration
        {
            StudyDoseLimit = 1000m,
            DailyDoseLimit = 3000m,  // Lower daily limit
            WarningThresholdPercent = 0.8m
        };
        var sut = new DoseTrackingCoordinator(config);
        var patientId = "PATIENT-001";
        var proposedDap = 3500m;  // Exceeds daily limit

        // Act
        var result = sut.CheckDoseLimits(patientId, proposedDap);

        // Assert
        result.WithinStudyLimit.Should().BeFalse();  // Also exceeds study
        result.WithinDailyLimit.Should().BeFalse();
        result.IsWithinLimits.Should().BeFalse();
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
