using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HnVue.Dicom.Worklist;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.Worklist;

/// <summary>
/// Tests for DicomWorklistClient following TDD methodology (RED-GREEN-REFACTOR).
/// SPEC-WORKFLOW-001 TASK-406: C-FIND Worklist Query
/// </summary>
public class DicomWorklistClientTests
{
    private readonly Mock<ILogger<DicomWorklistClient>> _loggerMock;
    private readonly Mock<IWorklistScu> _worklistScuMock;

    public DicomWorklistClientTests()
    {
        _loggerMock = new Mock<ILogger<DicomWorklistClient>>();
        _worklistScuMock = new Mock<IWorklistScu>();
    }

    [Fact]
    public async Task QueryWorklistAsync_ShouldReturnResults_WhenWorklistReturnsItems()
    {
        // Arrange
        var client = new DicomWorklistClient(_worklistScuMock.Object, _loggerMock.Object);
        var query = new WorklistQuery
        {
            PatientId = "PATIENT001",
            ScheduledDate = new DateRange(DateOnly.FromDateTime(DateTime.Today), null)
        };

        var expectedItems = new[]
        {
            new WorklistItem(
                "PATIENT001",
                "DOE^JOHN",
                DateOnly.FromDateTime(new DateTime(1980, 1, 1)),
                "M",
                "1.2.840.10008.1.1.1.1",
                "ACCESSION001",
                "REQ001",
                new ScheduledProcedureStep(
                    "STEP001",
                    "Chest PA",
                    DateTime.Now,
                    "Dr. Smith",
                    "CR"))
        };

        _worklistScuMock
            .Setup(x => x.QueryAsync(It.IsAny<WorklistQuery>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(expectedItems));

        // Act
        var result = await client.QueryWorklistAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].PatientId.Should().Be("PATIENT001");
        result.Items[0].PatientName.Should().Be("DOE^JOHN");
    }

    [Fact]
    public async Task QueryWorklistAsync_ShouldReturnEmptyResult_WhenWorklistReturnsNoItems()
    {
        // Arrange
        var client = new DicomWorklistClient(_worklistScuMock.Object, _loggerMock.Object);
        var query = new WorklistQuery { PatientId = "NONEXISTENT" };

        _worklistScuMock
            .Setup(x => x.QueryAsync(It.IsAny<WorklistQuery>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(Array.Empty<WorklistItem>()));

        // Act
        var result = await client.QueryWorklistAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryWorklistAsync_ShouldReturnFailedResult_WhenWorklistThrowsException()
    {
        // Arrange
        var client = new DicomWorklistClient(_worklistScuMock.Object, _loggerMock.Object);
        var query = new WorklistQuery { PatientId = "TEST" };

        _worklistScuMock
            .Setup(x => x.QueryAsync(It.IsAny<WorklistQuery>(), It.IsAny<CancellationToken>()))
            .Returns((WorklistQuery q, CancellationToken ct) =>
            {
                throw new DicomWorklistException(0xA700, "Worklist SCP unavailable");
            });

        // Act
        var result = await client.QueryWorklistAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryWorklistAsync_ShouldApplyTimeout_WhenQueryTakesTooLong()
    {
        // Arrange
        var client = new DicomWorklistClient(_worklistScuMock.Object, _loggerMock.Object);
        var query = new WorklistQuery { PatientId = "TEST" };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        _worklistScuMock
            .Setup(x => x.QueryAsync(It.IsAny<WorklistQuery>(), It.IsAny<CancellationToken>()))
            .Returns((WorklistQuery q, CancellationToken ct) =>
            {
                // Return an async enumerable that will delay before yielding
                return GetDelayedAsyncEnumerable(ct);
            });

        // Act
        var result = await client.QueryWorklistAsync(query, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Items.Should().BeEmpty();
    }

    private static async IAsyncEnumerable<WorklistItem> GetDelayedAsyncEnumerable([EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        yield break;
    }

    [Fact]
    public async Task QueryWorklistAsync_ShouldFilterByPatientId()
    {
        // Arrange
        var client = new DicomWorklistClient(_worklistScuMock.Object, _loggerMock.Object);
        var query = new WorklistQuery { PatientId = "PATIENT001" };

        var expectedItems = new[]
        {
            new WorklistItem(
                "PATIENT001",
                "DOE^JOHN",
                null,
                null,
                null,
                "ACC001",
                "REQ001",
                new ScheduledProcedureStep("STEP1", "Test", null, null, "CR"))
        };

        _worklistScuMock
            .Setup(x => x.QueryAsync(It.Is<WorklistQuery>(q => q.PatientId == "PATIENT001"), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(expectedItems));

        // Act
        var result = await client.QueryWorklistAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Items.Should().HaveCount(1);

        _worklistScuMock.Verify(
            x => x.QueryAsync(It.Is<WorklistQuery>(q => q.PatientId == "PATIENT001"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryWorklistAsync_ShouldFilterByScheduledDate()
    {
        // Arrange
        var client = new DicomWorklistClient(_worklistScuMock.Object, _loggerMock.Object);
        var dateRange = new DateRange(DateOnly.FromDateTime(new DateTime(2024, 1, 15)), null);
        var query = new WorklistQuery { ScheduledDate = dateRange };

        _worklistScuMock
            .Setup(x => x.QueryAsync(It.Is<WorklistQuery>(q => q.ScheduledDate == dateRange), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(Array.Empty<WorklistItem>()));

        // Act
        var result = await client.QueryWorklistAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        _worklistScuMock.Verify(
            x => x.QueryAsync(It.Is<WorklistQuery>(q => q.ScheduledDate == dateRange), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static async IAsyncEnumerable<WorklistItem> ToAsyncEnumerable(WorklistItem[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
