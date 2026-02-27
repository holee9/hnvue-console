using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HnVue.Dicom.Tests.Security;

/// <summary>
/// Implementation of <see cref="ILogCapture"/> that captures log records in memory.
/// Used for PHI audit testing per NFR-SEC-01.
/// </summary>
/// <typeparam name="T">The type the logger is for (category name).</typeparam>
public sealed class LogCapture<T> : ILogCapture<T>
{
    private readonly ConcurrentBag<(LogLevel Level, string Message)> _logs = new();
    private readonly string _categoryName;

    public LogCapture()
    {
        _categoryName = typeof(T).Name;
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null; // Scope not supported for PHI audit
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true; // Capture all log levels for PHI audit
    }

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        if (exception != null)
        {
            message += Environment.NewLine + exception.ToString();
        }

        _logs.Add((logLevel, message));
    }

    /// <inheritdoc/>
    public string GetCapturedLogs()
    {
        return string.Join(Environment.NewLine, _logs.Select(l => $"[{l.Level}] {l.Message}"));
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<(LogLevel Level, string Message)> GetLogEntries()
    {
        return _logs.ToList();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _logs.Clear();
    }
}
