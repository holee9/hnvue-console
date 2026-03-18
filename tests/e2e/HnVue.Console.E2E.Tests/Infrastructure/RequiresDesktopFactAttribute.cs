using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// xUnit Fact that auto-skips in non-interactive (CI) environments.
/// Requires a real desktop session for FlaUI/UIAutomation to work.
/// Prevents false CI failures when FlaUI cannot connect to a WPF process.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresDesktopFactAttribute : FactAttribute
{
    public RequiresDesktopFactAttribute()
    {
        if (!EnvironmentDetector.IsInteractiveDesktop())
            Skip = $"Requires interactive desktop session (FlaUI UIAutomation unavailable in CI). " +
                   $"Environment: {EnvironmentDetector.GetEnvironmentSummary()}";
    }
}
