namespace HnVue.Workflow.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// ViewModel for interlock status display component.
/// SPEC-WORKFLOW-001 TASK-413: Interlock Status Display Component
/// </summary>
/// <remarks>
/// @MX:NOTE: Interlock status display - shows 9 safety interlocks with color coding
/// Provides real-time status monitoring for door, generator, tube temperature, etc.
/// </remarks>
public sealed class InterlockStatusViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterlockStatusViewModel"/> class.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Initialize 9 safety interlocks with Green (OK) status
    /// Interlocks cover: door, generator, tube temperature, collimator, detector,
    /// high voltage, anode cooling, filament warmup, emergency stop
    /// </remarks>
    public InterlockStatusViewModel()
    {
        Interlocks = new ObservableCollection<InterlockInfo>
        {
            new InterlockInfo("Door Interlock", "Radiation room door safety sensor", InterlockStatus.Green),
            new InterlockInfo("Generator Ready", "X-ray generator ready state", InterlockStatus.Green),
            new InterlockInfo("Tube Temperature", "X-ray tube thermal status", InterlockStatus.Green),
            new InterlockInfo("Collimator Safety", "Collimator position sensors", InterlockStatus.Green),
            new InterlockInfo("Detector Ready", "Flat panel detector status", InterlockStatus.Green),
            new InterlockInfo("High Voltage State", "HV tank readiness", InterlockStatus.Green),
            new InterlockInfo("Anode Cooling", "Tube anode cooling system", InterlockStatus.Green),
            new InterlockInfo("Filament Warmup", "Cathode filament warmup state", InterlockStatus.Green),
            new InterlockInfo("Emergency Stop", "Emergency stop button status", InterlockStatus.Green)
        };
    }

    /// <summary>
    /// Gets the collection of safety interlocks.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Interlocks collection - observable for UI binding
    /// </remarks>
    public ObservableCollection<InterlockInfo> Interlocks { get; }

    /// <summary>
    /// Updates the status of an interlock by index.
    /// </summary>
    /// <param name="index">The interlock index (0-8).</param>
    /// <param name="status">The new status.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when index is less than 0 or greater than 8.
    /// </exception>
    /// <remarks>
    /// @MX:NOTE: Update interlock status - raises PropertyChanged on InterlockInfo
    /// Used by workflow engine to reflect hardware state changes
    /// </remarks>
    public void UpdateInterlockStatus(int index, InterlockStatus status)
    {
        if (index < 0 || index >= Interlocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be between 0 and 8.");
        }

        Interlocks[index].Status = status;
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Information about a safety interlock.
/// </summary>
/// <remarks>
/// @MX:NOTE: Interlock info - individual interlock state with color coding
/// Implements INotifyPropertyChanged for UI binding updates
/// </remarks>
public sealed class InterlockInfo : INotifyPropertyChanged
{
    private InterlockStatus _status;

    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterlockInfo"/> class.
    /// </summary>
    /// <param name="name">The interlock name.</param>
    /// <param name="description">The interlock description.</param>
    /// <param name="status">The initial status.</param>
    /// <remarks>
    /// @MX:NOTE: Constructor - sets name, description, and initial status
    /// Color is automatically derived from status
    /// </remarks>
    public InterlockInfo(string name, string description, InterlockStatus status)
    {
        Name = name;
        Description = description;
        _status = status;
        Color = GetColorFromStatus(status);
    }

    /// <summary>
    /// Gets the interlock name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the interlock description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets or sets the interlock status.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Status property - updates color when changed
    /// </remarks>
    public InterlockStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                Color = GetColorFromStatus(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Color));
            }
        }
    }

    /// <summary>
    /// Gets the color representing the status.
    /// </summary>
    /// <remarks>
    /// @MX:NOTE: Color property - derived from status for UI binding
    /// Returns "Green", "Yellow", or "Red" for WPF binding
    /// </remarks>
    public string Color { get; private set; }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Gets the color string from the interlock status.
    /// </summary>
    /// <param name="status">The interlock status.</param>
    /// <returns>The color string.</returns>
    /// <remarks>
    /// @MX:NOTE: Color mapping - status to color string for WPF
    /// Green = Normal, Yellow = Warning, Red = Blocked
    /// </remarks>
    private static string GetColorFromStatus(InterlockStatus status)
    {
        return status switch
        {
            InterlockStatus.Green => "Green",
            InterlockStatus.Yellow => "Yellow",
            InterlockStatus.Red => "Red",
            _ => "Green"
        };
    }
}

/// <summary>
/// Represents the status of a safety interlock.
/// </summary>
/// <remarks>
/// @MX:NOTE: Interlock status - safety interlock state enumeration
/// Green: Normal/OK, Yellow: Warning/Check, Red: Blocked/Unsafe
/// </remarks>
public enum InterlockStatus
{
    /// <summary>Interlock is in normal/OK state.</summary>
    Green,

    /// <summary>Interlock is in warning/check state.</summary>
    Yellow,

    /// <summary>Interlock is in blocked/unsafe state.</summary>
    Red
}
