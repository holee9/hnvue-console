using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IUserService.
/// No gRPC proto defined yet; returns graceful defaults.
/// </summary>
public sealed class UserServiceAdapter : GrpcAdapterBase, IUserService
{
    private readonly ILogger<UserServiceAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="UserServiceAdapter"/>.
    /// </summary>
    public UserServiceAdapter(IConfiguration configuration, ILogger<UserServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IUserService), nameof(GetUsersAsync));
        return Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
    }

    /// <inheritdoc />
    public Task<User> GetCurrentUserAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IUserService), nameof(GetCurrentUserAsync));
        return Task.FromResult(new User
        {
            UserId = string.Empty,
            UserName = string.Empty,
            Role = UserRole.Operator,
            IsActive = false
        });
    }

    /// <inheritdoc />
    public Task<UserRole> GetCurrentUserRoleAsync(CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IUserService), nameof(GetCurrentUserRoleAsync));
        return Task.FromResult(UserRole.Operator);
    }

    /// <inheritdoc />
    public Task<bool> CanAccessSectionAsync(ConfigSection section, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IUserService), nameof(CanAccessSectionAsync));
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task CreateUserAsync(User user, string password, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IUserService), nameof(CreateUserAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateUserAsync(User user, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IUserService), nameof(UpdateUserAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeactivateUserAsync(string userId, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IUserService), nameof(DeactivateUserAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct)
    {
        _logger.LogWarning("gRPC proto not yet defined for {Service}.{Method}", nameof(IUserService), nameof(ValidateCredentialsAsync));
        return Task.FromResult(false);
    }
}
