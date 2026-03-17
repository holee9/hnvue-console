using System.Diagnostics;
using System.Text;

namespace HnVue.Console.E2E.Tests.Helpers;

/// <summary>
/// E2E test logger with file output and console support.
/// Provides detailed logging for debugging and historical analysis.
/// </summary>
public sealed class E2ELogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly StringBuilder _logBuffer;
    private readonly object _lock = new();
    private bool _disposed;

    private E2ELogger(string testName, string logDirectory)
    {
        _logBuffer = new StringBuilder();

        // Create log directory if not exists
        Directory.CreateDirectory(logDirectory);

        // Create log file with timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logFilePath = Path.Combine(logDirectory, $"{testName}_{timestamp}.log");

        // Write header
        LogHeader(testName);
    }

    /// <summary>
    /// Creates a new logger instance for a test.
    /// </summary>
    public static E2ELogger Create(string testName, string baseLogDirectory = "e2e_logs")
    {
        var solutionRoot = GetSolutionRoot();
        var logDirectory = Path.Combine(solutionRoot, "tests", "e2e", baseLogDirectory);
        return new E2ELogger(testName, logDirectory);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public void LogInfo(string message)
    {
        Log("INFO", message);
    }

    /// <summary>
    /// Logs an action performed by the test.
    /// </summary>
    public void LogAction(string action, string? details = null)
    {
        var message = details != null
            ? $"ACTION: {action} | {details}"
            : $"ACTION: {action}";
        Log("ACTION", message);
    }

    /// <summary>
    /// Logs an element found during UI automation.
    /// </summary>
    public void LogElementFound(string elementType, string identifier, bool found)
    {
        var status = found ? "FOUND" : "NOT FOUND";
        Log("ELEMENT", $"{elementType} | {identifier} | {status}");
    }

    /// <summary>
    /// Logs a click action.
    /// </summary>
    public void LogClick(string elementName, string? automationId = null)
    {
        var details = automationId != null
            ? $"Element: {elementName}, AutomationId: {automationId}"
            : $"Element: {elementName}";
        LogAction("CLICK", details);
    }

    /// <summary>
    /// Logs a navigation action.
    /// </summary>
    public void LogNavigation(string from, string to)
    {
        LogAction("NAVIGATE", $"From: {from} -> To: {to}");
    }

    /// <summary>
    /// Logs an assertion.
    /// </summary>
    public void LogAssertion(string assertion, bool passed, string? expected = null, string? actual = null)
    {
        var status = passed ? "PASSED" : "FAILED";
        var message = $"ASSERTION: {assertion} | {status}";

        if (expected != null)
        {
            message += $" | Expected: {expected}";
        }

        if (actual != null)
        {
            message += $" | Actual: {actual}";
        }

        Log("ASSERT", message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void LogWarning(string message)
    {
        Log("WARNING", message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public void LogError(string message, Exception? exception = null)
    {
        var fullMessage = exception != null
            ? $"{message} | Exception: {exception.GetType().Name} - {exception.Message}"
            : message;

        Log("ERROR", fullMessage);

        if (exception != null && exception.StackTrace != null)
        {
            Log("ERROR", $"StackTrace: {exception.StackTrace}");
        }
    }

    /// <summary>
    /// Logs the start of a test phase.
    /// </summary>
    public void LogPhase(string phaseName)
    {
        var separator = new string('=', 60);
        Log("PHASE", $"\n{separator}\nPHASE: {phaseName}\n{separator}");
    }

    /// <summary>
    /// Logs a screenshot capture event.
    /// </summary>
    public void LogScreenshot(string screenshotPath, string reason)
    {
        Log("SCREENSHOT", $"Path: {screenshotPath} | Reason: {reason}");
    }

    /// <summary>
    /// Logs a wait operation.
    /// </summary>
    public void LogWait(string reason, TimeSpan duration)
    {
        Log("WAIT", $"Reason: {reason} | Duration: {duration.TotalMilliseconds:F0}ms");
    }

    /// <summary>
    /// Logs test completion summary.
    /// </summary>
    public void LogCompletion(bool passed, TimeSpan duration)
    {
        var separator = new string('=', 60);
        var message = $"\n{separator}\nTEST COMPLETED | Status: {(passed ? "PASSED" : "FAILED")} | Duration: {duration.TotalSeconds:F2}s\n{separator}";
        Log("SUMMARY", message);
    }

    /// <summary>
    /// Gets the log file path.
    /// </summary>
    public string LogFilePath => _logFilePath;

    /// <summary>
    /// Gets the current log content as string.
    /// </summary>
    public string GetLogContent()
    {
        lock (_lock)
        {
            return _logBuffer.ToString();
        }
    }

    private void Log(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] {message}";

        lock (_lock)
        {
            // Add to buffer
            _logBuffer.AppendLine(logEntry);

            // Write to file
            try
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore file write errors
            }

            // Also output to console for immediate visibility
            System.Console.WriteLine(logEntry);
        }
    }

    private void LogHeader(string testName)
    {
        var separator = new string('=', 60);
        var header = $@"
{separator}
E2E Test Log
Test: {testName}
Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Machine: {Environment.MachineName}
User: {Environment.UserName}
{separator}
";

        lock (_lock)
        {
            _logBuffer.AppendLine(header);
            try
            {
                File.WriteAllText(_logFilePath, header);
            }
            catch
            {
                // Ignore file write errors
            }
        }
    }

    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();

        while (currentDir != null)
        {
            if (Directory.GetFiles(currentDir, "*.sln").Any())
            {
                return currentDir;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        throw new InvalidOperationException("Could not find solution root directory.");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Write footer
            var footer = $"\n{'=', 60}\nLog ended at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{'=', 60}\n";

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, footer);
                }
                catch
                {
                    // Ignore file write errors
                }
            }
        }
    }
}
