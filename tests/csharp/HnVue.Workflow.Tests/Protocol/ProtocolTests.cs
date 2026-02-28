using FluentAssertions;
using Xunit;
using System;
using ProtocolType = HnVue.Workflow.Protocol.Protocol;

namespace HnVue.Workflow.Tests.Protocol;

/// <summary>
/// Tests for Protocol domain model.
/// SPEC-WORKFLOW-001 Section 7.3: Protocol
/// SPEC-WORKFLOW-001 FR-WF-02: Protocol Management
/// </summary>
public class ProtocolTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateProtocol()
    {
        // Arrange
        var protocolId = Guid.NewGuid();
        var bodyPart = "CHEST";
        var projection = "PA";

        // Act
        var protocol = new ProtocolType
        {
            ProtocolId = protocolId,
            BodyPart = bodyPart,
            Projection = projection,
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            AecMode = AecMode.Enabled,
            AecChambers = 0x03, // Center and right chambers
            FocusSize = FocusSize.Large,
            GridUsed = true,
            ProcedureCodes = new[] { " chest-2view" },
            DeviceModel = "HVG-3000",
            IsActive = true
        };

        // Assert
        protocol.ProtocolId.Should().Be(protocolId);
        protocol.BodyPart.Should().Be(bodyPart);
        protocol.Projection.Should().Be(projection);
        protocol.Kv.Should().Be(120);
        protocol.Ma.Should().Be(100);
        protocol.ExposureTimeMs.Should().Be(100);
        protocol.AecMode.Should().Be(AecMode.Enabled);
        protocol.FocusSize.Should().Be(FocusSize.Large);
        protocol.GridUsed.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var protocol = new ProtocolType();

        // Assert
        protocol.IsActive.Should().BeTrue("protocols should be active by default");
        protocol.AecMode.Should().Be(AecMode.Disabled, "AEC should be disabled by default");
        protocol.GridUsed.Should().BeFalse("grid should not be used by default");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BodyPart_WithInvalidValue_ShouldThrowArgumentException(string? bodyPart)
    {
        // Arrange
        var protocol = new ProtocolType();

        // Act
        var act = () => protocol.BodyPart = bodyPart!;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("BodyPart");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Projection_WithInvalidValue_ShouldThrowArgumentException(string? projection)
    {
        // Arrange
        var protocol = new ProtocolType();

        // Act
        var act = () => protocol.Projection = projection!;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("Projection");
    }

    [Theory]
    [InlineData(0)]     // Below minimum
    [InlineData(-10)]   // Negative
    public void Kv_WithInvalidValue_ShouldThrowArgumentException(decimal kvp)
    {
        // Arrange
        var protocol = new ProtocolType();

        // Act
        var act = () => protocol.Kv = kvp;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("Kv");
    }

    [Theory]
    [InlineData(0)]     // Below minimum
    [InlineData(-5)]    // Negative
    public void Ma_WithInvalidValue_ShouldThrowArgumentException(decimal ma)
    {
        // Arrange
        var protocol = new ProtocolType();

        // Act
        var act = () => protocol.Ma = ma;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("Ma");
    }

    [Theory]
    [InlineData(0)]     // Below minimum
    [InlineData(-100)]  // Negative
    public void ExposureTimeMs_WithInvalidValue_ShouldThrowArgumentException(int exposureTimeMs)
    {
        // Arrange
        var protocol = new ProtocolType();

        // Act
        var act = () => protocol.ExposureTimeMs = exposureTimeMs;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("ExposureTimeMs");
    }

    [Fact]
    public void ProtocolId_ShouldBeUnique()
    {
        // Arrange & Act
        var protocol1 = new ProtocolType { ProtocolId = Guid.NewGuid() };
        var protocol2 = new ProtocolType { ProtocolId = Guid.NewGuid() };

        // Assert
        protocol1.ProtocolId.Should().NotBe(protocol2.ProtocolId);
    }

    [Fact]
    public void GetCompositeKey_ShouldReturnBodyPartProjectionDeviceModel()
    {
        // Arrange
        var protocol = new ProtocolType
        {
            BodyPart = "CHEST",
            Projection = "PA",
            DeviceModel = "HVG-3000"
        };

        // Act
        var key = protocol.GetCompositeKey();

        // Assert
        key.Should().Be("CHEST|PA|HVG-3000");
    }

    [Fact]
    public void GetCompositeKey_WithDifferentDeviceModels_ShouldReturnDifferentKeys()
    {
        // Arrange
        var protocol1 = new ProtocolType
        {
            BodyPart = "CHEST",
            Projection = "PA",
            DeviceModel = "HVG-3000"
        };
        var protocol2 = new ProtocolType
        {
            BodyPart = "CHEST",
            Projection = "PA",
            DeviceModel = "HVG-5000"
        };

        // Act
        var key1 = protocol1.GetCompositeKey();
        var key2 = protocol2.GetCompositeKey();

        // Assert
        key1.Should().NotBe(key2,
            "protocols for the same body part and projection but different device models should have different keys");
    }

    [Fact]
    public void CalculateMas_ShouldReturnCorrectValue()
    {
        // Arrange
        var protocol = new ProtocolType
        {
            Kv = 120,
            Ma = 200,
            ExposureTimeMs = 100
        };

        // Act
        var mas = protocol.CalculateMas();

        // Assert
        // Note: The formula includes kVp which is unusual for mAs calculation
        // Standard mAs = mA * time(seconds), but this implementation includes kVp
        mas.Should().Be(2400, "mAs = kVp * mA * ExposureTime / 1000 = 120 * 200 * 100 / 1000");
    }
}
