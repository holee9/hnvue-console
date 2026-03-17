using System.Diagnostics;
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
    private readonly Stopwatch _testStopwatch = new();

    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(NavigationTests));
        _testStopwatch.Start();
        await LaunchApplicationAsync();
        await Task.Delay(1000); // Wait for application to fully load
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
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_Patient_View()
    {
        Logger.LogPhase("TEST: Navigate_To_Patient_View");

        // Arrange - Find button and wait for it
        var patientButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigatePatientButton", "Patient"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Patient button exists", patientButton != null);
        patientButton.Should().NotBeNull("Patient button should exist");

        // Act - Click using AsButton conversion
        Logger.LogNavigation("Unknown", "Patient View");
        var button = patientButton!.AsButton();
        button.Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(800); // Allow navigation to complete and render

        // Assert - Use AutomationId for reliable finding
        var patientHeader = await WaitForElementAsync(() => FindElementByAutomationId("PatientViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("Patient Management view displayed", patientHeader != null);
        patientHeader.Should().NotBeNull("Patient Management view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_Worklist_View()
    {
        Logger.LogPhase("TEST: Navigate_To_Worklist_View");

        // Arrange - Find button and wait for it
        var worklistButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateWorklistButton", "Worklist"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Worklist button exists", worklistButton != null);
        worklistButton.Should().NotBeNull("Worklist button should exist");

        // Act
        Logger.LogNavigation("Unknown", "Worklist View");
        worklistButton!.AsButton().Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(800);

        // Assert - Use AutomationId for reliable finding
        var worklistHeader = await WaitForElementAsync(() => FindElementByAutomationId("WorklistViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("Modality Worklist view displayed", worklistHeader != null);
        worklistHeader.Should().NotBeNull("Modality Worklist view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_System_Status_View()
    {
        Logger.LogPhase("TEST: Navigate_To_System_Status_View");

        // Arrange - Find button and wait for it
        var statusButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateStatusButton", "Status"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Status button exists", statusButton != null);
        statusButton.Should().NotBeNull("Status button should exist");

        // Act
        Logger.LogNavigation("Unknown", "System Status View");
        statusButton!.AsButton().Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(800);

        // Assert - Use AutomationId for reliable finding
        var statusHeader = await WaitForElementAsync(() => FindElementByAutomationId("SystemStatusViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("System Status view displayed", statusHeader != null);
        statusHeader.Should().NotBeNull("System Status view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_Configuration_View()
    {
        Logger.LogPhase("TEST: Navigate_To_Configuration_View");

        // Arrange - Find button and wait for it
        var configButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateConfigButton", "Config"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Config button exists", configButton != null);
        configButton.Should().NotBeNull("Config button should exist");

        // Act
        Logger.LogNavigation("Unknown", "Configuration View");
        configButton!.AsButton().Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(800);

        // Assert - Use AutomationId for reliable finding
        var configHeader = await WaitForElementAsync(() => FindElementByAutomationId("ConfigurationViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("System Configuration view displayed", configHeader != null);
        configHeader.Should().NotBeNull("System Configuration view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_To_Audit_Log_View()
    {
        Logger.LogPhase("TEST: Navigate_To_Audit_Log_View");

        // Arrange - Find button and wait for it
        var auditLogButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateAuditLogButton", "Audit Log"),
            TimeSpan.FromSeconds(5));
        LogAssertion("Audit Log button exists", auditLogButton != null);
        auditLogButton.Should().NotBeNull("Audit Log button should exist");

        // Act
        Logger.LogNavigation("Unknown", "Audit Log View");
        auditLogButton!.AsButton().Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(800);

        // Assert - Use AutomationId for reliable finding
        var auditLogHeader = await WaitForElementAsync(() => FindElementByAutomationId("AuditLogViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("Audit Log view displayed", auditLogHeader != null);
        auditLogHeader.Should().NotBeNull("Audit Log view should be displayed");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "Navigation")]
    public async Task Navigate_Between_Multiple_Views_Sequentially()
    {
        Logger.LogPhase("TEST: Navigate_Between_Multiple_Views_Sequentially");

        // Arrange & Act & Assert - Navigate through multiple views using AutomationId

        // Patient
        Logger.LogNavigation("Sequential", "Patient View");
        var patientButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigatePatientButton", "Patient"),
            TimeSpan.FromSeconds(5));
        patientButton!.AsButton().Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(600);
        var patientHeader = await WaitForElementAsync(() => FindElementByAutomationId("PatientViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("Patient view displayed", patientHeader != null);

        // Worklist
        Logger.LogNavigation("Patient View", "Worklist View");
        var worklistButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateWorklistButton", "Worklist"),
            TimeSpan.FromSeconds(5));
        worklistButton!.AsButton().Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(600);
        var worklistHeader = await WaitForElementAsync(() => FindElementByAutomationId("WorklistViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("Worklist view displayed", worklistHeader != null);

        // Status
        Logger.LogNavigation("Worklist View", "System Status View");
        var statusButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigateStatusButton", "Status"),
            TimeSpan.FromSeconds(5));
        statusButton!.AsButton().Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(600);
        var statusHeader = await WaitForElementAsync(() => FindElementByAutomationId("SystemStatusViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("System Status view displayed", statusHeader != null);

        // Back to Patient
        Logger.LogNavigation("System Status View", "Patient View");
        patientButton = await WaitForElementAsync(
            () => FindButtonByAutomationId("NavigatePatientButton", "Patient"),
            TimeSpan.FromSeconds(5));
        patientButton!.AsButton().Click();
        Wait.UntilInputIsProcessed();
        await Task.Delay(600);
        patientHeader = await WaitForElementAsync(() => FindElementByAutomationId("PatientViewHeader"), TimeSpan.FromSeconds(5));
        LogAssertion("Patient view displayed again", patientHeader != null);
    }
}
