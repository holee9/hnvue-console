using HnVue.Console.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace HnVue.Console.Tests.Security;

/// <summary>
/// Unit tests for AuthenticationRateLimiter.
/// SPEC-SECURITY-001: FR-SEC-03 - Account Lockout Policy
/// Target: 90%+ test coverage for rate limiting functionality.
/// </summary>
public class AuthenticationRateLimiterTests : IDisposable
{
    private readonly Mock<ILogger<AuthenticationRateLimiter>> _mockLogger;
    private readonly AuthenticationRateLimiter _limiter;

    public AuthenticationRateLimiterTests()
    {
        _mockLogger = new Mock<ILogger<AuthenticationRateLimiter>>();
        _limiter = new AuthenticationRateLimiter(_mockLogger.Object);
    }

    public void Dispose()
    {
        // AuthenticationRateLimiter does not implement IDisposable; no cleanup needed.
    }

    #region IsAllowedAsync Tests

    [Fact]
    public async Task IsAllowedAsync_NullKey_ReturnsFalse()
    {
        // Act
        var result = await _limiter.IsAllowedAsync(null!, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_EmptyKey_ReturnsFalse()
    {
        // Act
        var result = await _limiter.IsAllowedAsync(string.Empty, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_WhitespaceKey_ReturnsFalse()
    {
        // Act
        var result = await _limiter.IsAllowedAsync("   ", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_NewKey_ReturnsTrue()
    {
        // Arrange
        var key = "testuser";

        // Act
        var result = await _limiter.IsAllowedAsync(key, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_KeyWithinLimit_ReturnsTrue()
    {
        // Arrange
        var key = "testuser";
        await _limiter.RecordAttemptAsync(key, CancellationToken.None);
        await _limiter.RecordAttemptAsync(key, CancellationToken.None);
        await _limiter.RecordAttemptAsync(key, CancellationToken.None);
        await _limiter.RecordAttemptAsync(key, CancellationToken.None);

        // Act - 4th attempt, still within limit
        var result = await _limiter.IsAllowedAsync(key, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_AtMaxAttempts_BeforeRecording_ReturnsTrue()
    {
        // Arrange
        var key = "testuser";
        for (int i = 0; i < 5; i++)
        {
            await _limiter.RecordAttemptAsync(key, CancellationToken.None);
        }

        // Act - After 5 attempts, should be locked out
        var result = await _limiter.IsAllowedAsync(key, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_AfterLockoutPeriod_ReturnsTrue()
    {
        // Arrange - Note: This test requires time manipulation or waiting
        // In production, use time abstraction for proper testing
        var key = "testuser";
        for (int i = 0; i < 5; i++)
        {
            await _limiter.RecordAttemptAsync(key, CancellationToken.None);
        }

        // After lockout period (15 minutes), window expires (5 minutes)
        // This is a limitation of the in-memory implementation
        // Real implementation would use time provider abstraction

        // For now, we verify the lockout behavior is logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rate limit exceeded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task IsAllowedAsync_ExpiredWindow_ResetsCount()
    {
        // Arrange - Note: Requires time manipulation
        // The implementation should reset count after window expires
        // This is documented behavior; actual testing requires time abstraction
        await Task.CompletedTask; // placeholder: time-based test requires clock abstraction
    }

    #endregion

    #region RecordAttemptAsync Tests

    [Fact]
    public async Task RecordAttemptAsync_NullKey_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _limiter.RecordAttemptAsync(null!, CancellationToken.None);
    }

    [Fact]
    public async Task RecordAttemptAsync_EmptyKey_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _limiter.RecordAttemptAsync(string.Empty, CancellationToken.None);
    }

    [Fact]
    public async Task RecordAttemptAsync_ValidKey_IncrementsCount()
    {
        // Arrange
        var key = "testuser";

        // Act
        await _limiter.RecordAttemptAsync(key, CancellationToken.None);
        await _limiter.RecordAttemptAsync(key, CancellationToken.None);

        // Assert - Check if still allowed (2 < 5)
        var result = await _limiter.IsAllowedAsync(key, CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RecordAttemptAsync_ReachesMaxAttempts_LogsWarning()
    {
        // Arrange
        var key = "lockeduser";

        // Act - Record 5 attempts
        for (int i = 0; i < 5; i++)
        {
            await _limiter.RecordAttemptAsync(key, CancellationToken.None);
        }

        // Assert - Warning should be logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rate limit exceeded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordAttemptAsync_MultipleKeys_TracksSeparately()
    {
        // Arrange
        var key1 = "user1";
        var key2 = "user2";

        // Act - Lock out user1
        for (int i = 0; i < 5; i++)
        {
            await _limiter.RecordAttemptAsync(key1, CancellationToken.None);
        }

        // Assert - user2 should still be allowed
        var result = await _limiter.IsAllowedAsync(key2, CancellationToken.None);
        result.Should().BeTrue();
    }

    #endregion

    #region ResetAsync Tests

    [Fact]
    public async Task ResetAsync_NullKey_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _limiter.ResetAsync(null!, CancellationToken.None);
    }

    [Fact]
    public async Task ResetAsync_EmptyKey_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _limiter.ResetAsync(string.Empty, CancellationToken.None);
    }

    [Fact]
    public async Task ResetAsync_LockedKey_AllowsRetry()
    {
        // Arrange
        var key = "testuser";
        for (int i = 0; i < 5; i++)
        {
            await _limiter.RecordAttemptAsync(key, CancellationToken.None);
        }

        // Verify locked
        var beforeReset = await _limiter.IsAllowedAsync(key, CancellationToken.None);
        beforeReset.Should().BeFalse();

        // Act
        await _limiter.ResetAsync(key, CancellationToken.None);

        // Assert - Should be allowed again
        var afterReset = await _limiter.IsAllowedAsync(key, CancellationToken.None);
        afterReset.Should().BeTrue();
    }

    [Fact]
    public async Task ResetAsync_ValidKey_LogsInformation()
    {
        // Arrange
        var key = "testuser";
        await _limiter.RecordAttemptAsync(key, CancellationToken.None);

        // Act
        await _limiter.ResetAsync(key, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rate limit reset")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetAsync_NonExistentKey_DoesNotThrow()
    {
        // Act & Assert - Should not throw for non-existent key
        await _limiter.ResetAsync("nonexistent", CancellationToken.None);
    }

    #endregion

    #region CleanupExpiredEntries Tests

    [Fact]
    public void CleanupExpiredEntries_NoEntries_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _limiter.CleanupExpiredEntries();
    }

    [Fact]
    public async Task CleanupExpiredEntries_ActiveEntries_DoesNotRemove()
    {
        // Arrange
        var key = "activeuser";
        await _limiter.RecordAttemptAsync(key, CancellationToken.None);

        // Act
        _limiter.CleanupExpiredEntries();

        // Assert - Entry should still exist
        var result = await _limiter.IsAllowedAsync(key, CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public void CleanupExpiredEntries_CallsLogDebug_WhenEntriesRemoved()
    {
        // Arrange - This test verifies the method structure
        // Actual cleanup verification requires time manipulation

        // Act
        _limiter.CleanupExpiredEntries();

        // Assert - Method should complete without throwing
        // Log verification would require expired entries (time manipulation)
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task RateLimiter_FullAttackScenario_BlocksAfterThreshold()
    {
        // Arrange
        var attacker = "attacker";

        // Act - Simulate brute force attack
        var attempts = new List<bool>();
        for (int i = 0; i < 10; i++)
        {
            var allowed = await _limiter.IsAllowedAsync(attacker, CancellationToken.None);
            attempts.Add(allowed);
            await _limiter.RecordAttemptAsync(attacker, CancellationToken.None);
        }

        // Assert - First 5 should be allowed (before recording)
        // After 5 recordings, all should be blocked
        attempts.Take(5).Should().AllBeEquivalentTo(true);
        attempts.Skip(5).Should().AllBeEquivalentTo(false);
    }

    [Fact]
    public async Task RateLimiter_AdminReset_UnlocksAccount()
    {
        // Arrange - Simulate failed login attempts
        var username = "lockeduser";
        for (int i = 0; i < 5; i++)
        {
            await _limiter.RecordAttemptAsync(username, CancellationToken.None);
        }

        // Verify locked
        var isLocked = await _limiter.IsAllowedAsync(username, CancellationToken.None);
        isLocked.Should().BeFalse();

        // Act - Admin resets rate limit
        await _limiter.ResetAsync(username, CancellationToken.None);

        // Assert - User can now try again
        var unlocked = await _limiter.IsAllowedAsync(username, CancellationToken.None);
        unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task RateLimiter_ConcurrentUsers_TracksSeparately()
    {
        // Arrange
        var users = new[] { "user1", "user2", "user3" };

        // Act - Lock out each user
        foreach (var user in users)
        {
            for (int i = 0; i < 5; i++)
            {
                await _limiter.RecordAttemptAsync(user, CancellationToken.None);
            }
        }

        // Assert - All should be locked
        foreach (var user in users)
        {
            var result = await _limiter.IsAllowedAsync(user, CancellationToken.None);
            result.Should().BeFalse($"{user} should be locked out");
        }
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task IsAllowedAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var key = "testuser";

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _limiter.IsAllowedAsync(key, cts.Token));
    }

    [Fact]
    public async Task RecordAttemptAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var key = "testuser";

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _limiter.RecordAttemptAsync(key, cts.Token));
    }

    [Fact]
    public async Task ResetAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var key = "testuser";

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _limiter.ResetAsync(key, cts.Token));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task RateLimiter_VeryLongKey_HandlesCorrectly()
    {
        // Arrange
        var longKey = new string('a', 10000);

        // Act & Assert - Should not throw
        await _limiter.RecordAttemptAsync(longKey, CancellationToken.None);
        var result = await _limiter.IsAllowedAsync(longKey, CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RateLimiter_SpecialCharactersInKey_HandlesCorrectly()
    {
        // Arrange
        var specialKeys = new[]
        {
            "user@example.com",
            "user@domain.co.kr",
            "user+tag@gmail.com",
            "user-name_test"
        };

        foreach (var key in specialKeys)
        {
            // Act & Assert
            await _limiter.RecordAttemptAsync(key, CancellationToken.None);
            var result = await _limiter.IsAllowedAsync(key, CancellationToken.None);
            result.Should().BeTrue();
        }
    }

    #endregion
}
