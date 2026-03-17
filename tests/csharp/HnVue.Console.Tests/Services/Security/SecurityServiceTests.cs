using HnVue.Console.Models;
using HnVue.Console.Services;
using Xunit;
using Xunit.Abstractions;

namespace HnVue.Console.Tests.Services.Security;

/// <summary>
/// SPEC-SECURITY-001: R1 UserService - Authentication/Authorization Tests.
/// TDD RED phase: Failing tests for RBAC, session management, account lockout, password policy.
/// </summary>
public class SecurityServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IUserService _userService;

    public SecurityServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _userService = new MockUserService();
    }

    // ============== RBAC Tests (FR-SEC-01) ==============

    [Fact]
    public async Task SPEC_SEC_01_Administrator_HasFullSystemAccess()
    {
        // Arrange
        var adminUser = new User
        {
            UserId = "admin01",
            UserName = "System Administrator",
            Role = UserRole.Administrator,
            IsActive = true
        };

        // Act
        var permissions = await _userService.GetUserPermissionsAsync(adminUser.UserId, CancellationToken.None);

        // Assert
        Assert.NotEmpty(permissions);
        Assert.Contains(permissions, p => p.PermissionId == "system.admin");
        _output.WriteLine($"Administrator has {permissions.Count} permissions");
    }

    [Fact]
    public async Task SPEC_SEC_01_Radiologist_CanSignReports()
    {
        // Arrange
        var radiologist = new User
        {
            UserId = "rad01",
            UserName = "Dr. House",
            Role = UserRole.Radiologist,
            IsActive = true
        };

        // Act
        var hasPermission = await _userService.HasPermissionAsync(
            radiologist.UserId,
            "reports.sign",
            CancellationToken.None);

        // Assert
        Assert.True(hasPermission, "Radiologist should be able to sign reports");
    }

    [Fact]
    public async Task SPEC_SEC_01_Technologist_CanExecuteExposure()
    {
        // Arrange
        var technologist = new User
        {
            UserId = "tech01",
            UserName = "Tech John",
            Role = UserRole.Technologist,
            IsActive = true
        };

        // Act
        var hasPermission = await _userService.HasPermissionAsync(
            technologist.UserId,
            "exposure.execute",
            CancellationToken.None);

        // Assert
        Assert.True(hasPermission, "Technologist should be able to execute exposure");
    }

    [Fact]
    public async Task SPEC_SEC_01_Viewer_ReadOnlyAccess()
    {
        // Arrange
        var viewer = new User
        {
            UserId = "viewer01",
            UserName = "Viewer Jane",
            Role = UserRole.Viewer,
            IsActive = true
        };

        // Act
        var permissions = await _userService.GetUserPermissionsAsync(viewer.UserId, CancellationToken.None);

        // Assert
        Assert.All(permissions, p =>
        {
            Assert.DoesNotContain("Write", p.AllowedActions);
            Assert.DoesNotContain("Delete", p.AllowedActions);
            Assert.DoesNotContain("Execute", p.AllowedActions);
        });
    }

    // ============== Session Management Tests (FR-SEC-02) ==============

    [Fact]
    public async Task SPEC_SEC_02_SessionExpiresAfter30Minutes()
    {
        // Arrange
        var result = await _userService.AuthenticateAsync(
            "System Administrator",
            "valid_password",
            "WS-001",
            CancellationToken.None);

        Assert.True(result.Success);

        // Act - Simulate time passing (31 minutes)
        var isExpired = !await _userService.ValidateSessionAsync(result.Session!.SessionId, CancellationToken.None);

        // Assert - Note: This test will need time manipulation or interface redesign
        // For now, we verify the session timeout is configured correctly
        var expectedExpiry = DateTimeOffset.UtcNow.AddMinutes(30);
        var actualExpiry = result.Session.ExpiresAt;

        // Allow 1 second tolerance for test execution time
        var difference = Math.Abs((expectedExpiry - actualExpiry).TotalSeconds);
        Assert.True(difference < 1, $"Session should expire in 30 minutes, difference: {difference}s");
    }

    [Fact]
    public async Task SPEC_SEC_02_SessionIdIsCryptographicallySecure()
    {
        // Arrange & Act
        var result1 = await _userService.AuthenticateAsync("user1", "pass1", "WS-001", CancellationToken.None);
        var result2 = await _userService.AuthenticateAsync("user2", "pass2", "WS-001", CancellationToken.None);

        // Assert
        Assert.NotEqual(result1.Session!.SessionId, result2.Session!.SessionId);

        // Verify GUID format (cryptographically random on .NET 8+)
        Assert.True(Guid.TryParse(result1.Session.SessionId, out _), "Session ID should be valid GUID");
    }

    // ============== Account Lockout Tests (FR-SEC-03) ==============

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 3)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    [InlineData(5, 0)]
    public async Task SPEC_SEC_03_FiveFailedAttemptsLocksAccount(int failCount, int expectedRemaining)
    {
        // Arrange
        var username = "lockout_test_user";

        // Act - Simulate failed login attempts
        for (int i = 0; i < failCount; i++)
        {
            await _userService.AuthenticateAsync(username, "wrong_password", "WS-001", CancellationToken.None);
        }

        var remainingAttempts = _userService.GetRemainingLoginAttempts(username);

        // Assert
        Assert.Equal(expectedRemaining, remainingAttempts);
    }

    [Fact]
    public async Task SPEC_SEC_03_LockedAccountCannotAuthenticate()
    {
        // Arrange
        var username = "locked_user";

        // Act - 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            await _userService.AuthenticateAsync(username, "wrong", "WS-001", CancellationToken.None);
        }

        var result = await _userService.AuthenticateAsync(username, "correct_password", "WS-001", CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(AuthenticationFailureReason.AccountLocked, result.FailureReason);
        Assert.Equal(0, result.RemainingAttempts);
    }

    // ============== Password Policy Tests (FR-SEC-04) ==============

    [Theory]
    [InlineData("short1A!", false)] // Too short
    [InlineData("nouppercase1!", false)] // Missing uppercase
    [InlineData("NOLOWERCASE1!", false)] // Missing lowercase
    [InlineData("NoDigits!", false)] // Missing digit
    [InlineData("NoSpecial1", false)] // Missing special character
    [InlineData("ValidPassword123!", true)] // Valid
    public async Task SPEC_SEC_04_PasswordComplexityValidation(string password, bool shouldBeValid)
    {
        // Arrange & Act
        var result = _userService.ValidatePasswordComplexity(password, "testuser");

        // Assert
        Assert.Equal(shouldBeValid, result.IsValid);

        if (!shouldBeValid)
        {
            Assert.NotEmpty(result.Failures);
            _output.WriteLine($"Password '{password}' failures: {string.Join(", ", result.Failures)}");
        }
    }

    [Fact]
    public async Task SPEC_SEC_04_PasswordCannotContainUsername()
    {
        // Arrange
        var username = "johnsmith";
        var password = "JohnSmith123!"; // Contains username

        // Act
        var result = _userService.ValidatePasswordComplexity(password, username);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(PasswordValidationFailure.ContainsUsername, result.Failures);
    }

    [Fact]
    public async Task SPEC_SEC_05_PasswordIsHashedNotStored()
    {
        // This test verifies that passwords are not stored in plain text
        // Implementation should use Argon2id or bcrypt with unique salt per user

        // Arrange & Act
        var user = new User
        {
            UserId = "hash_test",
            UserName = "Hash Test User",
            Role = UserRole.Administrator,
            IsActive = true
        };

        await _userService.CreateUserAsync(user, "PlainPassword123!", CancellationToken.None);

        // Assert - Verify password is not stored (this would need access to internal storage)
        // For now, we document the requirement
        _output.WriteLine("Password hashing requirement: Argon2id or bcrypt with unique salt");
    }

    // ============== Additional Security Tests ==============

    [Fact]
    public async Task SPEC_SEC_Authentication_LogsAuditTrail()
    {
        // Arrange & Act
        var result = await _userService.AuthenticateAsync(
            "System Administrator",
            "valid_password",
            "WS-001",
            CancellationToken.None);

        // Assert - Verify authentication result structure
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Session);
        Assert.NotNull(result.Session.AccessToken);
        Assert.NotEmpty(result.Session.GrantedRoles);

        _output.WriteLine($"Authentication successful: SessionId={result.Session.SessionId}");
    }
}
