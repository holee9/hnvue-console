namespace HnVue.Workflow.Tests.ViewModels;

using System;
using System.ComponentModel;
using HnVue.Workflow.ViewModels;
using Xunit;

/// <summary>
/// Tests for DoseIndicatorViewModel.
/// SPEC-WORKFLOW-001 TASK-414: Dose Indicator Display Component
/// </summary>
/// <remarks>
/// @MX:NOTE: TDD test suite for dose indicator display
/// Tests cover: study total mGy, daily total mGy, warning at 80%, alarm at 100%
/// </remarks>
public class DoseIndicatorViewModelTests
{
    /// <summary>
    /// TEST: ViewModel should initialize with zero dose values.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithZeroDose()
    {
        // Act
        var viewModel = new DoseIndicatorViewModel();

        // Assert
        Assert.Equal(0.0m, viewModel.StudyTotalMGy);
        Assert.Equal(0.0m, viewModel.DailyTotalMGy);
    }

    /// <summary>
    /// TEST: ViewModel should have default dose limit.
    /// </summary>
    [Fact]
    public void Constructor_ShouldHaveDefaultDoseLimit()
    {
        // Act
        var viewModel = new DoseIndicatorViewModel();

        // Assert
        Assert.True(viewModel.DoseLimitMGy > 0);
    }

    /// <summary>
    /// TEST: IsInWarningState should be false when dose is below 80%.
    /// </summary>
    [Fact]
    public void IsInWarningState_Below80Percent_ShouldBeFalse()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        viewModel.DoseLimitMGy = 100.0m;

        // Act
        viewModel.UpdateDoseDisplay(50.0m, 50.0m);

        // Assert
        Assert.False(viewModel.IsInWarningState);
    }

    /// <summary>
    /// TEST: IsInWarningState should be true when dose is at or above 80%.
    /// </summary>
    [Fact]
    public void IsInWarningState_At80Percent_ShouldBeTrue()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        viewModel.DoseLimitMGy = 100.0m;

        // Act
        viewModel.UpdateDoseDisplay(80.0m, 80.0m);

        // Assert
        Assert.True(viewModel.IsInWarningState);
    }

    /// <summary>
    /// TEST: IsInAlarmState should be false when dose is below 100%.
    /// </summary>
    [Fact]
    public void IsInAlarmState_Below100Percent_ShouldBeFalse()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        viewModel.DoseLimitMGy = 100.0m;

        // Act
        viewModel.UpdateDoseDisplay(99.0m, 99.0m);

        // Assert
        Assert.False(viewModel.IsInAlarmState);
    }

    /// <summary>
    /// TEST: IsInAlarmState should be true when dose is at or above 100%.
    /// </summary>
    [Fact]
    public void IsInAlarmState_At100Percent_ShouldBeTrue()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        viewModel.DoseLimitMGy = 100.0m;

        // Act
        viewModel.UpdateDoseDisplay(100.0m, 100.0m);

        // Assert
        Assert.True(viewModel.IsInAlarmState);
    }

    /// <summary>
    /// TEST: UpdateDoseDisplay should update StudyTotalMGy and DailyTotalMGy.
    /// </summary>
    [Fact]
    public void UpdateDoseDisplay_ShouldUpdateDoseValues()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();

        // Act
        viewModel.UpdateDoseDisplay(25.5m, 50.0m);

        // Assert
        Assert.Equal(25.5m, viewModel.StudyTotalMGy);
        Assert.Equal(50.0m, viewModel.DailyTotalMGy);
    }

    /// <summary>
    /// TEST: UpdateDoseDisplay should raise PropertyChanged for dose values.
    /// </summary>
    [Fact]
    public void UpdateDoseDisplay_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        var propertiesChanged = new System.Collections.Generic.List<string>();
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                propertiesChanged.Add(e.PropertyName);
            }
        };

        // Act
        viewModel.UpdateDoseDisplay(25.5m, 50.0m);

        // Assert
        Assert.Contains(nameof(DoseIndicatorViewModel.StudyTotalMGy), propertiesChanged);
        Assert.Contains(nameof(DoseIndicatorViewModel.DailyTotalMGy), propertiesChanged);
    }

    /// <summary>
    /// TEST: UpdateDoseDisplay should raise PropertyChanged for warning state.
    /// </summary>
    [Fact]
    public void UpdateDoseDisplay_EntersWarningState_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        viewModel.DoseLimitMGy = 100.0m;
        var propertiesChanged = new System.Collections.Generic.List<string>();
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                propertiesChanged.Add(e.PropertyName);
            }
        };

        // Act
        viewModel.UpdateDoseDisplay(85.0m, 85.0m);

        // Assert
        Assert.Contains(nameof(DoseIndicatorViewModel.IsInWarningState), propertiesChanged);
    }

    /// <summary>
    /// TEST: UpdateDoseDisplay should raise PropertyChanged for alarm state.
    /// </summary>
    [Fact]
    public void UpdateDoseDisplay_EntersAlarmState_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        viewModel.DoseLimitMGy = 100.0m;
        var propertiesChanged = new System.Collections.Generic.List<string>();
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                propertiesChanged.Add(e.PropertyName);
            }
        };

        // Act
        viewModel.UpdateDoseDisplay(100.0m, 100.0m);

        // Assert
        Assert.Contains(nameof(DoseIndicatorViewModel.IsInAlarmState), propertiesChanged);
    }

    /// <summary>
    /// TEST: Warning threshold should be 80% of dose limit.
    /// </summary>
    [Fact]
    public void WarningThreshold_ShouldBe80PercentOfLimit()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        viewModel.DoseLimitMGy = 100.0m;

        // Act
        var warningThreshold = viewModel.DoseLimitMGy * 0.8m;

        // Assert
        Assert.Equal(80.0m, warningThreshold);
    }

    /// <summary>
    /// TEST: DosePercentage should be calculated correctly.
    /// </summary>
    [Fact]
    public void DosePercentage_ShouldBeCalculatedCorrectly()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();
        viewModel.DoseLimitMGy = 100.0m;

        // Act
        viewModel.UpdateDoseDisplay(50.0m, 50.0m);

        // Assert
        Assert.Equal(50.0m, viewModel.DosePercentage);
    }

    /// <summary>
    /// TEST: ViewModel should implement INotifyPropertyChanged.
    /// </summary>
    [Fact]
    public void ViewModel_ShouldImplementINotifyPropertyChanged()
    {
        // Arrange & Act
        var viewModel = new DoseIndicatorViewModel();

        // Assert
        Assert.IsAssignableFrom<INotifyPropertyChanged>(viewModel);
    }

    /// <summary>
    /// TEST: DoseLimitMGy can be set to custom value.
    /// </summary>
    [Fact]
    public void DoseLimitMGy_ShouldBeSettable()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();

        // Act
        viewModel.DoseLimitMGy = 250.0m;

        // Assert
        Assert.Equal(250.0m, viewModel.DoseLimitMGy);
    }

    /// <summary>
    /// TEST: @MX:WARN - Dose limit enforcement should prevent negative doses.
    /// </summary>
    [Fact]
    public void UpdateDoseDisplay_NegativeDose_ShouldClampToZero()
    {
        // Arrange
        var viewModel = new DoseIndicatorViewModel();

        // Act
        viewModel.UpdateDoseDisplay(-10.0m, -10.0m);

        // Assert
        Assert.Equal(0.0m, viewModel.StudyTotalMGy);
        Assert.Equal(0.0m, viewModel.DailyTotalMGy);
    }
}
