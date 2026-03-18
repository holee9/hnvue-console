using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for Audit Log view user journey.
/// User Journey: Navigate to Audit Log, verify filter panel, log grid, pagination, and export.
/// </summary>
public class AuditLogTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(AuditLogTests));
        await LaunchApplicationAsync();
        await Task.Delay(1000);

        // Navigate to Audit Log view using InvokePattern for reliable focus-independent activation
        await InvokeNavigationButtonAsync("NavigateAuditLogButton", "Audit Log");
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "AuditLog")]
    public async Task AuditLog_View_Has_Header()
    {
        await Task.Delay(500);
        var header = await WaitForElementAsync(
            () => FindElementByAutomationId("AuditLogViewHeader"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Audit Log header displayed", header != null);
        header.Should().NotBeNull("Audit Log header should be displayed");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "AuditLog")]
    public async Task AuditLog_View_Has_Date_Filter_From()
    {
        await Task.Delay(500);
        var fromLabel = await WaitForElementAsync(
            () => FindTextBlockContaining("From:"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Date filter 'From:' label exists", fromLabel != null);
        fromLabel.Should().NotBeNull("Date filter 'From:' label should be visible");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "AuditLog")]
    public async Task AuditLog_View_Has_Search_Button()
    {
        await Task.Delay(500);
        var searchButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Search"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Search button exists", searchButton != null);
        searchButton.Should().NotBeNull("Search button should exist in Audit Log view");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "AuditLog")]
    public async Task AuditLog_View_Has_Export_Button()
    {
        await Task.Delay(500);
        var exportButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Export"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Export button exists", exportButton != null);
        exportButton.Should().NotBeNull("Export button should exist in Audit Log view");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "AuditLog")]
    public async Task AuditLog_View_Has_Log_DataGrid()
    {
        await Task.Delay(500);
        var dataGrids = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.DataGrid));
        var tables = MainWindow.FindAllDescendants(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Table));

        var hasGrid = dataGrids.Length > 0 || tables.Length > 0;
        LogAssertion("Audit log DataGrid/Table exists", hasGrid);
        hasGrid.Should().BeTrue("audit log DataGrid should be visible");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "AuditLog")]
    public async Task AuditLog_View_Has_Pagination_Controls()
    {
        await Task.Delay(500);
        // Look for Previous/Next page buttons or page indicator
        var prevButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Previous"),
            TimeSpan.FromSeconds(3));
        var nextButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Next"),
            TimeSpan.FromSeconds(3));
        var pageText = await WaitForElementAsync(
            () => FindTextBlockContaining("Page"),
            TimeSpan.FromSeconds(3));

        var hasPagination = prevButton != null || nextButton != null || pageText != null;
        LogAssertion("Pagination controls exist", hasPagination);
        hasPagination.Should().BeTrue("pagination controls should exist in Audit Log view");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "AuditLog")]
    public async Task Search_Button_Is_Clickable()
    {
        await Task.Delay(500);
        var searchButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Search"),
            TimeSpan.FromSeconds(5));
        searchButton.Should().NotBeNull("Search button should exist");

        var action = () => searchButton!.Click();
        action.Should().NotThrow("Search button should be clickable");
        Wait.UntilInputIsProcessed();
        await Task.Delay(500);
    }
}
