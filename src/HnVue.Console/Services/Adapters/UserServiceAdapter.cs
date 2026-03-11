using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HnVue.Console.Models;

namespace HnVue.Console.Services.Adapters;

/// <summary>
/// gRPC adapter for IUserService.
/// SPEC-ADAPTER-001: User authentication and authorization (RBAC).
/// @MX:NOTE Uses UserService gRPC for authentication, session management, and user CRUD.
/// </summary>
public sealed class UserServiceAdapter : GrpcAdapterBase, IUserService
{
    private readonly ILogger<UserServiceAdapter> _logger;

    public UserServiceAdapter(IConfiguration configuration, ILogger<UserServiceAdapter> logger)
        : base(configuration, logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.ListUsersAsync(
                new HnVue.Ipc.ListUsersRequest { IncludeInactive = false },
                cancellationToken: ct);
            return response.Users.Select(MapToUser).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(GetUsersAsync));
            return Array.Empty<User>();
        }
    }

    public async Task<User> GetCurrentUserAsync(CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.GetCurrentSessionAsync(
                new HnVue.Ipc.GetCurrentSessionRequest(),
                cancellationToken: ct);
            return response.Session is not null ? MapToUser(response.Session.User) : CreateDefaultUser();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(GetCurrentUserAsync));
            return CreateDefaultUser();
        }
    }

    public async Task<UserRole> GetCurrentUserRoleAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(ct);
        return user.Role;
    }

    public async Task<bool> CanAccessSectionAsync(ConfigSection section, CancellationToken ct)
    {
        var role = await GetCurrentUserRoleAsync(ct);
        return HasSectionAccess(role, section);
    }

    public async Task CreateUserAsync(User user, string password, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            await client.CreateUserAsync(
                new HnVue.Ipc.CreateUserRequest
                {
                    User = MapToProtoUser(user),
                    InitialPassword = password
                },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(CreateUserAsync));
        }
    }

    public async Task UpdateUserAsync(User user, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            await client.UpdateUserAsync(
                new HnVue.Ipc.UpdateUserRequest
                {
                    UserId = user.UserId,
                    UpdatedUser = MapToProtoUser(user)
                },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(UpdateUserAsync));
        }
    }

    public async Task DeactivateUserAsync(string userId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var inactiveUser = new HnVue.Ipc.User { UserId = userId, IsActive = false };
            await client.UpdateUserAsync(
                new HnVue.Ipc.UpdateUserRequest { UserId = userId, UpdatedUser = inactiveUser },
                cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(DeactivateUserAsync));
        }
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct)
    {
        try
        {
            var client = CreateClient<HnVue.Ipc.UserService.UserServiceClient>();
            var response = await client.AuthenticateAsync(
                new HnVue.Ipc.AuthenticateRequest { Username = username, Password = password },
                cancellationToken: ct);
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed for {Service}.{Method}", nameof(IUserService), nameof(ValidateCredentialsAsync));
            return false;
        }
    }

    private static User MapToUser(HnVue.Ipc.User proto)
    {
        return new User
        {
            UserId = proto.UserId,
            UserName = proto.Username,
            Role = MapFromProtoRole(proto.PrimaryRole),
            IsActive = proto.IsActive
        };
    }

    private static HnVue.Ipc.User MapToProtoUser(User user)
    {
        return new HnVue.Ipc.User
        {
            UserId = user.UserId,
            Username = user.UserName,
            DisplayName = user.UserName,
            PrimaryRole = MapToProtoRole(user.Role),
            IsActive = user.IsActive
        };
    }

    private static UserRole MapFromProtoRole(HnVue.Ipc.UserRole protoRole) => protoRole switch
    {
        HnVue.Ipc.UserRole.Administrator => UserRole.Administrator,
        // Map other proto roles to closest domain role
        _ => UserRole.Operator
    };

    private static HnVue.Ipc.UserRole MapToProtoRole(UserRole role) => role switch
    {
        UserRole.Administrator => HnVue.Ipc.UserRole.Administrator,
        _ => HnVue.Ipc.UserRole.Operator
    };

    /// <summary>
    /// @MX:ANCHOR RBAC logic for section access control.
    /// </summary>
    private static bool HasSectionAccess(UserRole role, ConfigSection section) => section switch
    {
        ConfigSection.Users => role == UserRole.Administrator,
        ConfigSection.Network => role == UserRole.Administrator,
        ConfigSection.Logging => role == UserRole.Administrator,
        ConfigSection.Calibration => role == UserRole.Administrator,
        _ => false
    };

    private static User CreateDefaultUser() => new()
    {
        UserId = string.Empty,
        UserName = string.Empty,
        Role = UserRole.Operator,
        IsActive = false
    };
}
