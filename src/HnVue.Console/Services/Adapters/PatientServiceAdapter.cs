using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IPatientService.
/// SPEC-UI-001: FR-UI-01 Patient Management.
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
    public async Task<PatientSearchResult> SearchPatientsAsync(PatientSearchRequest request, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.PatientService.PatientServiceClient>();
            var grpcRequest = new HnVue.Ipc.SearchPatientsRequest
            {
                Query = request.Query,
                MaxResults = request.MaxResults
            };

            var response = await client.SearchPatientsAsync(grpcRequest, cancellationToken: ct);

            var patients = response.Patients.Select(p => new Patient
            {
                PatientId = p.PatientId,
                PatientName = $"{p.FamilyName} {p.GivenName}".Trim(),
                DateOfBirth = ParseDateOfBirth(p.DateOfBirth),
                Sex = MapSex(p.Sex),
                AccessionNumber = null
            }).ToList();

            return new PatientSearchResult
            {
                Patients = patients,
                TotalCount = response.TotalCount
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IPatientService), nameof(SearchPatientsAsync));
            return new PatientSearchResult
            {
                Patients = Array.Empty<Patient>(),
                TotalCount = 0
            };
        }
    }

    /// <inheritdoc />
    public async Task RegisterPatientAsync(PatientRegistration registration, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.PatientService.PatientServiceClient>();
            var grpcRequest = new HnVue.Ipc.RegisterPatientRequest
            {
                Patient = new HnVue.Ipc.Patient
                {
                    PatientId = registration.PatientId,
                    FamilyName = registration.PatientName,
                    DateOfBirth = registration.DateOfBirth.ToString("yyyy-MM-dd"),
                    Sex = MapSexToProto(registration.Sex)
                }
            };

            await client.RegisterPatientAsync(grpcRequest, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IPatientService), nameof(RegisterPatientAsync));
        }
    }

    /// <inheritdoc />
    public async Task UpdatePatientAsync(PatientEditRequest request, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.PatientService.PatientServiceClient>();
            var grpcRequest = new HnVue.Ipc.UpdatePatientRequest
            {
                PatientId = request.PatientId,
                UpdatedPatient = new HnVue.Ipc.Patient()
            };

            if (request.PatientName != null)
            {
                grpcRequest.UpdatedPatient.FamilyName = request.PatientName;
            }
            if (request.DateOfBirth.HasValue)
            {
                grpcRequest.UpdatedPatient.DateOfBirth = request.DateOfBirth.Value.ToString("yyyy-MM-dd");
            }
            if (request.Sex.HasValue)
            {
                grpcRequest.UpdatedPatient.Sex = MapSexToProto(request.Sex.Value);
            }

            await client.UpdatePatientAsync(grpcRequest, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IPatientService), nameof(UpdatePatientAsync));
        }
    }

    /// <inheritdoc />
    public async Task<Patient?> GetPatientAsync(string patientId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.PatientService.PatientServiceClient>();
            var grpcRequest = new HnVue.Ipc.GetPatientRequest
            {
                PatientId = patientId
            };

            var response = await client.GetPatientAsync(grpcRequest, cancellationToken: ct);

            if (response.Patient == null)
            {
                return null;
            }

            return new Patient
            {
                PatientId = response.Patient.PatientId,
                PatientName = $"{response.Patient.FamilyName} {response.Patient.GivenName}".Trim(),
                DateOfBirth = ParseDateOfBirth(response.Patient.DateOfBirth),
                Sex = MapSex(response.Patient.Sex),
                AccessionNumber = null
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IPatientService), nameof(GetPatientAsync));
            return null;
        }
    }

    private static DateOnly ParseDateOfBirth(string dateOfBirth)
    {
        if (DateOnly.TryParse(dateOfBirth, out var result))
        {
            return result;
        }
        return DateOnly.MinValue;
    }

    private static Sex MapSex(HnVue.Ipc.PatientSex protoSex)
    {
        return protoSex switch
        {
            HnVue.Ipc.PatientSex.Male => Sex.Male,
            HnVue.Ipc.PatientSex.Female => Sex.Female,
            HnVue.Ipc.PatientSex.Other => Sex.Other,
            _ => Sex.Unknown
        };
    }

    private static HnVue.Ipc.PatientSex MapSexToProto(Sex sex)
    {
        return sex switch
        {
            Sex.Male => HnVue.Ipc.PatientSex.Male,
            Sex.Female => HnVue.Ipc.PatientSex.Female,
            Sex.Other => HnVue.Ipc.PatientSex.Other,
            _ => HnVue.Ipc.PatientSex.Unknown
        };
    }
}
