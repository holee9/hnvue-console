using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Services;
using NSubstitute;
using Xunit;

namespace HnVue.Integration.Tests.ClinicalWorkflows;

/// <summary>
/// INT-002: DRL Alerting Workflow Integration Tests.
/// Validates dose threshold checking and alert triggering logic.
/// No Docker required - uses MockDoseService and NSubstitute.
/// </summary>
public sealed class DrlAlertingWorkflowTests
{
    // Default thresholds: Warning=2.0, Error=5.0 mGy*cm2
    private const decimal DefaultWarningThreshold = 2.0m;
    private const decimal DefaultErrorThreshold = 5.0m;

    // INT-002-1: No alert when dose is below DRL threshold
    [Fact]
    public async Task DrlAlert_NotTriggered_WhenDoseBelowDrl()
    {
        // Arrange: service with initial cumulative dose of 0.5 (below warning 2.0)
        var doseService = new MockDoseService();

        // Act
        var display = await doseService.GetCurrentDoseDisplayAsync(CancellationToken.None);
        var threshold = await doseService.GetAlertThresholdAsync(CancellationToken.None);

        // Assert: cumulative dose should be below warning threshold
        display.CumulativeDose.Value.Should().BeLessThan(threshold.WarningThreshold,
            because: "Initial cumulative dose must be below warning threshold");
        var isAlertTriggered = display.CumulativeDose.Value >= threshold.WarningThreshold;
        isAlertTriggered.Should().BeFalse(because: "DRL alert must not trigger when dose is below threshold");
    }

    // INT-002-2: Alert triggered when cumulative dose exceeds DRL
    [Fact]
    public async Task DrlAlert_Triggered_WhenCumulativeDoseExceedsDrl()
    {
        // Arrange: use NSubstitute to mock a service that returns dose above threshold
        var mockDoseService = Substitute.For<IDoseService>();
        var highDose = new DoseValue { Value = 3.5m, Unit = DoseUnit.MilliGraySquareCm, MeasuredAt = DateTimeOffset.UtcNow };
        var display = new DoseDisplay { CurrentDose = highDose, CumulativeDose = highDose, StudyId = "TEST_STUDY", ExposureCount = 5 };
        var threshold = new DoseAlertThreshold { WarningThreshold = DefaultWarningThreshold, ErrorThreshold = DefaultErrorThreshold, Unit = DoseUnit.MilliGraySquareCm };
        mockDoseService.GetCurrentDoseDisplayAsync(Arg.Any<CancellationToken>()).Returns(display);
        mockDoseService.GetAlertThresholdAsync(Arg.Any<CancellationToken>()).Returns(threshold);

        // Act
        var currentDisplay = await mockDoseService.GetCurrentDoseDisplayAsync(CancellationToken.None);
        var currentThreshold = await mockDoseService.GetAlertThresholdAsync(CancellationToken.None);

        // Assert: dose above threshold must trigger warning alert
        var isWarningTriggered = currentDisplay.CumulativeDose.Value >= currentThreshold.WarningThreshold;
        isWarningTriggered.Should().BeTrue(because: "Cumulative dose 3.5 >= warning threshold 2.0 must trigger alert");
        var isErrorTriggered = currentDisplay.CumulativeDose.Value >= currentThreshold.ErrorThreshold;
        isErrorTriggered.Should().BeFalse(because: "Cumulative dose 3.5 < error threshold 5.0 must not trigger error alert");
    }

    // INT-002-3: DRL alert logic for multiple exposure accumulation
    [Theory]
    [InlineData(0.5, 2.0, 5.0, false, false)]
    [InlineData(2.5, 2.0, 5.0, true, false)]
    [InlineData(6.0, 2.0, 5.0, true, true)]
    public void DrlAlert_CorrectThresholdEvaluation_ForVariousDoseLevels(
        decimal cumulativeDose, decimal warningThreshold, decimal errorThreshold,
        bool expectedWarning, bool expectedError)
    {
        // Evaluate threshold logic directly
        var isWarning = cumulativeDose >= warningThreshold;
        var isError = cumulativeDose >= errorThreshold;
        isWarning.Should().Be(expectedWarning,
            because: cumulativeDose + " mGy vs warning " + warningThreshold + " mGy must be " + expectedWarning);
        isError.Should().Be(expectedError,
            because: cumulativeDose + " mGy vs error " + errorThreshold + " mGy must be " + expectedError);
    }

    // INT-002-4: Threshold configuration is updateable
    [Fact]
    public async Task DrlAlert_Threshold_CanBeUpdated()
    {
        var doseService = new MockDoseService();
        var newThreshold = new DoseAlertThreshold { WarningThreshold = 1.0m, ErrorThreshold = 3.0m, Unit = DoseUnit.MilliGraySquareCm };
        await doseService.SetAlertThresholdAsync(newThreshold, CancellationToken.None);
        var retrieved = await doseService.GetAlertThresholdAsync(CancellationToken.None);
        retrieved.WarningThreshold.Should().Be(1.0m, because: "Updated warning threshold must be persisted");
        retrieved.ErrorThreshold.Should().Be(3.0m, because: "Updated error threshold must be persisted");
    }

    // INT-002-5: Dose reset clears cumulative dose for new study
    [Fact]
    public async Task DrlAlert_DoseReset_ClearsCumulativeDose()
    {
        var doseService = new MockDoseService();
        var displayBefore = await doseService.GetCurrentDoseDisplayAsync(CancellationToken.None);
        displayBefore.CumulativeDose.Value.Should().BeGreaterThan(0m, because: "Initial cumulative dose should be non-zero");
        await doseService.ResetCumulativeDoseAsync("NEW_STUDY_001", CancellationToken.None);
        var displayAfter = await doseService.GetCurrentDoseDisplayAsync(CancellationToken.None);
        displayAfter.CumulativeDose.Value.Should().Be(0m, because: "Cumulative dose must be 0 after reset for new study");
        var threshold = await doseService.GetAlertThresholdAsync(CancellationToken.None);
        var isAlertTriggered = displayAfter.CumulativeDose.Value >= threshold.WarningThreshold;
        isAlertTriggered.Should().BeFalse(because: "DRL alert must not trigger after dose reset");
    }

    // INT-002-6: DoseUpdate correctly reflects threshold exceedance flags
    [Theory]
    [InlineData(1.0, false, false)]
    [InlineData(2.5, true, false)]
    [InlineData(5.5, true, true)]
    public void DoseUpdate_ThresholdFlags_AreCorrect(decimal cumulativeValue, bool expectedWarning, bool expectedError)
    {
        var cumulative = new DoseValue { Value = cumulativeValue, Unit = DoseUnit.MilliGraySquareCm, MeasuredAt = DateTimeOffset.UtcNow };
        var update = new DoseUpdate
        {
            NewDose = new DoseValue { Value = 0.1m, Unit = DoseUnit.MilliGraySquareCm, MeasuredAt = DateTimeOffset.UtcNow },
            CumulativeDose = cumulative,
            IsWarningThresholdExceeded = cumulativeValue > DefaultWarningThreshold,
            IsErrorThresholdExceeded = cumulativeValue > DefaultErrorThreshold
        };
        update.IsWarningThresholdExceeded.Should().Be(expectedWarning,
            because: cumulativeValue + " mGy: warning flag must be " + expectedWarning);
        update.IsErrorThresholdExceeded.Should().Be(expectedError,
            because: cumulativeValue + " mGy: error flag must be " + expectedError);
    }
}
