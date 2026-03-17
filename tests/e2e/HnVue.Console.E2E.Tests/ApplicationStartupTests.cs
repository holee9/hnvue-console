using System.Diagnostics;
using FluentAssertions;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for application startup and main window verification.
/// User Journey: Application Launch -> Main Window Display
/// </summary>
public class ApplicationStartupTests : TestBase, IAsyncLifetime
{
    private readonly Stopwatch _testStopwatch = new();

    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(ApplicationStartupTests));
        _testStopwatch.Start();
        await LaunchApplicationAsync();
    }

    public Task DisposeAsync()
    {
        _testStopwatch.Stop();
        Logger.LogCompletion(true, _testStopwatch.Elapsed);
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Application_Starts_And_Shows_Main_Window()
    {
        Logger.LogPhase("TEST: Application_Starts_And_Shows_Main_Window");

        // Arrange & Act - Application launched in InitializeAsync

        // Assert
        MainWindow.Should().NotBeNull("main window should be displayed");

        var title = MainWindow.Title;
        var titleMatch = title == "HnVue Console";
        LogAssertion("Window title should be 'HnVue Console'", titleMatch, "HnVue Console", title);

        MainWindow.Title.Should().Be("HnVue Console", "window title should match");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Main_Window_Has_Navigation_Bar()
    {
        Logger.LogPhase("TEST: Main_Window_Has_Navigation_Bar");

        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Find navigation buttons by text
        var patientButton = FindButtonByText("Patient");
        LogAssertion("Patient navigation button exists", patientButton != null);

        var worklistButton = FindButtonByText("Worklist");
        LogAssertion("Worklist navigation button exists", worklistButton != null);

        var acquisitionButton = FindButtonByText("Acquisition");
        LogAssertion("Acquisition navigation button exists", acquisitionButton != null);

        var imageReviewButton = FindButtonByText("Image Review");
        LogAssertion("Image Review navigation button exists", imageReviewButton != null);

        var statusButton = FindButtonByText("Status");
        LogAssertion("Status navigation button exists", statusButton != null);

        var configButton = FindButtonByText("Config");
        LogAssertion("Config navigation button exists", configButton != null);

        var auditLogButton = FindButtonByText("Audit Log");
        LogAssertion("Audit Log navigation button exists", auditLogButton != null);

        patientButton.Should().NotBeNull("Patient navigation button should exist");
        worklistButton.Should().NotBeNull("Worklist navigation button should exist");
        acquisitionButton.Should().NotBeNull("Acquisition navigation button should exist");
        imageReviewButton.Should().NotBeNull("Image Review navigation button should exist");
        statusButton.Should().NotBeNull("Status navigation button should exist");
        configButton.Should().NotBeNull("Config navigation button should exist");
        auditLogButton.Should().NotBeNull("Audit Log navigation button should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Main_Window_Has_Status_Bar()
    {
        Logger.LogPhase("TEST: Main_Window_Has_Status_Bar");

        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Status bar should contain patient and study info
        var statusBarText = FindTextBlockContaining("Patient:");
        LogAssertion("Status bar should show patient info", statusBarText != null);
        statusBarText.Should().NotBeNull("status bar should show patient info");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Main_Window_Has_Locale_Selector()
    {
        Logger.LogPhase("TEST: Main_Window_Has_Locale_Selector");

        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Locale combo box should be present
        var comboBoxes = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox));

        var hasComboBoxes = comboBoxes != null && comboBoxes.Any();
        LogAssertion("Locale selector combo box exists", hasComboBoxes);

        comboBoxes.Should().NotBeEmpty("locale selector combo box should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Main_Window_Default_View_Is_Patient_Management()
    {
        Logger.LogPhase("TEST: Main_Window_Default_View_Is_Patient_Management");

        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Patient Management view should be displayed by default
        var patientManagementHeader = FindTextBlockContaining("Patient Management");
        LogAssertion("Patient Management view displayed by default", patientManagementHeader != null);
        patientManagementHeader.Should().NotBeNull("Patient Management view should be displayed by default");
    }
}
