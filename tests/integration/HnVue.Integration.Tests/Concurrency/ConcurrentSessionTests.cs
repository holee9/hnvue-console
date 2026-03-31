using FluentAssertions;
using HnVue.Console.Models;
using HnVue.Console.Services;
using Xunit;

namespace HnVue.Integration.Tests.Concurrency;

/// <summary>
/// INT-010: Concurrent Session Management Integration Tests.
/// Validates that the MockUserService handles multiple simultaneous authentication
/// attempts, provides session isolation between users, preserves session validity
/// after peer logouts, and performs thread-safe permission checks and session
/// validation under concurrent access.
/// No Docker required - uses MockUserService exclusively with Task.WhenAll.
/// IEC 62304 Class B/C: Multi-user concurrent access safety verification.
/// </summary>
public sealed class ConcurrentSessionTests
{
    // Mock password accepted by all MockUserService accounts.
    private const string MockPassword = "password123";
    private const string WorkstationId = "WS-INT-010";

    // Users available in MockUserService for concurrent authentication.
    private static readonly string[] ConcurrentUserNames =
    {
        "System Administrator",
        "Dr. House",
        "Technician Johnson",
        "Dr. Quantum",
        "Operator Smith"
    };

    /// <summary>
    /// INT-010-1: Five users must be able to authenticate simultaneously; each
    /// resulting session must have a Success flag of true.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P2")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task ConcurrentAuthentication_AllFiveUsers_Succeed()
    {
        // Arrange: one shared service instance for all concurrent logins.
        var userService = new MockUserService();
        var cancellationToken = CancellationToken.None;

        // Act: launch 5 authentication tasks simultaneously.
        var authTasks = ConcurrentUserNames
            .Select(name => userService.AuthenticateAsync(name, MockPassword, WorkstationId, cancellationToken))
            .ToArray();
        var results = await Task.WhenAll(authTasks);

        // Assert: every authentication must succeed.
        results.Should().HaveCount(ConcurrentUserNames.Length,
            because: "Each concurrent authentication task must resolve to a result");

        for (var i = 0; i < results.Length; i++)
        {
            results[i].Success.Should().BeTrue(
                because: $"User '{ConcurrentUserNames[i]}' must authenticate successfully with valid credentials");
            results[i].Session.Should().NotBeNull(
                because: $"A session must be created for user '{ConcurrentUserNames[i]}'");
            results[i].Session!.SessionId.Should().NotBeNullOrEmpty(
                because: $"Session for '{ConcurrentUserNames[i]}' must have a unique identifier");
        }
    }

    /// <summary>
    /// INT-010-2: Each concurrently created session must be isolated — different SessionId,
    /// different User object, and different AccessToken.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P2")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task ConcurrentSessions_AreIsolated_DistinctSessionIdAndUser()
    {
        // Arrange
        var userService = new MockUserService();
        var cancellationToken = CancellationToken.None;

        // Act
        var authTasks = ConcurrentUserNames
            .Select(name => userService.AuthenticateAsync(name, MockPassword, WorkstationId, cancellationToken))
            .ToArray();
        var results = await Task.WhenAll(authTasks);

        // Assert: SessionIds must all be unique.
        var sessionIds = results.Select(r => r.Session!.SessionId).ToList();
        sessionIds.Should().OnlyHaveUniqueItems(
            because: "Concurrent sessions must each receive a distinct SessionId (UUID-based)");

        // Assert: AccessTokens must all be unique.
        var accessTokens = results.Select(r => r.Session!.AccessToken).ToList();
        accessTokens.Should().OnlyHaveUniqueItems(
            because: "Concurrent sessions must each receive a distinct AccessToken");

        // Assert: each session references the correct user identity.
        for (var i = 0; i < results.Length; i++)
        {
            results[i].Session!.User.UserName.Should().Be(ConcurrentUserNames[i],
                because: $"Session for '{ConcurrentUserNames[i]}' must reference that exact user");
        }
    }

    /// <summary>
    /// INT-010-3: Logging out one user must not invalidate any other active sessions.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P2")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task Logout_OneSession_DoesNotAffect_OtherActiveSessions()
    {
        // Arrange: authenticate three users sequentially to get predictable sessions.
        var userService = new MockUserService();
        var cancellationToken = CancellationToken.None;

        var authA = await userService.AuthenticateAsync("System Administrator", MockPassword, WorkstationId, cancellationToken);
        var authB = await userService.AuthenticateAsync("Dr. House", MockPassword, WorkstationId, cancellationToken);
        var authC = await userService.AuthenticateAsync("Technician Johnson", MockPassword, WorkstationId, cancellationToken);

        authA.Success.Should().BeTrue(because: "User A must authenticate successfully");
        authB.Success.Should().BeTrue(because: "User B must authenticate successfully");
        authC.Success.Should().BeTrue(because: "User C must authenticate successfully");

        // Act: log out user B only.
        var logoutResult = await userService.LogoutAsync(authB.Session!.SessionId, cancellationToken);

        // Assert: user B's session is no longer valid.
        logoutResult.Should().BeTrue(because: "Logout must return true for a valid session");
        var sessionBAfterLogout = await userService.GetCurrentSessionAsync(authB.Session.SessionId, cancellationToken);
        sessionBAfterLogout.Should().BeNull(
            because: "User B's session must be invalidated after logout");

        // Assert: users A and C sessions remain valid and unaffected.
        var sessionAAfterLogout = await userService.GetCurrentSessionAsync(authA.Session!.SessionId, cancellationToken);
        sessionAAfterLogout.Should().NotBeNull(
            because: "User A's session must remain active after User B logs out");

        var sessionCAfterLogout = await userService.GetCurrentSessionAsync(authC.Session!.SessionId, cancellationToken);
        sessionCAfterLogout.Should().NotBeNull(
            because: "User C's session must remain active after User B logs out");

        // Assert: sessions A and C still validate successfully.
        var isAValid = await userService.ValidateSessionAsync(authA.Session.SessionId, cancellationToken);
        isAValid.Should().BeTrue(because: "User A's session must still pass validation");

        var isCValid = await userService.ValidateSessionAsync(authC.Session.SessionId, cancellationToken);
        isCValid.Should().BeTrue(because: "User C's session must still pass validation");
    }

    /// <summary>
    /// INT-010-4: Permission checks for the same user from multiple concurrent threads
    /// must return consistent results — no race condition must alter the outcome.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P2")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task PermissionChecks_AreConsistent_AcrossMultipleConcurrentCalls()
    {
        // Arrange: use a single user identity (Technologist) for all concurrent checks.
        var userService = new MockUserService();
        var cancellationToken = CancellationToken.None;
        const string userId = "tech01";
        const string allowedPermission = "exposure.execute";
        const string deniedPermission = "system.admin";
        const int concurrentChecks = 10;

        // Act: 10 concurrent HasPermissionAsync calls for the allowed permission.
        var allowedTasks = Enumerable.Range(0, concurrentChecks)
            .Select(_ => userService.HasPermissionAsync(userId, allowedPermission, cancellationToken))
            .ToArray();
        var allowedResults = await Task.WhenAll(allowedTasks);

        // Act: 10 concurrent HasPermissionAsync calls for the denied permission.
        var deniedTasks = Enumerable.Range(0, concurrentChecks)
            .Select(_ => userService.HasPermissionAsync(userId, deniedPermission, cancellationToken))
            .ToArray();
        var deniedResults = await Task.WhenAll(deniedTasks);

        // Assert: all allowed-permission checks must return true.
        allowedResults.Should().OnlyContain(result => result == true,
            because: $"All {concurrentChecks} concurrent checks for '{allowedPermission}' must consistently return true");

        // Assert: all denied-permission checks must return false.
        deniedResults.Should().OnlyContain(result => result == false,
            because: $"All {concurrentChecks} concurrent checks for '{deniedPermission}' must consistently return false (RBAC must be thread-safe)");
    }

    /// <summary>
    /// INT-010-5: ValidateSessionAsync must be thread-safe when called simultaneously
    /// from multiple threads for the same active session.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "P2")]
    [Trait("SPEC", "SPEC-INTEGRATION-001")]
    public async Task SessionValidation_IsThreadSafe_ConsistentResultsAcrossConcurrentCalls()
    {
        // Arrange: create a single active session.
        var userService = new MockUserService();
        var cancellationToken = CancellationToken.None;

        var authResult = await userService.AuthenticateAsync(
            "System Administrator", MockPassword, WorkstationId, cancellationToken);
        authResult.Success.Should().BeTrue(because: "Authentication must succeed to create a session");
        var sessionId = authResult.Session!.SessionId;

        // Act: validate the session from 8 concurrent tasks.
        const int concurrentValidations = 8;
        var validationTasks = Enumerable.Range(0, concurrentValidations)
            .Select(_ => userService.ValidateSessionAsync(sessionId, cancellationToken))
            .ToArray();
        var validationResults = await Task.WhenAll(validationTasks);

        // Assert: all validations must return true (the session is active and not expired).
        validationResults.Should().HaveCount(concurrentValidations,
            because: "Each concurrent validation task must resolve");
        validationResults.Should().OnlyContain(result => result == true,
            because: $"All {concurrentValidations} concurrent validations of the same active session must return true (no race conditions in session lookup)");

        // Clean up: log out to release the session.
        await userService.LogoutAsync(sessionId, cancellationToken);

        // Assert: after logout, all subsequent validations must return false.
        var postLogoutTasks = Enumerable.Range(0, 4)
            .Select(_ => userService.ValidateSessionAsync(sessionId, cancellationToken))
            .ToArray();
        var postLogoutResults = await Task.WhenAll(postLogoutTasks);

        postLogoutResults.Should().OnlyContain(result => result == false,
            because: "After logout the session must be invalid in all concurrent checks");
    }
}
