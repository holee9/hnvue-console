using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for Acquisition view user journey.
/// User Journey: Navigate to Acquisition, verify panels and controls are present and clickable.
/// </summary>
public class AcquisitionTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(AcquisitionTests));
        await LaunchApplicationAsync();
        await Task.Delay(1000);

        // Navigate to Acquisition view using InvokePattern for reliable focus-independent activation
        await InvokeNavigationButtonAsync("NavigateAcquisitionButton", "Acquisition");
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Acquisition")]
    public async Task Acquisition_View_Has_Header()
    {
        await Task.Delay(500);
        var header = await WaitForElementAsync(
            () => FindElementByAutomationId("AcquisitionViewHeader"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Acquisition view header displayed", header != null);
        header.Should().NotBeNull("Acquisition view header should be displayed");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Acquisition")]
    public async Task Acquisition_View_Has_Start_Preview_Button()
    {
        await Task.Delay(500);
        var startButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Start Preview"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Start Preview button exists", startButton != null);
        startButton.Should().NotBeNull("Start Preview button should exist");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Acquisition")]
    public async Task Acquisition_View_Has_Stop_Preview_Button()
    {
        await Task.Delay(500);
        var stopButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Stop Preview"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Stop Preview button exists", stopButton != null);
        stopButton.Should().NotBeNull("Stop Preview button should exist");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Acquisition")]
    public async Task Acquisition_View_Has_Trigger_Exposure_Button()
    {
        await Task.Delay(500);
        var triggerButton = await WaitForElementAsync(
            () => FindButtonByPartialText("TRIGGER EXPOSURE"),
            TimeSpan.FromSeconds(5));
        LogAssertion("TRIGGER EXPOSURE button exists", triggerButton != null);
        triggerButton.Should().NotBeNull("TRIGGER EXPOSURE button should exist");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Acquisition")]
    public async Task Acquisition_View_Has_AEC_Control_Panel()
    {
        await Task.Delay(500);
        var aecPanel = await WaitForElementAsync(
            () => FindTextBlockContaining("AEC Control"),
            TimeSpan.FromSeconds(5));
        LogAssertion("AEC Control panel exists", aecPanel != null);
        aecPanel.Should().NotBeNull("AEC Control panel should be visible");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Acquisition")]
    public async Task Acquisition_View_Has_Protocol_Selection_Panel()
    {
        await Task.Delay(500);
        var protocolPanel = await WaitForElementAsync(
            () => FindTextBlockContaining("Protocol"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Protocol selection panel exists", protocolPanel != null);
        protocolPanel.Should().NotBeNull("Protocol selection panel should be visible");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Acquisition")]
    public async Task Acquisition_View_Has_No_Preview_Indicator()
    {
        await Task.Delay(500);
        // Before preview is started, "No Preview" text should be visible
        var noPreviewText = await WaitForElementAsync(
            () => FindTextBlockContaining("No Preview"),
            TimeSpan.FromSeconds(5));
        LogAssertion("No Preview indicator visible when preview is not active", noPreviewText != null);
        noPreviewText.Should().NotBeNull("No Preview indicator should be visible when preview is not active");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Acquisition")]
    public async Task Start_Preview_Button_Is_Clickable()
    {
        await Task.Delay(500);
        var startButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Start Preview"),
            TimeSpan.FromSeconds(5));
        startButton.Should().NotBeNull("Start Preview button should exist");

        var action = () => startButton!.Click();
        action.Should().NotThrow("Start Preview button should be clickable");
        Wait.UntilInputIsProcessed();
        await Task.Delay(500);
    }
}
