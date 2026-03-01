using System;

namespace HnVue.Dicom.Common;

/// <summary>
/// Base exception for DICOM-related errors.
/// </summary>
/// <remarks>
/// @MX:WARN Error-critical paths - DicomException is the base for all DICOM errors
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-410
/// </remarks>
public class DicomException : Exception
{
    /// <summary>
    /// Gets whether this error is critical and requires operator intervention.
    /// </summary>
    public bool IsCritical { get; }

    /// <summary>
    /// Gets the error category.
    /// </summary>
    public DicomErrorCategory ErrorCategory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="isCritical">Whether the error is critical.</param>
    public DicomException(string message, bool isCritical = false)
        : base(message)
    {
        IsCritical = isCritical;
        ErrorCategory = DicomErrorCategory.Unknown;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="isCritical">Whether the error is critical.</param>
    public DicomException(string message, Exception innerException, bool isCritical = false)
        : base(message, innerException)
    {
        IsCritical = isCritical;
        ErrorCategory = DicomErrorCategory.Unknown;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DicomException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCategory">The error category.</param>
    /// <param name="isCritical">Whether the error is critical.</param>
    public DicomException(string message, DicomErrorCategory errorCategory, bool isCritical = false)
        : base(message)
    {
        IsCritical = isCritical;
        ErrorCategory = errorCategory;
    }
}

/// <summary>
/// Categories of DICOM errors for handling and notification.
/// </summary>
/// <remarks>
/// @MX:WARN Error-critical paths - Error categorization determines handling strategy
/// @MX:SPEC SPEC-WORKFLOW-001 TASK-410
/// </remarks>
public enum DicomErrorCategory
{
    /// <summary>Network-related errors (connection, timeout).</summary>
    Network,

    /// <summary>Timeout errors.</summary>
    Timeout,

    /// <summary>Configuration errors (invalid settings, missing values).</summary>
    Configuration,

    /// <summary>DICOM protocol errors (status codes, rejected operations).</summary>
    Dicom,

    /// <summary>Authentication/authorization errors.</summary>
    Authentication,

    /// <summary>Unknown or unclassified errors.</summary>
    Unknown
}
