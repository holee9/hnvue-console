using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for Image Review view user journey.
/// User Journey: Navigate to Image Review, verify viewer, measurement tools, QC actions.
/// </summary>
public class ImageReviewTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(ImageReviewTests));
        await LaunchApplicationAsync();
        await Task.Delay(1000);

        // Navigate to Image Review view using InvokePattern for reliable focus-independent activation
        await InvokeNavigationButtonAsync("NavigateImageReviewButton", "Image Review");
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task Diagnostic_Dump_UI_State()
    {
        await Task.Delay(3000); // Wait extra for any async rendering
        var allElements = MainWindow.FindAllDescendants();
        Logger.LogInfo($"=== DIAGNOSTIC: Total elements after ImageReview nav: {allElements.Length} ===");
        var buttons = allElements.Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button).ToArray();
        Logger.LogInfo($"=== DIAGNOSTIC: Total buttons: {buttons.Length} ===");
        foreach (var btn in buttons.Take(40))
        {
            Logger.LogInfo($"  BUTTON Name='{btn.Name}' AutomId='{btn.Properties.AutomationId.ValueOrDefault}'");
        }
        var texts = allElements.Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Text).ToArray();
        Logger.LogInfo($"=== DIAGNOSTIC: Total textblocks: {texts.Length} ===");
        foreach (var txt in texts.Take(20))
        {
            Logger.LogInfo($"  TEXT Name='{txt.Name}'");
        }
        Assert.True(true); // Always pass - diagnostic only
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Header()
    {
        await Task.Delay(500);
        var header = await WaitForElementAsync(
            () => FindElementByAutomationId("ImageReviewViewHeader"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Image Review header displayed", header != null);
        header.Should().NotBeNull("Image Review header should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Measurement_Tools_Panel()
    {
        await Task.Delay(500);
        var measurementPanel = await WaitForElementAsync(
            () => FindTextBlockContaining("Measurement Tools"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Measurement Tools panel exists", measurementPanel != null);
        measurementPanel.Should().NotBeNull("Measurement Tools panel should be visible");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_QC_Panel()
    {
        await Task.Delay(500);
        var qcPanel = await WaitForElementAsync(
            () => FindTextBlockContaining("Quality Control"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Quality Control panel exists", qcPanel != null);
        qcPanel.Should().NotBeNull("Quality Control panel should be visible");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Distance_Tool_Button()
    {
        await Task.Delay(500);
        var distanceButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Distance"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Distance measurement tool button exists", distanceButton != null);
        distanceButton.Should().NotBeNull("Distance tool button should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Angle_Tool_Button()
    {
        await Task.Delay(500);
        var angleButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Angle"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Angle measurement tool button exists", angleButton != null);
        angleButton.Should().NotBeNull("Angle tool button should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Cobb_Angle_Tool_Button()
    {
        await Task.Delay(500);
        var cobbButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Cobb"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Cobb Angle tool button exists", cobbButton != null);
        cobbButton.Should().NotBeNull("Cobb Angle tool button should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Annotation_Tool_Button()
    {
        await Task.Delay(500);
        var annotationButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Annotation"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Annotation tool button exists", annotationButton != null);
        annotationButton.Should().NotBeNull("Annotation tool button should exist");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Accept_Image_Button()
    {
        await Task.Delay(500);
        var acceptButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Accept Image"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Accept Image QC button exists", acceptButton != null);
        acceptButton.Should().NotBeNull("Accept Image button should exist in QC panel");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Reject_Image_Button()
    {
        await Task.Delay(500);
        var rejectButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Reject Image"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Reject Image QC button exists", rejectButton != null);
        rejectButton.Should().NotBeNull("Reject Image button should exist in QC panel");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task ImageReview_View_Has_Reprocess_Button()
    {
        await Task.Delay(500);
        var reprocessButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Reprocess"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Request Reprocess button exists", reprocessButton != null);
        reprocessButton.Should().NotBeNull("Request Reprocess button should exist in QC panel");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "ImageReview")]
    public async Task Distance_Tool_Button_Is_Clickable()
    {
        await Task.Delay(500);
        var distanceButton = await WaitForElementAsync(
            () => FindButtonByPartialText("Distance"),
            TimeSpan.FromSeconds(5));
        distanceButton.Should().NotBeNull("Distance tool button should exist");

        var action = () => distanceButton!.Click();
        action.Should().NotThrow("Distance tool button should be clickable");
        Wait.UntilInputIsProcessed();
    }
}
