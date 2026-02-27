using FluentAssertions;
using HnVue.Dicom.StorageCommit;
using Moq;
using Xunit;

namespace HnVue.Dicom.Tests.StorageCommit;

/// <summary>
/// Unit tests for IStorageCommitScu - DICOM Storage Commitment N-ACTION/N-EVENT-REPORT operations.
/// SPEC-DICOM-001 AC-05: Storage Commitment.
/// </summary>
public class StorageCommitScuTests
{
    private readonly Mock<IStorageCommitScu> _commitScu;

    public StorageCommitScuTests()
    {
        _commitScu = new Mock<IStorageCommitScu>();
    }

    // AC-05 Scenario 5.1 - All Images Committed Successfully
    [Fact]
    public async Task RequestCommitAsync_ValidSopInstances_ReturnsTransactionUid()
    {
        // Arrange
        var sopInstances = new[]
        {
            ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.100"),
            ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.101"),
            ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.102")
        };

        var expectedTransactionUid = "1.2.3.4.5.9001";

        _commitScu
            .Setup(s => s.RequestCommitAsync(
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTransactionUid);

        // Act
        var transactionUid = await _commitScu.Object.RequestCommitAsync(sopInstances);

        // Assert
        transactionUid.Should().NotBeNullOrEmpty(
            "a Transaction UID must be returned for tracking the commitment request");
        transactionUid.Should().Be(expectedTransactionUid);
    }

    // AC-05 Scenario 5.1 - N-EVENT-REPORT: CommitmentReceived event is raised
    [Fact]
    public void CommitmentReceived_Event_FiredOnResponse()
    {
        // Arrange
        CommitmentReceivedEventArgs? receivedArgs = null;
        var eventFired = false;

        _commitScu.Object.CommitmentReceived += (sender, args) =>
        {
            eventFired = true;
            receivedArgs = args;
        };

        var transactionUid = "1.2.3.4.5.9001";
        var successfulInstances = new (string SopClassUid, string SopInstanceUid)[]
        {
            ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.100")
        }.ToList().AsReadOnly();

        var failedInstances = new (string SopClassUid, string SopInstanceUid, ushort FailureReason)[]
        {
        }.ToList().AsReadOnly();

        var args = new CommitmentReceivedEventArgs(transactionUid, successfulInstances, failedInstances);

        // Act: raise the event using Moq's event simulation
        _commitScu.Raise(s => s.CommitmentReceived += null, args);

        // Assert
        eventFired.Should().BeTrue("CommitmentReceived event must be raised when N-EVENT-REPORT arrives");
        receivedArgs.Should().NotBeNull();
        receivedArgs!.TransactionUid.Should().Be(transactionUid);
        receivedArgs.CommittedItems.Should().HaveCount(1);
        receivedArgs.FailedItems.Should().BeEmpty();
    }

    // AC-05 Scenario 5.2 - Partial Commitment Failure Triggers Re-transmission
    [Fact]
    public void CommitmentReceived_WithPartialFailure_ExposesFailedItems()
    {
        // Arrange
        CommitmentReceivedEventArgs? receivedArgs = null;

        _commitScu.Object.CommitmentReceived += (sender, args) =>
        {
            receivedArgs = args;
        };

        var transactionUid = "1.2.3.4.5.9002";
        var successfulInstances = new (string SopClassUid, string SopInstanceUid)[]
        {
            ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.100")
        }.ToList().AsReadOnly();

        var failedInstances = new (string SopClassUid, string SopInstanceUid, ushort FailureReason)[]
        {
            ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.101", 0x0110) // Processing failure
        }.ToList().AsReadOnly();

        var args = new CommitmentReceivedEventArgs(transactionUid, successfulInstances, failedInstances);

        // Act
        _commitScu.Raise(s => s.CommitmentReceived += null, args);

        // Assert
        receivedArgs.Should().NotBeNull();
        receivedArgs!.FailedItems.Should().HaveCount(1,
            "partial failure must expose the failed SOP instances for re-transmission");
        receivedArgs.CommittedItems.Should().HaveCount(1,
            "successfully committed instances must still be listed");
        receivedArgs.FailedItems[0].SopInstanceUid.Should().Be("1.2.3.4.5.101");
    }

    // AC-05 Scenario 5.3 - Commitment Timeout
    [Fact]
    public async Task RequestCommitAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sopInstances = new[] { ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.200") };

        _commitScu
            .Setup(s => s.RequestCommitAsync(
                It.IsAny<IEnumerable<(string, string)>>(),
                It.Is<CancellationToken>(t => t.IsCancellationRequested)))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        Func<Task> act = () => _commitScu.Object.RequestCommitAsync(sopInstances, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "timeout must be surfaced as OperationCanceledException for operator notification");
    }

    // DicomStorageCommitException carries status code
    [Fact]
    public void DicomStorageCommitException_WithStatusCode_StoresStatusCodeCorrectly()
    {
        // Arrange & Act
        var exception = new DicomStorageCommitException(0xA700, "N-ACTION rejected by SCP");

        // Assert
        exception.StatusCode.Should().Be(0xA700);
        exception.Message.Should().Be("N-ACTION rejected by SCP");
    }

    // RequestCommitAsync throws DicomStorageCommitException when N-ACTION is rejected
    [Fact]
    public async Task RequestCommitAsync_WhenScpRejectsNAction_ThrowsDicomStorageCommitException()
    {
        // Arrange
        var sopInstances = new[] { ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.300") };
        var failureStatusCode = (ushort)0xA700;

        _commitScu
            .Setup(s => s.RequestCommitAsync(
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DicomStorageCommitException(failureStatusCode, "SCP rejected N-ACTION"));

        // Act & Assert
        Func<Task> act = () => _commitScu.Object.RequestCommitAsync(sopInstances);

        await act.Should().ThrowAsync<DicomStorageCommitException>()
            .Where(ex => ex.StatusCode == failureStatusCode);
    }

    // CommitmentReceivedEventArgs correctly stores all fields
    [Fact]
    public void CommitmentReceivedEventArgs_WithAllFields_StoresCorrectly()
    {
        // Arrange
        var transactionUid = "1.2.3.4.5.9003";
        var committed = new[] { ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.400") }.ToList().AsReadOnly();
        var failed = new[] { ("1.2.840.10008.5.1.4.1.1.1.1", "1.2.3.4.5.401", (ushort)0x0110) }.ToList().AsReadOnly();

        // Act
        var args = new CommitmentReceivedEventArgs(transactionUid, committed, failed);

        // Assert
        args.TransactionUid.Should().Be(transactionUid);
        args.CommittedItems.Should().HaveCount(1);
        args.FailedItems.Should().HaveCount(1);
        args.FailedItems[0].FailureReason.Should().Be(0x0110);
    }
}
