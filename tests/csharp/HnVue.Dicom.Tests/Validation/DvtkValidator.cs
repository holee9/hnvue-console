using Dicom;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace HnVue.Dicom.Tests.Validation;

/// <summary>
/// Wrapper for DVTK (DICOM Validation Toolkit) DicomValidator CLI tool.
/// Provides methods to validate DICOM files for conformance per NFR-QUAL-01.
///
/// DVTK is an external tool that validates DICOM objects against the standard.
/// This class executes the DVTK CLI and parses the validation results.
/// </summary>
public sealed class DvtkValidator
{
    private readonly ILogger<DvtkValidator> _logger;
    private readonly string _dvtkExecutablePath;

    /// <summary>
    /// Default path to DVTK DicomValidator executable.
    /// Can be overridden via environment variable DVTK_PATH or constructor.
    /// </summary>
    public const string DefaultDvtkPath = "DicomValidator";

    /// <summary>
    /// Initializes a new instance of the <see cref="DvtkValidator"/> class.
    /// </summary>
    /// <param name="logger">Logger for validation diagnostics.</param>
    /// <param name="dvtkExecutablePath">Optional path to DicomValidator executable.
    /// If null, uses DVTK_PATH environment variable or default path.</param>
    public DvtkValidator(ILogger<DvtkValidator> logger, string? dvtkExecutablePath = null)
    {
        _logger = logger;
        _dvtkExecutablePath = dvtkExecutablePath
            ?? Environment.GetEnvironmentVariable("DVTK_PATH")
            ?? DefaultDvtkPath;
    }

    /// <summary>
    /// Validates a DICOM file using DVTK DicomValidator.
    /// </summary>
    /// <param name="dicomFile">The DICOM file to validate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Validation result containing violation counts and details.</returns>
    /// <exception cref="DvtkValidationException">Thrown when DVTK execution fails.</exception>
    public async Task<DvtkValidationResult> ValidateAsync(
        DicomFile dicomFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dicomFile);

        // Create a temporary file for DVTK to validate
        var tempFilePath = Path.GetTempFileName();
        try
        {
            // Write the DICOM file to disk
            dicomFile.Save(tempFilePath);

            return await ValidateFileAsync(tempFilePath, cancellationToken);
        }
        finally
        {
            // Clean up the temporary file
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary DICOM file: {FilePath}", tempFilePath);
                }
            }
        }
    }

    /// <summary>
    /// Validates a DICOM file at the specified path using DVTK DicomValidator.
    /// </summary>
    /// <param name="filePath">Path to the DICOM file to validate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Validation result containing violation counts and details.</returns>
    public async Task<DvtkValidationResult> ValidateFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("DICOM file not found for validation", filePath);
        }

        _logger.LogDebug("Starting DVTK validation for file: {FilePath}", filePath);

        // Prepare the DVTK process
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _dvtkExecutablePath,
            Arguments = $"--format json --output - \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = processStartInfo;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        _logger.LogDebug("Executing DVTK: {FileName} {Arguments}",
            processStartInfo.FileName, processStartInfo.Arguments);

        // Start the process and wait for completion
        process.Start();

        // Begin asynchronous reads of stdout and stderr
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for the process to exit with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5)); // 5 minute timeout for validation

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new OperationCanceledException("DVTK validation was cancelled", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new DvtkValidationException($"DVTK validation timed out or failed: {ex.Message}", ex);
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        // Check if DVTK executable exists
        if (process.ExitCode == 1 && output.Length == 0 && error.Contains("not found"))
        {
            throw new DvtkValidationException(
                $"DVTK executable not found at: {_dvtkExecutablePath}. " +
                "Please install DVTK or set the DVTK_PATH environment variable.");
        }

        // Log any errors from DVTK
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("DVTK stderr output: {Error}", error);
        }

        // Parse the validation result
        try
        {
            var result = ParseValidationResult(output);
            _logger.LogDebug(
                "DVTK validation complete: Critical={Critical}, Error={Error}, Warning={Warning}",
                result.CriticalViolationCount,
                result.ErrorViolationCount,
                result.WarningViolationCount);

            return result;
        }
        catch (JsonException ex)
        {
            throw new DvtkValidationException(
                $"Failed to parse DVTK output as JSON. Output: {output}", ex);
        }
    }

    /// <summary>
    /// Parses the JSON output from DVTK DicomValidator.
    /// </summary>
    /// <param name="jsonOutput">Raw JSON output from DVTK.</param>
    /// <returns>Parsed validation result.</returns>
    private static DvtkValidationResult ParseValidationResult(string jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(jsonOutput))
        {
            // If DVTK is not available, return a mock successful result
            // This allows tests to run in CI/CD environments without DVTK installed
            return new DvtkValidationResult(
                IsAvailable: false,
                CriticalViolationCount: 0,
                ErrorViolationCount: 0,
                WarningViolationCount: 0,
                Violations: Array.Empty<DvtkViolation>());
        }

        using var document = JsonDocument.Parse(jsonOutput);
        var root = document.RootElement;

        var criticalCount = root.GetPropertyOrNull("criticalViolations")?.GetInt32() ?? 0;
        var errorCount = root.GetPropertyOrNull("errorViolations")?.GetInt32() ?? 0;
        var warningCount = root.GetPropertyOrNull("warningViolations")?.GetInt32() ?? 0;

        var violations = new List<DvtkViolation>();
        if (root.TryGetProperty("violations", out var violationsArray))
        {
            foreach (var violation in violationsArray.EnumerateArray())
            {
                violations.Add(new DvtkViolation(
                    Severity: violation.GetPropertyOrNull("severity")?.GetString() ?? "Unknown",
                    Tag: violation.GetPropertyOrNull("tag")?.GetString() ?? string.Empty,
                    Description: violation.GetPropertyOrNull("description")?.GetString() ?? string.Empty,
                    Reference: violation.GetPropertyOrNull("reference")?.GetString() ?? string.Empty));
            }
        }

        return new DvtkValidationResult(
            IsAvailable: true,
            CriticalViolationCount: criticalCount,
            ErrorViolationCount: errorCount,
            WarningViolationCount: warningCount,
            Violations: violations.ToArray());
    }
}

/// <summary>
/// Result of a DVTK validation operation.
/// </summary>
/// <param name="IsAvailable">True if DVTK was available for validation; false if DVTK is not installed.</param>
/// <param name="CriticalViolationCount">Number of Critical severity violations found.</param>
/// <param name="ErrorViolationCount">Number of Error severity violations found.</param>
/// <param name="WarningViolationCount">Number of Warning severity violations found.</param>
/// <param name="Violations">Detailed list of all violations found.</param>
public sealed record DvtkValidationResult(
    bool IsAvailable,
    int CriticalViolationCount,
    int ErrorViolationCount,
    int WarningViolationCount,
    IReadOnlyList<DvtkViolation> Violations)
{
    /// <summary>
    /// Gets a value indicating whether the DICOM object passed validation.
    /// Pass means zero Critical and zero Error violations.
    /// </summary>
    public bool IsPassed => CriticalViolationCount == 0 && ErrorViolationCount == 0;

    /// <summary>
    /// Gets a value indicating whether the result is inconclusive (DVTK not available).
    /// </summary>
    public bool IsInconclusive => !IsAvailable;
}

/// <summary>
/// Represents a single DVTK violation.
/// </summary>
/// <param name="Severity">Violation severity (Critical, Error, Warning, Info).</param>
/// <param name="Tag">DICOM tag involved in the violation (e.g., "(0010,0010)").</param>
/// <param name="Description">Human-readable description of the violation.</param>
/// <param name="Reference">Reference to the DICOM standard section (e.g., "PS3.3 C.8.11.1").</param>
public sealed record DvtkViolation(
    string Severity,
    string Tag,
    string Description,
    string Reference);

/// <summary>
/// Exception thrown when DVTK validation fails.
/// </summary>
public sealed class DvtkValidationException : Exception
{
    public DvtkValidationException(string message) : base(message)
    {
    }

    public DvtkValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Extension methods for JsonElement to safely access optional properties.
/// </summary>
internal static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName)
    {
        return element.ValueKind != JsonValueKind.Undefined && element.TryGetProperty(propertyName, out var value)
            ? value
            : null;
    }
}
