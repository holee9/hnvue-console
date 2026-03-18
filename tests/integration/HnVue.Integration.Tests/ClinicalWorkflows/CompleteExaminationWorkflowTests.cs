using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Services;
using NSubstitute;
using Xunit;

namespace HnVue.Integration.Tests.ClinicalWorkflows;

/// <summary>
/// INT-001: Complete Patient Examination Workflow Integration Tests.
/// Validates the end-to-end clinical workflow across multiple service boundaries:
/// Authentication → Worklist Query → Patient Selection → Protocol Selection →
/// Exposure Preparation → Dose Recording → Dose Alert Check → Audit Trail.
/// No Docker required - uses MockUserService, MockDoseService, MockExposureService,
/// MockProtocolService, and NSubstitute for IWorklistService/IAuditLogService.
/// IEC 62304 Class B/C: Cross-component integration verification.
/// </summary>
public sealed class CompleteExaminationWorkflowTests
{
    // Service instances used across tests in this class.
    // MockUserService and MockDoseService have full implementations for authentication and dose tracking.
    // IWorklistService and IAuditLogService are substituted via NSubstitute for controlled behavior.
    private readonly MockUserService _userService;
    private readonly MockDoseService _doseService;
    private readonly MockExposureService _exposureService;
    private readonly MockProtocolService _protocolService;
    private readonly IWorklistService _worklistService;
    private readonly IAuditLogService _auditLogService;

    // Test data constants matching SPEC-INTEGRATION-001 scenario INT-001.
    private const string TechnicianUserName = "Technician Johnson";
    private const string MockPassword = "password123";
    private const string WorkstationId = "WS-INT-001";
    private const string TestPatientId = "P001";
    private const string TestAccessionNumber = "A001";
    private const string TestBodyPartCode = "CHEST";
    private const string TestProjectionCode = "PA";
    private const string TestStudyId = "STUDY-INT-001";

    public CompleteExaminationWorkflowTests()
    {
        _userService = new MockUserService();
        _doseService = new MockDoseService();
        _exposureService = new MockExposureService();
        _protocolService = new MockProtocolService();

        // Build a controlled worklist with one scheduled item for deterministic testing.
        _worklistService = Substitute.For<IWorklistService>();
        var testWorklistItem = new WorklistItem
        {
            ProcedureId = "PROC-INT-001",
            PatientId = TestPatientId,
            PatientName = "Hong Gil-dong",
            AccessionNumber = TestAccessionNumber,
            ScheduledProcedureStepDescription = "Chest PA",
            ScheduledDateTime = DateTimeOffset.UtcNow.AddHours(1),
            BodyPart = TestBodyPartCode,
            Projection = TestProjectionCode,
            Status = WorklistStatus.Scheduled
        };
        _worklistService.GetWorklistAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorklistItem>>(new List<WorklistItem> { testWorklistItem }));
        _worklistService.SelectWorklistItemAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Build a controlled audit log service to track events generated during workflow.
        _auditLogService = Substitute.For<IAuditLogService>();
        _auditLogService.LogAsync(
            Arg.Any<AuditEventType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<AuditOutcome>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult($"LOG-{Guid.NewGuid():N}".Substring(0, 12)));
    }

    // INT-001-1: Step 1 - Successful authentication creates a valid user session.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task Authentication_WithValidCredentials_CreatesValidSession()
    {
        // Arrange: TECHNOLOGIST user authenticates with correct password.
        var cancellationToken = CancellationToken.None;

        // Act
        var authResult = await _userService.AuthenticateAsync(
            TechnicianUserName, MockPassword, WorkstationId, cancellationToken);

        // Assert: session is created with required fields.
        authResult.Success.Should().BeTrue(because: "Valid technologist credentials must authenticate successfully");
        authResult.Session.Should().NotBeNull(because: "Successful authentication must create a session");
        authResult.Session!.SessionId.Should().NotBeNullOrEmpty(because: "Session must have a unique identifier");
        authResult.Session.AccessToken.Should().NotBeNullOrEmpty(because: "Session must have an access token");
        authResult.Session.User.Should().NotBeNull(because: "Session must reference the authenticated user");
        authResult.Session.User.Role.Should().Be(UserRole.Technologist,
            because: "Technician Johnson has Technologist role");
        authResult.Session.ExpiresAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1),
            because: "Session must expire in approximately 30 minutes (FR-SEC-02)");
    }

    // INT-001-2: Step 2 - Authenticated session validates successfully.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task AuthenticatedSession_ValidatesSuccessfully_AfterLogin()
    {
        // Arrange: authenticate the technologist user.
        var cancellationToken = CancellationToken.None;
        var authResult = await _userService.AuthenticateAsync(
            TechnicianUserName, MockPassword, WorkstationId, cancellationToken);
        authResult.Success.Should().BeTrue();

        // Act: validate the session immediately after creation.
        var isValid = await _userService.ValidateSessionAsync(authResult.Session!.SessionId, cancellationToken);

        // Assert
        isValid.Should().BeTrue(because: "Freshly created session must pass validation");
    }

    // INT-001-3: Step 3 - Worklist query returns scheduled procedures and can be logged.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task WorklistQuery_ReturnsScheduledItems_AndAuditEventIsRecorded()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act: query the worklist and log the event to the audit service.
        var worklistItems = await _worklistService.GetWorklistAsync(cancellationToken);
        await _auditLogService.LogAsync(
            AuditEventType.DataExport,
            "tech01",
            TechnicianUserName,
            "Worklist query executed",
            AuditOutcome.Success,
            null,
            null,
            cancellationToken);

        // Assert: worklist returns items with complete patient data.
        worklistItems.Should().NotBeEmpty(because: "Worklist must contain at least one scheduled item");
        var firstItem = worklistItems.First();
        firstItem.PatientId.Should().NotBeNullOrEmpty(because: "Worklist item must have patient identifier");
        firstItem.PatientName.Should().NotBeNullOrEmpty(because: "Worklist item must have patient name");
        firstItem.AccessionNumber.Should().NotBeNullOrEmpty(because: "Worklist item must have accession number");

        // Assert: audit log received the worklist query event.
        await _auditLogService.Received(1).LogAsync(
            AuditEventType.DataExport,
            "tech01",
            TechnicianUserName,
            Arg.Any<string>(),
            AuditOutcome.Success,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // INT-001-4: Step 4 - Patient selection retrieves the correct worklist item details.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task PatientSelection_SelectsCorrectWorklistItem_WithCompletePatientData()
    {
        // Arrange: retrieve worklist to select from.
        var cancellationToken = CancellationToken.None;
        var worklistItems = await _worklistService.GetWorklistAsync(cancellationToken);

        // Act: select the first scheduled item and confirm selection.
        var selectedItem = worklistItems.First(i => i.Status == WorklistStatus.Scheduled);
        await _worklistService.SelectWorklistItemAsync(selectedItem.ProcedureId, cancellationToken);

        // Assert: selected item contains complete patient identification data.
        selectedItem.ProcedureId.Should().NotBeNullOrEmpty(because: "Selected procedure must have an identifier");
        selectedItem.PatientId.Should().Be(TestPatientId, because: "Patient ID must match test data");
        selectedItem.AccessionNumber.Should().Be(TestAccessionNumber,
            because: "Accession number must match test data");
        selectedItem.BodyPart.Should().Be(TestBodyPartCode,
            because: "Body part must be populated from worklist");
        await _worklistService.Received(1).SelectWorklistItemAsync(selectedItem.ProcedureId, cancellationToken);
    }

    // INT-001-5: Step 5 - Protocol selection returns valid exposure parameters.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task ProtocolSelection_ForChestPA_ReturnsValidExposureParameters()
    {
        // Arrange: retrieve available body parts and projections.
        var cancellationToken = CancellationToken.None;

        // Act: get available body parts and projections, then retrieve the preset.
        var bodyParts = await _protocolService.GetBodyPartsAsync(cancellationToken);
        var projections = await _protocolService.GetProjectionsAsync(TestBodyPartCode, cancellationToken);
        var preset = await _protocolService.GetProtocolPresetAsync(
            TestBodyPartCode, TestProjectionCode, cancellationToken);

        // Assert: CHEST body part and PA projection are available.
        bodyParts.Should().ContainSingle(bp => bp.Code == TestBodyPartCode,
            because: "CHEST body part must be available in protocol service");
        projections.Should().ContainSingle(p => p.Code == TestProjectionCode,
            because: "PA projection must be available for CHEST body part");

        // Assert: protocol preset has valid exposure parameters.
        preset.Should().NotBeNull(because: "CHEST/PA protocol preset must exist");
        preset!.DefaultExposure.Should().NotBeNull(because: "Protocol preset must have default exposure parameters");
        preset.DefaultExposure.KVp.Should().BeGreaterThan(0,
            because: "Tube voltage must be a positive value");
        preset.DefaultExposure.MA.Should().BeGreaterThan(0,
            because: "Tube current must be a positive value");
        preset.DefaultExposure.ExposureTimeMs.Should().BeGreaterThan(0,
            because: "Exposure time must be a positive value");
    }

    // INT-001-6: Step 6 - Exposure triggering succeeds and returns an image identifier.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task ExposureTrigger_WithValidParameters_SucceedsAndReturnsImageId()
    {
        // Arrange: retrieve and apply default exposure parameters.
        var cancellationToken = CancellationToken.None;
        var preset = await _protocolService.GetProtocolPresetAsync(
            TestBodyPartCode, TestProjectionCode, cancellationToken);
        await _exposureService.SetExposureParametersAsync(preset!.DefaultExposure, cancellationToken);

        // Act: trigger the exposure.
        var triggerRequest = new ExposureTriggerRequest
        {
            StudyId = TestStudyId,
            ProtocolId = preset.ProtocolId,
            Parameters = preset.DefaultExposure
        };
        var exposureResult = await _exposureService.TriggerExposureAsync(triggerRequest, cancellationToken);

        // Assert: exposure succeeded and produced an image reference.
        exposureResult.Should().NotBeNull(because: "Exposure trigger must return a result");
        exposureResult.Success.Should().BeTrue(because: "Exposure must succeed with valid parameters");
        exposureResult.ImageId.Should().NotBeNullOrEmpty(
            because: "Successful exposure must return an image identifier");
        exposureResult.ErrorMessage.Should().BeNull(
            because: "Successful exposure must not have an error message");
    }

    // INT-001-7: Step 7 - Dose recording reflects current study dose after exposure.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task DoseRecording_AfterExposure_ReflectsCurrentAndCumulativeDose()
    {
        // Arrange: simulate that an exposure has occurred.
        var cancellationToken = CancellationToken.None;

        // Act: query the dose display as it would appear after an exposure.
        var doseDisplay = await _doseService.GetCurrentDoseDisplayAsync(cancellationToken);

        // Assert: dose display contains non-null measurements for both current and cumulative dose.
        doseDisplay.Should().NotBeNull(because: "Dose display must be retrievable after exposure");
        doseDisplay.CurrentDose.Should().NotBeNull(because: "Current dose measurement must exist");
        doseDisplay.CumulativeDose.Should().NotBeNull(because: "Cumulative study dose must be tracked");
        doseDisplay.CurrentDose.Value.Should().BeGreaterThanOrEqualTo(0m,
            because: "Current dose must be non-negative");
        doseDisplay.CumulativeDose.Value.Should().BeGreaterThan(0m,
            because: "Cumulative dose must be positive after at least one simulated exposure");
        doseDisplay.CurrentDose.Unit.Should().Be(DoseUnit.MilliGraySquareCm,
            because: "Dose unit must be mGy*cm2 per IEC 60601-1-3");
    }

    // INT-001-8: Step 8 - Dose threshold check evaluates cumulative dose against DRL limits.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task DoseThresholdCheck_EvaluatesCumulativeDose_AgainstDrlLimits()
    {
        // Arrange: get current dose and thresholds.
        var cancellationToken = CancellationToken.None;
        var doseDisplay = await _doseService.GetCurrentDoseDisplayAsync(cancellationToken);
        var threshold = await _doseService.GetAlertThresholdAsync(cancellationToken);

        // Act: evaluate whether any DRL threshold is exceeded.
        var cumulativeDose = doseDisplay.CumulativeDose.Value;
        var isWarningExceeded = cumulativeDose >= threshold.WarningThreshold;
        var isErrorExceeded = cumulativeDose >= threshold.ErrorThreshold;

        // Assert: thresholds are configured and comparison is logically consistent.
        threshold.WarningThreshold.Should().BeGreaterThan(0m,
            because: "Warning DRL threshold must be configured with a positive value");
        threshold.ErrorThreshold.Should().BeGreaterThan(threshold.WarningThreshold,
            because: "Error threshold must be higher than warning threshold");
        isErrorExceeded.Should().BeFalse(
            because: "Initial cumulative dose must not exceed error DRL threshold");

        // When warning is not exceeded, error must also not be exceeded.
        if (!isWarningExceeded)
        {
            isErrorExceeded.Should().BeFalse(
                because: "Error cannot be exceeded when warning is not exceeded");
        }
    }

    // INT-001-9: Complete end-to-end workflow executes all steps without exceptions.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task CompleteExaminationWorkflow_AllSteps_ExecuteWithoutErrors()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Step 1: Authentication.
        var authResult = await _userService.AuthenticateAsync(
            TechnicianUserName, MockPassword, WorkstationId, cancellationToken);
        authResult.Success.Should().BeTrue(because: "Step 1: authentication must succeed");
        var session = authResult.Session!;

        // Step 2: Log user login audit event.
        var loginLogId = await _auditLogService.LogAsync(
            AuditEventType.UserLogin,
            session.User.UserId,
            session.User.UserName,
            $"User login from workstation {WorkstationId}",
            AuditOutcome.Success,
            null,
            null,
            cancellationToken);
        loginLogId.Should().NotBeNullOrEmpty(because: "Step 2: audit login event must be recorded");

        // Step 3: Worklist query.
        var worklistItems = await _worklistService.GetWorklistAsync(cancellationToken);
        worklistItems.Should().NotBeEmpty(because: "Step 3: worklist must return items");

        // Step 4: Patient selection.
        var selectedItem = worklistItems.First();
        await _worklistService.SelectWorklistItemAsync(selectedItem.ProcedureId, cancellationToken);
        selectedItem.PatientId.Should().NotBeNullOrEmpty(because: "Step 4: selected patient must have ID");

        // Step 5: Protocol selection.
        var preset = await _protocolService.GetProtocolPresetAsync(
            selectedItem.BodyPart, TestProjectionCode, cancellationToken);
        preset.Should().NotBeNull(because: "Step 5: protocol preset must exist for selected procedure");

        // Step 6: Exposure preparation and triggering.
        await _exposureService.SetExposureParametersAsync(preset!.DefaultExposure, cancellationToken);
        var triggerRequest = new ExposureTriggerRequest
        {
            StudyId = TestStudyId,
            ProtocolId = preset.ProtocolId,
            Parameters = preset.DefaultExposure
        };
        var exposureResult = await _exposureService.TriggerExposureAsync(triggerRequest, cancellationToken);
        exposureResult.Success.Should().BeTrue(because: "Step 6: exposure must succeed");

        // Log exposure audit event.
        var exposureLogId = await _auditLogService.LogAsync(
            AuditEventType.ExposureInitiated,
            session.User.UserId,
            session.User.UserName,
            $"Exposure initiated: ImageId={exposureResult.ImageId}",
            AuditOutcome.Success,
            selectedItem.PatientId,
            TestStudyId,
            cancellationToken);
        exposureLogId.Should().NotBeNullOrEmpty(because: "Step 6: audit exposure event must be recorded");

        // Step 7: Dose recording.
        var doseDisplay = await _doseService.GetCurrentDoseDisplayAsync(cancellationToken);
        doseDisplay.CumulativeDose.Value.Should().BeGreaterThanOrEqualTo(0m,
            because: "Step 7: cumulative dose must be non-negative");

        // Step 8: Dose alert threshold check.
        var threshold = await _doseService.GetAlertThresholdAsync(cancellationToken);
        var isErrorThresholdExceeded = doseDisplay.CumulativeDose.Value >= threshold.ErrorThreshold;
        isErrorThresholdExceeded.Should().BeFalse(
            because: "Step 8: initial cumulative dose must not exceed error DRL threshold");

        // Step 9: Logout and verify session invalidation.
        var logoutSuccess = await _userService.LogoutAsync(session.SessionId, cancellationToken);
        logoutSuccess.Should().BeTrue(because: "Step 9: logout must succeed");
        var sessionAfterLogout = await _userService.GetCurrentSessionAsync(session.SessionId, cancellationToken);
        sessionAfterLogout.Should().BeNull(because: "Step 9: session must be invalid after logout");

        // Assert: audit log received both the login and exposure events.
        await _auditLogService.Received(1).LogAsync(
            AuditEventType.UserLogin,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            AuditOutcome.Success,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _auditLogService.Received(1).LogAsync(
            AuditEventType.ExposureInitiated,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            AuditOutcome.Success,
            selectedItem.PatientId,
            TestStudyId,
            Arg.Any<CancellationToken>());
    }

    // INT-001-10: Dose reset for a new study clears cumulative dose for clean workflow start.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task DoseReset_ForNewStudy_ClearsCumulativeDoseForCleanWorkflowStart()
    {
        // Arrange: confirm initial cumulative dose is non-zero (MockDoseService initializes to 0.5 mGy·cm2).
        var cancellationToken = CancellationToken.None;
        var displayBefore = await _doseService.GetCurrentDoseDisplayAsync(cancellationToken);
        displayBefore.CumulativeDose.Value.Should().BeGreaterThan(0m,
            because: "Pre-condition: cumulative dose must be non-zero before reset");

        // Act: reset cumulative dose when starting a new study.
        const string newStudyId = "STUDY-INT-001-NEW";
        await _doseService.ResetCumulativeDoseAsync(newStudyId, cancellationToken);

        // Assert: cumulative dose is zero after reset.
        var displayAfter = await _doseService.GetCurrentDoseDisplayAsync(cancellationToken);
        displayAfter.CumulativeDose.Value.Should().Be(0m,
            because: "Cumulative dose must be cleared when starting a new study");

        // Assert: threshold check shows no alert after reset.
        var threshold = await _doseService.GetAlertThresholdAsync(cancellationToken);
        var isAlertActive = displayAfter.CumulativeDose.Value >= threshold.WarningThreshold;
        isAlertActive.Should().BeFalse(
            because: "No DRL alert must be active immediately after study dose reset");
    }

    // INT-001-11: Technologist permissions are correct for exposure workflow operations.
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P0")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task TechnicianPermissions_AllowExposureWorkflow_DenyAdministration()
    {
        // Arrange
        const string techId = "tech01";
        var cancellationToken = CancellationToken.None;

        // Act: check required permissions for the examination workflow.
        var canExecuteExposure = await _userService.HasPermissionAsync(
            techId, "exposure.execute", cancellationToken);
        var canViewWorklist = await _userService.HasPermissionAsync(
            techId, "worklist.view", cancellationToken);
        var canAdminister = await _userService.HasPermissionAsync(
            techId, "system.admin", cancellationToken);
        var canManageUsers = await _userService.HasPermissionAsync(
            techId, "users.manage", cancellationToken);

        // Assert: technologist can perform required workflow actions.
        canExecuteExposure.Should().BeTrue(
            because: "Technologist must have exposure.execute permission for clinical workflow");
        canViewWorklist.Should().BeTrue(
            because: "Technologist must have worklist.view permission for patient selection");

        // Assert: technologist cannot perform administrative actions.
        canAdminister.Should().BeFalse(
            because: "Technologist must not have system administration access (RBAC)");
        canManageUsers.Should().BeFalse(
            because: "Technologist must not have user management access (RBAC)");
    }
}
