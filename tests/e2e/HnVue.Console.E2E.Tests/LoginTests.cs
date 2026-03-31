using System.Diagnostics;
using FluentAssertions;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for the login screen.
/// SPEC-UI-003: GAP-11-01 Login screen UI verification.
/// Uses HNVUE_E2E_TEST=1 (mock services) + HNVUE_E2E_SHOW_LOGIN=1 (show LoginWindow).
/// </summary>
public class LoginTests : LoginTestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(LoginTests));
        await LaunchApplicationAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Login")]
    public void LoginWindow_Is_Displayed_On_Startup()
    {
        Logger.LogPhase("TEST: LoginWindow_Is_Displayed_On_Startup");

        // Assert — MainWindow here is actually the LoginWindow (first window)
        MainWindow.Should().NotBeNull("LoginWindow should appear on startup");
        var title = MainWindow.Title;
        LogAssertion("Login window title contains 'Login'", title.Contains("Login"), "contains Login", title);
        title.Should().Contain("Login", "window title should indicate login screen");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Login")]
    public void LoginWindow_Has_Username_And_Password_Fields()
    {
        Logger.LogPhase("TEST: LoginWindow_Has_Username_And_Password_Fields");

        var usernameBox = FindElementByAutomationId("UsernameTextBox");
        var passwordBox = FindElementByAutomationId("PasswordBox");
        var loginButton = FindButtonByAutomationId("LoginButton");

        LogAssertion("Username field exists", usernameBox != null);
        LogAssertion("Password field exists", passwordBox != null);
        LogAssertion("Login button exists", loginButton != null);

        usernameBox.Should().NotBeNull("username field must be present");
        passwordBox.Should().NotBeNull("password field must be present");
        loginButton.Should().NotBeNull("login button must be present");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Login")]
    public async Task Login_With_Valid_Credentials_Opens_MainWindow()
    {
        Logger.LogPhase("TEST: Login_With_Valid_Credentials_Opens_MainWindow");

        // Arrange — type credentials into LoginWindow
        var usernameBox = await WaitForElementAsync(() => FindElementByAutomationId("UsernameTextBox"));
        usernameBox.Should().NotBeNull("username field must be present");

        usernameBox!.AsTextBox().Text = "System Administrator";
        await Task.Delay(200);

        // Fill password via keyboard
        var passwordBox = FindElementByAutomationId("PasswordBox");
        passwordBox.Should().NotBeNull("password field must be present");
        passwordBox!.Click();
        await Task.Delay(100);
        FlaUI.Core.Input.Keyboard.Type("password123");
        await Task.Delay(200);

        // Act — click Login
        var loginButton = FindButtonByAutomationId("LoginButton", "Login");
        loginButton.Should().NotBeNull("Login button must be clickable");
        ClickButton(loginButton!.AsButton(), "Login");

        // Wait for MainWindow to appear (LoginWindow closes, MainWindow opens)
        await Task.Delay(2000);

        // Assert — MainWindow should now be the active window
        var mainWindow = await WaitForMainWindowAsync();
        mainWindow.Should().NotBeNull("MainWindow should appear after successful login");

        var title = mainWindow?.Title ?? "";
        LogAssertion("Main window title is 'HnVue Console'", title == "HnVue Console", "HnVue Console", title);
        title.Should().Be("HnVue Console", "MainWindow title should be displayed after login");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Login")]
    public async Task Login_With_Wrong_Password_Shows_Error()
    {
        Logger.LogPhase("TEST: Login_With_Wrong_Password_Shows_Error");

        // Arrange
        var usernameBox = await WaitForElementAsync(() => FindElementByAutomationId("UsernameTextBox"));
        usernameBox!.AsTextBox().Text = "System Administrator";
        await Task.Delay(200);

        var passwordBox = FindElementByAutomationId("PasswordBox");
        passwordBox!.Click();
        FlaUI.Core.Input.Keyboard.Type("wrongpassword");
        await Task.Delay(200);

        // Act
        var loginButton = FindButtonByAutomationId("LoginButton", "Login");
        ClickButton(loginButton!.AsButton(), "Login");
        await Task.Delay(500);

        // Assert — LoginWindow stays, error message visible
        var errorText = await WaitForElementAsync(
            () => FindElementByAutomationId("LoginErrorText"),
            TimeSpan.FromSeconds(3));

        LogAssertion("Error message displayed", errorText != null);
        errorText.Should().NotBeNull("error message should appear for wrong password");

        var errorMsg = errorText?.Name ?? "";
        LogAssertion("Error text is not empty", !string.IsNullOrEmpty(errorMsg));

        RecordTestPassed();
    }
}

/// <summary>
/// Base class for login tests — sets HNVUE_E2E_SHOW_LOGIN=1 so LoginWindow appears
/// even though mock services (HNVUE_E2E_TEST=1) are still used.
/// </summary>
public abstract class LoginTestBase : TestBase
{
    protected override void ConfigureEnvironmentVariables(ProcessStartInfo startInfo)
    {
        startInfo.EnvironmentVariables["HNVUE_E2E_SHOW_LOGIN"] = "1";
    }

    /// <summary>
    /// Waits for a new top-level window whose title is "HnVue Console" (MainWindow),
    /// which appears after successful login when LoginWindow closes.
    /// Updates MainWindow reference via SwitchMainWindow so subsequent FindElement calls work.
    /// </summary>
    protected async Task<FlaUI.Core.AutomationElements.Window?> WaitForMainWindowAsync(
        int timeoutMs = 8000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var desktop = Automation.GetDesktop();
                var windows = desktop.FindAllChildren();
                var mainWin = windows.FirstOrDefault(w => w.Name == "HnVue Console");
                if (mainWin != null)
                {
                    var win = mainWin.AsWindow();
                    SwitchMainWindow(win);
                    return win;
                }
            }
            catch
            {
                // Continue polling
            }
            await Task.Delay(300);
        }
        return null;
    }

    /// <summary>
    /// Performs login via the LoginWindow UI (types username + password, clicks Login),
    /// waits for MainWindow to appear, and switches the active window reference.
    /// Returns the MainWindow on success, null on failure.
    /// </summary>
    protected async Task<FlaUI.Core.AutomationElements.Window?> PerformLoginAsync(
        string username, string password)
    {
        var usernameBox = await WaitForElementAsync(
            () => FindElementByAutomationId("UsernameTextBox"),
            TimeSpan.FromSeconds(5));
        if (usernameBox == null) return null;

        usernameBox.AsTextBox().Text = username;
        await Task.Delay(150);

        var passwordBox = FindElementByAutomationId("PasswordBox");
        if (passwordBox == null) return null;

        passwordBox.Click();
        await Task.Delay(100);
        FlaUI.Core.Input.Keyboard.Type(password);
        await Task.Delay(150);

        var loginButton = FindButtonByAutomationId("LoginButton", "Login");
        if (loginButton == null) return null;

        ClickButton(loginButton.AsButton(), "Login");

        // Wait for LoginWindow to close and MainWindow to open
        return await WaitForMainWindowAsync(timeoutMs: 6000);
    }
}
