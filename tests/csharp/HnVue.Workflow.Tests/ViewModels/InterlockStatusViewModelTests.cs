namespace HnVue.Workflow.Tests.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using HnVue.Workflow.ViewModels;
using Xunit;

/// <summary>
/// Tests for InterlockStatusViewModel.
/// SPEC-WORKFLOW-001 TASK-413: Interlock Status Display Component
/// </summary>
/// <remarks>
/// @MX:NOTE: TDD test suite for interlock status display
/// Tests cover: 9 interlocks, color coding, status updates, INotifyPropertyChanged
/// </remarks>
public class InterlockStatusViewModelTests
{
    /// <summary>
    /// TEST: ViewModel should initialize with all 9 interlocks.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWith9Interlocks()
    {
        // Act
        var viewModel = new InterlockStatusViewModel();

        // Assert
        Assert.NotNull(viewModel.Interlocks);
        Assert.Equal(9, viewModel.Interlocks.Count);
    }

    /// <summary>
    /// TEST: All interlocks should have names, descriptions, and default status.
    /// </summary>
    [Fact]
    public void Constructor_AllInterlocks_ShouldHaveValidProperties()
    {
        // Act
        var viewModel = new InterlockStatusViewModel();

        // Assert
        foreach (var interlock in viewModel.Interlocks)
        {
            Assert.False(string.IsNullOrWhiteSpace(interlock.Name));
            Assert.False(string.IsNullOrWhiteSpace(interlock.Description));
            // InterlockStatus is a value type (enum), always has a value
        }
    }

    /// <summary>
    /// TEST: Interlock names should match expected safety interlocks.
    /// </summary>
    [Fact]
    public void InterlockNames_ShouldMatchExpectedInterlocks()
    {
        // Arrange
        var expectedNames = new[]
        {
            "Door Interlock",
            "Generator Ready",
            "Tube Temperature",
            "Collimator Safety",
            "Detector Ready",
            "High Voltage State",
            "Anode Cooling",
            "Filament Warmup",
            "Emergency Stop"
        };

        // Act
        var viewModel = new InterlockStatusViewModel();

        // Assert
        Assert.Equal(expectedNames.Length, viewModel.Interlocks.Count);
        for (int i = 0; i < expectedNames.Length; i++)
        {
            Assert.Equal(expectedNames[i], viewModel.Interlocks[i].Name);
        }
    }

    /// <summary>
    /// TEST: All interlocks should have Green status initially (safe state).
    /// </summary>
    [Fact]
    public void Constructor_AllInterlocks_ShouldHaveGreenStatus()
    {
        // Act
        var viewModel = new InterlockStatusViewModel();

        // Assert
        foreach (var interlock in viewModel.Interlocks)
        {
            Assert.Equal(InterlockStatus.Green, interlock.Status);
        }
    }

    /// <summary>
    /// TEST: InterlockInfo should have color based on status.
    /// </summary>
    [Fact]
    public void InterlockInfo_Color_ShouldMatchStatus()
    {
        // Arrange & Act
        var greenInterlock = new InterlockInfo("Test", "Desc", InterlockStatus.Green);
        var redInterlock = new InterlockInfo("Test", "Desc", InterlockStatus.Red);
        var yellowInterlock = new InterlockInfo("Test", "Desc", InterlockStatus.Yellow);

        // Assert
        Assert.Equal("Green", greenInterlock.Color);
        Assert.Equal("Red", redInterlock.Color);
        Assert.Equal("Yellow", yellowInterlock.Color);
    }

    /// <summary>
    /// TEST: UpdateInterlockStatus should change interlock status.
    /// </summary>
    [Fact]
    public void UpdateInterlockStatus_ShouldChangeInterlockStatus()
    {
        // Arrange
        var viewModel = new InterlockStatusViewModel();
        var initialStatus = viewModel.Interlocks[0].Status;

        // Act
        viewModel.UpdateInterlockStatus(0, InterlockStatus.Red);

        // Assert
        Assert.NotEqual(initialStatus, viewModel.Interlocks[0].Status);
        Assert.Equal(InterlockStatus.Red, viewModel.Interlocks[0].Status);
    }

    /// <summary>
    /// TEST: UpdateInterlockStatus should raise PropertyChanged event.
    /// </summary>
    [Fact]
    public void UpdateInterlockStatus_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new InterlockStatusViewModel();
        var interlock = viewModel.Interlocks[0];
        var propertiesChanged = new System.Collections.Generic.List<string>();

        interlock.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                propertiesChanged.Add(e.PropertyName);
            }
        };

        // Act
        viewModel.UpdateInterlockStatus(0, InterlockStatus.Yellow);

        // Assert
        Assert.Contains(nameof(InterlockInfo.Status), propertiesChanged);
        Assert.Contains(nameof(InterlockInfo.Color), propertiesChanged);
    }

    /// <summary>
    /// TEST: UpdateInterlockStatus should update color when status changes.
    /// </summary>
    [Fact]
    public void UpdateInterlockStatus_ShouldUpdateColor()
    {
        // Arrange
        var viewModel = new InterlockStatusViewModel();
        var initialColor = viewModel.Interlocks[0].Color;

        // Act
        viewModel.UpdateInterlockStatus(0, InterlockStatus.Red);

        // Assert
        Assert.NotEqual(initialColor, viewModel.Interlocks[0].Color);
        Assert.Equal("Red", viewModel.Interlocks[0].Color);
    }

    /// <summary>
    /// TEST: ViewModel should implement INotifyPropertyChanged.
    /// </summary>
    [Fact]
    public void ViewModel_ShouldImplementINotifyPropertyChanged()
    {
        // Arrange & Act
        var viewModel = new InterlockStatusViewModel();

        // Assert
        Assert.IsAssignableFrom<INotifyPropertyChanged>(viewModel);
    }

    /// <summary>
    /// TEST: InterlockInfo should implement INotifyPropertyChanged.
    /// </summary>
    [Fact]
    public void InterlockInfo_ShouldImplementINotifyPropertyChanged()
    {
        // Arrange & Act
        var interlock = new InterlockInfo("Test", "Description", InterlockStatus.Green);

        // Assert
        Assert.IsAssignableFrom<INotifyPropertyChanged>(interlock);
    }

    /// <summary>
    /// TEST: Multiple interlocks can be updated independently.
    /// </summary>
    [Fact]
    public void UpdateInterlockStatus_MultipleInterlocks_ShouldUpdateIndependently()
    {
        // Arrange
        var viewModel = new InterlockStatusViewModel();

        // Act
        viewModel.UpdateInterlockStatus(0, InterlockStatus.Red);
        viewModel.UpdateInterlockStatus(4, InterlockStatus.Yellow);
        viewModel.UpdateInterlockStatus(8, InterlockStatus.Red);

        // Assert
        Assert.Equal(InterlockStatus.Red, viewModel.Interlocks[0].Status);
        Assert.Equal(InterlockStatus.Green, viewModel.Interlocks[1].Status);
        Assert.Equal(InterlockStatus.Green, viewModel.Interlocks[2].Status);
        Assert.Equal(InterlockStatus.Green, viewModel.Interlocks[3].Status);
        Assert.Equal(InterlockStatus.Yellow, viewModel.Interlocks[4].Status);
        Assert.Equal(InterlockStatus.Red, viewModel.Interlocks[8].Status);
    }

    /// <summary>
    /// TEST: Invalid index should throw ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void UpdateInterlockStatus_InvalidIndex_ShouldThrowException()
    {
        // Arrange
        var viewModel = new InterlockStatusViewModel();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => viewModel.UpdateInterlockStatus(99, InterlockStatus.Red));
    }

    /// <summary>
    /// TEST: Negative index should throw ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void UpdateInterlockStatus_NegativeIndex_ShouldThrowException()
    {
        // Arrange
        var viewModel = new InterlockStatusViewModel();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => viewModel.UpdateInterlockStatus(-1, InterlockStatus.Red));
    }
}
