using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Services;
using Xunit;

namespace HnVue.Integration.Tests.Security;

public sealed class RbacEnforcementTests
{
    private readonly MockUserService _userService = new();

    // INT-003-1: Administrator has all admin permissions
    [Fact]
    public async Task Administrator_HasAccessTo_AllAdminPermissions()
    {
        const string adminId = "admin";
        var permissions = await _userService.GetUserPermissionsAsync(adminId, CancellationToken.None);
        permissions.Should().NotBeEmpty(because: "Administrator must have permissions");
        permissions.Any(p => p.PermissionId == "system.admin").Should().BeTrue(because: "Administrator must have system.admin permission");
        permissions.Any(p => p.PermissionId == "users.manage").Should().BeTrue(because: "Administrator must manage users");
        permissions.Any(p => p.PermissionId == "config.all").Should().BeTrue(because: "Administrator must have full config access");
    }

    // INT-003-2: Technologist exposure access but not user management
    [Fact]
    public async Task Technologist_HasExposureAccess_ButNot_UserManagement()
    {
        const string techId = "tech01";
        var permissions = await _userService.GetUserPermissionsAsync(techId, CancellationToken.None);
        permissions.Any(p => p.PermissionId == "exposure.execute").Should().BeTrue(because: "Technologist must execute exposures");
        permissions.Any(p => p.PermissionId == "users.manage").Should().BeFalse(because: "Technologist must not manage users");
        permissions.Any(p => p.PermissionId == "system.admin").Should().BeFalse(because: "Technologist must not have system.admin");
    }

    // INT-003-3: Viewer read-only access
    [Fact]
    public async Task Viewer_HasReadOnly_Access()
    {
        const string viewerId = "viewer01";
        var permissions = await _userService.GetUserPermissionsAsync(viewerId, CancellationToken.None);
        permissions.Should().NotBeEmpty(because: "Viewer must have at least read permissions");
        var allActions = permissions.SelectMany(p => p.AllowedActions).Distinct().ToList();
        allActions.Should().OnlyContain(a => a == "Read", because: "Viewer must only have Read actions");
        permissions.Any(p => p.PermissionId == "exposure.execute").Should().BeFalse(because: "Viewer must not execute exposures");
    }

    // INT-003-4: HasPermissionAsync correctly enforces access control
    [Fact]
    public async Task Technologist_AccessDenied_ForAdminPermission()
    {
        const string techId = "tech01";
        var hasAdminAccess = await _userService.HasPermissionAsync(techId, "system.admin", CancellationToken.None);
        hasAdminAccess.Should().BeFalse(because: "Technologist must not have system.admin permission");
        var hasExposureAccess = await _userService.HasPermissionAsync(techId, "exposure.execute", CancellationToken.None);
        hasExposureAccess.Should().BeTrue(because: "Technologist must have exposure.execute permission");
    }

    // INT-003-5: Session expires after 30 minutes
    [Fact]
    public async Task Session_Expires_After30Minutes()
    {
        var authResult = await _userService.AuthenticateAsync("System Administrator", "password123", "WS-01", CancellationToken.None);
        authResult.Success.Should().BeTrue(because: "Valid user name must authenticate successfully");
        var sessionId = authResult.Session!.SessionId;
        var isValid = await _userService.ValidateSessionAsync(sessionId, CancellationToken.None);
        isValid.Should().BeTrue(because: "Freshly created session must be valid");
        var session = await _userService.GetCurrentSessionAsync(sessionId, CancellationToken.None);
        session.Should().NotBeNull(because: "Active session must be retrievable");
        var expiryWindow = session!.ExpiresAt - DateTimeOffset.UtcNow;
        expiryWindow.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(1), because: "Session must expire in approx 30 minutes");
        await _userService.LogoutAsync(sessionId, CancellationToken.None);
    }

    // INT-003-6: Account lockout after 5 failed attempts
    [Fact]
    public async Task Account_LocksOut_After5FailedAttempts()
    {
        const string username = "System Administrator";
        const string wrongPwd = "wrong_password";
        const string workstationId = "WS-TEST";
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            var result = await _userService.AuthenticateAsync(username, wrongPwd, workstationId, CancellationToken.None);
            result.Success.Should().BeFalse(because: "Wrong password must fail");
        }
        var lockedResult = await _userService.AuthenticateAsync(username, wrongPwd, workstationId, CancellationToken.None);
        lockedResult.Success.Should().BeFalse(because: "Account must be locked after 5 failed attempts");
        lockedResult.FailureReason.Should().Be(AuthenticationFailureReason.AccountLocked, because: "Failure reason must be AccountLocked");
        lockedResult.RemainingAttempts.Should().Be(0, because: "No attempts should remain when locked");
    }

    // INT-003-7: Unknown user has no permissions
    [Fact]
    public async Task UnknownUser_HasNoPermissions()
    {
        const string unknownId = "nonexistent-user-99999";
        var permissions = await _userService.GetUserPermissionsAsync(unknownId, CancellationToken.None);
        permissions.Should().BeEmpty(because: "Unknown users must have no permissions");
        var hasPermission = await _userService.HasPermissionAsync(unknownId, "system.admin", CancellationToken.None);
        hasPermission.Should().BeFalse(because: "Unknown users must not have any permissions");
    }

    // INT-003-8: Each role has expected permission count
    [Theory]
    [InlineData("admin", 5)]
    [InlineData("rad01", 4)]
    [InlineData("tech01", 4)]
    [InlineData("viewer01", 2)]
    public async Task Role_HasExpectedPermissionCount(string userId, int expectedCount)
    {
        var permissions = await _userService.GetUserPermissionsAsync(userId, CancellationToken.None);
        permissions.Should().HaveCount(expectedCount, because: userId + " must have " + expectedCount + " permissions");
    }
}
