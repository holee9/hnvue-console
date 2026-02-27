namespace HnVue.Dicom.Worklist;

/// <summary>
/// Represents a date range for filtering scheduled procedure steps.
/// </summary>
/// <param name="Start">The inclusive start date. Null means no lower bound.</param>
/// <param name="End">The inclusive end date. Null means no upper bound.</param>
public record DateRange(DateOnly? Start, DateOnly? End)
{
    /// <summary>
    /// Creates a DateRange covering a single day.
    /// </summary>
    public static DateRange ForDate(DateOnly date) => new(date, date);

    /// <summary>
    /// Creates a DateRange covering today.
    /// </summary>
    public static DateRange Today()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new(today, today);
    }

    /// <summary>
    /// Formats the date range as a DICOM DA range string (YYYYMMDD-YYYYMMDD).
    /// Returns wildcard (*) if both bounds are null.
    /// </summary>
    public string ToDicomRangeString()
    {
        if (Start is null && End is null)
        {
            return "*";
        }

        var startStr = Start?.ToString("yyyyMMdd") ?? string.Empty;
        var endStr = End?.ToString("yyyyMMdd") ?? string.Empty;

        if (Start == End && Start is not null)
        {
            return startStr;
        }

        return $"{startStr}-{endStr}";
    }
}

/// <summary>
/// Encapsulates the parameters for a Modality Worklist C-FIND query (IHE SWF RAD-5).
/// </summary>
/// <param name="ScheduledDate">The date range for scheduled procedure steps. Defaults to today if not specified.</param>
/// <param name="Modality">The modality code to filter on (e.g., "DX", "CR"). Null means no modality filter.</param>
/// <param name="PatientId">Optional patient identifier filter. Null or empty means wildcard.</param>
/// <param name="AeTitle">The scheduled station AE Title to query. Null means use the calling AE title from configuration.</param>
public record WorklistQuery(
    DateRange? ScheduledDate = null,
    string? Modality = null,
    string? PatientId = null,
    string? AeTitle = null);
