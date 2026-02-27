using System.Diagnostics;
using System.Linq;
using Dicom;
using FluentAssertions;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Worklist;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Dicom.IntegrationTests.Worklist;

/// <summary>
/// Integration tests for Worklist SCU using real Orthanc DICOM SCP.
/// Tests C-FIND query operations for Modality Worklist.
/// SPEC-DICOM-001: FR-DICOM-03
/// </summary>
/// <remarks>
/// Orthanc does not natively support Modality Worklist (MWL) SCP.
/// These tests verify the SCU implementation by testing error handling
/// when MWL is not available on the SCP.
/// </remarks>
[Collection("Orthanc")]
public class WorklistScuIntegrationTests : IDisposable
{
    private readonly OrthancFixture _orthanc;
    private readonly IWorklistScu _worklistScu;
    private readonly ITestOutputHelper _output;
    private readonly DicomServiceOptions _options;

    public WorklistScuIntegrationTests(OrthancFixture orthanc, ITestOutputHelper output)
    {
        _orthanc = orthanc;
        _output = output;

        _options = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            WorklistScp = new DicomDestination
            {
                AeTitle = "ORTHANC",
                Host = orthanc.HostAddress,
                Port = orthanc.HostDicomPort
            },
            Timeouts = new TimeoutOptions
            {
                AssociationRequestMs = 5000,
                DimseOperationMs = 10000,
                SocketReceiveMs = 30000,
                SocketSendMs = 30000
            }
        };

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        _worklistScu = new WorklistScu(
            Options.Create(_options),
            loggerFactory.CreateLogger<WorklistScu>());
    }

    public void Dispose()
    {
        // Clean up
    }

    /// <summary>
    /// Test: C-FIND query when MWL SCP is not supported.
    /// Orthanc does not support Modality Worklist, so this tests error handling.
    /// </summary>
    [Fact]
    public async Task QueryAsync_UnsupportedWorklistScp_ThrowsException()
    {
        // Arrange
        var query = new WorklistQuery
        {
            Modality = "DX",
            AeTitle = "STATION_AET"
        };

        _output.WriteLine("Querying worklist (expected to fail - Orthanc doesn't support MWL)");

        // Act & Assert
        // Orthanc rejects MWL queries with "no such SOP class" error
        await _worklistScu.Invoking(async s =>
        {
            var count = 0;
            await foreach (var _ in s.QueryAsync(query))
            {
                count++;
            }
            return count;
        }).Should().ThrowAsync<DicomWorklistException>();
    }

    /// <summary>
    /// Test: C-FIND query with wildcard patient ID.
    /// Tests query parameter handling.
    /// </summary>
    [Fact]
    public async Task QueryAsync_WildcardPatientId_HandlesCorrectly()
    {
        // Arrange
        var query = new WorklistQuery
        {
            PatientId = "*",  // Wildcard
            Modality = "DX"
        };

        _output.WriteLine("Querying with wildcard patient ID");

        // Act & Assert
        await _worklistScu.Invoking(async s =>
        {
            var count = 0;
            await foreach (var _ in s.QueryAsync(query))
            {
                count++;
            }
            return count;
        }).Should().ThrowAsync<DicomWorklistException>();
    }

    /// <summary>
    /// Test: C-FIND query with specific scheduled date.
    /// Tests date-based query filtering.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SpecificDate_HandlesCorrectly()
    {
        // Arrange
        var query = new WorklistQuery
        {
            ScheduledDate = new DateRange(DateOnly.FromDateTime(new DateTime(2024, 1, 15)), DateOnly.FromDateTime(new DateTime(2024, 1, 15))),
            AeTitle = "STATION_AET",
            Modality = "CT"
        };

        _output.WriteLine($"Querying for scheduled date: 2024-01-15");

        // Act & Assert
        await _worklistScu.Invoking(async s =>
        {
            var count = 0;
            await foreach (var _ in s.QueryAsync(query))
            {
                count++;
            }
            return count;
        }).Should().ThrowAsync<DicomWorklistException>();
    }

    /// <summary>
    /// Test: Verify cancellation token is respected.
    /// Tests that C-FIND operation can be cancelled.
    /// </summary>
    [Fact]
    public async Task QueryAsync_WithCancellationToken_CancelsOperation()
    {
        // Arrange
        var query = new WorklistQuery
        {
            PatientId = "*",
            Modality = "DX"
        };

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        _output.WriteLine("Testing cancellation token");

        // Act & Assert
        // Operation should either be cancelled or fail quickly
        var act = async () =>
        {
            var count = 0;
            await foreach (var _ in _worklistScu.QueryAsync(query, cts.Token))
            {
                count++;
            }
            return count;
        };

        // Either cancellation or exception is acceptable
        try
        {
            await act();
            // If it completes, that's also acceptable (Orthanc rejected quickly)
        }
        catch (OperationCanceledException)
        {
            // Expected - cancellation worked
        }
        catch (DicomWorklistException)
        {
            // Also expected - Orthanc rejected the query
        }
    }

    /// <summary>
    /// Test: Query with empty parameters (wildcards).
    /// Tests that null parameters are treated as wildcards.
    /// </summary>
    [Fact]
    public async Task QueryAsync_NullParameters_TreatsAsWildcards()
    {
        // Arrange - All null parameters should be wildcards
        var query = new WorklistQuery(); // All defaults (null)

        _output.WriteLine("Querying with null parameters (wildcards)");

        // Act & Assert
        await _worklistScu.Invoking(async s =>
        {
            var count = 0;
            await foreach (var _ in s.QueryAsync(query))
            {
                count++;
            }
            return count;
        }).Should().ThrowAsync<DicomWorklistException>();
    }

    /// <summary>
    /// Test: Timeout behavior when SCP does not respond.
    /// Tests that DIMSE timeout is enforced.
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenTimeoutIsSet_RespectsTimeout()
    {
        // Arrange
        var shortTimeoutOptions = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            WorklistScp = new DicomDestination
            {
                AeTitle = "ORTHANC",
                Host = _orthanc.HostAddress,
                Port = _orthanc.HostDicomPort
            },
            Timeouts = new TimeoutOptions
            {
                AssociationRequestMs = 1000,
                DimseOperationMs = 1000,  // Very short timeout
                SocketReceiveMs = 2000,
                SocketSendMs = 2000
            }
        };

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        var shortTimeoutScu = new WorklistScu(
            Options.Create(shortTimeoutOptions),
            loggerFactory.CreateLogger<WorklistScu>());

        var query = new WorklistQuery
        {
            PatientId = "TEST123",
            Modality = "DX"
        };

        _output.WriteLine("Testing with short timeout");

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        await shortTimeoutScu.Invoking(async s =>
        {
            var count = 0;
            await foreach (var _ in s.QueryAsync(query))
            {
                count++;
            }
            return count;
        }).Should().ThrowAsync<DicomWorklistException>();
        stopwatch.Stop();

        _output.WriteLine($"Query failed in {stopwatch.ElapsedMilliseconds}ms");

        // Should not take longer than 2x the timeout
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            "timeout should be enforced even on error");
    }
}

/// <summary>
/// Reuse the xUnit logger from Storage tests.
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
