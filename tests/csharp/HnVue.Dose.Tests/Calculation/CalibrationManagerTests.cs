using FluentAssertions;
using HnVue.Dicom.Rdsr;
using HnVue.Dose.Calculation;
using HnVue.Dose.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Dose.Tests.Calculation;

/// <summary>
/// Unit tests for CalibrationManager.
/// SPEC-DOSE-001 Section 4.1.3 Calibration Management.
/// </summary>
public class CalibrationManagerTests
{
    private readonly CalibrationManager _manager;
    private readonly ILogger<CalibrationManagerTests> _testLogger;

    public CalibrationManagerTests()
    {
        _testLogger = new NullLogger<CalibrationManagerTests>();
        _manager = new CalibrationManager(new NullLogger<CalibrationManager>());
    }

    public static TheoryData<DoseModelParameters> ValidParameters => new()
    {
        new DoseModelParameters
        {
            KFactor = 0.00001m,
            VoltageExponent = 2.0m,
            CalibrationCoefficient = 0.5m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        },
        new DoseModelParameters
        {
            KFactor = 0.001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-002",
            CalibrationDateUtc = DateTime.UtcNow
        },
        new DoseModelParameters
        {
            KFactor = 0.01m,
            VoltageExponent = 3.0m,
            CalibrationCoefficient = 1.5m,
            TubeId = "TUBE-003",
            CalibrationDateUtc = DateTime.UtcNow
        }
    };

    [Theory]
    [MemberData(nameof(ValidParameters))]
    public void LoadCalibration_WithValidParameters_LoadsSuccessfully(DoseModelParameters parameters)
    {
        // Act
        var act = () => _manager.LoadCalibration(parameters);

        // Assert
        act.Should().NotThrow();
        _manager.IsCalibrated.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ValidParameters))]
    public void LoadCalibration_WithValidParameters_SetsCurrentParameters(DoseModelParameters parameters)
    {
        // Act
        _manager.LoadCalibration(parameters);

        // Assert
        var current = _manager.CurrentParameters;
        current.KFactor.Should().Be(parameters.KFactor);
        current.VoltageExponent.Should().Be(parameters.VoltageExponent);
        current.CalibrationCoefficient.Should().Be(parameters.CalibrationCoefficient);
        current.TubeId.Should().Be(parameters.TubeId);
    }

    public static TheoryData<DoseModelParameters> InvalidParameters => new()
    {
        new DoseModelParameters
        {
            KFactor = 0m,  // Invalid: zero KFactor
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        },
        new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 1.0m,  // Invalid: below minimum exponent
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        },
        new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 0m,  // Invalid: zero calibration coefficient
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        },
        new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "",  // Invalid: empty tube ID
            CalibrationDateUtc = DateTime.UtcNow
        }
    };

    [Theory]
    [MemberData(nameof(InvalidParameters))]
    public void LoadCalibration_WithInvalidParameters_ThrowsArgumentException(DoseModelParameters invalidParameters)
    {
        // Act
        var act = () => _manager.LoadCalibration(invalidParameters);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LoadCalibration_WithNullParameters_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _manager.LoadCalibration(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CurrentParameters_WhenNotCalibrated_ThrowsInvalidOperationException()
    {
        // Arrange
        var uncalibratedManager = new CalibrationManager(new NullLogger<CalibrationManager>());

        // Act
        var act = () => uncalibratedManager.CurrentParameters;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No calibration parameters loaded*");
    }

    [Fact]
    public void IsCalibrated_WhenNotCalibrated_ReturnsFalse()
    {
        // Arrange
        var uncalibratedManager = new CalibrationManager(new NullLogger<CalibrationManager>());

        // Assert
        uncalibratedManager.IsCalibrated.Should().BeFalse();
    }

    [Fact]
    public void UpdateCalibration_WithValidParameters_UpdatesCalibration()
    {
        // Arrange
        var initialParams = new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };
        _manager.LoadCalibration(initialParams);

        var newParams = new DoseModelParameters
        {
            KFactor = 0.00012m,
            VoltageExponent = 2.6m,
            CalibrationCoefficient = 1.05m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };
        var operatorId = "OPERATOR-001";

        // Act
        _manager.UpdateCalibration(newParams, operatorId);

        // Assert
        var current = _manager.CurrentParameters;
        current.KFactor.Should().Be(newParams.KFactor);
        current.VoltageExponent.Should().Be(newParams.VoltageExponent);
        current.CalibrationCoefficient.Should().Be(newParams.CalibrationCoefficient);
    }

    [Fact]
    public void UpdateCalibration_WithNullParameters_ThrowsArgumentNullException()
    {
        // Arrange
        var initialParams = new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };
        _manager.LoadCalibration(initialParams);

        // Act
        var act = () => _manager.UpdateCalibration(null!, "OPERATOR-001");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateCalibration_WithEmptyOperatorId_ThrowsArgumentException()
    {
        // Arrange
        var initialParams = new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };
        _manager.LoadCalibration(initialParams);

        var newParams = new DoseModelParameters
        {
            KFactor = 0.00012m,
            VoltageExponent = 2.6m,
            CalibrationCoefficient = 1.05m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };

        // Act
        var act = () => _manager.UpdateCalibration(newParams, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateCalibration_WithWhitespaceOperatorId_ThrowsArgumentException()
    {
        // Arrange
        var initialParams = new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };
        _manager.LoadCalibration(initialParams);

        var newParams = new DoseModelParameters
        {
            KFactor = 0.00012m,
            VoltageExponent = 2.6m,
            CalibrationCoefficient = 1.05m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };

        // Act
        var act = () => _manager.UpdateCalibration(newParams, "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [MemberData(nameof(InvalidParameters))]
    public void UpdateCalibration_WithInvalidParameters_ThrowsArgumentException(DoseModelParameters invalidParameters)
    {
        // Arrange
        var initialParams = new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };
        _manager.LoadCalibration(initialParams);

        // Act
        var act = () => _manager.UpdateCalibration(invalidParameters, "OPERATOR-001");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateParameters_WithValidParameters_ReturnsTrue()
    {
        // Arrange
        var parameters = new DoseModelParameters
        {
            KFactor = 0.0001m,
            VoltageExponent = 2.5m,
            CalibrationCoefficient = 1.0m,
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };

        // Act
        var isValid = CalibrationManager.ValidateParameters(parameters);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidParameters))]
    public void ValidateParameters_WithInvalidParameters_ReturnsFalse(DoseModelParameters invalidParameters)
    {
        // Act
        var isValid = CalibrationManager.ValidateParameters(invalidParameters);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithNullParameters_ReturnsFalse()
    {
        // Act
        var isValid = CalibrationManager.ValidateParameters(null!);

        // Assert
        isValid.Should().BeFalse();
    }

    public static TheoryData<DoseModelParameters> BoundaryParameters => new()
    {
        new DoseModelParameters
        {
            KFactor = 0.00001m,  // Minimum KFactor
            VoltageExponent = 2.0m,  // Minimum exponent
            CalibrationCoefficient = 0.5m,  // Minimum calibration coefficient
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        },
        new DoseModelParameters
        {
            KFactor = 0.1m,  // High KFactor
            VoltageExponent = 3.0m,  // Maximum exponent
            CalibrationCoefficient = 10m,  // Maximum calibration coefficient
            TubeId = "TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        }
    };

    [Theory]
    [MemberData(nameof(BoundaryParameters))]
    public void LoadCalibration_WithBoundaryParameters_AcceptsValidValues(DoseModelParameters parameters)
    {
        // Act
        var act = () => _manager.LoadCalibration(parameters);

        // Assert
        act.Should().NotThrow();
        _manager.IsCalibrated.Should().BeTrue();
    }
}
