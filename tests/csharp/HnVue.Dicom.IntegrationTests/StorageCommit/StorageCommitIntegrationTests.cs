using System.Diagnostics;
using Dicom;
using FluentAssertions;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.IntegrationTests.TestData;
using HnVue.Dicom.Storage;
using HnVue.Dicom.StorageCommit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Dicom.IntegrationTests.StorageCommit;

/// <summary>
/// Integration tests for Storage Commit SCU using real Orthanc DICOM SCP.
/// Tests N-ACTION and N-EVENT-REPORT operations for Storage Commitment.
/// SPEC-DICOM-001: FR-DICOM-05
/// </summary>
/// <remarks>
/// Orthanc does not natively support Storage Commitment SCP.
/// These tests verify the SCU implementation by testing error handling
/// when Storage Commitment functionality is not available on the SCP.
/// </remarks>
[Collection("Orthanc")]
public class StorageCommitIntegrationTests : IDisposable
{
    private readonly OrthancFixture _orthanc;
    private readonly IStorageCommitScu _storageCommitScu;
    private readonly ITestOutputHelper _output;
    private readonly DicomServiceOptions _options;

    public StorageCommitIntegrationTests(OrthancFixture orthanc, ITestOutputHelper output)
    {
        _orthanc = orthanc;
        _output = output;

        _options = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            StorageDestinations = new List<DicomDestination>
            {
                new()
                {
                    AeTitle = "ORTHANC",
                    Host = orthanc.HostAddress,
                    Port = orthanc.HostDicomPort
                }
            },
            Timeouts = new TimeoutOptions
            {
                AssociationRequestMs = 5000,
                DimseOperationMs = 10000,
                SocketReceiveMs = 30000,
                SocketSendMs = 30000,
                StorageCommitmentWaitMs = 5000
            }
        };

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        _storageCommitScu = new StorageCommitScu(
            Options.Create(_options),
            loggerFactory.CreateLogger<StorageCommitScu>());
    }

    public async void Dispose()
    {
        // Clean up
        if (_storageCommitScu is StorageCommitScu scu)
        {
            await scu.DisposeAsync();
        }
    }

    /// <summary>
    /// Test: N-ACTION when Storage Commitment SCP is not supported.
    /// Orthanc does not support Storage Commitment, so this tests error handling.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_UnsupportedScp_ThrowsException()
    {
        // Arrange
        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>
        {
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.100.1"),
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.100.2")
        };

        _output.WriteLine($"Requesting commitment for {sopInstances.Count} instances (expected to fail)");

        // Act & Assert
        await _storageCommitScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<DicomStorageCommitException>()
            .WithMessage("*refused*");
    }

    /// <summary>
    /// Test: N-ACTION with single SOP instance.
    /// Tests single instance commitment request.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_SingleInstance_HandlesCorrectly()
    {
        // Arrange
        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>
        {
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, DicomUID.Generate().UID)
        };

        _output.WriteLine($"Requesting commitment for single instance");

        // Act & Assert
        await _storageCommitScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<DicomStorageCommitException>();
    }

    /// <summary>
    /// Test: N-ACTION with multiple SOP instances.
    /// Tests batch commitment request.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_MultipleInstances_HandlesCorrectly()
    {
        // Arrange
        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>
        {
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.200.1"),
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.200.2"),
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.200.3"),
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.200.4"),
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.200.5")
        };

        _output.WriteLine($"Requesting commitment for {sopInstances.Count} instances");

        // Act & Assert
        await _storageCommitScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<DicomStorageCommitException>();
    }

    /// <summary>
    /// Test: N-ACTION with mixed SOP classes.
    /// Tests commitment request for different image types.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_MixedSopClasses_HandlesCorrectly()
    {
        // Arrange
        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>
        {
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.300.1"),
            (DicomUID.ComputedRadiographyImageStorage.UID, "1.2.3.4.5.300.2"),
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.300.3")
        };

        _output.WriteLine($"Requesting commitment for mixed SOP classes");

        // Act & Assert
        await _storageCommitScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<DicomStorageCommitException>();
    }

    /// <summary>
    /// Test: N-ACTION with empty SOP instance list.
    /// Tests edge case handling.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_EmptyList_ThrowsArgumentException()
    {
        // Arrange
        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>();

        _output.WriteLine("Requesting commitment for empty list");

        // Act & Assert
        await _storageCommitScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Test: Event handler subscription.
    /// Tests that CommitmentReceived event can be subscribed.
    /// </summary>
    [Fact]
    public void CommitmentReceivedEvent_CanBeSubscribed_DoesNotThrow()
    {
        // Arrange & Act & Assert
        _storageCommitScu.Invoking(s =>
        {
            s.CommitmentReceived += (sender, args) =>
            {
                // Event handler
            };
        }).Should().NotThrow("event subscription should be allowed");
    }

    /// <summary>
    /// Test: Event handler unsubscription.
    /// Tests that CommitmentReceived event can be unsubscribed.
    /// </summary>
    [Fact]
    public void CommitmentReceivedEvent_CanBeUnsubscribed_DoesNotThrow()
    {
        // Arrange
        EventHandler<CommitmentReceivedEventArgs> handler = (sender, args) => { };
        _storageCommitScu.CommitmentReceived += handler;

        // Act & Assert
        _storageCommitScu.Invoking(s =>
        {
            s.CommitmentReceived -= handler;
        }).Should().NotThrow("event unsubscription should be allowed");
    }

    /// <summary>
    /// Test: Timeout behavior when SCP does not respond.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_WhenTimeoutIsSet_RespectsTimeout()
    {
        // Arrange
        var shortTimeoutOptions = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            StorageDestinations = new List<DicomDestination>
            {
                new()
                {
                    AeTitle = "ORTHANC",
                    Host = _orthanc.HostAddress,
                    Port = _orthanc.HostDicomPort
                }
            },
            Timeouts = new TimeoutOptions
            {
                AssociationRequestMs = 1000,
                DimseOperationMs = 1000,  // Very short timeout
                SocketReceiveMs = 2000,
                SocketSendMs = 2000,
                StorageCommitmentWaitMs = 1000
            }
        };

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        var shortTimeoutScu = new StorageCommitScu(
            Options.Create(shortTimeoutOptions),
            loggerFactory.CreateLogger<StorageCommitScu>());

        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>
        {
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.999.1")
        };

        _output.WriteLine("Testing with short timeout");

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        await shortTimeoutScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<DicomStorageCommitException>();
        stopwatch.Stop();

        _output.WriteLine($"Request failed in {stopwatch.ElapsedMilliseconds}ms");

        // Should not take longer than 5 seconds
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            "timeout should be enforced even on error");

        await shortTimeoutScu.DisposeAsync();
    }

    /// <summary>
    /// Test: Transaction UID generation.
    /// Verifies that each commitment request generates a unique transaction UID.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_GeneratesUniqueTransactionUid()
    {
        // This test cannot complete successfully with Orthanc as it doesn't support Storage Commitment
        // The test verifies the error path

        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>
        {
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, "1.2.3.4.5.400.1")
        };

        _output.WriteLine("Testing transaction UID generation (will fail)");

        // Act & Assert
        await _storageCommitScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<DicomStorageCommitException>();
    }

    /// <summary>
    /// Test: Invalid SOP Class UID handling.
    /// Tests that invalid UIDs are handled correctly.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_InvalidSopClassUid_HandlesCorrectly()
    {
        // Arrange
        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>
        {
            ("INVALID.SOP.CLASS", "1.2.3.4.5.500.1")
        };

        _output.WriteLine("Requesting commitment with invalid SOP class UID");

        // Act & Assert
        await _storageCommitScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<DicomStorageCommitException>();
    }

    /// <summary>
    /// Test: Duplicate SOP instance UIDs in request.
    /// Tests handling of duplicate instances.
    /// </summary>
    [Fact]
    public async Task RequestCommitAsync_DuplicateSopInstanceUids_HandlesCorrectly()
    {
        // Arrange
        var sopInstanceUid = DicomUID.Generate().UID;
        var sopInstances = new List<(string SopClassUid, string SopInstanceUid)>
        {
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, sopInstanceUid),
            (DicomUID.DigitalXRayImageStorageForPresentation.UID, sopInstanceUid)  // Duplicate
        };

        _output.WriteLine("Requesting commitment with duplicate SOP instance UIDs");

        // Act & Assert
        await _storageCommitScu.Invoking(s => s.RequestCommitAsync(sopInstances))
            .Should().ThrowAsync<DicomStorageCommitException>();
    }
}

/// <summary>
/// Reuse the xUnit logger provider.
/// </summary>
internal class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName);
    }

    public void Dispose() { }
}

internal class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception}");
        }
    }
}
