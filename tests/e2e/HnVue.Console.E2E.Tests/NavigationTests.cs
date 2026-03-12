using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for main navigation between views.
/// User Journey: Navigate between Patient, Worklist, Acquisition, etc.
/// </summary>
public class NavigationTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await LaunchApplicationAsync();
        await Task.Delay(1000); // Wait for application to fully load
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_Patient_View()
    {
        // Arrange - Use AutomationId for stable element location
        var patientButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigatePatientButton", "Patient"),
            TimeSpan.FromSeconds(5));
        patientButton.Should().NotBeNull("Patient button should exist");

        // Act
        patientButton!.Click();
        await Task.Delay(800); // Allow navigation to complete and render
        Wait.UntilInputIsProcessed();

        // Assert - Use AutomationId for reliable finding
        var patientHeader = await WaitForElementAsync(() => FindElementByAutomationId("PatientViewHeader"), TimeSpan.FromSeconds(5));
        patientHeader.Should().NotBeNull("Patient Management view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_Worklist_View()
    {
        // Arrange - Use AutomationId for stable element location
        var worklistButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateWorklistButton", "Worklist"),
            TimeSpan.FromSeconds(5));
        worklistButton.Should().NotBeNull("Worklist button should exist");

        // Act
        worklistButton!.Click();
        await Task.Delay(800); // Allow navigation to complete and render
        Wait.UntilInputIsProcessed();

        // Assert - Use AutomationId for reliable finding
        var worklistHeader = await WaitForElementAsync(() => FindElementByAutomationId("WorklistViewHeader"), TimeSpan.FromSeconds(5));
        worklistHeader.Should().NotBeNull("Modality Worklist view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_System_Status_View()
    {
        // Arrange - Use AutomationId for stable element location
        var statusButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateStatusButton", "Status"),
            TimeSpan.FromSeconds(5));
        statusButton.Should().NotBeNull("Status button should exist");

        // Act
        statusButton!.Click();
        await Task.Delay(800); // Allow navigation to complete and render
        Wait.UntilInputIsProcessed();

        // Assert - Use AutomationId for reliable finding
        var statusHeader = await WaitForElementAsync(() => FindElementByAutomationId("SystemStatusViewHeader"), TimeSpan.FromSeconds(5));
        statusHeader.Should().NotBeNull("System Status view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_Configuration_View()
    {
        // Arrange - Use AutomationId for stable element location
        var configButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateConfigButton", "Config"),
            TimeSpan.FromSeconds(5));
        configButton.Should().NotBeNull("Config button should exist");

        // Act
        configButton!.Click();
        await Task.Delay(800); // Allow navigation to complete and render
        Wait.UntilInputIsProcessed();

        // Assert - Use AutomationId for reliable finding
        var configHeader = await WaitForElementAsync(() => FindElementByAutomationId("ConfigurationViewHeader"), TimeSpan.FromSeconds(5));
        configHeader.Should().NotBeNull("System Configuration view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_Audit_Log_View()
    {
        // Arrange - Use AutomationId for stable element location
        var auditLogButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateAuditLogButton", "Audit Log"),
            TimeSpan.FromSeconds(5));
        auditLogButton.Should().NotBeNull("Audit Log button should exist");

        // Act
        auditLogButton!.Click();
        await Task.Delay(800); // Allow navigation to complete and render
        Wait.UntilInputIsProcessed();

        // Assert - Use AutomationId for reliable finding
        var auditLogHeader = await WaitForElementAsync(() => FindElementByAutomationId("AuditLogViewHeader"), TimeSpan.FromSeconds(5));
        auditLogHeader.Should().NotBeNull("Audit Log view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_Between_Multiple_Views_Sequentially()
    {
        // Arrange & Act & Assert - Navigate through multiple views using AutomationId

        // Patient
        var patientButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigatePatientButton", "Patient"),
            TimeSpan.FromSeconds(5));
        patientButton!.Click();
        await Task.Delay(600);
        Wait.UntilInputIsProcessed();
        (await WaitForElementAsync(() => FindElementByAutomationId("PatientViewHeader"), TimeSpan.FromSeconds(5))).Should().NotBeNull();

        // Worklist
        var worklistButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateWorklistButton", "Worklist"),
            TimeSpan.FromSeconds(5));
        worklistButton!.Click();
        await Task.Delay(600);
        Wait.UntilInputIsProcessed();
        (await WaitForElementAsync(() => FindElementByAutomationId("WorklistViewHeader"), TimeSpan.FromSeconds(5))).Should().NotBeNull();

        // Status
        var statusButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateStatusButton", "Status"),
            TimeSpan.FromSeconds(5));
        statusButton!.Click();
        await Task.Delay(600);
        Wait.UntilInputIsProcessed();
        (await WaitForElementAsync(() => FindElementByAutomationId("SystemStatusViewHeader"), TimeSpan.FromSeconds(5))).Should().NotBeNull();

        // Back to Patient
        patientButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigatePatientButton", "Patient"),
            TimeSpan.FromSeconds(5));
        patientButton!.Click();
        await Task.Delay(600);
        Wait.UntilInputIsProcessed();
        (await WaitForElementAsync(() => FindElementByAutomationId("PatientViewHeader"), TimeSpan.FromSeconds(5))).Should().NotBeNull();
    }
}
