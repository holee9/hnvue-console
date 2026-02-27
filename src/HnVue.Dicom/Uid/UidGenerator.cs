using System.Linq;

namespace HnVue.Dicom.Uid;

/// <summary>
/// Generates globally unique DICOM UIDs according to DICOM PS 3.5.
/// Format: {OrgUidRoot}.{DeviceSerial}.{UnixTimestampMs}.{Sequence}
/// </summary>
public interface IUidGenerator
{
    /// <summary>
    /// Generates a new Study Instance UID.
    /// </summary>
    string GenerateStudyUid();

    /// <summary>
    /// Generates a new Series Instance UID.
    /// </summary>
    string GenerateSeriesUid();

    /// <summary>
    /// Generates a new SOP Instance UID.
    /// </summary>
    string GenerateSopInstanceUid();

    /// <summary>
    /// Generates a new MPPS Instance UID.
    /// </summary>
    string GenerateMppsUid();

    /// <summary>
    /// Validates that a UID conforms to DICOM format requirements.
    /// </summary>
    bool IsValidUid(string uid);
}

/// <summary>
/// Default implementation of DICOM UID generator.
/// Thread-safe for concurrent use.
/// </summary>
public sealed class UidGenerator : IUidGenerator
{
    private const int MaxUidLength = 64;
    private const string DefaultTestRoot = "2.25"; // UUID-based root for testing

    private readonly string _orgUidRoot;
    private readonly string _deviceSerial;
    private readonly object _lock = new();
    private long _sequence;

    /// <summary>
    /// Initializes a new instance of the <see cref="UidGenerator"/> class.
    /// </summary>
    /// <param name="orgUidRoot">Organization's registered DICOM UID root. Defaults to test root if not provided.</param>
    /// <param name="deviceSerial">Device serial number for uniqueness within organization.</param>
    /// <exception cref="ArgumentException">Thrown when UID root exceeds maximum length.</exception>
    public UidGenerator(string? orgUidRoot = null, string? deviceSerial = null)
    {
        _orgUidRoot = string.IsNullOrWhiteSpace(orgUidRoot) ? DefaultTestRoot : orgUidRoot.Trim();

        // DICOM UIDs must contain only digits and dots; strip non-digit characters and leading zeros.
        var rawSerial = string.IsNullOrWhiteSpace(deviceSerial) ? "0" : deviceSerial.Trim();
        var digitsOnly = new string(rawSerial.Where(char.IsDigit).ToArray());
        var trimmed = string.IsNullOrEmpty(digitsOnly) ? "0" : digitsOnly.TrimStart('0');
        _deviceSerial = string.IsNullOrEmpty(trimmed) ? "0" : trimmed;

        if (_orgUidRoot.Length > MaxUidLength - 20)
        {
            throw new ArgumentException(
                $"Organization UID root must leave room for device serial, timestamp, and sequence. Maximum allowed: {MaxUidLength - 20} characters.",
                nameof(orgUidRoot));
        }
    }

    /// <inheritdoc/>
    public string GenerateStudyUid()
    {
        return GenerateUid();
    }

    /// <inheritdoc/>
    public string GenerateSeriesUid()
    {
        return GenerateUid();
    }

    /// <inheritdoc/>
    public string GenerateSopInstanceUid()
    {
        return GenerateUid();
    }

    /// <inheritdoc/>
    public string GenerateMppsUid()
    {
        return GenerateUid();
    }

    private string GenerateUid()
    {
        long sequence;
        lock (_lock)
        {
            _sequence++;
            sequence = _sequence;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var uid = $"{_orgUidRoot}.{_deviceSerial}.{timestamp}.{sequence}";

        if (uid.Length > MaxUidLength)
        {
            throw new InvalidOperationException(
                $"Generated UID exceeds maximum length of {MaxUidLength} characters: {uid.Length}");
        }

        return uid;
    }

    /// <inheritdoc/>
    public bool IsValidUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return false;
        }

        // DICOM UID format: 0-9 characters separated by dots, no leading/trailing dots
        // Maximum 64 characters
        if (uid.Length > MaxUidLength)
        {
            return false;
        }

        if (uid[0] == '.' || uid[^1] == '.')
        {
            return false;
        }

        var parts = uid.Split('.');
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                return false; // Empty component (consecutive dots)
            }

            foreach (var c in part)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
