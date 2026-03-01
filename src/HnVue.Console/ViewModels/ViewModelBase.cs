using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Base class for all ViewModels implementing INotifyPropertyChanged.
/// Provides SetProperty helper for change notification.
/// SPEC-UI-001: MVVM infrastructure foundation.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the property value and raises PropertyChanged if the value changed.
    /// </summary>
    /// <typeparam name="T">Type of the property.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">New value.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns>True if the value changed, false otherwise.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises PropertyChanged for multiple properties (useful for computed properties).
    /// </summary>
    /// <param name="propertyNames">Names of properties that changed.</param>
    protected void RaisePropertiesChanged(params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// Debug helper to trace property changes.
    /// </summary>
    /// <param name="propertyName">Name of the property.</param>
    /// <param name="value">New value.</param>
    [Conditional("DEBUG")]
    protected void LogPropertyChange(string propertyName, object? value)
    {
        Debug.WriteLine($"PropertyChanged: {propertyName} = {value ?? "null"}");
    }
}
