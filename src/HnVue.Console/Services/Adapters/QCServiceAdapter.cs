using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IQCService.
/// No gRPC proto defined yet; returns graceful defaults.
/// </summary>
public sealed class QCServiceAdapter : GrpcAdapterBase, IQCService
{
    private readonly ILogger<QCServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="QCServiceAdapter"/>.
    /// </summary>
    public QCServiceAdapter(IConfiguration configuration, ILogger<QCServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<QCActionResult> AcceptImageAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IQCService), nameof(AcceptImageAsync));
        return Task.FromResult(new QCActionResult
        {
            Success = false,
            ImageId = imageId,
            NewStatus = QCStatus.Pending,
            ErrorMessage = "gRPC proto not yet defined"
        });
    }

    /// <inheritdoc />
    public Task<QCActionResult> RejectImageAsync(string imageId, RejectionReason reason, string? notes, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IQCService), nameof(RejectImageAsync));
        return Task.FromResult(new QCActionResult
        {
            Success = false,
            ImageId = imageId,
            NewStatus = QCStatus.Pending,
            ErrorMessage = "gRPC proto not yet defined"
        });
    }

    /// <inheritdoc />
    public Task<QCActionResult> ReprocessImageAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IQCService), nameof(ReprocessImageAsync));
        return Task.FromResult(new QCActionResult
        {
            Success = false,
            ImageId = imageId,
            NewStatus = QCStatus.Pending,
            ErrorMessage = "gRPC proto not yet defined"
        });
    }

    /// <inheritdoc />
    public Task<QCStatus> GetQCStatusAsync(string imageId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IQCService), nameof(GetQCStatusAsync));
        return Task.FromResult(QCStatus.Pending);
    }

    /// <inheritdoc />
    public Task<QCActionResult> ExecuteQCActionAsync(QCActionRequest request, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IQCService), nameof(ExecuteQCActionAsync));
        return Task.FromResult(new QCActionResult
        {
            Success = false,
            ImageId = request.ImageId,
            NewStatus = QCStatus.Pending,
            ErrorMessage = "gRPC proto not yet defined"
        });
    }
}
