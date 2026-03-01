using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Patient service interface for gRPC communication.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public interface IPatientService
{
    /// <summary>
    /// Searches for patients by query string.
    /// </summary>
    Task<PatientSearchResult> SearchPatientsAsync(PatientSearchRequest request, CancellationToken ct);

    /// <summary>
    /// Registers a new patient.
    /// </summary>
    Task RegisterPatientAsync(PatientRegistration registration, CancellationToken ct);

    /// <summary>
    /// Updates an existing patient record.
    /// </summary>
    Task UpdatePatientAsync(PatientEditRequest request, CancellationToken ct);

    /// <summary>
    /// Gets a patient by ID.
    /// </summary>
    Task<Patient?> GetPatientAsync(string patientId, CancellationToken ct);
}
