using FluentAssertions;
using HnVue.Workflow.Dose;
using HnVue.Workflow.Protocol;
using HnVue.Workflow.Study;
using Xunit;
using ProtocolType = HnVue.Workflow.Protocol.Protocol;

namespace HnVue.Workflow.Tests.Dose;

/// <summary>
/// Unit tests for MultiExposureCoordinator dose limit checking.
/// SPEC-WORKFLOW-001: Multi-view study dose tracking tests.
/// </summary>
public class MultiExposureCoordinatorTests
{
    private readonly DoseLimitConfiguration _configuration;

    public MultiExposureCoordinatorTests()
    {
        _configuration = new DoseLimitConfiguration
        {
            StudyDoseLimit = 1000m,
            DailyDoseLimit = 5000m,
            WarningThresholdPercent = 0.8m
        };
    }

    [Fact]
    public void GetCumulativeDose_ForNonExistentStudy_ReturnsNull()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);

        // Act
        var result = sut.GetCumulativeDose("NON-EXISTENT");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RecordExposure_CreatesNewCollection_WithLimitChecking()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var exposure = CreateExposure(500m);

        // Act
        sut.RecordExposure(studyId, patientId, exposure);
        var result = sut.GetCumulativeDose(studyId);

        // Assert
        result.Should().NotBeNull();
        result!.StudyId.Should().Be(studyId);
        result.TotalDap.Should().Be(500m);
        result.IsWithinLimits.Should().BeTrue();
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void RecordExposure_ExceedsLimit_ReturnsIsWithinLimitsFalse()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var exposure = CreateExposure(1500m);  // Exceeds limit

        // Act
        sut.RecordExposure(studyId, patientId, exposure);
        var result = sut.GetCumulativeDose(studyId);

        // Assert
        result.Should().NotBeNull();
        result!.IsWithinLimits.Should().BeFalse();
        result.TotalDap.Should().Be(1500m);
        result.DoseLimit.Should().Be(1000m);
    }

    [Fact]
    public void RecordExposure_MultipleExposures_CumulativeDoseAccumulates()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";

        // Act
        sut.RecordExposure(studyId, patientId, CreateExposure(300m));
        sut.RecordExposure(studyId, patientId, CreateExposure(400m));
        sut.RecordExposure(studyId, patientId, CreateExposure(200m));
        var result = sut.GetCumulativeDose(studyId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalDap.Should().Be(900m);
        result.ExposureCount.Should().Be(3);
        result.IsWithinLimits.Should().BeTrue();
    }

    [Fact]
    public void RecordExposure_CumulativeExceedsLimit_ReturnsIsWithinLimitsFalse()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";

        // Act
        sut.RecordExposure(studyId, patientId, CreateExposure(600m));
        sut.RecordExposure(studyId, patientId, CreateExposure(500m));  // Total 1100m
        var result = sut.GetCumulativeDose(studyId);

        // Assert
        result.Should().NotBeNull();
        result!.IsWithinLimits.Should().BeFalse();
        result.TotalDap.Should().Be(1100m);
    }

    [Fact]
    public void GetOrCreateCollection_SameStudy_ReturnsSameCollection()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";

        // Act
        var collection1 = sut.GetOrCreateCollection(studyId, patientId);
        var collection2 = sut.GetOrCreateCollection(studyId, patientId);

        // Assert
        collection1.Should().BeSameAs(collection2);
    }

    [Fact]
    public void GetOrCreateCollection_DifferentStudies_ReturnsDifferentCollections()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);

        // Act
        var collection1 = sut.GetOrCreateCollection("STUDY-001", "PATIENT-001");
        var collection2 = sut.GetOrCreateCollection("STUDY-002", "PATIENT-001");

        // Assert
        collection1.Should().NotBeSameAs(collection2);
        collection1.StudyId.Should().Be("STUDY-001");
        collection2.StudyId.Should().Be("STUDY-002");
    }

    [Fact]
    public void RemoveCollection_ExistingStudy_RemovesCollection()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        sut.RecordExposure(studyId, patientId, CreateExposure(500m));

        // Act
        sut.RemoveCollection(studyId);
        var result = sut.GetCumulativeDose(studyId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void HasPendingExposures_WithPendingExposure_ReturnsTrue()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var pendingExposure = CreateExposure(500m);
        pendingExposure.Status = ExposureStatus.Pending;

        sut.RecordExposure(studyId, patientId, pendingExposure);

        // Act
        var result = sut.HasPendingExposures(studyId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPendingExposures_WithAllCompletedExposures_ReturnsFalse()
    {
        // Arrange
        var sut = new MultiExposureCoordinator(_configuration);
        var studyId = "STUDY-001";
        var patientId = "PATIENT-001";
        var acceptedExposure = CreateExposure(500m);
        acceptedExposure.Status = ExposureStatus.Accepted;

        sut.RecordExposure(studyId, patientId, acceptedExposure);

        // Act
        var result = sut.HasPendingExposures(studyId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new MultiExposureCoordinator(null!);

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
