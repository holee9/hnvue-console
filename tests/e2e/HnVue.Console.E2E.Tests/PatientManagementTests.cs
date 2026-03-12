using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for Patient Management user journey.
/// User Journey: Search patients, view patient list, register/edit patients.
/// </summary>
public class PatientManagementTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await LaunchApplicationAsync();

        // Wait for application to fully load
        await Task.Delay(1000);

        // Ensure we're on Patient Management view using AutomationId
        var patientButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigatePatientButton", "Patient"),
            TimeSpan.FromSeconds(5));
        patientButton?.Click();
        await Task.Delay(500); // Allow navigation to complete
        Wait.UntilInputIsProcessed();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "PatientManagement")]
    public async Task Patient_View_Has_Search_Controls()
    {
        // Arrange & Act - View initialized in InitializeAsync
        await Task.Delay(500); // Allow UI to settle

        // Assert - Search button and register buttons should exist using AutomationId
        var searchButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("PatientSearchButton", "Search"),
            TimeSpan.FromSeconds(3));
        var registerButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("PatientRegisterButton", "Register"),
            TimeSpan.FromSeconds(3));
        var emergencyButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("PatientEmergencyButton", "Emergency"),
            TimeSpan.FromSeconds(3));

        searchButton.Should().NotBeNull("Search button should exist");
        registerButton.Should().NotBeNull("Register button should exist");
        emergencyButton.Should().NotBeNull("Emergency button should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "PatientManagement")]
    public async Task Patient_View_Has_DataGrid()
    {
        // Arrange & Act - View initialized in InitializeAsync
        await Task.Delay(500); // Allow UI to settle

        // Assert - DataGrid should be present for patient list
        // WPF DataGrid may be recognized as Table or DataGrid control type
        var dataGrids = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.DataGrid));
        var tables = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Table));

        var hasDataGrid = dataGrids.Length > 0 || tables.Length > 0;
        hasDataGrid.Should().BeTrue("patient list DataGrid/Table should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "PatientManagement")]
    public async Task Search_TextBox_Accepts_Input()
    {
        // Arrange - Use AutomationId to find the search text box
        await Task.Delay(500); // Allow UI to settle
        var searchTextBox = FindElementByAutomationId("PatientSearchTextBox");

        searchTextBox.Should().NotBeNull("search text box should exist");

        // Act
        searchTextBox!.AsTextBox().Text = "Test Patient";
        Wait.UntilInputIsProcessed();

        // Assert
        searchTextBox.AsTextBox().Text.Should().Be("Test Patient");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "PatientManagement")]
    public async Task Emergency_Button_Creates_Emergency_Patient()
    {
        // Arrange - Use AutomationId
        await Task.Delay(500); // Allow UI to settle
        var emergencyButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("PatientEmergencyButton", "Emergency"),
            TimeSpan.FromSeconds(3));
        emergencyButton.Should().NotBeNull("Emergency button should exist");

        // Act
        emergencyButton!.Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(1000); // Allow for navigation

        // Assert - Should navigate to Worklist view with emergency patient
        var worklistHeader = await WaitForElementAsync(() => FindTextBlockContaining("Modality Worklist"), TimeSpan.FromSeconds(3));
        worklistHeader.Should().NotBeNull("should navigate to Worklist after emergency registration");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "PatientManagement")]
    public async Task Patient_Status_Bar_Shows_Count()
    {
        // Arrange & Act - View initialized in InitializeAsync
        await Task.Delay(500); // Allow UI to settle

        // Assert - Status bar should show patient count
        var patientCountText = await WaitForElementAsync(() => FindTextBlockContaining("Patients:"), TimeSpan.FromSeconds(3));
        patientCountText.Should().NotBeNull("status bar should show patient count");
    }
}
