using System.IO;
using System.Text.RegularExpressions;

namespace HnVue.Console.Security;

/// <summary>
/// Security validation utilities for input sanitization and validation.
/// SPEC-SECURITY-001: FR-SEC-13 - Input Validation
/// </summary>
public static class SecurityValidator
{
    private static readonly Regex DicomUidRegex = new(@"^[0-9\.]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex PatientIdRegex = new(@"^[A-Z0-9\-]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex StudyIdRegex = new(@"^[0-9\.]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_\-\.@]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private const int MaxUidLength = 64;
    private const int MaxPatientIdLength = 64;
    private const int MaxStudyIdLength = 64;
    private const int MaxUsernameLength = 128;
    private const int MinUsernameLength = 3;

    /// <summary>
    /// Validates DICOM UID format according to DICOM PS3.5
    /// </summary>
    /// <param name="uid">DICOM UID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateDicomUid(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return false;
        }

        if (uid.Length > MaxUidLength)
        {
            return false;
        }

        // Check for null bytes and other injection characters
        if (ContainsInjectionCharacters(uid))
        {
            return false;
        }

        return DicomUidRegex.IsMatch(uid);
    }

    /// <summary>
    /// Validates patient ID according to healthcare data standards
    /// </summary>
    /// <param name="patientId">Patient ID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidatePatientId(string? patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return false;
        }

        if (patientId.Length < 4 || patientId.Length > MaxPatientIdLength)
        {
            return false;
        }

        if (ContainsInjectionCharacters(patientId))
        {
            return false;
        }

        return PatientIdRegex.IsMatch(patientId);
    }

    /// <summary>
    /// Validates study ID format
    /// </summary>
    /// <param name="studyId">Study ID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateStudyId(string? studyId)
    {
        if (string.IsNullOrWhiteSpace(studyId))
        {
            return false;
        }

        if (studyId.Length > MaxStudyIdLength)
        {
            return false;
        }

        if (ContainsInjectionCharacters(studyId))
        {
            return false;
        }

        return StudyIdRegex.IsMatch(studyId);
    }

    /// <summary>
    /// Validates username format
    /// </summary>
    /// <param name="username">Username to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
        {
            return false;
        }

        if (ContainsInjectionCharacters(username))
        {
            return false;
        }

        return UsernameRegex.IsMatch(username);
    }

    /// <summary>
    /// Sanitizes user input by removing potentially dangerous characters
    /// </summary>
    /// <param name="input">Input string to sanitize</param>
    /// <returns>Sanitized string</returns>
    public static string? SanitizeUserInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // Remove null bytes and control characters
        var sanitized = input.Replace("\0", string.Empty)
                            .Replace("\r", string.Empty)
                            .Replace("\n", string.Empty)
                            .Replace("\t", string.Empty);

        // Trim whitespace
        sanitized = sanitized.Trim();

        // Return null if empty after sanitization
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    /// <summary>
    /// Validates file path to prevent directory traversal attacks
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <param name="allowedDirectory">Allowed base directory</param>
    /// <returns>True if safe, false otherwise</returns>
    public static bool ValidateFilePath(string? filePath, string? allowedDirectory)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        // Check for directory traversal patterns
        if (filePath.Contains("..") || filePath.Contains("~"))
        {
            return false;
        }

        // Check for absolute paths (if allowed directory is specified)
        if (!string.IsNullOrWhiteSpace(allowedDirectory))
        {
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                var allowedPath = Path.GetFullPath(allowedDirectory);

                if (!fullPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates network endpoint format
    /// </summary>
    /// <param name="endpoint">Endpoint string (host:port)</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        // Basic endpoint validation
        var parts = endpoint.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var port) || port < 1 || port > 65535)
        {
            return false;
        }

        // Check for localhost or private IP ranges
        var host = parts[0].Trim();
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Validate hostname or IP format
        return Uri.CheckHostName(host) != UriHostNameType.Unknown;
    }

    /// <summary>
    /// Checks for injection characters in input
    /// </summary>
    /// <param name="input">Input string to check</param>
    /// <returns>True if injection characters found, false otherwise</returns>
    private static bool ContainsInjectionCharacters(string input)
    {
        // Check for common injection patterns
        var injectionChars = new[] { '\0', '\r', '\n', '\t', ';', '|', '&', '$', '`', '\\' };

        foreach (var c in injectionChars)
        {
            if (input.Contains(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates string length against min/max constraints
    /// </summary>
    /// <param name="value">String to validate</param>
    /// <param name="minLength">Minimum length (inclusive)</param>
    /// <param name="maxLength">Maximum length (inclusive)</param>
    /// <returns>True if within bounds, false otherwise</returns>
    public static bool ValidateLength(string? value, int minLength, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return minLength == 0;
        }

        return value.Length >= minLength && value.Length <= maxLength;
    }

    /// <summary>
    /// Validates enumeration value.
    /// </summary>
    /// <typeparam name="T">Enum type</typeparam>
    /// <param name="value">Value to validate</param>
    /// <returns>True if valid enum value, false otherwise</returns>
    public static bool ValidateEnum<T>(T value) where T : struct, Enum
    {
        return Enum.IsDefined(typeof(T), value);
    }
}
