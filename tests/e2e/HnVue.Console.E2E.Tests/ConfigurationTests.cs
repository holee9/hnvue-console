using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for Configuration view user journey.
/// User Journey: Navigate to Configuration, verify tabs, settings, and save/refresh buttons.
/// </summary>
public class ConfigurationTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(ConfigurationTests));
        await LaunchApplicationAsync();
        await Task.Delay(1000);

        // Navigate to Configuration view using InvokePattern for reliable focus-independent activation
        await InvokeNavigationButtonAsync("NavigateConfigButton", "Config");
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Configuration")]
    public async Task Configuration_View_Has_Header()
    {
        await Task.Delay(500);
        var header = await WaitForElementAsync(
            () => FindElementByAutomationId("ConfigurationViewHeader"),
            TimeSpan.FromSeconds(5));
        LogAssertion("System Configuration header displayed", header != null);
        header.Should().NotBeNull("System Configuration header should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Configuration")]
    public async Task Configuration_View_Has_User_Role_Display()
    {
        await Task.Delay(500);
        var roleText = await WaitForElementAsync(
            () => FindTextBlockContaining("Current Role"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Current Role display exists", roleText != null);
        roleText.Should().NotBeNull("Current Role display should be visible");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Configuration")]
    public async Task Configuration_View_Has_TabControl()
    {
        await Task.Delay(500);
        // TabItem headers are exposed as ControlType.TabItem in WPF UIA, not as TextBlock
        var tabItems = await WaitForElementAsync(
            () =>
            {
                var items = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
                return items.Length > 0 ? items[0] : null;
            },
            TimeSpan.FromSeconds(5));

        // Fallback: look for tab header text via TextBlock (may work in some WPF versions)
        var calibrationText = tabItems == null
            ? await WaitForElementAsync(() => FindTextBlockContaining("Calibration"), TimeSpan.FromSeconds(3))
            : null;

        var hasTab = tabItems != null || calibrationText != null;
        LogAssertion("TabControl with configuration sections exists", hasTab);
        hasTab.Should().BeTrue("configuration view should have tab sections (Calibration, Network, etc.)");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Configuration")]
    public async Task Configuration_View_Has_Save_Button()
    {
        await Task.Delay(500);
        var saveButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Save"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Save button exists", saveButton != null);
        saveButton.Should().NotBeNull("Save button should exist in Configuration view");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Configuration")]
    public async Task Configuration_View_Has_Refresh_Button()
    {
        await Task.Delay(500);
        var refreshButton = await WaitForElementAsync(
            () => FindButtonByText("Refresh"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Refresh button exists", refreshButton != null);
        refreshButton.Should().NotBeNull("Refresh button should exist in Configuration view");
    }
}
