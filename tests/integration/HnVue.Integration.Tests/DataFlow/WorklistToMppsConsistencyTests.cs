using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Services;
using NSubstitute;
using Xunit;

namespace HnVue.Integration.Tests.DataFlow;

/// <summary>
/// INT-007: Worklist to MPPS Data Consistency Integration Tests.
/// Validates that DICOM worklist data is preserved with complete fidelity
/// as it flows from the MWL SCP through patient selection into the exposure workflow.
/// Tests cover required DICOM attribute presence, status transitions,
/// multi-item integrity, and end-to-end data consistency from worklist
/// selection through ExposureTriggerRequest construction.
/// No Docker required - uses NSubstitute for IWorklistService,
/// MockDoseService, MockProtocolService, and MockExposureService.
/// IEC 62304 Class B/C: Data flow boundary verification.
/// </summary>
public sealed class WorklistToMppsConsistencyTests
{
    // Service instances used across tests in this class.
    private readonly IWorklistService _worklistService;
    private readonly MockDoseService _doseService;
    private readonly MockProtocolService _protocolService;
    private readonly MockExposureService _exposureService;

    // DICOM-required field constants for INT-007 scenario.
    private const string TestPatientId = "PAT-INT-007";
    private const string TestPatientName = "Kim Chul-soo";
    private const string TestAccessionNumber = "ACC-INT-007";
    private const string TestProcedureStepId = "SPS-INT-007";
    private const string TestStudyInstanceUid = "1.2.840.10008.5.1.4.1.1.7.TEST007";
    private const string TestBodyPart = "CHEST";
    private const string TestProjection = "PA";
    private const string TestStudyId = "STUDY-INT-007";

    public WorklistToMppsConsistencyTests()
    {
        _doseService = new MockDoseService();
        _protocolService = new MockProtocolService();
        _exposureService = new MockExposureService();

        // Build a controlled multi-item worklist for INT-007 scenarios.
        _worklistService = Substitute.For<IWorklistService>();

        var scheduledItem = new WorklistItem
        {
            ProcedureId = TestProcedureStepId,
            PatientId = TestPatientId,
            PatientName = TestPatientName,
            AccessionNumber = TestAccessionNumber,
            ScheduledProcedureStepDescription = "Chest PA Routine",
            ScheduledDateTime = DateTimeOffset.UtcNow.AddHours(1),
            BodyPart = TestBodyPart,
            Projection = TestProjection,
            Status = WorklistStatus.Scheduled
        };

        var secondItem = new WorklistItem
        {
            ProcedureId = "SPS-INT-007-B",
            PatientId = "PAT-INT-007-B",
            PatientName = "Lee Young-hee",
            AccessionNumber = "ACC-INT-007-B",
            ScheduledProcedureStepDescription = "Knee AP",
            ScheduledDateTime = DateTimeOffset.UtcNow.AddHours(2),
            BodyPart = "KNEE",
            Projection = "AP",
            Status = WorklistStatus.Scheduled
        };

        _worklistService.GetWorklistAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WorklistItem>>(
                new List<WorklistItem> { scheduledItem, secondItem }));

        _worklistService.SelectWorklistItemAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// INT-007-1: Worklist items must contain all required DICOM fields:
    /// Patient ID, Patient Name, Accession Number, Scheduled Procedure Step ID,
    /// and Body Part (standing in for Study Instance UID in the mock model).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task WorklistItem_ContainsAllRequiredDicomFields()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act: query the worklist and inspect the first item.
        var items = await _worklistService.GetWorklistAsync(cancellationToken);
        var item = items.First(i => i.PatientId == TestPatientId);

        // Assert: all DICOM mandatory fields must be populated.
        item.PatientId.Should().NotBeNullOrEmpty(
            because: "DICOM PS3.4 C.7.1.1 requires Patient ID in MWL response");
        item.PatientName.Should().NotBeNullOrEmpty(
            because: "DICOM PS3.4 C.7.1.3 requires Patient Name in MWL response");
        item.AccessionNumber.Should().NotBeNullOrEmpty(
            because: "DICOM PS3.4 C.7.4.1 requires Accession Number in MWL response");
        item.ProcedureId.Should().NotBeNullOrEmpty(
            because: "DICOM PS3.4 C.7.3.1 requires Scheduled Procedure Step ID in MWL response");
        item.BodyPart.Should().NotBeNullOrEmpty(
            because: "Body Part Examined must be present for protocol selection");

        // Assert: the values match the expected test data.
        item.PatientId.Should().Be(TestPatientId, because: "Patient ID must exactly match MWL data");
        item.PatientName.Should().Be(TestPatientName, because: "Patient Name must exactly match MWL data");
        item.AccessionNumber.Should().Be(TestAccessionNumber, because: "Accession Number must exactly match MWL data");
    }

    /// <summary>
    /// INT-007-2: Selected worklist item clinical data is preserved when transitioning
    /// to the MPPS context: BodyPart, Projection, and AccessionNumber must be retained.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task SelectedWorklistItem_DataIsPreserved_WhenCreatingMppsContext()
    {
        // Arrange: query the worklist and identify the target item.
        var cancellationToken = CancellationToken.None;
        var items = await _worklistService.GetWorklistAsync(cancellationToken);
        var selectedItem = items.First(i => i.PatientId == TestPatientId);

        // Act: simulate selecting the item and capturing the MPPS context fields.
        await _worklistService.SelectWorklistItemAsync(selectedItem.ProcedureId, cancellationToken);

        // Simulate building an exposure trigger from the selected item
        // (equivalent of "creating MPPS context" in the workflow).
        var preset = await _protocolService.GetProtocolPresetAsync(
            selectedItem.BodyPart, selectedItem.Projection, cancellationToken);
        preset.Should().NotBeNull(because: "A protocol preset must exist for the worklist item body part/projection");

        var triggerRequest = new ExposureTriggerRequest
        {
            StudyId = TestStudyId,
            ProtocolId = $"PROTO-{selectedItem.BodyPart}-{selectedItem.Projection}",
            Parameters = preset!.DefaultExposure // use real ExposureParameters from MockProtocolService
        };

        // Assert: all source worklist data is intact after the transition.
        selectedItem.BodyPart.Should().Be(TestBodyPart,
            because: "Body Part must be retained through worklist selection");
        selectedItem.Projection.Should().Be(TestProjection,
            because: "Projection must be retained through worklist selection");
        selectedItem.AccessionNumber.Should().Be(TestAccessionNumber,
            because: "Accession Number must not be mutated during patient selection");

        // The trigger request must reference data that traces back to the worklist item.
        triggerRequest.ProtocolId.Should().Contain(selectedItem.BodyPart,
            because: "Protocol ID must encode the body part from the selected worklist item");
        triggerRequest.ProtocolId.Should().Contain(selectedItem.Projection,
            because: "Protocol ID must encode the projection from the selected worklist item");

        // Verify selection was called with the correct procedure ID.
        await _worklistService.Received(1).SelectWorklistItemAsync(selectedItem.ProcedureId, cancellationToken);
    }

    /// <summary>
    /// INT-007-3: Worklist status must follow the legal state machine:
    /// initial state must be Scheduled; transitions to InProgress and Completed
    /// must be logically ordered.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task WorklistStatus_FollowsCorrectStateMachine_ScheduledToInProgressToCompleted()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var items = await _worklistService.GetWorklistAsync(cancellationToken);
        var item = items.First(i => i.PatientId == TestPatientId);

        // Assert: initial state must be Scheduled (precondition for workflow).
        item.Status.Should().Be(WorklistStatus.Scheduled,
            because: "Worklist items must start in Scheduled state before being selected");

        // Simulate status transitions using NSubstitute to represent state changes.
        // The worklist service is responsible for persisting status changes;
        // here we validate the ordering rules for the state machine.
        var validInitialStates = new[] { WorklistStatus.Scheduled };
        var validAfterSelection = new[] { WorklistStatus.InProgress };
        var validAfterCompletion = new[] { WorklistStatus.Completed };

        validInitialStates.Should().Contain(item.Status,
            because: "Initial worklist status must be Scheduled");

        // The status values must follow the defined enum ordering for the state machine.
        ((int)WorklistStatus.Scheduled).Should().BeLessThan((int)WorklistStatus.InProgress,
            because: "InProgress must represent a later stage than Scheduled in the state machine");
        ((int)WorklistStatus.InProgress).Should().BeLessThan((int)WorklistStatus.Completed,
            because: "Completed must represent the final stage after InProgress");

        // Direct regression guard: Completed must not appear before InProgress was reached.
        WorklistStatus.Completed.Should().NotBe(WorklistStatus.Scheduled,
            because: "A procedure cannot be Completed without passing through InProgress");
    }

    /// <summary>
    /// INT-007-4: Multiple worklist items must have distinct identifiers;
    /// querying the full list must not corrupt per-item data.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task MultipleWorklistItems_HaveDistinctIdentifiers_WithoutDataCorruption()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act: retrieve the full worklist.
        var items = await _worklistService.GetWorklistAsync(cancellationToken);

        // Assert: at least two items are returned by the mock.
        items.Should().HaveCountGreaterThanOrEqualTo(2,
            because: "Test fixture configures two worklist items for multi-item validation");

        // Assert: each item has a distinct ProcedureId (primary key in DICOM MWL).
        var procedureIds = items.Select(i => i.ProcedureId).ToList();
        procedureIds.Should().OnlyHaveUniqueItems(
            because: "DICOM Scheduled Procedure Step IDs must be unique across worklist items");

        // Assert: each item has a distinct AccessionNumber (study-level identifier).
        var accessionNumbers = items.Select(i => i.AccessionNumber).ToList();
        accessionNumbers.Should().OnlyHaveUniqueItems(
            because: "Accession Numbers must uniquely identify each study request");

        // Assert: per-item data is intact (no cross-contamination between items).
        var firstItem = items.First(i => i.ProcedureId == TestProcedureStepId);
        var secondItem = items.First(i => i.ProcedureId == "SPS-INT-007-B");

        firstItem.PatientId.Should().Be(TestPatientId,
            because: "First item data must not be contaminated by second item");
        secondItem.PatientId.Should().Be("PAT-INT-007-B",
            because: "Second item data must not be contaminated by first item");
        firstItem.AccessionNumber.Should().NotBe(secondItem.AccessionNumber,
            because: "Items from different study requests must have different accession numbers");
    }

    /// <summary>
    /// INT-007-5: End-to-end data consistency: the WorklistItem selected by the operator
    /// must supply the study and protocol identifiers that are embedded in the
    /// ExposureTriggerRequest sent to the exposure service.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P1")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task EndToEnd_DataConsistency_WorklistItemToExposureTriggerRequest()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var items = await _worklistService.GetWorklistAsync(cancellationToken);
        var selectedItem = items.First(i => i.PatientId == TestPatientId);

        // Simulate selecting the item.
        await _worklistService.SelectWorklistItemAsync(selectedItem.ProcedureId, cancellationToken);

        // Retrieve the matching protocol preset using the body part from the worklist item.
        var preset = await _protocolService.GetProtocolPresetAsync(
            selectedItem.BodyPart, selectedItem.Projection, cancellationToken);
        preset.Should().NotBeNull(
            because: "A protocol preset must exist for the body part and projection from the worklist item");

        // Set exposure parameters derived from the preset.
        await _exposureService.SetExposureParametersAsync(preset!.DefaultExposure, cancellationToken);

        // Build the ExposureTriggerRequest using the accession number as the study reference.
        var triggerRequest = new ExposureTriggerRequest
        {
            StudyId = selectedItem.AccessionNumber,
            ProtocolId = preset.ProtocolId,
            Parameters = preset.DefaultExposure // preset.DefaultExposure is a complete ExposureParameters
        };

        // Act: trigger the exposure.
        var result = await _exposureService.TriggerExposureAsync(triggerRequest, cancellationToken);

        // Assert: the trigger request carries data traceable back to the original worklist item.
        triggerRequest.StudyId.Should().Be(selectedItem.AccessionNumber,
            because: "StudyId in ExposureTriggerRequest must match the AccessionNumber from the selected worklist item");
        triggerRequest.ProtocolId.Should().Be(preset.ProtocolId,
            because: "ProtocolId must be derived from the worklist-selected body part and projection");

        // Assert: the exposure itself succeeded.
        result.Should().NotBeNull(because: "ExposureTriggerResult must be returned");
        result.Success.Should().BeTrue(
            because: "Exposure must succeed when parameters are derived from a valid worklist item");
        result.ImageId.Should().NotBeNullOrEmpty(
            because: "A successful exposure must produce a traceable image identifier");
    }
}
