using FlaUI.Core.AutomationElements;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// Async polling wait utilities for E2E tests.
/// Supports env-var timeout override (HNVUE_E2E_TIMEOUT_MS) for slow CI/debug scenarios.
/// On timeout, WaitForElementAsync automatically dumps the UIAutomation tree.
/// </summary>
public static class WaitHelper
{
    /// <summary>
    /// Reads HNVUE_E2E_TIMEOUT_MS to override the default timeout (opt-in).
    /// Returns <paramref name="defaultMs"/> if the env var is absent or invalid.
    /// </summary>
    private static int ResolveTimeout(int defaultMs)
    {
        var raw = Environment.GetEnvironmentVariable("HNVUE_E2E_TIMEOUT_MS");
        if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out var overrideMs) && overrideMs > 0)
            return overrideMs;
        return defaultMs;
    }

    /// <summary>
    /// Waits until <paramref name="condition"/> is true or timeout expires.
    /// Logs progress every 10 attempts.
    /// </summary>
    public static async Task<bool> WaitUntilAsync(
        Func<bool> condition,
        int timeoutMs = 5000,
        int pollIntervalMs = 200,
        string description = "condition")
    {
        var resolvedTimeout = ResolveTimeout(timeoutMs);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int attempt = 0;
        while (sw.ElapsedMilliseconds < resolvedTimeout)
        {
            if (condition()) return true;
            attempt++;
            if (attempt % 10 == 0)
                System.Diagnostics.Trace.WriteLine(
                    $"[WaitHelper] '{description}' attempt {attempt}, elapsed {sw.ElapsedMilliseconds}ms");
            await Task.Delay(pollIntervalMs);
        }
        System.Diagnostics.Trace.WriteLine(
            $"[WaitHelper] '{description}' timed out after {resolvedTimeout}ms ({attempt} attempts)");
        return false;
    }

    /// <summary>
    /// Waits for an element found by <paramref name="finder"/> to become non-null.
    /// On timeout, dumps the UIAutomation tree for diagnostics.
    /// </summary>
    public static async Task<AutomationElement?> WaitForElementAsync(
        AutomationElement root,
        Func<AutomationElement?> finder,
        int timeoutMs = 10000,
        int pollIntervalMs = 200,
        string description = "element")
    {
        var resolvedTimeout = ResolveTimeout(timeoutMs);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int attempt = 0;
        while (sw.ElapsedMilliseconds < resolvedTimeout)
        {
            var el = finder();
            if (el != null) return el;
            attempt++;
            if (attempt % 10 == 0)
                System.Diagnostics.Trace.WriteLine(
                    $"[WaitHelper] '{description}' attempt {attempt}, elapsed {sw.ElapsedMilliseconds}ms");
            await Task.Delay(pollIntervalMs);
        }
        System.Diagnostics.Trace.WriteLine(
            $"[WaitHelper] '{description}' timed out after {resolvedTimeout}ms.\n{TreeDumper.Dump(root)}");
        return null;
    }

    /// <summary>Waits for a specified duration.</summary>
    public static Task DelayAsync(int ms = 500) => Task.Delay(ms);
}
