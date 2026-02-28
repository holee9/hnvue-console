using FluentAssertions;
using HnVue.Dicom.Rdsr;
using HnVue.Dose.Calculation;
using HnVue.Dose.Interfaces;
using HnVue.Dose.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HnVue.Dose.Tests.Calculation;

/// <summary>
/// Unit tests for IDoseCalculator implementations.
/// SPEC-DOSE-001 FR-DOSE-01, Section 4.1.1 DAP Calculation Methodology.
/// </summary>
public class DapCalculatorTests
{
    [Fact]
    public void Calculate_WithValidParameters_ReturnsCalculationResult()
    {
        // Arrange
        var calculator = CreateCalculator();
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
        var result = calculator.Calculate(parameters, null);

        // Assert
        result.Should().NotBeNull();
        result.CalculatedDapGyCm2.Should().BeGreaterThan(0);
        result.DoseSource.Should().Be(DoseSource.Calculated);
        result.MeasuredDapGyCm2.Should().BeNull();
        result.FieldAreaCm2.Should().BeGreaterThan(0);
        result.AirKermaMgy.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_WithMeasuredDap_ReturnsMeasuredDoseSource()
    {
        // Arrange
        var calculator = CreateCalculator();
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
        var measuredDap = 0.0145m;

        // Act
        var result = calculator.Calculate(parameters, measuredDap);

        // Assert
        result.DoseSource.Should().Be(DoseSource.Measured);
        result.MeasuredDapGyCm2.Should().Be(measuredDap);
        result.CalculatedDapGyCm2.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_WithVariousParameters_ReturnsValidDap()
    {
        // Test with various parameter combinations
        var testCases = new[]
        {
            (kVp: 50m, mAs: 5m, sid: 1000m, width: 200m, height: 200m),
            (kVp: 80m, mAs: 10m, sid: 1000m, width: 300m, height: 400m),
            (kVp: 120m, mAs: 20m, sid: 1200m, width: 350m, height: 430m),
            (kVp: 150m, mAs: 50m, sid: 1500m, width: 400m, height: 400m)
        };

        foreach (var (kVp, mAs, sid, width, height) in testCases)
        {
            // Arrange
            var calculator = CreateCalculator();
            var parameters = new ExposureParameters
            {
                KvpValue = kVp,
                MasValue = mAs,
                FilterMaterial = "AL",
                FilterThicknessMm = 2.5m,
                SidMm = sid,
                FieldWidthMm = width,
                FieldHeightMm = height,
                TimestampUtc = DateTime.UtcNow
            };

            // Act
            var result = calculator.Calculate(parameters, null);

            // Assert
            result.CalculatedDapGyCm2.Should().BeGreaterThan(0);
            result.FieldAreaCm2.Should().BeGreaterThan(0);
            result.AirKermaMgy.Should().BeGreaterThan(0);

            // Verify field area calculation
            var expectedArea = (width / 10m) * (height / 10m); // mm² to cm²
            result.FieldAreaCm2.Should().Be(expectedArea);
        }
    }

    [Fact]
    public void Calculate_WithNullParameters_ThrowsArgumentNullException()
    {
        // Arrange
        var calculator = CreateCalculator();

        // Act
        var act = () => calculator.Calculate(null!, null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Calculate_DapFormula_KairTimesFieldArea()
    {
        // Arrange
        var calculator = CreateCalculator();
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
        var result = calculator.Calculate(parameters, null);

        // Assert: DAP = K_air × A_field
        // Verify the relationship between DAP, air kerma, and field area
        var expectedDap = result.AirKermaMgy * result.FieldAreaCm2 / 1000m; // mGy·cm² to Gy·cm²
        var difference = Math.Abs(result.CalculatedDapGyCm2 - expectedDap);
        difference.Should().BeLessOrEqualTo(0.0001m);
    }

    [Fact]
    public void Calculate_WithDifferentFieldSizes_ReturnsProportionalDap()
    {
        // Arrange
        var calculator = CreateCalculator();
        var baseParameters = new ExposureParameters
        {
            KvpValue = 80m,
            MasValue = 10m,
            FilterMaterial = "AL",
            FilterThicknessMm = 2.5m,
            SidMm = 1000m,
            FieldWidthMm = 200m,
            FieldHeightMm = 200m,
            TimestampUtc = DateTime.UtcNow
        };

        // Act
        var result1 = calculator.Calculate(baseParameters, null);
        var result2 = calculator.Calculate(
            baseParameters with { FieldWidthMm = 400m, FieldHeightMm = 400m }, null);

        // Assert: DAP should be proportional to field area
        // Area ratio = (400×400) / (200×200) = 4
        var areaRatio = result2.FieldAreaCm2 / result1.FieldAreaCm2;
        var dapRatio = result2.CalculatedDapGyCm2 / result1.CalculatedDapGyCm2;

        var ratioDifference = Math.Abs(dapRatio - areaRatio);
        ratioDifference.Should().BeLessOrEqualTo(0.05m); // ±5% tolerance
    }

    [Fact]
    public void Calculate_WithDifferentKvp_ReturnsNonLinearDap()
    {
        // Arrange
        var calculator = CreateCalculator();
        var baseParameters = new ExposureParameters
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
        var result80kV = calculator.Calculate(baseParameters, null);
        var result100kV = calculator.Calculate(
            baseParameters with { KvpValue = 100m }, null);

        // Assert: DAP should increase with kVp^n (n ≈ 2.5)
        // Ratio should be approximately (100/80)^2.5 ≈ 1.74
        result100kV.CalculatedDapGyCm2.Should().BeGreaterThan(result80kV.CalculatedDapGyCm2);
    }

    [Fact]
    public void Calculate_CompletesWithin200ms()
    {
        // Arrange
        var calculator = CreateCalculator();
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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = calculator.Calculate(parameters, null);
        stopwatch.Stop();

        // Assert: Must complete within 200ms per NFR-DOSE-01
        stopwatch.ElapsedMilliseconds.Should().BeLessOrEqualTo(200);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Calculate_ThreadSafe_MultipleConcurrentCalls()
    {
        // Arrange
        var calculator = CreateCalculator();
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
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            return calculator.Calculate(parameters, null);
        })).ToArray();

        await Task.WhenAll(tasks);
        var results = tasks.Select(t => t.Result).ToList();

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.CalculatedDapGyCm2 > 0);
    }

    // Helper method to create calculator implementation
    private static IDoseCalculator CreateCalculator()
    {
        var logger = new NullLogger<DapCalculator>();
        var calibrationManager = CreateCalibrationManager();
        return new DapCalculator(calibrationManager, logger);
    }

    private static CalibrationManager CreateCalibrationManager()
    {
        var logger = new NullLogger<CalibrationManager>();
        var manager = new CalibrationManager(logger);
        var parameters = new DoseModelParameters
        {
            KFactor = DoseTestData.DapReference.KFactor,
            VoltageExponent = DoseTestData.DapReference.VoltageExponent,
            CalibrationCoefficient = DoseTestData.DapReference.CalibrationCoefficient,
            TubeId = "TEST-TUBE-001",
            CalibrationDateUtc = DateTime.UtcNow
        };
        manager.LoadCalibration(parameters);
        return manager;
    }
}
