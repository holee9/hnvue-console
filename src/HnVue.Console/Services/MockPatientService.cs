using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock patient service for development.
/// TODO: Replace with gRPC adapter in Task #23 implementation.
/// </summary>
internal class MockPatientService : IPatientService
{
    public Task<PatientSearchResult> SearchPatientsAsync(PatientSearchRequest request, CancellationToken ct)
    {
        // Simulate search results
        var results = new List<Patient>
        {
            new Patient
            {
                PatientId = "P001",
                PatientName = "Hong Gil-dong",
                DateOfBirth = new DateOnly(1980, 5, 15),
                Sex = Sex.Male,
                AccessionNumber = "A001"
            },
            new Patient
            {
                PatientId = "P002",
                PatientName = "Kim Cheol-su",
                DateOfBirth = new DateOnly(1975, 8, 22),
                Sex = Sex.Male,
                AccessionNumber = "A002"
            }
        };

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            results = results.Where(p =>
                p.PatientId.Contains(request.Query, StringComparison.OrdinalIgnoreCase) ||
                p.PatientName.Contains(request.Query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        }

        return Task.FromResult(new PatientSearchResult
        {
            Patients = results,
            TotalCount = results.Count
        });
    }

    public Task RegisterPatientAsync(PatientRegistration registration, CancellationToken ct)
    {
        // Simulate successful registration
        return Task.CompletedTask;
    }

    public Task UpdatePatientAsync(PatientEditRequest request, CancellationToken ct)
    {
        // Simulate successful update
        return Task.CompletedTask;
    }

    public Task<Patient?> GetPatientAsync(string patientId, CancellationToken ct)
    {
        // Simulate patient retrieval
        var patient = new Patient
        {
            PatientId = patientId,
            PatientName = "Mock Patient",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Sex = Sex.Unknown
        };
        return Task.FromResult<Patient?>(patient);
    }
}
