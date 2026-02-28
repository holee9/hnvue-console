namespace HnVue.Workflow.Tests.Study;

using System;
using System.Linq;
using FluentAssertions;
using HnVue.Workflow.Protocol;
using Xunit;

/// <summary>
/// Unit tests for StudyContext and ExposureRecord models.
/// Tests study lifecycle, exposure tracking, and multi-exposure support.
///
/// SPEC-WORKFLOW-001 Section 7: Data Models
/// SPEC-WORKFLOW-001 FR-WF-05: Multi-Exposure Study Support
/// </summary>
public class StudyContextTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateStudyContext()
    {
        // Arrange
        var studyUid = "1.2.840.10008.1.2.3.4.5";
        var accessionNumber = "ACC-12345";
        var patientId = "PATIENT-001";
        var patientName = "Doe^John";
        var isEmergency = false;

        // Act
        var context = new StudyContext(studyUid, accessionNumber, patientId, patientName, isEmergency, worklistItemUID: null, patientBirthDate: null, patientSex: null);

        // Assert
        context.StudyInstanceUID.Should().Be(studyUid);
        context.AccessionNumber.Should().Be(accessionNumber);
        context.PatientID.Should().Be(patientId);
        context.PatientName.Should().Be(patientName);
        context.IsEmergency.Should().Be(isEmergency);
        context.ExposureSeries.Should().BeEmpty();
        context.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_ForEmergencyStudy_ShouldSetIsEmergencyTrue()
    {
        // Arrange & Act
        var context = new StudyContext(
            "1.2.840.10008.1.2.3.4.5",
            "EMERGENCY-001",
            "TEMP-PATIENT",
            "Emergency^Patient",
            true,
            worklistItemUID: null,
            patientBirthDate: null,
            patientSex: null);

        // Assert
        context.IsEmergency.Should().BeTrue();
        context.WorklistItemUID.Should().BeNull("emergency workflow has no worklist item");
    }

    [Fact]
    public void Constructor_WithWorklistItem_ShouldStoreWorklistItemUID()
    {
        // Arrange & Act
        var context = new StudyContext(
            "1.2.840.10008.1.2.3.4.5",
            "ACC-12345",
            "PATIENT-001",
            "Doe^John",
            false,
            worklistItemUID: "1.2.840.10008.1.2.3.4.5.6.7",
            patientBirthDate: null,
            patientSex: null);

        // Assert
        context.WorklistItemUID.Should().Be("1.2.840.10008.1.2.3.4.5.6.7");
    }

    [Fact]
    public void AddExposure_ShouldIncrementExposureCount()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();
        var operatorId = "operator1";

        // Act
        context.AddExposure(protocol, operatorId);

        // Assert
        context.ExposureSeries.Should().HaveCount(1);
        context.ExposureSeries[0].ExposureIndex.Should().Be(1);
        context.ExposureSeries[0].Status.Should().Be(ExposureStatus.Pending);
    }

    [Fact]
    public void AddExposure_MultipleExposures_ShouldUseSequentialIndices()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        // Act
        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");

        // Assert
        context.ExposureSeries.Should().HaveCount(3);
        context.ExposureSeries[0].ExposureIndex.Should().Be(1);
        context.ExposureSeries[1].ExposureIndex.Should().Be(2);
        context.ExposureSeries[2].ExposureIndex.Should().Be(3);
    }

    [Fact]
    public void HasMoreExposures_WithPendingExposures_ShouldReturnTrue()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");

        // Mark first as acquired, second still pending
        context.UpdateExposureStatus(1, ExposureStatus.Acquired);

        // Act & Assert
        context.HasMoreExposures.Should().BeTrue("second exposure is still pending");
    }

    [Fact]
    public void HasMoreExposures_WithAllAccepted_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        context.AddExposure(protocol, "operator1");
        context.UpdateExposureStatus(1, ExposureStatus.Accepted);

        // Act & Assert
        context.HasMoreExposures.Should().BeFalse("all exposures are accepted");
    }

    [Fact]
    public void HasMoreExposures_WithAllRejected_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        context.AddExposure(protocol, "operator1");
        context.UpdateExposureStatus(1, ExposureStatus.Rejected);

        // Act & Assert
        context.HasMoreExposures.Should().BeFalse("exposure was rejected");
    }

    [Fact]
    public void GetPendingExposureCount_ShouldReturnCountOfPendingExposures()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");

        context.UpdateExposureStatus(1, ExposureStatus.Acquired);
        context.UpdateExposureStatus(3, ExposureStatus.Accepted);

        // Act
        var pendingCount = context.GetPendingExposureCount();

        // Assert
        pendingCount.Should().Be(1, "only exposure #2 is still pending");
    }

    [Fact]
    public void GetAcceptedExposureCount_ShouldReturnCountOfAcceptedExposures()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");

        context.UpdateExposureStatus(1, ExposureStatus.Accepted);
        context.UpdateExposureStatus(2, ExposureStatus.Rejected);
        context.UpdateExposureStatus(3, ExposureStatus.Accepted);

        // Act
        var acceptedCount = context.GetAcceptedExposureCount();

        // Assert
        acceptedCount.Should().Be(2);
    }

    [Fact]
    public void GetRejectedExposureCount_ShouldReturnCountOfRejectedExposures()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");

        context.UpdateExposureStatus(1, ExposureStatus.Rejected);
        context.UpdateExposureStatus(2, ExposureStatus.Accepted);

        // Act
        var rejectedCount = context.GetRejectedExposureCount();

        // Assert
        rejectedCount.Should().Be(1);
    }

    [Fact]
    public void UpdateExposureStatus_ShouldChangeExposureStatus()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();
        context.AddExposure(protocol, "operator1");

        // Act
        context.UpdateExposureStatus(1, ExposureStatus.Acquired);

        // Assert
        context.ExposureSeries[0].Status.Should().Be(ExposureStatus.Acquired);
    }

    [Fact]
    public void UpdateExposureStatus_WithInvalidIndex_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var context = CreateTestStudyContext();

        // Act
        var act = () => context.UpdateExposureStatus(99, ExposureStatus.Accepted);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RecordAcquisition_ShouldSetImageInstanceUIDAndDose()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();
        context.AddExposure(protocol, "operator1");

        var imageUid = "1.2.840.10008.1.2.3.4.5.6.7.8.9";
        var dose = 12.5m;

        // Act
        context.RecordAcquisition(1, imageUid, dose);

        // Assert
        var exposure = context.ExposureSeries[0];
        exposure.ImageInstanceUID.Should().Be(imageUid);
        exposure.AdministeredDap.Should().Be(dose);
        exposure.Status.Should().Be(ExposureStatus.Acquired);
        exposure.AcquiredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordRejection_ShouldSetRejectReason()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();
        context.AddExposure(protocol, "operator1");
        context.UpdateExposureStatus(1, ExposureStatus.Acquired);

        // Act
        context.RecordRejection(1, RejectReason.Motion, "operator1");

        // Assert
        var exposure = context.ExposureSeries[0];
        exposure.Status.Should().Be(ExposureStatus.Rejected);
        exposure.RejectReason.Should().Be(RejectReason.Motion);
    }

    [Fact]
    public void GetTotalDose_ShouldSumAllExposureDose()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");

        context.RecordAcquisition(1, "img1", 10.0m);
        context.RecordAcquisition(2, "img2", 15.0m);
        context.RecordAcquisition(3, "img3", 12.5m);

        // Act
        var totalDose = context.GetTotalDose();

        // Assert
        totalDose.Should().Be(37.5m);
    }

    [Fact]
    public void GetTotalDose_ShouldIncludeRejectedExposures()
    {
        // Arrange
        var context = CreateTestStudyContext();
        var protocol = CreateTestProtocol();

        context.AddExposure(protocol, "operator1");
        context.AddExposure(protocol, "operator1");

        context.RecordAcquisition(1, "img1", 10.0m);
        context.RecordAcquisition(2, "img2", 15.0m);
        context.RecordRejection(2, RejectReason.Positioning, "operator1");

        // Act
        var totalDose = context.GetTotalDose();

        // Assert
        totalDose.Should().Be(25.0m, "rejected exposure dose is still included");
    }

    private StudyContext CreateTestStudyContext()
    {
        return new StudyContext(
            "1.2.840.10008.1.2.3.4.5",
            "ACC-12345",
            "PATIENT-001",
            "Doe^John",
            false,
            worklistItemUID: null,
            patientBirthDate: null,
            patientSex: null);
    }

    private Protocol CreateTestProtocol()
    {
        return new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120m,
            Ma = 100m,
            ExposureTimeMs = 100
        };
    }
}
