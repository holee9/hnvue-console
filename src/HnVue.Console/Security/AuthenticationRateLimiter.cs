using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HnVue.Console.Security;

/// <summary>
/// Rate limiter interface for protecting against brute force attacks.
/// SPEC-SECURITY-001: FR-SEC-15 - Rate Limiting
/// OWASP A07:2021 - Authentication Failures
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Checks if an action is allowed for the given key
    /// </summary>
    /// <param name="key">Rate limiter key (e.g., username, IP address)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if allowed, false if rate limit exceeded</returns>
    Task<bool> IsAllowedAsync(string key, CancellationToken ct);

    /// <summary>
    /// Records an attempt for the given key
    /// </summary>
    /// <param name="key">Rate limiter key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task RecordAttemptAsync(string key, CancellationToken ct);

    /// <summary>
    /// Resets the rate limit for the given key
    /// </summary>
    /// <param name="key">Rate limiter key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task ResetAsync(string key, CancellationToken ct);
}

/// <summary>
/// Rate limit entry for tracking attempts.
/// </summary>
internal sealed record RateLimitEntry
{
    public int AttemptCount { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}

/// <summary>
/// Authentication rate limiter to prevent brute force attacks.
/// SPEC-SECURITY-001: FR-SEC-15 - Rate Limiting
/// Compliance: IEC 6234 6.3.2, HIPAA 164.308(a)(5)
/// </summary>
public class AuthenticationRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new();
    private readonly ILogger<AuthenticationRateLimiter> _logger;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan WindowDuration = TimeSpan.FromMinutes(5);

    public AuthenticationRateLimiter(ILogger<AuthenticationRateLimiter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<bool> IsAllowedAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(false);
        }

        var entry = _entries.GetOrAdd(key, _ => new RateLimitEntry
        {
            AttemptCount = 0,
            WindowStart = DateTimeOffset.UtcNow
        });

        // Check if currently locked out
        if (entry.LockedUntil.HasValue && entry.LockedUntil.Value > DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Rate limit exceeded for key {Key}, locked until {LockedUntil}",
                key, entry.LockedUntil.Value);
            return Task.FromResult(false);
        }

        // Check if window has expired and reset
        if (entry.WindowStart < DateTimeOffset.UtcNow - WindowDuration)
        {
            entry.AttemptCount = 0;
            entry.WindowStart = DateTimeOffset.UtcNow;
            entry.LockedUntil = null;
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task RecordAttemptAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.CompletedTask;
        }

        var entry = _entries.AddOrUpdate(
            key,
            _ => new RateLimitEntry
            {
                AttemptCount = 1,
                WindowStart = DateTimeOffset.UtcNow
            },
            (_, existing) =>
            {
                // Reset window if expired
                if (existing.WindowStart < DateTimeOffset.UtcNow - WindowDuration)
                {
                    return new RateLimitEntry
                    {
                        AttemptCount = 1,
                        WindowStart = DateTimeOffset.UtcNow
                    };
                }

                var newCount = existing.AttemptCount + 1;
                return new RateLimitEntry
                {
                    AttemptCount = newCount,
                    WindowStart = existing.WindowStart,
                    LockedUntil = newCount >= MaxAttempts ? DateTimeOffset.UtcNow + LockoutDuration : null
                };
            });

        if (entry.AttemptCount >= MaxAttempts)
        {
            _logger.LogWarning("Rate limit exceeded for key {Key} after {Count} attempts. Locked until {LockedUntil}",
                key, entry.AttemptCount, entry.LockedUntil);
        }
        else
        {
            _logger.LogDebug("Recorded attempt {Count}/{Max} for key {Key}",
                entry.AttemptCount, MaxAttempts, key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResetAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.CompletedTask;
        }

        _entries.TryRemove(key, out _);
        _logger.LogInformation("Rate limit reset for key {Key}", key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up expired entries to prevent memory leaks.
    /// </summary>
    public void CleanupExpiredEntries()
    {
        var cutoff = DateTimeOffset.UtcNow - LockoutDuration;
        var keysToRemove = _entries
            .Where(kvp => kvp.Value.WindowStart < cutoff &&
                         (!kvp.Value.LockedUntil.HasValue || kvp.Value.LockedUntil.Value < DateTimeOffset.UtcNow))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _entries.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired rate limit entries", keysToRemove.Count);
        }
    }
}
