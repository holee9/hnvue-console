using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using HnVue.Console.Services;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IDoseService.
/// SPEC-UI-001: FR-UI-10 Dose Display.
/// SPEC-IPC-002: REQ-DOSE-001 through REQ-DOSE-008 - Real gRPC implementation.
/// IEC 62304 Class C - Safety Critical: NEVER return 0 dose on failure.
/// </summary>
public sealed class DoseServiceAdapter : GrpcAdapterBase, IDoseService
{
    private readonly ILogger<DoseServiceAdapter> _logger;
    private readonly IAuditLogService _auditLogService;

    // SPEC-IPC-002: REQ-DOSE-003/006 - Configuration keys for dose thresholds
    private const string WarningThresholdKey = "dose.warning_threshold_mgy";
    private const string ErrorThresholdKey = "dose.error_threshold_mgy";

    // SPEC-IPC-002: REQ-DOSE-008 - Conservative defaults on configuration failure
    private const decimal ConservativeWarningThresholdMgy = 50m;
    private const decimal ConservativeErrorThresholdMgy = 100m;

    /// <summary>
    /// Initializes a new instance of <see cref="DoseServiceAdapter"/>.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="auditLogService">Audit log service for IEC 62304 compliance (SPEC-IPC-002: REQ-DOSE-004/006).</param>
    public DoseServiceAdapter(
        IConfiguration configuration,
        ILogger<DoseServiceAdapter> logger,
        IAuditLogService auditLogService)
        : base(configuration, logger)
    {
        _logger = logger;
        _auditLogService = auditLogService;
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-DOSE-002 - GetDoseSummary with LAST_30_DAYS period.
    /// REQ-DOSE-007: MUST throw exception on failure (IEC 62304 Class C - NEVER silently return 0).
    /// </remarks>
    public async Task<DoseDisplay> GetCurrentDoseDisplayAsync(CancellationToken ct)
    {
        var client = CreateClient<HnVue.Ipc.DoseService.DoseServiceClient>();
        var callOptions = CreateCallOptions(CommandDeadline).WithCancellationToken(ct);

        var response = await client.GetDoseSummaryAsync(
            new HnVue.Ipc.GetDoseSummaryRequest
            {
                Period = HnVue.Ipc.SummaryPeriod.Last30Days
            },
            callOptions);

        if (response.Error != null && response.Error.Code != 0)
        {
            throw new RpcException(new Status(StatusCode.Internal, response.Error.Message ?? "Dose summary retrieval failed"));
        }

        var now = DateTimeOffset.UtcNow;
        return new DoseDisplay
        {
            CurrentDose = new DoseValue
            {
                Value = (decimal)response.CumulativeSkinDoseMgy,
                Unit = DoseUnit.MilliGray,
                MeasuredAt = now
            },
            CumulativeDose = new DoseValue
            {
                Value = (decimal)response.CumulativeSkinDoseMgy,
                Unit = DoseUnit.MilliGray,
                MeasuredAt = now
            },
            StudyId = response.PatientId ?? string.Empty,
            ExposureCount = response.ExamCount
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-DOSE-003 - ConfigService.GetConfiguration with dose threshold keys.
    /// REQ-DOSE-008: Returns conservative defaults (50mGy/100mGy) on any failure.
    /// </remarks>
    public async Task<DoseAlertThreshold> GetAlertThresholdAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.ConfigService.ConfigServiceClient>();
            var callOptions = CreateCallOptions(CommandDeadline).WithCancellationToken(ct);

            var response = await client.GetConfigurationAsync(
                new HnVue.Ipc.GetConfigRequest
                {
                    ParameterKeys = { WarningThresholdKey, ErrorThresholdKey }
                },
                callOptions);

            if (response.Error != null && response.Error.Code != 0)
            {
                _logger.LogWarning("ConfigService returned error for dose thresholds: {Error}. Using conservative defaults.",
                    response.Error.Message);
                return GetConservativeDefaults();
            }

            var warningThreshold = ExtractDoubleFromConfig(response.Parameters, WarningThresholdKey, (double)ConservativeWarningThresholdMgy);
            var errorThreshold = ExtractDoubleFromConfig(response.Parameters, ErrorThresholdKey, (double)ConservativeErrorThresholdMgy);

            return new DoseAlertThreshold
            {
                WarningThreshold = (decimal)warningThreshold,
                ErrorThreshold = (decimal)errorThreshold,
                Unit = DoseUnit.MilliGray
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "ConfigService call failed for dose thresholds. Using conservative defaults (warning={Warning}mGy, error={Error}mGy).",
                ConservativeWarningThresholdMgy, ConservativeErrorThresholdMgy);
            return GetConservativeDefaults();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-DOSE-004 - ConfigService.SetConfiguration + IAuditLogService audit log.
    /// REQ-AUDIT-004: Audit log failure must NOT block the original operation.
    /// </remarks>
    public async Task SetAlertThresholdAsync(DoseAlertThreshold threshold, CancellationToken ct)
    {
        // Audit log: record before/after values (fire-and-forget per REQ-AUDIT-004)
        var beforeThreshold = await GetAlertThresholdAsync(ct);

        await TryLogAuditAsync(
            AuditEventType.ConfigChange,
            $"Dose alert threshold changed: warning {beforeThreshold.WarningThreshold}mGy -> {threshold.WarningThreshold}mGy, " +
            $"error {beforeThreshold.ErrorThreshold}mGy -> {threshold.ErrorThreshold}mGy",
            ct);

        var client = CreateClient<HnVue.Ipc.ConfigService.ConfigServiceClient>();
        var callOptions = CreateCallOptions(CommandDeadline).WithCancellationToken(ct);

        var parameters = new Dictionary<string, HnVue.Ipc.ConfigValue>
        {
            [WarningThresholdKey] = new HnVue.Ipc.ConfigValue { DoubleValue = (double)threshold.WarningThreshold },
            [ErrorThresholdKey] = new HnVue.Ipc.ConfigValue { DoubleValue = (double)threshold.ErrorThreshold }
        };

        var request = new HnVue.Ipc.SetConfigRequest();
        foreach (var kvp in parameters)
        {
            request.Parameters[kvp.Key] = kvp.Value;
        }

        await client.SetConfigurationAsync(request, callOptions);
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-DOSE-005 - DoseService.SubscribeDoseAlerts streaming → IAsyncEnumerable&lt;DoseUpdate&gt;.
    /// No deadline for long-lived subscriptions.
    /// </remarks>
    public async IAsyncEnumerable<DoseUpdate> SubscribeDoseUpdatesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        HnVue.Ipc.DoseService.DoseServiceClient client;
        Grpc.Core.AsyncServerStreamingCall<HnVue.Ipc.DoseAlertEvent> call;

        try
        {
            client = CreateClient<HnVue.Ipc.DoseService.DoseServiceClient>();
            // No deadline for long-lived alert subscriptions (per SPEC-IPC-002 deadline policy)
            call = client.SubscribeDoseAlerts(
                new HnVue.Ipc.DoseAlertSubscribeRequest { IncludeAllPatients = true },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IDoseService), nameof(SubscribeDoseUpdatesAsync));
            yield break;
        }

        await foreach (var alertEvent in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return new DoseUpdate
            {
                NewDose = new DoseValue
                {
                    Value = (decimal)alertEvent.CurrentDoseMsv,
                    Unit = DoseUnit.MilliGray,
                    MeasuredAt = DateTimeOffset.UtcNow
                },
                CumulativeDose = new DoseValue
                {
                    Value = (decimal)alertEvent.CurrentDoseMsv,
                    Unit = DoseUnit.MilliGray,
                    MeasuredAt = DateTimeOffset.UtcNow
                },
                IsWarningThresholdExceeded = alertEvent.Level >= HnVue.Ipc.DoseAlertLevel.Warning,
                IsErrorThresholdExceeded = alertEvent.Level >= HnVue.Ipc.DoseAlertLevel.Critical
            };
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-DOSE-006 - DoseService.ResetStudyDose RPC + audit log.
    /// REQ-AUDIT-004: Audit log failure must NOT block the reset operation.
    /// </remarks>
    public async Task ResetCumulativeDoseAsync(string studyId, CancellationToken ct)
    {
        // Audit log: record dose reset (fire-and-forget per REQ-AUDIT-004)
        await TryLogAuditAsync(
            AuditEventType.ConfigChange,
            $"Cumulative dose reset for study {studyId}",
            ct,
            studyId: studyId);

        var client = CreateClient<HnVue.Ipc.DoseService.DoseServiceClient>();
        var callOptions = CreateCallOptions(CommandDeadline).WithCancellationToken(ct);

        var response = await client.ResetStudyDoseAsync(
            new HnVue.Ipc.ResetStudyDoseRequest
            {
                StudyId = studyId,
                Reason = "Manual reset via GUI"
            },
            callOptions);

        if (!response.Success)
        {
            _logger.LogWarning("ResetStudyDose failed for study {StudyId}: {Message}", studyId, response.Message);
        }
    }

    /// <summary>
    /// Logs an audit event without propagating exceptions (REQ-AUDIT-004).
    /// </summary>
    private async Task TryLogAuditAsync(
        AuditEventType eventType,
        string description,
        CancellationToken ct,
        string? studyId = null)
    {
        try
        {
            await _auditLogService.LogAsync(
                eventType: eventType,
                userId: "system",
                userName: "System",
                eventDescription: description,
                outcome: AuditOutcome.Success,
                studyId: studyId,
                ct: ct);
        }
        catch (Exception ex)
        {
            // REQ-AUDIT-004: Audit log failure MUST NOT block the original operation
            _logger.LogWarning(ex, "Audit log failed for {EventType}: {Description}. Original operation continues.",
                eventType, description);
        }
    }

    /// <summary>
    /// Returns conservative dose alert defaults (SPEC-IPC-002: REQ-DOSE-008).
    /// </summary>
    private static DoseAlertThreshold GetConservativeDefaults() =>
        new DoseAlertThreshold
        {
            WarningThreshold = ConservativeWarningThresholdMgy,
            ErrorThreshold = ConservativeErrorThresholdMgy,
            Unit = DoseUnit.MilliGray
        };

    /// <summary>
    /// Extracts a double value from gRPC ConfigValue map with fallback.
    /// </summary>
    private static double ExtractDoubleFromConfig(
        Google.Protobuf.Collections.MapField<string, HnVue.Ipc.ConfigValue> parameters,
        string key,
        double fallback)
    {
        if (parameters.TryGetValue(key, out var value) && value.ValueCase == HnVue.Ipc.ConfigValue.ValueOneofCase.DoubleValue)
        {
            return value.DoubleValue;
        }
        return fallback;
    }
}
