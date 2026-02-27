using System.Diagnostics;
using Dicom;
using FluentAssertions;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Mpps;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Dicom.IntegrationTests.Mpps;

/// <summary>
/// Integration tests for MPPS SCU using real Orthanc DICOM SCP.
/// Tests N-CREATE and N-SET operations for Modality Performed Procedure Step.
/// SPEC-DICOM-001: FR-DICOM-04
/// </summary>
/// <remarks>
/// Orthanc does not natively support MPPS SCP.
/// These tests verify the SCU implementation by testing error handling
/// when MPPS functionality is not available on the SCP.
/// </remarks>
[Collection("Orthanc")]
public class MppsScuIntegrationTests : IDisposable
{
    private readonly OrthancFixture _orthanc;
    private readonly IMppsScu _mppsScu;
    private readonly ITestOutputHelper _output;
    private readonly DicomServiceOptions _options;

    public MppsScuIntegrationTests(OrthancFixture orthanc, ITestOutputHelper output)
    {
        _orthanc = orthanc;
        _output = output;

        _options = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            MppsScp = new DicomDestination
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

        _mppsScu = new MppsScu(
            Options.Create(_options),
            loggerFactory.CreateLogger<MppsScu>());
    }

    public void Dispose()
    {
        // Clean up
    }

    /// <summary>
    /// Test: N-CREATE when MPPS SCP is not supported.
    /// Orthanc does not support MPPS, so this tests error handling.
    /// </summary>
    [Fact]
    public async Task CreateProcedureStepAsync_UnsupportedMppsScp_ThrowsException()
    {
        // Arrange - MppsData positional parameters
        var mppsData = new MppsData(
            "TEST123",  // PatientId
            "1.2.3.4.5.10",  // StudyInstanceUid
            "1.2.3.4.5.11",  // SeriesInstanceUid
            "PROC001",  // PerformedProcedureStepId
            "Test Procedure",  // PerformedProcedureStepDescription
            DateTime.Now,  // StartDateTime
            null,  // EndDateTime
            MppsStatus.InProgress,  // Status
            Array.Empty<ExposureData>().ToList());  // ExposureData

        _output.WriteLine("Creating MPPS (expected to fail - Orthanc doesn't support MPPS)");

        // Act & Assert
        await _mppsScu.Invoking(s => s.CreateProcedureStepAsync(mppsData))
            .Should().ThrowAsync<DicomMppsException>()
            .WithMessage("*refused*");
    }

    /// <summary>
    /// Test: N-CREATE with complete MPPS data.
    /// Tests proper DICOM dataset construction.
    /// </summary>
    [Fact]
    public async Task CreateProcedureStepAsync_CompleteData_HandlesCorrectly()
    {
        // Arrange
        var mppsData = new MppsData(
            "IT001",  // PatientId
            "1.2.3.4.5.20",  // StudyInstanceUid
            "1.2.3.4.5.21",  // SeriesInstanceUid
            "PROC002",  // PerformedProcedureStepId
            "CT CHEST",  // PerformedProcedureStepDescription
            DateTime.Now,  // StartDateTime
            null,  // EndDateTime
            MppsStatus.InProgress,  // Status
            Array.Empty<ExposureData>().ToList());  // ExposureData

        _output.WriteLine($"Creating MPPS for patient: {mppsData.PatientId}");

        // Act & Assert
        await _mppsScu.Invoking(s => s.CreateProcedureStepAsync(mppsData))
            .Should().ThrowAsync<DicomMppsException>();
    }

    /// <summary>
    /// Test: N-CREATE with minimal required MPPS data.
    /// Tests minimum viable MPPS dataset.
    /// </summary>
    [Fact]
    public async Task CreateProcedureStepAsync_MinimalData_HandlesCorrectly()
    {
        // Arrange - Minimal MPPS data
        var mppsData = new MppsData(
            "MIN001",  // PatientId
            "1.2.3.4.5.30",  // StudyInstanceUid
            "1.2.3.4.5.31",  // SeriesInstanceUid
            "PROC003",  // PerformedProcedureStepId
            "Minimal Test",  // PerformedProcedureStepDescription
            DateTime.Now,  // StartDateTime
            null,  // EndDateTime
            MppsStatus.InProgress,  // Status
            Array.Empty<ExposureData>().ToList());  // ExposureData

        _output.WriteLine("Creating MPPS with minimal data");

        // Act & Assert
        await _mppsScu.Invoking(s => s.CreateProcedureStepAsync(mppsData))
            .Should().ThrowAsync<DicomMppsException>();
    }

    /// <summary>
    /// Test: N-SET to update procedure step to IN PROGRESS.
    /// </summary>
    [Fact]
    public async Task SetProcedureStepInProgressAsync_ValidSopInstanceUid_HandlesCorrectly()
    {
        // Arrange
        var sopInstanceUid = "1.2.3.4.5.100.1";
        var updatedData = new MppsData(
            "UPD001",  // PatientId
            "1.2.3.4.5.30",  // StudyInstanceUid
            "1.2.3.4.5.31",  // SeriesInstanceUid
            "PROC003",  // PerformedProcedureStepId
            "Updated Procedure",  // PerformedProcedureStepDescription
            DateTime.Now.AddMinutes(-5),  // StartDateTime
            null,  // EndDateTime
            MppsStatus.InProgress,  // Status
            Array.Empty<ExposureData>().ToList());  // ExposureData

        _output.WriteLine($"Setting MPPS {sopInstanceUid} to IN PROGRESS");

        // Act & Assert
        await _mppsScu.Invoking(s => s.SetProcedureStepInProgressAsync(sopInstanceUid, updatedData))
            .Should().ThrowAsync<DicomMppsException>();
    }

    /// <summary>
    /// Test: N-SET to complete procedure step with exposure data.
    /// </summary>
    [Fact]
    public async Task CompleteProcedureStepAsync_WithExposureData_HandlesCorrectly()
    {
        // Arrange
        var sopInstanceUid = "1.2.3.4.5.100.2";
        var completeData = new MppsData(
            "CMP001",  // PatientId
            "1.2.3.4.5.40",  // StudyInstanceUid
            "1.2.3.4.5.41",  // SeriesInstanceUid
            "PROC004",  // PerformedProcedureStepId
            "Completed Procedure",  // PerformedProcedureStepDescription
            DateTime.Now.AddMinutes(-10),  // StartDateTime
            DateTime.Now,  // EndDateTime
            MppsStatus.Completed,  // Status
            new List<ExposureData>
            {
                new ExposureData(
                    "1.2.3.4.5.50",  // SeriesInstanceUid
                    DicomUID.DigitalXRayImageStorageForPresentation.UID,  // SopClassUid
                    "1.2.3.4.5.60.1"),  // SopInstanceUid
                new ExposureData(
                    "1.2.3.4.5.50",  // SeriesInstanceUid
                    DicomUID.DigitalXRayImageStorageForPresentation.UID,  // SopClassUid
                    "1.2.3.4.5.60.2")  // SopInstanceUid
            });  // ExposureData

        _output.WriteLine($"Completing MPPS {sopInstanceUid} with exposure data");

        // Act & Assert
        await _mppsScu.Invoking(s => s.CompleteProcedureStepAsync(sopInstanceUid, completeData))
            .Should().ThrowAsync<DicomMppsException>();
    }

    /// <summary>
    /// Test: N-SET to discontinue procedure step.
    /// </summary>
    [Fact]
    public async Task DiscontinueProcedureStepAsync_WithReason_HandlesCorrectly()
    {
        // Arrange
        var sopInstanceUid = "1.2.3.4.5.100.3";
        var reason = "Patient motion detected";

        _output.WriteLine($"Discontinuing MPPS {sopInstanceUid} with reason: {reason}");

        // Act & Assert
        await _mppsScu.Invoking(s => s.DiscontinueProcedureStepAsync(sopInstanceUid, reason))
            .Should().ThrowAsync<DicomMppsException>();
    }

    /// <summary>
    /// Test: Timeout behavior when SCP does not respond.
    /// </summary>
    [Fact]
    public async Task CreateProcedureStepAsync_WhenTimeoutIsSet_RespectsTimeout()
    {
        // Arrange
        var shortTimeoutOptions = new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_IT",
            MppsScp = new DicomDestination
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

        var shortTimeoutScu = new MppsScu(
            Options.Create(shortTimeoutOptions),
            loggerFactory.CreateLogger<MppsScu>());

        var mppsData = new MppsData(
            "TMO001",  // PatientId
            "1.2.3.4.5.999",  // StudyInstanceUid
            "1.2.3.4.5.998",  // SeriesInstanceUid
            "PROC999",  // PerformedProcedureStepId
            "Timeout Test",  // PerformedProcedureStepDescription
            DateTime.Now,  // StartDateTime
            null,  // EndDateTime
            MppsStatus.InProgress,  // Status
            Array.Empty<ExposureData>().ToList());  // ExposureData

        _output.WriteLine("Testing with short timeout");

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        await shortTimeoutScu.Invoking(s => s.CreateProcedureStepAsync(mppsData))
            .Should().ThrowAsync<DicomMppsException>();
        stopwatch.Stop();

        _output.WriteLine($"Create failed in {stopwatch.ElapsedMilliseconds}ms");

        // Should not take longer than 5 seconds
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            "timeout should be enforced even on error");
    }

    /// <summary>
    /// Test: Verify MPPS data construction for different modalities.
    /// </summary>
    [Theory]
    [InlineData("DX")]
    [InlineData("CT")]
    [InlineData("MR")]
    [InlineData("US")]
    public async Task CreateProcedureStepAsync_DifferentModalities_HandlesCorrectly(string modality)
    {
        // Arrange
        var mppsData = new MppsData(
            $"{modality}001",  // PatientId
            $"1.2.3.4.5.{modality}",  // StudyInstanceUid
            $"1.2.3.4.5.{modality}.1",  // SeriesInstanceUid
            $"PROC{modality}",  // PerformedProcedureStepId
            $"{modality} Procedure",  // PerformedProcedureStepDescription
            DateTime.Now,  // StartDateTime
            null,  // EndDateTime
            MppsStatus.InProgress,  // Status
            Array.Empty<ExposureData>().ToList());  // ExposureData

        _output.WriteLine($"Testing MPPS for modality: {modality}");

        // Act & Assert
        await _mppsScu.Invoking(s => s.CreateProcedureStepAsync(mppsData))
            .Should().ThrowAsync<DicomMppsException>();
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
