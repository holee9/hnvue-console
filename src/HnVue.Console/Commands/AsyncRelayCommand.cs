using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace HnVue.Console.Commands;

/// <summary>
/// An asynchronous command whose sole purpose is to relay its functionality
/// to other objects by invoking delegates. Supports cancellation and
/// busy state management for UI binding.
/// SPEC-UI-001: MVVM infrastructure for async operations.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isExecuting;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncRelayCommand"/>.
    /// </summary>
    /// <param name="execute">The asynchronous execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    /// <param name="onError">Optional error handler.</param>
    public AsyncRelayCommand(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    /// <summary>
    /// Gets a value indicating whether a command is currently executing.
    /// </summary>
    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                RaiseCanExecuteChanged();
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute is null || _canExecute());
    }

    /// <inheritdoc/>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            Debug.WriteLine("AsyncRelayCommand: Cannot execute command - already executing or CanExecute returned false");
            return;
        }

        // Cancel previous operation if still running
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        IsExecuting = true;

        try
        {
            await _execute(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
            Debug.WriteLine("AsyncRelayCommand: Operation was cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AsyncRelayCommand: Exception during execution: {ex.Message}");
            _onError?.Invoke(ex);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>
    /// Cancels the currently executing command.
    /// </summary>
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
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
/// An asynchronous command whose sole purpose is to relay its functionality
/// to other objects by invoking delegates. Supports cancellation and
/// busy state management for UI binding.
/// SPEC-UI-001: MVVM infrastructure for async operations with parameters.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, CancellationToken, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isExecuting;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncRelayCommand{T}"/>.
    /// </summary>
    /// <param name="execute">The asynchronous execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    /// <param name="onError">Optional error handler.</param>
    public AsyncRelayCommand(
        Func<T?, CancellationToken, Task> execute,
        Func<T?, bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    /// <summary>
    /// Gets a value indicating whether a command is currently executing.
    /// </summary>
    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                RaiseCanExecuteChanged();
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute is null || _canExecute((T?)parameter));
    }

    /// <inheritdoc/>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            Debug.WriteLine($"AsyncRelayCommand<{typeof(T).Name}>: Cannot execute command - already executing or CanExecute returned false");
            return;
        }

        // Cancel previous operation if still running
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        IsExecuting = true;

        try
        {
            await _execute((T?)parameter, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
            Debug.WriteLine($"AsyncRelayCommand<{typeof(T).Name}>: Operation was cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AsyncRelayCommand<{typeof(T).Name}>: Exception during execution: {ex.Message}");
            _onError?.Invoke(ex);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>
    /// Cancels the currently executing command.
    /// </summary>
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
