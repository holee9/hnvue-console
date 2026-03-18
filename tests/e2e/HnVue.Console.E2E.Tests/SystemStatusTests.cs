using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for System Status view user journey.
/// User Journey: Navigate to System Status, verify component tiles and refresh.
/// </summary>
public class SystemStatusTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(SystemStatusTests));
        await LaunchApplicationAsync();
        await Task.Delay(1000);

        // Navigate to System Status view using InvokePattern for reliable focus-independent activation
        await InvokeNavigationButtonAsync("NavigateStatusButton", "Status");
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "SystemStatus")]
    public async Task SystemStatus_View_Has_Header()
    {
        await Task.Delay(500);
        var header = await WaitForElementAsync(
            () => FindElementByAutomationId("SystemStatusViewHeader"),
            TimeSpan.FromSeconds(5));
        LogAssertion("System Status header displayed", header != null);
        header.Should().NotBeNull("System Status header should be displayed");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "SystemStatus")]
    public async Task SystemStatus_View_Has_System_Health_Indicator()
    {
        await Task.Delay(500);
        var healthText = await WaitForElementAsync(
            () => FindTextBlockContaining("System Health"),
            TimeSpan.FromSeconds(5));
        LogAssertion("System Health indicator exists", healthText != null);
        healthText.Should().NotBeNull("System Health indicator should be visible");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "SystemStatus")]
    public async Task SystemStatus_View_Has_Refresh_Button()
    {
        await Task.Delay(500);
        var refreshButton = await WaitForElementAsync(
            () => FindButtonByText("Refresh"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Refresh button exists", refreshButton != null);
        refreshButton.Should().NotBeNull("Refresh button should exist in System Status view");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "SystemStatus")]
    public async Task SystemStatus_View_Has_Components_Count()
    {
        await Task.Delay(500);
        var componentsText = await WaitForElementAsync(
            () => FindTextBlockContaining("Components:"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Components count shown in status bar", componentsText != null);
        componentsText.Should().NotBeNull("Components count should be visible in status bar");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "SystemStatus")]
    public async Task Refresh_Button_Is_Clickable()
    {
        await Task.Delay(500);
        var refreshButton = await WaitForElementAsync(
            () => FindButtonByText("Refresh"),
            TimeSpan.FromSeconds(5));
        refreshButton.Should().NotBeNull("Refresh button should exist");

        var action = () => refreshButton!.Click();
        action.Should().NotThrow("Refresh button should be clickable");
        Wait.UntilInputIsProcessed();
        await Task.Delay(500);
    }
}
