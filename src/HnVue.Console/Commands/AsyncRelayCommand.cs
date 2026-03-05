using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;

namespace HnVue.Console.Commands;

/// <summary>
/// An asynchronous command whose sole purpose is to relay its functionality
/// to other objects by invoking delegates. Supports cancellation and
/// busy state management for UI binding.
/// SPEC-UI-001: MVVM infrastructure for async operations.
/// </summary>
public class AsyncRelayCommand : AsyncRelayCommandBase, ICommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onError;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncRelayCommand"/>.
    /// </summary>
    /// <param name="execute">The asynchronous execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    /// <param name="onError">Optional error handler.</param>
    /// <param name="dispatcher">Optional dispatcher for UI thread marshaling.
    /// SPEC-UI-002: When null, events are raised directly (test mode).
    /// When omitted, CurrentDispatcher is captured automatically.</param>
    public AsyncRelayCommand(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onError = null,
        Dispatcher? dispatcher = null) : base(dispatcher)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        if (!CanExecuteCore()) return false;
        return _canExecute is null || _canExecute();
    }

    /// <summary>
    /// Core execution logic. Override in derived classes for custom behavior.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.
    /// IMPORTANT: This token is only valid during the execution of this method.
    /// Do NOT store this token or pass it to long-running operations that may outlive
    /// the command execution. The token will be canceled when the command is disposed.</param>
    protected virtual async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _execute(cancellationToken);
    }

    /// <inheritdoc/>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            Debug.WriteLine("AsyncRelayCommand: Cannot execute command - already executing or CanExecute returned false");
            return;
        }

        var localCts = StartNewExecution();
        IsExecuting = true;

        try
        {
            await ExecuteAsync(localCts.Token);
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
            // SPEC-UI-002: Clean up CTS BEFORE marking execution as complete
            var currentCts = Interlocked.CompareExchange(ref _cancellationTokenSource, null, localCts);
            if (currentCts == localCts)
            {
                localCts.Dispose();
            }

            EndExecution();
        }
    }
}

/// <summary>
/// An asynchronous command whose sole purpose is to relay its functionality
/// to other objects by invoking delegates. Supports cancellation and
/// busy state management for UI binding.
/// SPEC-UI-001: MVVM infrastructure for async operations with parameters.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public class AsyncRelayCommand<T> : AsyncRelayCommandBase, ICommand
{
    private readonly Func<T?, CancellationToken, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private readonly Action<Exception>? _onError;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncRelayCommand{T}"/>.
    /// </summary>
    /// <param name="execute">The asynchronous execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    /// <param name="onError">Optional error handler.</param>
    /// <param name="dispatcher">Optional dispatcher for UI thread marshaling.
    /// SPEC-UI-002: When null, events are raised directly (test mode).
    /// When omitted, CurrentDispatcher is captured automatically.</param>
    public AsyncRelayCommand(
        Func<T?, CancellationToken, Task> execute,
        Func<T?, bool>? canExecute = null,
        Action<Exception>? onError = null,
        Dispatcher? dispatcher = null) : base(dispatcher)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        if (!CanExecuteCore()) return false;
        return _canExecute is null || _canExecute((T?)parameter);
    }

    /// <summary>
    /// Core execution logic. Override in derived classes for custom behavior.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.
    /// IMPORTANT: This token is only valid during the execution of this method.
    /// Do NOT store this token or pass it to long-running operations that may outlive
    /// the command execution. The token will be canceled when the command is disposed.</param>
    protected virtual async Task ExecuteAsync(T? parameter, CancellationToken cancellationToken)
    {
        await _execute(parameter, cancellationToken);
    }

    /// <inheritdoc/>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            Debug.WriteLine($"AsyncRelayCommand<{typeof(T).Name}>: Cannot execute command - already executing or CanExecute returned false");
            return;
        }

        var localCts = StartNewExecution();
        IsExecuting = true;

        try
        {
            await ExecuteAsync((T?)parameter, localCts.Token);
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
            // SPEC-UI-002: Clean up CTS BEFORE marking execution as complete
            var currentCts = Interlocked.CompareExchange(ref _cancellationTokenSource, null, localCts);
            if (currentCts == localCts)
            {
                localCts.Dispose();
            }

            EndExecution();
        }
    }
}
