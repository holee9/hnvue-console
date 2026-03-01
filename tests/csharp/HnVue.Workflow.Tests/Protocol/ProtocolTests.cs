namespace HnVue.Workflow.Tests.Protocol;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using HnVue.Workflow.Protocol;

/// <summary>
/// Unit tests for Protocol entity.
/// SPEC-WORKFLOW-001 FR-WF-08: Protocol definition and validation
/// </summary>
public class ProtocolTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesProtocol()
    {
        // Arrange & Act
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        // Assert
        protocol.BodyPart.Should().Be("CHEST");
        protocol.Projection.Should().Be("PA");
        protocol.Kv.Should().Be(120);
        protocol.Ma.Should().Be(100);
        protocol.ExposureTimeMs.Should().Be(100);
        protocol.DeviceModel.Should().Be("HVG-3000");
        protocol.IsActive.Should().BeTrue();
        protocol.ProcedureCodes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BodyPart_WithInvalidValue_ThrowsArgumentException(string? value)
    {
        // Arrange
        var protocol = new Protocol { BodyPart = "CHEST" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => protocol.BodyPart = value!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Projection_WithInvalidValue_ThrowsArgumentException(string? value)
    {
        // Arrange
        var protocol = new Protocol { Projection = "PA" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => protocol.Projection = value!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Kv_WithInvalidValue_ThrowsArgumentException(decimal value)
    {
        // Arrange
        var protocol = new Protocol { Kv = 120 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => protocol.Kv = value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Ma_WithInvalidValue_ThrowsArgumentException(decimal value)
    {
        // Arrange
        var protocol = new Protocol { Ma = 100 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => protocol.Ma = value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void ExposureTimeMs_WithInvalidValue_ThrowsArgumentException(int value)
    {
        // Arrange
        var protocol = new Protocol { ExposureTimeMs = 100 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => protocol.ExposureTimeMs = value);
    }

    [Fact]
    public void CompositeKey_ReturnsExpectedFormat()
    {
        // Arrange
        var protocol = new Protocol
        {
            BodyPart = "chest",
            Projection = "pa",
            DeviceModel = "HVG-3000"
        };

        // Act
        var key = protocol.CompositeKey;

        // Assert
        key.Should().Be("CHEST|PA|HVG-3000");
    }

    [Fact]
    public void CalculatedMas_ReturnsCorrectValue()
    {
        // Arrange
        var protocol = new Protocol
        {
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 100
        };

        // Act
        var mas = protocol.CalculatedMas;

        // Assert
        mas.Should().Be(1200m); // 120 * 100 * 100 / 1000
    }

    [Fact]
    public void Equality_SameProtocolId_ReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var protocol1 = new Protocol { ProtocolId = id };
        var protocol2 = new Protocol { ProtocolId = id };

        // Act & Assert
        Assert.True(protocol1 == protocol2);
        Assert.True(protocol1.Equals(protocol2));
    }

    [Fact]
    public void Equality_DifferentProtocolId_ReturnsFalse()
    {
        // Arrange
        var protocol1 = new Protocol { ProtocolId = Guid.NewGuid() };
        var protocol2 = new Protocol { ProtocolId = Guid.NewGuid() };

        // Act & Assert
        Assert.True(protocol1 != protocol2);
        Assert.False(protocol1.Equals(protocol2));
    }

    [Fact]
    public void BodyPart_NormalizesToUpperCase()
    {
        // Arrange
        var protocol = new Protocol();

        // Act
        protocol.BodyPart = "chest";

        // Assert
        protocol.BodyPart.Should().Be("CHEST");
    }

    [Fact]
    public void Projection_NormalizesToUpperCase()
    {
        // Arrange
        var protocol = new Protocol();

        // Act
        protocol.Projection = "pa";

        // Assert
        protocol.Projection.Should().Be("PA");
    }
}
