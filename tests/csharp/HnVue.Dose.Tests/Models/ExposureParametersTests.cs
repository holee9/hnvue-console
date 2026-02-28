using FluentAssertions;
using HnVue.Dose.Calculation;
using HnVue.Dose.Tests.TestHelpers;
using Xunit;

namespace HnVue.Dose.Tests.Models;

/// <summary>
/// Unit tests for ExposureParameters domain model.
/// SPEC-DOSE-001 FR-DOSE-06, Section 4.4.
/// </summary>
public class ExposureParametersTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesParameters()
    {
        // Arrange
        var timestampUtc = DateTime.UtcNow;

        // Act
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = timestampUtc,
            AcquisitionProtocol = "CXR PA",
            BodyRegionCode = "Chest"
        };

        // Assert
        parameters.KvpValue.Should().Be(80m);
        parameters.MasValue.Should().Be(10m);
        parameters.FilterMaterial.Should().Be("AL");
        parameters.FilterThicknessMm.Should().Be(2.5m);
        parameters.SidMm.Should().Be(1000m);
        parameters.FieldWidthMm.Should().Be(300m);
        parameters.FieldHeightMm.Should().Be(400m);
        parameters.TimestampUtc.Should().Be(timestampUtc);
        parameters.AcquisitionProtocol.Should().Be("CXR PA");
        parameters.BodyRegionCode.Should().Be("Chest");
    }

    [Fact]
    public void IsValid_WithAllValidParameters_ReturnsTrue()
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    public static TheoryData<decimal> KvpValues => new()
    {
        19m,  // Below minimum
        20m,  // At minimum
        80m,  // Typical value
        150m, // At maximum
        151m  // Above maximum
    };

    [Theory]
    [MemberData(nameof(KvpValues))]
    public void IsValid_WithKvpValue_ValidatesRange(decimal kvpValue)
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = kvpValue,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        var expected = kvpValue >= 20m && kvpValue <= 150m;
        result.Should().Be(expected);
    }

    public static TheoryData<decimal> MasValues => new()
    {
        0m,     // Below minimum
        0.1m,   // At minimum
        10m,    // Typical value
        1000m,  // At maximum
        1001m   // Above maximum
    };

    [Theory]
    [MemberData(nameof(MasValues))]
    public void IsValid_WithMasValue_ValidatesRange(decimal masValue)
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = masValue,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        var expected = masValue > 0m && masValue <= 1000m;
        result.Should().Be(expected);
    }

    public static TheoryData<decimal> SidValues => new()
    {
        799m,  // Below minimum
        800m,  // At minimum
        1000m, // Typical value
        2000m, // At maximum
        2001m  // Above maximum
    };

    [Theory]
    [MemberData(nameof(SidValues))]
    public void IsValid_WithSidMm_ValidatesRange(decimal sidMm)
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = sidMm,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        var expected = sidMm >= 800m && sidMm <= 2000m;
        result.Should().Be(expected);
    }

    public static TheoryData<decimal> FieldWidthValues => new()
    {
        0m,    // Below minimum
        1m,    // At minimum
        300m,  // Typical value
        500m,  // At maximum
        501m   // Above maximum
    };

    [Theory]
    [MemberData(nameof(FieldWidthValues))]
    public void IsValid_WithFieldWidthMm_ValidatesRange(decimal fieldWidthMm)
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = fieldWidthMm,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        var expected = fieldWidthMm > 0m && fieldWidthMm <= 500m;
        result.Should().Be(expected);
    }

    public static TheoryData<decimal> FieldHeightValues => new()
    {
        0m,    // Below minimum
        1m,    // At minimum
        400m,  // Typical value
        500m,  // At maximum
        501m   // Above maximum
    };

    [Theory]
    [MemberData(nameof(FieldHeightValues))]
    public void IsValid_WithFieldHeightMm_ValidatesRange(decimal fieldHeightMm)
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = fieldHeightMm,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        var expected = fieldHeightMm > 0m && fieldHeightMm <= 500m;
        result.Should().Be(expected);
    }

    public static TheoryData<decimal> FilterThicknessValues => new()
    {
        -1m,   // Below minimum
        0m,    // At minimum
        2.5m,  // Typical value
        10m,   // At maximum
        11m    // Above maximum
    };

    [Theory]
    [MemberData(nameof(FilterThicknessValues))]
    public void IsValid_WithFilterThicknessMm_ValidatesRange(decimal filterThicknessMm)
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = filterThicknessMm,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        var expected = filterThicknessMm >= 0m && filterThicknessMm <= 10m;
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void IsValid_WithNullOrWhitespaceFilterMaterial_ReturnsFalse(string? filterMaterial)
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = filterMaterial!,
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("AL")]
    [InlineData("CU")]
    [InlineData("MO")]
    [InlineData("RH")]
    public void IsValid_WithValidFilterMaterial_ReturnsTrue(string filterMaterial)
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = filterMaterial,
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CXR PA")]
    [InlineData("Abdomen AP")]
    public void AcquisitionProtocol_AcceptsOptionalValues(string? protocol)
    {
        // Arrange & Act
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow,
            AcquisitionProtocol = protocol
        };

        // Assert
        parameters.AcquisitionProtocol.Should().Be(protocol);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Chest")]
    [InlineData("Abdomen")]
    public void BodyRegionCode_AcceptsOptionalValues(string? bodyRegion)
    {
        // Arrange & Act
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = DateTime.UtcNow,
            BodyRegionCode = bodyRegion
        };

        // Assert
        parameters.BodyRegionCode.Should().Be(bodyRegion);
    }

    [Fact]
    public void TimestampUtc_RecordsUtcTime()
    {
        // Arrange
        var timestamp = new DateTime(2025, 2, 28, 14, 30, 45, DateTimeKind.Utc);

        // Act
        var parameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 300m,
            FieldHeightMm = 400m,
            TimestampUtc = timestamp
        };

        // Assert
        parameters.TimestampUtc.Should().Be(timestamp);
        parameters.TimestampUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void IsValid_WithMultipleInvalidParameters_ReturnsFalse()
    {
        // Arrange
        var parameters = new ExposureParameters
        {
            KvpValue = 10m,          // Invalid: < 20
            MasValue = 2000m,        // Invalid: > 1000
            FilterMaterial = "",     // Invalid: empty
            FilterThicknessMm = 15m, // Invalid: > 10
            SidMm = 500m,            // Invalid: < 800
            FieldWidthMm = 600m,     // Invalid: > 500
            FieldHeightMm = 700m,    // Invalid: > 500
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result = parameters.IsValid();

        // Assert
        result.Should().BeFalse();
    }
}
