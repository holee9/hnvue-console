using FluentAssertions;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Worklist;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HnVue.Dicom.Tests.Worklist;

/// <summary>
/// Unit tests for the actual WorklistScu implementation class.
/// SPEC-DICOM-001 AC-03: Modality Worklist.
/// Network tests use a closed port (fast failure); dataset tests use in-memory data.
/// </summary>
public class WorklistScuTests
{
    private readonly DicomServiceOptions _optionsWithScp;
    private readonly DicomServiceOptions _optionsWithoutScp;

    public WorklistScuTests()
    {
        _optionsWithScp = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            WorklistScp = new DicomDestination
            {
                AeTitle = "WORKLIST_SCP",
                Host = "127.0.0.1",
                Port = 19995  // closed port - fast connection refused
            }
        };

        _optionsWithoutScp = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            WorklistScp = null
        };
    }

    private WorklistScu CreateSut(DicomServiceOptions options)
    {
        return new WorklistScu(Options.Create(options), NullLogger<WorklistScu>.Instance);
    }

    // Constructor injection: WorklistScu initializes correctly
    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        // Act
        Action act = () => CreateSut(_optionsWithScp);

        // Assert
        act.Should().NotThrow("all required dependencies are provided");
    }

    // QueryAsync throws InvalidOperationException when WorklistScp is null
    [Fact]
    public async Task QueryAsync_WithNullWorklistScp_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithoutScp);
        var query = new WorklistQuery();

        // Act & Assert
        var act = async () =>
        {
            await foreach (var item in sut.QueryAsync(query))
            {
                // should not enumerate
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>(
            "WorklistScp must be configured before calling QueryAsync");
    }

    // QueryAsync with unreachable SCP propagates a network exception
    [Fact]
    public async Task QueryAsync_WithUnreachableScp_ThrowsNetworkException()
    {
        // Arrange: closed port ensures immediate connection refused
        var sut = CreateSut(_optionsWithScp);
        var query = new WorklistQuery(ScheduledDate: DateRange.Today(), Modality: "DX");

        // Act & Assert
        var act = async () =>
        {
            await foreach (var item in sut.QueryAsync(query))
            {
                // should not enumerate items
            }
        };

        await act.Should().ThrowAsync<Exception>(
            "unreachable SCP causes a network exception to propagate to the caller");
    }

    // DateRange.Today returns a range where Start == End == today
    [Fact]
    public void DateRange_Today_StartEqualsEndEqualsToday()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var range = DateRange.Today();

        // Assert
        range.Start.Should().Be(today);
        range.End.Should().Be(today);
    }

    // DateRange.Today formats as single YYYYMMDD string (not YYYYMMDD-YYYYMMDD)
    [Fact]
    public void DateRange_Today_FormatsTodayAsSingleDate()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var range = DateRange.Today();

        // Assert
        range.ToDicomRangeString().Should().Be(today.ToString("yyyyMMdd"),
            "single-day range formats as YYYYMMDD without dash per DICOM DA range encoding");
    }

    // DateRange with both null produces wildcard for C-FIND
    [Fact]
    public void DateRange_BothBoundsNull_ReturnsWildcard()
    {
        // Arrange
        var range = new DateRange(null, null);

        // Act & Assert
        range.ToDicomRangeString().Should().Be("*",
            "null start and end produce wildcard for DICOM C-FIND query");
    }

    // DateRange with start and end formats as YYYYMMDD-YYYYMMDD
    [Fact]
    public void DateRange_WithStartAndEnd_FormatsAsDicomRange()
    {
        // Arrange
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 12, 31);

        // Act
        var range = new DateRange(start, end);

        // Assert
        range.ToDicomRangeString().Should().Be("20240101-20241231",
            "date range must format as YYYYMMDD-YYYYMMDD per DICOM DA range encoding");
    }

    // DateRange.ForDate creates a single-day range
    [Fact]
    public void DateRange_ForDate_CreatesSingleDayRange()
    {
        // Arrange
        var date = new DateOnly(2024, 6, 15);

        // Act
        var range = DateRange.ForDate(date);

        // Assert
        range.Start.Should().Be(date);
        range.End.Should().Be(date);
        range.ToDicomRangeString().Should().Be("20240615");
    }

    // WorklistQuery default constructor - all fields null (wildcard)
    [Fact]
    public void WorklistQuery_DefaultConstructor_AllFieldsAreNull()
    {
        // Act
        var query = new WorklistQuery();

        // Assert
        query.ScheduledDate.Should().BeNull();
        query.Modality.Should().BeNull();
        query.PatientId.Should().BeNull();
        query.AeTitle.Should().BeNull();
    }

    // WorklistItem fields are all accessible
    [Fact]
    public void WorklistItem_AllMandatoryFields_AreAccessible()
    {
        // Arrange & Act
        var item = new WorklistItem(
            PatientId: "P001",
            PatientName: "Doe^John",
            BirthDate: new DateOnly(1980, 1, 1),
            PatientSex: "M",
            StudyInstanceUid: "1.2.3.4.5.100",
            AccessionNumber: "ACC001",
            RequestedProcedureId: "REQ001",
            ScheduledProcedureStep: new ScheduledProcedureStep(
                StepId: "SPS001",
                Description: "DX Chest PA",
                DateTime: DateTime.Today,
                PerformingPhysician: null,
                Modality: "DX"));

        // Assert
        item.PatientId.Should().Be("P001");
        item.PatientName.Should().Be("Doe^John");
        item.BirthDate.Should().Be(new DateOnly(1980, 1, 1));
        item.PatientSex.Should().Be("M");
        item.StudyInstanceUid.Should().Be("1.2.3.4.5.100");
        item.AccessionNumber.Should().Be("ACC001");
        item.RequestedProcedureId.Should().Be("REQ001");
        item.ScheduledProcedureStep.Should().NotBeNull();
        item.ScheduledProcedureStep.StepId.Should().Be("SPS001");
        item.ScheduledProcedureStep.Modality.Should().Be("DX");
    }

    // DicomWorklistException carries the DICOM status code
    [Fact]
    public void DicomWorklistException_WithStatusCode_StoresStatusCodeCorrectly()
    {
        // Arrange & Act
        var exception = new DicomWorklistException(0xA700, "SCP failure");

        // Assert
        exception.StatusCode.Should().Be(0xA700);
        exception.Message.Should().Be("SCP failure");
    }
}
