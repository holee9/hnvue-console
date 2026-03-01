using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HnVue.Dicom.Common;

/// <summary>
/// Interface for operator notification without creating circular dependency.
/// </summary>
public interface IDicomOperatorNotifier
{
    /// <summary>
    /// Notifies the operator of a DICOM error.
    /// </summary>
    Task NotifyErrorAsync(string title, string message, bool isCritical, CancellationToken cancellationToken = default);
}

/// <summary>
/// Severity levels for operator notifications.
/// </summary>
public enum NotificationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Centralized error handling for DICOM operations.
/// Provides error categorization, operator notification, and graceful degradation.
/// </summary>
/// <remarks>
/// @MX:WARN Error-critical paths - Centralized error handling for all DICOM operations
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-410
///
/// Features:
/// - Centralized error handling
/// - Error categorization
/// - Operator notification via IDicomOperatorNotifier
/// - Graceful degradation
/// </remarks>
public sealed class DicomErrorHandler
{
    private readonly IDicomOperatorNotifier _operatorNotifier;
    private readonly ILogger<DicomErrorHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomErrorHandler"/> class.
    /// </summary>
    /// <param name="operatorNotifier">The operator notifier for notifications.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DicomErrorHandler(
        IDicomOperatorNotifier operatorNotifier,
        ILogger<DicomErrorHandler> logger)
    {
        _operatorNotifier = operatorNotifier ?? throw new ArgumentNullException(nameof(operatorNotifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Categorizes an exception into a DICOM error category.
    /// </summary>
    /// <param name="exception">The exception to categorize.</param>
    /// <returns>The error category.</returns>
    /// <remarks>
    /// @MX:WARN Error-critical paths - Error categorization determines handling strategy
    /// </remarks>
    public DicomErrorCategory CategorizeError(Exception exception)
    {
        if (exception == null)
        {
            return DicomErrorCategory.Unknown;
        }

        // Check for known exception types
        return exception switch
        {
            TimeoutException _ => DicomErrorCategory.Timeout,
            System.Net.Sockets.SocketException _ => DicomErrorCategory.Network,
            System.IO.IOException _ => DicomErrorCategory.Network,
            ArgumentNullException _ => DicomErrorCategory.Configuration,
            ArgumentException _ => DicomErrorCategory.Configuration,
            InvalidOperationException _ when (exception.Message?.Contains("configured", StringComparison.OrdinalIgnoreCase) ?? false) => DicomErrorCategory.Configuration,
            Worklist.DicomWorklistException _ => DicomErrorCategory.Dicom,
            Mpps.DicomMppsException _ => DicomErrorCategory.Dicom,
            DicomException dicomEx => dicomEx.ErrorCategory,
            _ => DicomErrorCategory.Unknown
        };
    }

    /// <summary>
    /// Determines whether the system should degrade gracefully for the given error.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns>True if graceful degradation is appropriate; otherwise, false.</returns>
    /// <remarks>
    /// @MX:WARN Error-critical paths - Determines whether workflow should continue
    ///
    /// Graceful degradation is appropriate for:
    /// - Network errors (temporary connectivity issues)
    /// - Timeout errors (temporary delays)
    /// - DICOM protocol errors (SCP-side issues)
    ///
    /// Graceful degradation is NOT appropriate for:
    /// - Configuration errors (require fix)
    /// - Authentication errors (require fix)
    /// </remarks>
    public bool ShouldDegradeGracefully(Exception exception)
    {
        var category = CategorizeError(exception);

        return category switch
        {
            DicomErrorCategory.Network => true,
            DicomErrorCategory.Timeout => true,
            DicomErrorCategory.Dicom => true,
            _ => false
        };
    }

    /// <summary>
    /// Handles a DICOM error with appropriate logging and operator notification.
    /// </summary>
    /// <param name="exception">The exception to handle.</param>
    /// <param name="studyId">The study identifier (if applicable).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <remarks>
    /// @MX:WARN Error-critical paths - Centralized error handling with operator notification
    /// </remarks>
    public async Task HandleErrorAsync(
        Exception exception,
        string? studyId = null,
        CancellationToken cancellationToken = default)
    {
        if (exception == null)
        {
            return;
        }

        var category = CategorizeError(exception);
        var isCritical = exception is DicomException dicomEx && dicomEx.IsCritical;

        // Log the error
        _logger.LogError(
            exception,
            "DICOM error (Category: {Category}, Study: {StudyId}, Critical: {IsCritical})",
            category,
            studyId ?? "[none]",
            isCritical);

        // Notify operator for critical errors
        if (isCritical || !ShouldDegradeGracefully(exception))
        {
            await NotifyOperatorAsync(exception, category, isCritical, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies the operator of a critical DICOM error.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="category">The error category.</param>
    /// <param name="isCritical">Whether the error is critical.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task NotifyOperatorAsync(
        Exception exception,
        DicomErrorCategory category,
        bool isCritical,
        CancellationToken cancellationToken)
    {
        var (title, message) = GenerateOperatorMessage(exception, category);

        await _operatorNotifier.NotifyErrorAsync(title, message, isCritical, cancellationToken);
    }

    /// <summary>
    /// Generates an operator-facing message for the error.
    /// </summary>
    private (string Title, string Message) GenerateOperatorMessage(
        Exception exception,
        DicomErrorCategory category)
    {
        var title = category switch
        {
            DicomErrorCategory.Network => "DICOM Network Error",
            DicomErrorCategory.Timeout => "DICOM Timeout",
            DicomErrorCategory.Configuration => "DICOM Configuration Error",
            DicomErrorCategory.Dicom => "DICOM Protocol Error",
            DicomErrorCategory.Authentication => "DICOM Authentication Error",
            _ => "DICOM Error"
        };

        var message = category switch
        {
            DicomErrorCategory.Network => "Unable to connect to DICOM server. Please check network connectivity.",
            DicomErrorCategory.Timeout => "DICOM operation timed out. Please retry the operation.",
            DicomErrorCategory.Configuration => "DICOM configuration is invalid. Please contact system administrator.",
            DicomErrorCategory.Dicom => "DICOM operation failed. Please check PACS/MPPS server status.",
            DicomErrorCategory.Authentication => "DICOM authentication failed. Please verify credentials.",
            _ => "A DICOM error has occurred. Please contact system administrator if the problem persists."
        };

        return (title, message);
    }
}
