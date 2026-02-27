using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Tests.Security;

/// <summary>
/// Interface for capturing log output during test execution.
/// Used to audit logs for PHI (Protected Health Information) per NFR-SEC-01.
/// </summary>
public interface ILogCapture : ILogger
{
    /// <summary>
    /// Gets all captured log entries as a single string.
    /// </summary>
    string GetCapturedLogs();

    /// <summary>
    /// Gets all captured log entries as a collection of (LogLevel, Message) tuples.
    /// </summary>
    IReadOnlyCollection<(LogLevel Level, string Message)> GetLogEntries();

    /// <summary>
    /// Clears all captured log entries.
    /// </summary>
    void Clear();
}

/// <summary>
/// Generic version of ILogCapture that implements ILogger[T].
/// </summary>
public interface ILogCapture<T> : ILogCapture, ILogger<T>
{
}
