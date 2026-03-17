using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using HnVue.Console.E2E.Tests.Helpers;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// Base class for E2E tests providing WPF application lifecycle management.
/// </summary>
public abstract class TestBase : IDisposable
{
    private readonly string _applicationPath;
    private Application? _application;
    private AutomationBase? _automation;
    private Window? _mainWindow;
    private bool _disposed;
    private E2ELogger? _logger;

    /// <summary>
    /// Gets the main window of the application.
    /// </summary>
    protected Window MainWindow
    {
        get
        {
            if (_mainWindow == null)
            {
                throw new InvalidOperationException("Main window not available. Ensure LaunchApplicationAsync has been called.");
            }
            return _mainWindow;
        }
    }

    /// <summary>
    /// Gets the automation object.
    /// </summary>
    protected AutomationBase Automation
    {
        get
        {
            if (_automation == null)
            {
                throw new InvalidOperationException("Automation not available. Ensure LaunchApplicationAsync has been called.");
            }
            return _automation;
        }
    }

    /// <summary>
    /// Gets the logger instance for the current test.
    /// </summary>
    protected E2ELogger Logger => _logger ?? throw new InvalidOperationException("Logger not initialized. Call InitializeLogger first.");

    protected TestBase()
    {
        // Determine the application path relative to test output directory
        var solutionRoot = GetSolutionRoot();

        // Try multiple possible build output locations
        var possiblePaths = new[]
        {
            // New SDK style project output (artifacts directory)
            Path.Combine(solutionRoot, "artifacts", "bin", "HnVue.Console", "Debug", "net8.0-windows", "HnVue.Console.exe"),
            Path.Combine(solutionRoot, "artifacts", "bin", "HnVue.Console", "Release", "net8.0-windows", "HnVue.Console.exe"),
            // Legacy bin directory under project
            Path.Combine(solutionRoot, "src", "HnVue.Console", "bin", "Debug", "net8.0-windows", "HnVue.Console.exe"),
            Path.Combine(solutionRoot, "src", "HnVue.Console", "bin", "Release", "net8.0-windows", "HnVue.Console.exe"),
        };

        _applicationPath = possiblePaths.FirstOrDefault(File.Exists)
            ?? possiblePaths[0]; // Default to first path if none exist (will fail with helpful error)
    }

    /// <summary>
    /// Initializes the logger for the current test.
    /// </summary>
    protected void InitializeLogger(string testName)
    {
        _logger = E2ELogger.Create(testName);
        _logger.LogInfo($"Test initialized. Application path: {_applicationPath}");
    }

    /// <summary>
    /// Launches the WPF application and attaches to its main window.
    /// </summary>
    protected async Task LaunchApplicationAsync()
    {
        _logger?.LogPhase("APPLICATION LAUNCH");

        if (!File.Exists(_applicationPath))
        {
            _logger?.LogError("Application executable not found", new FileNotFoundException(_applicationPath));
            throw new FileNotFoundException(
                $"Application not found at {_applicationPath}. " +
                "Please build the application before running E2E tests."
            );
        }

        _logger?.LogInfo($"Application found at: {_applicationPath}");

        _automation = new UIA3Automation();

        // Configure automation timeouts
        _automation.ConnectionTimeout = TimeSpan.FromSeconds(10);
        _automation.TransactionTimeout = TimeSpan.FromSeconds(10);

        _logger?.LogInfo("Launching application...");

        // Launch the application with explicit working directory to ensure
        // appsettings.json is found correctly
        // Note: appsettings.json has TLS disabled for E2E testing
        var applicationDirectory = Path.GetDirectoryName(_applicationPath);
        _logger?.LogInfo($"Application directory: {applicationDirectory}");
        _logger?.LogInfo($"Current directory: {Directory.GetCurrentDirectory()}");

        var sw = Stopwatch.StartNew();

        // Use ProcessStartInfo to explicitly set the working directory
        var startInfo = new ProcessStartInfo
        {
            FileName = _applicationPath,
            UseShellExecute = false,
            WorkingDirectory = applicationDirectory,  // KEY FIX: Set working directory to app directory
            CreateNoWindow = false
        };

        // Enable E2E mode so the application uses mock services instead of gRPC adapters.
        // This prevents TCP connection timeouts (OS default ~20s) from blocking UI rendering
        // when gRPC server is not running during E2E test execution.
        startInfo.EnvironmentVariables["HNVUE_E2E_TEST"] = "1";

        // Start the process
        var process = Process.Start(startInfo);

        if (process == null)
        {
            throw new InvalidOperationException("Failed to start application process.");
        }

        _logger?.LogInfo($"Process started with ID: {process.Id}");

        // Wait for the process to initialize before attaching FlaUI
        // This prevents NullReferenceException in Application.Attach()
        _logger?.LogWait("Waiting for process initialization", TimeSpan.FromMilliseconds(500));
        await Task.Delay(500);

        // Some additional waiting for the main window to be created
        if (!process.HasExited)
        {
            try
            {
                process.WaitForInputIdle(3000); // Wait up to 3 seconds for idle state
            }
            catch (InvalidOperationException)
            {
                // Process may not have a message loop (console app), ignore
            }
        }

        // Attach FlaUI to the running process
        _application = Application.Attach(process);

        sw.Stop();

        _logger?.LogInfo($"Application launched in {sw.ElapsedMilliseconds:F0}ms (E2E mode enabled)");
        _logger?.LogInfo($"Process ID: {process.Id}");

        // Additional wait for UI to fully load
        _logger?.LogWait("Waiting for UI stabilization", TimeSpan.FromMilliseconds(1500));
        await Task.Delay(1500);

        // Find the main window
        _logger?.LogInfo("Searching for main window...");
        _mainWindow = _application.GetMainWindow(_automation);

        if (_mainWindow == null)
        {
            _logger?.LogError("Failed to find main window after launching application");
            throw new InvalidOperationException("Failed to find main window after launching application.");
        }

        _logger?.LogElementFound("Window", $"MainWindow (Title: {_mainWindow.Title})", true);
        _logger?.LogInfo($"Main window found with title: {_mainWindow.Title}");
    }

    /// <summary>
    /// Finds an element by its automation ID.
    /// </summary>
    protected AutomationElement? FindElementByAutomationId(string automationId)
    {
        return MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }

    /// <summary>
    /// Finds a button by its automation ID (preferred for stability).
    /// Falls back to text search if automation ID is not found.
    /// </summary>
    protected Button? FindButtonByAutomationId(string automationId, string? fallbackText = null)
    {
        // Try automation ID first (most stable)
        var element = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        if (element != null)
        {
            _logger?.LogElementFound("Button", $"AutomationId={automationId}", true);
            return element.AsButton();
        }

        _logger?.LogElementFound("Button", $"AutomationId={automationId}", false);

        // Fallback to text search if provided
        if (fallbackText != null)
        {
            _logger?.LogInfo($"Falling back to text search for: {fallbackText}");
            return FindButtonByText(fallbackText);
        }

        return null;
    }

    /// <summary>
    /// Finds a button by its text content.
    /// </summary>
    protected Button? FindButtonByText(string text)
    {
        var button = MainWindow.FindFirstDescendant(cf => cf.ByText(text))?.AsButton();
        _logger?.LogElementFound("Button", $"Text={text}", button != null);
        return button;
    }

    /// <summary>
    /// Finds a button by partial text match in button content or name property.
    /// </summary>
    protected Button? FindButtonByPartialText(string partialText)
    {
        var buttons = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
        foreach (var button in buttons)
        {
            var btn = button.AsButton();
            if (btn != null && btn.Name?.Contains(partialText, StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger?.LogElementFound("Button", $"PartialText={partialText} (Found: {btn.Name})", true);
                return btn;
            }
        }

        _logger?.LogElementFound("Button", $"PartialText={partialText}", false);
        return null;
    }

    /// <summary>
    /// Clicks a button with logging.
    /// </summary>
    protected void ClickButton(Button button, string buttonName)
    {
        var automationId = button.Properties.AutomationId.ValueOrDefault;
        _logger?.LogClick(buttonName, automationId);

        var sw = Stopwatch.StartNew();
        button.Click();
        sw.Stop();

        _logger?.LogInfo($"Button clicked in {sw.ElapsedMilliseconds:F0}ms");
        Wait.UntilInputIsProcessed();
    }

    /// <summary>
    /// Clicks an automation element with logging.
    /// </summary>
    protected void ClickElement(AutomationElement element, string elementName)
    {
        _logger?.LogClick(elementName, element.Properties.AutomationId.ValueOrDefault);

        var sw = Stopwatch.StartNew();
        element.Click();
        sw.Stop();

        _logger?.LogInfo($"Element clicked in {sw.ElapsedMilliseconds:F0}ms");
        Wait.UntilInputIsProcessed();
    }

    /// <summary>
    /// Finds a text block by partial text match.
    /// </summary>
    protected AutomationElement? FindTextBlockContaining(string partialText)
    {
        var textBlocks = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
        var result = textBlocks.FirstOrDefault(tb => tb.Name.Contains(partialText, StringComparison.OrdinalIgnoreCase));
        _logger?.LogElementFound("TextBlock", $"PartialText={partialText}", result != null);
        return result;
    }

    /// <summary>
    /// Waits for an element to appear within the specified timeout.
    /// </summary>
    protected async Task<AutomationElement?> WaitForElementAsync(
        Func<AutomationElement?> findElement,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;
        var attemptCount = 0;

        _logger?.LogInfo($"Waiting for element... (Timeout: {timeout.Value.TotalSeconds}s)");

        while (DateTime.UtcNow - startTime < timeout)
        {
            attemptCount++;
            var element = findElement();
            if (element != null)
            {
                var elapsed = DateTime.UtcNow - startTime;
                _logger?.LogInfo($"Element found after {attemptCount} attempts ({elapsed.TotalMilliseconds:F0}ms)");
                return element;
            }

            await Task.Delay(200);
        }

        _logger?.LogWarning($"Element not found after {attemptCount} attempts (timeout: {timeout.Value.TotalSeconds}s)");
        return null;
    }

    /// <summary>
    /// Takes a screenshot of the main window for debugging purposes.
    /// </summary>
    protected string CaptureScreenshot(string testName, string reason = "Debug")
    {
        try
        {
            var solutionRoot = GetSolutionRoot();
            var screenshotDir = Path.Combine(solutionRoot, "tests", "e2e", "screenshots", testName);
            Directory.CreateDirectory(screenshotDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var screenshotPath = Path.Combine(screenshotDir, $"{timestamp}_{reason}.png");

            // Capture screenshot using FlaUI
            if (_mainWindow != null && _automation != null)
            {
                var capture = _mainWindow.Capture();
                if (capture != null)
                {
                    // Save the captured image to file
                    capture.Save(screenshotPath);
                }
            }

            _logger?.LogScreenshot(screenshotPath, reason);
            return screenshotPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to capture screenshot", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Logs an assertion result.
    /// </summary>
    protected void LogAssertion(string description, bool passed, string? expected = null, string? actual = null)
    {
        _logger?.LogAssertion(description, passed, expected, actual);

        // Capture screenshot on assertion failure
        if (!passed)
        {
            CaptureScreenshot("AssertionFailure", description);
        }
    }

    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();

        // Walk up the directory tree to find the solution root (contains .sln file)
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

            _logger?.LogInfo("Disposing test resources...");

            try
            {
                _application?.Close();
                _logger?.LogInfo("Application closed");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to close application", ex);
            }

            try
            {
                _application?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to dispose application", ex);
            }

            try
            {
                _automation?.Dispose();
                _logger?.LogInfo("Automation disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to dispose automation", ex);
            }

            _logger?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
