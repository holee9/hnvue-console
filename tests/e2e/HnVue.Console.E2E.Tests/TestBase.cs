using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
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
    /// Launches the WPF application and attaches to its main window.
    /// </summary>
    protected async Task LaunchApplicationAsync()
    {
        if (!File.Exists(_applicationPath))
        {
            throw new FileNotFoundException(
                $"Application not found at {_applicationPath}. " +
                "Please build the application before running E2E tests."
            );
        }

        _automation = new UIA3Automation();

        // Configure automation timeouts
        _automation.ConnectionTimeout = TimeSpan.FromSeconds(10);
        _automation.TransactionTimeout = TimeSpan.FromSeconds(10);

        // Launch the application
        _application = Application.Launch(_applicationPath);

        // Wait for the application to start
        await Task.Delay(2000);

        // Find the main window
        _mainWindow = _application.GetMainWindow(_automation);

        if (_mainWindow == null)
        {
            throw new InvalidOperationException("Failed to find main window after launching application.");
        }
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
            return element.AsButton();
        }

        // Fallback to text search if provided
        if (fallbackText != null)
        {
            return FindButtonByText(fallbackText);
        }

        return null;
    }

    /// <summary>
    /// Finds a button by its text content.
    /// </summary>
    protected Button? FindButtonByText(string text)
    {
        return MainWindow.FindFirstDescendant(cf => cf.ByText(text))?.AsButton();
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
                return btn;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds a text block by partial text match.
    /// </summary>
    protected AutomationElement? FindTextBlockContaining(string partialText)
    {
        var textBlocks = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
        return textBlocks.FirstOrDefault(tb => tb.Name.Contains(partialText, StringComparison.OrdinalIgnoreCase));
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

        while (DateTime.UtcNow - startTime < timeout)
        {
            var element = findElement();
            if (element != null)
            {
                return element;
            }

            await Task.Delay(200);
        }

        return null;
    }

    /// <summary>
    /// Takes a screenshot of the main window for debugging purposes.
    /// </summary>
    protected void CaptureScreenshot(string testName)
    {
        try
        {
            var screenshotPath = Path.Combine(
                GetSolutionRoot(),
                "tests",
                "e2e",
                "screenshots",
                $"{testName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);

            // Note: FlaUI screenshot capture requires additional setup
            // This is a placeholder for screenshot functionality
        }
        catch
        {
            // Ignore screenshot failures
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

            try
            {
                _application?.Close();
            }
            catch
            {
                // Ignore close errors
            }

            try
            {
                _application?.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }

            try
            {
                _automation?.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }

            GC.SuppressFinalize(this);
        }
    }
}
