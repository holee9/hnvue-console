using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace HnVue.Console.Services;

/// <summary>
/// Mock AEC service for development.
/// SPEC-UI-001: Mock service for AEC control.
/// </summary>
public class MockAECService : IAECService
{
    private bool _isEnabled = false;

    /// <inheritdoc/>
    public Task EnableAECAsync(CancellationToken ct)
    {
        _isEnabled = true;
        Debug.WriteLine($"[MockAECService] Enabling AEC");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisableAECAsync(CancellationToken ct)
    {
        _isEnabled = false;
        Debug.WriteLine($"[MockAECService] Disabling AEC");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> GetAECStateAsync(CancellationToken ct)
    {
        Debug.WriteLine($"[MockAECService] Getting AEC state: {_isEnabled}");
        return Task.FromResult(_isEnabled);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<bool> SubscribeAECStateChangesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Debug.WriteLine("[MockAECService] Starting AEC state subscription");

        // Simulate periodic state checks
        while (!ct.IsCancellationRequested)
        {
            yield return _isEnabled;
            await Task.Delay(5000, ct);
        }
    }
}
