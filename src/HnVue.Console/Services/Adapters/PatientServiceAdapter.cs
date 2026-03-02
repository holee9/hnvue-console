using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IPatientService.
/// No gRPC proto defined yet; returns graceful defaults.
/// </summary>
public sealed class PatientServiceAdapter : GrpcAdapterBase, IPatientService
{
    private readonly ILogger<PatientServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PatientServiceAdapter"/>.
    /// </summary>
    public PatientServiceAdapter(IConfiguration configuration, ILogger<PatientServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<PatientSearchResult> SearchPatientsAsync(PatientSearchRequest request, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IPatientService), nameof(SearchPatientsAsync));
        return Task.FromResult(new PatientSearchResult
        {
            Patients = Array.Empty<Patient>(),
            TotalCount = 0
        });
    }

    /// <inheritdoc />
    public Task RegisterPatientAsync(PatientRegistration registration, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IPatientService), nameof(RegisterPatientAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdatePatientAsync(PatientEditRequest request, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IPatientService), nameof(UpdatePatientAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Patient?> GetPatientAsync(string patientId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IPatientService), nameof(GetPatientAsync));
        return Task.FromResult<Patient?>(null);
    }
}
