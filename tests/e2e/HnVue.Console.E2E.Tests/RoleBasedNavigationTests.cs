using FluentAssertions;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests verifying role-based navigation menu visibility.
/// SPEC-UI-003: GAP-11-04 — Config/AuditLog visible only for Admin/Service roles.
///
/// Test matrix:
///   Administrator  → Config visible, AuditLog visible
///   Technologist   → Config hidden,  AuditLog hidden
///   Viewer         → Config hidden,  AuditLog hidden
///
/// Uses LoginWindow (HNVUE_E2E_SHOW_LOGIN=1) with MockUserService for all users.
/// All users share password "password123" in MockUserService.
/// </summary>
public class RoleBasedNavigationTests : LoginTestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        InitializeLogger(nameof(RoleBasedNavigationTests));
        await LaunchApplicationAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────
    // Administrator role — should see Config and AuditLog
    // ─────────────────────────────────────────────────────────

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "RBAC")]
    public async Task Admin_Can_See_Config_Button()
    {
        Logger.LogPhase("TEST: Admin_Can_See_Config_Button");

        var mainWindow = await PerformLoginAsync("System Administrator", "password123");
        mainWindow.Should().NotBeNull("Admin login must succeed");
        await Task.Delay(500);

        var configButton = FindButtonByAutomationId("NavigateConfigButton");
        LogAssertion("Config button visible for Administrator", configButton != null);
        configButton.Should().NotBeNull("Config button must be visible for Administrator role");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "RBAC")]
    public async Task Admin_Can_See_AuditLog_Button()
    {
        Logger.LogPhase("TEST: Admin_Can_See_AuditLog_Button");

        var mainWindow = await PerformLoginAsync("System Administrator", "password123");
        mainWindow.Should().NotBeNull("Admin login must succeed");
        await Task.Delay(500);

        var auditLogButton = FindButtonByAutomationId("NavigateAuditLogButton");
        LogAssertion("AuditLog button visible for Administrator", auditLogButton != null);
        auditLogButton.Should().NotBeNull("AuditLog button must be visible for Administrator role");

        RecordTestPassed();
    }

    // ─────────────────────────────────────────────────────────
    // Technologist role — should NOT see Config or AuditLog
    // ─────────────────────────────────────────────────────────

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "RBAC")]
    public async Task Technologist_Cannot_See_Config_Button()
    {
        Logger.LogPhase("TEST: Technologist_Cannot_See_Config_Button");

        var mainWindow = await PerformLoginAsync("Technician Johnson", "password123");
        mainWindow.Should().NotBeNull("Technologist login must succeed");
        await Task.Delay(500);

        var configButton = FindButtonByAutomationId("NavigateConfigButton");
        LogAssertion("Config button hidden for Technologist", configButton == null);
        configButton.Should().BeNull("Config button must be hidden for Technologist role");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "RBAC")]
    public async Task Technologist_Cannot_See_AuditLog_Button()
    {
        Logger.LogPhase("TEST: Technologist_Cannot_See_AuditLog_Button");

        var mainWindow = await PerformLoginAsync("Technician Johnson", "password123");
        mainWindow.Should().NotBeNull("Technologist login must succeed");
        await Task.Delay(500);

        var auditLogButton = FindButtonByAutomationId("NavigateAuditLogButton");
        LogAssertion("AuditLog button hidden for Technologist", auditLogButton == null);
        auditLogButton.Should().BeNull("AuditLog button must be hidden for Technologist role");

        RecordTestPassed();
    }

    // ─────────────────────────────────────────────────────────
    // Viewer role — should NOT see Config or AuditLog
    // ─────────────────────────────────────────────────────────

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "RBAC")]
    public async Task Viewer_Cannot_See_Config_Button()
    {
        Logger.LogPhase("TEST: Viewer_Cannot_See_Config_Button");

        var mainWindow = await PerformLoginAsync("Viewer Jane", "password123");
        mainWindow.Should().NotBeNull("Viewer login must succeed");
        await Task.Delay(500);

        var configButton = FindButtonByAutomationId("NavigateConfigButton");
        LogAssertion("Config button hidden for Viewer", configButton == null);
        configButton.Should().BeNull("Config button must be hidden for Viewer role");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "RBAC")]
    public async Task Viewer_Cannot_See_AuditLog_Button()
    {
        Logger.LogPhase("TEST: Viewer_Cannot_See_AuditLog_Button");

        var mainWindow = await PerformLoginAsync("Viewer Jane", "password123");
        mainWindow.Should().NotBeNull("Viewer login must succeed");
        await Task.Delay(500);

        var auditLogButton = FindButtonByAutomationId("NavigateAuditLogButton");
        LogAssertion("AuditLog button hidden for Viewer", auditLogButton == null);
        auditLogButton.Should().BeNull("AuditLog button must be hidden for Viewer role");

        RecordTestPassed();
    }

    // ─────────────────────────────────────────────────────────
    // Common navigation buttons — visible for ALL roles
    // ─────────────────────────────────────────────────────────

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "RBAC")]
    public async Task Viewer_Can_See_Patient_And_Worklist_Buttons()
    {
        Logger.LogPhase("TEST: Viewer_Can_See_Patient_And_Worklist_Buttons");

        var mainWindow = await PerformLoginAsync("Viewer Jane", "password123");
        mainWindow.Should().NotBeNull("Viewer login must succeed");
        await Task.Delay(500);

        var patientButton = FindButtonByAutomationId("NavigatePatientButton");
        var worklistButton = FindButtonByAutomationId("NavigateWorklistButton");
        var acquisitionButton = FindButtonByAutomationId("NavigateAcquisitionButton");

        LogAssertion("Patient button visible for Viewer", patientButton != null);
        LogAssertion("Worklist button visible for Viewer", worklistButton != null);
        LogAssertion("Acquisition button visible for Viewer", acquisitionButton != null);

        patientButton.Should().NotBeNull("Patient button must be visible regardless of role");
        worklistButton.Should().NotBeNull("Worklist button must be visible regardless of role");
        acquisitionButton.Should().NotBeNull("Acquisition button must be visible regardless of role");

        RecordTestPassed();
    }
}
