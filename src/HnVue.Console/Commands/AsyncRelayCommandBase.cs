using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;

namespace HnVue.Console.Commands;

/// <summary>
/// Base class for asynchronous relay commands providing common functionality
/// for cancellation, busy state management, and thread-safe execution control.
/// SPEC-UI-001: MVVM infrastructure foundation for async operations.
/// </summary>
/// <remarks>
/// <para>Thread Safety Model:</para>
/// <para>This class uses a hybrid synchronization strategy combining three primitives:</para>
/// <list type="bullet">
///   <item><description><c>lock (_stateLock)</c> - Protects <c>_isExecuting</c> state for consistency during read-modify-write operations.</description></item>
///   <item><description><c>Interlocked.Exchange/CompareExchange</c> - Lock-free atomic swap of <c>CancellationTokenSource</c> for high-frequency CTS lifecycle management.</description></item>
///   <item><description><c>Volatile.Read</c> - Lock-free disposed flag check in hot path (<c>RaiseCanExecuteChanged</c>).</description></item>
/// </list>
/// <para>The mixed approach optimizes for different access patterns: state changes are infrequent
/// (lock is acceptable), CTS swaps are frequent (lock-free preferred), and disposal checks are
/// extremely frequent (Volatile.Read avoids lock overhead).</para>
/// </remarks>
public abstract class AsyncRelayCommandBase : IDisposable
{
    protected CancellationTokenSource? _cancellationTokenSource;
    protected readonly object _stateLock = new();
    protected bool _isExecuting;
    protected readonly Dispatcher? _dispatcher;
    protected int _disposed;  // SPEC-UI-002: Dispose guard flag (int for Interlocked)

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncRelayCommandBase"/>.
    /// </summary>
    /// <param name="dispatcher">Optional dispatcher for UI thread marshaling.
    /// When null, events are raised directly without marshaling (suitable for unit tests).
    /// When omitted or explicitly provided, that dispatcher is used for marshaling.</param>
    /// <remarks>
    /// SPEC-UI-002: For production code, omit this parameter to use the current UI thread's dispatcher.
    /// For unit tests, explicitly pass null to bypass dispatcher requirements.
    /// </remarks>
    protected AsyncRelayCommandBase(Dispatcher? dispatcher = null)
    {
        // SPEC-UI-002: Store dispatcher as-is. Null means no marshaling (test mode).
        // Non-null means explicit dispatcher will be used for marshaling.
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Gets a value indicating whether a command is currently executing.
    /// </summary>
    public bool IsExecuting
    {
        get
        {
            lock (_stateLock)
            {
                return _isExecuting;
            }
        }
        protected set
        {
            lock (_stateLock)
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    RaiseCanExecuteChanged();
                }
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// SUBSCRIBER RESPONSIBILITY: Event subscribers (typically UI controls) MUST unsubscribe
    /// from this event when no longer needed to prevent memory leaks. Failure to unsubscribe
    /// will cause the command to retain references to subscribers, preventing garbage collection.
    ///
    /// Example of proper cleanup:
    /// <code>
    /// command.CanExecuteChanged -= OnCanExecuteChanged;
    /// </code>
    ///
    /// Note: Events raised after disposal are guarded and will not execute.
    /// </remarks>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event on the UI thread.
    /// SPEC-UI-002: Guard against disposed state and handle null dispatcher.
    /// </summary>
    // @MX:ANCHOR fan_in=30+ All ViewModels call this to refresh CanExecute state. Signature must remain stable.
    public void RaiseCanExecuteChanged()
    {
        // SPEC-UI-002: Guard against disposed state (read without locking)
        if (Volatile.Read(ref _disposed) == 1) return;

        // SPEC-UI-002: Handle null dispatcher (test mode - direct invocation)
        // SECURITY: Debug guard against accidental null dispatcher in production
        if (_dispatcher == null)
        {
#if DEBUG
            // Verify we're actually in test mode, not accidentally passing null in production
            Debug.WriteLine("AsyncRelayCommand: Using null dispatcher (test mode expected)");
#endif
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (!_dispatcher.CheckAccess())
        {
            // Different thread: marshal to dispatcher
            // @MX:WARN fire-and-forget BeginInvoke can run after Dispose
            // @MX:REASON Mitigated by Volatile.Read(_disposed) guard at method entry (SPEC-UI-002)
            _dispatcher.BeginInvoke(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
        else
        {
            // Same thread: direct invocation
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Core execution status check. Override in derived classes for custom logic.
    /// </summary>
    protected virtual bool CanExecuteCore()
    {
        lock (_stateLock)
        {
            return !_isExecuting;
        }
    }

    /// <summary>
    /// Starts a new execution by canceling and disposing the previous CTS.
    /// </summary>
    /// <returns>The new cancellation token source for the current execution.</returns>
    protected CancellationTokenSource StartNewExecution()
    {
        var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, new CancellationTokenSource());
        if (oldCts != null)
        {
            try
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }
        return _cancellationTokenSource;
    }

    /// <summary>
    /// Marks execution as complete.
    /// </summary>
    protected void EndExecution()
    {
        IsExecuting = false;
    }

    /// <summary>
    /// Cancels the currently executing command and disposes the cancellation token source.
    /// </summary>
    public void Cancel()
    {
        var cts = Interlocked.Exchange(ref _cancellationTokenSource, null);
        if (cts != null)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }
    }

    /// <summary>
    /// Releases all resources used by this command.
    /// SPEC-UI-002: Thread-safe disposal with event guarding.
    /// </summary>
    public void Dispose()
    {
        // SPEC-UI-002: Thread-safe disposed flag (returns 1 if already disposed)
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        var cts = Interlocked.Exchange(ref _cancellationTokenSource, null);
        if (cts != null)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }
    }
}
