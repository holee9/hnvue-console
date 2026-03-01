namespace HnVue.Console.Models;

/// <summary>
/// Patient domain model.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public record Patient
{
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required Sex Sex { get; init; }
    public string? AccessionNumber { get; init; }
}

/// <summary>
/// Patient sex enumeration.
/// </summary>
public enum Sex
{
    Unknown,
    Male,
    Female,
    Other
}

/// <summary>
/// Patient search request.
/// </summary>
public record PatientSearchRequest
{
    public required string Query { get; init; }
    public int MaxResults { get; init; } = 50;
}

/// <summary>
/// Patient search result.
/// </summary>
public record PatientSearchResult
{
    public required IReadOnlyList<Patient> Patients { get; init; }
    public int TotalCount { get; init; }
}

/// <summary>
/// Patient registration request.
/// </summary>
public record PatientRegistration
{
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required Sex Sex { get; init; }
    public string? AccessionNumber { get; init; }
    public bool IsEmergency { get; init; }
}

/// <summary>
/// Patient edit request.
/// </summary>
public record PatientEditRequest
{
    public required string PatientId { get; init; }
    public string? PatientName { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public Sex? Sex { get; init; }
    public string? AccessionNumber { get; init; }
}
