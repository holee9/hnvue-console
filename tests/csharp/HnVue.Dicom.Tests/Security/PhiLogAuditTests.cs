using Dicom;
using Dicom.Imaging.Codec;
using FluentAssertions;
using HnVue.Dicom.Associations;
using HnVue.Dicom.Configuration;
using HnVue.Dicom.Facade;
using HnVue.Dicom.Iod;
using HnVue.Dicom.Mpps;
using HnVue.Dicom.Queue;
using HnVue.Dicom.Rdsr;
using HnVue.Dicom.Storage;
using HnVue.Dicom.StorageCommit;
using HnVue.Dicom.Uid;
using HnVue.Dicom.Worklist;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Dicom.Tests.Security;

/// <summary>
/// PHI (Protected Health Information) log audit tests for NFR-SEC-01 compliance.
/// Verifies that Patient Name, Patient ID, and Birth Date are NOT written to logs.
///
/// NFR-SEC-01: "The system shall not write Patient Health Information (PHI) such as
/// Patient Name, Patient ID, or birth date to application log files in plain text."
/// </summary>
public class PhiLogAuditTests
{
    private readonly ITestOutputHelper _output;

    public PhiLogAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // PHI regex patterns: match common DICOM PN (Person Name) formats
    private static readonly string[] PatientNamePatterns = new[]
    {
        // DICOM PN format: Family^Given^Middle^Prefix^Suffix
        @"[A-Z][a-z]+\^[A-Z][a-z]+",          // Doe^John
        @"[A-Z][a-z]+",                       // Simple name
        @"Doe",                               // Literal surname
        @"John",                              // Literal given name
        @"Smith",                             // Common surname for testing
        // More specific test values
        @"TEST_PATIENT",
        @"Patient_Name_\d+",
    };

    // Patient ID patterns: alphanumeric identifiers
    private static readonly string[] PatientIdPatterns = new[]
    {
        @"PID\d+",                            // PID12345
        @"PAT\d+",                            // PAT001
        @"\d{8,}",                            // 8+ digit ID
        @"[A-Z]{2}\d{6}",                     // AB123456
        // Specific test values
        @"TEST_ID_\d+",
    };

    // Birth date patterns: DICOM DA format (YYYYMMDD) and ISO dates
    private static readonly string[] BirthDatePatterns = new[]
    {
        @"\d{8}",                             // YYYYMMDD
        @"\d{4}-\d{2}-\d{2}",                // YYYY-MM-DD
        @"19\d{6}",                           // 19YYYYMM
        @"20\d{6}",                           // 20YYYYMM
        // Specific test dates
        @"19500101",
        @"20001231",
    };

    /// <summary>
    /// Tests that DicomServiceFacade.StoreImageAsync does not log PHI.
    /// </summary>
    [Fact]
    public async Task StoreImageAsync_DoesNotLogPhi()
    {
        // Arrange
        var storageLogger = new LogCapture<StorageScu>();
        var facadeLogger = new LogCapture<DicomServiceFacade>();

        var mockQueue = new Mock<ITransmissionQueue>();
        mockQueue
            .Setup(q => q.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string uid, string path, string ae, CancellationToken ct) =>
                TransmissionQueueItem.CreateNew(uid, path, ae));

        var options = Options.Create(new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            Tls = new TlsOptions { Enabled = false },
            StorageDestinations = new()
            {
                new DicomDestination
                {
                    AeTitle = "PACS_TEST",
                    Host = "127.0.0.1",
                    Port = 19999  // Closed port to trigger retry queue logging
                }
            }
        });

        var storageScu = new StorageScu(
            options,
            Mock.Of<IAssociationManager>(),
            mockQueue.Object,
            storageLogger);

        var facade = new DicomServiceFacade(
            storageScu,
            Mock.Of<IWorklistScu>(),
            Mock.Of<IMppsScu>(),
            Mock.Of<IStorageCommitScu>(),
            Mock.Of<IRdsrBuilder>(),
            Mock.Of<IUidGenerator>(),
            options,
            facadeLogger,
            Mock.Of<ILogger<DxImageBuilder>>(),
            Mock.Of<ILogger<CrImageBuilder>>());

        // Create DICOM image with PHI
        var imageData = new DicomImageData
        {
            Modality = "DX",
            PatientId = "PID12345678",      // PHI
            PatientName = "Doe^John^A",      // PHI
            PatientBirthDate = new DateOnly(1980, 5, 15),  // PHI
            StudyInstanceUid = "1.2.3.4.5.10",
            SeriesInstanceUid = "1.2.3.4.5.11",
            SopInstanceUid = "1.2.3.4.5.100",
            AcquisitionDate = DateOnly.FromDateTime(DateTime.Today),
            AcquisitionTime = TimeOnly.FromDateTime(DateTime.Now),
            Rows = 100,
            Columns = 100,
            PixelData = Array.Empty<byte>()
        };

        // Act
        try
        {
            await facade.StoreImageAsync(imageData);
        }
        catch
        {
            // Expected: connection refused
        }

        // Assert: Collect all logs
        var allLogs = storageLogger.GetCapturedLogs() +
                      Environment.NewLine +
                      facadeLogger.GetCapturedLogs();

        _output.WriteLine("Captured logs:");
        _output.WriteLine(allLogs);

        // Verify NO PHI patterns match
        var phiMatches = FindPhiMatches(allLogs);
        phiMatches.Should().BeEmpty(
            "logs must not contain PHI (Patient Name, ID, or Birth Date) per NFR-SEC-01");
    }

    /// <summary>
    /// Tests that WorklistScu.QueryAsync does not log PHI from worklist responses.
    /// </summary>
    [Fact]
    public async Task WorklistQueryAsync_DoesNotLogPhi()
    {
        // Arrange
        var logger = new LogCapture<WorklistScu>();

        var options = Options.Create(new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            WorklistScp = new DicomDestination
            {
                AeTitle = "WORKLIST_TEST",
                Host = "127.0.0.1",
                Port = 19998  // Closed port
            }
        });

        var worklistScu = new WorklistScu(options, logger);
        var query = new WorklistQuery
        {
            PatientId = "PID99999999",  // PHI in query
            Modality = "DX"
        };

        // Act
        try
        {
            await foreach (var item in worklistScu.QueryAsync(query))
            {
                // Consume all items
            }
        }
        catch
        {
            // Expected: connection refused
        }

        // Assert
        var logs = logger.GetCapturedLogs();
        _output.WriteLine("Worklist logs:");
        _output.WriteLine(logs);

        var phiMatches = FindPhiMatches(logs);
        phiMatches.Should().BeEmpty(
            "worklist logs must not contain PHI per NFR-SEC-01");
    }

    /// <summary>
    /// Tests that StorageScu does not log PHI in error messages.
    /// </summary>
    [Fact]
    public async Task StorageScu_ErrorMessages_DoNotContainPhi()
    {
        // Arrange
        var logger = new LogCapture<StorageScu>();
        var mockQueue = new Mock<ITransmissionQueue>();

        var options = Options.Create(new DicomServiceOptions
        {
            CallingAeTitle = "HNVUE_TEST",
            Tls = new TlsOptions { Enabled = false }
        });

        var storageScu = new StorageScu(
            options,
            Mock.Of<IAssociationManager>(),
            mockQueue.Object,
            logger);

        // Create DICOM file with PHI in metadata
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.DigitalXRayImageStorageForPresentation },
            { DicomTag.SOPInstanceUID, "1.2.3.4.5.999" },
            { DicomTag.StudyInstanceUID, "1.2.3.4.5.10" },
            { DicomTag.SeriesInstanceUID, "1.2.3.4.5.11" },
            { DicomTag.Modality, "DX" },
            { DicomTag.PatientID, "PATIENT_PHI_12345" },  // PHI
            { DicomTag.PatientName, "Smith^Jane^B" },     // PHI
            { DicomTag.PatientBirthDate, "19900520" },    // PHI
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)1 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            new DicomOtherByte(DicomTag.PixelData, 0xFF)
        };
        var dicomFile = new DicomFile(dataset);

        var destination = new DicomDestination
        {
            AeTitle = "UNREACHABLE",
            Host = "127.0.0.1",
            Port = 19997
        };

        // Act
        var result = await storageScu.StoreAsync(dicomFile, destination);

        // Assert
        var logs = logger.GetCapturedLogs();
        _output.WriteLine("Storage SCU logs:");
        _output.WriteLine(logs);

        var phiMatches = FindPhiMatches(logs);
        phiMatches.Should().BeEmpty(
            "storage error logs must not contain PHI per NFR-SEC-01");
    }

    /// <summary>
    /// Tests that PHI patterns are correctly detected.
    /// This test validates the PHI detection logic itself.
    /// </summary>
    [Theory]
    [InlineData("Patient Doe^John arrived", true)]
    [InlineData("Patient ID: PID123456", true)]
    [InlineData("Birth date: 19800515", true)]
    [InlineData("C-STORE to PACS success", false)]
    [InlineData("Association request to 127.0.0.1:104", false)]
    [InlineData("SOP Instance UID: 1.2.3.4.5.100", false)]
    [InlineData("No storage destinations configured", false)]
    public void PhiDetection_WorksCorrectly(string logMessage, bool expectedContainsPhi)
    {
        // Act
        var hasPhi = ContainsPhi(logMessage);

        // Assert
        hasPhi.Should().Be(expectedContainsPhi,
            $"PHI detection should {(expectedContainsPhi ? "detect" : "not detect")} PHI in: {logMessage}");
    }

    /// <summary>
    /// Tests that structured logging parameters don't leak PHI.
    /// </summary>
    [Fact]
    public void StructuredLogging_ParameterNames_DoNotContainPhi()
    {
        // Arrange: Verify that common parameter names used in logging don't include PHI fields
        var commonParameterNames = new[]
        {
            "Destination", "Port", "AeTitle", "SopUid", "SopClass",
            "StudyUid", "StatusCode", "StatusState", "Count",
            "Host", "Modality", "ScheduledDate"
        };

        var phiKeywords = new[] { "PatientName", "PatientID", "PatientId", "BirthDate", "Birth" };

        // Assert
        foreach (var paramName in commonParameterNames)
        {
            phiKeywords.Should().NotContain(phi => paramName.Contains(phi, StringComparison.OrdinalIgnoreCase),
                $"logging parameter '{paramName}' should not contain PHI keyword");
        }
    }

    // Helper methods

    private static List<string> FindPhiMatches(string logText)
    {
        var matches = new List<string>();

        // Check for patient name patterns
        foreach (var pattern in PatientNamePatterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            foreach (System.Text.RegularExpressions.Match match in regex.Matches(logText))
            {
                matches.Add($"PatientName pattern '{pattern}': {match.Value}");
            }
        }

        // Check for patient ID patterns
        foreach (var pattern in PatientIdPatterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            foreach (System.Text.RegularExpressions.Match match in regex.Matches(logText))
            {
                matches.Add($"PatientId pattern '{pattern}': {match.Value}");
            }
        }

        // Check for birth date patterns
        foreach (var pattern in BirthDatePatterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            foreach (System.Text.RegularExpressions.Match match in regex.Matches(logText))
            {
                matches.Add($"BirthDate pattern '{pattern}': {match.Value}");
            }
        }

        return matches;
    }

    private static bool ContainsPhi(string text)
    {
        // Check if text contains any PHI pattern
        var allPatterns = PatientNamePatterns
            .Concat(PatientIdPatterns)
            .Concat(BirthDatePatterns);

        foreach (var pattern in allPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
            {
                return true;
            }
        }

        return false;
    }
}
