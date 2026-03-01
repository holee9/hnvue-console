using System;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using DicomNetwork = Dicom.Network;
using Dicom.Network.Client;
using FluentAssertions;
using HnVue.Dicom.Associations;
using HnVue.Dicom.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.Association;

/// <summary>
/// Tests for DicomAssociationManager following TDD methodology (RED-GREEN-REFACTOR).
/// SPEC-WORKFLOW-001 TASK-409: DICOM Association Management
/// </summary>
public class DicomAssociationManagerTests
{
    private readonly Mock<ILogger<DicomAssociationPool>> _loggerMock;
    private readonly Mock<IAssociationManager> _innerManagerMock;

    public DicomAssociationManagerTests()
    {
        _loggerMock = new Mock<ILogger<DicomAssociationPool>>();
        _innerManagerMock = new Mock<IAssociationManager>();
    }

    [Fact]
    public async Task AcquireAssociationAsync_ShouldReturnClient_WhenSuccessful()
    {
        // Arrange
        var pool = new DicomAssociationPool(_innerManagerMock.Object, _loggerMock.Object);
        var destination = new DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };
        var presentationContexts = new List<DicomNetwork.DicomPresentationContext>();

        var expectedClient = new DicomClient("localhost", 104, false, "CALLING", "CALLED");

        _innerManagerMock
            .Setup(x => x.CreateAssociationAsync(
                It.IsAny<DicomDestination>(),
                It.IsAny<List<DicomNetwork.DicomPresentationContext>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClient);

        // Act
        var result = await pool.AcquireAssociationAsync(destination, new List<DicomNetwork.DicomPresentationContext>(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedClient);
    }

    [Fact]
    public async Task ReleaseAssociationAsync_ShouldCallInnerManager()
    {
        // Arrange
        var pool = new DicomAssociationPool(_innerManagerMock.Object, _loggerMock.Object);
        var client = new DicomClient("localhost", 104, false, "CALLING", "CALLED");

        _innerManagerMock
            .Setup(x => x.CloseAssociationAsync(It.IsAny<DicomClient>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await pool.ReleaseAssociationAsync(client, CancellationToken.None);

        // Assert
        _innerManagerMock.Verify(
            x => x.CloseAssociationAsync(client, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcquireAssociationAsync_ShouldEnforcePoolLimit()
    {
        // Arrange
        var pool = new DicomAssociationPool(_innerManagerMock.Object, _loggerMock.Object);
        var destination = new DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        var expectedClient = new DicomClient("localhost", 104, false, "CALLING", "CALLED");

        _innerManagerMock
            .Setup(x => x.CreateAssociationAsync(
                It.IsAny<DicomDestination>(),
                It.IsAny<List<DicomNetwork.DicomPresentationContext>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClient);

        // Act - Acquire multiple associations up to pool limit (5)
        var associations = new List<DicomClient>();
        for (int i = 0; i < 5; i++)
        {
            var client = await pool.AcquireAssociationAsync(destination, new List<DicomNetwork.DicomPresentationContext>(), CancellationToken.None);
            associations.Add(client);
        }

        // Assert
        associations.Should().HaveCount(5);
    }

    [Fact]
    public async Task AcquireAssociationAsync_ShouldTimeout_WhenPoolExhausted()
    {
        // Arrange
        var pool = new DicomAssociationPool(_innerManagerMock.Object, _loggerMock.Object);
        var destination = new DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        var expectedClient = new DicomClient("localhost", 104, false, "CALLING", "CALLED");

        _innerManagerMock
            .Setup(x => x.CreateAssociationAsync(
                It.IsAny<DicomDestination>(),
                It.IsAny<List<DicomNetwork.DicomPresentationContext>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClient);

        // Act - Acquire all pool slots
        var associations = new List<DicomClient>();
        for (int i = 0; i < 5; i++)
        {
            var client = await pool.AcquireAssociationAsync(destination, new List<DicomNetwork.DicomPresentationContext>(), CancellationToken.None);
            associations.Add(client);
        }

        // Try to acquire one more (should timeout or fail)
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Func<Task> act = async () => await pool.AcquireAssociationAsync(
            destination,
            new List<DicomNetwork.DicomPresentationContext>(),
            timeoutCts.Token);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task ReleaseAssociationAsync_ShouldFreePoolSlot()
    {
        // Arrange
        var pool = new DicomAssociationPool(_innerManagerMock.Object, _loggerMock.Object);
        var destination = new DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        var expectedClient = new DicomClient("localhost", 104, false, "CALLING", "CALLED");

        _innerManagerMock
            .Setup(x => x.CreateAssociationAsync(
                It.IsAny<DicomDestination>(),
                It.IsAny<List<DicomNetwork.DicomPresentationContext>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClient);

        _innerManagerMock
            .Setup(x => x.CloseAssociationAsync(It.IsAny<DicomClient>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Acquire and release
        var client = await pool.AcquireAssociationAsync(destination, new List<DicomNetwork.DicomPresentationContext>(), CancellationToken.None);
        await pool.ReleaseAssociationAsync(client, CancellationToken.None);

        // Should be able to acquire again
        var client2 = await pool.AcquireAssociationAsync(destination, new List<DicomNetwork.DicomPresentationContext>(), CancellationToken.None);

        // Assert
        client2.Should().NotBeNull();
    }

    [Fact]
    public async Task ShutdownAsync_ShouldReleaseAllAssociations()
    {
        // Arrange
        var pool = new DicomAssociationPool(_innerManagerMock.Object, _loggerMock.Object);
        var destination = new DicomDestination { AeTitle = "PACS", Host = "localhost", Port = 104 };

        var expectedClient = new DicomClient("localhost", 104, false, "CALLING", "CALLED");

        _innerManagerMock
            .Setup(x => x.CreateAssociationAsync(
                It.IsAny<DicomDestination>(),
                It.IsAny<List<DicomNetwork.DicomPresentationContext>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClient);

        _innerManagerMock
            .Setup(x => x.CloseAssociationAsync(It.IsAny<DicomClient>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Acquire multiple associations
        var associations = new List<DicomClient>();
        for (int i = 0; i < 3; i++)
        {
            var client = await pool.AcquireAssociationAsync(destination, new List<DicomNetwork.DicomPresentationContext>(), CancellationToken.None);
            associations.Add(client);
        }

        await pool.ShutdownAsync();

        // Assert
        pool.PoolCount.Should().Be(0);
    }
}
