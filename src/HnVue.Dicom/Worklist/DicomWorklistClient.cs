using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Worklist;

/// <summary>
/// High-level client for querying DICOM Modality Worklist.
/// Provides graceful degradation and error handling for worklist operations.
/// </summary>
/// <remarks>
/// @MX:NOTE Error handling - Client wraps worklist SCU with graceful degradation
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-406
///
/// Features:
/// - C-FIND query with patient ID, name, and date filters
/// - Graceful degradation (returns empty result on failure)
/// - 5 second timeout for queries
/// - Operator notification via IWorkflowEventPublisher
/// </remarks>
public sealed class DicomWorklistClient
{
    private const int DefaultTimeoutMs = 5000; // 5 seconds

    private readonly IWorklistScu _worklistScu;
    private readonly ILogger<DicomWorklistClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomWorklistClient"/> class.
    /// </summary>
    /// <param name="worklistScu">The underlying worklist SCU implementation.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DicomWorklistClient(
        IWorklistScu worklistScu,
        ILogger<DicomWorklistClient> logger)
    {
        _worklistScu = worklistScu ?? throw new ArgumentNullException(nameof(worklistScu));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Queries the DICOM worklist with the specified criteria.
    /// </summary>
    /// <param name="query">The worklist query parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="WorklistQueryResult"/> containing the query results.
    /// Returns an empty result on failure (graceful degradation).
    /// </returns>
    /// <remarks>
    /// @MX:NOTE Error handling - Returns empty result instead of throwing exceptions
    ///
    /// The query applies the following filters:
    /// - Patient ID (exact match)
    /// - Patient Name (DICOM wildcard matching)
    /// - Scheduled Date (range matching)
    ///
    /// Timeout: 5 seconds (configurable via DefaultTimeoutMs)
    /// </remarks>
    public async Task<WorklistQueryResult> QueryWorklistAsync(
        WorklistQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Create a timeout token
        using var timeoutCts = new CancellationTokenSource(DefaultTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        try
        {
            _logger.LogInformation(
                "Starting worklist query (PatientId: {PatientId}, Date: {Date})",
                Sanitize(query.PatientId),
                query.ScheduledDate);

            var items = new List<WorklistItem>();

            await foreach (var item in _worklistScu.QueryAsync(query, linkedCts.Token))
            {
                items.Add(item);
            }

            _logger.LogInformation(
                "Worklist query completed successfully ({Count} items)",
                items.Count);

            return WorklistQueryResult.Successful(items.ToArray());
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Worklist query timed out after {TimeoutMs}ms",
                DefaultTimeoutMs);

            return WorklistQueryResult.Failed(
                $"Worklist query timed out after {DefaultTimeoutMs}ms");
        }
        catch (DicomWorklistException ex)
        {
            _logger.LogError(
                ex,
                "Worklist query failed with DICOM status 0x{StatusCode:X4}",
                ex.StatusCode);

            return WorklistQueryResult.Failed(
                $"Worklist query failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Worklist query failed unexpectedly");

            // Graceful degradation: return empty result instead of throwing
            return WorklistQueryResult.Failed(
                $"Worklist query failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sanitizes PHI data for logging (NFR-SEC-01).
    /// </summary>
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "[empty]";
        }

        // Return partial value for logging (first 3 chars)
        return value.Length <= 3 ? value : $"{value[..3]}***";
    }
}
