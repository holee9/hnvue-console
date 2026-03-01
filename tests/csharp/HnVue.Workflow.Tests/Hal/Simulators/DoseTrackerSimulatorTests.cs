namespace HnVue.Workflow.Tests.Hal.Simulators;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Workflow.Hal.Simulators;
using HnVue.Workflow.Interfaces;
using Xunit;

/// <summary>
/// Unit tests for DoseTrackerSimulator.
/// SPEC-WORKFLOW-001 TASK-403: IDoseTracker Simulator implementation
/// </summary>
public class DoseTrackerSimulatorTests
{
    /// <summary>
    /// Test that simulator initializes correctly.
    /// </summary>
    [Fact]
    public void InitializeAsync_SetsInitialState()
    {
        // Arrange & Act
        var simulator = new DoseTrackerSimulator();

        // Assert
        var dose = simulator.GetCumulativeDoseSync();
        dose.StudyId.Should().BeNullOrEmpty();
        dose.TotalDap.Should().Be(0);
        dose.ExposureCount.Should().Be(0);
        dose.IsWithinLimits.Should().BeTrue();
    }

    /// <summary>
    /// Test that recording dose works correctly.
    /// </summary>
    [Fact]
    public async Task RecordDoseAsync_AddsDoseToAccumulator()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        var doseEntry = new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 100.0,
            Esd = 5.0,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        await simulator.RecordDoseAsync(doseEntry, CancellationToken.None);

        // Assert
        var cumulative = simulator.GetCumulativeDoseSync();
        cumulative.StudyId.Should().Be("STUDY-001");
        cumulative.TotalDap.Should().Be(100.0);
        cumulative.ExposureCount.Should().Be(1);
    }

    /// <summary>
    /// Test that multiple doses accumulate correctly.
    /// </summary>
    [Fact]
    public async Task RecordDoseAsync_MultipleDosesAccumulate()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();

        // Act - Record 3 doses
        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 100.0,
            Esd = 5.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 150.0,
            Esd = 7.5,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 200.0,
            Esd = 10.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        // Assert
        var cumulative = simulator.GetCumulativeDoseSync();
        cumulative.TotalDap.Should().Be(450.0);
        cumulative.ExposureCount.Should().Be(3);
    }

    /// <summary>
    /// Test that cumulative dose can be retrieved.
    /// </summary>
    [Fact]
    public async Task GetCumulativeDoseAsync_ReturnsAccumulatedDose()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 100.0,
            Esd = 5.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        // Act
        var cumulative = await simulator.GetCumulativeDoseAsync(CancellationToken.None);

        // Assert
        cumulative.StudyId.Should().Be("STUDY-001");
        cumulative.TotalDap.Should().Be(100.0);
        cumulative.ExposureCount.Should().Be(1);
    }

    /// <summary>
    /// Test that dose limit check works when within limits.
    /// </summary>
    [Fact]
    public async Task IsWithinDoseLimitsAsync_WhenWithinLimitsReturnsTrue()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        simulator.SetDoseLimit(1000.0);

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 500.0,
            Esd = 25.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        var proposedDose = new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 400.0,
            Esd = 20.0,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var isWithinLimits = await simulator.IsWithinDoseLimitsAsync(proposedDose, CancellationToken.None);

        // Assert
        isWithinLimits.Should().BeTrue();
    }

    /// <summary>
    /// Test that dose limit check works when exceeding limits.
    /// </summary>
    [Fact]
    public async Task IsWithinDoseLimitsAsync_WhenExceedingLimitsReturnsFalse()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        simulator.SetDoseLimit(1000.0);

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 700.0,
            Esd = 35.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        var proposedDose = new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 400.0,
            Esd = 20.0,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var isWithinLimits = await simulator.IsWithinDoseLimitsAsync(proposedDose, CancellationToken.None);

        // Assert
        isWithinLimits.Should().BeFalse();
    }

    /// <summary>
    /// Test that dose limit can be set.
    /// </summary>
    [Fact]
    public void SetDoseLimit_UpdatesLimit()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();

        // Act
        simulator.SetDoseLimit(5000.0);

        // Assert
        var dose = simulator.GetCumulativeDoseSync();
        dose.DoseLimit.Should().Be(5000.0);
    }

    /// <summary>
    /// Test that IsWithinLimits reflects current state.
    /// </summary>
    [Fact]
    public async Task GetCumulativeDoseAsync_IsWithinLimitsReflectsCurrentState()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        simulator.SetDoseLimit(1000.0);

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 500.0,
            Esd = 25.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        // Act
        var cumulative = await simulator.GetCumulativeDoseAsync(CancellationToken.None);

        // Assert
        cumulative.IsWithinLimits.Should().BeTrue();
        cumulative.DoseLimit.Should().Be(1000.0);
    }

    /// <summary>
    /// Test that IsWithinLimits is false when limit exceeded.
    /// </summary>
    [Fact]
    public async Task GetCumulativeDoseAsync_WhenLimitExceededIsWithinLimitsIsFalse()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        simulator.SetDoseLimit(500.0);

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 600.0,
            Esd = 30.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        // Act
        var cumulative = await simulator.GetCumulativeDoseAsync(CancellationToken.None);

        // Assert
        cumulative.IsWithinLimits.Should().BeFalse();
    }

    /// <summary>
    /// Test that simulator can be reset.
    /// </summary>
    [Fact]
    public async Task ResetAsync_ClearsAllData()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        simulator.SetDoseLimit(1000.0);

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 100.0,
            Esd = 5.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        // Act
        await simulator.ResetAsync(CancellationToken.None);

        // Assert
        var cumulative = simulator.GetCumulativeDoseSync();
        cumulative.StudyId.Should().BeNullOrEmpty();
        cumulative.TotalDap.Should().Be(0);
        cumulative.ExposureCount.Should().Be(0);
        cumulative.IsWithinLimits.Should().BeTrue();
    }

    /// <summary>
    /// Test that dose entries with different study IDs are tracked separately.
    /// </summary>
    [Fact]
    public async Task RecordDoseAsync_DifferentStudiesTrackedSeparately()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();

        // Act - Record dose for STUDY-001
        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 100.0,
            Esd = 5.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        // Record dose for STUDY-002
        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-002",
            PatientId = "PATIENT-001",
            Dap = 200.0,
            Esd = 10.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        // Assert - Should track the last study
        var cumulative = simulator.GetCumulativeDoseSync();
        cumulative.StudyId.Should().Be("STUDY-002");
        cumulative.TotalDap.Should().Be(200.0);
    }

    /// <summary>
    /// Test that cancellation token works for async operations.
    /// </summary>
    [Fact]
    public async Task RecordDoseAsync_CancellationTokenCancelsOperation()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var doseEntry = new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 100.0,
            Esd = 5.0,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => simulator.RecordDoseAsync(doseEntry, cts.Token));
    }

    /// <summary>
    /// Test that negative DAP values are rejected.
    /// </summary>
    [Fact]
    public async Task RecordDoseAsync_NegativeDapIsRejected()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();
        var doseEntry = new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = -100.0,
            Esd = 5.0,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => simulator.RecordDoseAsync(doseEntry, CancellationToken.None));
        exception.ParamName.Should().Be("doseEntry");
    }

    /// <summary>
    /// Test that dose history is maintained.
    /// </summary>
    [Fact]
    public async Task GetDoseHistory_ReturnsAllRecordedDoses()
    {
        // Arrange
        var simulator = new DoseTrackerSimulator();

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 100.0,
            Esd = 5.0,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        await simulator.RecordDoseAsync(new DoseEntry
        {
            StudyId = "STUDY-001",
            PatientId = "PATIENT-001",
            Dap = 150.0,
            Esd = 7.5,
            Timestamp = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        // Act
        var history = simulator.GetDoseHistory();

        // Assert
        history.Should().HaveCount(2);
        history[0].Dap.Should().Be(100.0);
        history[1].Dap.Should().Be(150.0);
    }
}
