using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;
using HnVue.Console.Services;
using System.Runtime.CompilerServices;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IAECService.
/// SPEC-UI-001: FR-UI-11 AEC Mode Toggle.
/// SPEC-IPC-002: REQ-AUDIT-003 - EnableAECAsync/DisableAECAsync must log audit events.
/// </summary>
public sealed class AECServiceAdapter : GrpcAdapterBase, IAECService
{
    private readonly ILogger<AECServiceAdapter> _logger;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Initializes a new instance of <see cref="AECServiceAdapter"/>.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="auditLogService">Audit log service for IEC 62304 compliance (SPEC-IPC-002: REQ-AUDIT-003).</param>
    public AECServiceAdapter(
        IConfiguration configuration,
        ILogger<AECServiceAdapter> logger,
        IAuditLogService auditLogService)
        : base(configuration, logger)
    {
        _logger = logger;
        _auditLogService = auditLogService;
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-AUDIT-003 - Logs audit event before enabling AEC.
    /// REQ-AUDIT-004: Audit log failure must NOT block the enable operation.
    /// </remarks>
    public async Task EnableAECAsync(CancellationToken ct)
    {
        // Audit log: record AEC enable action (fire-and-forget per REQ-AUDIT-004)
        await TryLogAuditAsync("AEC mode enabled", ct);

        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            var grpcRequest = new HnVue.Ipc.SetAecEnabledRequest
            {
                Enabled = true
            };

            await client.SetAecEnabledAsync(grpcRequest, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(EnableAECAsync));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// SPEC-IPC-002: REQ-AUDIT-003 - Logs audit event before disabling AEC.
    /// REQ-AUDIT-004: Audit log failure must NOT block the disable operation.
    /// </remarks>
    public async Task DisableAECAsync(CancellationToken ct)
    {
        // Audit log: record AEC disable action (fire-and-forget per REQ-AUDIT-004)
        await TryLogAuditAsync("AEC mode disabled", ct);

        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            var grpcRequest = new HnVue.Ipc.SetAecEnabledRequest
            {
                Enabled = false
            };

            await client.SetAecEnabledAsync(grpcRequest, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(DisableAECAsync));
        }
    }

    /// <inheritdoc />
    public async Task<bool> GetAECStateAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            var grpcRequest = new HnVue.Ipc.GetAecStatusRequest();

            var response = await client.GetAecStatusAsync(grpcRequest, cancellationToken: ct);
            return response.IsEnabled;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(GetAECStateAsync));
            return false;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<bool> SubscribeAECStateChangesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        HnVue.Ipc.AECService.AECServiceClient client;
        Grpc.Core.AsyncServerStreamingCall<HnVue.Ipc.AecChangeEvent> call;

        try
        {
            client = CreateClient<HnVue.Ipc.AECService.AECServiceClient>();
            call = client.SubscribeAecChanges(new HnVue.Ipc.AecChangeSubscribeRequest(), cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IAECService), nameof(SubscribeAECStateChangesAsync));
            yield break;
        }

        await foreach (var changeEvent in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return changeEvent.IsEnabled;
        }
    }

    /// <summary>
    /// Logs an audit event without propagating exceptions (SPEC-IPC-002: REQ-AUDIT-004).
    /// </summary>
    private async Task TryLogAuditAsync(string description, CancellationToken ct)
    {
        try
        {
            await _auditLogService.LogAsync(
                eventType: AuditEventType.ConfigChange,
                userId: "system",
                userName: "System",
                eventDescription: description,
                outcome: AuditOutcome.Success,
                ct: ct);
        }
        catch (Exception ex)
        {
            // REQ-AUDIT-004: Audit log failure MUST NOT block the original operation
            _logger.LogWarning(ex, "Audit log failed for AEC action: {Description}. Original operation continues.", description);
        }
    }
}
