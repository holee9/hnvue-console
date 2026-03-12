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
    public async Task InitializeAsync()
    {
        await LaunchApplicationAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Application_Starts_And_Shows_Main_Window()
    {
        // Arrange & Act - Application launched in InitializeAsync

        // Assert
        MainWindow.Should().NotBeNull("main window should be displayed");
        MainWindow.Title.Should().Be("HnVue Console", "window title should match");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Main_Window_Has_Navigation_Bar()
    {
        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Find navigation buttons by text
        var patientButton = FindButtonByText("Patient");
        var worklistButton = FindButtonByText("Worklist");
        var acquisitionButton = FindButtonByText("Acquisition");
        var imageReviewButton = FindButtonByText("Image Review");
        var statusButton = FindButtonByText("Status");
        var configButton = FindButtonByText("Config");
        var auditLogButton = FindButtonByText("Audit Log");

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
        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Status bar should contain patient and study info
        var statusBarText = FindTextBlockContaining("Patient:");
        statusBarText.Should().NotBeNull("status bar should show patient info");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Main_Window_Has_Locale_Selector()
    {
        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Locale combo box should be present
        var comboBoxes = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox));

        comboBoxes.Should().NotBeEmpty("locale selector combo box should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ApplicationLaunch")]
    public void Main_Window_Default_View_Is_Patient_Management()
    {
        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Patient Management view should be displayed by default
        var patientManagementHeader = FindTextBlockContaining("Patient Management");
        patientManagementHeader.Should().NotBeNull("Patient Management view should be displayed by default");
    }
}
