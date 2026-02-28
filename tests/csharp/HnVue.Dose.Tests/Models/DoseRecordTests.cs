using FluentAssertions;
using HnVue.Dicom.Rdsr;
using HnVue.Dose.Tests.TestHelpers;
using Xunit;

namespace HnVue.Dose.Tests.Models;

/// <summary>
/// Unit tests for DoseRecord domain model.
/// SPEC-DOSE-001 Section 4.1.4 Dose Recording.
/// </summary>
public class DoseRecordTests
{
    [Fact]
    public void Constructor_WithAllParameters_CreatesRecord()
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };

        // Assert
        record.ExposureEventId.Should().NotBeEmpty();
        record.IrradiationEventUid.Should().NotBeEmpty();
        record.StudyInstanceUid.Should().Be(DoseTestData.Uids.StudyInstanceUid);
        record.PatientId.Should().Be(DoseTestData.Uids.PatientId);
        record.CalculatedDapGyCm2.Should().Be(0.015m);
        record.DoseSource.Should().Be(DoseSource.Calculated);
        record.DrlExceedance.Should().BeFalse();
    }

    [Fact]
    public void EffectiveDapGyCm2_WithCalculatedDoseSource_ReturnsCalculatedDap()
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };

        // Assert
        record.EffectiveDapGyCm2.Should().Be(record.CalculatedDapGyCm2);
    }

    [Fact]
    public void EffectiveDapGyCm2_WithMeasuredDoseSource_ReturnsMeasuredDap()
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = 0.0145m,
            DoseSource = DoseSource.Measured,
            DrlExceedance = false
        };

        // Assert
        record.EffectiveDapGyCm2.Should().Be(record.MeasuredDapGyCm2);
    }

    [Fact]
    public void EffectiveDapGyCm2_WithMeasuredDoseSourceButNoMeasuredValue_UsesCalculatedDap()
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Measured,
            DrlExceedance = false
        };

        // Assert
        record.EffectiveDapGyCm2.Should().Be(record.CalculatedDapGyCm2);
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(0.010)]
    [InlineData(0.100)]
    [InlineData(1.000)]
    public void CalculatedDapGyCm2_AcceptsValidRange(decimal dap)
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = dap,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };

        // Assert
        record.CalculatedDapGyCm2.Should().Be(dap);
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(0.010)]
    [InlineData(0.100)]
    public void MeasuredDapGyCm2_AcceptsValidRange(decimal measuredDap)
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = measuredDap,
            DoseSource = DoseSource.Measured,
            DrlExceedance = false
        };

        // Assert
        record.MeasuredDapGyCm2.Should().Be(measuredDap);
        record.DoseSource.Should().Be(DoseSource.Measured);
    }

    [Fact]
    public void DoseSource_Calculated_SetsMeasuredDapGyCm2ToNull()
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };

        // Assert
        record.DoseSource.Should().Be(DoseSource.Calculated);
        record.MeasuredDapGyCm2.Should().BeNull();
    }

    [Fact]
    public void DoseSource_Measured_SetsMeasuredDapGyCm2ToValue()
    {
        // Arrange & Act
        var measuredDap = 0.0145m;
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = measuredDap,
            DoseSource = DoseSource.Measured,
            DrlExceedance = false
        };

        // Assert
        record.DoseSource.Should().Be(DoseSource.Measured);
        record.MeasuredDapGyCm2.Should().Be(measuredDap);
    }

    [Fact]
    public void DrlExceedance_True_IndicatesDoseExceededThreshold()
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = true
        };

        // Assert
        record.DrlExceedance.Should().BeTrue();
    }

    [Fact]
    public void DrlExceedance_False_IndicatesDoseWithinThreshold()
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };

        // Assert
        record.DrlExceedance.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CXR PA")]
    [InlineData("Abdomen AP")]
    [InlineData("Extremity")]
    public void AcquisitionProtocol_AcceptsOptionalValues(string? protocol)
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            AcquisitionProtocol = protocol,
            DrlExceedance = false
        };

        // Assert
        record.AcquisitionProtocol.Should().Be(protocol);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CHEST")]
    [InlineData("ABDOMEN")]
    [InlineData("EXTREMITY")]
    public void BodyRegionCode_AcceptsOptionalValues(string? bodyRegionCode)
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            BodyRegionCode = bodyRegionCode,
            DrlExceedance = false
        };

        // Assert
        record.BodyRegionCode.Should().Be(bodyRegionCode);
    }

    public static TheoryData<DateTime> ValidTimestamps => new()
    {
        DateTime.UtcNow,
        DateTime.UtcNow.AddMinutes(-1),
        DateTime.UtcNow.AddHours(-1)
    };

    [Theory]
    [MemberData(nameof(ValidTimestamps))]
    public void TimestampUtc_AcceptsValidValues(DateTime timestamp)
    {
        // Arrange & Act
        var record = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = timestamp,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };

        // Assert
        record.TimestampUtc.Should().Be(timestamp);
    }

    [Fact]
    public void ExposureEventId_GeneratesUniqueId()
    {
        // Arrange & Act
        var record1 = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };

        var record2 = new DoseRecord
        {
            ExposureEventId = Guid.NewGuid(),
            IrradiationEventUid = DoseTestData.CreateIrradiationEventUid(0),
            StudyInstanceUid = DoseTestData.Uids.StudyInstanceUid,
            PatientId = DoseTestData.Uids.PatientId,
            TimestampUtc = DateTime.UtcNow,
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            DrlExceedance = false
        };

        // Assert
        record1.ExposureEventId.Should().NotBe(record2.ExposureEventId);
    }
}
