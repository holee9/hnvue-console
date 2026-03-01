using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace HnVue.Console.Commands;

/// <summary>
/// A command whose sole purpose is to relay its functionality to other
/// objects by invoking delegates. The default return value for the CanExecute
/// method is 'true'. This class does not allow you to accept command parameters
/// in the Execute and CanExecute callback methods.
/// SPEC-UI-001: MVVM infrastructure.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Initializes a new instance of <see cref="RelayCommand"/>.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        return _canExecute is null || _canExecute();
    }

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            Debug.WriteLine("RelayCommand: Cannot execute command - CanExecute returned false");
            return;
        }

        _execute();
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// A command whose sole purpose is to relay its functionality to other
/// objects by invoking delegates. The default return value for the CanExecute
/// method is 'true'. This class allows you to accept command parameters
/// in the Execute and CanExecute callback methods.
/// SPEC-UI-001: MVVM infrastructure.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    /// <summary>
    /// Initializes a new instance of <see cref="RelayCommand{T}"/>.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        return _canExecute is null || _canExecute((T?)parameter);
    }

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            Debug.WriteLine($"RelayCommand<{typeof(T).Name}>: Cannot execute command - CanExecute returned false");
            return;
        }

        _execute((T?)parameter);
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
