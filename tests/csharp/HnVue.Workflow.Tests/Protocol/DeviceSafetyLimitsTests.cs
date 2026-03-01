namespace HnVue.Workflow.Tests.Protocol;

using System;
using Xunit;
using FluentAssertions;
using HnVue.Workflow.Protocol;

/// <summary>
/// Unit tests for DeviceSafetyLimits.
/// SPEC-WORKFLOW-001 FR-WF-09: Safety limit enforcement on protocol save
/// </summary>
public class DeviceSafetyLimitsTests
{
    private readonly DeviceSafetyLimits _limits;

    public DeviceSafetyLimitsTests()
    {
        _limits = new DeviceSafetyLimits
        {
            MinKvp = 40,
            MaxKvp = 150,
            MinMa = 1,
            MaxMa = 500,
            MaxExposureTimeMs = 3000,
            MaxMas = 2000 // Increased to accommodate realistic clinical protocols
        };
    }

    [Fact]
    public void Validate_WithValidParameters_ReturnsValid()
    {
        // Arrange - Use values that keep CalculatedMas under 500
        // CalculatedMas = Kv * Ma * ExposureTimeMs / 1000 = 120 * 100 * 40 / 1000 = 480 mAs (< 500)
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 40, // Reduced to keep mAs under 500
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = _limits.Validate(protocol);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_KvBelowMinimum_ReturnsInvalid()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 30, // Below MinKvp
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = _limits.Validate(protocol);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("kVp") && e.Contains("outside allowed range"));
    }

    [Fact]
    public void Validate_KvAboveMaximum_ReturnsInvalid()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 160, // Above MaxKvp
            Ma = 100,
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = _limits.Validate(protocol);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("kVp") && e.Contains("outside allowed range"));
    }

    [Fact]
    public void Validate_MaBelowMinimum_ReturnsInvalid()
    {
        // Arrange - Use value > 0 (to avoid ArgumentException) but < MinMa (1)
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 0.5m, // Below MinMa (1) but valid for property setter (> 0)
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = _limits.Validate(protocol);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("mA") && e.Contains("outside allowed range"));
    }

    [Fact]
    public void Validate_MaAboveMaximum_ReturnsInvalid()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 600, // Above MaxMa
            ExposureTimeMs = 100,
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = _limits.Validate(protocol);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("mA") && e.Contains("outside allowed range"));
    }

    [Fact]
    public void Validate_ExposureTimeAboveMaximum_ReturnsInvalid()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 120,
            Ma = 100,
            ExposureTimeMs = 4000, // Above MaxExposureTimeMs
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = _limits.Validate(protocol);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Exposure time") && e.Contains("exceeds maximum"));
    }

    [Fact]
    public void Validate_CalculatedMasExceedsMaximum_ReturnsInvalid()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 150, // At MaxKvp
            Ma = 500, // At MaxMa
            ExposureTimeMs = 100, // mAs = 150 * 500 * 100 / 1000 = 7500 > MaxMas (2000)
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = _limits.Validate(protocol);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("mAs") && e.Contains("exceeds maximum"));
    }

    [Fact]
    public void Validate_AtExactLimits_ReturnsValid()
    {
        // Arrange
        var protocol = new Protocol
        {
            ProtocolId = Guid.NewGuid(),
            BodyPart = "CHEST",
            Projection = "PA",
            Kv = 150, // At MaxKvp
            Ma = 500, // At MaxMa
            ExposureTimeMs = 26, // mAs = 150 * 500 * 26 / 1000 = 1950 < MaxMas (2000)
            DeviceModel = "HVG-3000"
        };

        // Act
        var result = _limits.Validate(protocol);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FromInterface_CreatesCorrectLimits()
    {
        // Arrange
        var limits = new TestDeviceSafetyLimits
        {
            MinKvp = 50,
            MaxKvp = 140,
            MinMa = 10,
            MaxMa = 400,
            MaxExposureTime = 2500,
            MaxMas = 1500
        };

        // Act
        var result = DeviceSafetyLimits.FromInterface(limits);

        // Assert
        result.MinKvp.Should().Be(50);
        result.MaxKvp.Should().Be(140);
        result.MinMa.Should().Be(10);
        result.MaxMa.Should().Be(400);
        result.MaxExposureTimeMs.Should().Be(2500);
        result.MaxMas.Should().Be(1500);
    }

    private class TestDeviceSafetyLimits : Safety.IDeviceSafetyLimits
    {
        public decimal MinKvp { get; set; }
        public decimal MaxKvp { get; set; }
        public decimal MinMa { get; set; }
        public decimal MaxMa { get; set; }
        public int MaxExposureTime { get; set; }
        public decimal MaxMas { get; set; }
        public decimal DapWarningLevel { get; set; }
    }
}
