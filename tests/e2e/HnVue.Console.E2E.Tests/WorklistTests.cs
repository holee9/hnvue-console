using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for Modality Worklist user journey.
/// User Journey: View worklist, refresh procedures, select procedure.
/// </summary>
public class WorklistTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await LaunchApplicationAsync();

        // Wait for application to fully load
        await Task.Delay(1500);

        // Navigate to Worklist view using AutomationId
        var worklistButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateWorklistButton", "Worklist"),
            TimeSpan.FromSeconds(5));
        worklistButton?.Click();
        await Task.Delay(800); // Allow navigation to complete
        Wait.UntilInputIsProcessed();

        // Verify we are on the correct view
        var worklistHeader = await WaitForElementAsync(() => FindTextBlockContaining("Modality Worklist"), TimeSpan.FromSeconds(3));
        worklistHeader.Should().NotBeNull("should be on Worklist view after navigation");
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Worklist")]
    public async Task Worklist_View_Has_Refresh_Button()
    {
        // Arrange & Act - View initialized in InitializeAsync
        await Task.Delay(500); // Allow UI to settle

        // Assert - Look for Refresh button in Worklist view using AutomationId
        var refreshButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("WorklistRefreshButton", "Refresh"),
            TimeSpan.FromSeconds(5));
        refreshButton.Should().NotBeNull("Refresh button should exist in Worklist view");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Worklist")]
    public async Task Worklist_View_Has_Procedure_DataGrid()
    {
        // Arrange & Act - View initialized in InitializeAsync
        await Task.Delay(500); // Allow UI to settle

        // Assert - DataGrid or Table should be present for worklist items
        // WPF DataGrid may be recognized as DataGrid, Table, or or Custom depending on the automation peer
        var dataGrids = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.DataGrid));
        var tables = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Table));
        var customs = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Custom));

        var hasDataGrid = dataGrids.Length > 0 || tables.Length > 0 || customs.Any(c => c.Name.Contains("DataGrid", StringComparison.OrdinalIgnoreCase) || c.ClassName.Contains("DataGrid", StringComparison.OrdinalIgnoreCase));
        hasDataGrid.Should().BeTrue("worklist DataGrid/Table should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Worklist")]
    public async Task Worklist_View_Has_Status_Bar()
    {
        // Arrange & Act - View initialized in InitializeAsync
        await Task.Delay(500); // Allow UI to settle

        // Assert - Status bar should show procedure count and last refreshed
        var proceduresText = await WaitForElementAsync(() => FindTextBlockContaining("Procedures:"), TimeSpan.FromSeconds(5));
        proceduresText.Should().NotBeNull("status bar should show procedure count");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Worklist")]
    public async Task Refresh_Button_Is_Clickable()
    {
        // Arrange - Use AutomationId
        await Task.Delay(500); // Allow UI to settle
        var refreshButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("WorklistRefreshButton", "Refresh"),
            TimeSpan.FromSeconds(5));
        refreshButton.Should().NotBeNull("Refresh button should exist");

        // Act - Click should not throw
        var action = () => refreshButton!.Click();
        action.Should().NotThrow("Refresh button should be clickable");
        Wait.UntilInputIsProcessed();
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Worklist")]
    public async Task Worklist_DataGrid_Has_Expected_Columns()
    {
        // Arrange & Act - View initialized in InitializeAsync
        await Task.Delay(500); // Allow UI to settle

        // Assert - Check for expected column headers in the DataGrid
        var patientIdHeader = await WaitForElementAsync(() => FindTextBlockContaining("Patient ID"), TimeSpan.FromSeconds(5));
        var patientNameHeader = await WaitForElementAsync(() => FindTextBlockContaining("Patient Name"), TimeSpan.FromSeconds(5));
        var statusHeader = await WaitForElementAsync(() => FindTextBlockContaining("Status"), TimeSpan.FromSeconds(5));

        patientIdHeader.Should().NotBeNull("Patient ID column should exist");
        patientNameHeader.Should().NotBeNull("Patient Name column should exist");
        statusHeader.Should().NotBeNull("Status column should exist");
    }
}
