namespace Frog.Core.Observability;

/// <summary>
/// Opt-in switch for movement baseline timers. Off unless
/// <see cref="EnvironmentVariable"/> is 1 / true / yes / on (case-insensitive).
/// Release and DEBUG builds are silent without the flag.
/// </summary>
public static class MovementMeasureOptions
{
    public const string EnvironmentVariable = "FROG_MOVEMENT_MEASURE";

    public static bool IsEnabledFromEnvironment()
        => IsEnabledValue(Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static bool IsEnabledValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Equals("1", StringComparison.Ordinal)
               || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
