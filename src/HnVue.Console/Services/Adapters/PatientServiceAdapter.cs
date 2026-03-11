using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IPatientService.
/// SPEC-ADAPTER-001: Patient CRUD and search operations.
/// @MX:NOTE Uses PatientService gRPC for patient registration, search, and updates.
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
            var response = await client.SearchPatientsAsync(
                new HnVue.Ipc.SearchPatientsRequest
                {
                    Query = request.Query,
                    MaxResults = request.MaxResults
                },
                cancellationToken: ct);

            var patients = response.Patients.Select(MapToPatient).ToList();
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
            var patient = new HnVue.Ipc.Patient
            {
                PatientId = registration.PatientId,
                FamilyName = ExtractFamilyName(registration.PatientName),
                GivenName = ExtractGivenName(registration.PatientName),
                DateOfBirth = registration.DateOfBirth.ToString("yyyy-MM-dd"),
                Sex = MapToProtoSex(registration.Sex)
            };

            await client.RegisterPatientAsync(
                new HnVue.Ipc.RegisterPatientRequest
                {
                    Patient = patient
                },
                cancellationToken: ct);
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
            var patient = new HnVue.Ipc.Patient
            {
                PatientId = request.PatientId
            };

            if (request.PatientName is not null)
            {
                patient.FamilyName = ExtractFamilyName(request.PatientName);
                patient.GivenName = ExtractGivenName(request.PatientName);
            }

            if (request.DateOfBirth.HasValue)
            {
                patient.DateOfBirth = request.DateOfBirth.Value.ToString("yyyy-MM-dd");
            }

            if (request.Sex.HasValue)
            {
                patient.Sex = MapToProtoSex(request.Sex.Value);
            }

            await client.UpdatePatientAsync(
                new HnVue.Ipc.UpdatePatientRequest
                {
                    PatientId = request.PatientId,
                    UpdatedPatient = patient
                },
                cancellationToken: ct);
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
            var response = await client.GetPatientAsync(
                new HnVue.Ipc.GetPatientRequest
                {
                    PatientId = patientId
                },
                cancellationToken: ct);

            return response.Patient is not null ? MapToPatient(response.Patient) : null;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IPatientService), nameof(GetPatientAsync));
            return null;
        }
    }

    /// <summary>
    /// Maps proto Patient to domain Patient model.
    /// @MX:ANCHOR Central mapping logic for Patient conversion.
    /// </summary>
    private static Patient MapToPatient(HnVue.Ipc.Patient proto)
    {
        var familyName = proto.FamilyName ?? string.Empty;
        var givenName = proto.GivenName ?? string.Empty;
        var fullName = string.IsNullOrEmpty(givenName)
            ? familyName
            : $"{familyName}^{givenName}";

        return new Patient
        {
            PatientId = proto.PatientId,
            PatientName = fullName,
            DateOfBirth = DateOnly.TryParse(proto.DateOfBirth, out var dob) ? dob : DateOnly.MinValue,
            Sex = MapFromProtoSex(proto.Sex)
        };
    }

    private static Sex MapFromProtoSex(HnVue.Ipc.PatientSex protoSex)
    {
        return protoSex switch
        {
            HnVue.Ipc.PatientSex.Male => Sex.Male,
            HnVue.Ipc.PatientSex.Female => Sex.Female,
            HnVue.Ipc.PatientSex.Other => Sex.Other,
            _ => Sex.Unknown
        };
    }

    private static HnVue.Ipc.PatientSex MapToProtoSex(Sex sex)
    {
        return sex switch
        {
            Sex.Male => HnVue.Ipc.PatientSex.Male,
            Sex.Female => HnVue.Ipc.PatientSex.Female,
            Sex.Other => HnVue.Ipc.PatientSex.Other,
            _ => HnVue.Ipc.PatientSex.Unknown
        };
    }

    /// <summary>
    /// Extracts family name from DICOM-style name (Family^Given).
    /// </summary>
    private static string ExtractFamilyName(string fullName)
    {
        var parts = fullName.Split('^');
        return parts.Length > 0 ? parts[0] : fullName;
    }

    /// <summary>
    /// Extracts given name from DICOM-style name (Family^Given).
    /// </summary>
    private static string ExtractGivenName(string fullName)
    {
        var parts = fullName.Split('^');
        return parts.Length > 1 ? parts[1] : string.Empty;
    }
}
