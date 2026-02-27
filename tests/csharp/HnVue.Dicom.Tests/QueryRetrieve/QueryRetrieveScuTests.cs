using FluentAssertions;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.QueryRetrieve;
using HnVue.Dicom.Worklist;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HnVue.Dicom.Tests.QueryRetrieve;

/// <summary>
/// Unit tests for the QueryRetrieveScu implementation class.
/// SPEC-DICOM-001 AC-06, AC-07: Query/Retrieve (C-FIND, C-MOVE).
/// Network tests use a closed port (fast failure); dataset tests use in-memory data.
/// </summary>
public class QueryRetrieveScuTests
{
    private readonly DicomServiceOptions _optionsWithScp;
    private readonly DicomServiceOptions _optionsWithoutScp;

    public QueryRetrieveScuTests()
    {
        _optionsWithScp = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            QueryRetrieveScp = new DicomDestination
            {
                AeTitle = "QR_SCP",
                Host = "127.0.0.1",
                Port = 19996  // closed port - fast connection refused
            }
        };

        _optionsWithoutScp = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            QueryRetrieveScp = null
        };
    }

    private QueryRetrieveScu CreateSut(DicomServiceOptions options)
    {
        return new QueryRetrieveScu(Options.Create(options), NullLogger<QueryRetrieveScu>.Instance);
    }

    // Constructor injection: QueryRetrieveScu initializes correctly
    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        // Act
        Action act = () => CreateSut(_optionsWithScp);

        // Assert
        act.Should().NotThrow("all required dependencies are provided");
    }

    // FindStudiesAsync throws InvalidOperationException when QueryRetrieveScp is null
    [Fact]
    public async Task FindStudiesAsync_WithNullQueryRetrieveScp_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithoutScp);
        var query = new StudyQuery();

        // Act & Assert
        var act = async () =>
        {
            await foreach (var item in sut.FindStudiesAsync(query))
            {
                // should not enumerate
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>(
            "QueryRetrieveScp must be configured before calling FindStudiesAsync");
    }

    // FindStudiesAsync with unreachable SCP propagates a network exception
    [Fact]
    public async Task FindStudiesAsync_WithUnreachableScp_ThrowsNetworkException()
    {
        // Arrange: closed port ensures immediate connection refused
        var sut = CreateSut(_optionsWithScp);
        var query = new StudyQuery
        {
            PatientId = "P001",
            StudyDate = DateRange.Today()
        };

        // Act & Assert
        var act = async () =>
        {
            await foreach (var item in sut.FindStudiesAsync(query))
            {
                // should not enumerate items
            }
        };

        await act.Should().ThrowAsync<Exception>(
            "unreachable SCP causes a network exception to propagate to the caller");
    }

    // MoveStudyAsync throws InvalidOperationException when QueryRetrieveScp is null
    [Fact]
    public async Task MoveStudyAsync_WithNullQueryRetrieveScp_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithoutScp);
        var studyUid = "1.2.840.10008.1.1.1.1";
        var destinationAe = "DEST_AE";

        // Act & Assert
        var act = async () => await sut.MoveStudyAsync(studyUid, destinationAe);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "QueryRetrieveScp must be configured before calling MoveStudyAsync");
    }

    // MoveStudyAsync with unreachable SCP propagates a network exception
    [Fact]
    public async Task MoveStudyAsync_WithUnreachableScp_ThrowsNetworkException()
    {
        // Arrange: closed port ensures immediate connection refused
        var sut = CreateSut(_optionsWithScp);
        var studyUid = "1.2.840.10008.1.1.1.1";
        var destinationAe = "DEST_AE";

        // Act & Assert
        var act = async () => await sut.MoveStudyAsync(studyUid, destinationAe);

        await act.Should().ThrowAsync<Exception>(
            "unreachable SCP causes a network exception to propagate to the caller");
    }

    // MoveStudyAsync throws ArgumentNullException when studyInstanceUid is null
    [Fact]
    public async Task MoveStudyAsync_WithNullStudyUid_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithScp);

        // Act & Assert
        var act = async () => await sut.MoveStudyAsync(null!, "DEST_AE");

        await act.Should().ThrowAsync<ArgumentNullException>(
            "studyInstanceUid must not be null");
    }

    // MoveStudyAsync throws ArgumentNullException when destinationAeTitle is null
    [Fact]
    public async Task MoveStudyAsync_WithNullDestinationAe_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut(_optionsWithScp);
        var studyUid = "1.2.840.10008.1.1.1.1";

        // Act & Assert
        var act = async () => await sut.MoveStudyAsync(studyUid, null!);

        await act.Should().ThrowAsync<ArgumentNullException>(
            "destinationAeTitle must not be null");
    }

    // StudyQuery default constructor - all fields null (wildcard)
    [Fact]
    public void StudyQuery_DefaultConstructor_AllFieldsAreNull()
    {
        // Act
        var query = new StudyQuery();

        // Assert
        query.PatientId.Should().BeNull();
        query.AccessionNumber.Should().BeNull();
        query.StudyInstanceUid.Should().BeNull();
        query.Modality.Should().BeNull();
        query.StudyDate.Should().BeNull();
    }

    // StudyQuery with specific values
    [Fact]
    public void StudyQuery_WithSpecificValues_StoresValuesCorrectly()
    {
        // Arrange & Act
        var query = new StudyQuery
        {
            PatientId = "P001",
            AccessionNumber = "ACC001",
            StudyInstanceUid = "1.2.840.10008.1.1.1.1",
            Modality = "DX",
            StudyDate = DateRange.Today()
        };

        // Assert
        query.PatientId.Should().Be("P001");
        query.AccessionNumber.Should().Be("ACC001");
        query.StudyInstanceUid.Should().Be("1.2.840.10008.1.1.1.1");
        query.Modality.Should().Be("DX");
        query.StudyDate.Should().NotBeNull();
    }

    // StudyResult all fields are accessible
    [Fact]
    public void StudyResult_AllFields_AreAccessible()
    {
        // Arrange & Act
        var result = new StudyResult(
            StudyInstanceUid: "1.2.840.10008.1.1.1.1",
            PatientId: "P001",
            PatientName: "Doe^John",
            AccessionNumber: "ACC001",
            Modality: "DX",
            StudyDate: new DateOnly(2024, 6, 15),
            StudyDescription: "Chest PA",
            NumberOfStudyRelatedSeries: 1,
            NumberOfStudyRelatedInstances: 2);

        // Assert
        result.StudyInstanceUid.Should().Be("1.2.840.10008.1.1.1.1");
        result.PatientId.Should().Be("P001");
        result.PatientName.Should().Be("Doe^John");
        result.AccessionNumber.Should().Be("ACC001");
        result.Modality.Should().Be("DX");
        result.StudyDate.Should().Be(new DateOnly(2024, 6, 15));
        result.StudyDescription.Should().Be("Chest PA");
        result.NumberOfStudyRelatedSeries.Should().Be(1);
        result.NumberOfStudyRelatedInstances.Should().Be(2);
    }

    // StudyResult with nullable fields as null
    [Fact]
    public void StudyResult_WithNullOptionalFields_HandlesNullsCorrectly()
    {
        // Arrange & Act
        var result = new StudyResult(
            StudyInstanceUid: "1.2.840.10008.1.1.1.1",
            PatientId: null,
            PatientName: null,
            AccessionNumber: null,
            Modality: null,
            StudyDate: null,
            StudyDescription: null,
            NumberOfStudyRelatedSeries: null,
            NumberOfStudyRelatedInstances: null);

        // Assert
        result.StudyInstanceUid.Should().Be("1.2.840.10008.1.1.1.1");
        result.PatientId.Should().BeNull();
        result.PatientName.Should().BeNull();
        result.AccessionNumber.Should().BeNull();
        result.Modality.Should().BeNull();
        result.StudyDate.Should().BeNull();
        result.StudyDescription.Should().BeNull();
        result.NumberOfStudyRelatedSeries.Should().BeNull();
        result.NumberOfStudyRelatedInstances.Should().BeNull();
    }

    // DicomQueryRetrieveException carries the DICOM status code
    [Fact]
    public void DicomQueryRetrieveException_WithStatusCode_StoresStatusCodeCorrectly()
    {
        // Arrange & Act
        var exception = new DicomQueryRetrieveException(0xA700, "QR SCP failure");

        // Assert
        exception.StatusCode.Should().Be(0xA700);
        exception.Message.Should().Be("QR SCP failure");
    }

    // StudyQuery with DateRange.Today formats correctly
    [Fact]
    public void StudyQuery_WithDateRangeToday_FormatsQueryCorrectly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var query = new StudyQuery
        {
            StudyDate = DateRange.Today()
        };

        // Assert
        query.StudyDate.Should().NotBeNull();
        query.StudyDate!.Start.Should().Be(today);
        query.StudyDate!.End.Should().Be(today);
        query.StudyDate!.ToDicomRangeString().Should().Be(today.ToString("yyyyMMdd"));
    }
}
