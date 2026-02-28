using FluentAssertions;
using FluentAssertions.Execution;

namespace HnVue.Dose.Tests.TestHelpers;

/// <summary>
/// Custom FluentAssertions extensions for dose-related testing.
/// </summary>
public static class AssertionExtensions
{
    /// <summary>
    /// Asserts a decimal value is within a specified percentage tolerance of an expected value.
    /// Used for DAP calculation accuracy validation per SPEC-DOSE-001 NFR-DOSE-03.
    /// </summary>
    public static void BeApproximatelyPercent(
        this decimal actualValue,
        decimal expectedValue,
        decimal tolerancePercent,
        string because = "",
        params object[] becauseArgs)
    {
        var difference = Math.Abs(actualValue - expectedValue);
        var tolerance = expectedValue * tolerancePercent / 100m;

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(difference <= tolerance)
            .FailWith($"Expected {actualValue} to be within {tolerancePercent}% of {expectedValue} (±{tolerance}), but difference was {difference}.");
    }

    /// <summary>
    /// Asserts a DateTime is within a specified number of milliseconds of an expected time.
    /// Used for timestamp validation in audit trail and dose records.
    /// </summary>
    public static void BeWithinMilliseconds(
        this DateTime actualTime,
        DateTime expectedTime,
        int toleranceMilliseconds,
        string because = "",
        params object[] becauseArgs)
    {
        var difference = Math.Abs((actualTime - expectedTime).TotalMilliseconds);

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(difference <= toleranceMilliseconds)
            .FailWith($"Expected {actualTime} to be within {toleranceMilliseconds}ms of {expectedTime}, but difference was {difference}ms.");
    }
}
