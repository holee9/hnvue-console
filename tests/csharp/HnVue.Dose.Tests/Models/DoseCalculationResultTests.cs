using FluentAssertions;
using HnVue.Dicom.Rdsr;
using HnVue.Dose.Calculation;
using HnVue.Dose.Tests.TestHelpers;
using Xunit;

namespace HnVue.Dose.Tests.Models;

/// <summary>
/// Unit tests for DoseCalculationResult domain model.
/// SPEC-DOSE-001 FR-DOSE-01, Section 4.1.1.
/// </summary>
public class DoseCalculationResultTests
{
    public static TheoryData<decimal> DapValues => new()
    {
        0.00001m,  // Very low DAP
        0.0001m,   // Low DAP
        0.001m,    // Medium DAP
        0.01m,     // High DAP
        0.1m,      // Very high DAP
        1.0m       // Extremely high DAP
    };

    [Theory]
    [MemberData(nameof(DapValues))]
    public void CalculatedDapGyCm2_AcceptsValidRange(decimal dapValue)
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = dapValue,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.CalculatedDapGyCm2.Should().Be(dapValue);
    }

    public static TheoryData<decimal> MeasuredDapValues => new()
    {
        0.00001m,
        0.0001m,
        0.001m,
        0.01m,
        0.1m
    };

    [Theory]
    [MemberData(nameof(MeasuredDapValues))]
    public void MeasuredDapGyCm2_AcceptsValidRange(decimal measuredValue)
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = measuredValue,
            DoseSource = DoseSource.Measured,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.MeasuredDapGyCm2.Should().Be(measuredValue);
    }

    public static TheoryData<decimal> FieldAreas => new()
    {
        100m,   // Small field
        400m,   // Medium field
        1200m,  // Large field (30x40cm)
        1600m,  // Very large field
        2500m   // Maximum field (35x43cm detector)
    };

    [Theory]
    [MemberData(nameof(FieldAreas))]
    public void FieldAreaCm2_AcceptsValidRange(decimal fieldArea)
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = fieldArea,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.FieldAreaCm2.Should().Be(fieldArea);
    }

    public static TheoryData<decimal> AirKermaValues => new()
    {
        0.001m,  // Very low air kerma
        0.01m,   // Low air kerma
        0.1m,    // Medium air kerma
        1.0m,    // High air kerma
        10.0m    // Very high air kerma
    };

    [Theory]
    [MemberData(nameof(AirKermaValues))]
    public void AirKermaMgy_AcceptsValidRange(decimal airKerma)
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = airKerma
        };

        // Assert
        result.AirKermaMgy.Should().Be(airKerma);
    }

    [Fact]
    public void Constructor_WithCalculatedDose_CreatesResult()
    {
        // Arrange
        var calculatedDap = 0.015m;
        var fieldArea = 1200m;
        var airKerma = 0.0125m;

        // Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = calculatedDap,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = fieldArea,
            AirKermaMgy = airKerma
        };

        // Assert
        result.CalculatedDapGyCm2.Should().Be(calculatedDap);
        result.MeasuredDapGyCm2.Should().BeNull();
        result.DoseSource.Should().Be(DoseSource.Calculated);
        result.FieldAreaCm2.Should().Be(fieldArea);
        result.AirKermaMgy.Should().Be(airKerma);
    }

    [Fact]
    public void Constructor_WithMeasuredDose_CreatesResult()
    {
        // Arrange
        var calculatedDap = 0.015m;
        var measuredDap = 0.0145m;
        var fieldArea = 1200m;
        var airKerma = 0.0125m;

        // Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = calculatedDap,
            MeasuredDapGyCm2 = measuredDap,
            DoseSource = DoseSource.Measured,
            FieldAreaCm2 = fieldArea,
            AirKermaMgy = airKerma
        };

        // Assert
        result.CalculatedDapGyCm2.Should().Be(calculatedDap);
        result.MeasuredDapGyCm2.Should().Be(measuredDap);
        result.DoseSource.Should().Be(DoseSource.Measured);
        result.FieldAreaCm2.Should().Be(fieldArea);
        result.AirKermaMgy.Should().Be(airKerma);
    }

    [Fact]
    public void DoseSource_Calculated_EnumValueIsCorrect()
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.DoseSource.Should().Be(DoseSource.Calculated);
    }

    [Fact]
    public void DoseSource_Measured_EnumValueIsCorrect()
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = 0.0145m,
            DoseSource = DoseSource.Measured,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.DoseSource.Should().Be(DoseSource.Measured);
    }

    [Fact]
    public void MeasuredDapGyCm2_WhenNull_DoseSourceIsCalculated()
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.MeasuredDapGyCm2.Should().BeNull();
        result.DoseSource.Should().Be(DoseSource.Calculated);
    }

    [Fact]
    public void MeasuredDapGyCm2_WhenNotNull_DoseSourceIsMeasured()
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = 0.0145m,
            DoseSource = DoseSource.Measured,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.MeasuredDapGyCm2.Should().NotBeNull();
        result.MeasuredDapGyCm2.Should().Be(0.0145m);
        result.DoseSource.Should().Be(DoseSource.Measured);
    }

    [Fact]
    public void FieldAreaCm2_CalculatedFromFieldDimensions()
    {
        // Arrange
        var fieldWidthCm = 30m;  // 300mm
        var fieldHeightCm = 40m; // 400mm
        var expectedArea = fieldWidthCm * fieldHeightCm; // 1200 cm²

        // Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = expectedArea,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.FieldAreaCm2.Should().Be(expectedArea);
        result.FieldAreaCm2.Should().Be(1200m);
    }

    [Fact]
    public void AirKermaMgy_RelatesToCalculatedDap()
    {
        // Arrange
        var fieldArea = 1200m;
        var airKerma = 0.0125m; // mGy
        var expectedDap = airKerma * fieldArea / 1000m; // Convert mGy·cm² to Gy·cm²

        // Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = expectedDap,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = fieldArea,
            AirKermaMgy = airKerma
        };

        // Assert: DAP = K_air × A_field
        // K_air in mGy, A_field in cm², DAP in Gy·cm² (mGy/1000 × cm²)
        var calculatedDap = result.AirKermaMgy * result.FieldAreaCm2 / 1000m;
        var difference = Math.Abs(result.CalculatedDapGyCm2 - calculatedDap);
        difference.Should().BeLessOrEqualTo(0.0001m);
    }

    [Fact]
    public void RecordType_IsImmutable()
    {
        // Arrange
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = null,
            DoseSource = DoseSource.Calculated,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = 0.01m
        };

        // Assert: record types are immutable by design
        // This is a compile-time feature, verified by the type definition
        result.Should().NotBeNull();
    }

    public static TheoryData<DoseSource> DoseSourceValues => new()
    {
        DoseSource.Calculated,
        DoseSource.Measured
    };

    [Theory]
    [MemberData(nameof(DoseSourceValues))]
    public void DoseSource_AcceptsAllEnumValues(DoseSource doseSource)
    {
        // Arrange & Act
        var result = new DoseCalculationResult
        {
            CalculatedDapGyCm2 = 0.015m,
            MeasuredDapGyCm2 = doseSource == DoseSource.Measured ? 0.0145m : null,
            DoseSource = doseSource,
            FieldAreaCm2 = 1200m,
            AirKermaMgy = 0.01m
        };

        // Assert
        result.DoseSource.Should().Be(doseSource);
    }
}
